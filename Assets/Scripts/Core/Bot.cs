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
        private readonly Game _game;

        // 같은 침투로에 요새를 더 놓을 때 성벽을 따라 벌려 보는 간격(월드 단위)
        private static readonly float[] s_fortOffsets = { 0f, 60f, -60f, 120f, -120f, 90f, -90f, 160f, -160f, 30f, -30f };

        public Bot(Game game)
        {
            _game = game;
        }

        /// <summary>헤드리스 시뮬레이션용입니다. 보상 단계면 보상을 고른 뒤 Act를 호출합니다.</summary>
        public void Tick()
        {
            if (_game.Phase == Phase.Over)
            {
                return;
            }

            if (_game.Phase == Phase.Reward)
            {
                _game.PickReward(ChooseReward());
                if (_game.Phase == Phase.Reward)
                {
                    return;
                }
            }
            Act();
        }

        // 이번 Act에서 남은 명령 수. 게임 안에서는 1로 두어 한 번에 하나씩 보이게 함
        private int _budget;
        private bool CanAct => _budget > 0;
        private void Used() => _budget--;

        /// <summary>
        /// 보상 선택을 뺀 건설, 노선 편집, 마차, 강화 행동을 수행합니다.
        /// maxActions개의 명령을 쓰면 멈춥니다. 반환값은 실제로 쓴 명령 수입니다.
        /// </summary>
        public int Act(int maxActions = int.MaxValue)
        {
            if (_game.Phase == Phase.Over || _game.Phase == Phase.Reward)
            {
                return 0;
            }

            _budget = maxActions;
            // 새로 이을 생산처나 요새가 생기면 순환을 풀어 다시 늘릴 수 있게 함
            if (AnyFreeWork())
            {
                foreach (var line in _game.Lines)
                {
                    if (line.Loop && CanAct)
                    {
                        _game.BreakLoop(line);
                        Used();
                    }
                }
            }

            PlaceForts();
            BuildLines();
            PlaceStable();
            BuildLines();
            AddCarts();
            SetSlots();
            SpendGold();
            CloseLoops();
            return maxActions - _budget;
        }

        private bool AnyFreeWork()
        {
            foreach (var node in _game.Nodes)
            {
                if ((node.Type == NodeType.Farm || node.Type == NodeType.Forge || node.Type == NodeType.Fort) && IsFree(node))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 더 이을 것이 없으면 요새가 있는 노선을 순환으로 닫습니다.
        /// 마차가 왔던 길을 되짚지 않고 요새에서 마굿간으로 바로 돌아와 보급이 잦아집니다(시뮬 500판 평균 +0.5웨이브).
        /// </summary>
        private void CloseLoops()
        {
            if (AnyFreeWork())
            {
                return;
            }

            foreach (var line in _game.Lines)
            {
                if (line.Loop || line.Nodes.Count < 3 || FortCount(line) == 0 || !CanAct)
                {
                    continue;
                }

                _game.DragTo(line, LineEnd.Tail, _game.NodeById(line.Nodes[0]));
                if (line.Loop)
                {
                    Used();
                }
            }
        }

        // 노선 요새들의 빈 칸 비율대로 식량/무기 칸을 나누되 각 자원 최소 1칸은 남김
        private void SetSlots()
        {
            int capacity = _game.CartCapacity();
            foreach (var line in _game.Lines)
            {
                int neededFood = 0, neededWeapons = 0;
                foreach (int id in line.Nodes)
                {
                    var node = _game.NodeById(id);
                    if (node.Type != NodeType.Fort)
                    {
                        continue;
                    }

                    neededFood += Math.Max(0, (int)_game.Balance.FORT_CAP - node.Food);
                    neededWeapons += Math.Max(0, (int)_game.Balance.FORT_CAP - node.Weapon);
                }
                int food = neededFood + neededWeapons == 0 ? (capacity + 1) / 2 : (int)Math.Round(capacity * (double)neededFood / (neededFood + neededWeapons));
                food = Math.Max(1, Math.Min(capacity - 1, food));
                if (line.CartFoodSlots == food)
                {
                    continue;
                }

                if (!CanAct)
                {
                    return;
                }

                _game.SetLineFoodSlots(line, food);
                Used();
            }
        }

        // ---------- 보상 ----------

        /// <summary>현재 보상 후보 중 고를 키를 반환합니다. 후보가 없으면 null입니다.</summary>
        public string ChooseReward()
        {
            var choices = _game.RewardChoices;
            if (choices.Count == 0)
            {
                return null;
            }

            if (choices.Contains("fort") && AnyUncoveredRoute())
            {
                return "fort";
            }

            if (choices.Contains("wall") && _game.Wall < _game.WallMax * .6f)
            {
                return "wall";
            }

            if (choices.Contains("stable") && _game.Lines.Count + _game.StableTokens < 3)
            {
                return "stable";
            }

            foreach (var k in new[] { "cartExtra", "cartCap", "cartSpd", "fort", "stable", "gold" })
            {
                if (choices.Contains(k))
                {
                    return k;
                }
            }

            foreach (var k in choices)
            {
                if (k != "warehouse" && k != "whCap" && k != "whUpg")
                {
                    return k;
                }
            }

            return choices[0];
        }

        // ---------- 요새 ----------

        private static Vec2 WallPoint(float angle) => new Vec2(MathF.Cos(angle) * Game.WallRadius, Game.WallRadius + MathF.Sin(angle) * Game.WallRadius);

        private int Coverage(Route route)
        {
            var wallPoint = WallPoint(route.Angle);
            int count = 0;
            foreach (var fort in _game.Nodes)
            {
                if (fort.Type == NodeType.Fort && Vec2.Distance(fort.Position, wallPoint) <= _game.Balance.RANGE)
                {
                    count++;
                }
            }

            return count;
        }

        private bool AnyUncoveredRoute()
        {
            foreach (var route in _game.Routes)
            {
                if (Coverage(route) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void PlaceForts()
        {
            while (_game.FortTokens > 0 && CanAct)
            {
                Route worst = null;
                int worstCoverage = int.MaxValue;
                foreach (var route in _game.Routes)
                {
                    int coverage = Coverage(route);
                    if (coverage < worstCoverage)
                    {
                        worstCoverage = coverage;
                        worst = route;
                    }
                }
                if (worst == null)
                {
                    return;
                }

                bool placed = false;
                foreach (float off in s_fortOffsets)
                {
                    var position = WallPoint(worst.Angle + off / Game.WallRadius);
                    if (_game.CanPlace(BuildKind.Fort, position) && _game.Place(BuildKind.Fort, position) != null)
                    {
                        placed = true;
                        Used();
                        break;
                    }
                }
                if (!placed)
                {
                    return;
                }
            }
        }

        // ---------- 노선 ----------

        private static bool HasNodeOfType(Game game, Line line, NodeType nodeType)
        {
            foreach (int id in line.Nodes)
            {
                if (game.NodeById(id).Type == nodeType)
                {
                    return true;
                }
            }

            return false;
        }

        private int FortCount(Line line)
        {
            int count = 0;
            foreach (int id in line.Nodes)
            {
                if (_game.NodeById(id).Type == NodeType.Fort)
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsFree(Node node)
        {
            foreach (var _ in _game.LinesWith(node.Id))
            {
                return false;
            }

            return true;
        }

        private Node Nearest(NodeType type, Vec2 from, Func<Node, bool> filter)
        {
            Node best = null;
            float bestDistance = float.MaxValue;
            foreach (var node in _game.Nodes)
            {
                if (node.Type != type || !filter(node))
                {
                    continue;
                }

                float distance = Vec2.Distance(node.Position, from);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }
            return best;
        }

        private Vec2 TailPosition(Line line) => _game.NodeById(line.Nodes[line.Nodes.Count - 1]).Position;

        private bool Extend(Line line, Node target)
        {
            if (target == null || !CanAct)
            {
                return false;
            }

            _game.DragTo(line, LineEnd.Tail, target);
            if (!line.Nodes.Contains(target.Id))
            {
                return false;
            }

            Used();
            return true;
        }

        /// <summary>마굿간 - 농장 - 대장간 - 요새 순으로 한 번에 한 정거장씩 이어 변화가 없을 때까지 반복합니다.</summary>
        private void BuildLines()
        {
            for (int guard = 0; guard < 40; guard++)
            {
                bool changed = false;
                foreach (var line in _game.Lines)
                {
                    if (line.Loop)
                    {
                        continue;
                    }

                    var tail = TailPosition(line);
                    // 남는 농장이 없으면 대장간이라도 이음. 마굿간만 놓고 못 이은 노선이 다음 Act에서 이어지게 함
                    bool extended = !HasNodeOfType(_game, line, NodeType.Farm) && Extend(line, Nearest(NodeType.Farm, tail, IsFree));
                    if (!extended && !HasNodeOfType(_game, line, NodeType.Forge))
                    {
                        extended = Extend(line, Nearest(NodeType.Forge, TailPosition(line), IsFree));
                    }

                    changed |= extended;
                }
                changed |= AttachForts();
                // 스스로 늘릴 마굿간이 없으면 남는 생산처를 꼬리가 가장 가까운 노선에 붙임
                if (_game.StableTokens <= 0 || _game.Lines.Count >= Game.LineColorCount)
                {
                    changed |= AttachSpareProducer();
                }

                if (!changed)
                {
                    return;
                }
            }
        }

        // 농장과 대장간을 가져올 수 있는 만큼 가져온 노선만 요새로 넘어감
        private bool ProducersDone(Line line)
        {
            bool farm = HasNodeOfType(_game, line, NodeType.Farm), forge = HasNodeOfType(_game, line, NodeType.Forge);
            if (!farm && !forge)
            {
                return false;
            }

            return (farm || Nearest(NodeType.Farm, TailPosition(line), IsFree) == null) && (forge || Nearest(NodeType.Forge, TailPosition(line), IsFree) == null);
        }

        private bool AttachForts()
        {
            bool changed = false;
            // 노선이 없는 요새는 꼬리가 가까우면서 요새가 적은 노선에 붙임
            foreach (var fort in _game.Nodes)
            {
                if (fort.Type != NodeType.Fort || !IsFree(fort))
                {
                    continue;
                }

                Line best = null;
                float bestCost = float.MaxValue;
                foreach (var line in _game.Lines)
                {
                    if (line.Loop || !ProducersDone(line))
                    {
                        continue;
                    }

                    float cost = Vec2.Distance(TailPosition(line), fort.Position) + 150f * FortCount(line);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = line;
                    }
                }
                if (best != null)
                {
                    changed |= Extend(best, fort);
                }
            }
            // 요새가 하나도 없는 노선에는 가까운 요새를 공유시킴
            foreach (var line in _game.Lines)
            {
                if (line.Loop || FortCount(line) > 0 || !ProducersDone(line))
                {
                    continue;
                }

                changed |= Extend(line, Nearest(NodeType.Fort, TailPosition(line), node => !line.Nodes.Contains(node.Id)));
            }
            return changed;
        }

        private bool AttachSpareProducer()
        {
            foreach (var node in _game.Nodes)
            {
                if ((node.Type != NodeType.Farm && node.Type != NodeType.Forge) || !IsFree(node))
                {
                    continue;
                }

                Line best = null;
                float bestDistance = float.MaxValue;
                foreach (var line in _game.Lines)
                {
                    if (line.Loop)
                    {
                        continue;
                    }

                    float distance = Vec2.Distance(TailPosition(line), node.Position);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = line;
                    }
                }
                if (best != null && Extend(best, node))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>어느 노선도 가져가지 못한 생산처 옆에 마굿간을 놓음</summary>
        private void PlaceStable()
        {
            if (_game.StableTokens <= 0 || !CanAct)
            {
                return;
            }

            Node target = null;
            foreach (var node in _game.Nodes)
            {
                if ((node.Type != NodeType.Farm && node.Type != NodeType.Forge) || !IsFree(node))
                {
                    continue;
                }

                if (target == null || (node.Type == NodeType.Farm && target.Type != NodeType.Farm))
                {
                    target = node;
                }
            }
            if (target == null)
            {
                return;
            }

            var fort = Nearest(NodeType.Fort, target.Position, _ => true);
            var direction = fort != null ? fort.Position - target.Position : new Vec2(0f, -1f);
            float length = direction.Length;
            direction = length > 0f ? direction / length : new Vec2(0f, -1f);
            foreach (float radius in new[] { 70f, 100f, 130f })
            {
                foreach (float degrees in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
                {
                    float angle = degrees * MathF.PI / 180f, cosine = MathF.Cos(angle), sine = MathF.Sin(angle);
                    var position = target.Position + new Vec2(direction.X * cosine - direction.Y * sine, direction.X * sine + direction.Y * cosine) * radius;
                    if (!_game.CanPlace(BuildKind.Stable, position))
                    {
                        continue;
                    }

                    var node = _game.Place(BuildKind.Stable, position);
                    if (node == null)
                    {
                        return;
                    }

                    Used();
                    // 다른 노선이 같은 생산처를 먼저 가져가지 않게 바로 이음. 남은 명령이 없으면 다음 Act의 BuildLines가 이음
                    Extend(_game.LineById(node.LineId), target);
                    return;
                }
            }
        }

        // ---------- 마차, 골드 ----------

        private void AddCarts()
        {
            while (_game.CartTokens > 0 && CanAct)
            {
                Line best = null;
                float bestRatio = -1f;
                foreach (var line in _game.Lines)
                {
                    int forts = FortCount(line);
                    if (forts == 0)
                    {
                        continue;
                    }

                    float ratio = (float)forts / line.Carts.Count;
                    if (ratio > bestRatio)
                    {
                        bestRatio = ratio;
                        best = line;
                    }
                }
                if (best == null || !_game.AddCart(best))
                {
                    return;
                }

                Used();
            }
        }

        /// <summary>노선에 이어진 요새 중 레벨이 낮은 쪽부터, 공격력과 공격 속도를 번갈아 올림</summary>
        private void SpendGold()
        {
            int max = _game.MaxUpgradeLevel();
            for (int guard = 0; guard < 50 && CanAct; guard++)
            {
                Node pick = null;
                int pickLevel = int.MaxValue;
                bool upgradeAttack = true;
                foreach (var fort in _game.Nodes)
                {
                    if (fort.Type != NodeType.Fort || IsFree(fort))
                    {
                        continue;
                    }

                    bool attackIsLower = fort.AttackLevel <= fort.SpeedLevel;
                    int level = attackIsLower ? fort.AttackLevel : fort.SpeedLevel;
                    if (level >= max)
                    {
                        continue;
                    }

                    if (level < pickLevel)
                    {
                        pickLevel = level;
                        pick = fort;
                        upgradeAttack = attackIsLower;
                    }
                }
                if (pick == null || !_game.UpgradeFort(pick, upgradeAttack))
                {
                    return;
                }

                Used();
            }
        }
    }
}
