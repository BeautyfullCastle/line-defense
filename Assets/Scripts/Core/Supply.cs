using System.Collections.Generic;

namespace LineDefense.Core
{
    /// <summary>보급 선택지 한 행입니다. game.js의 SUP_META에 대응합니다.</summary>
    public sealed class SupplyDef
    {
        public string Id;
        public string Key;
        public string Name;
        /// <summary>효과 수치입니다. null이면 밸런스 상수를 사용합니다.</summary>
        public float? Val;
        public float Weight;
        /// <summary>최대 선택 횟수입니다. 0이면 제한 없음입니다.</summary>
        public int Max;
        public int FirstWave;
    }

    /// <summary>보급 선택지 기본 표입니다.</summary>
    public static class Supply
    {
        /// <summary>군자금 지원의 웨이브당 증가량 기본값입니다.</summary>
        public const float SupGoldGrow = 5f;

        public static Dictionary<string, SupplyDef> Build()
        {
            var d = new Dictionary<string, SupplyDef>();
            void Add(string id, string key, string name, float? val, float w, int max, int first)
            {
                d[key] = new SupplyDef { Id = id, Key = key, Name = name, Val = val, Weight = w, Max = max, FirstWave = first };
            }
            Add("SUP_BUILD_STABLE", "stable", "마굿간 건설 권한", 1f, 30, 0, 1);
            Add("SUP_BUILD_WAREHOUSE", "warehouse", "창고 건설 권한", 1f, 25, 0, 2);
            Add("SUP_BUILD_FORT", "fort", "요새 건설 권한", 1f, 25, 0, 1);
            Add("SUP_CART", "cartExtra", "마차 지원", 1f, 25, 0, 1);
            Add("SUP_GOLD", "gold", "군자금 지원", 30f, 20, 0, 1);
            Add("CART_SPD_UP", "cartSpd", "역참 마필 개량", null, 15, 4, 1);
            Add("CART_CAP_UP", "cartCap", "수레 개조", null, 15, 4, 1);
            Add("WH_CAP_STEP", "whCap", "창고 확장", null, 15, 4, 2);
            Add("WALL_REPAIR", "wall", "성벽 보수 자재", null, 15, 0, 2);
            Add("SUP_CAP_STABLE", "capStable", "마굿간 개량 한계 돌파", 1f, 8, 2, 8);
            Add("SUP_CAP_WAREHOUSE", "whUpg", "창고 증축 허가", 1f, 8, 2, 8);
            Add("SUP_CAP_FORT", "capFort", "요새 강화 한계 돌파", 1f, 8, 2, 10);
            return d;
        }
    }
}
