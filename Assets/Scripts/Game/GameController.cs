using LineDefense.Core;
using LineDefense.View.Ui;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LineDefense.View
{
    /// <summary>시뮬레이션 루프, 입력, 모드/드래그 상태, UI 연결을 맡는 최상위 컨트롤러입니다.</summary>
    public sealed partial class GameController : MonoBehaviour
    {
        // 자동 플레이 명령 간격과 보상 고르기 전 대기 시간(실제 초)
        const float BotInterval = 0.6f;
        const float RewardDelay = 1.2f;
        const float BotIdleRecheck = 0.1f;

        Game _g;
        Bot _bot;
        ViewState _v;
        Effects _fx;
        CameraFit _cam;
        WorldRenderer _world;
        WorldLabels _labels;
        UiRoot _ui;

        bool _paused;
        int _speed = 1;
        bool _auto;
        float _botT;
        float _rewardT;
        float _wallHitAcc, _wallHitT;
        bool _rewardPending;
        float _fps = 60f;

        void Awake()
        {
            Application.runInBackground = true;
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            _cam = new CameraFit(cam);
            _v = new ViewState();
            _fx = new Effects();
            _world = new WorldRenderer(transform);
            _ui = new UiRoot(transform);
            _labels = new WorldLabels(_ui.LabelCanvas);
            WireUi();

            // Game 생성자가 NewGame을 이미 호출하므로 화면만 맞춤
            ApplyBalance(BalanceStore.Load(out string warn));
            if (warn != null) _ui.Toast.Show(warn, _ui.CanvasW);
        }

        void StartNewGame()
        {
            _g.NewGame();
            ResetView();
        }

        void ResetView()
        {
            _v.ClearSelection();
            _v.HWd = _g.HW;
            _v.Dd = _g.D;
            _fx.Clear();
            _ui.Dialogue.Clear();
            _ui.Reward.Hide();
            _ui.RewardBanner.Hide();
            _ui.Fly.Clear();
            _wallHitAcc = 0f;
            _ui.Over.Hide();
            _ui.Help.Hide();
            _ui.Config.Close();
            _rewardPending = false;
            _botT = 0f;
            _rewardT = 0f;
            ProcessEvents();
        }

        void Update()
        {
            float rdt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            if (Time.unscaledDeltaTime > 0f) _fps = Mathf.Lerp(_fps, 1f / Time.unscaledDeltaTime, 0.1f);

            _ui.UpdateLayout();
            HandleKeys();

            if (_auto && _ui.Dialogue.Blocking) _ui.Dialogue.Skip();
            float simDt = Simulate(rdt);

            if (_ui.Reward.IsOpen && _g.Phase != Phase.Reward) _ui.Reward.Hide();
            TryShowReward();
            // 자동 플레이 간격은 프레임이 느려도 실제 시간을 따르도록 자르지 않은 델타를 씀
            AutoAct(Time.unscaledDeltaTime);
            AutoPickReward(Time.unscaledDeltaTime);
            if (_g.Over && !_ui.Over.IsOpen) _ui.Over.Show(_g, _ui.CanvasW);

            _fx.Update(simDt);
            _ui.Dialogue.Tick(rdt);
            _ui.Config.Tick(rdt);
            _ui.Toast.Tick(rdt);
            _ui.RewardBanner.Tick(rdt);
            TickWallHitPop(Time.unscaledDeltaTime);
            _ui.Fly.Tick(Time.unscaledDeltaTime);

            float k = Mathf.Min(1f, rdt * 2.2f);
            _v.HWd += (_g.HW - _v.HWd) * k;
            _v.Dd += (_g.D - _v.Dd) * k;
            _cam.Update(_v.HWd, _v.Dd, _ui.HeaderPx, _ui.FooterPx);

            HandlePointer();

            _world.Render(_g, _v, _cam, _fx, _labels);
            _ui.Hud.Refresh(_g, _paused, _speed, _ui.Fly.Pending(FlyTarget.Gold));
            _ui.Hud.SetCheatOn(_ui.Cheat.Visible);
            _ui.Footer.Refresh(_g, _v);
            _ui.Cheat.Refresh(_g, _speed, _auto, _fps);
        }

        void LateUpdate()
        {
            // 클릭한 버튼이 선택 상태로 남아 Space/Enter에 다시 눌리는 것을 막음
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
        }

        bool IsBlocked() => _ui.Help.IsOpen || _ui.Dialogue.Blocking || _g.Over || _g.Phase == Phase.Reward || _rewardPending;

        float Simulate(float rdt)
        {
            if (_paused || IsBlocked()) return 0f;
            float simDt = 0f;
            for (int i = 0; i < _speed; i++)
            {
                _g.Step(rdt);
                simDt += rdt;
                ProcessEvents();
                if (IsBlocked()) break;
            }
            return simDt;
        }

        // 자동 플레이도 플레이어 입력이 받아지는 동안에만, 실제 시간 BotInterval마다 명령 하나씩 수행함
        // 배속과 무관하게 화면에서 봇이 무엇을 하는지 따라볼 수 있음
        void AutoAct(float rdt)
        {
            if (!_auto || _paused || IsBlocked()) return;
            _botT += rdt;
            if (_botT < BotInterval) return;
            // 할 일이 없으면 BotIdleRecheck 뒤에 다시 봄
            _botT = _bot.Act(1) > 0 ? 0f : BotInterval - BotIdleRecheck;
            ProcessEvents();
        }

        // 봇도 플레이어처럼 보상 창이 열린 뒤 같은 버튼 경로로만 보상을 고름. 후보를 볼 시간을 둠
        void AutoPickReward(float rdt)
        {
            if (!_auto || !_ui.Reward.IsOpen || _ui.Help.IsOpen || _ui.Dialogue.Blocking) { _rewardT = 0f; return; }
            _rewardT += rdt;
            if (_rewardT < RewardDelay) return;
            _rewardT = 0f;
            string key = _bot.ChooseReward();
            if (key != null) _ui.Reward.OnPick(key);
        }

        void ProcessEvents()
        {
            foreach (var e in _g.DrainEvents())
            {
                switch (e.Type)
                {
                    case GameEventType.Shot:
                    case GameEventType.Burst:
                    case GameEventType.Float:
                        _fx.Add(e);
                        break;
                    case GameEventType.Kill:
                        // 처치 골드는 동전이 몬스터 자리에서 HUD 골드 칸으로 날아가 들어감
                        if (e.Amount > 0f)
                            _ui.Fly.Fly(_cam.WorldToScreen(e.A.x, e.A.y), FlyTarget.Gold, "gold", Palette.Food, Mathf.RoundToInt(e.Amount), null, 0.1f, 16f);
                        break;
                    case GameEventType.WallHit:
                        _fx.Add(e);
                        _ui.Hud.WallHit();
                        _wallHitAcc += e.Amount;
                        break;
                    case GameEventType.Bark:
                        if (!_ui.Dialogue.Blocking) _ui.Dialogue.Say(e.Lines, false);
                        break;
                    case GameEventType.Say:
                        if (e.Block) _ui.Dialogue.Say(e.Lines, true);
                        else if (!_ui.Dialogue.Blocking) _ui.Dialogue.Say(e.Lines, false);
                        break;
                    case GameEventType.Toast:
                        _ui.Toast.Show(e.Text, _ui.CanvasW);
                        break;
                    case GameEventType.RewardReady:
                        _rewardPending = true;
                        break;
                }
            }
            TryShowReward();
        }

        // 차단 대사가 끝난 뒤에 보상 오버레이를 연다
        void TryShowReward()
        {
            if (!_rewardPending) return;
            if (_g.Phase != Phase.Reward) { _rewardPending = false; return; }
            if (_ui.Dialogue.Blocking || _ui.Reward.IsOpen) return;
            _rewardPending = false;
            _ui.Reward.Show(_g, _ui.CanvasW);
        }

        void WireUi()
        {
            _ui.Hud.OnHelp = () => _ui.Help.Show(_g.B, _ui.CanvasW, _ui.CanvasH);
            _ui.Hud.OnPause = () => _paused = !_paused;
            _ui.Hud.OnSpeed = () => _speed = _speed == 1 ? 2 : _speed == 2 ? 3 : 1;
            _ui.Hud.OnCheat = _ui.Cheat.Toggle;
            _ui.Help.OnClose = _ui.Help.Hide;
            _ui.Over.OnRestart = StartNewGame;
            _ui.Reward.OnPick = key =>
            {
                // 설명에 적용 전 수치가 들어가는 보급이 있어 고르기 전에 문구를 읽어 둠
                bool valid = _g.Phase == Phase.Reward && _g.RewardChoices.Contains(key);
                string icon = valid ? _g.RewardIcon(key) : null, title = valid ? _g.RewardTitle(key) : null, desc = valid ? _g.RewardDesc(key) : null;
                var before = Snapshot();
                _g.PickReward(key);
                _ui.Reward.Hide();
                if (valid)
                {
                    _ui.RewardBanner.Show(icon, title, desc, _auto, _ui.CanvasW);
                    FlyReward(key, icon, before, Snapshot());
                }
                ProcessEvents();
            };
            _ui.Footer.OnBuild = SetMode;
            WireCheats();
        }

        // ---------- 획득 연출 ----------

        struct Stock { public int Stable, Fort, Wh, Cart, Gold, Wall; }

        Stock Snapshot() => new Stock
        {
            Stable = _g.TokStable, Fort = _g.TokFort, Wh = _g.TokWarehouse, Cart = _g.TokCart,
            Gold = Mathf.FloorToInt(_g.Gold), Wall = Mathf.CeilToInt(_g.Wall),
        };

        /// <summary>보급으로 늘어난 것마다 배너 아이콘에서 해당 칸으로 아이콘을 날립니다.</summary>
        void FlyReward(string key, string icon, Stock a, Stock b)
        {
            var from = _ui.RewardBanner.IconScreen();
            const float delay = 0.55f, gap = 0.09f;
            bool any = false;
            void Many(FlyTarget t, string ic, Color col, int n, float size)
            {
                if (n <= 0) return;
                any = true;
                // 개수가 많으면 아이콘 수를 줄이고 한 아이콘에 여러 개를 실음
                int icons = Mathf.Min(n, 8);
                for (int i = 0; i < icons; i++)
                {
                    int part = n / icons + (i < n % icons ? 1 : 0);
                    _ui.Fly.Fly(from, t, ic, col, part, i == icons - 1 ? "+" + n : string.Empty, delay + gap * i, size);
                }
            }
            Many(FlyTarget.Stable, "stable", Palette.Ink, b.Stable - a.Stable, 24f);
            Many(FlyTarget.Fort, "fort", Palette.Ink, b.Fort - a.Fort, 24f);
            Many(FlyTarget.Warehouse, "wh", Palette.Ink, b.Wh - a.Wh, 24f);
            Many(FlyTarget.Gold, "gold", Palette.Food, b.Gold - a.Gold, 18f);
            Many(FlyTarget.Info, "cart", Palette.Ink, b.Cart - a.Cart, 24f);
            if (b.Wall > a.Wall)
            {
                any = true;
                // 성벽은 도착 전에 숫자를 붙잡아 두지 않고, 도착 때 칸만 튀게 함
                _ui.Fly.Fly(from, FlyTarget.Wall, "wall", Palette.SpAide, 0, "+" + (b.Wall - a.Wall), delay, 24f);
            }
            if (!any) _ui.Fly.Fly(from, FlyTarget.Info, icon, Palette.SpAide, 0, UpgradeLabel(key), delay, 24f);
        }

        static string UpgradeLabel(string key)
        {
            switch (key)
            {
                case "cartSpd": return "속도 ↑";
                case "cartCap": return "적재 ↑";
                case "whCap": return "보관 ↑";
                case "capStable": return "개조 한도 ↑";
                case "whUpg": return "확장 한도 ↑";
                case "capFort": return "강화 한도 ↑";
                default: return "↑";
            }
        }

        // 성벽 피해 숫자는 여러 몬스터 몫을 모아 HUD에 0.4초마다 한 번씩 띄움
        void TickWallHitPop(float dt)
        {
            _wallHitT = Mathf.Max(0f, _wallHitT - dt);
            if (_wallHitAcc < 1f || _wallHitT > 0f) return;
            int n = Mathf.FloorToInt(_wallHitAcc);
            _wallHitAcc -= n;
            _wallHitT = 0.4f;
            _ui.Fly.Punch(FlyTarget.Wall, "-" + n, Palette.Danger);
        }

        void SetMode(BuildKind k)
        {
            int tok = k == BuildKind.Stable ? _g.TokStable : k == BuildKind.Fort ? _g.TokFort : _g.TokWarehouse;
            if (tok <= 0 || (_v.HasMode && _v.Mode == k)) { _v.HasMode = false; return; }
            _v.HasMode = true;
            _v.Mode = k;
        }

        void CancelSelection()
        {
            _v.HasMode = false;
            _v.Dragging = false;
            _v.DragLine = null;
            _v.DragSeg = -1;
            _v.DragTarget = null;
            _ui.Config.Close();
        }

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CancelSelection();
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && _ui.Dialogue.Active) _ui.Dialogue.Advance();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SetMode(BuildKind.Stable);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SetMode(BuildKind.Fort);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SetMode(BuildKind.Warehouse);
            if (Input.GetKeyDown(KeyCode.Space)) _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote)) _ui.Cheat.Toggle();
        }
    }
}
