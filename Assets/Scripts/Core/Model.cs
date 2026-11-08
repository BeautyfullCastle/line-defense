using System.Collections.Generic;

namespace LineDefense.Core
{
    public enum NodeType { Farm, Forge, Stable, Warehouse, Fort }
    public enum Res { Food = 0, Weapon = 1 }
    public enum Phase { Countdown, Wave, Reward, Over }
    public enum BuildKind { Stable, Fort, Warehouse }
    public enum LineEnd { Head, Tail }

    /// <summary>맵 위의 건물 하나입니다. 종류에 따라 쓰는 필드가 다릅니다.</summary>
    public sealed class Node
    {
        public int Id;
        public NodeType Type;
        public Vec2 Pos;
        public float Born;

        // 농장, 대장간
        public int Stock;
        public float ProdTimer;

        // 마굿간
        public int LineId;

        // 마굿간, 창고, 요새의 보관량
        public int Food, Weapon;

        // 창고 칸 구성
        public int SlotFood, SlotWeapon;
        public float Pulse;
        public float EmptyTimer;

        // 요새
        public float Ammo, Cooldown, FoodTimer, Morale;
        public int AtkLv, SpdLv;
        public int ReservedFood, ReservedWeapon;

        public int Get(Res r) => r == Res.Food ? Food : Weapon;

        public void Add(Res r, int n)
        {
            if (r == Res.Food) Food += n; else Weapon += n;
        }

        public int Slot(Res r) => r == Res.Food ? SlotFood : SlotWeapon;
        public int Reserved(Res r) => r == Res.Food ? ReservedFood : ReservedWeapon;

        internal void AddReserved(Res r, int n)
        {
            if (r == Res.Food) ReservedFood += n; else ReservedWeapon += n;
        }
    }

    /// <summary>노선을 오가는 마차입니다.</summary>
    public sealed class Cart
    {
        public Vec2 Pos;
        public int PointIndex;
        public int Dir;
        public float Dwell;
        public int Food, Weapon;
        public float Angle;
        public int SlotFood, SlotWeapon;
        /// <summary>도착 창고로 옮기는 중인 짐의 양입니다.</summary>
        public int TransferFood, TransferWeapon;

        /// <summary>요새 id별 예약량입니다. 인덱스는 (int)Res입니다.</summary>
        internal Dictionary<int, int[]> Plan;

        public int Get(Res r) => r == Res.Food ? Food : Weapon;

        public void Add(Res r, int n)
        {
            if (r == Res.Food) Food += n; else Weapon += n;
        }

        public int Slot(Res r) => r == Res.Food ? SlotFood : SlotWeapon;
    }

    /// <summary>노선의 꺾임점 또는 정거장입니다.</summary>
    public sealed class LinePoint
    {
        public Vec2 Pos;
        /// <summary>정거장이면 노드 id, 꺾임점이면 -1입니다.</summary>
        public int NodeId;
    }

    public sealed class Line
    {
        public int Id;
        public int ColorIndex;
        public List<int> Nodes = new List<int>();
        public List<LinePoint> Points = new List<LinePoint>();
        public List<Cart> Carts = new List<Cart>();
        /// <summary>마굿간에서 설정한 마차의 식량 칸 수입니다.</summary>
        public int CartFoodSlots;
        public bool Loop;
    }

    public sealed class Monster
    {
        public Vec2 Pos;
        public float Hp, MaxHp, Speed, Size, Alpha, Hit, Bob;
    }

    /// <summary>몬스터가 들어오는 침투로입니다.</summary>
    public sealed class Route
    {
        public float Angle;
        public float Born;
        public int W0;
    }
}
