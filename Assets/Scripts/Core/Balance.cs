using System;
using System.Collections.Generic;
using System.Reflection;

namespace LineDefense.Core
{
    /// <summary>밸런스 시트 값을 적용한 결과입니다.</summary>
    public sealed class BalanceApplyResult
    {
        /// <summary>값이 바뀐 상수와 이전·새 값입니다.</summary>
        public readonly List<(string Key, float From, float To)> Changed = new List<(string, float, float)>();

        /// <summary>시트에 있지만 코드에 없는 상수 이름입니다.</summary>
        public readonly List<string> Unknown = new List<string>();

        /// <summary>코드에 있지만 시트에 없어 이전 값을 유지한 상수 이름입니다.</summary>
        public readonly List<string> Missing = new List<string>();

        /// <summary>적용은 했지만 확인이 필요한 내용입니다.</summary>
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// 밸런스 시트의 코드 상수 모음입니다. 필드 이름은 시트의 "코드 상수" 열과 같습니다.
    /// 실제 값은 기본 시트(Resources/BalanceSheet.bytes)에서 읽고, 여기 값은 시트를 못 읽거나 시트에 없는 상수에 씁니다.
    /// </summary>
    public sealed class Balance
    {
        // 마굿간
        public float CART_BASE_SPEED = 64f, CART_SPD_UP = .25f, CART_BASE_CAP = 4f, CART_CAP_UP = 2f, DWELL = .45f;
        // 요새
        public float RANGE = 165f, FORT_ATK = 150f, FORT_CD = .75f, SHOTS_PER_WEAPON = 4f, FORT_FOOD_EVERY = 8f;
        public float MORALE_UP = .2f, MORALE_DOWN = .2f, FORT_START_MORALE = .4f;
        public float FORT_CAP = 4f, FORT_START_FOOD = 2f, FORT_START_WEAPON = 2f;
        // 생산 건물
        public float PROD_RATE = 9.5f, PROD_CAP = 4f, PRODUCER_EVERY = 48f;
        // 창고
        public float WH_BASE_CAP = 6f, WH_CAP_STEP = 3f, WH_MAX_LINES = 0f, WH_RESERVE = .5f, WH_MIN_TRANSFER = 1f;
        // 성벽
        public float WALL_HP = 100f, WALL_UP = 20f, WALL_REPAIR = 40f;
        // 몬스터
        public float MON_BASE = 3f, MON_GROW = 2f, HP_BASE = 200f, HP_GROW = 50f;
        public float SPD_BASE = 16f, SPD_GROW = .9f, MON_SPD_RND = 5f, MON_ATK = .9f;
        public float SPAWN_GAP = .9f, SPAWN_GAP_STEP = .08f, SPAWN_GAP_MIN = .35f;
        // 위협 수준
        public float THREAT_START = .4f, THREAT_GROW = .2f;
        public float THREAT_HP_A = .5f, THREAT_HP_B = .5f, THREAT_SPD_A = .8f, THREAT_SPD_B = .2f;
        // 요새 강화
        public float GOLD_PER_KILL = 2f, UPG_MAX_LV = 5f, UPG_COST_BASE = 20f, UPG_COST_GROW = 20f;
        public float ATK_UP_STEP = .5f, ATK_FOOD_STEP = .3f, SPD_UP_STEP = .4f, SPD_AMMO_STEP = 0f;
        // 웨이브 진행
        public float FIRST_WAVE = 32f, BETWEEN = 24f, ROUTE_EVERY = 3f, ROUTE_MAX = 7f;

        /// <summary>시트 "보급 선택지"에서 읽은 행입니다(ID → 행). null이면 Supply의 기본 표를 씁니다.</summary>
        public IReadOnlyDictionary<string, SupplyRow> SupplyRows;

        static readonly Dictionary<string, FieldInfo> fields = BuildFields();
        static readonly string[] keys = BuildKeys();

        // game.js BAL_META에서 정수 여부가 true인 키
        static readonly HashSet<string> intKeys = new HashSet<string>
        {
            "CART_BASE_CAP", "CART_CAP_UP", "SHOTS_PER_WEAPON", "FORT_CAP", "FORT_START_FOOD", "FORT_START_WEAPON",
            "PROD_CAP", "WH_BASE_CAP", "WH_CAP_STEP", "WH_MAX_LINES", "WH_MIN_TRANSFER", "MON_BASE",
            "UPG_MAX_LV", "ROUTE_EVERY", "ROUTE_MAX",
        };

        static Dictionary<string, FieldInfo> BuildFields()
        {
            var d = new Dictionary<string, FieldInfo>();
            foreach (var f in typeof(Balance).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType == typeof(float)) d[f.Name] = f;
            return d;
        }

        static string[] BuildKeys()
        {
            var list = new List<string>();
            foreach (var f in typeof(Balance).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType == typeof(float)) list.Add(f.Name);
            return list.ToArray();
        }

        /// <summary>모든 키 이름입니다.</summary>
        public static string[] Keys => keys;

        /// <summary>값을 복사한 새 인스턴스를 반환합니다.</summary>
        public Balance Clone() => (Balance)MemberwiseClone();

        /// <summary>이름으로 값을 설정합니다. 없는 키면 false입니다.</summary>
        public bool TrySet(string key, float value)
        {
            if (key == null || !fields.TryGetValue(key, out var f)) return false;
            if (intKeys.Contains(key)) value = MathF.Floor(value + .5f);
            f.SetValue(this, value);
            return true;
        }

        /// <summary>시트에서 읽은 값을 넣고 바뀐 값, 모르는 상수, 빠진 상수를 돌려줍니다.</summary>
        public BalanceApplyResult Apply(IReadOnlyDictionary<string, float> values)
        {
            var r = new BalanceApplyResult();
            foreach (var kv in values)
            {
                if (!TryGet(kv.Key, out float before)) { r.Unknown.Add(kv.Key); continue; }
                TrySet(kv.Key, kv.Value);
                TryGet(kv.Key, out float after);
                if (after != before) r.Changed.Add((kv.Key, before, after));
            }
            foreach (string k in keys) if (!values.ContainsKey(k)) r.Missing.Add(k);
            return r;
        }

        /// <summary>이름으로 값을 읽습니다. 없는 키면 false입니다.</summary>
        public bool TryGet(string key, out float value)
        {
            value = 0f;
            if (key == null || !fields.TryGetValue(key, out var f)) return false;
            value = (float)f.GetValue(this);
            return true;
        }
    }
}
