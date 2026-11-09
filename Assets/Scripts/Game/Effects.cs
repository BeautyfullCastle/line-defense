using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>사격선, 파편, 떠오르는 글자 같은 표현 전용 효과를 관리합니다.</summary>
    public sealed class Effects
    {
        private const int MaxShots = 200, MaxParts = 800, MaxFloats = 40;

        /// <summary>요새에서 목표로 향하는 짧은 사격선입니다.</summary>
        public struct Shot
        {
            public Vector2 A, B; public float Life;
        }

        /// <summary>파편 하나입니다.</summary>
        public struct Part
        {
            public Vector2 P, V; public float Life; public Color Col;
        }

        /// <summary>떠오르는 글자입니다.</summary>
        public struct FloatText
        {
            public Vector2 P; public float Life; public string Text; public Color Col; public float Size;
        }

        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<Part> Parts = new List<Part>();
        public readonly List<FloatText> Floats = new List<FloatText>();

        /// <summary>성벽이 피해를 받은 뒤 성벽 전체가 붉게 물드는 남은 시간입니다.</summary>
        public float WallGlow;

        /// <summary>모든 효과를 지웁니다.</summary>
        public void Clear()
        {
            Shots.Clear();
            Parts.Clear();
            Floats.Clear();
            WallGlow = 0f;
        }

        /// <summary>시뮬레이션 이벤트 중 효과에 해당하는 것을 반영합니다.</summary>
        public void Add(GameEvent gameEvent)
        {
            switch (gameEvent.Type)
            {
                case GameEventType.Shot:
                    if (Shots.Count < MaxShots)
                    {
                        Shots.Add(new Shot { A = gameEvent.From.ToVector2(), B = gameEvent.To.ToVector2(), Life = 0.18f });
                    }

                    break;
                case GameEventType.Burst:
                    Burst(gameEvent.From.ToVector2(), 8);
                    break;
                case GameEventType.Float:
                    Floats.Add(new FloatText { P = gameEvent.From.ToVector2(), Life = 1f, Text = gameEvent.Text ?? string.Empty, Col = Palette.Food });
                    break;
                case GameEventType.WallHit:
                    {
                        // 성벽 안쪽(성 중심 방향)으로 살짝 들여 빨간 -N과 붉은 파편을 띄움
                        var hitPosition = gameEvent.From.ToVector2();
                        var inward = (new Vector2(0f, Game.WallRadius) - hitPosition).normalized;
                        var textPosition = hitPosition + inward * 40f + new Vector2(Random.Range(-10f, 10f), 0f);
                        if (Floats.Count < MaxFloats)
                        {
                            Floats.Add(new FloatText { P = textPosition, Life = 0.9f, Text = "-" + Mathf.RoundToInt(gameEvent.Amount), Col = Palette.Danger, Size = 20f });
                        }

                        for (int i = 0; i < 5 && Parts.Count < MaxParts; i++)
                        {
                            float angle = Random.value * Mathf.PI * 2f, speed = Random.Range(20f, 60f);
                            Parts.Add(new Part { P = hitPosition + inward * 10f, V = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, Life = Random.Range(0.25f, 0.45f), Col = Palette.Danger });
                        }
                        WallGlow = 0.45f;
                        break;
                    }
            }
        }

        private void Burst(Vector2 position, int count)
        {
            for (int i = 0; i < count && Parts.Count < MaxParts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f, speed = Random.Range(30f, 90f);
                Parts.Add(new Part { P = position, V = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, Life = Random.Range(0.3f, 0.6f), Col = Palette.Monster });
            }
        }

        /// <summary>시뮬레이션 시간 dt만큼 효과를 진행시킵니다.</summary>
        public void Update(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            WallGlow = Mathf.Max(0f, WallGlow - dt);
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var shot = Shots[i];
                shot.Life -= dt;
                if (shot.Life <= 0f)
                {
                    Shots.RemoveAt(i);
                }
                else
                {
                    Shots[i] = shot;
                }
            }
            float damp = Mathf.Pow(0.92f, dt * 60f);
            for (int i = Parts.Count - 1; i >= 0; i--)
            {
                var particle = Parts[i];
                particle.Life -= dt;
                particle.P += particle.V * dt;
                particle.V *= damp;
                if (particle.Life <= 0f)
                {
                    Parts.RemoveAt(i);
                }
                else
                {
                    Parts[i] = particle;
                }
            }
            for (int i = Floats.Count - 1; i >= 0; i--)
            {
                var floatText = Floats[i];
                floatText.Life -= dt;
                floatText.P.y -= dt * 18f;
                if (floatText.Life <= 0f)
                {
                    Floats.RemoveAt(i);
                }
                else
                {
                    Floats[i] = floatText;
                }
            }
        }
    }
}
