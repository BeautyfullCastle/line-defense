using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>요새 설정창의 강화 줄(구분선, 이름, 레벨, 버튼, 설명)입니다. 높이는 설명 글자에 따라 달라집니다.</summary>
    public sealed class ConfigUpgradeRow : MonoBehaviour
    {
        private const float RowTop = 9f, RowHeight = 28f, DescriptionTop = 41f, DescriptionGap = 2f;

        [SerializeField] private RectTransform _divider, _row;
        [SerializeField] private Text _name, _level, _description;
        [SerializeField] private UiButton _button;

        private float _width = -1f;

        /// <summary>줄의 RectTransform입니다.</summary>
        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>강화 버튼입니다.</summary>
        public UiButton Button => _button;

        /// <summary>내용을 채우고 줄의 높이를 돌려줍니다. 값이 그대로면 아무것도 바꾸지 않습니다.</summary>
        public float Show(string title, string level, string buttonText, bool canClick, string description, float width)
        {
            if (!Mathf.Approximately(_width, width))
            {
                _width = width;
                SetSize(_divider, width, 1f);
                SetSize(_row, width, RowHeight);
                SetSize(_description.rectTransform, width, _description.rectTransform.sizeDelta.y);
            }
            SetText(_name, title);
            SetText(_level, level);
            _button.SetText(buttonText);
            SetSize(_button.RectTransform, UiFit.Width(_button.Label) + 22f, 26f);
            _button.SetInteractable(canClick);
            SetText(_description, description);
            float descriptionHeight = Mathf.Ceil(_description.preferredHeight);
            SetSize(_description.rectTransform, width, descriptionHeight);
            float rowHeight = DescriptionTop + descriptionHeight + DescriptionGap;
            SetSize(RectTransform, width, rowHeight);
            return rowHeight;
        }

        private static void SetText(Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static void SetSize(RectTransform rectTransform, float width, float height)
        {
            var size = new Vector2(width, height);
            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }
    }
}
