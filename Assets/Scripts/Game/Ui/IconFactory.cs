using System;
using System.Collections.Generic;
using UnityEngine;

namespace LineDefense.View.Ui
{
    /// <summary>도형 SDF를 직접 래스터화해 아이콘 스프라이트를 만듭니다. 아이콘 좌표는 26x26, y 아래 방향입니다.</summary>
    public static class IconFactory
    {
        const int Scale = 3;
        const int Size = 26 * Scale;
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>키에 해당하는 흰색 아이콘을 돌려줍니다. 색은 Image.color로 입힙니다.</summary>
        public static Sprite Get(string key)
        {
            if (Cache.TryGetValue(key, out var s)) return s;
            var m = new float[Size * Size];
            Draw(key, m);
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    byte a = (byte)(Mathf.Clamp01(m[y * Size + x]) * 255f);
                    px[(Size - 1 - y) * Size + x] = new Color32(255, 255, 255, a);
                }
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = s;
            return s;
        }

        delegate float Sdf(float x, float y);

        static void Op(float[] m, Sdf sdf, bool erase = false)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = sdf((x + 0.5f) / Scale, (y + 0.5f) / Scale);
                    float cov = Mathf.Clamp01(0.5f - d * Scale);
                    int i = y * Size + x;
                    m[i] = erase ? Mathf.Min(m[i], 1f - cov) : Mathf.Max(m[i], cov);
                }
            }
        }

        static Sdf Circle(float cx, float cy, float r) => (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static Sdf Ring(float cx, float cy, float r, float w) => (x, y) => Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - w * 0.5f;

        static float SegDist(float x, float y, float ax, float ay, float bx, float by)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01((px * dx + py * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
            float qx = px - dx * t, qy = py - dy * t;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        static Sdf Seg(float ax, float ay, float bx, float by, float w) => (x, y) => SegDist(x, y, ax, ay, bx, by) - w * 0.5f;

        static Sdf Box(float x0, float y0, float w, float h, float r)
        {
            float cx = x0 + w * 0.5f, cy = y0 + h * 0.5f, hx = w * 0.5f - r, hy = h * 0.5f - r;
            return (x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - hx, qy = Mathf.Abs(y - cy) - hy;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            };
        }

        static Sdf BoxRing(float x0, float y0, float w, float h, float r, float lw)
        {
            var b = Box(x0, y0, w, h, r);
            return (x, y) => Mathf.Abs(b(x, y)) - lw * 0.5f;
        }

        static Sdf Poly(float[] p)
        {
            int n = p.Length / 2;
            return (x, y) =>
            {
                float best = float.MaxValue;
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    float ax = p[i * 2], ay = p[i * 2 + 1], bx = p[j * 2], by = p[j * 2 + 1];
                    best = Mathf.Min(best, SegDist(x, y, ax, ay, bx, by));
                    if ((ay > y) != (by > y) && x < (bx - ax) * (y - ay) / (by - ay) + ax) inside = !inside;
                }
                return inside ? -best : best;
            };
        }

        static Sdf Outline(Sdf s, float w) => (x, y) => Mathf.Abs(s(x, y)) - w * 0.5f;

        static float[] Tri(float r) => new[] { 13f, 13f - r * 1.15f, 13f + r * 1.05f, 13f + r * 0.75f, 13f - r * 1.05f, 13f + r * 0.75f };

        static void Draw(string key, float[] m)
        {
            switch (key)
            {
                case "circle": Op(m, Circle(13, 13, 11)); break;
                case "circleRing": Op(m, Ring(13, 13, 10, 2.4f)); break;
                case "tri": Op(m, Poly(Tri(11f))); break;
                case "triRing": Op(m, Outline(Poly(Tri(10f)), 2.4f)); break;
                case "disc": Op(m, Circle(13, 13, 12.5f)); break;
                case "stable":
                    Op(m, Box(4, 9, 18, 14, 2));
                    Op(m, Seg(2, 11, 13, 3, 2.5f)); Op(m, Seg(13, 3, 24, 11, 2.5f));
                    break;
                case "fort":
                    Op(m, Poly(new[] { 13f, 2f, 23f, 7.5f, 23f, 18.5f, 13f, 24f, 3f, 18.5f, 3f, 7.5f }));
                    Op(m, Circle(13, 13, 4), true);
                    break;
                case "wall":
                    Op(m, Poly(new[] { 3f, 22f, 3f, 9f, 7f, 9f, 7f, 12f, 10f, 12f, 10f, 9f, 16f, 9f, 16f, 12f, 19f, 12f, 19f, 9f, 23f, 9f, 23f, 22f }));
                    break;
                case "cart":
                    Op(m, Box(2, 7, 22, 11, 2)); Op(m, Circle(7.5f, 20.5f, 3)); Op(m, Circle(18.5f, 20.5f, 3));
                    break;
                case "speed":
                    Op(m, Box(8, 7, 16, 11, 2));
                    Op(m, Seg(1, 9, 6, 9, 2)); Op(m, Seg(2, 13, 6, 13, 2)); Op(m, Seg(1, 17, 6, 17, 2));
                    Op(m, Circle(12, 20.5f, 2.6f)); Op(m, Circle(20, 20.5f, 2.6f));
                    break;
                case "wh":
                    Op(m, BoxRing(3, 3, 20, 20, 2, 3));
                    Op(m, Seg(8, 8, 18, 18, 2.4f)); Op(m, Seg(18, 8, 8, 18, 2.4f));
                    break;
                case "whcap":
                    Op(m, BoxRing(2, 8, 13, 13, 1.5f, 2.6f));
                    Op(m, Seg(20, 6, 20, 16, 2.6f)); Op(m, Seg(15, 11, 25, 11, 2.6f));
                    break;
                case "cap":
                    Op(m, Box(2, 11, 22, 9, 2)); Op(m, Box(5, 3, 6, 7, 1)); Op(m, Box(13, 5, 7, 5, 1));
                    break;
                case "gold":
                    Op(m, Ring(13, 13, 10, 2.6f));
                    Op(m, Seg(13, 7, 13, 19, 2f));
                    Op(m, Seg(9.5f, 10.5f, 16.5f, 10.5f, 2f)); Op(m, Seg(9.5f, 15.5f, 16.5f, 15.5f, 2f));
                    break;
                case "capup":
                    Op(m, Seg(4, 22, 22, 22, 2.6f)); Op(m, Seg(13, 18, 13, 5, 2.6f));
                    Op(m, Seg(7.5f, 10.5f, 13, 5, 2.6f)); Op(m, Seg(13, 5, 18.5f, 10.5f, 2.6f));
                    break;
                default:
                    Op(m, Box(4, 4, 18, 18, 3));
                    break;
            }
        }
    }
}
