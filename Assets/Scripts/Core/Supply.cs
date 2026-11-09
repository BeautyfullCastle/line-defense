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
        public float? Value;
        public float Weight;
        /// <summary>최대 선택 횟수입니다. 0이면 제한 없음입니다.</summary>
        public int MaxPicks;
        public int FirstWave;
    }

    /// <summary>밸런스 시트 "보급 선택지"의 한 행입니다. 빈 칸은 null입니다.</summary>
    public sealed class SupplyRow
    {
        public float? Value;
        public float? Weight;
        public int? MaxPicks;
        public int? FirstWave;
    }

    /// <summary>보급 선택지 기본 표입니다.</summary>
    public static class Supply
    {
        /// <summary>군자금 지원의 웨이브당 증가량 기본값입니다.</summary>
        public const float DefaultGoldGrowth = 5f;

        /// <summary>시트에서 군자금 웨이브당 증가량을 담는 행의 ID입니다. 선택지가 아닙니다.</summary>
        public const string GoldGrowId = "SUP_GOLD_GROW";

        /// <summary>
        /// 기본 표에 시트 행을 덮어쓴 표를 만듭니다. rows가 null이면 기본 표입니다.
        /// 최대 선택 횟수가 빈 칸이면 제한 없음(0)입니다. 효과 값은 밸런스 상수를 쓰지 않는 선택지에만 넣습니다.
        /// 코드에 없는 ID는 unknown에 넣습니다.
        /// </summary>
        public static Dictionary<string, SupplyDef> Build(IReadOnlyDictionary<string, SupplyRow> rows, out float goldGrow, List<string> unknown = null)
        {
            var supplies = Build();
            goldGrow = DefaultGoldGrowth;
            if (rows == null)
            {
                return supplies;
            }

            var byId = new Dictionary<string, SupplyDef>();
            foreach (var supply in supplies.Values)
            {
                byId[supply.Id] = supply;
            }

            foreach (var pair in rows)
            {
                if (pair.Key == GoldGrowId)
                {
                    goldGrow = pair.Value.Value ?? goldGrow;
                    continue;
                }
                if (byId.TryGetValue(pair.Key, out var supply))
                {
                    Overwrite(supply, pair.Value);
                }
                else
                {
                    unknown?.Add(pair.Key);
                }
            }
            return supplies;
        }

        private static void Overwrite(SupplyDef supply, SupplyRow row)
        {
            if (supply.Value != null && row.Value != null)
            {
                supply.Value = row.Value;
            }

            if (row.Weight != null)
            {
                supply.Weight = row.Weight.Value;
            }

            supply.MaxPicks = row.MaxPicks ?? 0;
            if (row.FirstWave != null)
            {
                supply.FirstWave = row.FirstWave.Value;
            }
        }

        /// <summary>두 표에서 값이 다른 항목을 "ID 항목" 이름으로 changed에 넣습니다.</summary>
        public static void Diff(Dictionary<string, SupplyDef> from, Dictionary<string, SupplyDef> target, List<(string Key, float From, float To)> changed)
        {
            foreach (var pair in target)
            {
                if (!from.TryGetValue(pair.Key, out var supply))
                {
                    continue;
                }

                var updatedSupply = pair.Value;
                void Compare(string what, float x, float y)
                {
                    if (x != y)
                    {
                        changed.Add((updatedSupply.Id + " " + what, x, y));
                    }
                }
                Compare("값", supply.Value ?? 0f, updatedSupply.Value ?? 0f);
                Compare("등장 가중치", supply.Weight, updatedSupply.Weight);
                Compare("최대 선택 횟수", supply.MaxPicks, updatedSupply.MaxPicks);
                Compare("첫 등장 웨이브", supply.FirstWave, updatedSupply.FirstWave);
            }
        }

        public static Dictionary<string, SupplyDef> Build()
        {
            var supplies = new Dictionary<string, SupplyDef>();
            void Add(string id, string key, string name, float? value, float weight, int max, int first)
            {
                supplies[key] = new SupplyDef { Id = id, Key = key, Name = name, Value = value, Weight = weight, MaxPicks = max, FirstWave = first };
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
            return supplies;
        }
    }
}
