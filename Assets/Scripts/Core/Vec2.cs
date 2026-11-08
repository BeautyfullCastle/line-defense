using System;

namespace LineDefense.Core
{
    /// <summary>엔진에 의존하지 않는 2차원 벡터입니다.</summary>
    public struct Vec2
    {
        public float x, y;

        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>두 점 사이의 거리를 반환합니다.</summary>
        public static float Dist(Vec2 a, Vec2 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>벡터의 길이입니다.</summary>
        public float Length => MathF.Sqrt(x * x + y * y);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.x + b.x, a.y + b.y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.x - b.x, a.y - b.y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.x * s, a.y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.x * s, a.y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.x / s, a.y / s);

        public override string ToString() => "(" + x.ToString("0.##") + ", " + y.ToString("0.##") + ")";
    }
}
