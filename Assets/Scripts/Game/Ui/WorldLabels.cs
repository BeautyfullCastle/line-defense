using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>월드 위에 얹는 글자를 풀링해 오버레이 캔버스에 배치합니다. 프리팹 Prefabs/UI/WorldLabels에 붙습니다.</summary>
    public sealed class WorldLabels : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Font _regular, _bold;
        [SerializeField] private Text _labelPrefab;

        private readonly List<Text> _pool = new List<Text>();
        private int _used;

        /// <summary>한 프레임의 배치를 시작합니다.</summary>
        public void Begin() => _used = 0;

        /// <summary>월드 좌표(y 아래 방향)에 가운데 정렬 글자를 놓습니다. worldSize는 월드 단위 글자 크기입니다.</summary>
        public void Add(CameraFit cameraFit, string text, float worldX, float worldY, float worldSize, Color color, bool bold = false)
        {
            float scaleFactor = _canvas.scaleFactor;
            var screenPosition = cameraFit.WorldToScreen(worldX, worldY - worldSize * 0.35f);
            if (screenPosition.x < -200f || screenPosition.y < -100f || screenPosition.x > cameraFit.ScreenWidth + 200f || screenPosition.y > cameraFit.ScreenHeight + 100f)
            {
                return;
            }

            if (_used >= _pool.Count)
            {
                _pool.Add(Instantiate(_labelPrefab, transform, false));
            }

            var label = _pool[_used++];
            if (!label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(true);
            }

            var font = bold ? _bold : _regular;
            if (label.font != font)
            {
                label.font = font;
            }

            int size = Mathf.Max(9, Mathf.RoundToInt(worldSize * cameraFit.PixelsPerUnit / scaleFactor));
            if (label.fontSize != size)
            {
                label.fontSize = size;
            }

            if (label.text != text)
            {
                label.text = text;
            }

            if (label.color != color)
            {
                label.color = color;
            }

            label.rectTransform.anchoredPosition = screenPosition / scaleFactor;
        }

        /// <summary>이번 프레임에 쓰지 않은 글자를 숨깁니다.</summary>
        public void End()
        {
            for (int i = _used; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf)
                {
                    _pool[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
