using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>
    /// 보급을 고른 직후 화면 가운데에 무엇을 골랐는지 잠깐 띄우는 배너입니다.
    /// 입력을 막지 않으며 실제 시간으로 나타났다 사라집니다. 프리팹 Prefabs/UI/RewardBanner에 붙습니다.
    /// </summary>
    public sealed class RewardBannerView : MonoBehaviour
    {
        const float FadeIn = 0.22f;
        const float Hold = 1.8f;
        const float FadeOut = 0.45f;
        const float Width = 360f;

        [SerializeField] CanvasGroup _group;
        [SerializeField] RectTransform _card;
        [SerializeField] Text _head, _title, _desc;
        [SerializeField] Image _icon;
        [SerializeField] IconSet _icons;

        float _t = -1f;

        RectTransform Root => (RectTransform)transform;

        /// <summary>고른 보급의 아이콘, 이름, 설명을 띄웁니다. byBot이면 자동 플레이가 골랐다고 표시합니다.</summary>
        public void Show(string icon, string title, string desc, bool byBot, float canvasW)
        {
            float w = Mathf.Min(Width, canvasW - 32f);
            const float pad = 18f, iconS = 40f;
            float textX = pad + iconS + 14f, textW = w - textX - pad;
            UiFit.Str(_head, byBot ? "자동 플레이 · 보급 선택" : "보급 획득");
            UiFit.Box(_head.rectTransform, textX, pad, textW, 16f);
            float th = UiFit.Fit(_title, title, textW, textX, pad + 18f);
            float dh = UiFit.Fit(_desc, desc, textW, textX, pad + 18f + th + 4f);
            float h = Mathf.Max(iconS + pad * 2f, pad + 18f + th + 4f + dh + pad);

            var sprite = _icons.Get(icon);
            if (_icon.sprite != sprite) _icon.sprite = sprite;

            UiFit.Size(_card, w, h);
            UiFit.Size(Root, w, h);
            _t = 0f;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Apply();
        }

        /// <summary>실제 경과 시간만큼 애니메이션을 진행하고 끝나면 숨깁니다.</summary>
        public void Tick(float dt)
        {
            if (_t < 0f) return;
            _t += dt;
            if (_t >= FadeIn + Hold + FadeOut)
            {
                _t = -1f;
                gameObject.SetActive(false);
                return;
            }
            Apply();
        }

        // 살짝 커지며 나타나고, 머문 뒤 위로 떠오르며 사라짐
        void Apply()
        {
            float alpha, scale, rise;
            if (_t < FadeIn)
            {
                float k = _t / FadeIn, back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                alpha = k;
                scale = Mathf.LerpUnclamped(0.86f, 1f, back);
                rise = 0f;
            }
            else if (_t < FadeIn + Hold)
            {
                alpha = 1f; scale = 1f; rise = 0f;
            }
            else
            {
                float k = (_t - FadeIn - Hold) / FadeOut;
                alpha = 1f - k;
                scale = 1f;
                rise = 18f * k * k;
            }
            _group.alpha = alpha;
            Root.localScale = new Vector3(scale, scale, 1f);
            Root.anchoredPosition = new Vector2(0f, 40f + rise);
        }

        /// <summary>배너 아이콘 가운데의 화면 좌표입니다. 얻은 것이 여기서 날아갑니다.</summary>
        public Vector2 IconScreen()
        {
            var ic = _icon.rectTransform;
            return ic.TransformPoint(ic.rect.center);
        }

        /// <summary>배너를 바로 숨깁니다.</summary>
        public void Hide()
        {
            _t = -1f;
            gameObject.SetActive(false);
        }
    }
}
