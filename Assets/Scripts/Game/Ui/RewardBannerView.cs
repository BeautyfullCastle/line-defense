using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>
    /// 보급을 고른 직후 화면 가운데에 무엇을 골랐는지 잠깐 띄우는 배너입니다.
    /// 입력을 막지 않으며 실제 시간으로 나타났다 사라집니다.
    /// </summary>
    public sealed class RewardBannerView
    {
        const float FadeIn = 0.22f;
        const float Hold = 1.8f;
        const float FadeOut = 0.45f;
        const float Width = 360f;

        readonly RectTransform _root;
        readonly CanvasGroup _group;
        RectTransform _card;
        float _t = -1f;

        /// <summary>배너 루트를 만듭니다. 처음에는 숨겨져 있습니다.</summary>
        public RewardBannerView(Transform parent)
        {
            _root = UiFactory.Rect(parent, "RewardBanner");
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _root.gameObject.SetActive(false);
        }

        /// <summary>고른 보급의 아이콘, 이름, 설명을 띄웁니다. byBot이면 자동 플레이가 골랐다고 표시합니다.</summary>
        public void Show(string icon, string title, string desc, bool byBot, float canvasW)
        {
            if (_card != null) Object.Destroy(_card.gameObject);
            float w = Mathf.Min(Width, canvasW - 32f);
            _card = UiFactory.Card(_root, "Card", Palette.Panel, Palette.Ink);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);

            const float pad = 18f, iconS = 40f;
            float textX = pad + iconS + 14f, textW = w - textX - pad;
            var head = UiFactory.Label(_card, byBot ? "자동 플레이 · 보급 선택" : "보급 획득", 12, Palette.Muted, true);
            Place(head.rectTransform, textX, pad, textW, 16f);
            var t = UiFactory.Label(_card, title, 18, Palette.Ink, true);
            t.rectTransform.sizeDelta = new Vector2(textW, 10f);
            float th = Mathf.Ceil(t.preferredHeight);
            Place(t.rectTransform, textX, pad + 18f, textW, th);
            var d = UiFactory.Label(_card, desc, 13, Palette.Muted);
            d.rectTransform.sizeDelta = new Vector2(textW, 10f);
            float dh = Mathf.Ceil(d.preferredHeight);
            Place(d.rectTransform, textX, pad + 18f + th + 4f, textW, dh);
            float h = Mathf.Max(iconS + pad * 2f, pad + 18f + th + 4f + dh + pad);

            var ic = UiFactory.Rect(_card, "Icon");
            ic.anchorMin = ic.anchorMax = new Vector2(0, 0.5f);
            ic.pivot = new Vector2(0, 0.5f);
            ic.anchoredPosition = new Vector2(pad, 0f);
            ic.sizeDelta = new Vector2(iconS, iconS);
            var img = ic.gameObject.AddComponent<Image>();
            img.sprite = IconFactory.Get(icon);
            img.color = Palette.Ink;
            img.raycastTarget = false;

            _card.sizeDelta = new Vector2(w, h);
            _root.sizeDelta = _card.sizeDelta;
            _t = 0f;
            _root.gameObject.SetActive(true);
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
                _root.gameObject.SetActive(false);
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
            _root.localScale = new Vector3(scale, scale, 1f);
            _root.anchoredPosition = new Vector2(0f, 40f + rise);
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>배너를 바로 숨깁니다.</summary>
        public void Hide()
        {
            _t = -1f;
            _root.gameObject.SetActive(false);
        }
    }
}
