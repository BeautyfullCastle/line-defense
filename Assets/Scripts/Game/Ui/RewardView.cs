using System;
using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>웨이브 방어 후 보급 선택지 세 개를 보여주는 중앙 오버레이입니다. 프리팹 Prefabs/UI/Reward에 붙습니다.</summary>
    public sealed class RewardView : MonoBehaviour
    {
        const float Pad = 22f;

        [SerializeField] RectTransform _card, _list, _quoteBar;
        [SerializeField] Text _title, _quote, _note;
        [SerializeField] RewardChoice _choicePrefab;
        [SerializeField] IconSet _icons;

        readonly List<RewardChoice> _pool = new List<RewardChoice>();

        /// <summary>선택지를 눌렀을 때 보급 키와 함께 호출됩니다.</summary>
        public Action<string> OnPick;

        /// <summary>오버레이가 열려 있는지 여부입니다.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>현재 보급 선택지를 채워 보여줍니다.</summary>
        public void Show(Game g, float canvasW)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            float w = Mathf.Min(440f, canvasW - 32f);
            float inner = w - Pad * 2f;
            float y = Pad - 4f;

            y += UiFit.Fit(_title, $"황실 전령 도착 · 웨이브 {g.Wave} 방어", inner, Pad, y) + 10f;
            float qh = UiFit.Fit(_quote, $"“{g.RewardQuote()}”", inner - 13f, Pad + 13f, y);
            UiFit.Box(_quoteBar, Pad, y, 3f, qh);
            y += qh + 12f;
            y += UiFit.Fit(_note, g.UpcomingNote(), inner, Pad, y) + 12f;

            int n = 0;
            foreach (string key in g.RewardChoices)
            {
                string k = key;
                var c = GetChoice(n++);
                y += c.Apply(_icons.Get(g.RewardIcon(k)), g.RewardTitle(k), g.RewardDesc(k), Pad, y, inner, () => OnPick?.Invoke(k)) + 8f;
            }
            // 이번에 안 쓰는 풀 항목은 숨김
            for (int i = n; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
            }
            UiFit.Size(_card, w, y + Pad - 8f);
        }

        RewardChoice GetChoice(int i)
        {
            if (i >= _pool.Count) _pool.Add(Instantiate(_choicePrefab, _list, false));
            var c = _pool[i];
            if (!c.gameObject.activeSelf) c.gameObject.SetActive(true);
            return c;
        }

        /// <summary>오버레이를 숨깁니다.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
