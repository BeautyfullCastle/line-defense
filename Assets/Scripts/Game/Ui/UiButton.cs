using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>둥근 버튼의 상태(활성, 선택)를 바꾸는 도우미 컴포넌트입니다. 눌림 처리는 Button.onClick에 붙입니다.</summary>
    public sealed class UiButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _border, _fill;
        [SerializeField] private Text _label;
        [SerializeField] private bool _primary;

        /// <summary>눌림 이벤트 대상 버튼입니다.</summary>
        public Button Button => _button;

        /// <summary>버튼 크기를 지정합니다.</summary>
        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>글자 요소입니다.</summary>
        public Text Label => _label;

        /// <summary>글자를 바꿉니다.</summary>
        public void SetText(string text)
        {
            if (_label.text != text)
            {
                _label.text = text;
            }
        }

        /// <summary>눌릴 수 있는지 지정합니다. 비활성이면 글자도 흐려집니다.</summary>
        public void SetInteractable(bool on)
        {
            if (_button.interactable == on)
            {
                return;
            }

            _button.interactable = on;
            var color = _label.color;
            color.a = on ? 1f : 0.4f;
            _label.color = color;
        }

        /// <summary>선택 상태(검정 바탕에 밝은 글자)를 지정합니다.</summary>
        public void SetOn(bool on)
        {
            if (_primary)
            {
                return;
            }

            var border = on ? Palette.Ink : Palette.Line;
            var fill = on ? Palette.Ink : Palette.Panel;
            var color = on ? Palette.Panel : Palette.Ink;
            color.a = _label.color.a;
            // 같은 값을 다시 넣어도 캔버스가 다시 그려지므로 바뀔 때만 넣음
            if (_border.color != border)
            {
                _border.color = border;
            }

            if (_fill.color != fill)
            {
                _fill.color = fill;
            }

            if (_label.color != color)
            {
                _label.color = color;
            }
        }

    }
}
