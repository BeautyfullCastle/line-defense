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
        private const float Padding = 22f;

        [SerializeField] private RectTransform _card, _list, _quoteBar;
        [SerializeField] private Text _title, _quote, _note;
        [SerializeField] private RewardChoice _choicePrefab;
        [SerializeField] private IconSet _icons;

        private readonly List<RewardChoice> _pool = new List<RewardChoice>();

        /// <summary>선택지를 눌렀을 때 보급 키와 함께 호출됩니다.</summary>
        public Action<string> OnPick;

        /// <summary>오버레이가 열려 있는지 여부입니다.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>현재 보급 선택지를 채워 보여줍니다.</summary>
        public void Show(Game game, float canvasWidth)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            float cardWidth = Mathf.Min(440f, canvasWidth - 32f);
            float inner = cardWidth - Padding * 2f;
            float y = Padding - 4f;

            y += UiFit.FitText(_title, $"황실 전령 도착 · 웨이브 {game.Wave} 방어", inner, Padding, y) + 10f;
            float quoteHeight = UiFit.FitText(_quote, $"“{game.RewardQuote()}”", inner - 13f, Padding + 13f, y);
            UiFit.Place(_quoteBar, Padding, y, 3f, quoteHeight);
            y += quoteHeight + 12f;
            y += UiFit.FitText(_note, game.UpcomingNote(), inner, Padding, y) + 12f;

            int choiceCount = 0;
            foreach (string key in game.RewardChoices)
            {
                string k = key;
                var choice = GetChoice(choiceCount++);
                y += choice.Apply(_icons.Get(game.RewardIcon(k)), game.RewardTitle(k), game.RewardDesc(k), Padding, y, inner, () => OnPick?.Invoke(k)) + 8f;
            }
            // 이번에 안 쓰는 풀 항목은 숨김
            for (int i = choiceCount; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf)
                {
                    _pool[i].gameObject.SetActive(false);
                }
            }
            UiFit.SetSize(_card, cardWidth, y + Padding - 8f);
        }

        private RewardChoice GetChoice(int i)
        {
            if (i >= _pool.Count)
            {
                _pool.Add(Instantiate(_choicePrefab, _list, false));
            }

            var choice = _pool[i];
            if (!choice.gameObject.activeSelf)
            {
                choice.gameObject.SetActive(true);
            }

            return choice;
        }

        /// <summary>오버레이를 숨깁니다.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
