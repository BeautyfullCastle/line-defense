using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>왼쪽 위 기준 요소의 글자·크기·위치를 바뀔 때만 갱신하는 도우미입니다. 캔버스 재빌드를 줄입니다.</summary>
    internal static class UiFit
    {
        /// <summary>글자를 바꿉니다. 같으면 건드리지 않습니다.</summary>
        public static void Str(Text t, string s)
        {
            if (t.text != s) t.text = s;
        }

        /// <summary>크기를 바꿉니다. 같으면 건드리지 않습니다.</summary>
        public static void Size(RectTransform rt, float w, float h)
        {
            var s = new Vector2(w, h);
            if (rt.sizeDelta != s) rt.sizeDelta = s;
        }

        /// <summary>왼쪽 위 기준으로 위치와 크기를 지정합니다(y는 아래 방향).</summary>
        public static void Box(RectTransform rt, float x, float y, float w, float h)
        {
            Size(rt, w, h);
            var p = new Vector2(x, -y);
            if (rt.anchoredPosition != p) rt.anchoredPosition = p;
        }

        static readonly TextGenerator Gen = new TextGenerator();

        /// <summary>글자 한 줄의 폭(캔버스 단위)입니다. 줄바꿈 없이 잰 값에 2를 더합니다.</summary>
        public static float Width(Text t)
        {
            // Text.preferredWidth는 조금 작게 나와서 이전 배치와 같은 방식으로 잼
            var s = new TextGenerationSettings
            {
                font = t.font,
                fontSize = t.fontSize,
                fontStyle = t.fontStyle,
                scaleFactor = 1f,
                generateOutOfBounds = true,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                verticalOverflow = VerticalWrapMode.Overflow,
                generationExtents = new Vector2(10000f, 10000f),
                pivot = new Vector2(0.5f, 0.5f),
                richText = t.supportRichText,
                lineSpacing = 1f,
                textAnchor = TextAnchor.MiddleLeft,
                color = Color.black,
            };
            return Gen.GetPreferredWidth(t.text, s) + 2f;
        }

        /// <summary>글자를 넣고 폭에 맞는 높이로 줄여 놓은 뒤 그 높이를 돌려줍니다.</summary>
        public static float Fit(Text t, string s, float w, float x, float y)
        {
            Str(t, s);
            var rt = t.rectTransform;
            // 높이는 폭이 정해진 뒤에야 재므로 폭을 먼저 넣음
            if (rt.sizeDelta.x != w) rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            float h = Mathf.Ceil(t.preferredHeight);
            Box(rt, x, y, w, h);
            return h;
        }
    }
}
