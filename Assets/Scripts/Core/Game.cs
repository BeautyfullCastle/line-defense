using System;
using System.Collections.Generic;
using System.Globalization;

namespace LineDefense.Core
{
    /// <summary>
    /// 시뮬레이션 상태와 명령을 가진 게임 본체입니다. 생성자가 NewGame을 한 번 호출합니다.
    /// y축은 성벽 꼭대기(0)에서 안쪽이 +입니다.
    /// </summary>
    public sealed partial class Game
    {
        public const float RW = 1200f;
        public const float HW0 = 420f, D0 = 430f;
        public const float HW_MAX = 1100f, D_MAX = 1000f, EXPAND_HW = 140f, EXPAND_D = 100f;
        public const int EXPAND_EVERY = 4;
        public const float DEN = 200f;
        public const int REQ_PER_ROW = 4;
        public const float WALL_SNAP = 70f;
        public static readonly int LineColorCount = 6;

        public Balance B;
        public IReadOnlyDictionary<string, SupplyDef> Sup;
        /// <summary>군자금 지원의 웨이브당 증가량입니다.</summary>
        public float SupGoldGrow;

        // 상태
        public float T, HW, D;
        public List<Node> Nodes = new List<Node>();
        public List<Line> Lines = new List<Line>();
        public List<Monster> Mons = new List<Monster>();
        public List<Route> Routes = new List<Route>();
        public float Wall, WallMax, Flash;
        public int Wave;
        public Phase Phase;
        public float Count;
        public int QueueCount => queue.Count;
        public int TokStable, TokFort, TokWarehouse, TokCart;
        public int CartSpdLv, CartCapLv, WhCapLv, WhUpgBonus, CapStable, CapFort;
        public float Gold;
        public int Kills, Delivered;
        public bool Over => Phase == Phase.Over;
        /// <summary>새 생산처가 나오기까지 남은 시간입니다.</summary>
        public float ProdT;

        public List<GameEvent> Events = new List<GameEvent>();
        /// <summary>false면 이벤트를 쌓지 않습니다. 시뮬레이션 전용입니다.</summary>
        public bool EmitEvents = true;

        public bool CheatInvincible;

        readonly Random rng;
        readonly Queue<(float a, float s)> queue = new Queue<(float a, float s)>();
        readonly Dictionary<string, float> flags = new Dictionary<string, float>();
        readonly Dictionary<string, int> picks = new Dictionary<string, int>();
        float spawnT;
        int nid;
        int runs;

        public Game(Balance b = null, int seed = 0)
        {
            B = b ?? new Balance();
            rng = seed == 0 ? new Random() : new Random(seed);
            Sup = Supply.Build(B.SupplyRows, out SupGoldGrow);
            BuildRewards();
            NewGame();
        }

        // ---------- 유틸 ----------

        float Rnd(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>JS Math.round와 같은 반올림(0.5는 올림)입니다.</summary>
        public static int I(float v) => (int)MathF.Floor(v + .5f);

        public static string FmtNum(float v)
        {
            if (v == MathF.Floor(v)) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return Math.Round((double)v, 4).ToString("0.####", CultureInfo.InvariantCulture);
        }

        void Emit(GameEvent e)
        {
            if (EmitEvents) Events.Add(e);
        }

        List<GameEvent> _drained = new List<GameEvent>();

        /// <summary>쌓인 이벤트를 꺼내고 비웁니다.</summary>
        /// <remarks>반환한 목록은 다음 호출에서 재사용되므로 그 전에 다 읽어야 합니다.</remarks>
        public List<GameEvent> DrainEvents()
        {
            (Events, _drained) = (_drained, Events);
            Events.Clear();
            return _drained;
        }

        void Say(string[][] lines, bool block = false, string key = null)
        {
            Emit(new GameEvent { Type = GameEventType.Say, Lines = lines, Block = block, Key = key });
        }

        /// <summary>cd가 0이면 한 번만, 아니면 cd초 간격으로 대사를 낸다.</summary>
        void Bark(string key, string[][] lines, float cd = 0f)
        {
            if (flags.TryGetValue(key, out float t) && (cd <= 0f || T - t < cd)) return;
            flags[key] = T;
            Emit(new GameEvent { Type = GameEventType.Bark, Key = key, Lines = lines });
        }

        void Toast(string text)
        {
            Emit(new GameEvent { Type = GameEventType.Toast, Text = text });
        }

        public Node NodeById(int id)
        {
            for (int i = 0; i < Nodes.Count; i++) if (Nodes[i].Id == id) return Nodes[i];
            return null;
        }

        public Line LineById(int id)
        {
            for (int i = 0; i < Lines.Count; i++) if (Lines[i].Id == id) return Lines[i];
            return null;
        }

        public IEnumerable<Line> LinesWith(int nodeId)
        {
            for (int i = 0; i < Lines.Count; i++) if (Lines[i].Nodes.Contains(nodeId)) yield return Lines[i];
        }

        /// <summary>노드를 지나는 노선 수입니다.</summary>
        public int LineCountWith(int nodeId)
        {
            int n = 0;
            for (int i = 0; i < Lines.Count; i++) if (Lines[i].Nodes.Contains(nodeId)) n++;
            return n;
        }

        // ---------- 파생 수치 ----------

        public float CartSpeed() => B.CART_BASE_SPEED * (1f + B.CART_SPD_UP * CartSpdLv);
        public int CartCap() => I(B.CART_BASE_CAP + B.CART_CAP_UP * CartCapLv);
        public int WhCap() => I(B.WH_BASE_CAP + B.WH_CAP_STEP * WhCapLv);
        public float FortAtk(Node f) => B.FORT_ATK * (1f + B.ATK_UP_STEP * f.AtkLv);
        public float FortFoodEvery(Node f) => B.FORT_FOOD_EVERY / (1f + B.ATK_FOOD_STEP * f.AtkLv);
        public float FortCd(Node f) => B.FORT_CD / (1f + B.SPD_UP_STEP * f.SpdLv);
        public float FortShotsPerWeapon(Node f) => I(B.SHOTS_PER_WEAPON) / (1f + B.SPD_AMMO_STEP * f.SpdLv);
        public int FortDmg(Node f) => (int)Math.Floor((double)FortAtk(f) * f.Morale + .5);
        public int UpgCost(int lv) => I(B.UPG_COST_BASE + B.UPG_COST_GROW * lv);
        public int UpgMax() => I(B.UPG_MAX_LV) + (int)(CapFort * SupVal("capFort"));

        float SupVal(string key) => Sup[key].Val ?? 0f;
        float SupCap(string key) => Sup[key].Max > 0 ? Sup[key].Max : float.PositiveInfinity;
        float SpdMax() => SupCap("cartSpd") + CapStable * SupVal("capStable");
        float CapMax() => SupCap("cartCap") + CapStable * SupVal("capStable");
        float WhUpgMax() => SupCap("whCap") + WhUpgBonus * SupVal("whUpg");
        int GoldAmt() => I(SupVal("gold") + SupGoldGrow * Math.Max(0, Wave - 1));

        /// <summary>침투로 위협도입니다. 처음 열린 웨이브 40%에서 시작해 웨이브마다 늘고 최대 100%입니다.</summary>
        public float RouteThreat(Route r, int wave) =>
            MathF.Max(B.THREAT_START, MathF.Min(1f, B.THREAT_START + B.THREAT_GROW * (wave - r.W0 - 1)));

        public float RouteThreatForDisplay(Route r) => RouteThreat(r, Phase == Phase.Wave ? Wave : Wave + 1);

        // ---------- 새 게임 ----------

        /// <summary>상태를 처음으로 되돌립니다.</summary>
        public void NewGame()
        {
            T = 0f; HW = HW0; D = D0;
            Nodes.Clear(); Lines.Clear(); Mons.Clear(); Routes.Clear();
            Wall = WallMax = B.WALL_HP; Flash = 0f;
            TokStable = 2; TokFort = 2; TokWarehouse = 0; TokCart = 0;
            CartSpdLv = CartCapLv = WhCapLv = WhUpgBonus = CapStable = CapFort = 0;
            picks.Clear();
            Wave = 0; Phase = Phase.Countdown; Count = B.FIRST_WAVE;
            queue.Clear(); spawnT = 0f;
            ProdT = B.PRODUCER_EVERY;
            nid = 1; Kills = 0; Delivered = 0; Gold = 0f;
            flags.Clear();
            RewardChoices.Clear();
            Events.Clear();
            AddRoute(true);
            SpawnProducer(NodeType.Farm);
            SpawnProducer(NodeType.Forge);
            Say(runs++ > 0 ? LineTexts.Restart : LineTexts.Story, true);
        }
    }
}
