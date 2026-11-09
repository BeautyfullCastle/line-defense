using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    /// <summary>
    /// 공개 명령 API만 쓰는 휴리스틱 자동 플레이어입니다. 약 0.25초마다 Tick을 호출합니다.
    /// 밸런스 시뮬레이션과 게임 내 자동 플레이 치트에 함께 씁니다.
    /// </summary>
    public sealed class Bot
    {
        readonly Game g;

        // 같은 침투로에 요새를 더 놓을 때 성벽을 따라 벌려 보는 간격(월드 단위)
        static readonly float[] FortOffsets = { 0f, 60f, -60f, 120f, -120f, 90f, -90f, 160f, -160f, 30f, -30f };

        public Bot(Game g)
        {
            this.g = g;
        }

        /// <summary>헤드리스 시뮬레이션용입니다. 보상 단계면 보상을 고른 뒤 Act를 호출합니다.</summary>
        public void Tick()
        {
            if (g.Phase == Phase.Over) return;
            if (g.Phase == Phase.Reward)
            {
                g.PickReward(ChooseReward());
                if (g.Phase == Phase.Reward) return;
            }
            Act();
        }

        // 이번 Act에서 남은 명령 수. 게임 안에서는 1로 두어 한 번에 하나씩 보이게 함
        int budget;
        bool CanAct => budget > 0;
        void Used() => budget--;

        /// <summary>
        /// 보상 선택을 뺀 건설, 노선 편집, 마차, 강화 행동을 수행합니다.
        /// maxActions개의 명령을 쓰면 멈춥니다. 반환값은 실제로 쓴 명령 수입니다.
        /// </summary>
        public int Act(int maxActions = int.MaxValue)
        {
            if (g.Phase == Phase.Over || g.Phase == Phase.Reward) return 0;
            budget = maxActions;
            PlaceForts();
            BuildLines();
            PlaceStable();
            BuildLines();
            AddCarts();
            SetSlots();
            SpendGold();
            return maxActions - budget;
        }

        // 노선 요새들의 빈 칸 비율대로 식량/무기 칸을 나누되 각 자원 최소 1칸은 남김
        void SetSlots()
        {
            int cap = g.CartCap();
            foreach (var l in g.Lines)
            {
                int needF = 0, needW = 0;
                foreach (int id in l.Nodes)
                {
                    var n = g.NodeById(id);
                    if (n.Type != NodeType.Fort) continue;
                    needF += Math.Max(0, (int)g.B.FORT_CAP - n.Food);
                    needW += Math.Max(0, (int)g.B.FORT_CAP - n.Weapon);
                }
                int food = needF + needW == 0 ? (cap + 1) / 2 : (int)Math.Round(cap * (double)needF / (needF + needW));
                food = Math.Max(1, Math.Min(cap - 1, food));
                if (l.CartFoodSlots == food) continue;
                if (!CanAct) return;
                g.SetLineFoodSlots(l, food);
                Used();
            }
        }

        // ---------- 보상 ----------

        /// <summary>현재 보상 후보 중 고를 키를 반환합니다. 후보가 없으면 null입니다.</summary>
        public string ChooseReward()
        {
            var c = g.RewardChoices;
            if (c.Count == 0) return null;
            if (c.Contains("fort") && AnyUncoveredRoute()) return "fort";
            if (c.Contains("wall") && g.Wall < g.WallMax * .6f) return "wall";
            if (c.Contains("stable") && g.Lines.Count + g.TokStable < 3) return "stable";
            foreach (var k in new[] { "cartExtra", "cartCap", "cartSpd", "fort", "stable", "gold" })
                if (c.Contains(k)) return k;
            foreach (var k in c) if (k != "warehouse" && k != "whCap" && k != "whUpg") return k;
            return c[0];
        }

        // ---------- 요새 ----------

        static Vec2 WallPoint(float angle) => new Vec2(MathF.Cos(angle) * Game.RW, Game.RW + MathF.Sin(angle) * Game.RW);

        int Coverage(Route r)
        {
            var wp = WallPoint(r.Angle);
            int n = 0;
            foreach (var f in g.Nodes) if (f.Type == NodeType.Fort && Vec2.Dist(f.Pos, wp) <= g.B.RANGE) n++;
            return n;
        }

        bool AnyUncoveredRoute()
        {
            foreach (var r in g.Routes) if (Coverage(r) == 0) return true;
            return false;
        }

        void PlaceForts()
        {
            while (g.TokFort > 0 && CanAct)
            {
                Route worst = null;
                int wc = int.MaxValue;
                foreach (var r in g.Routes)
                {
                    int c = Coverage(r);
                    if (c < wc) { wc = c; worst = r; }
                }
                if (worst == null) return;
                bool placed = false;
                foreach (float off in FortOffsets)
                {
                    var p = WallPoint(worst.Angle + off / Game.RW);
                    if (g.CanPlace(BuildKind.Fort, p) && g.Place(BuildKind.Fort, p) != null) { placed = true; Used(); break; }
                }
                if (!placed) return;
            }
        }

        // ---------- 노선 ----------

        static bool Has(Game g, Line l, NodeType t)
        {
            foreach (int id in l.Nodes) if (g.NodeById(id).Type == t) return true;
            return false;
        }

        int FortCount(Line l)
        {
            int n = 0;
            foreach (int id in l.Nodes) if (g.NodeById(id).Type == NodeType.Fort) n++;
            return n;
        }

        bool IsFree(Node n)
        {
            foreach (var _ in g.LinesWith(n.Id)) return false;
            return true;
        }

        Node Nearest(NodeType type, Vec2 from, Func<Node, bool> filter)
        {
            Node best = null;
            float bd = float.MaxValue;
            foreach (var n in g.Nodes)
            {
                if (n.Type != type || !filter(n)) continue;
                float d = Vec2.Dist(n.Pos, from);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        Vec2 TailPos(Line l) => g.NodeById(l.Nodes[l.Nodes.Count - 1]).Pos;

        bool Extend(Line l, Node t)
        {
            if (t == null || !CanAct) return false;
            g.DragTo(l, LineEnd.Tail, t);
            if (!l.Nodes.Contains(t.Id)) return false;
            Used();
            return true;
        }

        /// <summary>마굿간 - 농장 - 대장간 - 요새 순으로 한 번에 한 정거장씩 이어 변화가 없을 때까지 반복합니다.</summary>
        void BuildLines()
        {
            for (int guard = 0; guard < 40; guard++)
            {
                bool changed = false;
                foreach (var l in g.Lines)
                {
                    if (l.Loop) continue;
                    var tail = TailPos(l);
                    // 남는 농장이 없으면 대장간이라도 이음. 마굿간만 놓고 못 이은 노선이 다음 Act에서 이어지게 함
                    bool got = !Has(g, l, NodeType.Farm) && Extend(l, Nearest(NodeType.Farm, tail, IsFree));
                    if (!got && !Has(g, l, NodeType.Forge)) got = Extend(l, Nearest(NodeType.Forge, TailPos(l), IsFree));
                    changed |= got;
                }
                changed |= AttachForts();
                // 스스로 늘릴 마굿간이 없으면 남는 생산처를 꼬리가 가장 가까운 노선에 붙임
                if (g.TokStable <= 0 || g.Lines.Count >= Game.LineColorCount) changed |= AttachSpareProducer();
                if (!changed) return;
            }
        }

        // 농장과 대장간을 가져올 수 있는 만큼 가져온 노선만 요새로 넘어감
        bool ProducersDone(Line l)
        {
            bool farm = Has(g, l, NodeType.Farm), forge = Has(g, l, NodeType.Forge);
            if (!farm && !forge) return false;
            return (farm || Nearest(NodeType.Farm, TailPos(l), IsFree) == null) && (forge || Nearest(NodeType.Forge, TailPos(l), IsFree) == null);
        }

        bool AttachForts()
        {
            bool changed = false;
            // 노선이 없는 요새는 꼬리가 가까우면서 요새가 적은 노선에 붙임
            foreach (var f in g.Nodes)
            {
                if (f.Type != NodeType.Fort || !IsFree(f)) continue;
                Line best = null;
                float bc = float.MaxValue;
                foreach (var l in g.Lines)
                {
                    if (l.Loop || !ProducersDone(l)) continue;
                    float cost = Vec2.Dist(TailPos(l), f.Pos) + 150f * FortCount(l);
                    if (cost < bc) { bc = cost; best = l; }
                }
                if (best != null) changed |= Extend(best, f);
            }
            // 요새가 하나도 없는 노선에는 가까운 요새를 공유시킴
            foreach (var l in g.Lines)
            {
                if (l.Loop || FortCount(l) > 0 || !ProducersDone(l)) continue;
                changed |= Extend(l, Nearest(NodeType.Fort, TailPos(l), n => !l.Nodes.Contains(n.Id)));
            }
            return changed;
        }

        bool AttachSpareProducer()
        {
            foreach (var p in g.Nodes)
            {
                if ((p.Type != NodeType.Farm && p.Type != NodeType.Forge) || !IsFree(p)) continue;
                Line best = null;
                float bd = float.MaxValue;
                foreach (var l in g.Lines)
                {
                    if (l.Loop) continue;
                    float d = Vec2.Dist(TailPos(l), p.Pos);
                    if (d < bd) { bd = d; best = l; }
                }
                if (best != null && Extend(best, p)) return true;
            }
            return false;
        }

        /// <summary>어느 노선도 가져가지 못한 생산처 옆에 마굿간을 놓음</summary>
        void PlaceStable()
        {
            if (g.TokStable <= 0 || !CanAct) return;
            Node target = null;
            foreach (var n in g.Nodes)
            {
                if ((n.Type != NodeType.Farm && n.Type != NodeType.Forge) || !IsFree(n)) continue;
                if (target == null || (n.Type == NodeType.Farm && target.Type != NodeType.Farm)) target = n;
            }
            if (target == null) return;
            var fort = Nearest(NodeType.Fort, target.Pos, _ => true);
            var dir = fort != null ? fort.Pos - target.Pos : new Vec2(0f, -1f);
            float len = dir.Length;
            dir = len > 0f ? dir / len : new Vec2(0f, -1f);
            foreach (float r in new[] { 70f, 100f, 130f })
                foreach (float deg in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
                {
                    float a = deg * MathF.PI / 180f, cs = MathF.Cos(a), sn = MathF.Sin(a);
                    var p = target.Pos + new Vec2(dir.x * cs - dir.y * sn, dir.x * sn + dir.y * cs) * r;
                    if (!g.CanPlace(BuildKind.Stable, p)) continue;
                    var st = g.Place(BuildKind.Stable, p);
                    if (st == null) return;
                    Used();
                    // 다른 노선이 같은 생산처를 먼저 가져가지 않게 바로 이음. 남은 명령이 없으면 다음 Act의 BuildLines가 이음
                    Extend(g.LineById(st.LineId), target);
                    return;
                }
        }

        // ---------- 마차, 골드 ----------

        void AddCarts()
        {
            while (g.TokCart > 0 && CanAct)
            {
                Line best = null;
                float br = -1f;
                foreach (var l in g.Lines)
                {
                    int forts = FortCount(l);
                    if (forts == 0) continue;
                    float ratio = (float)forts / l.Carts.Count;
                    if (ratio > br) { br = ratio; best = l; }
                }
                if (best == null || !g.AddCart(best)) return;
                Used();
            }
        }

        /// <summary>노선에 이어진 요새 중 레벨이 낮은 쪽부터, 공격력과 공격 속도를 번갈아 올림</summary>
        void SpendGold()
        {
            int max = g.UpgMax();
            for (int guard = 0; guard < 50 && CanAct; guard++)
            {
                Node pick = null;
                int pl = int.MaxValue;
                bool atk = true;
                foreach (var f in g.Nodes)
                {
                    if (f.Type != NodeType.Fort || IsFree(f)) continue;
                    bool a = f.AtkLv <= f.SpdLv;
                    int lv = a ? f.AtkLv : f.SpdLv;
                    if (lv >= max) continue;
                    if (lv < pl) { pl = lv; pick = f; atk = a; }
                }
                if (pick == null || !g.UpgradeFort(pick, atk)) return;
                Used();
            }
        }
    }
}
