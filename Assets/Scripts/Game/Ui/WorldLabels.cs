using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>월드 위에 얹는 글자를 풀링해 오버레이 캔버스에 배치합니다.</summary>
    public sealed class WorldLabels
    {
        readonly RectTransform _root;
        readonly Canvas _canvas;
        readonly List<Text> _pool = new List<Text>();
        int _used;

        /// <summary>글자가 붙을 캔버스를 지정합니다.</summary>
        public WorldLabels(Canvas canvas)
        {
            _canvas = canvas;
            _root = UiFactory.Rect(canvas.transform, "WorldLabels");
            UiFactory.Stretch(_root);
        }

        /// <summary>한 프레임의 배치를 시작합니다.</summary>
        public void Begin() => _used = 0;

        /// <summary>월드 좌표(y 아래 방향)에 가운데 정렬 글자를 놓습니다. worldSize는 월드 단위 글자 크기입니다.</summary>
        public void Add(CameraFit cam, string text, float wx, float wy, float worldSize, Color color, bool bold = false)
        {
            float sf = _canvas.scaleFactor;
            var sp = cam.WorldToScreen(wx, wy - worldSize * 0.35f);
            if (sp.x < -200f || sp.y < -100f || sp.x > cam.W + 200f || sp.y > cam.H + 100f) return;
            Text t;
            if (_used < _pool.Count) t = _pool[_used];
            else
            {
                t = UiFactory.Label(_root, string.Empty, 12, Color.black, false, TextAnchor.MiddleCenter);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(400f, 40f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                _pool.Add(t);
            }
            _used++;
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            var font = bold ? UiFactory.Bold : UiFactory.Regular;
            if (t.font != font) t.font = font;
            int size = Mathf.Max(9, Mathf.RoundToInt(worldSize * cam.S / sf));
            if (t.fontSize != size) t.fontSize = size;
            if (t.text != text) t.text = text;
            if (t.color != color) t.color = color;
            t.rectTransform.anchoredPosition = sp / sf;
        }

        /// <summary>이번 프레임에 쓰지 않은 글자를 숨깁니다.</summary>
        public void End()
        {
            for (int i = _used; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
