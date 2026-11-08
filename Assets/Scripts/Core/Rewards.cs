using System;
using System.Collections.Generic;

namespace LineDefense.Core
{
    // 웨이브 사이 보급 선택지
    public sealed partial class Game
    {
        sealed class RewardDef
        {
            public string Icon;
            public Func<string> Title;
            public Func<string> Desc;
            public Func<bool> Ok;
            public Action Apply;
            /// <summary>최대 선택 횟수를 강화 상한으로 쓰는 선택지입니다. 선택 횟수로 막지 않습니다.</summary>
            public bool Capped;
        }

        readonly Dictionary<string, RewardDef> rewards = new Dictionary<string, RewardDef>();
        readonly List<string> rewardKeys = new List<string>();

        /// <summary>정비 단계에 들어갈 때 채워지는 선택지 키 목록입니다.</summary>
        public List<string> RewardChoices = new List<string>();

        string Nm(string k) => Sup[k].Name;
        string Times(string k) => SupVal(k) > 1f ? " ×" + FmtNum(SupVal(k)) : "";
        static string FmtMax(float v) => float.IsPositiveInfinity(v) ? "제한 없음" : FmtNum(v);

        bool HasWarehouse()
        {
            if (TokWarehouse > 0) return true;
            foreach (var n in Nodes) if (n.Type == NodeType.Warehouse) return true;
            return false;
        }

        void Def(string key, string icon, Func<string> title, Func<string> desc, Func<bool> ok, Action apply, bool capped = false)
        {
            rewards[key] = new RewardDef { Icon = icon, Title = title, Desc = desc, Ok = ok, Apply = apply, Capped = capped };
            rewardKeys.Add(key);
        }

        void BuildRewards()
        {
            Def("stable", "stable", () => Nm("stable") + Times("stable"), () => "새 노선과 마차 한 대",
                () => Lines.Count + TokStable < LineColorCount,
                () => TokStable = Math.Min(LineColorCount - Lines.Count, TokStable + I(SupVal("stable"))));
            Def("fort", "fort", () => Nm("fort") + Times("fort"), () => "사거리 안의 몬스터를 공격",
                () => true, () => TokFort += I(SupVal("fort")));
            Def("warehouse", "wh", () => Nm("warehouse") + Times("warehouse"),
                () => "여러 노선을 잇는 환승 거점 · 자원 " + WhCap() + "개 보관", () => true, () =>
                {
                    int had = TokWarehouse;
                    foreach (var n in Nodes) if (n.Type == NodeType.Warehouse) had++;
                    TokWarehouse += I(SupVal("warehouse"));
                    if (had >= 1) Bark("wh2", LineTexts.One("aide", "창고를 성벽 가까이에 하나 더 두면, 전방 요새들이 가까운 곳에서 보급받습니다."));
                });
            Def("cartExtra", "cart", () => Nm("cartExtra") + Times("cartExtra"), () => "원하는 노선에 마차를 한 대 더 배치",
                () => true, () => TokCart += I(SupVal("cartExtra")));
            Def("gold", "gold", () => Nm("gold") + " · " + GoldAmt() + "골드",
                () => "즉시 지급 · 보유 " + FmtNum(Gold) + " → " + FmtNum(Gold + GoldAmt()) + "골드",
                () => GoldAmt() > 0, () => Gold += GoldAmt());
            Def("wall", "wall", () => Nm("wall"), () => "성벽 내구도 +" + FmtNum(B.WALL_REPAIR),
                () => Wall < WallMax, () => Wall = MathF.Min(WallMax, Wall + B.WALL_REPAIR));
            Def("cartSpd", "speed", () => Nm("cartSpd") + " · 속도 +" + I(B.CART_SPD_UP * 100f) + "%",
                () => "모든 마차에 적용 · 현재 " + I(100f * (1f + B.CART_SPD_UP * CartSpdLv)) + "% · 강화 " + CartSpdLv + "/" + FmtMax(SpdMax()),
                () => CartSpdLv < SpdMax(), () => CartSpdLv++, true);
            Def("cartCap", "cap", () => Nm("cartCap") + " · 적재 +" + I(B.CART_CAP_UP),
                () => "모든 마차에 적용 · 현재 " + CartCap() + "칸 · 강화 " + CartCapLv + "/" + FmtMax(CapMax()),
                () => CartCapLv < CapMax(), () =>
                {
                    CartCapLv++;
                    int up = I(B.CART_CAP_UP), uf = (int)Math.Ceiling(up / 2.0), uw = up - uf;
                    foreach (var l in Lines)
                    {
                        l.CartFoodSlots += uf;
                        foreach (var c in l.Carts) { c.SlotFood += uf; c.SlotWeapon += uw; }
                    }
                }, true);
            Def("whCap", "whcap", () => Nm("whCap") + " · 보관 +" + I(B.WH_CAP_STEP),
                () => "모든 창고에 적용 · 현재 " + WhCap() + "개 · 확장 " + WhCapLv + "/" + FmtMax(WhUpgMax()),
                () => WhCapLv < WhUpgMax() && HasWarehouse(), () =>
                {
                    WhCapLv++;
                    foreach (var n in Nodes)
                    {
                        if (n.Type != NodeType.Warehouse) continue;
                        n.SlotFood += (int)Math.Ceiling(I(B.WH_CAP_STEP) / 2.0);
                        n.SlotWeapon = WhCap() - n.SlotFood;
                    }
                }, true);
            // 한계 돌파: 해당 강화가 상한 근처(1회 남음 이하)일 때만 나옴
            Def("capStable", "capup", () => Nm("capStable"),
                () => "역참 마필 개량 · 수레 개조 최대 횟수 +" + FmtNum(SupVal("capStable")) + " (속도 " + FmtMax(SpdMax()) + " · 적재 " + FmtMax(CapMax()) + "회)",
                () => SupVal("capStable") > 0f && (CartSpdLv >= SpdMax() - 1f || CartCapLv >= CapMax() - 1f), () => CapStable++);
            Def("whUpg", "capup", () => Nm("whUpg"),
                () => "창고 확장 최대 횟수 " + FmtMax(WhUpgMax()) + "회 → " + FmtMax(WhUpgMax() + SupVal("whUpg")) + "회",
                () => SupVal("whUpg") > 0f && HasWarehouse() && WhCapLv >= WhUpgMax() - 1f, () => WhUpgBonus++);
            Def("capFort", "capup", () => Nm("capFort"),
                () => "요새 공격력 · 공격 속도 강화 최대 레벨 " + UpgMax() + " → " + (UpgMax() + I(SupVal("capFort"))),
                () =>
                {
                    if (SupVal("capFort") <= 0f) return false;
                    foreach (var n in Nodes) if (n.Type == NodeType.Fort && Math.Max(n.AtkLv, n.SpdLv) >= UpgMax() - 1) return true;
                    return false;
                }, () => CapFort++);
        }

        public string RewardTitle(string key) => rewards[key].Title();
        public string RewardDesc(string key) => rewards[key].Desc();
        public string RewardIcon(string key) => rewards[key].Icon;

        /// <summary>이번 정비에 나올 수 있는지 확인합니다: 가중치, 첫 등장 웨이브, 최대 선택 횟수, 보상 자체 조건 순입니다.</summary>
        bool Eligible(string k)
        {
            if (!Sup.TryGetValue(k, out var s) || !rewards.TryGetValue(k, out var r)) return false;
            if (!(s.Weight > 0f) || Wave < s.FirstWave) return false;
            picks.TryGetValue(k, out int n);
            if (!r.Capped && s.Max > 0 && n >= s.Max) return false;
            return r.Ok();
        }

        /// <summary>선택지 3개를 가중치 비율로 뽑습니다(중복 없음). 성벽이 70% 미만이면 성벽 보수를 먼저 넣습니다.</summary>
        List<string> RewardSet()
        {
            var pool = new List<string>();
            foreach (var k in rewardKeys) if (Eligible(k)) pool.Add(k);
            var set = new List<string>();
            if (pool.Contains("wall") && Wall < WallMax * .7f) set.Add("wall");
            while (set.Count < 3)
            {
                var cand = pool.FindAll(k => !set.Contains(k));
                if (cand.Count == 0) break;
                float tot = 0f;
                foreach (var k in cand) tot += Sup[k].Weight;
                float r = (float)rng.NextDouble() * tot;
                string chosen = cand[cand.Count - 1];
                foreach (var k in cand)
                {
                    r -= Sup[k].Weight;
                    if (r < 0f) { chosen = k; break; }
                }
                set.Add(chosen);
            }
            return set;
        }

        /// <summary>정비 단계 안내 문구입니다. 이번 정비 후 열릴 침투로와 개척지를 알려 줍니다.</summary>
        public string UpcomingNote()
        {
            var notes = new List<string>();
            if (Wave % I(B.ROUTE_EVERY) == 0 && Routes.Count < I(B.ROUTE_MAX)) notes.Add("새 침투로가 열립니다");
            if (Wave % EXPAND_EVERY == 0 && HW < HW_MAX) notes.Add("개척지가 넓어집니다");
            return notes.Count > 0
                ? "중앙이 허가한 권한 중 하나를 고르세요. 이번 정비 후 " + string.Join(", ", notes) + "."
                : "중앙이 허가한 권한 중 하나를 고르세요.";
        }

        /// <summary>정비 단계의 황실 전령 인용구입니다.</summary>
        public string RewardQuote() => LineTexts.MsgQuotes[(Wave - 1) % LineTexts.MsgQuotes.Length];

        void BeforeReward()
        {
            RewardChoices = RewardSet();
            Emit(new GameEvent { Type = GameEventType.RewardReady });
        }

        /// <summary>선택지를 적용하고 개척지 확장과 침투로 추가를 처리한 뒤 다음 대기 단계로 넘어갑니다.</summary>
        public void PickReward(string key)
        {
            if (Phase != Phase.Reward || key == null || !rewards.TryGetValue(key, out var r)) return;
            r.Apply();
            picks.TryGetValue(key, out int n);
            picks[key] = n + 1;
            var msgs = new List<string>();
            if (Wave % EXPAND_EVERY == 0 && HW < HW_MAX)
            {
                HW = MathF.Min(HW_MAX, HW + EXPAND_HW);
                D = MathF.Min(D_MAX, D + EXPAND_D);
                WallMax += B.WALL_UP;
                Wall += B.WALL_UP;
                msgs.Add("개척지 확장");
            }
            if (Wave % I(B.ROUTE_EVERY) == 0 && Routes.Count < I(B.ROUTE_MAX))
            {
                AddRoute(false);
                TokFort += 1;
                msgs.Add("새 침투로 발견");
                msgs.Add("요새 권한 +1");
            }
            if (msgs.Count > 0) Toast(string.Join(" · ", msgs));
            RewardChoices.Clear();
            Phase = Phase.Countdown;
            Count = B.BETWEEN;
        }
    }
}
