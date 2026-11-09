using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    // 마차의 예약, 창고, 요새 인도 로직
    public sealed partial class Game
    {
        private static readonly Res[] s_resources = { Res.Food, Res.Weapon };

        private int FortCap => RoundToInt(Balance.FORT_CAP);

        /// <summary>요새가 더 받을 수 있는 실수요입니다. 예약분은 뺍니다.</summary>
        private int NeedOf(Node fort, Res resource) => Math.Max(0, FortCap - fort.Get(resource) - fort.Reserved(resource));

        private int CFree(Cart cart, Res resource) => Math.Max(0, cart.Slot(resource) - cart.Get(resource));
        private int WarehouseRoom(Node node, Res resource) => Math.Max(0, node.Slot(resource) - node.Get(resource));

        private int PlanOf(Cart cart, Node fort, Res resource) =>
            cart.Plan != null && cart.Plan.TryGetValue(fort.Id, out var planAmounts) ? planAmounts[(int)resource] : 0;

        private void AddPlan(Cart cart, Node fort, Res resource, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (cart.Plan == null)
            {
                cart.Plan = new Dictionary<int, int[]>();
            }

            if (!cart.Plan.TryGetValue(fort.Id, out var planAmounts))
            {
                cart.Plan[fort.Id] = planAmounts = new int[2];
            }

            planAmounts[(int)resource] += amount;
            fort.AddReserved(resource, amount);
        }

        /// <summary>fid가 -1이면 이 마차의 예약을 모두 풀고, 아니면 그 요새 예약만 풉니다.</summary>
        private void ReleasePlan(Cart cart, int fortId)
        {
            if (cart.Plan == null || cart.Plan.Count == 0)
            {
                return;
            }

            var fortIds = fortId >= 0 ? new List<int> { fortId } : new List<int>(cart.Plan.Keys);
            foreach (int id in fortIds)
            {
                if (!cart.Plan.TryGetValue(id, out var planAmounts))
                {
                    continue;
                }

                var fort = NodeById(id);
                if (fort != null)
                {
                    foreach (var resource in s_resources)
                    {
                        fort.AddReserved(resource, -Math.Min(fort.Reserved(resource), planAmounts[(int)resource]));
                    }
                }

                cart.Plan.Remove(id);
            }
        }

        /// <summary>요새 몫 n개를 구간 요새들에 나눠 예약합니다. 실수요가 큰 요새부터 1개씩입니다.</summary>
        private void Reserve(Cart cart, List<Node> forts, Res resource, int count)
        {
            var left = new int[forts.Count];
            for (int i = 0; i < left.Length; i++)
            {
                left[i] = NeedOf(forts[i], resource);
            }

            for (int unitIndex = 0; unitIndex < count; unitIndex++)
            {
                int bestIndex = -1;
                for (int i = 0; i < left.Length; i++)
                {
                    if (left[i] > 0 && (bestIndex < 0 || left[i] > left[bestIndex]))
                    {
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                left[bestIndex]--;
                AddPlan(cart, forts[bestIndex], resource, 1);
            }
        }

        /// <summary>노선 점 i에서 방향 d로 정거장을 하나씩 지나며 cb를 부릅니다. cb가 true면 멈춥니다.</summary>
        private void Walk(Line line, int i, int direction, Func<Node, bool> visitor)
        {
            var points = line.Points;
            int pointCount = points.Count;
            if (pointCount < 2)
            {
                return;
            }

            for (int stepIndex = 0; stepIndex < 2 * pointCount + 2; stepIndex++)
            {
                int nextIndex = i + direction;
                if (line.Loop)
                {
                    nextIndex = (nextIndex + pointCount) % pointCount;
                }
                else if (nextIndex < 0 || nextIndex >= pointCount)
                {
                    direction = -direction;
                    nextIndex = i + direction;
                }
                i = nextIndex;
                int id = points[i].NodeId;
                if (id >= 0 && visitor(NodeById(id)))
                {
                    return;
                }
            }
        }

        /// <summary>마차 위치에서 다음 창고 전까지의 요새들과 도착 창고입니다. 같은 창고로 돌아오면 dest는 null입니다.</summary>
        private (List<Node> forts, Node destination) SegmentFrom(Line line, Cart cart, int fromId)
        {
            var forts = new List<Node>();
            Node destination = null;
            Walk(line, cart.PointIndex, cart.Direction, node =>
            {
                if (node.Type == NodeType.Warehouse)
                {
                    if (node.Id != fromId)
                    {
                        destination = node;
                    }

                    return true;
                }
                if (node.Type == NodeType.Fort && !forts.Contains(node))
                {
                    forts.Add(node);
                }

                return false;
            });
            return (forts, destination);
        }

        /// <summary>창고에서 출발하는 모든 구간의 요새입니다. 순환 노선은 진행 방향만 봅니다.</summary>
        private List<Node> WarehouseForts(Node warehouse)
        {
            var outList = new List<Node>();
            foreach (var line in LinesWith(warehouse.Id))
            {
                int warehouseIndex = line.Points.FindIndex(point => point.NodeId == warehouse.Id);
                if (warehouseIndex < 0 || line.Points.Count < 2)
                {
                    continue;
                }

                var dirs = new List<int>();
                if (line.Loop)
                {
                    foreach (var cart in line.Carts)
                    {
                        if (!dirs.Contains(cart.Direction))
                        {
                            dirs.Add(cart.Direction);
                        }
                    }
                }
                else
                {
                    dirs.Add(1);
                    dirs.Add(-1);
                }
                if (dirs.Count == 0)
                {
                    dirs.Add(1);
                }

                foreach (int direction in dirs)
                {
                    Walk(line, warehouseIndex, direction, node =>
                    {
                        if (node.Type == NodeType.Warehouse)
                        {
                            return true;
                        }

                        if (node.Type == NodeType.Fort && !outList.Contains(node))
                        {
                            outList.Add(node);
                        }

                        return false;
                    });
                }
            }
            return outList;
        }

        /// <summary>창고 목표량 = min(자원 칸 수, 구간 요새 실수요 합 + 올림(WH_RESERVE x 자원 칸 수))입니다.</summary>
        public int WarehouseTarget(Node warehouse, Res resource) => ComputeWarehouseTarget(warehouse, resource, WarehouseForts(warehouse));

        private int ComputeWarehouseTarget(Node warehouse, Res resource, List<Node> forts)
        {
            int slot = warehouse.Slot(resource), need = 0;
            foreach (var fort in forts)
            {
                need += NeedOf(fort, resource);
            }

            return Math.Min(slot, need + (int)Math.Ceiling(Balance.WH_RESERVE * slot));
        }

        private void ArriveWarehouse(Line line, Cart cart, Node here)
        {
            ReleasePlan(cart, -1);                                // 1. 남아 있던 예약 해제
            var segment = SegmentFrom(line, cart, here.Id);              // 2. 구간 파악
            int moved = 0;
            bool full = false;
            cart.TransferFood = cart.TransferWeapon = 0;
            foreach (var resource in s_resources)
            {
                int pool = here.Get(resource) + cart.Get(resource), need = 0;
                foreach (var fort in segment.forts)
                {
                    need += NeedOf(fort, resource);
                }

                int fortAmount = Math.Min(Math.Min(need, pool), cart.Slot(resource));   // 3. 요새 몫
                Reserve(cart, segment.forts, resource, fortAmount);                           // 4. 예약
                int x = 0;
                if (segment.destination != null)                                        // 5. 도착 창고 몫 (충족률 맞추기)
                {
                    var next = segment.destination;
                    int targetHere = WarehouseTarget(here, resource), targetNext = WarehouseTarget(next, resource), spareHere = pool - fortAmount, stockNext = next.Get(resource);
                    if (targetHere + targetNext > 0)
                    {
                        x = (int)Math.Floor((double)(spareHere * targetNext - stockNext * targetHere) / (targetHere + targetNext));
                        x = Math.Min(Math.Min(x, targetNext - stockNext), Math.Min(spareHere, cart.Slot(resource) - fortAmount));
                    }
                    if (x < Math.Max(1, RoundToInt(Balance.WH_MIN_TRANSFER)))
                    {
                        x = 0;
                    }
                }
                int goal = fortAmount + x;                                      // 6. 목표 적재량에 맞춘다
                if (cart.Get(resource) > goal)
                {
                    int unloaded = Math.Min(cart.Get(resource) - goal, WarehouseRoom(here, resource));
                    cart.Add(resource, -unloaded);
                    here.Add(resource, unloaded);
                    moved += unloaded;
                    if (cart.Get(resource) > goal)
                    {
                        full = true;                        // 7. 못 내린 짐은 마차에 남는다
                    }
                }
                else
                {
                    int loadAmount = Math.Min(goal - cart.Get(resource), here.Get(resource));
                    here.Add(resource, -loadAmount);
                    cart.Add(resource, loadAmount);
                    moved += loadAmount;
                }
                if (resource == Res.Food)
                {
                    cart.TransferFood = x;
                }
                else
                {
                    cart.TransferWeapon = x;
                }
            }
            if (moved > 0)
            {
                here.Pulse = .5f;
            }

            if (full)
            {
                Bark("whfull", LineTexts.One("aide", "창고의 해당 자원 칸이 가득 찼습니다. 실어 갈 노선을 잇거나 창고 칸 구성을 바꿔 주십시오."), 40f);
            }
        }

        private void ArriveFort(Line line, Cart cart, Node fort)
        {
            int deliveredTotal = 0;
            // 1. 이 요새에 예약해 둔 몫을 내림 (빈 칸, 적재량 한도 안에서). 예약은 내린 양과 상관없이 푼다
            foreach (var resource in s_resources)
            {
                int amount = Math.Min(Math.Min(PlanOf(cart, fort, resource), Math.Max(0, FortCap - fort.Get(resource))), cart.Get(resource));
                fort.Add(resource, amount);
                cart.Add(resource, -amount);
                deliveredTotal += amount;
            }
            ReleasePlan(cart, fort.Id);
            // 2. 남은 요새들의 예약 몫을 뺀 여분은 요새끼리 나눔: 실수요가 큰 요새부터 1개씩, 이 요새는 자기 몫만
            var rest = FortsOnRoute(line, cart);
            rest.Remove(fort);
            var group = new List<Node> { fort };
            group.AddRange(rest);
            foreach (var resource in s_resources)
            {
                int extra = cart.Get(resource);
                foreach (var node in rest)
                {
                    extra -= PlanOf(cart, node, resource);
                }

                if (extra <= 0)
                {
                    continue;
                }

                var fillLevels = new int[group.Count];
                for (int i = 0; i < fillLevels.Length; i++)
                {
                    fillLevels[i] = group[i].Get(resource) + group[i].Reserved(resource);
                }

                int mine = 0;
                for (int unitIndex = 0; unitIndex < extra; unitIndex++)
                {
                    int bestIndex = -1;
                    for (int i = 0; i < fillLevels.Length; i++)
                    {
                        if (fillLevels[i] < FortCap && (bestIndex < 0 || fillLevels[i] < fillLevels[bestIndex]))
                        {
                            bestIndex = i;
                        }
                    }

                    if (bestIndex < 0)
                    {
                        break;
                    }

                    fillLevels[bestIndex]++;
                    if (bestIndex == 0)
                    {
                        mine++;
                    }
                }
                fort.Add(resource, mine);
                cart.Add(resource, -mine);
                deliveredTotal += mine;
            }
            if (deliveredTotal > 0)
            {
                fort.Pulse = .5f;
                Delivered += deliveredTotal;
            }
        }

        private void Serve(Line line, Cart cart, Node node)
        {
            if (node.Type == NodeType.Fort)
            {
                ArriveFort(line, cart, node);
            }
            else if (node.Type == NodeType.Warehouse)
            {
                ArriveWarehouse(line, cart, node);
            }
            else if (node.Type == NodeType.Stable)
            {
                // 마굿간 통과: 설정한 칸 구성을 마차에 적용하고, 칸을 넘는 자원은 마굿간에 남김
                if (line.Id != node.LineId)
                {
                    return;
                }

                cart.SlotFood = line.CartFoodSlots;
                cart.SlotWeapon = CartCapacity() - line.CartFoodSlots;
                foreach (var resource in s_resources)
                {
                    int over = Math.Max(0, cart.Get(resource) - cart.Slot(resource));
                    cart.Add(resource, -over);
                    node.Add(resource, over);
                    // 마굿간에 남은 자원은 그 자원 칸이 비면 다시 싣는다
                    int back = Math.Min(node.Get(resource), CFree(cart, resource));
                    node.Add(resource, -back);
                    cart.Add(resource, back);
                }
            }
            else if (node.Type == NodeType.Farm || node.Type == NodeType.Forge)
            {
                // 노선에 요새나 창고가 있을 때만 해당 자원 칸이 빈 만큼 싣는다
                bool hasFortOrWarehouse = false;
                foreach (int id in line.Nodes)
                {
                    var nodeType = NodeById(id).Type;
                    if (nodeType == NodeType.Fort || nodeType == NodeType.Warehouse)
                    {
                        hasFortOrWarehouse = true;
                        break;
                    }
                }
                if (!hasFortOrWarehouse)
                {
                    return;
                }

                var resource = node.Type == NodeType.Farm ? Res.Food : Res.Weapon;
                int take = Math.Max(0, Math.Min(CFree(cart, resource), node.Stock));
                node.Stock -= take;
                cart.Add(resource, take);
            }
        }

        /// <summary>마차의 현재 위치와 방향에서 다음 창고 전까지 지날 요새들입니다. 현재 위치는 제외하고 만나는 순서대로입니다.</summary>
        private List<Node> FortsOnRoute(Line line, Cart cart)
        {
            var outList = new List<Node>();
            Walk(line, cart.PointIndex, cart.Direction, node =>
            {
                if (node.Type == NodeType.Warehouse)
                {
                    return true;
                }

                if (node.Type == NodeType.Fort && !outList.Contains(node))
                {
                    outList.Add(node);
                }

                return false;
            });
            return outList;
        }

        private void StepCart(Line line, Cart cart, float dt)
        {
            var points = line.Points;
            if (points.Count < 2)
            {
                cart.Position = NodeById(line.Id).Position;
                return;
            }
            if (cart.Dwell > 0f)
            {
                cart.Dwell -= dt;
                return;
            }
            float move = CartSpeed() * dt;
            int guard = 0;
            while (move > 0f && guard++ < 12)
            {
                var targetPoint = points[cart.PointIndex];
                float dx = targetPoint.Position.X - cart.Position.X, dy = targetPoint.Position.Y - cart.Position.Y, distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= move)
                {
                    cart.Position = targetPoint.Position;
                    move -= distance;
                    if (targetPoint.NodeId >= 0)
                    {
                        Serve(line, cart, NodeById(targetPoint.NodeId));
                        cart.Dwell = Balance.DWELL;
                    }
                    int nextIndex = cart.PointIndex + cart.Direction;
                    if (line.Loop)
                    {
                        nextIndex = (nextIndex + points.Count) % points.Count;     // 순환: 한 방향으로 계속
                    }
                    else if (nextIndex < 0 || nextIndex >= points.Count)
                    {
                        cart.Direction *= -1;
                        nextIndex = cart.PointIndex + cart.Direction;
                    }
                    cart.PointIndex = nextIndex;
                    if (cart.Dwell > 0f)
                    {
                        break;
                    }
                }
                else
                {
                    cart.Position.X += dx / distance * move;
                    cart.Position.Y += dy / distance * move;
                    cart.Angle = MathF.Atan2(dy, dx);
                    move = 0f;
                }
            }
        }
    }
}
