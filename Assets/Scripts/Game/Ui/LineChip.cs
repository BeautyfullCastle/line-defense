using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>하단 꼬리말의 노선 칩 한 개입니다. 풀에서 재사용하며 프리팹 Prefabs/UI/Items/LineChip에 붙습니다.</summary>
    public sealed class LineChip : MonoBehaviour
    {
        const float LabelX = 28f;

        [SerializeField] Image _color;
        [SerializeField] Text _label;
        [SerializeField] UiButton _break, _cart, _reset;

        /// <summary>칩의 RectTransform입니다.</summary>
        public RectTransform Rt => (RectTransform)transform;

        /// <summary>노선 정보로 글자와 버튼을 채우고 칩 폭을 다시 계산합니다.</summary>
        public void Bind(Game g, Line l)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            int stops = l.Nodes.Count - 1, forts = 0;
            foreach (int id in l.Nodes)
            {
                var n = g.NodeById(id);
                if (n != null && n.Type == NodeType.Fort) forts++;
            }
            string text = $"{(l.Loop ? "순환 · " : string.Empty)}정거장 {stops} · 요새 {forts} · 마차 {l.Carts.Count}";
            var col = Palette.LineColor(l.ColorIndex);
            if (_color.color != col) _color.color = col;
            if (_label.text != text) _label.text = text;

            float tw = UiFit.Width(_label) + 4f;
            SetBox(_label.rectTransform, LabelX, tw, 24f);
            float x = LabelX + tw + 6f;
            x = PlaceButton(_break, l.Loop, x);
            x = PlaceButton(_cart, g.TokCart > 0, x);
            x = PlaceButton(_reset, true, x);
            _cart.SetOn(true);

            Rewire(_break, () => g.BreakLoop(l));
            Rewire(_cart, () => g.AddCart(l));
            Rewire(_reset, () => g.ResetLine(l));

            var size = new Vector2(x + 2f, 28f);
            if (Rt.sizeDelta != size) Rt.sizeDelta = size;
        }

        /// <summary>풀로 돌려보냅니다.</summary>
        public void Release()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        static float PlaceButton(UiButton b, bool show, float x)
        {
            if (b.gameObject.activeSelf != show) b.gameObject.SetActive(show);
            if (!show) return x;
            float w = UiFit.Width(b.Label) + 18f;
            SetBox(b.Rt, x, w, 20f);
            return x + w + 4f;
        }

        static void SetBox(RectTransform rt, float x, float w, float h)
        {
            var p = new Vector2(x, 0f);
            if (rt.anchoredPosition != p) rt.anchoredPosition = p;
            var s = new Vector2(w, h);
            if (rt.sizeDelta != s) rt.sizeDelta = s;
        }

        static void Rewire(UiButton b, Action click)
        {
            b.Button.onClick.RemoveAllListeners();
            b.Button.onClick.AddListener(() => click());
        }
    }
}
