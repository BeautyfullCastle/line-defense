using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    // 노선 편집 명령
    public sealed partial class Game
    {
        // ---------- 노선 편집 ----------

        /// <summary>노선의 양 끝에서 이어 끌 수 있는 막대 위치를 <paramref name="out_"/>에 채웁니다. 끝점에서 26 단위 바깥입니다.</summary>
        /// <param name="out_">비운 뒤 채울 재사용 목록입니다. 순환 노선이면 빈 채로 남습니다.</param>
        public void Handles(Line l, List<(Vec2 pos, Vec2 endPos, Vec2 dir, LineEnd end, Line line)> out_)
        {
            out_.Clear();
            if (l.Loop || l.Nodes.Count < 2) return;
            var p = l.Points;
            void Mk(Vec2 e, Vec2 prev, LineEnd end)
            {
                float d = Vec2.Dist(e, prev);
                if (d == 0f) d = 1f;
                var u = new Vec2((e.x - prev.x) / d, (e.y - prev.y) / d);
                out_.Add((e + u * 26f, e, u, end, l));
            }
            Mk(p[p.Count - 1].Pos, p[p.Count - 2].Pos, LineEnd.Tail);
            Mk(p[0].Pos, p[1].Pos, LineEnd.Head);
        }

        /// <summary>노드가 어떤 노선의 끝이면 그 노선과 끝을 반환합니다.</summary>
        public bool TryEndForNode(Node n, out Line line, out LineEnd end)
        {
            line = null; end = LineEnd.Tail;
            if (n.Type == NodeType.Stable)
            {
                var l = LineById(n.LineId);
                var ids = l.Nodes;
                if (l.Loop) return false;
                if (ids.Count == 1 || ids[ids.Count - 1] == n.Id) { line = l; end = LineEnd.Tail; return true; }
                if (ids[0] == n.Id) { line = l; end = LineEnd.Head; return true; }
                return false;
            }
            foreach (var l in Lines)
            {
                if (l.Loop || l.Nodes.Count < 2) continue;
                if (l.Nodes[l.Nodes.Count - 1] == n.Id) { line = l; end = LineEnd.Tail; return true; }
                if (l.Nodes[0] == n.Id) { line = l; end = LineEnd.Head; return true; }
            }
            return false;
        }

        int WhLimit() => Math.Max(0, I(B.WH_MAX_LINES));

        /// <summary>농장과 대장간은 한 노선에만, 요새는 여러 노선에, 창고는 WH_MAX_LINES까지 이을 수 있습니다.</summary>
        public bool CanJoin(Line l, Node t)
        {
            if (t.Type == NodeType.Stable || l.Nodes.Contains(t.Id)) return false;
            int others = LineCountWith(t.Id);
            if (t.Type == NodeType.Fort) return true;
            return t.Type == NodeType.Warehouse ? (WhLimit() == 0 || others < WhLimit()) : others == 0;
        }

        /// <summary>노선 끝을 끌어 target에 놓은 결과를 적용합니다. 안내할 문구가 있으면 반환합니다.</summary>
        public string DragTo(Line l, LineEnd end, Node target)
        {
            var ids = l.Nodes;
            bool tail = end == LineEnd.Tail;
            int endId = tail ? ids[ids.Count - 1] : ids[0];
            int prevId = ids.Count > 1 ? (tail ? ids[ids.Count - 2] : ids[1]) : -1;
            int otherId = tail ? ids[0] : ids[ids.Count - 1];
            var t = target;
            if (t == null) return null;
            if (ids.Count >= 3 && t.Id == otherId && t.Id != endId)
            {
                l.Loop = true;
                Rebuild(l);
                Bark("loop", LineTexts.One("aide", "순환로가 완성되었습니다. 마차가 한 방향으로 쉬지 않고 돕니다."));
            }
            else if (t.Id != endId)
            {
                if (CanJoin(l, t))
                {
                    if (tail) ids.Add(t.Id); else ids.Insert(0, t.Id);
                    Rebuild(l);
                    bool hasFort = false, hasProducer = false;
                    foreach (int id in ids)
                    {
                        var ty = NodeById(id).Type;
                        if (ty == NodeType.Fort) hasFort = true;
                        if (ty == NodeType.Farm || ty == NodeType.Forge) hasProducer = true;
                    }
                    if (t.Type == NodeType.Warehouse && LineCountWith(t.Id) == 2)
                        Bark("transfer", LineTexts.One("aide", "두 노선이 창고에서 만났습니다. 이제 짐을 넘겨받을 수 있습니다."));
                    if (hasFort && hasProducer)
                        Bark("supply", LineTexts.One("aide", "보급로가 열렸습니다. 마차가 짐을 싣기 시작합니다."));
                }
                else if (t.Id == prevId && NodeById(endId).Type != NodeType.Stable)
                {
                    if (tail) ids.RemoveAt(ids.Count - 1); else ids.RemoveAt(0);
                    Rebuild(l);
                }
                else if (t.Type != NodeType.Stable && !ids.Contains(t.Id))
                {
                    return t.Type == NodeType.Warehouse
                        ? "창고에는 노선을 " + WhLimit() + "개까지만 이을 수 있습니다"
                        : "다른 노선의 농장·대장간입니다 · 노선끼리 짐을 넘기려면 창고가 필요합니다";
                }
            }
            return null;
        }

        /// <summary>노선을 마굿간 하나만 남기고 초기화합니다.</summary>
        public void ResetLine(Line l)
        {
            l.Nodes.Clear();
            l.Nodes.Add(l.Id);
            l.Loop = false;
            Rebuild(l);
        }

        public void BreakLoop(Line l)
        {
            l.Loop = false;
            Rebuild(l);
        }

        /// <summary>배치 대기 중인 마차 권한을 써서 노선에 마차를 추가합니다.</summary>
        public bool AddCart(Line l)
        {
            if (TokCart <= 0 || l == null) return false;
            TokCart--;
            l.Carts.Add(NewCart(l));
            return true;
        }

        public void SetLineFoodSlots(Line l, int food)
        {
            l.CartFoodSlots = Math.Max(0, Math.Min(CartCap(), food));
        }

        public void SetWarehouseFoodSlots(Node w, int food)
        {
            int cap = WhCap();
            w.SlotFood = Math.Max(0, Math.Min(cap, food));
            w.SlotWeapon = cap - w.SlotFood;
        }

        /// <summary>골드로 요새를 강화합니다. attack이 true면 공격력, false면 공격 속도입니다.</summary>
        public bool UpgradeFort(Node f, bool attack)
        {
            if (f == null || f.Type != NodeType.Fort) return false;
            int cur = attack ? f.AtkLv : f.SpdLv, cost = UpgCost(cur);
            if (cur >= UpgMax() || Gold < cost) return false;
            Gold -= cost;
            if (attack) f.AtkLv++; else f.SpdLv++;
            f.Pulse = .5f;
            return true;
        }
    }
}
