using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    // 마차의 예약, 창고, 요새 인도 로직
    public sealed partial class Game
    {
        static readonly Res[] Ks = { Res.Food, Res.Weapon };

        int FortCap => I(B.FORT_CAP);

        /// <summary>요새가 더 받을 수 있는 실수요입니다. 예약분은 뺍니다.</summary>
        int NeedOf(Node f, Res k) => Math.Max(0, FortCap - f.Get(k) - f.Reserved(k));

        int CFree(Cart c, Res k) => Math.Max(0, c.Slot(k) - c.Get(k));
        int WhRoom(Node n, Res k) => Math.Max(0, n.Slot(k) - n.Get(k));

        int PlanOf(Cart c, Node f, Res k) =>
            c.Plan != null && c.Plan.TryGetValue(f.Id, out var p) ? p[(int)k] : 0;

        void AddPlan(Cart c, Node f, Res k, int n)
        {
            if (n <= 0) return;
            if (c.Plan == null) c.Plan = new Dictionary<int, int[]>();
            if (!c.Plan.TryGetValue(f.Id, out var p)) c.Plan[f.Id] = p = new int[2];
            p[(int)k] += n;
            f.AddReserved(k, n);
        }

        /// <summary>fid가 -1이면 이 마차의 예약을 모두 풀고, 아니면 그 요새 예약만 풉니다.</summary>
        void ReleasePlan(Cart c, int fid)
        {
            if (c.Plan == null || c.Plan.Count == 0) return;
            var ids = fid >= 0 ? new List<int> { fid } : new List<int>(c.Plan.Keys);
            foreach (int id in ids)
            {
                if (!c.Plan.TryGetValue(id, out var p)) continue;
                var f = NodeById(id);
                if (f != null) foreach (var k in Ks) f.AddReserved(k, -Math.Min(f.Reserved(k), p[(int)k]));
                c.Plan.Remove(id);
            }
        }

        /// <summary>요새 몫 n개를 구간 요새들에 나눠 예약합니다. 실수요가 큰 요새부터 1개씩입니다.</summary>
        void Reserve(Cart c, List<Node> forts, Res k, int n)
        {
            var left = new int[forts.Count];
            for (int i = 0; i < left.Length; i++) left[i] = NeedOf(forts[i], k);
            for (int u = 0; u < n; u++)
            {
                int bi = -1;
                for (int i = 0; i < left.Length; i++) if (left[i] > 0 && (bi < 0 || left[i] > left[bi])) bi = i;
                if (bi < 0) break;
                left[bi]--;
                AddPlan(c, forts[bi], k, 1);
            }
        }

        /// <summary>노선 점 i에서 방향 d로 정거장을 하나씩 지나며 cb를 부릅니다. cb가 true면 멈춥니다.</summary>
        void Walk(Line line, int i, int d, Func<Node, bool> cb)
        {
            var pts = line.Points;
            int n = pts.Count;
            if (n < 2) return;
            for (int s = 0; s < 2 * n + 2; s++)
            {
                int ni = i + d;
                if (line.Loop) ni = (ni + n) % n;
                else if (ni < 0 || ni >= n) { d = -d; ni = i + d; }
                i = ni;
                int id = pts[i].NodeId;
                if (id >= 0 && cb(NodeById(id))) return;
            }
        }

        /// <summary>마차 위치에서 다음 창고 전까지의 요새들과 도착 창고입니다. 같은 창고로 돌아오면 dest는 null입니다.</summary>
        (List<Node> forts, Node dest) SegmentFrom(Line line, Cart c, int fromId)
        {
            var forts = new List<Node>();
            Node dest = null;
            Walk(line, c.PointIndex, c.Dir, nd =>
            {
                if (nd.Type == NodeType.Warehouse) { if (nd.Id != fromId) dest = nd; return true; }
                if (nd.Type == NodeType.Fort && !forts.Contains(nd)) forts.Add(nd);
                return false;
            });
            return (forts, dest);
        }

        /// <summary>창고에서 출발하는 모든 구간의 요새입니다. 순환 노선은 진행 방향만 봅니다.</summary>
        List<Node> WhForts(Node w)
        {
            var outList = new List<Node>();
            foreach (var l in LinesWith(w.Id))
            {
                int i0 = l.Points.FindIndex(p => p.NodeId == w.Id);
                if (i0 < 0 || l.Points.Count < 2) continue;
                var dirs = new List<int>();
                if (l.Loop) { foreach (var c in l.Carts) if (!dirs.Contains(c.Dir)) dirs.Add(c.Dir); }
                else { dirs.Add(1); dirs.Add(-1); }
                if (dirs.Count == 0) dirs.Add(1);
                foreach (int d in dirs)
                    Walk(l, i0, d, nd =>
                    {
                        if (nd.Type == NodeType.Warehouse) return true;
                        if (nd.Type == NodeType.Fort && !outList.Contains(nd)) outList.Add(nd);
                        return false;
                    });
            }
            return outList;
        }

        /// <summary>창고 목표량 = min(자원 칸 수, 구간 요새 실수요 합 + 올림(WH_RESERVE x 자원 칸 수))입니다.</summary>
        public int WarehouseTarget(Node w, Res r) => WhTarget(w, r, WhForts(w));

        int WhTarget(Node w, Res k, List<Node> forts)
        {
            int slot = w.Slot(k), need = 0;
            foreach (var f in forts) need += NeedOf(f, k);
            return Math.Min(slot, need + (int)Math.Ceiling(B.WH_RESERVE * slot));
        }

        void ArriveWarehouse(Line line, Cart c, Node A)
        {
            ReleasePlan(c, -1);                                // 1. 남아 있던 예약 해제
            var seg = SegmentFrom(line, c, A.Id);              // 2. 구간 파악
            int moved = 0;
            bool full = false;
            c.TransferFood = c.TransferWeapon = 0;
            foreach (var k in Ks)
            {
                int pool = A.Get(k) + c.Get(k), need = 0;
                foreach (var f in seg.forts) need += NeedOf(f, k);
                int fortAmt = Math.Min(Math.Min(need, pool), c.Slot(k));   // 3. 요새 몫
                Reserve(c, seg.forts, k, fortAmt);                           // 4. 예약
                int x = 0;
                if (seg.dest != null)                                        // 5. 도착 창고 몫 (충족률 맞추기)
                {
                    var Bw = seg.dest;
                    int TA = WarehouseTarget(A, k), TB = WarehouseTarget(Bw, k), SA = pool - fortAmt, SB = Bw.Get(k);
                    if (TA + TB > 0)
                    {
                        x = (int)Math.Floor((double)(SA * TB - SB * TA) / (TA + TB));
                        x = Math.Min(Math.Min(x, TB - SB), Math.Min(SA, c.Slot(k) - fortAmt));
                    }
                    if (x < Math.Max(1, I(B.WH_MIN_TRANSFER))) x = 0;
                }
                int goal = fortAmt + x;                                      // 6. 목표 적재량에 맞춘다
                if (c.Get(k) > goal)
                {
                    int d = Math.Min(c.Get(k) - goal, WhRoom(A, k));
                    c.Add(k, -d); A.Add(k, d); moved += d;
                    if (c.Get(k) > goal) full = true;                        // 7. 못 내린 짐은 마차에 남는다
                }
                else
                {
                    int t = Math.Min(goal - c.Get(k), A.Get(k));
                    A.Add(k, -t); c.Add(k, t); moved += t;
                }
                if (k == Res.Food) c.TransferFood = x; else c.TransferWeapon = x;
            }
            if (moved > 0) A.Pulse = .5f;
            if (full)
                Bark("whfull", LineTexts.One("aide", "창고의 해당 자원 칸이 가득 찼습니다. 실어 갈 노선을 잇거나 창고 칸 구성을 바꿔 주십시오."), 40f);
        }

        void ArriveFort(Line line, Cart c, Node f)
        {
            int got = 0;
            // 1. 이 요새에 예약해 둔 몫을 내림 (빈 칸, 적재량 한도 안에서). 예약은 내린 양과 상관없이 푼다
            foreach (var k in Ks)
            {
                int d = Math.Min(Math.Min(PlanOf(c, f, k), Math.Max(0, FortCap - f.Get(k))), c.Get(k));
                f.Add(k, d); c.Add(k, -d); got += d;
            }
            ReleasePlan(c, f.Id);
            // 2. 남은 요새들의 예약 몫을 뺀 여분은 요새끼리 나눔: 실수요가 큰 요새부터 1개씩, 이 요새는 자기 몫만
            var rest = FortsOnRoute(line, c);
            rest.Remove(f);
            var group = new List<Node> { f };
            group.AddRange(rest);
            foreach (var k in Ks)
            {
                int extra = c.Get(k);
                foreach (var g in rest) extra -= PlanOf(c, g, k);
                if (extra <= 0) continue;
                var lv = new int[group.Count];
                for (int i = 0; i < lv.Length; i++) lv[i] = group[i].Get(k) + group[i].Reserved(k);
                int mine = 0;
                for (int u = 0; u < extra; u++)
                {
                    int bi = -1;
                    for (int i = 0; i < lv.Length; i++) if (lv[i] < FortCap && (bi < 0 || lv[i] < lv[bi])) bi = i;
                    if (bi < 0) break;
                    lv[bi]++;
                    if (bi == 0) mine++;
                }
                f.Add(k, mine); c.Add(k, -mine); got += mine;
            }
            if (got > 0) { f.Pulse = .5f; Delivered += got; }
        }

        void Serve(Line line, Cart c, Node n)
        {
            if (n.Type == NodeType.Fort) ArriveFort(line, c, n);
            else if (n.Type == NodeType.Warehouse) ArriveWarehouse(line, c, n);
            else if (n.Type == NodeType.Stable)
            {
                // 마굿간 통과: 설정한 칸 구성을 마차에 적용하고, 칸을 넘는 자원은 마굿간에 남김
                if (line.Id != n.LineId) return;
                c.SlotFood = line.CartFoodSlots;
                c.SlotWeapon = CartCap() - line.CartFoodSlots;
                foreach (var k in Ks)
                {
                    int over = Math.Max(0, c.Get(k) - c.Slot(k));
                    c.Add(k, -over); n.Add(k, over);
                    // 마굿간에 남은 자원은 그 자원 칸이 비면 다시 싣는다
                    int back = Math.Min(n.Get(k), CFree(c, k));
                    n.Add(k, -back); c.Add(k, back);
                }
            }
            else if (n.Type == NodeType.Farm || n.Type == NodeType.Forge)
            {
                // 노선에 요새나 창고가 있을 때만 해당 자원 칸이 빈 만큼 싣는다
                bool ok = false;
                foreach (int id in line.Nodes)
                {
                    var t = NodeById(id).Type;
                    if (t == NodeType.Fort || t == NodeType.Warehouse) { ok = true; break; }
                }
                if (!ok) return;
                var key = n.Type == NodeType.Farm ? Res.Food : Res.Weapon;
                int take = Math.Max(0, Math.Min(CFree(c, key), n.Stock));
                n.Stock -= take;
                c.Add(key, take);
            }
        }

        /// <summary>마차의 현재 위치와 방향에서 다음 창고 전까지 지날 요새들입니다. 현재 위치는 제외하고 만나는 순서대로입니다.</summary>
        List<Node> FortsOnRoute(Line line, Cart c)
        {
            var outList = new List<Node>();
            Walk(line, c.PointIndex, c.Dir, nd =>
            {
                if (nd.Type == NodeType.Warehouse) return true;
                if (nd.Type == NodeType.Fort && !outList.Contains(nd)) outList.Add(nd);
                return false;
            });
            return outList;
        }

        void StepCart(Line line, Cart c, float dt)
        {
            var pts = line.Points;
            if (pts.Count < 2)
            {
                c.Pos = NodeById(line.Id).Pos;
                return;
            }
            if (c.Dwell > 0f) { c.Dwell -= dt; return; }
            float move = CartSpeed() * dt;
            int guard = 0;
            while (move > 0f && guard++ < 12)
            {
                var t = pts[c.PointIndex];
                float dx = t.Pos.x - c.Pos.x, dy = t.Pos.y - c.Pos.y, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d <= move)
                {
                    c.Pos = t.Pos;
                    move -= d;
                    if (t.NodeId >= 0)
                    {
                        Serve(line, c, NodeById(t.NodeId));
                        c.Dwell = B.DWELL;
                    }
                    int np = c.PointIndex + c.Dir;
                    if (line.Loop) np = (np + pts.Count) % pts.Count;     // 순환: 한 방향으로 계속
                    else if (np < 0 || np >= pts.Count) { c.Dir *= -1; np = c.PointIndex + c.Dir; }
                    c.PointIndex = np;
                    if (c.Dwell > 0f) break;
                }
                else
                {
                    c.Pos.x += dx / d * move;
                    c.Pos.y += dy / d * move;
                    c.Angle = MathF.Atan2(dy, dx);
                    move = 0f;
                }
            }
        }
    }
}
