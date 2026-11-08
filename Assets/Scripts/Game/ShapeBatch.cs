using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LineDefense.View
{
    /// <summary>도형 종류입니다. 식량/농장은 원, 무기/대장간은 삼각형, 요새는 육각형입니다.</summary>
    public enum ShapeKind { Circle, Triangle, Hex }

    /// <summary>y 아래 방향 월드 좌표로 도형을 쌓아 한 개의 동적 메시로 만드는 즉시 모드 배처입니다.</summary>
    public sealed partial class ShapeBatch
    {
        const float Tau = Mathf.PI * 2f;

        struct Xf { public float a, b, c, d, e, f, s; }

        readonly List<Vector3> _verts = new List<Vector3>(1 << 14);
        readonly List<Color> _cols = new List<Color>(1 << 14);
        readonly List<int> _tris = new List<int>(1 << 15);
        readonly Xf[] _stack = new Xf[24];
        readonly List<Vector2> _tmp = new List<Vector2>(64);
        readonly List<Vector2> _tmp2 = new List<Vector2>(64);
        int _depth;
        Xf _cur;

        /// <summary>업로드 대상 메시입니다.</summary>
        public Mesh Mesh { get; }

        /// <summary>이후 모든 도형의 알파에 곱해지는 값입니다.</summary>
        public float Alpha = 1f;

        /// <summary>현재 변환의 균등 배율입니다.</summary>
        public float Scale => _cur.s;

        /// <summary>32비트 인덱스 메시를 가진 배처를 만듭니다.</summary>
        public ShapeBatch(string name)
        {
            Mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            Mesh.MarkDynamic();
            _cur = new Xf { a = 1, d = 1, s = 1 };
        }

        /// <summary>쌓인 도형과 변환을 비웁니다.</summary>
        public void Begin()
        {
            _verts.Clear();
            _cols.Clear();
            _tris.Clear();
            _depth = 0;
            _cur = new Xf { a = 1, d = 1, s = 1 };
            Alpha = 1f;
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
        public void Push(float x, float y, float rot = 0f, float scale = 1f)
        {
            _stack[_depth++] = _cur;
            float cs = Mathf.Cos(rot) * scale, sn = Mathf.Sin(rot) * scale;
            var n = new Xf
            {
                a = _cur.a * cs + _cur.c * sn,
                b = _cur.b * cs + _cur.d * sn,
                c = -_cur.a * sn + _cur.c * cs,
                d = -_cur.b * sn + _cur.d * cs,
                e = _cur.a * x + _cur.c * y + _cur.e,
                f = _cur.b * x + _cur.d * y + _cur.f,
                s = _cur.s * scale,
            };
            _cur = n;
        }

        /// <summary>마지막 변환을 되돌립니다.</summary>
        public void Pop() => _cur = _stack[--_depth];

        int Vtx(float x, float y, Color col)
        {
            float wx = _cur.a * x + _cur.c * y + _cur.e;
            float wy = _cur.b * x + _cur.d * y + _cur.f;
            col.a *= Alpha;
            _verts.Add(new Vector3(wx, -wy, 0f));
            _cols.Add(col);
            return _verts.Count - 1;
        }

        void Idx(int a, int b, int c)
        {
            _tris.Add(a);
            _tris.Add(b);
            _tris.Add(c);
        }

        /// <summary>삼각형을 채웁니다.</summary>
        public void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color col)
        {
            Idx(Vtx(ax, ay, col), Vtx(bx, by, col), Vtx(cx, cy, col));
        }

        /// <summary>사각형을 채웁니다. 꼭짓점은 순서대로 이어져야 합니다.</summary>
        public void Quad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color col)
        {
            int i0 = Vtx(p0.x, p0.y, col), i1 = Vtx(p1.x, p1.y, col), i2 = Vtx(p2.x, p2.y, col), i3 = Vtx(p3.x, p3.y, col);
            Idx(i0, i1, i2);
            Idx(i0, i2, i3);
        }

        /// <summary>축 정렬 사각형을 채웁니다.</summary>
        public void FillRect(float x, float y, float w, float h, Color col)
        {
            Quad(new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h), col);
        }

        /// <summary>볼록하거나 별 모양인 다각형을 무게중심 부채꼴로 채웁니다.</summary>
        public void FillPoly(IList<Vector2> pts, Color col)
        {
            int n = pts.Count;
            if (n < 3) return;
            float cx = 0f, cy = 0f;
            for (int i = 0; i < n; i++) { cx += pts[i].x; cy += pts[i].y; }
            cx /= n; cy /= n;
            int c = Vtx(cx, cy, col);
            int first = Vtx(pts[0].x, pts[0].y, col), prev = first;
            for (int i = 1; i < n; i++)
            {
                int cur = Vtx(pts[i].x, pts[i].y, col);
                Idx(c, prev, cur);
                prev = cur;
            }
            Idx(c, prev, first);
        }

        static int CircleSegs(float r) => r < 6f ? 10 : r < 20f ? 16 : r < 60f ? 28 : 64;

        /// <summary>원을 채웁니다.</summary>
        public void FillCircle(float x, float y, float r, Color col, int segs = 0)
        {
            if (segs <= 0) segs = CircleSegs(r);
            int c = Vtx(x, y, col);
            int first = Vtx(x + r, y, col), prev = first;
            for (int i = 1; i < segs; i++)
            {
                float a = i * Tau / segs;
                int cur = Vtx(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r, col);
                Idx(c, prev, cur);
                prev = cur;
            }
            Idx(c, prev, first);
        }

        /// <summary>부채꼴(반원 포함)을 채웁니다.</summary>
        public void FillSector(float x, float y, float r, float a0, float a1, Color col)
        {
            int segs = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / Tau * CircleSegs(r)));
            int c = Vtx(x, y, col);
            int prev = Vtx(x + Mathf.Cos(a0) * r, y + Mathf.Sin(a0) * r, col);
            for (int i = 1; i <= segs; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)segs);
                int cur = Vtx(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r, col);
                Idx(c, prev, cur);
                prev = cur;
            }
        }

        /// <summary>호를 두께를 주어 그립니다. 끝은 평평합니다.</summary>
        public void StrokeArc(float x, float y, float r, float a0, float a1, float width, Color col)
        {
            float h = width * 0.5f;
            float span = Mathf.Abs(a1 - a0);
            int segs = Mathf.Max(6, Mathf.CeilToInt(span * r / 18f), Mathf.CeilToInt(span / Tau * CircleSegs(r) * 1.5f));
            int pi = 0, po = 0;
            for (int i = 0; i <= segs; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)segs), cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                int ii = Vtx(x + cs * (r - h), y + sn * (r - h), col);
                int io = Vtx(x + cs * (r + h), y + sn * (r + h), col);
                if (i > 0)
                {
                    Idx(pi, po, io);
                    Idx(pi, io, ii);
                }
                pi = ii; po = io;
            }
        }

        /// <summary>원 테두리를 그립니다.</summary>
        public void StrokeCircle(float x, float y, float r, float width, Color col)
        {
            StrokeArc(x, y, r, 0f, Tau, width, col);
        }

        /// <summary>선분 하나를 그립니다. round가 참이면 양 끝에 둥근 마감을 붙입니다.</summary>
        public void Line(float ax, float ay, float bx, float by, float width, Color col, bool round = false)
        {
            float dx = bx - ax, dy = by - ay, len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len > 1e-4f)
            {
                float nx = -dy / len * width * 0.5f, ny = dx / len * width * 0.5f;
                Quad(new Vector2(ax + nx, ay + ny), new Vector2(bx + nx, by + ny), new Vector2(bx - nx, by - ny), new Vector2(ax - nx, ay - ny), col);
            }
            if (round)
            {
                FillCircle(ax, ay, width * 0.5f, col, 10);
                FillCircle(bx, by, width * 0.5f, col, 10);
            }
        }

        /// <summary>꺾은선을 그립니다. round가 참이면 꼭짓점마다 원을 붙여 이음새를 메웁니다.</summary>
        public void Polyline(IList<Vector2> pts, float width, Color col, bool closed = false, bool round = true)
        {
            int n = pts.Count;
            if (n < 2) return;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];
                Line(a.x, a.y, b.x, b.y, width, col);
            }
            if (!round) return;
            for (int i = 0; i < n; i++)
            {
                FillCircle(pts[i].x, pts[i].y, width * 0.5f, col, 10);
            }
        }
    }
}
