using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LineDefense.View
{
    /// <summary>y 아래 방향 월드 좌표로 도형을 쌓아 한 개의 동적 메시로 만드는 즉시 모드 배처입니다.</summary>
    public sealed partial class ShapeBatch
    {
        private const float Tau = Mathf.PI * 2f;

        private struct Transform2D
        {
            public float A, B, C, D, E, F;
        }

        private readonly List<Vector3> _verts = new List<Vector3>(1 << 14);
        private readonly List<Color> _cols = new List<Color>(1 << 14);
        private readonly List<int> _tris = new List<int>(1 << 15);
        private readonly Transform2D[] _stack = new Transform2D[24];
        private readonly List<Vector2> _scratchPoints = new List<Vector2>(64);
        private int _depth;
        private Transform2D _current;

        /// <summary>업로드 대상 메시입니다.</summary>
        public Mesh Mesh
        {
            get;
        }

        /// <summary>32비트 인덱스 메시를 가진 배처를 만듭니다.</summary>
        public ShapeBatch(string name)
        {
            Mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            Mesh.MarkDynamic();
            _current = new Transform2D { A = 1, D = 1 };
        }

        /// <summary>쌓인 도형과 변환을 비웁니다.</summary>
        public void Begin()
        {
            _verts.Clear();
            _cols.Clear();
            _tris.Clear();
            _depth = 0;
            _current = new Transform2D { A = 1, D = 1 };
        }

        /// <summary>쌓인 도형을 메시에 올립니다.</summary>
        public void End()
        {
            Mesh.Clear();
            Mesh.SetVertices(_verts);
            Mesh.SetColors(_cols);
            Mesh.SetTriangles(_tris, 0, false);
            Mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e5f, 1e5f, 10f));
        }

        /// <summary>이동, 회전(라디안), 균등 배율 변환을 현재 변환 위에 겹칩니다.</summary>
        public void Push(float x, float y, float rotation = 0f, float scale = 1f)
        {
            _stack[_depth++] = _current;
            float scaledCosine = Mathf.Cos(rotation) * scale, scaledSine = Mathf.Sin(rotation) * scale;
            var newTransform = new Transform2D
            {
                A = _current.A * scaledCosine + _current.C * scaledSine,
                B = _current.B * scaledCosine + _current.D * scaledSine,
                C = -_current.A * scaledSine + _current.C * scaledCosine,
                D = -_current.B * scaledSine + _current.D * scaledCosine,
                E = _current.A * x + _current.C * y + _current.E,
                F = _current.B * x + _current.D * y + _current.F,
            };
            _current = newTransform;
        }

        /// <summary>마지막 변환을 되돌립니다.</summary>
        public void Pop() => _current = _stack[--_depth];

        private int AddVertex(float x, float y, Color color)
        {
            float worldX = _current.A * x + _current.C * y + _current.E;
            float worldY = _current.B * x + _current.D * y + _current.F;
            _verts.Add(new Vector3(worldX, -worldY, 0f));
            _cols.Add(color);
            return _verts.Count - 1;
        }

        private void AddTriangle(int firstIndex, int secondIndex, int thirdIndex)
        {
            _tris.Add(firstIndex);
            _tris.Add(secondIndex);
            _tris.Add(thirdIndex);
        }

        /// <summary>사각형을 채웁니다. 꼭짓점은 순서대로 이어져야 합니다.</summary>
        public void Quad(Vector2 firstCorner, Vector2 secondCorner, Vector2 thirdCorner, Vector2 fourthCorner, Color color)
        {
            int firstIndex = AddVertex(firstCorner.x, firstCorner.y, color), secondIndex = AddVertex(secondCorner.x, secondCorner.y, color), thirdIndex = AddVertex(thirdCorner.x, thirdCorner.y, color), fourthIndex = AddVertex(fourthCorner.x, fourthCorner.y, color);
            AddTriangle(firstIndex, secondIndex, thirdIndex);
            AddTriangle(firstIndex, thirdIndex, fourthIndex);
        }

        /// <summary>축 정렬 사각형을 채웁니다.</summary>
        public void FillRect(float x, float y, float width, float height, Color color)
        {
            Quad(new Vector2(x, y), new Vector2(x + width, y), new Vector2(x + width, y + height), new Vector2(x, y + height), color);
        }

        /// <summary>볼록하거나 별 모양인 다각형을 무게중심 부채꼴로 채웁니다.</summary>
        public void FillPoly(IList<Vector2> points, Color color)
        {
            int pointCount = points.Count;
            if (pointCount < 3)
            {
                return;
            }

            float centerX = 0f, centerY = 0f;
            for (int i = 0; i < pointCount; i++)
            {
                centerX += points[i].x;
                centerY += points[i].y;
            }
            centerX /= pointCount;
            centerY /= pointCount;
            int centerIndex = AddVertex(centerX, centerY, color);
            int first = AddVertex(points[0].x, points[0].y, color), previousVertex = first;
            for (int i = 1; i < pointCount; i++)
            {
                int currentIndex = AddVertex(points[i].x, points[i].y, color);
                AddTriangle(centerIndex, previousVertex, currentIndex);
                previousVertex = currentIndex;
            }
            AddTriangle(centerIndex, previousVertex, first);
        }

        private static int CircleSegmentCount(float radius) => radius < 6f ? 10 : radius < 20f ? 16 : radius < 60f ? 28 : 64;

        /// <summary>원을 채웁니다.</summary>
        public void FillCircle(float x, float y, float radius, Color color, int segmentCount = 0)
        {
            if (segmentCount <= 0)
            {
                segmentCount = CircleSegmentCount(radius);
            }

            int centerIndex = AddVertex(x, y, color);
            int first = AddVertex(x + radius, y, color), previousVertex = first;
            for (int i = 1; i < segmentCount; i++)
            {
                float angle = i * Tau / segmentCount;
                int currentIndex = AddVertex(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius, color);
                AddTriangle(centerIndex, previousVertex, currentIndex);
                previousVertex = currentIndex;
            }
            AddTriangle(centerIndex, previousVertex, first);
        }

        /// <summary>호를 두께를 주어 그립니다. 끝은 평평합니다.</summary>
        public void StrokeArc(float x, float y, float radius, float startAngle, float endAngle, float width, Color color)
        {
            float halfWidth = width * 0.5f;
            float span = Mathf.Abs(endAngle - startAngle);
            int segmentCount = Mathf.Max(6, Mathf.CeilToInt(span * radius / 18f), Mathf.CeilToInt(span / Tau * CircleSegmentCount(radius) * 1.5f));
            int previousInnerIndex = 0, previousOuterIndex = 0;
            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segmentCount), cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
                int innerIndex = AddVertex(x + cosine * (radius - halfWidth), y + sine * (radius - halfWidth), color);
                int outerIndex = AddVertex(x + cosine * (radius + halfWidth), y + sine * (radius + halfWidth), color);
                if (i > 0)
                {
                    AddTriangle(previousInnerIndex, previousOuterIndex, outerIndex);
                    AddTriangle(previousInnerIndex, outerIndex, innerIndex);
                }
                previousInnerIndex = innerIndex;
                previousOuterIndex = outerIndex;
            }
        }

        /// <summary>선분 하나를 그립니다. round가 참이면 양 끝에 둥근 마감을 붙입니다.</summary>
        public void Line(float startX, float startY, float endX, float endY, float width, Color color, bool round = false)
        {
            float dx = endX - startX, dy = endY - startY, length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length > 1e-4f)
            {
                float normalX = -dy / length * width * 0.5f, normalY = dx / length * width * 0.5f;
                Quad(new Vector2(startX + normalX, startY + normalY), new Vector2(endX + normalX, endY + normalY), new Vector2(endX - normalX, endY - normalY), new Vector2(startX - normalX, startY - normalY), color);
            }
            if (round)
            {
                FillCircle(startX, startY, width * 0.5f, color, 10);
                FillCircle(endX, endY, width * 0.5f, color, 10);
            }
        }

        /// <summary>꺾은선을 그립니다. round가 참이면 꼭짓점마다 원을 붙여 이음새를 메웁니다.</summary>
        public void Polyline(IList<Vector2> points, float width, Color color, bool closed = false, bool round = true)
        {
            int pointCount = points.Count;
            if (pointCount < 2)
            {
                return;
            }

            int segmentCount = closed ? pointCount : pointCount - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                var segmentStart = points[i];
                var segmentEnd = points[(i + 1) % pointCount];
                Line(segmentStart.x, segmentStart.y, segmentEnd.x, segmentEnd.y, width, color);
            }
            if (!round)
            {
                return;
            }

            for (int i = 0; i < pointCount; i++)
            {
                FillCircle(points[i].x, points[i].y, width * 0.5f, color, 10);
            }
        }
    }
}
