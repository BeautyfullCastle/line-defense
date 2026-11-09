using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>성벽이 무너졌을 때 결과를 보여주는 오버레이입니다. 프리팹 Prefabs/UI/Over에 붙습니다.</summary>
    public sealed class OverView : MonoBehaviour
    {
        private const float Padding = 22f;

        [SerializeField] private RectTransform _card, _quoteBar, _kpis;
        [SerializeField] private Text _title, _quote;
        [SerializeField] private Text[] _kpiValues;
        [SerializeField] private UiButton _restart;

        /// <summary>다시 시작 버튼을 눌렀을 때 호출됩니다.</summary>
        public Action OnRestart;

        /// <summary>열려 있는지 여부입니다.</summary>
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            _restart.Button.onClick.AddListener(() => OnRestart?.Invoke());
        }

        /// <summary>결과를 보여줍니다.</summary>
        public void Show(Game game, float canvasWidth)
        {
            // 글자 높이를 재려면 켜져 있어야 함
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            float cardWidth = Mathf.Min(440f, canvasWidth - 32f);
            float inner = cardWidth - Padding * 2f;
            float y = Padding - 4f;
            y += Put(_title, inner, Padding, y, out _) + 10f;
            y += Put(_quote, inner - 13f, Padding + 13f, y, out float quoteHeight) + 14f;
            // 인용 막대는 인용문 높이를 따라감
            Set(_quoteBar, new Vector2(Padding, -(y - 14f - quoteHeight)), new Vector2(3f, quoteHeight));

            Set(_kpis, new Vector2(Padding, -y), new Vector2(inner, 46f));
            Value(0, Mathf.Max(0, game.Wave - 1));
            Value(1, game.Kills);
            Value(2, game.Delivered);
            float rowY = y;
            Set(_restart.RectTransform, new Vector2(Padding, -(rowY + 54f)), _restart.RectTransform.sizeDelta);
            var card = new Vector2(cardWidth, rowY + 116f);
            if (_card.sizeDelta != card)
            {
                _card.sizeDelta = card;
            }
        }

        private void Value(int i, int value)
        {
            string text = value.ToString();
            if (_kpiValues[i].text != text)
            {
                _kpiValues[i].text = text;
            }
        }

        private static void Set(RectTransform rectTransform, Vector2 position, Vector2 size)
        {
            if (rectTransform.anchoredPosition != position)
            {
                rectTransform.anchoredPosition = position;
            }

            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }

        // 폭을 먼저 맞춘 뒤 높이를 재서 놓고, 차지한 높이를 돌려줌
        private static float Put(Text label, float width, float x, float y, out float height)
        {
            var rectTransform = label.rectTransform;
            var sizeDelta = rectTransform.sizeDelta;
            if (sizeDelta.x != width)
            {
                sizeDelta.x = width;
                rectTransform.sizeDelta = sizeDelta;
            }
            height = Mathf.Ceil(label.preferredHeight);
            if (sizeDelta.y != height)
            {
                sizeDelta.y = height;
                rectTransform.sizeDelta = sizeDelta;
            }
            var anchoredPosition = new Vector2(x, -y);
            if (rectTransform.anchoredPosition != anchoredPosition)
            {
                rectTransform.anchoredPosition = anchoredPosition;
            }

            return height;
        }

        /// <summary>오버레이를 숨깁니다.</summary>
        public void Hide() => gameObject.SetActive(false);
    }
}
