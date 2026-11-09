using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>상단 머리말(제목, 웨이브, 골드, 성벽, 버튼)입니다. 프리팹 Prefabs/UI/Hud에 붙습니다.</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private FlowLayout _flow;
        [SerializeField] private Text _version, _wave, _phaseLabel, _phase, _routes, _gold, _wallText;
        [SerializeField] private RectTransform _barFill;
        [SerializeField] private Image _barFillImage;
        [SerializeField] private UiButton _helpButton, _pauseButton, _speedButton, _cheatButton;

        private int _refreshKey = int.MinValue;
        private float _wallFlash;

        /// <summary>머리말 루트입니다.</summary>
        public RectTransform Root => (RectTransform)transform;

        public Action OnHelp, OnPause, OnSpeed, OnCheat;

        private void Awake()
        {
            // 배포된 빌드가 어느 버전인지 바로 알 수 있게 제목 옆에 표시
            _version.text = "v" + Application.version;
            var rectTransform = _version.rectTransform;
            rectTransform.sizeDelta = new Vector2(UiFit.Width(_version) + 12f, rectTransform.sizeDelta.y);
            _helpButton.Button.onClick.AddListener(() => OnHelp?.Invoke());
            _pauseButton.Button.onClick.AddListener(() => OnPause?.Invoke());
            _speedButton.Button.onClick.AddListener(() => OnSpeed?.Invoke());
            _cheatButton.Button.onClick.AddListener(() => OnCheat?.Invoke());
        }

        /// <summary>폭에 맞춰 배치하고 머리말 높이를 돌려줍니다.</summary>
        public float Layout(float canvasWidth)
        {
            float headerHeight = _flow.Layout(canvasWidth, 16f, 10f, 18f, 6f);
            Root.sizeDelta = new Vector2(0f, headerHeight);
            return headerHeight;
        }

        /// <summary>골드 숫자가 날아오는 동전을 맞는 칸입니다.</summary>
        public RectTransform GoldRect => _gold.rectTransform;

        /// <summary>성벽 수치 글자 칸입니다.</summary>
        public RectTransform WallRect => _wallText.rectTransform;

        /// <summary>성벽이 피해를 받았을 때 막대와 수치를 잠깐 빨갛게 합니다.</summary>
        public void WallHit() => _wallFlash = 0.35f;

        /// <summary>
        /// 수치와 버튼 글자를 갱신합니다. goldPending은 아직 날아오는 중인 골드로, 도착할 때 숫자에 더해집니다.
        /// </summary>
        public void Refresh(Game game, bool paused, int speed, int goldPending = 0)
        {
            float ratio = game.WallMax > 0f ? Mathf.Clamp01(game.Wall / game.WallMax) : 0f;
            var anchorMax = new Vector2(ratio, 1f);
            if (_barFill.anchorMax != anchorMax)
            {
                _barFill.anchorMax = anchorMax;
            }

            _wallFlash = Mathf.Max(0f, _wallFlash - Time.unscaledDeltaTime);
            bool hurt = _wallFlash > 0f;
            var barColor = hurt || game.Wall < game.WallMax * 0.35f ? Palette.Danger : Palette.Ink;
            if (_barFillImage.color != barColor)
            {
                _barFillImage.color = barColor;
            }

            var textColor = hurt ? Palette.Danger : Palette.Ink;
            if (_wallText.color != textColor)
            {
                _wallText.color = textColor;
            }

            // 표시 값이 바뀐 프레임에만 문자열을 새로 만듦
            float shownGold = Mathf.Max(0f, game.Gold - goldPending);
            int key = HashCode.Combine(game.Wave, game.Routes.Count, shownGold, game.Phase, Mathf.CeilToInt(game.Countdown), game.Monsters.Count + game.QueueCount,
                HashCode.Combine(Mathf.CeilToInt(game.Wall), Mathf.RoundToInt(game.WallMax), paused, speed));
            if (key == _refreshKey)
            {
                return;
            }

            _refreshKey = key;
            SetText(_wave, game.Wave.ToString());
            SetText(_routes, game.Routes.Count.ToString());
            SetText(_gold, FormatNumber(shownGold));
            if (game.Phase == Phase.Countdown)
            {
                SetText(_phaseLabel, "다음 침공");
                int seconds = Mathf.CeilToInt(game.Countdown);
                SetText(_phase, $"{seconds / 60}:{seconds % 60:00}");
            }
            else if (game.Phase == Phase.Wave)
            {
                SetText(_phaseLabel, "침공 중");
                SetText(_phase, $"몬스터 {game.Monsters.Count + game.QueueCount}");
            }
            else
            {
                SetText(_phaseLabel, "정비");
                SetText(_phase, "-");
            }
            SetText(_wallText, $"{Mathf.CeilToInt(game.Wall)}/{Mathf.RoundToInt(game.WallMax)}");
            _pauseButton.SetText(paused ? "계속" : "일시정지");
            _speedButton.SetText(speed + "×");
        }

        /// <summary>치트 버튼의 선택 표시를 바꿉니다.</summary>
        public void SetCheatOn(bool on) => _cheatButton.SetOn(on);

        private static void SetText(Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        /// <summary>정수면 소수점 없이, 아니면 소수 4자리까지 줄여 표기합니다.</summary>
        public static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString() : value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
