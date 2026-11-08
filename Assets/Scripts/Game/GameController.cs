using LineDefense.Core;
using LineDefense.View.Ui;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LineDefense.View
{
    /// <summary>시뮬레이션 루프, 입력, 모드/드래그 상태, UI 연결을 맡는 최상위 컨트롤러입니다.</summary>
    public sealed partial class GameController : MonoBehaviour
    {
        const float BotInterval = 0.25f;

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
            _g = new Game();
            _bot = new Bot(_g);
            ResetView();
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
            _ui.Over.Hide();
            _ui.Help.Hide();
            _ui.Config.Close();
            _rewardPending = false;
            _botT = 0f;
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
            if (_g.Over && !_ui.Over.IsOpen) _ui.Over.Show(_g, _ui.CanvasW);

            _fx.Update(simDt);
            _ui.Dialogue.Tick(rdt);
            _ui.Config.Tick(rdt);
            _ui.Toast.Tick(rdt);

            float k = Mathf.Min(1f, rdt * 2.2f);
            _v.HWd += (_g.HW - _v.HWd) * k;
            _v.Dd += (_g.D - _v.Dd) * k;
            _cam.Update(_v.HWd, _v.Dd, _ui.HeaderPx, _ui.FooterPx);

            HandlePointer();

            _world.Render(_g, _v, _cam, _fx, _labels);
            _ui.Hud.Refresh(_g, _paused, _speed);
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
            if (_g.Phase == Phase.Reward && _auto && !_ui.Dialogue.Blocking)
            {
                _bot.Tick();
                ProcessEvents();
            }
            if (_paused || IsBlocked()) return 0f;
            float simDt = 0f;
            for (int i = 0; i < _speed; i++)
            {
                _g.Step(rdt);
                simDt += rdt;
                if (_auto)
                {
                    _botT += rdt;
                    while (_botT >= BotInterval)
                    {
                        _botT -= BotInterval;
                        _bot.Tick();
                    }
                }
                ProcessEvents();
                if (IsBlocked()) break;
            }
            return simDt;
        }

        void ProcessEvents()
        {
            foreach (var e in _g.DrainEvents())
            {
                switch (e.Type)
                {
                    case GameEventType.Shot:
                    case GameEventType.Kill:
                    case GameEventType.Burst:
                    case GameEventType.Float:
                        _fx.Add(e);
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
            _ui.Hud.OnHelp = () => _ui.Help.Show(_ui.CanvasW, _ui.CanvasH);
            _ui.Hud.OnPause = () => _paused = !_paused;
            _ui.Hud.OnSpeed = () => _speed = _speed == 1 ? 2 : _speed == 2 ? 3 : 1;
            _ui.Hud.OnCheat = _ui.Cheat.Toggle;
            _ui.Help.OnClose = _ui.Help.Hide;
            _ui.Over.OnRestart = StartNewGame;
            _ui.Reward.OnPick = key =>
            {
                _g.PickReward(key);
                _ui.Reward.Hide();
                ProcessEvents();
            };
            _ui.Footer.OnBuild = SetMode;
            WireCheats();
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
