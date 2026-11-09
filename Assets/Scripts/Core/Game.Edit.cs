using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    // 노선 편집 명령
    public sealed partial class Game
    {
        // ---------- 노선 편집 ----------

        /// <summary>노선의 양 끝에서 이어 끌 수 있는 막대 위치를 <paramref name="handles"/>에 채웁니다. 끝점에서 26 단위 바깥입니다.</summary>
        /// <param name="handles">비운 뒤 채울 재사용 목록입니다. 순환 노선이면 빈 채로 남습니다.</param>
        public void Handles(Line line, List<(Vec2 Position, Vec2 EndPosition, Vec2 Direction, LineEnd End, Line Line)> handles)
        {
            handles.Clear();
            if (line.Loop || line.Nodes.Count < 2)
            {
                return;
            }

            var points = line.Points;
            void AddHandle(Vec2 endPoint, Vec2 previousPoint, LineEnd end)
            {
                float length = Vec2.Distance(endPoint, previousPoint);
                if (length == 0f)
                {
                    length = 1f;
                }

                var direction = new Vec2((endPoint.X - previousPoint.X) / length, (endPoint.Y - previousPoint.Y) / length);
                handles.Add((endPoint + direction * 26f, endPoint, direction, end, line));
            }
            AddHandle(points[points.Count - 1].Position, points[points.Count - 2].Position, LineEnd.Tail);
            AddHandle(points[0].Position, points[1].Position, LineEnd.Head);
        }

        /// <summary>노드가 어떤 노선의 끝이면 그 노선과 끝을 반환합니다.</summary>
        public bool TryEndForNode(Node node, out Line line, out LineEnd end)
        {
            line = null;
            end = LineEnd.Tail;
            if (node.Type == NodeType.Stable)
            {
                var ownerLine = LineById(node.LineId);
                var nodeIds = ownerLine.Nodes;
                if (ownerLine.Loop)
                {
                    return false;
                }

                if (nodeIds.Count == 1 || nodeIds[nodeIds.Count - 1] == node.Id)
                {
                    line = ownerLine;
                    end = LineEnd.Tail;
                    return true;
                }
                if (nodeIds[0] == node.Id)
                {
                    line = ownerLine;
                    end = LineEnd.Head;
                    return true;
                }
                return false;
            }
            foreach (var candidate in Lines)
            {
                if (candidate.Loop || candidate.Nodes.Count < 2)
                {
                    continue;
                }

                if (candidate.Nodes[candidate.Nodes.Count - 1] == node.Id)
                {
                    line = candidate;
                    end = LineEnd.Tail;
                    return true;
                }
                if (candidate.Nodes[0] == node.Id)
                {
                    line = candidate;
                    end = LineEnd.Head;
                    return true;
                }
            }
            return false;
        }

        private int WarehouseLineLimit() => Math.Max(0, RoundToInt(Balance.WH_MAX_LINES));

        /// <summary>농장과 대장간은 한 노선에만, 요새는 여러 노선에, 창고는 WH_MAX_LINES까지 이을 수 있습니다.</summary>
        public bool CanJoin(Line line, Node target)
        {
            if (target.Type == NodeType.Stable || line.Nodes.Contains(target.Id))
            {
                return false;
            }

            int others = LineCountWith(target.Id);
            if (target.Type == NodeType.Fort)
            {
                return true;
            }

            return target.Type == NodeType.Warehouse ? (WarehouseLineLimit() == 0 || others < WarehouseLineLimit()) : others == 0;
        }

        /// <summary>노선 끝을 끌어 target에 놓은 결과를 적용합니다. 안내할 문구가 있으면 반환합니다.</summary>
        public string DragTo(Line line, LineEnd end, Node target)
        {
            var nodeIds = line.Nodes;
            bool tail = end == LineEnd.Tail;
            int endId = tail ? nodeIds[nodeIds.Count - 1] : nodeIds[0];
            int previousId = nodeIds.Count > 1 ? (tail ? nodeIds[nodeIds.Count - 2] : nodeIds[1]) : -1;
            int otherId = tail ? nodeIds[0] : nodeIds[nodeIds.Count - 1];
            var targetNode = target;
            if (targetNode == null)
            {
                return null;
            }

            if (nodeIds.Count >= 3 && targetNode.Id == otherId && targetNode.Id != endId)
            {
                line.Loop = true;
                Rebuild(line);
                Bark("loop", LineTexts.One("aide", "순환로가 완성되었습니다. 마차가 한 방향으로 쉬지 않고 돕니다."));
            }
            else if (targetNode.Id != endId)
            {
                if (CanJoin(line, targetNode))
                {
                    if (tail)
                    {
                        nodeIds.Add(targetNode.Id);
                    }
                    else
                    {
                        nodeIds.Insert(0, targetNode.Id);
                    }

                    Rebuild(line);
                    AfterJoin(line, targetNode);
                }
                else if (targetNode.Id == previousId && NodeById(endId).Type != NodeType.Stable)
                {
                    if (tail)
                    {
                        nodeIds.RemoveAt(nodeIds.Count - 1);
                    }
                    else
                    {
                        nodeIds.RemoveAt(0);
                    }

                    Rebuild(line);
                }
                else if (targetNode.Type != NodeType.Stable && !nodeIds.Contains(targetNode.Id))
                {
                    return JoinRefusal(targetNode);
                }
            }
            return null;
        }

        private void AfterJoin(Line line, Node target)
        {
            bool hasFort = false, hasProducer = false;
            foreach (int id in line.Nodes)
            {
                var type = NodeById(id).Type;
                if (type == NodeType.Fort)
                {
                    hasFort = true;
                }

                if (type == NodeType.Farm || type == NodeType.Forge)
                {
                    hasProducer = true;
                }
            }
            if (target.Type == NodeType.Warehouse && LineCountWith(target.Id) == 2)
            {
                Bark("transfer", LineTexts.One("aide", "두 노선이 창고에서 만났습니다. 이제 짐을 넘겨받을 수 있습니다."));
            }

            if (hasFort && hasProducer)
            {
                Bark("supply", LineTexts.One("aide", "보급로가 열렸습니다. 마차가 짐을 싣기 시작합니다."));
            }
        }

        private string JoinRefusal(Node target) => target.Type == NodeType.Warehouse
            ? "창고에는 노선을 " + WarehouseLineLimit() + "개까지만 이을 수 있습니다"
            : "다른 노선의 농장·대장간입니다 · 노선끼리 짐을 넘기려면 창고가 필요합니다";

        /// <summary>노선의 구간 수입니다. 구간 i는 Nodes[i]와 다음 정거장 사이이고, 순환이면 마지막 구간이 끝에서 처음으로 갑니다.</summary>
        public static int SegmentCount(Line line) => line.Nodes.Count < 2 ? 0 : line.Loop ? line.Nodes.Count : line.Nodes.Count - 1;

        /// <summary>p에서 r 안에 있는 가장 가까운 노선 구간을 찾습니다. 그림에 쓰는 곡선 점을 기준으로 잽니다.</summary>
        public bool HitSegment(Vec2 point, float radius, out Line line, out int segment)
        {
            line = null;
            segment = -1;
            float best = radius;
            foreach (var candidate in Lines)
            {
                var points = candidate.Points;
                if (SegmentCount(candidate) == 0 || points.Count < 2)
                {
                    continue;
                }

                int segmentIndex = -1, pairCount = candidate.Loop ? points.Count : points.Count - 1;
                for (int i = 0; i < pairCount; i++)
                {
                    var startPoint = points[i];
                    if (startPoint.NodeId >= 0)
                    {
                        segmentIndex++;
                    }

                    float distance = DistanceToSegment(point, startPoint.Position, points[(i + 1) % points.Count].Position);
                    if (distance < best)
                    {
                        best = distance;
                        line = candidate;
                        segment = segmentIndex;
                    }
                }
            }
            return line != null;
        }

        private static float DistanceToSegment(Vec2 point, Vec2 start, Vec2 end)
        {
            float segmentX = end.X - start.X, segmentY = end.Y - start.Y, lengthSquared = segmentX * segmentX + segmentY * segmentY;
            float k = lengthSquared > 0f ? Math.Clamp(((point.X - start.X) * segmentX + (point.Y - start.Y) * segmentY) / lengthSquared, 0f, 1f) : 0f;
            return Vec2.Distance(point, new Vec2(start.X + segmentX * k, start.Y + segmentY * k));
        }

        /// <summary>
        /// 노선 구간 seg 사이에 target을 끼워 넣습니다. a-b 구간에 c를 놓으면 a-c-b가 됩니다.
        /// 끝을 끌 때와 같은 CanJoin 규칙을 따르며, 안내할 문구가 있으면 반환합니다.
        /// </summary>
        public string InsertStop(Line line, int segment, Node target)
        {
            if (line == null || target == null || segment < 0 || segment >= SegmentCount(line))
            {
                return null;
            }

            if (line.Nodes.Contains(target.Id))
            {
                return null;
            }

            if (!CanJoin(line, target))
            {
                return target.Type == NodeType.Stable ? null : JoinRefusal(target);
            }

            line.Nodes.Insert(segment + 1, target.Id);
            Rebuild(line);
            AfterJoin(line, target);
            return null;
        }

        /// <summary>노선을 마굿간 하나만 남기고 초기화합니다.</summary>
        public void ResetLine(Line line)
        {
            line.Nodes.Clear();
            line.Nodes.Add(line.Id);
            line.Loop = false;
            Rebuild(line);
        }

        public void BreakLoop(Line line)
        {
            line.Loop = false;
            Rebuild(line);
        }

        /// <summary>배치 대기 중인 마차 권한을 써서 노선에 마차를 추가합니다.</summary>
        public bool AddCart(Line line)
        {
            if (CartTokens <= 0 || line == null)
            {
                return false;
            }

            CartTokens--;
            line.Carts.Add(NewCart(line));
            return true;
        }

        public void SetLineFoodSlots(Line line, int food)
        {
            line.CartFoodSlots = Math.Max(0, Math.Min(CartCapacity(), food));
        }

        public void SetWarehouseFoodSlots(Node warehouse, int food)
        {
            int capacity = WarehouseCapacity();
            warehouse.SlotFood = Math.Max(0, Math.Min(capacity, food));
            warehouse.SlotWeapon = capacity - warehouse.SlotFood;
        }

        /// <summary>골드로 요새를 강화합니다. attack이 true면 공격력, false면 공격 속도입니다.</summary>
        public bool UpgradeFort(Node fort, bool attack)
        {
            if (fort == null || fort.Type != NodeType.Fort)
            {
                return false;
            }

            int currentLevel = attack ? fort.AttackLevel : fort.SpeedLevel, cost = UpgradeCost(currentLevel);
            if (currentLevel >= MaxUpgradeLevel() || Gold < cost)
            {
                return false;
            }

            Gold -= cost;
            if (attack)
            {
                fort.AttackLevel++;
            }
            else
            {
                fort.SpeedLevel++;
            }

            fort.Pulse = .5f;
            return true;
        }
    }
}
