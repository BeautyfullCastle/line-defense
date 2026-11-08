using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>사격선, 파편, 떠오르는 글자 같은 표현 전용 효과를 관리합니다.</summary>
    public sealed class Effects
    {
        const int MaxShots = 200, MaxParts = 800;

        /// <summary>요새에서 목표로 향하는 짧은 사격선입니다.</summary>
        public struct Shot { public Vector2 A, B; public float Life; }

        /// <summary>파편 하나입니다.</summary>
        public struct Part { public Vector2 P, V; public float Life; public Color Col; }

        /// <summary>떠오르는 글자입니다.</summary>
        public struct FloatText { public Vector2 P; public float Life; public string Text; }

        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<Part> Parts = new List<Part>();
        public readonly List<FloatText> Floats = new List<FloatText>();

        /// <summary>모든 효과를 지웁니다.</summary>
        public void Clear()
        {
            Shots.Clear();
            Parts.Clear();
            Floats.Clear();
        }

        /// <summary>시뮬레이션 이벤트 중 효과에 해당하는 것을 반영합니다.</summary>
        public void Add(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.Shot:
                    if (Shots.Count < MaxShots) Shots.Add(new Shot { A = e.A.ToV(), B = e.B.ToV(), Life = 0.18f });
                    break;
                case GameEventType.Burst:
                    Burst(e.A.ToV(), 8);
                    break;
                case GameEventType.Float:
                    Floats.Add(new FloatText { P = e.A.ToV(), Life = 1f, Text = e.Text ?? string.Empty });
                    break;
            }
        }

        void Burst(Vector2 p, int n)
        {
            for (int i = 0; i < n && Parts.Count < MaxParts; i++)
            {
                float a = Random.value * Mathf.PI * 2f, s = Random.Range(30f, 90f);
                Parts.Add(new Part { P = p, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, Life = Random.Range(0.3f, 0.6f), Col = Palette.Monster });
            }
        }

        /// <summary>시뮬레이션 시간 dt만큼 효과를 진행시킵니다.</summary>
        public void Update(float dt)
        {
            if (dt <= 0f) return;
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                s.Life -= dt;
                if (s.Life <= 0f) Shots.RemoveAt(i); else Shots[i] = s;
            }
            float damp = Mathf.Pow(0.92f, dt * 60f);
            for (int i = Parts.Count - 1; i >= 0; i--)
            {
                var p = Parts[i];
                p.Life -= dt;
                p.P += p.V * dt;
                p.V *= damp;
                if (p.Life <= 0f) Parts.RemoveAt(i); else Parts[i] = p;
            }
            for (int i = Floats.Count - 1; i >= 0; i--)
            {
                var f = Floats[i];
                f.Life -= dt;
                f.P.y -= dt * 18f;
                if (f.Life <= 0f) Floats.RemoveAt(i); else Floats[i] = f;
            }
        }
    }
}
