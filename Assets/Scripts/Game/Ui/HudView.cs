using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>상단 머리말(제목, 웨이브, 골드, 성벽, 버튼)입니다.</summary>
    public sealed class HudView
    {
        const float RowH = 30f;

        readonly FlowPanel _flow = new FlowPanel();
        readonly Text _wave, _phaseL, _phase, _routes, _gold, _wallTxt;
        readonly RectTransform _barFill;
        int _key = int.MinValue;
        readonly Image _barFillImg;
        readonly UiButton _bPause, _bSpeed, _bCheat;

        /// <summary>머리말 루트입니다.</summary>
        public RectTransform Root { get; }

        public Action OnHelp, OnPause, OnSpeed, OnCheat;

        /// <summary>머리말 요소를 모두 만듭니다.</summary>
        public HudView(Transform parent)
        {
            Root = UiFactory.Rect(parent, "Header");
            Root.anchorMin = new Vector2(0, 1);
            Root.anchorMax = new Vector2(1, 1);
            Root.pivot = new Vector2(0.5f, 1);
            UiFactory.Fill(Root, Palette.Outside, false, true);

            var title = UiFactory.Label(Root, "요새 노선도", 22, Palette.Ink, true);
            title.rectTransform.sizeDelta = new Vector2(150f, RowH);
            _flow.Add(title.rectTransform);

            _wave = Stat("웨이브", 44f, 40f, out _);
            _phase = Stat("다음 침공", 56f, 78f, out _phaseL);
            _routes = Stat("침투로", 44f, 30f, out _);
            _gold = Stat("골드", 32f, 56f, out _);

            var wall = UiFactory.Rect(Root, "Wall");
            wall.sizeDelta = new Vector2(196f, RowH);
            var wl = UiFactory.Label(wall, "성벽", 11, Palette.Muted, true);
            Place(wl.rectTransform, 0f, 30f);
            var bar = UiFactory.Rect(wall, "Bar");
            Place(bar, 32f, 90f, 8f);
            UiFactory.Fill(bar, Palette.Line, true, false);
            _barFill = UiFactory.Rect(bar, "Fill");
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = Vector2.one;
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
            _barFillImg = UiFactory.Fill(_barFill, Palette.Ink, false, false);
            _wallTxt = UiFactory.Label(wall, "100/100", 16, Palette.Ink, true);
            Place(_wallTxt.rectTransform, 128f, 68f);
            _flow.Add(wall);

            Btn("도움말", 64f, () => OnHelp?.Invoke());
            _bPause = Btn("일시정지", 76f, () => OnPause?.Invoke());
            _bSpeed = Btn("1×", 48f, () => OnSpeed?.Invoke());
            _bCheat = Btn("치트", 52f, () => OnCheat?.Invoke());
        }

        static void Place(RectTransform rt, float x, float w, float h = RowH)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(w, h);
        }

        Text Stat(string label, float labelW, float valueW, out Text labelText)
        {
            var box = UiFactory.Rect(Root, "Stat_" + label);
            box.sizeDelta = new Vector2(labelW + valueW, RowH);
            labelText = UiFactory.Label(box, label, 11, Palette.Muted, true);
            Place(labelText.rectTransform, 0f, labelW);
            var val = UiFactory.Label(box, "0", 16, Palette.Ink, true);
            Place(val.rectTransform, labelW, valueW);
            _flow.Add(box);
            return val;
        }

        UiButton Btn(string label, float w, Action click)
        {
            var b = UiFactory.Button(Root, label, click, w, RowH);
            _flow.Add(b.Rt);
            return b;
        }

        /// <summary>폭에 맞춰 배치하고 머리말 높이를 돌려줍니다.</summary>
        public float Layout(float canvasW)
        {
            float h = _flow.Layout(canvasW, 16f, 10f, 18f, 6f);
            Root.sizeDelta = new Vector2(0f, h);
            return h;
        }

        /// <summary>수치와 버튼 글자를 갱신합니다.</summary>
        public void Refresh(Game g, bool paused, int speed)
        {
            float ratio = g.WallMax > 0f ? Mathf.Clamp01(g.Wall / g.WallMax) : 0f;
            _barFill.anchorMax = new Vector2(ratio, 1f);
            _barFillImg.color = g.Wall < g.WallMax * 0.35f ? Palette.Danger : Palette.Ink;

            // 표시 값이 바뀐 프레임에만 문자열을 새로 만듦
            int key = HashCode.Combine(g.Wave, g.Routes.Count, g.Gold, g.Phase, Mathf.CeilToInt(g.Count), g.Mons.Count + g.QueueCount,
                HashCode.Combine(Mathf.CeilToInt(g.Wall), Mathf.RoundToInt(g.WallMax), paused, speed));
            if (key == _key) return;
            _key = key;
            Set(_wave, g.Wave.ToString());
            Set(_routes, g.Routes.Count.ToString());
            Set(_gold, Fmt(g.Gold));
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
