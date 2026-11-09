using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>상단 가운데에 잠깐 뜨는 알림입니다. 프리팹 Prefabs/UI/Toast에 붙습니다.</summary>
    public sealed class ToastView : MonoBehaviour
    {
        const float Duration = 3.2f;

        [SerializeField] Text _text;

        float _left;
        float _top = 60f;

        RectTransform Rt => (RectTransform)transform;

        /// <summary>머리말 높이에 맞춰 위치를 잡습니다.</summary>
        public void SetTop(float topInset)
        {
            _top = topInset;
            var p = new Vector2(0f, -(topInset + 14f));
            if (Rt.anchoredPosition != p) Rt.anchoredPosition = p;
        }

        /// <summary>알림을 띄웁니다.</summary>
        public void Show(string msg, float canvasW)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (_text.text != msg) _text.text = msg;
            // 비활성 상태에서는 글자 폭이 정확하지 않아 먼저 켬
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            float w = Mathf.Min(UiFit.Width(_text) + 32f, canvasW - 32f);
            var size = new Vector2(w, 34f);
            if (Rt.sizeDelta != size) Rt.sizeDelta = size;
            SetTop(_top);
            _left = Duration;
        }

        /// <summary>남은 시간을 줄이고 끝나면 숨깁니다.</summary>
        public void Tick(float dt)
        {
            if (_left <= 0f) return;
            _left -= dt;
            if (_left <= 0f) gameObject.SetActive(false);
        }
    }
}
