using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>부모 안에 위에서 아래로 요소를 쌓는 단순 배치기입니다.</summary>
    public sealed class VStack
    {
        readonly RectTransform _parent;
        readonly float _width;
        readonly float _pad;
        float _y;

        /// <summary>쌓기를 시작합니다. width는 부모의 가로 크기이고 pad는 사방 여백입니다.</summary>
        public VStack(RectTransform parent, float width, float pad)
        {
            _parent = parent;
            _width = width;
            _pad = pad;
            _y = pad;
        }

        /// <summary>여백을 포함한 현재 높이입니다.</summary>
        public float Height => _y + _pad;

        /// <summary>안쪽 가로 크기입니다.</summary>
        public float InnerWidth => _width - _pad * 2f;

        /// <summary>세로 간격을 더합니다.</summary>
        public void Gap(float g) => _y += g;

        /// <summary>자동 줄바꿈 글자를 쌓고 만든 글자를 돌려줍니다.</summary>
        public Text AddText(string s, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft, float gapAfter = 8f, float indent = 0f)
        {
            var t = UiFactory.Label(_parent, s, size, color, bold, anchor);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(InnerWidth - indent, 10f);
            float h = Mathf.Ceil(t.preferredHeight);
            rt.sizeDelta = new Vector2(InnerWidth - indent, h);
            rt.anchoredPosition = new Vector2(_pad + indent, -_y);
            _y += h + gapAfter;
            return t;
        }

        /// <summary>왼쪽 위 기준 빈 요소를 현재 위치에 놓고 돌려줍니다. w가 0 이하이면 안쪽 가로 전체를 씁니다.</summary>
        public RectTransform AddRect(string name, float h, float w = -1f, float gapAfter = 8f)
        {
            var rt = UiFactory.Rect(_parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w > 0f ? w : InnerWidth, h);
            rt.anchoredPosition = new Vector2(_pad, -_y);
            _y += h + gapAfter;
            return rt;
        }

        /// <summary>구분선(1px)을 현재 위치에 긋습니다.</summary>
        public void Divider(float gapAfter = 8f)
        {
            var rt = AddRect("Divider", 1f, -1f, gapAfter);
            UiFactory.Fill(rt, Palette.Line, false, false);
        }
    }
}
