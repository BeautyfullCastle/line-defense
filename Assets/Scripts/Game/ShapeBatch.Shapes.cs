using System.Collections.Generic;
using UnityEngine;

namespace LineDefense.View
{
    // 점선
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
    }
}
