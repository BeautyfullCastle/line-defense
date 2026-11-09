using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>하단 꼬리말의 노선 칩 한 개입니다. 풀에서 재사용하며 프리팹 Prefabs/UI/Items/LineChip에 붙습니다.</summary>
    public sealed class LineChip : MonoBehaviour
    {
        private const float LabelX = 28f;

        [SerializeField] private Image _color;
        [SerializeField] private Text _label;
        [SerializeField] private UiButton _break, _cart, _reset;

        /// <summary>칩의 RectTransform입니다.</summary>
        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>노선 정보로 글자와 버튼을 채우고 칩 폭을 다시 계산합니다.</summary>
        public void Bind(Game game, Line line)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            int stops = line.Nodes.Count - 1, forts = 0;
            foreach (int id in line.Nodes)
            {
                var node = game.NodeById(id);
                if (node != null && node.Type == NodeType.Fort)
                {
                    forts++;
                }
            }
            string text = $"{(line.Loop ? "순환 · " : string.Empty)}정거장 {stops} · 요새 {forts} · 마차 {line.Carts.Count}";
            var color = Palette.LineColor(line.ColorIndex);
            if (_color.color != color)
            {
                _color.color = color;
            }

            if (_label.text != text)
            {
                _label.text = text;
            }

            float labelWidth = UiFit.Width(_label) + 4f;
            SetBox(_label.rectTransform, LabelX, labelWidth, 24f);
            float x = LabelX + labelWidth + 6f;
            x = PlaceButton(_break, line.Loop, x);
            x = PlaceButton(_cart, game.CartTokens > 0, x);
            x = PlaceButton(_reset, true, x);
            _cart.SetOn(true);

            Rewire(_break, () => game.BreakLoop(line));
            Rewire(_cart, () => game.AddCart(line));
            Rewire(_reset, () => game.ResetLine(line));

            var size = new Vector2(x + 2f, 28f);
            if (RectTransform.sizeDelta != size)
            {
                RectTransform.sizeDelta = size;
            }
        }

        /// <summary>풀로 돌려보냅니다.</summary>
        public void Release()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private static float PlaceButton(UiButton button, bool show, float x)
        {
            if (button.gameObject.activeSelf != show)
            {
                button.gameObject.SetActive(show);
            }

            if (!show)
            {
                return x;
            }

            float buttonWidth = UiFit.Width(button.Label) + 18f;
            SetBox(button.RectTransform, x, buttonWidth, 20f);
            return x + buttonWidth + 4f;
        }

        private static void SetBox(RectTransform rectTransform, float x, float width, float height)
        {
            var position = new Vector2(x, 0f);
            if (rectTransform.anchoredPosition != position)
            {
                rectTransform.anchoredPosition = position;
            }

            var size = new Vector2(width, height);
            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }

        private static void Rewire(UiButton button, Action click)
        {
            button.Button.onClick.RemoveAllListeners();
            button.Button.onClick.AddListener(() => click());
        }
    }
}
