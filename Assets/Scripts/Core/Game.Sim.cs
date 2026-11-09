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
            if (Phase == Phase.Reward || Phase == Phase.Over)
            {
                return;
            }

            ElapsedTime += dt;
            foreach (var node in Nodes)
            {
                if (node.Type != NodeType.Farm && node.Type != NodeType.Forge)
                {
                    continue;
                }

                node.ProdTimer += dt;
                if (node.ProdTimer >= Balance.PROD_RATE)
                {
                    node.ProdTimer = 0f;
                    if (node.Stock < RoundToInt(Balance.PROD_CAP))
                    {
                        node.Stock++;
                    }
                    else
                    {
                        Bark("full", LineTexts.One("aide", "재고가 가득 찬 농장이나 대장간이 있습니다. 마차로 실어 내지 않으면 생산이 멈춥니다."));
                    }
                }
            }
            ProducerTimer -= dt;
            if (ProducerTimer <= 0f)
            {
                ProducerTimer = Balance.PRODUCER_EVERY;
                int farmCount = 0, forgeCount = 0;
                foreach (var node in Nodes)
                {
                    if (node.Type == NodeType.Farm)
                    {
                        farmCount++;
                    }
                    else if (node.Type == NodeType.Forge)
                    {
                        forgeCount++;
                    }
                }
                SpawnProducer(farmCount < forgeCount ? NodeType.Farm : forgeCount < farmCount ? NodeType.Forge : (_random.NextDouble() < .5 ? NodeType.Farm : NodeType.Forge));
            }
            foreach (var line in Lines)
            {
                foreach (var cart in line.Carts)
                {
                    StepCart(line, cart, dt);
                }
            }

            // 창고가 요청량이 있는데 30초 넘게 비어 있으면 안내함 (60초 간격)
            foreach (var node in Nodes)
            {
                if (node.Type != NodeType.Warehouse)
                {
                    continue;
                }

                bool empty = node.Food + node.Weapon == 0 && (WarehouseTarget(node, Res.Food) > 0 || WarehouseTarget(node, Res.Weapon) > 0) && LineCountWith(node.Id) > 0;
                node.EmptyTimer = empty ? node.EmptyTimer + dt : 0f;
                if (node.EmptyTimer > 30f)
                {
                    Bark("whempty", LineTexts.One("aide", "전방 창고가 비었습니다. 생산 노선이나 다른 창고와 이어 주십시오."), 60f);
                }
            }

            StepForts(dt);
            StepMonsters(dt);
            StepPhase(dt);

            Flash = MathF.Max(0f, Flash - dt);
            if (Wall < WallMax * .5f)
            {
                Bark("wall50", LineTexts.One("aide", "성벽이 절반 넘게 무너졌습니다!"));
            }

            if (Wall <= 0f)
            {
                Wall = 0f;
                Phase = Phase.Over;
                Emit(new GameEvent { Type = GameEventType.GameOver });
            }
        }

        private void StepForts(float dt)
        {
            foreach (var fort in Nodes)
            {
                if (fort.Type != NodeType.Fort)
                {
                    continue;
                }

                fort.Pulse = MathF.Max(0f, fort.Pulse - dt);
                // 사기: 식량을 1개 먹을 때마다 +MORALE_UP, 먹을 때가 됐는데 식량이 없으면 -MORALE_DOWN
                fort.FoodTimer += dt;
                if (fort.FoodTimer >= FortFoodEvery(fort))
                {
                    fort.FoodTimer = 0f;
                    if (fort.Food > 0)
                    {
                        fort.Food--;
                        fort.Morale = MathF.Min(1f, fort.Morale + Balance.MORALE_UP);
                    }
                    else
                    {
                        fort.Morale = MathF.Max(0f, fort.Morale - Balance.MORALE_DOWN);
                    }

                    fort.Morale = MathF.Round(fort.Morale * 1000f) / 1000f;   // 소수점 오차 제거
                }
                if (fort.Food <= 0)
                {
                    Bark("starve", LineTexts.One("aide", "요새 병사들이 굶고 있습니다! 사기가 떨어지면 화살에 힘이 빠집니다. 식량 마차를 보내 주십시오."), 35f);
                }
                else if (Phase == Phase.Wave && fort.Weapon <= 0 && fort.Ammo <= 0)
                {
                    Bark("dry", LineTexts.One("aide", "화살이 바닥났습니다! 대장간에서 무기를 날라야 합니다."), 35f);
                }

                fort.Cooldown -= dt;
                // 사기 0(또는 반올림 피해 0)이면 공격하지 않음
                if (fort.Cooldown <= 0f && fort.Morale > 0f && FortDamage(fort) > 0 && (fort.Ammo >= 1f || fort.Weapon > 0))
                {
                    Monster monster = null;
                    float bestDistance = Balance.RANGE;
                    foreach (var candidate in Monsters)
                    {
                        float distance = Vec2.Distance(fort.Position, candidate.Position);
                        if (distance < bestDistance && candidate.Alpha > .5f)
                        {
                            bestDistance = distance;
                            monster = candidate;
                        }
                    }
                    if (monster != null)
                    {
                        if (fort.Ammo < 1f)
                        {
                            fort.Weapon--;
                            fort.Ammo += FortShotsPerWeapon(fort);
                        }
                        fort.Ammo--;
                        fort.Cooldown = FortCooldown(fort);
                        monster.Hp -= FortDamage(fort);
                        monster.HitFlash = .12f;
                        Emit(new GameEvent { Type = GameEventType.Shot, From = fort.Position, To = monster.Position });
                    }
                }
            }
        }

        private void StepMonsters(float dt)
        {
            foreach (var monster in Monsters)
            {
                monster.Alpha = MathF.Min(1f, monster.Alpha + dt * 1.2f);
                monster.HitFlash = MathF.Max(0f, monster.HitFlash - dt);
                float dx = monster.Position.X, dy = monster.Position.Y - WallRadius, distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > WallRadius + 13f)
                {
                    monster.Position.X -= dx / distance * monster.Speed * dt;
                    monster.Position.Y -= dy / distance * monster.Speed * dt;
                    monster.BobPhase += dt * 8f;
                }
                else
                {
                    if (!CheatInvincible)
                    {
                        float damage = dt * Balance.MON_ATK;
                        Wall -= damage;
                        // 피해가 1 쌓일 때마다 표시용으로 알림
                        monster.WallAcc += damage;
                        if (monster.WallAcc >= 1f)
                        {
                            int wholeDamage = (int)monster.WallAcc;
                            monster.WallAcc -= wholeDamage;
                            Emit(new GameEvent { Type = GameEventType.WallHit, From = monster.Position, Amount = wholeDamage });
                        }
                    }
                    Flash = .25f;
                    monster.BobPhase += dt * 16f;
                }
            }
            for (int i = Monsters.Count - 1; i >= 0; i--)
            {
                var monster = Monsters[i];
                if (monster.Hp > 0f)
                {
                    continue;
                }

                Emit(new GameEvent { Type = GameEventType.Burst, From = monster.Position });
                // 골드는 표시 계층에서 동전이 HUD로 날아가 들어가게 함
                Emit(new GameEvent { Type = GameEventType.Kill, From = monster.Position, Amount = MathF.Max(0f, Balance.GOLD_PER_KILL) });
                Monsters.RemoveAt(i);
                Kills++;
                if (Balance.GOLD_PER_KILL > 0f)
                {
                    Gold += Balance.GOLD_PER_KILL;
                }

                if (Gold >= UpgradeCost(0) && AnyFortUpgradable())
                {
                    Bark("canUpg", LineTexts.One("aide", "전리품으로 골드가 모였습니다. 요새를 눌러 강화하실 수 있습니다. 다만 강한 요새는 더 많이 먹고, 빠른 요새는 화살을 더 씁니다."));
                }
            }
        }

        private bool AnyFortUpgradable()
        {
            int max = MaxUpgradeLevel();
            foreach (var node in Nodes)
            {
                if (node.Type == NodeType.Fort && (node.AttackLevel < max || node.SpeedLevel < max))
                {
                    return true;
                }
            }

            return false;
        }

        private void StepPhase(float dt)
        {
            if (Phase == Phase.Countdown)
            {
                Countdown -= dt;
                if (Countdown <= 0f)
                {
                    StartWave();
                }
            }
            else if (Phase == Phase.Wave)
            {
                _spawnTimer -= dt;
                if (_spawnQueue.Count > 0 && _spawnTimer <= 0f)
                {
                    SpawnMonster();
                }

                if (_spawnQueue.Count == 0 && Monsters.Count == 0)
                {
                    Phase = Phase.Reward;
                    BeforeReward();
                }
            }
        }

        private void StartWave()
        {
            Wave++;
            Phase = Phase.Wave;
            int total = RoundToInt(Balance.MON_BASE) + RoundToInt(Balance.MON_GROW * (Wave - 1));
            // 침투로별 위협도에 비례해 몬스터 수를 배분함 (새 길은 적게, 약하게)
            var weights = new float[Routes.Count];
            var counts = new int[Routes.Count];
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = RouteThreat(Routes[i], Wave);
                sum += weights[i];
            }
            for (int i = 0; i < weights.Length; i++)
            {
                counts[i] = Math.Max(1, RoundToInt(total * weights[i] / sum));
            }

            _spawnQueue.Clear();
            for (int i = 0, more = 1; more == 1; i++)
            {
                more = 0;
                for (int j = 0; j < weights.Length; j++)
                {
                    if (i < counts[j])
                    {
                        _spawnQueue.Enqueue((Routes[j].Angle, weights[j]));
                        more = 1;
                    }
                }
            }
            _spawnTimer = 0f;
            Emit(new GameEvent { Type = GameEventType.WaveStart, Key = Wave.ToString(CultureInfo.InvariantCulture) });
            if (Wave == 1)
            {
                Bark("firstWave", LineTexts.FirstWave);
            }
        }

        private void SpawnMonster()
        {
            _spawnTimer = MathF.Max(Balance.SPAWN_GAP_MIN, Balance.SPAWN_GAP - Routes.Count * Balance.SPAWN_GAP_STEP);
            var entry = _spawnQueue.Dequeue();
            float angle = entry.Angle + RandomRange(-.05f, .05f), radius = WallRadius + DenDistance + RandomRange(-5f, 15f);
            int health = Math.Max(1, RoundToInt((Balance.HP_BASE + MathF.Floor(Balance.HP_GROW * (Wave - 1))) * (Balance.THREAT_HP_A + Balance.THREAT_HP_B * entry.Threat)));
            Monsters.Add(new Monster
            {
                Position = new Vec2(MathF.Cos(angle) * radius, WallRadius + MathF.Sin(angle) * radius),
                Hp = health,
                MaxHp = health,
                Speed = (Balance.SPD_BASE + Wave * Balance.SPD_GROW + RandomRange(0f, Balance.MON_SPD_RND)) * (Balance.THREAT_SPD_A + Balance.THREAT_SPD_B * entry.Threat),
                Size = .75f + .25f * entry.Threat,
                Alpha = 0f,
                HitFlash = 0f,
                BobPhase = (float)_random.NextDouble() * 6f,
            });
        }

        // ---------- 치트 ----------

        public void CheatAddGold(float amount)
        {
            Gold += amount;
        }

        public void CheatAddTokens()
        {
            StableTokens = Math.Min(LineColorCount - Lines.Count, StableTokens + 1);
            FortTokens++;
            WarehouseTokens++;
            CartTokens++;
        }

        public void CheatHealWall()
        {
            Wall = WallMax;
        }

        public void CheatSkipCountdown()
        {
            if (Phase == Phase.Countdown)
            {
                Countdown = 0f;
            }
        }

        public void CheatKillAll()
        {
            foreach (var monster in Monsters)
            {
                monster.Hp = 0f;
            }
        }

        /// <summary>남은 몬스터와 대기열을 모두 없애 이번 웨이브를 끝냅니다.</summary>
        public void CheatWinWave()
        {
            _spawnQueue.Clear();
            CheatKillAll();
        }

        public void CheatSetWave(int waveNumber)
        {
            Wave = Math.Max(0, waveNumber);
        }
    }
}
