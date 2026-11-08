using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    // 영역 계산과 건설 명령
    public sealed partial class Game
    {
        // ---------- 영역 ----------

        /// <summary>요새는 성벽 선 위에만 지을 수 있어 위치를 성벽 원 위로 끌어당깁니다.</summary>
        public Vec2 SnapWall(Vec2 p, out float off)
        {
            float dx = p.x, dy = p.y - RW, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d == 0f) d = 1f;
            off = MathF.Abs(d - RW);
            return new Vec2(dx / d * (RW + 4f), RW + dy / d * (RW + 4f));
        }

        /// <summary>개척지 안인지 확인합니다. 개척지는 원과 |x|&lt;HW, y&lt;D의 교집합입니다.</summary>
        public bool Inside(Vec2 p, float pad = 0f) =>
            MathF.Sqrt(p.x * p.x + (p.y - RW) * (p.y - RW)) < RW - pad && MathF.Abs(p.x) < HW - pad && p.y < D - pad;

        /// <summary>침투로 각도를 정합니다. 위쪽 반원 안에서 기존 길과 가장 먼 곳을 고릅니다.</summary>
        float AddRoute(bool first)
        {
            float span = MathF.Asin(MathF.Min(1f, (HW - 80f) / RW)), up = -MathF.PI / 2f;
            float best = up;
            if (!first)
            {
                float bd = -1f;
                for (int i = 0; i < 60; i++)
                {
                    float a = up + Rnd(-span, span);
                    float d = float.MaxValue;
                    foreach (var r in Routes) d = MathF.Min(d, MathF.Abs(r.Angle - a));
                    if (d > bd) { bd = d; best = a; }
                }
            }
            else best += Rnd(-span, span) * .5f;
            Routes.Add(new Route { Angle = best, Born = T, W0 = first ? -10 : Wave });
            return best;
        }

        void SpawnProducer(NodeType type)
        {
            for (int i = 0; i < 120; i++)
            {
                var p = new Vec2(Rnd(-HW + 40f, HW - 40f), Rnd(20f, D - 30f));
                if (!Inside(p, 40f) || !FarFromAll(p, 62f)) continue;
                Nodes.Add(new Node { Id = nid++, Type = type, Pos = p, Stock = 0, ProdTimer = Rnd(0f, 3f), Born = T });
                if (T > 1f) Bark("new-producer", LineTexts.One("aide", "새 농장이나 대장간이 생기면 노선에 이어 주십시오."));
                return;
            }
        }

        bool FarFromAll(Vec2 p, float dist)
        {
            for (int i = 0; i < Nodes.Count; i++) if (Vec2.Dist(Nodes[i].Pos, p) <= dist) return false;
            return true;
        }

        // ---------- 건설 ----------

        public bool CanPlace(BuildKind k, Vec2 p)
        {
            if (k == BuildKind.Fort)
            {
                var q = SnapWall(p, out float off);
                return off < WALL_SNAP && q.y < RW && MathF.Abs(q.x) < HW - 20f && FarFromAll(q, 48f);
            }
            return Inside(p, 20f) && FarFromAll(p, 48f);
        }

        int Tokens(BuildKind k) => k == BuildKind.Stable ? TokStable : k == BuildKind.Fort ? TokFort : TokWarehouse;

        /// <summary>건물을 세우고 만든 노드를 반환합니다. 권한이 없거나 자리가 안 되면 null입니다.</summary>
        public Node Place(BuildKind k, Vec2 p)
        {
            if (Tokens(k) <= 0 || !CanPlace(k, p)) return null;
            if (k == BuildKind.Fort) p = SnapWall(p, out _);
            Node n;
            if (k == BuildKind.Stable)
            {
                int color = -1;
                for (int c = 0; c < LineColorCount && color < 0; c++)
                {
                    bool used = false;
                    foreach (var l in Lines) if (l.ColorIndex == c) { used = true; break; }
                    if (!used) color = c;
                }
                if (color < 0) return null;
                n = new Node { Id = nid++, Type = NodeType.Stable, Pos = p, Born = T };
                n.LineId = n.Id;
                Nodes.Add(n);
                var line = new Line { Id = n.Id, ColorIndex = color, CartFoodSlots = (int)Math.Ceiling(CartCap() / 2.0) };
                line.Nodes.Add(n.Id);
                Lines.Add(line);
                Rebuild(line);
                line.Carts.Add(NewCart(line));
            }
            else if (k == BuildKind.Warehouse)
            {
                int food = (int)Math.Ceiling(WhCap() / 2.0);
                n = new Node { Id = nid++, Type = NodeType.Warehouse, Pos = p, SlotFood = food, SlotWeapon = WhCap() - food, Born = T };
                Nodes.Add(n);
                Bark("warehouse", LineTexts.One("aide", "창고가 섰습니다. 여러 노선을 이 창고에서 만나게 하면, 한 노선이 내려 둔 짐을 다른 노선들이 실어 갑니다."));
            }
            else
            {
                n = new Node
                {
                    Id = nid++, Type = NodeType.Fort, Pos = p,
                    Food = Math.Min(I(B.FORT_CAP), I(B.FORT_START_FOOD)),
                    Weapon = Math.Min(I(B.FORT_CAP), I(B.FORT_START_WEAPON)),
                    Morale = MathF.Min(1f, B.FORT_START_MORALE), Born = T,
                };
                Nodes.Add(n);
                Bark("fort", LineTexts.One("aide", "요새는 식량이 있어야 버티고, 무기가 있어야 싸웁니다. 꼭 노선에 이어 주십시오."));
            }
            if (k == BuildKind.Stable) TokStable--; else if (k == BuildKind.Fort) TokFort--; else TokWarehouse--;
            Emit(new GameEvent { Type = GameEventType.NodePlaced, A = n.Pos, NodeId = n.Id });
            return n;
        }

        Cart NewCart(Line line)
        {
            var s = NodeById(line.Id);
            int pi = 0;
            for (int i = 0; i < line.Points.Count; i++) if (line.Points[i].NodeId == line.Id) { pi = i; break; }
            return new Cart { Pos = s.Pos, PointIndex = pi, Dir = 1, Dwell = .3f, SlotFood = line.CartFoodSlots, SlotWeapon = CartCap() - line.CartFoodSlots };
        }

        static bool Bend(Vec2 a, Vec2 b, out Vec2 r)
        {
            float dx = b.x - a.x, dy = b.y - a.y, ax = MathF.Abs(dx), ay = MathF.Abs(dy);
            r = default;
            if (ax < 1f || ay < 1f || MathF.Abs(ax - ay) < 1f) return false;
            r = ax > ay ? new Vec2(a.x + MathF.Sign(dx) * (ax - ay), a.y) : new Vec2(a.x, a.y + MathF.Sign(dy) * (ay - ax));
            return true;
        }

        const float CornerR = 36f;
        const int CornerSteps = 6;

        // 꺾임점을 2차 베지어 호로 바꿈. 마차도 같은 점을 따라가서 그림과 경로가 일치함
        static void AddCorner(List<LinePoint> pts, Vec2 from, Vec2 bend, Vec2 to)
        {
            float d0 = Vec2.Dist(bend, from), d1 = Vec2.Dist(bend, to);
            float r = MathF.Min(CornerR, MathF.Min(d0, d1) * .45f);
            var a = bend + (from - bend) * (r / d0);
            var c = bend + (to - bend) * (r / d1);
            for (int s = 0; s <= CornerSteps; s++)
            {
                float u = s / (float)CornerSteps, v = 1f - u;
                pts.Add(new LinePoint { Pos = a * (v * v) + bend * (2f * u * v) + c * (u * u), NodeId = -1 });
            }
        }

        /// <summary>노선의 꺾임점을 다시 만들고 마차를 가장 가까운 점에 붙입니다.</summary>
        void Rebuild(Line line)
        {
            foreach (var c in line.Carts) ReleasePlan(c, -1);
            var olds = new List<LinePoint>();
            foreach (var c in line.Carts) olds.Add(c.PointIndex >= 0 && c.PointIndex < line.Points.Count ? line.Points[c.PointIndex] : null);
            var ns = new List<Node>();
            foreach (int id in line.Nodes) ns.Add(NodeById(id));
            var pts = new List<LinePoint>();
            for (int i = 0; i < ns.Count; i++)
            {
                pts.Add(new LinePoint { Pos = ns[i].Pos, NodeId = ns[i].Id });
                if (i < ns.Count - 1 && Bend(ns[i].Pos, ns[i + 1].Pos, out var b)) AddCorner(pts, ns[i].Pos, b, ns[i + 1].Pos);
            }
            if (line.Loop && ns.Count >= 3)
            {
                if (Bend(ns[ns.Count - 1].Pos, ns[0].Pos, out var b)) AddCorner(pts, ns[ns.Count - 1].Pos, b, ns[0].Pos);
            }
            else line.Loop = false;
            line.Points = pts;
            for (int k = 0; k < line.Carts.Count; k++)
            {
                var old = olds[k];
                int best = 0;
                if (old != null)
                {
                    float bd = 1e9f;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        float d = Vec2.Dist(pts[i].Pos, old.Pos);
                        if (d < bd) { bd = d; best = i; }
                    }
                }
                line.Carts[k].PointIndex = best;
            }
        }
    }
}
