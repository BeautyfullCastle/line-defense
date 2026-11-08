using System.Collections.Generic;
using UnityEngine;

namespace LineDefense.View
{
    // 점선, 둥근 사각형, 자원 도형
    public sealed partial class ShapeBatch
    {
        /// <summary>점선 꺾은선을 그립니다. 점선 길이와 간격은 현재 변환 이전 단위입니다.</summary>
        public void Dashed(IList<Vector2> pts, float width, Color col, float dash, float gap, bool closed = false)
        {
            int n = pts.Count;
            if (n < 2) return;
            int segs = closed ? n : n - 1;
            float into = 0f;
            bool on = true;
            for (int i = 0; i < segs; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];
                float len = Vector2.Distance(a, b);
                if (len < 1e-4f) continue;
                var dir = (b - a) / len;
                float pos = 0f;
                while (pos < len)
                {
                    float limit = on ? dash : gap;
                    float take = Mathf.Min(limit - into, len - pos);
                    if (on)
                    {
                        var p0 = a + dir * pos;
                        var p1 = a + dir * (pos + take);
                        Line(p0.x, p0.y, p1.x, p1.y, width, col);
                    }
                    pos += take;
                    into += take;
                    if (into >= limit - 1e-4f) { on = !on; into = 0f; }
                }
            }
        }

        /// <summary>원을 점선으로 그립니다.</summary>
        public void DashedCircle(float x, float y, float r, float width, Color col, float dash, float gap)
        {
            _tmp2.Clear();
            int segs = Mathf.Max(24, Mathf.CeilToInt(r / 4f));
            for (int i = 0; i < segs; i++)
            {
                float a = i * Tau / segs;
                _tmp2.Add(new Vector2(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r));
            }
            Dashed(_tmp2, width, col, dash, gap, true);
        }

        void RoundRectPoints(List<Vector2> o, float x, float y, float w, float h, float r)
        {
            o.Clear();
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            const int steps = 4;
            for (int k = 0; k < 4; k++)
            {
                float a0 = (k - 1) * Mathf.PI * 0.5f;
                float cx = (k == 0 || k == 1) ? x + w - r : x + r;
                float cy = (k == 0 || k == 3) ? y + r : y + h - r;
                for (int i = 0; i <= steps; i++)
                {
                    float a = a0 + i * Mathf.PI * 0.5f / steps;
                    o.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
        }

        /// <summary>둥근 사각형을 채웁니다.</summary>
        public void FillRoundRect(float x, float y, float w, float h, float r, Color col)
        {
            RoundRectPoints(_tmp, x, y, w, h, r);
            FillPoly(_tmp, col);
        }

        /// <summary>둥근 사각형 테두리를 그립니다.</summary>
        public void StrokeRoundRect(float x, float y, float w, float h, float r, float width, Color col)
        {
            RoundRectPoints(_tmp, x, y, w, h, r);
            Polyline(_tmp, width, col, true, true);
        }

        void ShapePoints(List<Vector2> o, ShapeKind kind, float x, float y, float r)
        {
            o.Clear();
            switch (kind)
            {
                case ShapeKind.Triangle:
                    o.Add(new Vector2(x, y - r * 1.15f));
                    o.Add(new Vector2(x + r * 1.05f, y + r * 0.75f));
                    o.Add(new Vector2(x - r * 1.05f, y + r * 0.75f));
                    break;
                case ShapeKind.Hex:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Tau / 6f - Mathf.PI * 0.5f;
                        o.Add(new Vector2(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r));
                    }
                    break;
                default:
                    int segs = CircleSegs(r) + 4;
                    for (int i = 0; i < segs; i++)
                    {
                        float a = i * Tau / segs;
                        o.Add(new Vector2(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r));
                    }
                    break;
            }
        }

        /// <summary>도형을 채웁니다.</summary>
        public void FillShape(ShapeKind kind, float x, float y, float r, Color col)
        {
            if (kind == ShapeKind.Circle) { FillCircle(x, y, r, col); return; }
            ShapePoints(_tmp, kind, x, y, r);
            FillPoly(_tmp, col);
        }

        /// <summary>도형 테두리를 그립니다.</summary>
        public void StrokeShape(ShapeKind kind, float x, float y, float r, float width, Color col)
        {
            if (kind == ShapeKind.Circle) { StrokeCircle(x, y, r, width, col); return; }
            ShapePoints(_tmp, kind, x, y, r);
            Polyline(_tmp, width, col, true, true);
        }

        /// <summary>도형 테두리를 점선으로 그립니다.</summary>
        public void DashedShape(ShapeKind kind, float x, float y, float r, float width, Color col, float dash, float gap)
        {
            ShapePoints(_tmp, kind, x, y, r);
            Dashed(_tmp, width, col, dash, gap, true);
        }
    }
}
