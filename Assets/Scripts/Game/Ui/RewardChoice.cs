using System;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>보급 선택지 카드 하나(아이콘, 이름, 설명)입니다. 프리팹 Prefabs/UI/Items/RewardChoice에 붙습니다.</summary>
    public sealed class RewardChoice : MonoBehaviour
    {
        [SerializeField] private UiButton _button;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _title, _description;

        /// <summary>내용을 채우고 (x, y)에 width 폭으로 놓은 뒤 카드 높이를 돌려줍니다. 누르면 onClick이 불립니다.</summary>
        public float Apply(Sprite icon, string title, string description, float x, float y, float width, Action onClick)
        {
            float textWidth = width - 24f - 28f - 12f;
            float titleHeight = UiFit.FitText(_title, title, textWidth, 52f, 10f);
            float descriptionHeight = UiFit.FitText(_description, description, textWidth, 52f, 10f + titleHeight + 2f);
            float cardHeight = Mathf.Max(56f, titleHeight + descriptionHeight + 24f);
            UiFit.Place((RectTransform)transform, x, y, width, cardHeight);
            if (_icon.sprite != icon)
            {
                _icon.sprite = icon;
            }
            // 풀에서 재사용하므로 이전 선택 대상을 지우고 다시 연결함
            _button.Button.onClick.RemoveAllListeners();
            _button.Button.onClick.AddListener(() => onClick?.Invoke());
            return cardHeight;
        }
    }
}
