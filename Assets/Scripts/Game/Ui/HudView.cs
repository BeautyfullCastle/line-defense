using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>상단 머리말(제목, 웨이브, 골드, 성벽, 버튼)입니다. 프리팹 Prefabs/UI/Hud에 붙습니다.</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] FlowLayout _flow;
        [SerializeField] Text _version, _wave, _phaseL, _phase, _routes, _gold, _wallTxt;
        [SerializeField] RectTransform _barFill;
        [SerializeField] Image _barFillImg;
        [SerializeField] UiButton _bHelp, _bPause, _bSpeed, _bCheat;

        int _key = int.MinValue;
        float _wallFlash;

        /// <summary>머리말 루트입니다.</summary>
        public RectTransform Root => (RectTransform)transform;

        public Action OnHelp, OnPause, OnSpeed, OnCheat;

        void Awake()
        {
            // 배포된 빌드가 어느 버전인지 바로 알 수 있게 제목 옆에 표시
            _version.text = "v" + Application.version;
            var vr = _version.rectTransform;
            vr.sizeDelta = new Vector2(UiFit.Width(_version) + 12f, vr.sizeDelta.y);
            _bHelp.Button.onClick.AddListener(() => OnHelp?.Invoke());
            _bPause.Button.onClick.AddListener(() => OnPause?.Invoke());
            _bSpeed.Button.onClick.AddListener(() => OnSpeed?.Invoke());
            _bCheat.Button.onClick.AddListener(() => OnCheat?.Invoke());
        }

        /// <summary>폭에 맞춰 배치하고 머리말 높이를 돌려줍니다.</summary>
        public float Layout(float canvasW)
        {
            float h = _flow.Layout(canvasW, 16f, 10f, 18f, 6f);
            Root.sizeDelta = new Vector2(0f, h);
            return h;
        }

        /// <summary>골드 숫자가 날아오는 동전을 맞는 칸입니다.</summary>
        public RectTransform GoldRect => _gold.rectTransform;

        /// <summary>성벽 수치 글자 칸입니다.</summary>
        public RectTransform WallRect => _wallTxt.rectTransform;

        /// <summary>성벽이 피해를 받았을 때 막대와 수치를 잠깐 빨갛게 합니다.</summary>
        public void WallHit() => _wallFlash = 0.35f;

        /// <summary>
        /// 수치와 버튼 글자를 갱신합니다. goldPending은 아직 날아오는 중인 골드로, 도착할 때 숫자에 더해집니다.
        /// </summary>
        public void Refresh(Game g, bool paused, int speed, int goldPending = 0)
        {
            float ratio = g.WallMax > 0f ? Mathf.Clamp01(g.Wall / g.WallMax) : 0f;
            var am = new Vector2(ratio, 1f);
            if (_barFill.anchorMax != am) _barFill.anchorMax = am;
            _wallFlash = Mathf.Max(0f, _wallFlash - Time.unscaledDeltaTime);
            bool hurt = _wallFlash > 0f;
            var barCol = hurt || g.Wall < g.WallMax * 0.35f ? Palette.Danger : Palette.Ink;
            if (_barFillImg.color != barCol) _barFillImg.color = barCol;
            var txtCol = hurt ? Palette.Danger : Palette.Ink;
            if (_wallTxt.color != txtCol) _wallTxt.color = txtCol;

            // 표시 값이 바뀐 프레임에만 문자열을 새로 만듦
            float shownGold = Mathf.Max(0f, g.Gold - goldPending);
            int key = HashCode.Combine(g.Wave, g.Routes.Count, shownGold, g.Phase, Mathf.CeilToInt(g.Count), g.Mons.Count + g.QueueCount,
                HashCode.Combine(Mathf.CeilToInt(g.Wall), Mathf.RoundToInt(g.WallMax), paused, speed));
            if (key == _key) return;
            _key = key;
            Set(_wave, g.Wave.ToString());
            Set(_routes, g.Routes.Count.ToString());
            Set(_gold, Fmt(shownGold));
            if (g.Phase == Phase.Countdown)
            {
                Set(_phaseL, "다음 침공");
                int s = Mathf.CeilToInt(g.Count);
                Set(_phase, $"{s / 60}:{s % 60:00}");
            }
            else if (g.Phase == Phase.Wave)
            {
                Set(_phaseL, "침공 중");
                Set(_phase, $"몬스터 {g.Mons.Count + g.QueueCount}");
            }
            else
            {
                Set(_phaseL, "정비");
                Set(_phase, "-");
            }
            Set(_wallTxt, $"{Mathf.CeilToInt(g.Wall)}/{Mathf.RoundToInt(g.WallMax)}");
            _bPause.SetText(paused ? "계속" : "일시정지");
            _bSpeed.SetText(speed + "×");
        }

        /// <summary>치트 버튼의 선택 표시를 바꿉니다.</summary>
        public void SetCheatOn(bool on) => _bCheat.SetOn(on);

        static void Set(Text t, string s)
        {
            if (t.text != s) t.text = s;
        }

        /// <summary>정수면 소수점 없이, 아니면 소수 4자리까지 줄여 표기합니다.</summary>
        public static string Fmt(float v)
        {
            return Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
