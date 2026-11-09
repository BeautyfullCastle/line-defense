using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// 개수가 바뀌는 작은 도형(자원 칸, 사기 막대, 강화 표시)을 자식 SpriteRenderer로 그립니다.
    /// 매 프레임 Begin, 그리기, End 순서로 부르면 쓰지 않은 자식은 숨깁니다.
    /// 좌표는 y 아래 방향 월드 단위이며 부모 기준입니다.
    /// </summary>
    public sealed class PipGroup : MonoBehaviour
    {
        // 원·삼각형 스프라이트 반지름과 테두리 스프라이트 기준 반지름(월드 단위)
        private const float ShapeRadius = 16f, RingRadius = 3.3f, SquareSide = 4f;

        [SerializeField] private WorldArt _worldArt;
        [SerializeField] private int _order;

        private readonly List<SpriteRenderer> _items = new List<SpriteRenderer>();
        private int _used;

        /// <summary>이후 그리는 도형의 알파에 곱하는 값입니다.</summary>
        public float Alpha = 1f;

        public void Begin()
        {
            _used = 0;
            Alpha = 1f;
        }

        public void End()
        {
            for (int i = _used; i < _items.Count; i++)
            {
                if (_items[i].gameObject.activeSelf)
                {
                    _items[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>스프라이트 하나를 (x, y)에 균등 배율로 그립니다. layer는 같은 묶음 안의 위아래 순서입니다.</summary>
        public SpriteRenderer Shape(Sprite sprite, float x, float y, float scale, Color color, int layer = 0) =>
            Put(sprite, x, y, new Vector3(scale, scale, 1f), color, layer);

        /// <summary>왼쪽 위 (x, y), 폭 w, 높이 h 사각형을 채웁니다.</summary>
        public void Rect(float x, float y, float width, float height, Color color, int layer = 0) =>
            Put(_worldArt.Square, x + width * 0.5f, y + height * 0.5f, new Vector3(width / SquareSide, height / SquareSide, 1f), color, layer);

        /// <summary>자원 모양(식량 원, 무기 삼각형)을 반지름 r로 채웁니다.</summary>
        public void Dot(Res resource, float x, float y, float radius, Color color) => Shape(Fill(resource), x, y, radius / ShapeRadius, color);

        /// <summary>찬 칸은 크고 진한 채움, 빈 칸은 작고 흐린 테두리로 그립니다. onColor가 있으면 그 색으로 그립니다.</summary>
        public void Slot(Res resource, float x, float y, bool full, float radius, Color? onColor = null)
        {
            var color = onColor ?? Palette.ColorOf(resource);
            if (full)
            {
                Dot(resource, x, y, radius * 1.15f, color);
                return;
            }
            float prev = Alpha;
            Alpha = prev * 0.45f;
            if (onColor == null)
            {
                Dot(resource, x, y, radius * 0.85f, Palette.Ground);
            }

            Shape(resource == Res.Food ? _worldArt.PipRingCircle : _worldArt.PipRingTriangle, x, y, radius / RingRadius, color, 1);
            Alpha = prev;
        }

        /// <summary>요청 칸(창고 목표량, 요새 예약)을 점선 테두리로 그립니다.</summary>
        public void Request(Res resource, float x, float y, float radius)
        {
            Dot(resource, x, y, radius * 0.95f, Palette.Ground);
            Shape(resource == Res.Food ? _worldArt.PipRequestCircle : _worldArt.PipRequestTriangle, x, y, radius / RingRadius, Palette.ColorOf(resource), 1);
        }

        /// <summary>칸을 넘친 자원 둘레의 붉은 테두리입니다.</summary>
        public void Over(Res resource, float x, float y) =>
            Shape(resource == Res.Food ? _worldArt.PipOverCircle : _worldArt.PipOverTriangle, x, y, 1f, Palette.Danger, 1);

        private Sprite Fill(Res resource) => resource == Res.Food ? _worldArt.Disc : _worldArt.Triangle;

        private SpriteRenderer Put(Sprite sprite, float x, float y, Vector3 scale, Color color, int layer)
        {
            if (_used == _items.Count)
            {
                var spriteRenderer = Instantiate(_worldArt.Pip, transform, false);
                _items.Add(spriteRenderer);
            }
            var item = _items[_used++];
            if (!item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(true);
            }

            item.sprite = sprite;
            item.sortingOrder = _order + layer;
            color.a *= Alpha;
            item.color = color;
            var itemTransform = item.transform;
            itemTransform.localPosition = new Vector3(x, -y, 0f);
            itemTransform.localScale = scale;
            return item;
        }
    }
}
