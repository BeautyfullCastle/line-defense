using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>상단 가운데에 잠깐 뜨는 알림입니다. 프리팹 Prefabs/UI/Toast에 붙습니다.</summary>
    public sealed class ToastView : MonoBehaviour
    {
        private const float Duration = 3.2f;

        [SerializeField] private Text _text;

        private float _left;
        private float _topInset = 60f;

        private RectTransform RectTransform => (RectTransform)transform;

        /// <summary>머리말 높이에 맞춰 위치를 잡습니다.</summary>
        public void SetTop(float topInset)
        {
            _topInset = topInset;
            var position = new Vector2(0f, -(topInset + 14f));
            if (RectTransform.anchoredPosition != position)
            {
                RectTransform.anchoredPosition = position;
            }
        }

        /// <summary>알림을 띄웁니다.</summary>
        public void Show(string msg, float canvasWidth)
        {
            if (string.IsNullOrEmpty(msg))
            {
                return;
            }

            if (_text.text != msg)
            {
                _text.text = msg;
            }
            // 비활성 상태에서는 글자 폭이 정확하지 않아 먼저 켬
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            float toastWidth = Mathf.Min(UiFit.Width(_text) + 32f, canvasWidth - 32f);
            var size = new Vector2(toastWidth, 34f);
            if (RectTransform.sizeDelta != size)
            {
                RectTransform.sizeDelta = size;
            }

            SetTop(_topInset);
            _left = Duration;
        }

        /// <summary>남은 시간을 줄이고 끝나면 숨깁니다.</summary>
        public void Tick(float dt)
        {
            if (_left <= 0f)
            {
                return;
            }

            _left -= dt;
            if (_left <= 0f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
