using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>요새 설정창의 강화 줄(구분선, 이름, 레벨, 버튼, 설명)입니다. 높이는 설명 글자에 따라 달라집니다.</summary>
    public sealed class ConfigUpgradeRow : MonoBehaviour
    {
        const float RowTop = 9f, RowH = 28f, DescTop = 41f, DescGap = 2f;

        [SerializeField] RectTransform _divider, _row;
        [SerializeField] Text _name, _level, _desc;
        [SerializeField] UiButton _button;

        float _w = -1f;

        /// <summary>줄의 RectTransform입니다.</summary>
        public RectTransform Rt => (RectTransform)transform;

        /// <summary>강화 버튼입니다.</summary>
        public UiButton Button => _button;

        /// <summary>내용을 채우고 줄의 높이를 돌려줍니다. 값이 그대로면 아무것도 바꾸지 않습니다.</summary>
        public float Show(string title, string level, string btnText, bool canClick, string desc, float width)
        {
            if (!Mathf.Approximately(_w, width))
            {
                _w = width;
                SetSize(_divider, width, 1f);
                SetSize(_row, width, RowH);
                SetSize(_desc.rectTransform, width, _desc.rectTransform.sizeDelta.y);
            }
            SetText(_name, title);
            SetText(_level, level);
            _button.SetText(btnText);
            SetSize(_button.Rt, UiFit.Width(_button.Label) + 22f, 26f);
            _button.SetInteractable(canClick);
            SetText(_desc, desc);
            float dh = Mathf.Ceil(_desc.preferredHeight);
            SetSize(_desc.rectTransform, width, dh);
            float h = DescTop + dh + DescGap;
            SetSize(Rt, width, h);
            return h;
        }

        static void SetText(Text t, string s)
        {
            if (t.text != s) t.text = s;
        }

        static void SetSize(RectTransform rt, float w, float h)
        {
            var s = new Vector2(w, h);
            if (rt.sizeDelta != s) rt.sizeDelta = s;
        }
    }
}
