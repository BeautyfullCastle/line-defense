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
        const float ShapeR = 16f, RingR = 3.3f, SquareSide = 4f;

        [SerializeField] WorldArt _art;
        [SerializeField] int _order;

        readonly List<SpriteRenderer> _items = new List<SpriteRenderer>();
        int _used;

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
                if (_items[i].gameObject.activeSelf) _items[i].gameObject.SetActive(false);
        }

        /// <summary>스프라이트 하나를 (x, y)에 균등 배율로 그립니다. layer는 같은 묶음 안의 위아래 순서입니다.</summary>
        public SpriteRenderer Shape(Sprite s, float x, float y, float scale, Color c, int layer = 0) =>
            Put(s, x, y, new Vector3(scale, scale, 1f), c, layer);

        /// <summary>왼쪽 위 (x, y), 폭 w, 높이 h 사각형을 채웁니다.</summary>
        public void Rect(float x, float y, float w, float h, Color c, int layer = 0) =>
            Put(_art.Square, x + w * 0.5f, y + h * 0.5f, new Vector3(w / SquareSide, h / SquareSide, 1f), c, layer);

        /// <summary>자원 모양(식량 원, 무기 삼각형)을 반지름 r로 채웁니다.</summary>
        public void Dot(Res k, float x, float y, float r, Color c) => Shape(Fill(k), x, y, r / ShapeR, c);

        /// <summary>찬 칸은 크고 진한 채움, 빈 칸은 작고 흐린 테두리로 그립니다. onColor가 있으면 그 색으로 그립니다.</summary>
        public void Slot(Res k, float x, float y, bool full, float r, Color? onColor = null)
        {
            var col = onColor ?? Palette.Of(k);
            if (full)
            {
                Dot(k, x, y, r * 1.15f, col);
                return;
            }
            float prev = Alpha;
            Alpha = prev * 0.45f;
            if (onColor == null) Dot(k, x, y, r * 0.85f, Palette.Ground);
            Shape(k == Res.Food ? _art.PipRingCircle : _art.PipRingTri, x, y, r / RingR, col, 1);
            Alpha = prev;
        }

        /// <summary>요청 칸(창고 목표량, 요새 예약)을 점선 테두리로 그립니다.</summary>
        public void Request(Res k, float x, float y, float r)
        {
            Dot(k, x, y, r * 0.95f, Palette.Ground);
            Shape(k == Res.Food ? _art.PipReqCircle : _art.PipReqTri, x, y, r / RingR, Palette.Of(k), 1);
        }

        /// <summary>칸을 넘친 자원 둘레의 붉은 테두리입니다.</summary>
        public void Over(Res k, float x, float y) =>
            Shape(k == Res.Food ? _art.PipOverCircle : _art.PipOverTri, x, y, 1f, Palette.Danger, 1);

        Sprite Fill(Res k) => k == Res.Food ? _art.Disc : _art.Tri;

        SpriteRenderer Put(Sprite s, float x, float y, Vector3 scale, Color c, int layer)
        {
            if (_used == _items.Count)
            {
                var sr = Instantiate(_art.Pip, transform, false);
                _items.Add(sr);
            }
            var r = _items[_used++];
            if (!r.gameObject.activeSelf) r.gameObject.SetActive(true);
            r.sprite = s;
            r.sortingOrder = _order + layer;
            c.a *= Alpha;
            r.color = c;
            var t = r.transform;
            t.localPosition = new Vector3(x, -y, 0f);
            t.localScale = scale;
            return r;
        }
    }
}
