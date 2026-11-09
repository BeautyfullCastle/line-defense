using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    // 영역 계산과 건설 명령
    public sealed partial class Game
    {
        // ---------- 영역 ----------

        /// <summary>요새는 성벽 선 위에만 지을 수 있어 위치를 성벽 원 위로 끌어당깁니다.</summary>
        public Vec2 SnapWall(Vec2 point, out float off)
        {
            float dx = point.X, dy = point.Y - WallRadius, distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance == 0f)
            {
                distance = 1f;
            }

            off = MathF.Abs(distance - WallRadius);
            return new Vec2(dx / distance * (WallRadius + 4f), WallRadius + dy / distance * (WallRadius + 4f));
        }

        /// <summary>개척지 안인지 확인합니다. 개척지는 원과 |x|&lt;HW, y&lt;D의 교집합입니다.</summary>
        public bool Inside(Vec2 point, float padding = 0f) =>
            MathF.Sqrt(point.X * point.X + (point.Y - WallRadius) * (point.Y - WallRadius)) < WallRadius - padding && MathF.Abs(point.X) < HalfWidth - padding && point.Y < Depth - padding;

        /// <summary>침투로 각도를 정합니다. 위쪽 반원 안에서 기존 길과 가장 먼 곳을 고릅니다.</summary>
        private float AddRoute(bool first)
        {
            float span = MathF.Asin(MathF.Min(1f, (HalfWidth - 80f) / WallRadius)), upwardAngle = -MathF.PI / 2f;
            float best = upwardAngle;
            if (!first)
            {
                float bestDistance = -1f;
                for (int i = 0; i < 60; i++)
                {
                    float angle = upwardAngle + RandomRange(-span, span);
                    float nearestDistance = float.MaxValue;
                    foreach (var route in Routes)
                    {
                        nearestDistance = MathF.Min(nearestDistance, MathF.Abs(route.Angle - angle));
                    }

                    if (nearestDistance > bestDistance)
                    {
                        bestDistance = nearestDistance;
                        best = angle;
                    }
                }
            }
            else
            {
                best += RandomRange(-span, span) * .5f;
            }

            Routes.Add(new Route { Angle = best, Born = ElapsedTime, OpenedWave = first ? -10 : Wave });
            return best;
        }

        private void SpawnProducer(NodeType type)
        {
            for (int i = 0; i < 120; i++)
            {
                var position = new Vec2(RandomRange(-HalfWidth + 40f, HalfWidth - 40f), RandomRange(20f, Depth - 30f));
                if (!Inside(position, 40f) || !FarFromAll(position, 62f))
                {
                    continue;
                }

                Nodes.Add(new Node { Id = _nextNodeId++, Type = type, Position = position, Stock = 0, ProdTimer = RandomRange(0f, 3f), Born = ElapsedTime });
                if (ElapsedTime > 1f)
                {
                    Bark("new-producer", LineTexts.One("aide", "새 농장이나 대장간이 생기면 노선에 이어 주십시오."));
                }

                return;
            }
        }

        private bool FarFromAll(Vec2 point, float minimumDistance)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Vec2.Distance(Nodes[i].Position, point) <= minimumDistance)
                {
                    return false;
                }
            }

            return true;
        }

        // ---------- 건설 ----------

        public bool CanPlace(BuildKind kind, Vec2 point)
        {
            if (kind == BuildKind.Fort)
            {
                var snapped = SnapWall(point, out float off);
                return off < WallSnap && snapped.Y < WallRadius && MathF.Abs(snapped.X) < HalfWidth - 20f && FarFromAll(snapped, 48f);
            }
            return Inside(point, 20f) && FarFromAll(point, 48f);
        }

        private int Tokens(BuildKind kind) => kind == BuildKind.Stable ? StableTokens : kind == BuildKind.Fort ? FortTokens : WarehouseTokens;

        /// <summary>건물을 세우고 만든 노드를 반환합니다. 권한이 없거나 자리가 안 되면 null입니다.</summary>
        public Node Place(BuildKind kind, Vec2 position)
        {
            if (Tokens(kind) <= 0 || !CanPlace(kind, position))
            {
                return null;
            }

            if (kind == BuildKind.Fort)
            {
                position = SnapWall(position, out _);
            }

            Node node;
            if (kind == BuildKind.Stable)
            {
                int color = -1;
                for (int candidateColor = 0; candidateColor < LineColorCount && color < 0; candidateColor++)
                {
                    bool used = false;
                    foreach (var existing in Lines)
                    {
                        if (existing.ColorIndex == candidateColor)
                        {
                            used = true;
                            break;
                        }
                    }

                    if (!used)
                    {
                        color = candidateColor;
                    }
                }
                if (color < 0)
                {
                    return null;
                }

                node = new Node { Id = _nextNodeId++, Type = NodeType.Stable, Position = position, Born = ElapsedTime };
                node.LineId = node.Id;
                Nodes.Add(node);
                var line = new Line { Id = node.Id, ColorIndex = color, CartFoodSlots = (int)Math.Ceiling(CartCapacity() / 2.0) };
                line.Nodes.Add(node.Id);
                Lines.Add(line);
                Rebuild(line);
                line.Carts.Add(NewCart(line));
            }
            else if (kind == BuildKind.Warehouse)
            {
                int food = (int)Math.Ceiling(WarehouseCapacity() / 2.0);
                node = new Node { Id = _nextNodeId++, Type = NodeType.Warehouse, Position = position, SlotFood = food, SlotWeapon = WarehouseCapacity() - food, Born = ElapsedTime };
                Nodes.Add(node);
                Bark("warehouse", LineTexts.One("aide", "창고가 섰습니다. 여러 노선을 이 창고에서 만나게 하면, 한 노선이 내려 둔 짐을 다른 노선들이 실어 갑니다."));
            }
            else
            {
                node = new Node
                {
                    Id = _nextNodeId++,
                    Type = NodeType.Fort,
                    Position = position,
                    Food = Math.Min(RoundToInt(Balance.FORT_CAP), RoundToInt(Balance.FORT_START_FOOD)),
                    Weapon = Math.Min(RoundToInt(Balance.FORT_CAP), RoundToInt(Balance.FORT_START_WEAPON)),
                    Morale = MathF.Min(1f, Balance.FORT_START_MORALE),
                    Born = ElapsedTime,
                };
                Nodes.Add(node);
                Bark("fort", LineTexts.One("aide", "요새는 식량이 있어야 버티고, 무기가 있어야 싸웁니다. 꼭 노선에 이어 주십시오."));
            }
            if (kind == BuildKind.Stable)
            {
                StableTokens--;
            }
            else if (kind == BuildKind.Fort)
            {
                FortTokens--;
            }
            else
            {
                WarehouseTokens--;
            }

            Emit(new GameEvent { Type = GameEventType.NodePlaced, From = node.Position, NodeId = node.Id });
            return node;
        }

        private Cart NewCart(Line line)
        {
            var stable = NodeById(line.Id);
            int pointIndex = 0;
            for (int i = 0; i < line.Points.Count; i++)
            {
                if (line.Points[i].NodeId == line.Id)
                {
                    pointIndex = i;
                    break;
                }
            }

            return new Cart { Position = stable.Position, PointIndex = pointIndex, Direction = 1, Dwell = .3f, SlotFood = line.CartFoodSlots, SlotWeapon = CartCapacity() - line.CartFoodSlots };
        }

        /// <summary>
        /// 두 정거장 사이의 꺾임점(수평·수직 구간 + 45도 구간)을 구합니다. 꺾지 않으면 false입니다.
        /// 꺾임점 후보 두 개 중 center에서 먼 쪽을 골라, 노선이 둥글게 바깥으로 부풀게 합니다.
        /// </summary>
        public static bool Bend(Vec2 start, Vec2 end, Vec2 center, out Vec2 bendPoint)
        {
            float dx = end.X - start.X, dy = end.Y - start.Y, absoluteDx = MathF.Abs(dx), absoluteDy = MathF.Abs(dy);
            float signX = MathF.Sign(dx), signY = MathF.Sign(dy);
            bendPoint = default;
            if (absoluteDx < 1f || absoluteDy < 1f || MathF.Abs(absoluteDx - absoluteDy) < 1f)
            {
                return false;
            }
            // straight: 곧은 구간 먼저, diag: 45도 구간 먼저
            Vec2 straight, diag;
            if (absoluteDx > absoluteDy)
            {
                straight = new Vec2(start.X + signX * (absoluteDx - absoluteDy), start.Y);
                diag = new Vec2(start.X + signX * absoluteDy, end.Y);
            }
            else
            {
                straight = new Vec2(start.X, start.Y + signY * (absoluteDy - absoluteDx));
                diag = new Vec2(end.X, start.Y + signY * absoluteDx);
            }
            // 두 후보는 구간 중점에 대해 대칭이라 중심이 구간 위에 있으면 비김. 이때는 기존 모양 유지
            float straightDistance = Vec2.Distance(straight, center), diagonalDistance = Vec2.Distance(diag, center);
            bendPoint = diagonalDistance > straightDistance + 1f ? diag : straight;
            return true;
        }

        /// <summary>노선 정거장들의 무게중심입니다. 꺾임점을 바깥으로 고를 때 기준으로 씁니다.</summary>
        public static Vec2 Centroid(IReadOnlyList<Vec2> points)
        {
            if (points.Count == 0)
            {
                return default;
            }

            float x = 0f, y = 0f;
            foreach (var point in points)
            {
                x += point.X;
                y += point.Y;
            }
            return new Vec2(x / points.Count, y / points.Count);
        }

        private const float CornerR = 90f;
        private const int CornerSteps = 10;

        // 꺾임점을 2차 베지어 호로 바꿈. 마차도 같은 점을 따라가서 그림과 경로가 일치함
        // 짧은 다리를 거의 다 곡선으로 써서 노선이 둥글게 보이게 함
        private static void AddCorner(List<LinePoint> points, Vec2 from, Vec2 bend, Vec2 endPoint)
        {
            float startDistance = Vec2.Distance(bend, from), endDistance = Vec2.Distance(bend, endPoint);
            float radius = MathF.Min(CornerR, MathF.Min(startDistance, endDistance) * .9f);
            var curveStart = bend + (from - bend) * (radius / startDistance);
            var curveEnd = bend + (endPoint - bend) * (radius / endDistance);
            for (int step = 0; step <= CornerSteps; step++)
            {
                float progress = step / (float)CornerSteps, complement = 1f - progress;
                points.Add(new LinePoint { Position = curveStart * (complement * complement) + bend * (2f * progress * complement) + curveEnd * (progress * progress), NodeId = -1 });
            }
        }

        /// <summary>노선의 꺾임점을 다시 만들고 마차를 가장 가까운 점에 붙입니다.</summary>
        private void Rebuild(Line line)
        {
            foreach (var cart in line.Carts)
            {
                ReleasePlan(cart, -1);
            }

            var olds = new List<LinePoint>();
            foreach (var cart in line.Carts)
            {
                olds.Add(cart.PointIndex >= 0 && cart.PointIndex < line.Points.Count ? line.Points[cart.PointIndex] : null);
            }

            var stopNodes = new List<Node>();
            var positions = new List<Vec2>();
            foreach (int id in line.Nodes)
            {
                var node = NodeById(id);
                stopNodes.Add(node);
                positions.Add(node.Position);
            }
            var center = Centroid(positions);
            var points = new List<LinePoint>();
            for (int i = 0; i < stopNodes.Count; i++)
            {
                points.Add(new LinePoint { Position = stopNodes[i].Position, NodeId = stopNodes[i].Id });
                if (i < stopNodes.Count - 1 && Bend(stopNodes[i].Position, stopNodes[i + 1].Position, center, out var bendPoint))
                {
                    AddCorner(points, stopNodes[i].Position, bendPoint, stopNodes[i + 1].Position);
                }
            }
            if (line.Loop && stopNodes.Count >= 3)
            {
                if (Bend(stopNodes[stopNodes.Count - 1].Position, stopNodes[0].Position, center, out var bendPoint))
                {
                    AddCorner(points, stopNodes[stopNodes.Count - 1].Position, bendPoint, stopNodes[0].Position);
                }
            }
            else
            {
                line.Loop = false;
            }

            line.Points = points;
            for (int k = 0; k < line.Carts.Count; k++)
            {
                var old = olds[k];
                int best = 0;
                if (old != null)
                {
                    float bestDistance = 1e9f;
                    for (int i = 0; i < points.Count; i++)
                    {
                        float distance = Vec2.Distance(points[i].Position, old.Position);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = i;
                        }
                    }
                }
                line.Carts[k].PointIndex = best;
            }
        }
    }
}
