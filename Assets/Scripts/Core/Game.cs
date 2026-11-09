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
        public const float WallRadius = 1200f;
        public const float StartHalfWidth = 420f, StartDepth = 430f;
        public const float MaxHalfWidth = 1100f, MaxDepth = 1000f, ExpandHalfWidth = 140f, ExpandDepth = 100f;
        public const int ExpandEvery = 4;
        public const float DenDistance = 200f;
        public const int SlotsPerRow = 4;
        public const float WallSnap = 70f;
        public static readonly int LineColorCount = 6;

        public Balance Balance;
        public IReadOnlyDictionary<string, SupplyDef> Supplies;
        /// <summary>군자금 지원의 웨이브당 증가량입니다.</summary>
        public float GoldSupplyGrowth;

        // 상태
        public float ElapsedTime, HalfWidth, Depth;
        public List<Node> Nodes = new List<Node>();
        public List<Line> Lines = new List<Line>();
        public List<Monster> Monsters = new List<Monster>();
        public List<Route> Routes = new List<Route>();
        public float Wall, WallMax, Flash;
        public int Wave;
        public Phase Phase;
        public float Countdown;
        public int QueueCount => _spawnQueue.Count;
        public int StableTokens, FortTokens, WarehouseTokens, CartTokens;
        public int CartSpeedLevel, CartCapacityLevel, WarehouseCapacityLevel, WarehouseLimitBreaks, StableLimitBreaks, FortLimitBreaks;
        public float Gold;
        public int Kills, Delivered;
        public bool Over => Phase == Phase.Over;
        /// <summary>새 생산처가 나오기까지 남은 시간입니다.</summary>
        public float ProducerTimer;

        public List<GameEvent> Events = new List<GameEvent>();
        /// <summary>false면 이벤트를 쌓지 않습니다. 시뮬레이션 전용입니다.</summary>
        public bool EmitEvents = true;

        public bool CheatInvincible;

        private readonly Random _random;
        private readonly Queue<(float Angle, float Threat)> _spawnQueue = new Queue<(float Angle, float Threat)>();
        private readonly Dictionary<string, float> _barkTimes = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _pickCounts = new Dictionary<string, int>();
        private float _spawnTimer;
        private int _nextNodeId;
        private int _gamesStarted;

        public Game(Balance balance = null, int seed = 0)
        {
            Balance = balance ?? new Balance();
            _random = seed == 0 ? new Random() : new Random(seed);
            Supplies = Supply.Build(Balance.SupplyRows, out GoldSupplyGrowth);
            BuildRewards();
            NewGame();
        }

        // ---------- 유틸 ----------

        private float RandomRange(float minimum, float maximum) => minimum + (float)_random.NextDouble() * (maximum - minimum);

        /// <summary>JS Math.round와 같은 반올림(0.5는 올림)입니다.</summary>
        public static int RoundToInt(float value) => (int)MathF.Floor(value + .5f);

        public static string FormatNumber(float value)
        {
            if (value == MathF.Floor(value))
            {
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            }

            return Math.Round((double)value, 4).ToString("0.####", CultureInfo.InvariantCulture);
        }

        private void Emit(GameEvent gameEvent)
        {
            if (EmitEvents)
            {
                Events.Add(gameEvent);
            }
        }

        private List<GameEvent> _drained = new List<GameEvent>();

        /// <summary>쌓인 이벤트를 꺼내고 비웁니다.</summary>
        /// <remarks>반환한 목록은 다음 호출에서 재사용되므로 그 전에 다 읽어야 합니다.</remarks>
        public List<GameEvent> DrainEvents()
        {
            (Events, _drained) = (_drained, Events);
            Events.Clear();
            return _drained;
        }

        private void Say(string[][] lines, bool block = false, string key = null)
        {
            Emit(new GameEvent { Type = GameEventType.Say, Lines = lines, Block = block, Key = key });
        }

        /// <summary>cd가 0이면 한 번만, 아니면 cd초 간격으로 대사를 낸다.</summary>
        private void Bark(string key, string[][] lines, float cooldown = 0f)
        {
            if (_barkTimes.TryGetValue(key, out float t) && (cooldown <= 0f || ElapsedTime - t < cooldown))
            {
                return;
            }

            _barkTimes[key] = ElapsedTime;
            Emit(new GameEvent { Type = GameEventType.Bark, Key = key, Lines = lines });
        }

        private void Toast(string text)
        {
            Emit(new GameEvent { Type = GameEventType.Toast, Text = text });
        }

        public Node NodeById(int id)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Id == id)
                {
                    return Nodes[i];
                }
            }

            return null;
        }

        public Line LineById(int id)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Id == id)
                {
                    return Lines[i];
                }
            }

            return null;
        }

        public IEnumerable<Line> LinesWith(int nodeId)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Nodes.Contains(nodeId))
                {
                    yield return Lines[i];
                }
            }
        }

        /// <summary>노드를 지나는 노선 수입니다.</summary>
        public int LineCountWith(int nodeId)
        {
            int count = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Nodes.Contains(nodeId))
                {
                    count++;
                }
            }

            return count;
        }

        // ---------- 파생 수치 ----------

        public float CartSpeed() => Balance.CART_BASE_SPEED * (1f + Balance.CART_SPD_UP * CartSpeedLevel);
        public int CartCapacity() => RoundToInt(Balance.CART_BASE_CAP + Balance.CART_CAP_UP * CartCapacityLevel);
        public int WarehouseCapacity() => RoundToInt(Balance.WH_BASE_CAP + Balance.WH_CAP_STEP * WarehouseCapacityLevel);
        public float FortAttack(Node fort) => Balance.FORT_ATK * (1f + Balance.ATK_UP_STEP * fort.AttackLevel);
        public float FortFoodEvery(Node fort) => Balance.FORT_FOOD_EVERY / (1f + Balance.ATK_FOOD_STEP * fort.AttackLevel);
        public float FortCooldown(Node fort) => Balance.FORT_CD / (1f + Balance.SPD_UP_STEP * fort.SpeedLevel);
        public float FortShotsPerWeapon(Node fort) => RoundToInt(Balance.SHOTS_PER_WEAPON) / (1f + Balance.SPD_AMMO_STEP * fort.SpeedLevel);
        public int FortDamage(Node fort) => (int)Math.Floor((double)FortAttack(fort) * fort.Morale + .5);
        public int UpgradeCost(int level) => RoundToInt(Balance.UPG_COST_BASE + Balance.UPG_COST_GROW * level);
        public int MaxUpgradeLevel() => RoundToInt(Balance.UPG_MAX_LV) + (int)(FortLimitBreaks * SupplyValue("capFort"));

        private float SupplyValue(string key) => Supplies[key].Value ?? 0f;
        private float SupplyLimit(string key) => Supplies[key].MaxPicks > 0 ? Supplies[key].MaxPicks : float.PositiveInfinity;
        private float MaxCartSpeedLevel() => SupplyLimit("cartSpd") + StableLimitBreaks * SupplyValue("capStable");
        private float MaxCartCapacityLevel() => SupplyLimit("cartCap") + StableLimitBreaks * SupplyValue("capStable");
        private float MaxWarehouseCapacityLevel() => SupplyLimit("whCap") + WarehouseLimitBreaks * SupplyValue("whUpg");
        private int GoldSupplyAmount() => RoundToInt(SupplyValue("gold") + GoldSupplyGrowth * Math.Max(0, Wave - 1));

        /// <summary>침투로 위협도입니다. 처음 열린 웨이브 40%에서 시작해 웨이브마다 늘고 최대 100%입니다.</summary>
        public float RouteThreat(Route route, int wave) =>
            MathF.Max(Balance.THREAT_START, MathF.Min(1f, Balance.THREAT_START + Balance.THREAT_GROW * (wave - route.OpenedWave - 1)));

        public float RouteThreatForDisplay(Route route) => RouteThreat(route, Phase == Phase.Wave ? Wave : Wave + 1);

        // ---------- 새 게임 ----------

        /// <summary>상태를 처음으로 되돌립니다.</summary>
        public void NewGame()
        {
            ElapsedTime = 0f;
            HalfWidth = StartHalfWidth;
            Depth = StartDepth;
            Nodes.Clear();
            Lines.Clear();
            Monsters.Clear();
            Routes.Clear();
            Wall = WallMax = Balance.WALL_HP;
            Flash = 0f;
            StableTokens = 2;
            FortTokens = 2;
            WarehouseTokens = 0;
            CartTokens = 0;
            CartSpeedLevel = CartCapacityLevel = WarehouseCapacityLevel = WarehouseLimitBreaks = StableLimitBreaks = FortLimitBreaks = 0;
            _pickCounts.Clear();
            Wave = 0;
            Phase = Phase.Countdown;
            Countdown = Balance.FIRST_WAVE;
            _spawnQueue.Clear();
            _spawnTimer = 0f;
            ProducerTimer = Balance.PRODUCER_EVERY;
            _nextNodeId = 1;
            Kills = 0;
            Delivered = 0;
            Gold = 0f;
            _barkTimes.Clear();
            RewardChoices.Clear();
            Events.Clear();
            AddRoute(true);
            SpawnProducer(NodeType.Farm);
            SpawnProducer(NodeType.Forge);
            Say(_gamesStarted++ > 0 ? LineTexts.Restart : LineTexts.Story, true);
        }
    }
}
