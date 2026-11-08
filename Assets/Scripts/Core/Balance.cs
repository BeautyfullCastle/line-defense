using System;
using System.Collections.Generic;
using System.Reflection;

namespace LineDefense.Core
{
    /// <summary>밸런스 시트의 코드 상수 모음입니다. 필드 이름은 game.js의 BAL_DEFAULT 키와 같습니다.</summary>
    public sealed class Balance
    {
        // 마굿간
        public float CART_BASE_SPEED = 64f, CART_SPD_UP = .25f, CART_BASE_CAP = 4f, CART_CAP_UP = 2f, DWELL = .45f;
        // 요새
        public float RANGE = 165f, FORT_ATK = 150f, FORT_CD = .75f, SHOTS_PER_WEAPON = 4f, FORT_FOOD_EVERY = 12f;
        public float MORALE_UP = .2f, MORALE_DOWN = .2f, FORT_START_MORALE = .6f;
        public float FORT_CAP = 4f, FORT_START_FOOD = 2f, FORT_START_WEAPON = 2f;
        // 생산 건물
        public float PROD_RATE = 7.5f, PROD_CAP = 4f, PRODUCER_EVERY = 48f;
        // 창고
        public float WH_BASE_CAP = 6f, WH_CAP_STEP = 3f, WH_MAX_LINES = 0f, WH_RESERVE = .5f, WH_MIN_TRANSFER = 1f;
        // 성벽
        public float WALL_HP = 100f, WALL_UP = 20f, WALL_REPAIR = 40f;
        // 몬스터
        public float MON_BASE = 3f, MON_GROW = 2f, HP_BASE = 200f, HP_GROW = 50f;
        public float SPD_BASE = 16f, SPD_GROW = .9f, MON_SPD_RND = 5f, MON_ATK = .75f;
        public float SPAWN_GAP = .9f, SPAWN_GAP_STEP = .08f, SPAWN_GAP_MIN = .35f;
        // 위협 수준
        public float THREAT_START = .4f, THREAT_GROW = .2f;
        public float THREAT_HP_A = .5f, THREAT_HP_B = .5f, THREAT_SPD_A = .8f, THREAT_SPD_B = .2f;
        // 요새 강화
        public float GOLD_PER_KILL = 2f, UPG_MAX_LV = 5f, UPG_COST_BASE = 20f, UPG_COST_GROW = 20f;
        public float ATK_UP_STEP = .5f, ATK_FOOD_STEP = .3f, SPD_UP_STEP = .4f, SPD_AMMO_STEP = 0f;
        // 웨이브 진행
        public float FIRST_WAVE = 32f, BETWEEN = 24f, ROUTE_EVERY = 3f, ROUTE_MAX = 7f;

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
