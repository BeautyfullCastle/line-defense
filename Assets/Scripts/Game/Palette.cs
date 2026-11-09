using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>프로토타입 라이트 테마의 색상 모음입니다.</summary>
    public static class Palette
    {
        public static readonly Color Outside = FromHex(0xD3D8C8);
        public static readonly Color Ground = FromHex(0xF2F3EC);
        public static readonly Color Wall = FromHex(0x585D52);
        public static readonly Color Ink = FromHex(0x23261F);
        public static readonly Color Muted = FromHex(0x6F7568);
        public static readonly Color Fog = FromHex(0xC8CDBC);
        public static readonly Color Panel = FromHex(0xFBFBF6);
        public static readonly Color Line = FromHex(0xC4C9B9);
        public static readonly Color Food = FromHex(0xDE9A12);
        public static readonly Color Weapon = FromHex(0x56677A);
        public static readonly Color Monster = FromHex(0x3B2D44);
        public static readonly Color Danger = FromHex(0xD23E2C);
        public static readonly Color Grid = new Color(0.137f, 0.149f, 0.122f, 0.05f);
        public static readonly Color SpeakerGeneral = FromHex(0x2F4F86);
        public static readonly Color SpeakerAide = FromHex(0x3F7A4B);
        public static readonly Color SpeakerMessenger = FromHex(0x9A5B12);

        private static readonly Color[] s_lineColors =
        {
            FromHex(0xE2463A), FromHex(0x2C78D4), FromHex(0x1F9D63), FromHex(0x8B57D6), FromHex(0xD9508F), FromHex(0x139AA8),
        };

        /// <summary>노선 색 인덱스에 해당하는 색을 돌려줍니다.</summary>
        public static Color LineColor(int index) => s_lineColors[Mathf.Abs(index) % s_lineColors.Length];

        /// <summary>자원 종류별 색을 돌려줍니다.</summary>
        public static Color ColorOf(Res resource) => resource == Res.Food ? Food : Weapon;

        /// <summary>알파만 바꾼 색을 돌려줍니다.</summary>
        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        /// <summary>0xRRGGBB 정수를 색으로 바꿉니다.</summary>
        public static Color FromHex(int hexColor) =>
            new Color(((hexColor >> 16) & 0xFF) / 255f, ((hexColor >> 8) & 0xFF) / 255f, (hexColor & 0xFF) / 255f, 1f);
    }

    /// <summary>Core 좌표형과 UnityEngine 좌표형 사이의 변환 도우미입니다.</summary>
    public static class VecExt
    {
        public static Vector2 ToVector2(this Vec2 coreVector) => new Vector2(coreVector.X, coreVector.Y);
        public static Vec2 ToCore(this Vector2 unityVector) => new Vec2(unityVector.x, unityVector.y);
    }
}
