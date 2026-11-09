using System;
using System.Globalization;

namespace LineDefense.Core
{
    // 시뮬레이션 진행과 치트
    public sealed partial class Game
    {
        // ---------- 시뮬레이션 ----------

        /// <summary>dt초만큼 진행합니다. 정비 단계와 종료 상태에서는 아무것도 하지 않습니다.</summary>
        public void Step(float dt)
        {
            if (Phase == Phase.Reward || Phase == Phase.Over) return;
            T += dt;
            foreach (var n in Nodes)
            {
                if (n.Type != NodeType.Farm && n.Type != NodeType.Forge) continue;
                n.ProdTimer += dt;
                if (n.ProdTimer >= B.PROD_RATE)
                {
                    n.ProdTimer = 0f;
                    if (n.Stock < I(B.PROD_CAP)) n.Stock++;
                    else Bark("full", LineTexts.One("aide", "재고가 가득 찬 농장이나 대장간이 있습니다. 마차로 실어 내지 않으면 생산이 멈춥니다."));
                }
            }
            ProdT -= dt;
            if (ProdT <= 0f)
            {
                ProdT = B.PRODUCER_EVERY;
                int f = 0, w = 0;
                foreach (var n in Nodes) { if (n.Type == NodeType.Farm) f++; else if (n.Type == NodeType.Forge) w++; }
                SpawnProducer(f < w ? NodeType.Farm : w < f ? NodeType.Forge : (rng.NextDouble() < .5 ? NodeType.Farm : NodeType.Forge));
            }
            foreach (var l in Lines) foreach (var c in l.Carts) StepCart(l, c, dt);

            // 창고가 요청량이 있는데 30초 넘게 비어 있으면 안내함 (60초 간격)
            foreach (var n in Nodes)
            {
                if (n.Type != NodeType.Warehouse) continue;
                bool empty = n.Food + n.Weapon == 0 && (WarehouseTarget(n, Res.Food) > 0 || WarehouseTarget(n, Res.Weapon) > 0) && LineCountWith(n.Id) > 0;
                n.EmptyTimer = empty ? n.EmptyTimer + dt : 0f;
                if (n.EmptyTimer > 30f) Bark("whempty", LineTexts.One("aide", "전방 창고가 비었습니다. 생산 노선이나 다른 창고와 이어 주십시오."), 60f);
            }

            StepForts(dt);
            StepMonsters(dt);
            StepPhase(dt);

            Flash = MathF.Max(0f, Flash - dt);
            if (Wall < WallMax * .5f) Bark("wall50", LineTexts.One("aide", "성벽이 절반 넘게 무너졌습니다!"));
            if (Wall <= 0f)
            {
                Wall = 0f;
                Phase = Phase.Over;
                Emit(new GameEvent { Type = GameEventType.GameOver });
            }
        }

        void StepForts(float dt)
        {
            foreach (var f in Nodes)
            {
                if (f.Type != NodeType.Fort) continue;
                f.Pulse = MathF.Max(0f, f.Pulse - dt);
                // 사기: 식량을 1개 먹을 때마다 +MORALE_UP, 먹을 때가 됐는데 식량이 없으면 -MORALE_DOWN
                f.FoodTimer += dt;
                if (f.FoodTimer >= FortFoodEvery(f))
                {
                    f.FoodTimer = 0f;
                    if (f.Food > 0) { f.Food--; f.Morale = MathF.Min(1f, f.Morale + B.MORALE_UP); }
                    else f.Morale = MathF.Max(0f, f.Morale - B.MORALE_DOWN);
                    f.Morale = MathF.Round(f.Morale * 1000f) / 1000f;   // 소수점 오차 제거
                }
                if (f.Food <= 0) Bark("starve", LineTexts.One("aide", "요새 병사들이 굶고 있습니다! 사기가 떨어지면 화살에 힘이 빠집니다. 식량 마차를 보내 주십시오."), 35f);
                else if (Phase == Phase.Wave && f.Weapon <= 0 && f.Ammo <= 0) Bark("dry", LineTexts.One("aide", "화살이 바닥났습니다! 대장간에서 무기를 날라야 합니다."), 35f);
                f.Cooldown -= dt;
                // 사기 0(또는 반올림 피해 0)이면 공격하지 않음
                if (f.Cooldown <= 0f && f.Morale > 0f && FortDmg(f) > 0 && (f.Ammo >= 1f || f.Weapon > 0))
                {
                    Monster tgt = null;
                    float bd = B.RANGE;
                    foreach (var m in Mons)
                    {
                        float d = Vec2.Dist(f.Pos, m.Pos);
                        if (d < bd && m.Alpha > .5f) { bd = d; tgt = m; }
                    }
                    if (tgt != null)
                    {
                        if (f.Ammo < 1f) { f.Weapon--; f.Ammo += FortShotsPerWeapon(f); }
                        f.Ammo--;
                        f.Cooldown = FortCd(f);
                        tgt.Hp -= FortDmg(f);
                        tgt.Hit = .12f;
                        Emit(new GameEvent { Type = GameEventType.Shot, A = f.Pos, B = tgt.Pos });
                    }
                }
            }
        }

        void StepMonsters(float dt)
        {
            foreach (var m in Mons)
            {
                m.Alpha = MathF.Min(1f, m.Alpha + dt * 1.2f);
                m.Hit = MathF.Max(0f, m.Hit - dt);
                float dx = m.Pos.x, dy = m.Pos.y - RW, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > RW + 13f)
                {
                    m.Pos.x -= dx / d * m.Speed * dt;
                    m.Pos.y -= dy / d * m.Speed * dt;
                    m.Bob += dt * 8f;
                }
                else
                {
                    if (!CheatInvincible)
                    {
                        float dmg = dt * B.MON_ATK;
                        Wall -= dmg;
                        // 피해가 1 쌓일 때마다 표시용으로 알림
                        m.WallAcc += dmg;
                        if (m.WallAcc >= 1f)
                        {
                            int n = (int)m.WallAcc;
                            m.WallAcc -= n;
                            Emit(new GameEvent { Type = GameEventType.WallHit, A = m.Pos, Amount = n });
                        }
                    }
                    Flash = .25f;
                    m.Bob += dt * 16f;
                }
            }
            for (int i = Mons.Count - 1; i >= 0; i--)
            {
                var m = Mons[i];
                if (m.Hp > 0f) continue;
                Emit(new GameEvent { Type = GameEventType.Burst, A = m.Pos });
                // 골드는 표시 계층에서 동전이 HUD로 날아가 들어가게 함
                Emit(new GameEvent { Type = GameEventType.Kill, A = m.Pos, Amount = MathF.Max(0f, B.GOLD_PER_KILL) });
                Mons.RemoveAt(i);
                Kills++;
                if (B.GOLD_PER_KILL > 0f) Gold += B.GOLD_PER_KILL;
                if (Gold >= UpgCost(0) && AnyFortUpgradable())
                    Bark("canUpg", LineTexts.One("aide", "전리품으로 골드가 모였습니다. 요새를 눌러 강화하실 수 있습니다. 다만 강한 요새는 더 많이 먹고, 빠른 요새는 화살을 더 씁니다."));
            }
        }

        bool AnyFortUpgradable()
        {
            int max = UpgMax();
            foreach (var n in Nodes) if (n.Type == NodeType.Fort && (n.AtkLv < max || n.SpdLv < max)) return true;
            return false;
        }

        void StepPhase(float dt)
        {
            if (Phase == Phase.Countdown)
            {
                Count -= dt;
                if (Count <= 0f) StartWave();
            }
            else if (Phase == Phase.Wave)
            {
                spawnT -= dt;
                if (queue.Count > 0 && spawnT <= 0f) SpawnMonster();
                if (queue.Count == 0 && Mons.Count == 0)
                {
                    Phase = Phase.Reward;
                    BeforeReward();
                }
            }
        }

        void StartWave()
        {
            Wave++;
            Phase = Phase.Wave;
            int total = I(B.MON_BASE) + I(B.MON_GROW * (Wave - 1));
            // 침투로별 위협도에 비례해 몬스터 수를 배분함 (새 길은 적게, 약하게)
            var ws = new float[Routes.Count];
            var counts = new int[Routes.Count];
            float sum = 0f;
            for (int i = 0; i < ws.Length; i++) { ws[i] = RouteThreat(Routes[i], Wave); sum += ws[i]; }
            for (int i = 0; i < ws.Length; i++) counts[i] = Math.Max(1, I(total * ws[i] / sum));
            queue.Clear();
            for (int i = 0, more = 1; more == 1; i++)
            {
                more = 0;
                for (int j = 0; j < ws.Length; j++)
                    if (i < counts[j]) { queue.Enqueue((Routes[j].Angle, ws[j])); more = 1; }
            }
            spawnT = 0f;
            Emit(new GameEvent { Type = GameEventType.WaveStart, Key = Wave.ToString(CultureInfo.InvariantCulture) });
            if (Wave == 1) Bark("firstWave", LineTexts.FirstWave);
        }

        void SpawnMonster()
        {
            spawnT = MathF.Max(B.SPAWN_GAP_MIN, B.SPAWN_GAP - Routes.Count * B.SPAWN_GAP_STEP);
            var q = queue.Dequeue();
            float a = q.a + Rnd(-.05f, .05f), r = RW + DEN + Rnd(-5f, 15f);
            int hp = Math.Max(1, I((B.HP_BASE + MathF.Floor(B.HP_GROW * (Wave - 1))) *(B.THREAT_HP_A + B.THREAT_HP_B * q.s)));
            Mons.Add(new Monster
            {
                Pos = new Vec2(MathF.Cos(a) * r, RW + MathF.Sin(a) * r),
                Hp = hp, MaxHp = hp,
                Speed = (B.SPD_BASE + Wave * B.SPD_GROW + Rnd(0f, B.MON_SPD_RND)) * (B.THREAT_SPD_A + B.THREAT_SPD_B * q.s),
                Size = .75f + .25f * q.s,
                Alpha = 0f, Hit = 0f, Bob = (float)rng.NextDouble() * 6f,
            });
        }

        // ---------- 치트 ----------

        public void CheatAddGold(float g) { Gold += g; }

        public void CheatAddTokens()
        {
            TokStable = Math.Min(LineColorCount - Lines.Count, TokStable + 1);
            TokFort++; TokWarehouse++; TokCart++;
        }

        public void CheatHealWall() { Wall = WallMax; }

        public void CheatSkipCountdown()
        {
            if (Phase == Phase.Countdown) Count = 0f;
        }

        public void CheatKillAll()
        {
            foreach (var m in Mons) m.Hp = 0f;
        }

        /// <summary>남은 몬스터와 대기열을 모두 없애 이번 웨이브를 끝냅니다.</summary>
        public void CheatWinWave()
        {
            queue.Clear();
            CheatKillAll();
        }

        public void CheatSetWave(int w)
        {
            Wave = Math.Max(0, w);
        }
    }
}
