using System.Collections.Generic;
using UnityEngine;

namespace LineDefense.View
{
    // 점선
    public sealed partial class ShapeBatch
    {
        /// <summary>점선 꺾은선을 그립니다. 점선 길이와 간격은 현재 변환 이전 단위입니다.</summary>
        public void Dashed(IList<Vector2> points, float width, Color color, float dash, float gap, bool closed = false)
        {
            int pointCount = points.Count;
            if (pointCount < 2)
            {
                return;
            }

            int segmentCount = closed ? pointCount : pointCount - 1;
            float into = 0f;
            bool on = true;
            for (int i = 0; i < segmentCount; i++)
            {
                var segmentStart = points[i];
                var segmentEnd = points[(i + 1) % pointCount];
                float length = Vector2.Distance(segmentStart, segmentEnd);
                if (length < 1e-4f)
                {
                    continue;
                }

                var direction = (segmentEnd - segmentStart) / length;
                float segmentOffset = 0f;
                while (segmentOffset < length)
                {
                    float limit = on ? dash : gap;
                    float take = Mathf.Min(limit - into, length - segmentOffset);
                    if (on)
                    {
                        var dashStart = segmentStart + direction * segmentOffset;
                        var dashEnd = segmentStart + direction * (segmentOffset + take);
                        Line(dashStart.x, dashStart.y, dashEnd.x, dashEnd.y, width, color);
                    }
                    segmentOffset += take;
                    into += take;
                    if (into >= limit - 1e-4f)
                    {
                        on = !on;
                        into = 0f;
                    }
                }
            }
        }

        /// <summary>원을 점선으로 그립니다.</summary>
        public void DashedCircle(float x, float y, float radius, float width, Color color, float dash, float gap)
        {
            _scratchPoints.Clear();
            int segmentCount = Mathf.Max(24, Mathf.CeilToInt(radius / 4f));
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i * Tau / segmentCount;
                _scratchPoints.Add(new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius));
            }
            Dashed(_scratchPoints, width, color, dash, gap, true);
        }
    }
}
