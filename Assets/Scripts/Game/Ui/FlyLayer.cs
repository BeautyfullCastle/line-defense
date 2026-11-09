using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>아이콘이 날아가 닿는 HUD·꼬리말 칸입니다.</summary>
    public enum FlyTarget { Gold, Wall, Stable, Fort, Warehouse, Info }

    /// <summary>
    /// 얻은 것이 해당 칸으로 날아 들어가고, 닿는 순간 칸이 튀며 +N이 떠오르게 하는 UI 연출층입니다.
    /// 날아가는 동안의 양은 Pending으로 잡아 두어, 칸의 숫자가 도착할 때 올라가게 합니다.
    /// 입력을 막지 않으며 실제 시간으로 움직입니다.
    /// </summary>
    public sealed class FlyLayer
    {
        const int MaxItems = 60;
        const int MaxPops = 24;
        const float Dur = 0.7f;
        const float PopDur = 0.9f;
        const float PunchDur = 0.28f;

        sealed class Item
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 From, Ctrl;
            public FlyTarget Target;
            public float T, Delay, Size;
            public int Amount;
            public string Label;
            public Color Col;
        }

        sealed class Pop
        {
            public RectTransform Rt;
            public Text Txt;
            public Vector2 P;
            public float T, Dir;
        }

        readonly RectTransform _root;
        readonly Func<FlyTarget, RectTransform> _resolve;
        readonly List<Item> _items = new List<Item>();
        readonly List<Pop> _pops = new List<Pop>();
        readonly int[] _pending = new int[6];
        readonly float[] _punch = new float[6];
        readonly Vector3[] _baseScale = new Vector3[6];

        /// <summary>연출층을 만듭니다. resolve는 칸 종류를 실제 RectTransform으로 바꿉니다.</summary>
        public FlyLayer(Transform parent, Func<FlyTarget, RectTransform> resolve)
        {
            _resolve = resolve;
            _root = UiFactory.Rect(parent, "FlyLayer");
            UiFactory.Stretch(_root);
            var cg = _root.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            for (int i = 0; i < _baseScale.Length; i++) _baseScale[i] = Vector3.one;
        }

        /// <summary>아직 날아가는 중이라 칸에 반영하지 않은 양입니다.</summary>
        public int Pending(FlyTarget t) => _pending[(int)t];

        /// <summary>
        /// 화면 좌표 fromScreen에서 target 칸으로 아이콘 하나를 날립니다.
        /// amount는 도착 때 숫자에 더해질 양이며, label이 있으면 +N 대신 그 글자를 띄웁니다.
        /// </summary>
        public void Fly(Vector2 fromScreen, FlyTarget target, string icon, Color col, int amount, string label, float delay, float size = 22f)
        {
            if (_items.Count >= MaxItems)
            {
                // 너무 많으면 날리지 않고 바로 칸에 반영
                Arrive(target, amount, label, col);
                return;
            }
            var rt = UiFactory.Rect(_root, "Fly");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = IconFactory.Get(icon);
            img.color = col;
            img.raycastTarget = false;
            var from = ToLocal(fromScreen);
            var to = TargetLocal(target);
            // 진행 방향 옆으로 휘었다 들어가도록 제어점을 옆으로 밀어 둠
            var mid = (from + to) * 0.5f;
            var dir = to - from;
            var side = new Vector2(-dir.y, dir.x).normalized * Mathf.Min(140f, dir.magnitude * 0.35f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            var item = new Item
            {
                Rt = rt, Img = img, From = from, Ctrl = mid + side + new Vector2(0f, 60f), Target = target,
                T = 0f, Delay = delay, Size = size, Amount = amount, Label = label, Col = col,
            };
            _pending[(int)target] += amount;
            _items.Add(item);
            Place(item);
        }

        /// <summary>날지 않고 칸만 튀게 하고 글자를 띄웁니다. 성벽 피해처럼 바로 반영되는 변화에 씁니다.</summary>
        public void Punch(FlyTarget target, string label, Color col)
        {
            _punch[(int)target] = PunchDur;
            if (!string.IsNullOrEmpty(label)) SpawnPop(target, label, col);
        }

        /// <summary>모든 연출을 지웁니다.</summary>
        public void Clear()
        {
            foreach (var it in _items) UnityEngine.Object.Destroy(it.Rt.gameObject);
            foreach (var p in _pops) UnityEngine.Object.Destroy(p.Rt.gameObject);
            _items.Clear();
            _pops.Clear();
            Array.Clear(_pending, 0, _pending.Length);
            for (int i = 0; i < _punch.Length; i++)
            {
                _punch[i] = 0f;
                var rt = _resolve((FlyTarget)i);
                if (rt != null) rt.localScale = _baseScale[i];
            }
        }

        /// <summary>실제 시간 dt만큼 진행합니다.</summary>
        public void Tick(float dt)
        {
            _root.SetAsLastSibling();
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                if (it.Delay > 0f) { it.Delay -= dt; Place(it); continue; }
                it.T += dt / Dur;
                if (it.T >= 1f)
                {
                    _pending[(int)it.Target] -= it.Amount;
                    Arrive(it.Target, it.Amount, it.Label, it.Col);
                    UnityEngine.Object.Destroy(it.Rt.gameObject);
                    _items.RemoveAt(i);
                    continue;
                }
                Place(it);
            }

            for (int i = 0; i < _punch.Length; i++)
            {
                var rt = _resolve((FlyTarget)i);
                bool active = _punch[i] > 0f;
                _punch[i] = Mathf.Max(0f, _punch[i] - dt);
                if (rt == null) continue;
                if (!active) { if (rt.localScale != _baseScale[i]) rt.localScale = _baseScale[i]; continue; }
                float k = _punch[i] / PunchDur, amp = (FlyTarget)i == FlyTarget.Info ? 0.1f : 0.35f;
                rt.localScale = _baseScale[i] * (1f + amp * Mathf.Sin(k * Mathf.PI));
            }

            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.T += dt / PopDur;
                if (p.T >= 1f)
                {
                    UnityEngine.Object.Destroy(p.Rt.gameObject);
                    _pops.RemoveAt(i);
                    continue;
                }
                p.Rt.anchoredPosition = p.P + new Vector2(0f, 26f * p.Dir * Mathf.Sqrt(p.T));
                var c = p.Txt.color;
                c.a = p.T < 0.6f ? 1f : 1f - (p.T - 0.6f) / 0.4f;
                p.Txt.color = c;
            }
        }

        void Arrive(FlyTarget target, int amount, string label, Color col)
        {
            _punch[(int)target] = PunchDur;
            // label이 빈 문자열이면 글자 없이 칸만 튐. null이면 양을 +N으로 띄움
            if (label != null) { if (label.Length > 0) SpawnPop(target, label, col); }
            else if (amount != 0) SpawnPop(target, (amount > 0 ? "+" : "") + amount, col);
        }

        void SpawnPop(FlyTarget target, string text, Color col)
        {
            // 처치가 몰릴 때 글자 오브젝트가 끝없이 늘지 않게 막음. 칸은 계속 튐
            if (_pops.Count >= MaxPops) return;
            var rt = UiFactory.Rect(_root, "Pop");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(160f, 24f);
            var t = UiFactory.Label(rt, text, 16, col, true, TextAnchor.MiddleCenter);
            UiFactory.Stretch(t.rectTransform);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.WithAlpha(Palette.Panel, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            // 화면 위쪽 칸(머리말)은 아래로, 아래쪽 칸(꼬리말)은 위로 글자를 띄워 화면 밖으로 나가지 않게 함
            var tl = TargetLocal(target);
            float dir = tl.y > 0f ? -1f : 1f;
            var p = tl + new Vector2(0f, 18f * dir);
            rt.anchoredPosition = p;
            _pops.Add(new Pop { Rt = rt, Txt = t, P = p, T = 0f, Dir = dir });
        }

        void Place(Item it)
        {
            if (it.Delay > 0f)
            {
                it.Rt.anchoredPosition = it.From;
                it.Rt.localScale = Vector3.zero;
                return;
            }
            float k = it.T < 0.5f ? 2f * it.T * it.T : 1f - 2f * (1f - it.T) * (1f - it.T);
            var to = TargetLocal(it.Target);
            float u = 1f - k;
            it.Rt.anchoredPosition = it.From * (u * u) + it.Ctrl * (2f * u * k) + to * (k * k);
            float s = it.T < 0.15f ? Mathf.Lerp(0f, 1.25f, it.T / 0.15f) : Mathf.Lerp(1.25f, 0.7f, (it.T - 0.15f) / 0.85f);
            it.Rt.localScale = new Vector3(s, s, 1f);
        }

        Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            return local;
        }

        /// <summary>칸 가운데의 화면 좌표입니다. 칸을 못 찾으면 화면 가운데를 돌려줍니다.</summary>
        public Vector2 TargetScreen(FlyTarget t)
        {
            var rt = _resolve(t);
            if (rt == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return rt.TransformPoint(rt.rect.center);
        }

        Vector2 TargetLocal(FlyTarget t) => ToLocal(TargetScreen(t));
    }
}
