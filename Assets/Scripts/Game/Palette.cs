using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>프로토타입 라이트 테마의 색상 모음입니다.</summary>
    public static class Palette
    {
        public static readonly Color Outside = Rgb(0xD3D8C8);
        public static readonly Color Ground = Rgb(0xF2F3EC);
        public static readonly Color Wall = Rgb(0x585D52);
        public static readonly Color Ink = Rgb(0x23261F);
        public static readonly Color Muted = Rgb(0x6F7568);
        public static readonly Color Fog = Rgb(0xC8CDBC);
        public static readonly Color Panel = Rgb(0xFBFBF6);
        public static readonly Color Line = Rgb(0xC4C9B9);
        public static readonly Color Food = Rgb(0xDE9A12);
        public static readonly Color Weapon = Rgb(0x56677A);
        public static readonly Color Monster = Rgb(0x3B2D44);
        public static readonly Color Danger = Rgb(0xD23E2C);
        public static readonly Color Grid = new Color(0.137f, 0.149f, 0.122f, 0.05f);
        public static readonly Color SpGen = Rgb(0x2F4F86);
        public static readonly Color SpAide = Rgb(0x3F7A4B);
        public static readonly Color SpMsg = Rgb(0x9A5B12);

        static readonly Color[] LineColors =
        {
            Rgb(0xE2463A), Rgb(0x2C78D4), Rgb(0x1F9D63), Rgb(0x8B57D6), Rgb(0xD9508F), Rgb(0x139AA8),
        };

        /// <summary>노선 색 인덱스에 해당하는 색을 돌려줍니다.</summary>
        public static Color LineColor(int index) => LineColors[Mathf.Abs(index) % LineColors.Length];

        /// <summary>자원 종류별 색을 돌려줍니다.</summary>
        public static Color Of(Res r) => r == Res.Food ? Food : Weapon;

        /// <summary>알파만 바꾼 색을 돌려줍니다.</summary>
        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>0xRRGGBB 정수를 색으로 바꿉니다.</summary>
        public static Color Rgb(int hex) =>
            new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);
    }

    /// <summary>Core 좌표형과 UnityEngine 좌표형 사이의 변환 도우미입니다.</summary>
    public static class VecExt
    {
        public static Vector2 ToV(this Vec2 v) => new Vector2(v.x, v.y);
        public static Vec2 ToCore(this Vector2 v) => new Vec2(v.x, v.y);
    }
}
