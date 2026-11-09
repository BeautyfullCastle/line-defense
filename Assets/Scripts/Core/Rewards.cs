using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    // 웨이브 사이 보급 선택지
    public sealed partial class Game
    {
        private sealed class RewardDef
        {
            public string Icon;
            public Func<string> Title;
            public Func<string> Desc;
            public Func<bool> Ok;
            public Action Apply;
            /// <summary>최대 선택 횟수를 강화 상한으로 쓰는 선택지입니다. 선택 횟수로 막지 않습니다.</summary>
            public bool Capped;
        }

        private readonly Dictionary<string, RewardDef> _rewards = new Dictionary<string, RewardDef>();
        private readonly List<string> _rewardKeys = new List<string>();

        /// <summary>정비 단계에 들어갈 때 채워지는 선택지 키 목록입니다.</summary>
        public List<string> RewardChoices = new List<string>();

        private string SupplyName(string k) => Supplies[k].Name;
        private string Times(string k) => SupplyValue(k) > 1f ? " ×" + FormatNumber(SupplyValue(k)) : "";
        private static string FormatMax(float value) => float.IsPositiveInfinity(value) ? "제한 없음" : FormatNumber(value);

        private bool HasWarehouse()
        {
            if (WarehouseTokens > 0)
            {
                return true;
            }

            foreach (var node in Nodes)
            {
                if (node.Type == NodeType.Warehouse)
                {
                    return true;
                }
            }

            return false;
        }

        private void DefineReward(string key, string icon, Func<string> title, Func<string> desc, Func<bool> isEligible, Action apply, bool capped = false)
        {
            _rewards[key] = new RewardDef { Icon = icon, Title = title, Desc = desc, Ok = isEligible, Apply = apply, Capped = capped };
            _rewardKeys.Add(key);
        }

        private void BuildRewards()
        {
            DefineReward("stable", "stable", () => SupplyName("stable") + Times("stable"), () => "새 노선과 마차 한 대",
                () => Lines.Count + StableTokens < LineColorCount,
                () => StableTokens = Math.Min(LineColorCount - Lines.Count, StableTokens + RoundToInt(SupplyValue("stable"))));
            DefineReward("fort", "fort", () => SupplyName("fort") + Times("fort"), () => "사거리 안의 몬스터를 공격",
                () => true, () => FortTokens += RoundToInt(SupplyValue("fort")));
            DefineReward("warehouse", "wh", () => SupplyName("warehouse") + Times("warehouse"),
                () => "여러 노선을 잇는 환승 거점 · 자원 " + WarehouseCapacity() + "개 보관", () => true, () =>
                {
                    int warehouseCount = WarehouseTokens;
                    foreach (var node in Nodes)
                    {
                        if (node.Type == NodeType.Warehouse)
                        {
                            warehouseCount++;
                        }
                    }

                    WarehouseTokens += RoundToInt(SupplyValue("warehouse"));
                    if (warehouseCount >= 1)
                    {
                        Bark("wh2", LineTexts.One("aide", "창고를 성벽 가까이에 하나 더 두면, 전방 요새들이 가까운 곳에서 보급받습니다."));
                    }
                });
            DefineReward("cartExtra", "cart", () => SupplyName("cartExtra") + Times("cartExtra"), () => "원하는 노선에 마차를 한 대 더 배치",
                () => true, () => CartTokens += RoundToInt(SupplyValue("cartExtra")));
            DefineReward("gold", "gold", () => SupplyName("gold") + " · " + GoldSupplyAmount() + "골드",
                () => "즉시 지급 · 보유 " + FormatNumber(Gold) + " → " + FormatNumber(Gold + GoldSupplyAmount()) + "골드",
                () => GoldSupplyAmount() > 0, () => Gold += GoldSupplyAmount());
            DefineReward("wall", "wall", () => SupplyName("wall"), () => "성벽 내구도 +" + FormatNumber(Balance.WALL_REPAIR),
                () => Wall < WallMax, () => Wall = MathF.Min(WallMax, Wall + Balance.WALL_REPAIR));
            DefineReward("cartSpd", "speed", () => SupplyName("cartSpd") + " · 속도 +" + RoundToInt(Balance.CART_SPD_UP * 100f) + "%",
                () => "모든 마차에 적용 · 현재 " + RoundToInt(100f * (1f + Balance.CART_SPD_UP * CartSpeedLevel)) + "% · 강화 " + CartSpeedLevel + "/" + FormatMax(MaxCartSpeedLevel()),
                () => CartSpeedLevel < MaxCartSpeedLevel(), () => CartSpeedLevel++, true);
            DefineReward("cartCap", "cap", () => SupplyName("cartCap") + " · 적재 +" + RoundToInt(Balance.CART_CAP_UP),
                () => "모든 마차에 적용 · 현재 " + CartCapacity() + "칸 · 강화 " + CartCapacityLevel + "/" + FormatMax(MaxCartCapacityLevel()),
                () => CartCapacityLevel < MaxCartCapacityLevel(), () =>
                {
                    CartCapacityLevel++;
                    int capacityIncrease = RoundToInt(Balance.CART_CAP_UP), foodIncrease = (int)Math.Ceiling(capacityIncrease / 2.0), weaponIncrease = capacityIncrease - foodIncrease;
                    foreach (var line in Lines)
                    {
                        line.CartFoodSlots += foodIncrease;
                        foreach (var cart in line.Carts)
                        {
                            cart.SlotFood += foodIncrease;
                            cart.SlotWeapon += weaponIncrease;
                        }
                    }
                }, true);
            DefineReward("whCap", "whcap", () => SupplyName("whCap") + " · 보관 +" + RoundToInt(Balance.WH_CAP_STEP),
                () => "모든 창고에 적용 · 현재 " + WarehouseCapacity() + "개 · 확장 " + WarehouseCapacityLevel + "/" + FormatMax(MaxWarehouseCapacityLevel()),
                () => WarehouseCapacityLevel < MaxWarehouseCapacityLevel() && HasWarehouse(), () =>
                {
                    WarehouseCapacityLevel++;
                    foreach (var node in Nodes)
                    {
                        if (node.Type != NodeType.Warehouse)
                        {
                            continue;
                        }

                        node.SlotFood += (int)Math.Ceiling(RoundToInt(Balance.WH_CAP_STEP) / 2.0);
                        node.SlotWeapon = WarehouseCapacity() - node.SlotFood;
                    }
                }, true);
            // 한계 돌파: 해당 강화가 상한 근처(1회 남음 이하)일 때만 나옴
            DefineReward("capStable", "capup", () => SupplyName("capStable"),
                () => "역참 마필 개량 · 수레 개조 최대 횟수 +" + FormatNumber(SupplyValue("capStable")) + " (속도 " + FormatMax(MaxCartSpeedLevel()) + " · 적재 " + FormatMax(MaxCartCapacityLevel()) + "회)",
                () => SupplyValue("capStable") > 0f && (CartSpeedLevel >= MaxCartSpeedLevel() - 1f || CartCapacityLevel >= MaxCartCapacityLevel() - 1f), () => StableLimitBreaks++);
            DefineReward("whUpg", "capup", () => SupplyName("whUpg"),
                () => "창고 확장 최대 횟수 " + FormatMax(MaxWarehouseCapacityLevel()) + "회 → " + FormatMax(MaxWarehouseCapacityLevel() + SupplyValue("whUpg")) + "회",
                () => SupplyValue("whUpg") > 0f && HasWarehouse() && WarehouseCapacityLevel >= MaxWarehouseCapacityLevel() - 1f, () => WarehouseLimitBreaks++);
            DefineReward("capFort", "capup", () => SupplyName("capFort"),
                () => "요새 공격력 · 공격 속도 강화 최대 레벨 " + MaxUpgradeLevel() + " → " + (MaxUpgradeLevel() + RoundToInt(SupplyValue("capFort"))),
                () =>
                {
                    if (SupplyValue("capFort") <= 0f)
                    {
                        return false;
                    }

                    foreach (var node in Nodes)
                    {
                        if (node.Type == NodeType.Fort && Math.Max(node.AttackLevel, node.SpeedLevel) >= MaxUpgradeLevel() - 1)
                        {
                            return true;
                        }
                    }

                    return false;
                }, () => FortLimitBreaks++);
        }

        public string RewardTitle(string key) => _rewards[key].Title();
        public string RewardDesc(string key) => _rewards[key].Desc();
        public string RewardIcon(string key) => _rewards[key].Icon;

        /// <summary>이번 정비에 나올 수 있는지 확인합니다: 가중치, 첫 등장 웨이브, 최대 선택 횟수, 보상 자체 조건 순입니다.</summary>
        private bool Eligible(string k)
        {
            if (!Supplies.TryGetValue(k, out var supply) || !_rewards.TryGetValue(k, out var reward))
            {
                return false;
            }

            if (!(supply.Weight > 0f) || Wave < supply.FirstWave)
            {
                return false;
            }

            _pickCounts.TryGetValue(k, out int pickCount);
            if (!reward.Capped && supply.MaxPicks > 0 && pickCount >= supply.MaxPicks)
            {
                return false;
            }

            return reward.Ok();
        }

        /// <summary>선택지 3개를 가중치 비율로 뽑습니다(중복 없음). 성벽이 70% 미만이면 성벽 보수를 먼저 넣습니다.</summary>
        private List<string> RewardSet()
        {
            var pool = new List<string>();
            foreach (var k in _rewardKeys)
            {
                if (Eligible(k))
                {
                    pool.Add(k);
                }
            }

            var set = new List<string>();
            if (pool.Contains("wall") && Wall < WallMax * .7f)
            {
                set.Add("wall");
            }

            while (set.Count < 3)
            {
                var cand = pool.FindAll(k => !set.Contains(k));
                if (cand.Count == 0)
                {
                    break;
                }

                float totalWeight = 0f;
                foreach (var k in cand)
                {
                    totalWeight += Supplies[k].Weight;
                }

                float roll = (float)_random.NextDouble() * totalWeight;
                string chosen = cand[cand.Count - 1];
                foreach (var k in cand)
                {
                    roll -= Supplies[k].Weight;
                    if (roll < 0f)
                    {
                        chosen = k;
                        break;
                    }
                }
                set.Add(chosen);
            }
            return set;
        }

        /// <summary>정비 단계 안내 문구입니다. 이번 정비 후 열릴 침투로와 개척지를 알려 줍니다.</summary>
        public string UpcomingNote()
        {
            var notes = new List<string>();
            if (Wave % RoundToInt(Balance.ROUTE_EVERY) == 0 && Routes.Count < RoundToInt(Balance.ROUTE_MAX))
            {
                notes.Add("새 침투로가 열립니다");
            }

            if (Wave % ExpandEvery == 0 && HalfWidth < MaxHalfWidth)
            {
                notes.Add("개척지가 넓어집니다");
            }

            return notes.Count > 0
                ? "중앙이 허가한 권한 중 하나를 고르세요. 이번 정비 후 " + string.Join(", ", notes) + "."
                : "중앙이 허가한 권한 중 하나를 고르세요.";
        }

        /// <summary>정비 단계의 황실 전령 인용구입니다.</summary>
        public string RewardQuote() => LineTexts.MsgQuotes[(Wave - 1) % LineTexts.MsgQuotes.Length];

        private void BeforeReward()
        {
            RewardChoices = RewardSet();
            Emit(new GameEvent { Type = GameEventType.RewardReady });
        }

        /// <summary>선택지를 적용하고 개척지 확장과 침투로 추가를 처리한 뒤 다음 대기 단계로 넘어갑니다.</summary>
        public void PickReward(string key)
        {
            if (Phase != Phase.Reward || key == null || !_rewards.TryGetValue(key, out var reward))
            {
                return;
            }

            reward.Apply();
            _pickCounts.TryGetValue(key, out int pickCount);
            _pickCounts[key] = pickCount + 1;
            var msgs = new List<string>();
            if (Wave % ExpandEvery == 0 && HalfWidth < MaxHalfWidth)
            {
                HalfWidth = MathF.Min(MaxHalfWidth, HalfWidth + ExpandHalfWidth);
                Depth = MathF.Min(MaxDepth, Depth + ExpandDepth);
                WallMax += Balance.WALL_UP;
                Wall += Balance.WALL_UP;
                msgs.Add("개척지 확장");
            }
            if (Wave % RoundToInt(Balance.ROUTE_EVERY) == 0 && Routes.Count < RoundToInt(Balance.ROUTE_MAX))
            {
                AddRoute(false);
                FortTokens += 1;
                msgs.Add("새 침투로 발견");
                msgs.Add("요새 권한 +1");
            }
            if (msgs.Count > 0)
            {
                Toast(string.Join(" · ", msgs));
            }

            RewardChoices.Clear();
            Phase = Phase.Countdown;
            Countdown = Balance.BETWEEN;
        }
    }
}
