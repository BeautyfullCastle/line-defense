using System;
using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>하단 꼬리말(건설 버튼, 마차 정보, 노선 칩, 안내 문구)입니다.</summary>
    public sealed class FooterView
    {
        const string InkHex = "#23261F";

        readonly FlowPanel _flow = new FlowPanel();
        readonly UiButton[] _build = new UiButton[3];
        readonly Text[] _counts = new Text[3];
        readonly Text _cartInfo;
        readonly Text _hint;
        readonly List<RectTransform> _chips = new List<RectTransform>();
        readonly BuildKind[] _kinds = { BuildKind.Stable, BuildKind.Fort, BuildKind.Warehouse };
        readonly Image[] _icons = new Image[3];
        string _hintText = string.Empty;
        int _sig = int.MinValue;
        int _infoKey = int.MinValue;
        float _lastWidth;

        /// <summary>꼬리말 루트입니다.</summary>
        public RectTransform Root { get; }

        /// <summary>건설 버튼을 눌렀을 때 호출됩니다.</summary>
        public Action<BuildKind> OnBuild;

        /// <summary>칩이 바뀌어 다시 배치해야 하는지 여부입니다.</summary>
        public bool NeedsLayout { get; private set; } = true;

        /// <summary>꼬리말 요소를 만듭니다.</summary>
        public FooterView(Transform parent)
        {
            Root = UiFactory.Rect(parent, "Footer");
            Root.anchorMin = new Vector2(0, 0);
            Root.anchorMax = new Vector2(1, 0);
            Root.pivot = new Vector2(0.5f, 0);
            UiFactory.Fill(Root, Palette.Outside, false, true);

            string[] names = { "마굿간", "요새", "창고" };
            string[] icons = { "stable", "fort", "wh" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = UiFactory.Button(Root, names[i], () => OnBuild?.Invoke(_kinds[idx]), 112f, 36f, 14);
                var label = btn.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                UiFactory.Stretch(label.rectTransform, 32f, 0f, 36f, 0f);
                var icon = UiFactory.Rect(btn.Rt.GetChild(0), "Icon");
                var img = icon.gameObject.AddComponent<Image>();
                img.sprite = IconFactory.Get(icons[i]);
                img.color = Palette.Ink;
                img.raycastTarget = false;
                _icons[i] = img;
                icon.anchorMin = icon.anchorMax = new Vector2(0, 0.5f);
                icon.pivot = new Vector2(0, 0.5f);
                icon.anchoredPosition = new Vector2(9f, 0f);
                icon.sizeDelta = new Vector2(18f, 18f);
                var chip = UiFactory.Rect(btn.Rt.GetChild(0), "Count");
                chip.anchorMin = chip.anchorMax = new Vector2(1, 0.5f);
                chip.pivot = new Vector2(1, 0.5f);
                chip.anchoredPosition = new Vector2(-8f, 0f);
                chip.sizeDelta = new Vector2(24f, 20f);
                UiFactory.Fill(chip, Palette.Line, true, false);
                var cnt = UiFactory.Label(chip, "0", 13, Palette.Ink, true, TextAnchor.MiddleCenter);
                UiFactory.Stretch(cnt.rectTransform);
                _build[i] = btn;
                _counts[i] = cnt;
                _flow.Add(btn.Rt);
            }

            _cartInfo = UiFactory.Label(Root, string.Empty, 12, Palette.Muted);
            _cartInfo.rectTransform.sizeDelta = new Vector2(400f, 36f);
            _flow.Add(_cartInfo.rectTransform);

            _hint = UiFactory.Label(Root, string.Empty, 13, Palette.Muted);
            _hint.rectTransform.sizeDelta = new Vector2(400f, 20f);
            _flow.Add(_hint.rectTransform, true);
        }

        /// <summary>폭에 맞춰 칩과 안내 문구까지 배치하고 꼬리말 높이를 돌려줍니다.</summary>
        public float Layout(float canvasW)
        {
            _lastWidth = canvasW;
            NeedsLayout = false;
            var hr = _hint.rectTransform;
            hr.sizeDelta = new Vector2(canvasW - 32f, 20f);
            hr.sizeDelta = new Vector2(canvasW - 32f, Mathf.Max(20f, Mathf.Ceil(_hint.preferredHeight)));
            var cr = _cartInfo.rectTransform;
            cr.sizeDelta = new Vector2(Mathf.Min(420f, canvasW - 32f), canvasW < 520f ? 34f : 20f);
            float h = _flow.Layout(canvasW, 16f, 10f, 8f, 8f);
            Root.sizeDelta = new Vector2(0f, h);
            return h;
        }

        /// <summary>건설 버튼의 권한 개수 칩입니다. 날아오는 권한 아이콘이 여기로 들어갑니다.</summary>
        public RectTransform CountRect(BuildKind k) => (RectTransform)_counts[Array.IndexOf(_kinds, k)].rectTransform.parent;

        /// <summary>마차 적재·속도·창고 보관 안내 글자입니다.</summary>
        public RectTransform InfoRect => _cartInfo.rectTransform;

        /// <summary>날아오는 중이라 아직 숫자에 넣지 않을 권한 수를 돌려줍니다.</summary>
        public Func<BuildKind, int> Pending;

        /// <summary>토큰 수, 선택 상태, 칩, 안내 문구를 갱신합니다.</summary>
        public void Refresh(Game g, ViewState v)
        {
            for (int i = 0; i < 3; i++)
            {
                int tok = Token(g, _kinds[i]);
                _build[i].SetInteractable(tok > 0);
                bool on = v.HasMode && v.Mode == _kinds[i];
                _build[i].SetOn(on);
                _icons[i].color = on ? Palette.Panel : Palette.Ink;
            }

            int pct = Mathf.RoundToInt(100f * (1f + g.B.CART_SPD_UP * g.CartSpdLv));
            int p0 = Pending?.Invoke(BuildKind.Stable) ?? 0, p1 = Pending?.Invoke(BuildKind.Fort) ?? 0, p2 = Pending?.Invoke(BuildKind.Warehouse) ?? 0;
            int key = HashCode.Combine(g.TokStable - p0, g.TokFort - p1, g.TokWarehouse - p2, g.CartCap(), pct, g.WhCap(), g.TokCart);
            if (key != _infoKey)
            {
                _infoKey = key;
                for (int i = 0; i < 3; i++) _counts[i].text = Mathf.Max(0, Token(g, _kinds[i]) - (Pending?.Invoke(_kinds[i]) ?? 0)).ToString();
                string info = $"마차 적재 <color={InkHex}><b>{g.CartCap()}</b></color> · 속도 <color={InkHex}><b>{pct}%</b></color> · 창고 보관 <color={InkHex}><b>{g.WhCap()}</b></color>";
                if (g.TokCart > 0) info += $"  <color={InkHex}><b>배치 대기 {g.TokCart}</b></color>";
                _cartInfo.text = info;
            }

            int sig = Signature(g);
            if (sig != _sig)
            {
                _sig = sig;
                RebuildChips(g);
                NeedsLayout = true;
            }

            string hint = HintFor(g, v);
            if (hint != _hintText)
            {
                _hintText = hint;
                _hint.text = hint;
                NeedsLayout = true;
            }
        }

        static int Signature(Game g)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + g.TokCart;
                foreach (var l in g.Lines)
                {
                    h = h * 31 + l.Id;
                    h = h * 31 + l.Nodes.Count;
                    h = h * 31 + l.Carts.Count;
                    h = h * 31 + (l.Loop ? 1 : 0);
                    foreach (int id in l.Nodes)
                    {
                        var n = g.NodeById(id);
                        if (n != null && n.Type == NodeType.Fort) h = h * 31 + 7;
                    }
                }
                return h;
            }
        }

        static int Token(Game g, BuildKind k) => k == BuildKind.Stable ? g.TokStable : k == BuildKind.Fort ? g.TokFort : g.TokWarehouse;

        static string HintFor(Game g, ViewState v)
        {
            if (v.HasMode)
            {
                switch (v.Mode)
                {
                    case BuildKind.Stable: return "개척지 안 빈 곳을 클릭해 마굿간을 놓으세요. 우클릭이나 Esc로 취소.";
                    case BuildKind.Warehouse: return "노선들이 만날 자리에 창고를 놓으세요. 우클릭이나 Esc로 취소.";
                    default: return "요새는 성벽 위에만 지을 수 있습니다. 성벽 근처를 클릭하면 성벽 선에 붙습니다. 우클릭이나 Esc로 취소.";
                }
            }
            if (g.TokCart > 0) return "마차가 배치를 기다립니다. 아래 노선 칩의 +마차를 누르세요.";
            if (g.Lines.Count == 0) return "① 아래에서 마굿간을 골라 성 안에 놓으세요.";
            bool anyFort = false;
            foreach (var n in g.Nodes) if (n.Type == NodeType.Fort) { anyFort = true; break; }
            if (!anyFort) return "② 요새를 침투로가 닿는 성벽 위에 놓으세요.";
            bool allShort = true, lineHasFort = false;
            foreach (var l in g.Lines)
            {
                if (l.Nodes.Count >= 2) allShort = false;
                foreach (int id in l.Nodes)
                {
                    var n = g.NodeById(id);
                    if (n != null && n.Type == NodeType.Fort) lineHasFort = true;
                }
            }
            if (allShort) return "③ 마굿간에서 농장·대장간·요새로 드래그해 길을 이으세요.";
            if (!lineHasFort) return "노선에 요새가 있어야 마차가 짐을 싣습니다. 길을 요새까지 이으세요.";
            return "선 중간을 끌어 새 정거장에 놓으면 그 사이에 끼워 넣습니다. 길 끝 막대를 반대쪽 끝 정거장에 놓으면 순환 노선이 됩니다.";
        }

        void RebuildChips(Game g)
        {
            // 칩 위치는 힌트 앞에 오도록 flow를 다시 구성
            foreach (var c in _chips) UnityEngine.Object.Destroy(c.gameObject);
            _chips.Clear();
            _flow.Clear();
            for (int i = 0; i < 3; i++) _flow.Add(_build[i].Rt);
            _flow.Add(_cartInfo.rectTransform);
            bool first = true;
            foreach (var l in g.Lines)
            {
                var chip = MakeChip(g, l);
                _flow.Add(chip, first);
                first = false;
                _chips.Add(chip);
            }
            _flow.Add(_hint.rectTransform, true);
        }

        RectTransform MakeChip(Game g, Line l)
        {
            int stops = l.Nodes.Count - 1, forts = 0;
            foreach (int id in l.Nodes)
            {
                var n = g.NodeById(id);
                if (n != null && n.Type == NodeType.Fort) forts++;
            }
            string text = $"{(l.Loop ? "순환 · " : string.Empty)}정거장 {stops} · 요새 {forts} · 마차 {l.Carts.Count}";
            var chip = UiFactory.Card(Root, "Chip", Palette.Panel, Palette.Line);
            var sq = UiFactory.Rect(chip, "Color");
            sq.anchorMin = sq.anchorMax = new Vector2(0, 0.5f);
            sq.pivot = new Vector2(0, 0.5f);
            sq.anchoredPosition = new Vector2(10f, 0f);
            sq.sizeDelta = new Vector2(12f, 12f);
            UiFactory.Fill(sq, Palette.LineColor(l.ColorIndex), true, false);
            var lab = UiFactory.Label(chip, text, 12, Palette.Ink);
            float tw = UiFactory.TextWidth(text, 12, false) + 4f;
            lab.horizontalOverflow = HorizontalWrapMode.Overflow;
            Anchor(lab.rectTransform, 28f, tw);
            float x = 28f + tw + 6f;
            if (l.Loop) x = AddChipButton(chip, "순환 끊기", x, () => g.BreakLoop(l), false);
            if (g.TokCart > 0) x = AddChipButton(chip, "+마차", x, () => g.AddCart(l), true);
            x = AddChipButton(chip, "초기화", x, () => g.ResetLine(l), false);
            chip.sizeDelta = new Vector2(x + 2f, 28f);
            return chip;
        }

        static void Anchor(RectTransform rt, float x, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(w, 24f);
        }

        static float AddChipButton(RectTransform chip, string label, float x, Action click, bool strong)
        {
            float w = UiFactory.TextWidth(label, 11, true) + 18f;
            var b = UiFactory.Button(chip, label, click, w, 20f, 11);
            if (strong) b.SetOn(true);
            Anchor(b.Rt, x, w);
            b.Rt.sizeDelta = new Vector2(w, 20f);
            return x + w + 4f;
        }
    }
}
