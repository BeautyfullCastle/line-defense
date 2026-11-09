using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>왼쪽 위 기준 요소의 글자·크기·위치를 바뀔 때만 갱신하는 도우미입니다. 캔버스 재빌드를 줄입니다.</summary>
    internal static class UiFit
    {
        /// <summary>글자를 바꿉니다. 같으면 건드리지 않습니다.</summary>
        public static void SetText(Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        /// <summary>크기를 바꿉니다. 같으면 건드리지 않습니다.</summary>
        public static void SetSize(RectTransform rectTransform, float width, float height)
        {
            var size = new Vector2(width, height);
            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }

        /// <summary>왼쪽 위 기준으로 위치와 크기를 지정합니다(y는 아래 방향).</summary>
        public static void Place(RectTransform rectTransform, float x, float y, float width, float height)
        {
            SetSize(rectTransform, width, height);
            var position = new Vector2(x, -y);
            if (rectTransform.anchoredPosition != position)
            {
                rectTransform.anchoredPosition = position;
            }
        }

        private static readonly TextGenerator s_generator = new TextGenerator();

        /// <summary>글자 한 줄의 폭(캔버스 단위)입니다. 줄바꿈 없이 잰 값에 2를 더합니다.</summary>
        public static float Width(Text label)
        {
            // Text.preferredWidth는 조금 작게 나와서 이전 배치와 같은 방식으로 잼
            var settings = new TextGenerationSettings
            {
                font = label.font,
                fontSize = label.fontSize,
                fontStyle = label.fontStyle,
                scaleFactor = 1f,
                generateOutOfBounds = true,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                verticalOverflow = VerticalWrapMode.Overflow,
                generationExtents = new Vector2(10000f, 10000f),
                pivot = new Vector2(0.5f, 0.5f),
                richText = label.supportRichText,
                lineSpacing = 1f,
                textAnchor = TextAnchor.MiddleLeft,
                color = Color.black,
            };
            return s_generator.GetPreferredWidth(label.text, settings) + 2f;
        }

        /// <summary>글자를 넣고 폭에 맞는 높이로 줄여 놓은 뒤 그 높이를 돌려줍니다.</summary>
        public static float FitText(Text label, string text, float width, float x, float y)
        {
            SetText(label, text);
            var rectTransform = label.rectTransform;
            // 높이는 폭이 정해진 뒤에야 재므로 폭을 먼저 넣음
            if (rectTransform.sizeDelta.x != width)
            {
                rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);
            }

            float height = Mathf.Ceil(label.preferredHeight);
            Place(rectTransform, x, y, width, height);
            return height;
        }
    }
}
