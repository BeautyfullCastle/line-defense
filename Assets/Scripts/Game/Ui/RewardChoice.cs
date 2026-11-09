using System;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>보급 선택지 카드 하나(아이콘, 이름, 설명)입니다. 프리팹 Prefabs/UI/Items/RewardChoice에 붙습니다.</summary>
    public sealed class RewardChoice : MonoBehaviour
    {
        [SerializeField] UiButton _button;
        [SerializeField] Image _icon;
        [SerializeField] Text _title, _desc;

        /// <summary>내용을 채우고 (x, y)에 width 폭으로 놓은 뒤 카드 높이를 돌려줍니다. 누르면 onClick이 불립니다.</summary>
        public float Apply(Sprite icon, string title, string desc, float x, float y, float width, Action onClick)
        {
            float textW = width - 24f - 28f - 12f;
            float th = UiFit.Fit(_title, title, textW, 52f, 10f);
            float dh = UiFit.Fit(_desc, desc, textW, 52f, 10f + th + 2f);
            float h = Mathf.Max(56f, th + dh + 24f);
            UiFit.Box((RectTransform)transform, x, y, width, h);
            if (_icon.sprite != icon) _icon.sprite = icon;
            // 풀에서 재사용하므로 이전 선택 대상을 지우고 다시 연결함
            _button.Button.onClick.RemoveAllListeners();
            _button.Button.onClick.AddListener(() => onClick?.Invoke());
            return h;
        }
    }
}
