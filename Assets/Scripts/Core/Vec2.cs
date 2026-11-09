using System;

namespace LineDefense.Core
{
    /// <summary>엔진에 의존하지 않는 2차원 벡터입니다.</summary>
    public struct Vec2
    {
        public float X, Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>두 점 사이의 거리를 반환합니다.</summary>
        public static float Distance(Vec2 first, Vec2 second)
        {
            float dx = first.X - second.X, dy = first.Y - second.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>벡터의 길이입니다.</summary>
        public float Length => MathF.Sqrt(X * X + Y * Y);

        public static Vec2 operator +(Vec2 left, Vec2 right) => new Vec2(left.X + right.X, left.Y + right.Y);
        public static Vec2 operator -(Vec2 left, Vec2 right) => new Vec2(left.X - right.X, left.Y - right.Y);
        public static Vec2 operator *(Vec2 vector, float factor) => new Vec2(vector.X * factor, vector.Y * factor);
        public static Vec2 operator *(float factor, Vec2 vector) => new Vec2(vector.X * factor, vector.Y * factor);
        public static Vec2 operator /(Vec2 vector, float divisor) => new Vec2(vector.X / divisor, vector.Y / divisor);

        public override string ToString() => "(" + X.ToString("0.##") + ", " + Y.ToString("0.##") + ")";
    }
}
