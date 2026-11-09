using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>성벽이 무너졌을 때 결과를 보여주는 오버레이입니다. 프리팹 Prefabs/UI/Over에 붙습니다.</summary>
    public sealed class OverView : MonoBehaviour
    {
        const float Pad = 22f;

        [SerializeField] RectTransform _card, _quoteBar, _kpis;
        [SerializeField] Text _title, _quote;
        [SerializeField] Text[] _kpiValues;
        [SerializeField] UiButton _restart;

        /// <summary>다시 시작 버튼을 눌렀을 때 호출됩니다.</summary>
        public Action OnRestart;

        /// <summary>열려 있는지 여부입니다.</summary>
        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            _restart.Button.onClick.AddListener(() => OnRestart?.Invoke());
        }

        /// <summary>결과를 보여줍니다.</summary>
        public void Show(Game g, float canvasW)
        {
            // 글자 높이를 재려면 켜져 있어야 함
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            float w = Mathf.Min(440f, canvasW - 32f);
            float inner = w - Pad * 2f;
            float y = Pad - 4f;
            y += Put(_title, inner, Pad, y, out _) + 10f;
            y += Put(_quote, inner - 13f, Pad + 13f, y, out float qh) + 14f;
            // 인용 막대는 인용문 높이를 따라감
            Set(_quoteBar, new Vector2(Pad, -(y - 14f - qh)), new Vector2(3f, qh));

            Set(_kpis, new Vector2(Pad, -y), new Vector2(inner, 46f));
            Value(0, Mathf.Max(0, g.Wave - 1));
            Value(1, g.Kills);
            Value(2, g.Delivered);
            float rowY = y;
            Set(_restart.Rt, new Vector2(Pad, -(rowY + 54f)), _restart.Rt.sizeDelta);
            var card = new Vector2(w, rowY + 116f);
            if (_card.sizeDelta != card) _card.sizeDelta = card;
        }

        void Value(int i, int v)
        {
            string s = v.ToString();
            if (_kpiValues[i].text != s) _kpiValues[i].text = s;
        }

        static void Set(RectTransform rt, Vector2 pos, Vector2 size)
        {
            if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
            if (rt.sizeDelta != size) rt.sizeDelta = size;
        }

        // 폭을 먼저 맞춘 뒤 높이를 재서 놓고, 차지한 높이를 돌려줌
        static float Put(Text t, float w, float x, float y, out float h)
        {
            var rt = t.rectTransform;
            var sd = rt.sizeDelta;
            if (sd.x != w)
            {
                sd.x = w;
                rt.sizeDelta = sd;
            }
            h = Mathf.Ceil(t.preferredHeight);
            if (sd.y != h)
            {
                sd.y = h;
                rt.sizeDelta = sd;
            }
            var p = new Vector2(x, -y);
            if (rt.anchoredPosition != p) rt.anchoredPosition = p;
            return h;
        }

        /// <summary>오버레이를 숨깁니다.</summary>
        public void Hide() => gameObject.SetActive(false);
    }
}
