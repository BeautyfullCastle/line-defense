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
        private const float BotInterval = 0.6f;
        private const float RewardDelay = 1.2f;
        private const float BotIdleRecheck = 0.1f;

        [SerializeField] private WorldArt _worldArt;
        [SerializeField] private UiRoot _ui;

        private Game _game;
        private Bot _bot;
        private ViewState _view;
        private Effects _effects;
        private CameraFit _cameraFit;
        private WorldRenderer _world;
        private WorldLabels _labels;

        private bool _paused;
        private int _speed = 1;
        private bool _auto;
        private float _botT;
        private float _rewardT;
        private float _wallHitAcc, _wallHitT;
        private bool _rewardPending;
        private float _framesPerSecond = 60f;

        private void Awake()
        {
            Application.runInBackground = true;
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
            }
            _cameraFit = new CameraFit(mainCamera);
            _view = new ViewState();
            _effects = new Effects();
            _world = new WorldRenderer(transform, _worldArt);
            _ui.Init();
            _labels = _ui.Labels;
            WireUi();

            // Game 생성자가 NewGame을 이미 호출하므로 화면만 맞춤
            ApplyBalance(BalanceStore.Load(out string warn));
            if (warn != null)
            {
                _ui.Toast.Show(warn, _ui.CanvasWidth);
            }
        }

        private void StartNewGame()
        {
            _game.NewGame();
            ResetView();
        }

        private void ResetView()
        {
            _view.ClearSelection();
            _world.Clear();
            _view.DisplayHalfWidth = _game.HalfWidth;
            _view.DisplayDepth = _game.Depth;
            _effects.Clear();
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

        private void Update()
        {
            float realDeltaTime = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            if (Time.unscaledDeltaTime > 0f)
            {
                _framesPerSecond = Mathf.Lerp(_framesPerSecond, 1f / Time.unscaledDeltaTime, 0.1f);
            }

            _ui.UpdateLayout();
            HandleKeys();

            if (_auto && _ui.Dialogue.Blocking)
            {
                _ui.Dialogue.Skip();
            }

            float simulatedDeltaTime = Simulate(realDeltaTime);

            if (_ui.Reward.IsOpen && _game.Phase != Phase.Reward)
            {
                _ui.Reward.Hide();
            }

            TryShowReward();
            // 자동 플레이 간격은 프레임이 느려도 실제 시간을 따르도록 자르지 않은 델타를 씀
            AutoAct(Time.unscaledDeltaTime);
            AutoPickReward(Time.unscaledDeltaTime);
            if (_game.Over && !_ui.Over.IsOpen)
            {
                _ui.Over.Show(_game, _ui.CanvasWidth);
            }

            _effects.Update(simulatedDeltaTime);
            _ui.Dialogue.Tick(realDeltaTime);
            _ui.Config.Tick(realDeltaTime);
            _ui.Toast.Tick(realDeltaTime);
            _ui.RewardBanner.Tick(realDeltaTime);
            TickWallHitPop(Time.unscaledDeltaTime);
            _ui.Fly.Tick(Time.unscaledDeltaTime);

            float k = Mathf.Min(1f, realDeltaTime * 2.2f);
            _view.DisplayHalfWidth += (_game.HalfWidth - _view.DisplayHalfWidth) * k;
            _view.DisplayDepth += (_game.Depth - _view.DisplayDepth) * k;
            _cameraFit.Update(_view.DisplayHalfWidth, _view.DisplayDepth, _ui.HeaderPixels, _ui.FooterPixels);

            HandlePointer();

            _world.Render(_game, _view, _cameraFit, _effects, _labels);
            _ui.Hud.Refresh(_game, _paused, _speed, _ui.Fly.Pending(FlyTarget.Gold));
            _ui.Hud.SetCheatOn(_ui.Cheat.Visible);
            _ui.Footer.Refresh(_game, _view);
            _ui.Cheat.Refresh(_game, _speed, _auto, _framesPerSecond);
        }

        private void LateUpdate()
        {
            // 클릭한 버튼이 선택 상태로 남아 Space/Enter에 다시 눌리는 것을 막음
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        private bool IsBlocked() => _ui.Help.IsOpen || _ui.Dialogue.Blocking || _game.Over || _game.Phase == Phase.Reward || _rewardPending;

        private float Simulate(float realDeltaTime)
        {
            if (_paused || IsBlocked())
            {
                return 0f;
            }

            float simulatedDeltaTime = 0f;
            for (int i = 0; i < _speed; i++)
            {
                _game.Step(realDeltaTime);
                simulatedDeltaTime += realDeltaTime;
                ProcessEvents();
                if (IsBlocked())
                {
                    break;
                }
            }
            return simulatedDeltaTime;
        }

        // 자동 플레이도 플레이어 입력이 받아지는 동안에만, 실제 시간 BotInterval마다 명령 하나씩 수행함
        // 배속과 무관하게 화면에서 봇이 무엇을 하는지 따라볼 수 있음
        private void AutoAct(float realDeltaTime)
        {
            if (!_auto || _paused || IsBlocked())
            {
                return;
            }

            _botT += realDeltaTime;
            if (_botT < BotInterval)
            {
                return;
            }
            // 할 일이 없으면 BotIdleRecheck 뒤에 다시 봄
            _botT = _bot.Act(1) > 0 ? 0f : BotInterval - BotIdleRecheck;
            ProcessEvents();
        }

        // 봇도 플레이어처럼 보상 창이 열린 뒤 같은 버튼 경로로만 보상을 고름. 후보를 볼 시간을 둠
        private void AutoPickReward(float realDeltaTime)
        {
            if (!_auto || !_ui.Reward.IsOpen || _ui.Help.IsOpen || _ui.Dialogue.Blocking)
            {
                _rewardT = 0f;
                return;
            }
            _rewardT += realDeltaTime;
            if (_rewardT < RewardDelay)
            {
                return;
            }

            _rewardT = 0f;
            string key = _bot.ChooseReward();
            if (key != null)
            {
                _ui.Reward.OnPick(key);
            }
        }

        private void ProcessEvents()
        {
            foreach (var gameEvent in _game.DrainEvents())
            {
                switch (gameEvent.Type)
                {
                    case GameEventType.Shot:
                    case GameEventType.Burst:
                    case GameEventType.Float:
                        _effects.Add(gameEvent);
                        break;
                    case GameEventType.Kill:
                        // 처치 골드는 동전이 몬스터 자리에서 HUD 골드 칸으로 날아가 들어감
                        if (gameEvent.Amount > 0f)
                        {
                            _ui.Fly.Fly(_cameraFit.WorldToScreen(gameEvent.From.X, gameEvent.From.Y), FlyTarget.Gold, "gold", Palette.Food, Mathf.RoundToInt(gameEvent.Amount), null, 0.1f, 16f);
                        }

                        break;
                    case GameEventType.WallHit:
                        _effects.Add(gameEvent);
                        _ui.Hud.WallHit();
                        _wallHitAcc += gameEvent.Amount;
                        break;
                    case GameEventType.Bark:
                        if (!_ui.Dialogue.Blocking)
                        {
                            _ui.Dialogue.Say(gameEvent.Lines, false);
                        }

                        break;
                    case GameEventType.Say:
                        if (gameEvent.Block)
                        {
                            _ui.Dialogue.Say(gameEvent.Lines, true);
                        }
                        else if (!_ui.Dialogue.Blocking)
                        {
                            _ui.Dialogue.Say(gameEvent.Lines, false);
                        }

                        break;
                    case GameEventType.Toast:
                        _ui.Toast.Show(gameEvent.Text, _ui.CanvasWidth);
                        break;
                    case GameEventType.RewardReady:
                        _rewardPending = true;
                        break;
                }
            }
            TryShowReward();
        }

        // 차단 대사가 끝난 뒤에 보상 오버레이를 연다
        private void TryShowReward()
        {
            if (!_rewardPending)
            {
                return;
            }

            if (_game.Phase != Phase.Reward)
            {
                _rewardPending = false;
                return;
            }
            if (_ui.Dialogue.Blocking || _ui.Reward.IsOpen)
            {
                return;
            }

            _rewardPending = false;
            _ui.Reward.Show(_game, _ui.CanvasWidth);
        }

        private void WireUi()
        {
            _ui.Hud.OnHelp = () => _ui.Help.Show(_game.Balance, _ui.CanvasWidth, _ui.CanvasHeight);
            _ui.Hud.OnPause = () => _paused = !_paused;
            _ui.Hud.OnSpeed = () => _speed = _speed == 1 ? 2 : _speed == 2 ? 3 : 1;
            _ui.Hud.OnCheat = _ui.Cheat.Toggle;
            _ui.Help.OnClose = _ui.Help.Hide;
            _ui.Over.OnRestart = StartNewGame;
            _ui.Reward.OnPick = key =>
            {
                // 설명에 적용 전 수치가 들어가는 보급이 있어 고르기 전에 문구를 읽어 둠
                bool valid = _game.Phase == Phase.Reward && _game.RewardChoices.Contains(key);
                string icon = valid ? _game.RewardIcon(key) : null, title = valid ? _game.RewardTitle(key) : null, desc = valid ? _game.RewardDesc(key) : null;
                var before = Snapshot();
                _game.PickReward(key);
                _ui.Reward.Hide();
                if (valid)
                {
                    _ui.RewardBanner.Show(icon, title, desc, _auto, _ui.CanvasWidth);
                    FlyReward(key, icon, before, Snapshot());
                }
                ProcessEvents();
            };
            _ui.Footer.OnBuild = SetMode;
            WireCheats();
        }

        // ---------- 획득 연출 ----------

        private struct Stock
        {
            public int Stable, Fort, Wh, Cart, Gold, Wall;
        }

        private Stock Snapshot() => new Stock
        {
            Stable = _game.StableTokens,
            Fort = _game.FortTokens,
            Wh = _game.WarehouseTokens,
            Cart = _game.CartTokens,
            Gold = Mathf.FloorToInt(_game.Gold),
            Wall = Mathf.CeilToInt(_game.Wall),
        };

        /// <summary>보급으로 늘어난 것마다 배너 아이콘에서 해당 칸으로 아이콘을 날립니다.</summary>
        private void FlyReward(string key, string icon, Stock stockBefore, Stock stockAfter)
        {
            var from = _ui.RewardBanner.IconScreen();
            const float Delay = 0.55f, Gap = 0.09f;
            bool any = false;
            void Many(FlyTarget target, string iconKey, Color color, int count, float size)
            {
                if (count <= 0)
                {
                    return;
                }

                any = true;
                // 개수가 많으면 아이콘 수를 줄이고 한 아이콘에 여러 개를 실음
                int icons = Mathf.Min(count, 8);
                for (int i = 0; i < icons; i++)
                {
                    int part = count / icons + (i < count % icons ? 1 : 0);
                    _ui.Fly.Fly(from, target, iconKey, color, part, i == icons - 1 ? "+" + count : string.Empty, Delay + Gap * i, size);
                }
            }
            Many(FlyTarget.Stable, "stable", Palette.Ink, stockAfter.Stable - stockBefore.Stable, 24f);
            Many(FlyTarget.Fort, "fort", Palette.Ink, stockAfter.Fort - stockBefore.Fort, 24f);
            Many(FlyTarget.Warehouse, "wh", Palette.Ink, stockAfter.Wh - stockBefore.Wh, 24f);
            Many(FlyTarget.Gold, "gold", Palette.Food, stockAfter.Gold - stockBefore.Gold, 18f);
            Many(FlyTarget.Info, "cart", Palette.Ink, stockAfter.Cart - stockBefore.Cart, 24f);
            if (stockAfter.Wall > stockBefore.Wall)
            {
                any = true;
                // 성벽은 도착 전에 숫자를 붙잡아 두지 않고, 도착 때 칸만 튀게 함
                _ui.Fly.Fly(from, FlyTarget.Wall, "wall", Palette.SpeakerAide, 0, "+" + (stockAfter.Wall - stockBefore.Wall), Delay, 24f);
            }
            if (!any)
            {
                _ui.Fly.Fly(from, FlyTarget.Info, icon, Palette.SpeakerAide, 0, UpgradeLabel(key), Delay, 24f);
            }
        }

        private static string UpgradeLabel(string key)
        {
            switch (key)
            {
                case "cartSpd":
                    return "속도 ↑";
                case "cartCap":
                    return "적재 ↑";
                case "whCap":
                    return "보관 ↑";
                case "capStable":
                    return "개조 한도 ↑";
                case "whUpg":
                    return "확장 한도 ↑";
                case "capFort":
                    return "강화 한도 ↑";
                default:
                    return "↑";
            }
        }

        // 성벽 피해 숫자는 여러 몬스터 몫을 모아 HUD에 0.4초마다 한 번씩 띄움
        private void TickWallHitPop(float dt)
        {
            _wallHitT = Mathf.Max(0f, _wallHitT - dt);
            if (_wallHitAcc < 1f || _wallHitT > 0f)
            {
                return;
            }

            int damageToShow = Mathf.FloorToInt(_wallHitAcc);
            _wallHitAcc -= damageToShow;
            _wallHitT = 0.4f;
            _ui.Fly.Punch(FlyTarget.Wall, "-" + damageToShow, Palette.Danger);
        }

        private void SetMode(BuildKind kind)
        {
            int tokenCount = kind == BuildKind.Stable ? _game.StableTokens : kind == BuildKind.Fort ? _game.FortTokens : _game.WarehouseTokens;
            if (tokenCount <= 0 || (_view.HasMode && _view.Mode == kind))
            {
                _view.HasMode = false;
                return;
            }
            _view.HasMode = true;
            _view.Mode = kind;
        }

        private void CancelSelection()
        {
            _view.HasMode = false;
            _view.Dragging = false;
            _view.DragLine = null;
            _view.DragSegment = -1;
            _view.DragTarget = null;
            _ui.Config.Close();
        }

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelSelection();
            }

            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && _ui.Dialogue.Active)
            {
                _ui.Dialogue.Advance();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SetMode(BuildKind.Stable);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SetMode(BuildKind.Fort);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                SetMode(BuildKind.Warehouse);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _paused = !_paused;
            }

            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote))
            {
                _ui.Cheat.Toggle();
            }
        }
    }
}
