using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>둥근 버튼의 상태(활성, 선택)를 바꾸는 도우미 컴포넌트입니다. 눌림 처리는 Button.onClick에 붙입니다.</summary>
    public sealed class UiButton : MonoBehaviour
    {
        [SerializeField] Button _btn;
        [SerializeField] Image _border, _fill;
        [SerializeField] Text _label;
        [SerializeField] bool _primary;

        /// <summary>눌림 이벤트 대상 버튼입니다.</summary>
        public Button Button => _btn;

        /// <summary>버튼 크기를 지정합니다.</summary>
        public RectTransform Rt => (RectTransform)transform;

        /// <summary>글자 요소입니다.</summary>
        public Text Label => _label;

        /// <summary>글자를 바꿉니다.</summary>
        public void SetText(string s)
        {
            if (_label.text != s) _label.text = s;
        }

        /// <summary>눌릴 수 있는지 지정합니다. 비활성이면 글자도 흐려집니다.</summary>
        public void SetInteractable(bool on)
        {
            if (_btn.interactable == on) return;
            _btn.interactable = on;
            var c = _label.color;
            c.a = on ? 1f : 0.4f;
            _label.color = c;
        }

        /// <summary>선택 상태(검정 바탕에 밝은 글자)를 지정합니다.</summary>
        public void SetOn(bool on)
        {
            if (_primary) return;
            var border = on ? Palette.Ink : Palette.Line;
            var fill = on ? Palette.Ink : Palette.Panel;
            var c = on ? Palette.Panel : Palette.Ink;
            c.a = _label.color.a;
            // 같은 값을 다시 넣어도 캔버스가 다시 그려지므로 바뀔 때만 넣음
            if (_border.color != border) _border.color = border;
            if (_fill.color != fill) _fill.color = fill;
            if (_label.color != c) _label.color = c;
        }

    }
}
