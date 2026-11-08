using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>웨이브 방어 후 보급 선택지 세 개를 보여주는 중앙 오버레이입니다.</summary>
    public sealed class RewardView
    {
        readonly RectTransform _root;
        RectTransform _card;

        /// <summary>선택지를 눌렀을 때 보급 키와 함께 호출됩니다.</summary>
        public Action<string> OnPick;

        /// <summary>오버레이 루트를 만듭니다. 처음에는 숨겨져 있습니다.</summary>
        public RewardView(Transform parent)
        {
            _root = UiFactory.Rect(parent, "Reward");
            UiFactory.Stretch(_root);
            UiFactory.Fill(_root, Palette.WithAlpha(Palette.Outside, 0.55f), false, true);
            _root.gameObject.SetActive(false);
        }

        /// <summary>오버레이가 열려 있는지 여부입니다.</summary>
        public bool IsOpen => _root.gameObject.activeSelf;

        /// <summary>현재 보급 선택지를 그려 보여줍니다.</summary>
        public void Show(Game g, float canvasW)
        {
            _root.gameObject.SetActive(true);
            if (_card != null) UnityEngine.Object.Destroy(_card.gameObject);
            float w = Mathf.Min(440f, canvasW - 32f);
            _card = UiFactory.Card(_root, "Card", Palette.Panel, Palette.Line);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            var s = new VStack(_card, w, 22f);
            s.Gap(-4f);
            s.AddText($"황실 전령 도착 · 웨이브 {g.Wave} 방어", 22, Palette.Ink, true, TextAnchor.UpperLeft, 10f);
            string quote = $"“{g.RewardQuote()}”";
            var q = s.AddText(quote, 14, Palette.Muted, false, TextAnchor.UpperLeft, 12f, 13f);
            q.fontStyle = FontStyle.Italic;
            var bar = UiFactory.Rect(_card, "QuoteBar");
            bar.anchorMin = bar.anchorMax = new Vector2(0, 1);
            bar.pivot = new Vector2(0, 1);
            bar.anchoredPosition = new Vector2(22f, q.rectTransform.anchoredPosition.y);
            bar.sizeDelta = new Vector2(3f, q.rectTransform.sizeDelta.y);
            UiFactory.Fill(bar, Palette.Line, false, false);
            s.AddText(g.UpcomingNote(), 14, Palette.Ink, false, TextAnchor.UpperLeft, 12f);

            foreach (string key in g.RewardChoices)
            {
                string k = key;
                AddChoice(s, g, k);
            }
            _card.sizeDelta = new Vector2(w, s.Height - 8f);
        }

        void AddChoice(VStack s, Game g, string key)
        {
            string title = g.RewardTitle(key), desc = g.RewardDesc(key);
            float textW = s.InnerWidth - 24f - 28f - 12f;
            var probeT = UiFactory.Label(_card, title, 15, Palette.Ink, true);
            probeT.rectTransform.sizeDelta = new Vector2(textW, 10f);
            float th = Mathf.Ceil(probeT.preferredHeight);
            UnityEngine.Object.Destroy(probeT.gameObject);
            var probeD = UiFactory.Label(_card, desc, 12, Palette.Muted);
            probeD.rectTransform.sizeDelta = new Vector2(textW, 10f);
            float dh = Mathf.Ceil(probeD.preferredHeight);
            UnityEngine.Object.Destroy(probeD.gameObject);

            float h = Mathf.Max(56f, th + dh + 24f);
            var slot = s.AddRect("Choice", h, -1f, 8f);
            var btn = UiFactory.Button(slot, string.Empty, () => OnPick?.Invoke(key), s.InnerWidth, h);
            UiFactory.Stretch(btn.Rt);
            var inner = btn.Rt.GetChild(0);

            var icon = UiFactory.Rect(inner, "Icon");
            icon.anchorMin = icon.anchorMax = new Vector2(0, 0.5f);
            icon.pivot = new Vector2(0, 0.5f);
            icon.anchoredPosition = new Vector2(12f, 0f);
            icon.sizeDelta = new Vector2(28f, 28f);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = IconFactory.Get(g.RewardIcon(key));
            img.color = Palette.Ink;
            img.raycastTarget = false;

            var t = UiFactory.Label(inner, title, 15, Palette.Ink, true);
            Top(t.rectTransform, 52f, 10f, textW, th);
            var d = UiFactory.Label(inner, desc, 12, Palette.Muted);
            Top(d.rectTransform, 52f, 10f + th + 2f, textW, dh);
        }

        static void Top(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>오버레이를 숨기고 내용을 지웁니다.</summary>
        public void Hide()
        {
            _root.gameObject.SetActive(false);
            if (_card != null) UnityEngine.Object.Destroy(_card.gameObject);
            _card = null;
        }
    }
}
