using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>아이콘이 날아가 닿는 HUD·꼬리말 칸입니다.</summary>
    public enum FlyTarget
    {
        Gold, Wall, Stable, Fort, Warehouse, Info
    }

    /// <summary>
    /// 얻은 것이 해당 칸으로 날아 들어가고, 닿는 순간 칸이 튀며 +N이 떠오르게 하는 UI 연출층입니다.
    /// 날아가는 동안의 양은 Pending으로 잡아 두어, 칸의 숫자가 도착할 때 올라가게 합니다.
    /// 입력을 막지 않으며 실제 시간으로 움직입니다. 프리팹 Prefabs/UI/FlyLayer에 붙습니다.
    /// </summary>
    public sealed class FlyLayer : MonoBehaviour
    {
        private const int MaxItems = 60;
        private const int MaxPops = 24;
        private const float FlyDuration = 0.7f;
        private const float PopDuration = 0.9f;
        private const float PunchDuration = 0.28f;

        private sealed class Item
        {
            public RectTransform RectTransform;
            public Image Image;
            public Vector2 From, ControlPoint;
            public FlyTarget Target;
            public float Elapsed, Delay;
            public int Amount;
            public string Label;
            public Color Color;
        }

        private sealed class Pop
        {
            public RectTransform RectTransform;
            public Text Label;
            public Vector2 Position;
            public float Elapsed, Direction;
        }

        [SerializeField] private Image _iconPrefab;
        [SerializeField] private Text _popPrefab;
        [SerializeField] private IconSet _icons;

        private Func<FlyTarget, RectTransform> _resolve;
        private readonly List<Item> _items = new List<Item>();
        private readonly List<Pop> _pops = new List<Pop>();
        private readonly Stack<Image> _iconPool = new Stack<Image>();
        private readonly Stack<Text> _popPool = new Stack<Text>();
        private readonly int[] _pending = new int[6];
        private readonly float[] _punch = new float[6];
        private readonly Vector3[] _baseScale = new Vector3[6];

        private RectTransform Root => (RectTransform)transform;

        /// <summary>사용 전에 한 번 부릅니다. resolve는 칸 종류를 실제 RectTransform으로 바꿉니다.</summary>
        public void Init(Func<FlyTarget, RectTransform> resolve)
        {
            _resolve = resolve;
            for (int i = 0; i < _baseScale.Length; i++)
            {
                _baseScale[i] = Vector3.one;
            }
        }

        /// <summary>아직 날아가는 중이라 칸에 반영하지 않은 양입니다.</summary>
        public int Pending(FlyTarget target) => _pending[(int)target];

        /// <summary>
        /// 화면 좌표 fromScreen에서 target 칸으로 아이콘 하나를 날립니다.
        /// amount는 도착 때 숫자에 더해질 양이며, label이 있으면 +N 대신 그 글자를 띄웁니다.
        /// </summary>
        public void Fly(Vector2 fromScreen, FlyTarget target, string icon, Color color, int amount, string label, float delay, float size = 22f)
        {
            if (_items.Count >= MaxItems)
            {
                // 너무 많으면 날리지 않고 바로 칸에 반영
                Arrive(target, amount, label, color);
                return;
            }
            var image = _iconPool.Count > 0 ? _iconPool.Pop() : Instantiate(_iconPrefab, transform, false);
            var rectTransform = image.rectTransform;
            rectTransform.sizeDelta = new Vector2(size, size);
            image.sprite = _icons.Get(icon);
            image.color = color;
            image.gameObject.SetActive(true);
            var from = ToLocal(fromScreen);
            var targetPosition = TargetLocal(target);
            // 진행 방향 옆으로 휘었다 들어가도록 제어점을 옆으로 밀어 둠
            var midpoint = (from + targetPosition) * 0.5f;
            var direction = targetPosition - from;
            var side = new Vector2(-direction.y, direction.x).normalized * Mathf.Min(140f, direction.magnitude * 0.35f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            var item = new Item
            {
                RectTransform = rectTransform,
                Image = image,
                From = from,
                ControlPoint = midpoint + side + new Vector2(0f, 60f),
                Target = target,
                Elapsed = 0f,
                Delay = delay,
                Amount = amount,
                Label = label,
                Color = color,
            };
            _pending[(int)target] += amount;
            _items.Add(item);
            Place(item);
        }

        /// <summary>날지 않고 칸만 튀게 하고 글자를 띄웁니다. 성벽 피해처럼 바로 반영되는 변화에 씁니다.</summary>
        public void Punch(FlyTarget target, string label, Color color)
        {
            _punch[(int)target] = PunchDuration;
            if (!string.IsNullOrEmpty(label))
            {
                SpawnPop(target, label, color);
            }
        }

        /// <summary>모든 연출을 지웁니다.</summary>
        public void Clear()
        {
            foreach (var item in _items)
            {
                ReleaseIcon(item);
            }

            foreach (var pop in _pops)
            {
                ReleasePop(pop);
            }

            _items.Clear();
            _pops.Clear();
            Array.Clear(_pending, 0, _pending.Length);
            for (int i = 0; i < _punch.Length; i++)
            {
                _punch[i] = 0f;
                var rectTransform = _resolve((FlyTarget)i);
                if (rectTransform != null)
                {
                    rectTransform.localScale = _baseScale[i];
                }
            }
        }

        /// <summary>실제 시간 dt만큼 진행합니다.</summary>
        public void Tick(float dt)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                if (item.Delay > 0f)
                {
                    item.Delay -= dt;
                    Place(item);
                    continue;
                }
                item.Elapsed += dt / FlyDuration;
                if (item.Elapsed >= 1f)
                {
                    _pending[(int)item.Target] -= item.Amount;
                    Arrive(item.Target, item.Amount, item.Label, item.Color);
                    ReleaseIcon(item);
                    _items.RemoveAt(i);
                    continue;
                }
                Place(item);
            }

            for (int i = 0; i < _punch.Length; i++)
            {
                var rectTransform = _resolve((FlyTarget)i);
                bool active = _punch[i] > 0f;
                _punch[i] = Mathf.Max(0f, _punch[i] - dt);
                if (rectTransform == null)
                {
                    continue;
                }

                if (!active)
                {
                    if (rectTransform.localScale != _baseScale[i])
                    {
                        rectTransform.localScale = _baseScale[i];
                    }

                    continue;
                }
                float k = _punch[i] / PunchDuration, amplitude = (FlyTarget)i == FlyTarget.Info ? 0.1f : 0.35f;
                rectTransform.localScale = _baseScale[i] * (1f + amplitude * Mathf.Sin(k * Mathf.PI));
            }

            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var pop = _pops[i];
                pop.Elapsed += dt / PopDuration;
                if (pop.Elapsed >= 1f)
                {
                    ReleasePop(pop);
                    _pops.RemoveAt(i);
                    continue;
                }
                pop.RectTransform.anchoredPosition = pop.Position + new Vector2(0f, 26f * pop.Direction * Mathf.Sqrt(pop.Elapsed));
                var color = pop.Label.color;
                color.a = pop.Elapsed < 0.6f ? 1f : 1f - (pop.Elapsed - 0.6f) / 0.4f;
                pop.Label.color = color;
            }
        }

        private void ReleaseIcon(Item item)
        {
            item.Image.gameObject.SetActive(false);
            _iconPool.Push(item.Image);
        }

        private void ReleasePop(Pop pop)
        {
            pop.Label.gameObject.SetActive(false);
            _popPool.Push(pop.Label);
        }

        private void Arrive(FlyTarget target, int amount, string label, Color color)
        {
            _punch[(int)target] = PunchDuration;
            // label이 빈 문자열이면 글자 없이 칸만 튐. null이면 양을 +N으로 띄움
            if (label != null)
            {
                if (label.Length > 0)
                {
                    SpawnPop(target, label, color);
                }
            }
            else if (amount != 0)
            {
                SpawnPop(target, (amount > 0 ? "+" : "") + amount, color);
            }
        }

        private void SpawnPop(FlyTarget target, string text, Color color)
        {
            // 처치가 몰릴 때 글자 오브젝트가 끝없이 늘지 않게 막음. 칸은 계속 튐
            if (_pops.Count >= MaxPops)
            {
                return;
            }

            var label = _popPool.Count > 0 ? _popPool.Pop() : Instantiate(_popPrefab, transform, false);
            UiFit.SetText(label, text);
            label.color = color;
            // 화면 위쪽 칸(머리말)은 아래로, 아래쪽 칸(꼬리말)은 위로 글자를 띄워 화면 밖으로 나가지 않게 함
            var targetLocalPosition = TargetLocal(target);
            float direction = targetLocalPosition.y > 0f ? -1f : 1f;
            var popPosition = targetLocalPosition + new Vector2(0f, 18f * direction);
            label.rectTransform.anchoredPosition = popPosition;
            label.gameObject.SetActive(true);
            _pops.Add(new Pop { RectTransform = label.rectTransform, Label = label, Position = popPosition, Elapsed = 0f, Direction = direction });
        }

        private void Place(Item item)
        {
            if (item.Delay > 0f)
            {
                item.RectTransform.anchoredPosition = item.From;
                item.RectTransform.localScale = Vector3.zero;
                return;
            }
            float k = item.Elapsed < 0.5f ? 2f * item.Elapsed * item.Elapsed : 1f - 2f * (1f - item.Elapsed) * (1f - item.Elapsed);
            var targetPosition = TargetLocal(item.Target);
            float remaining = 1f - k;
            item.RectTransform.anchoredPosition = item.From * (remaining * remaining) + item.ControlPoint * (2f * remaining * k) + targetPosition * (k * k);
            float scale = item.Elapsed < 0.15f ? Mathf.Lerp(0f, 1.25f, item.Elapsed / 0.15f) : Mathf.Lerp(1.25f, 0.7f, (item.Elapsed - 0.15f) / 0.85f);
            item.RectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, null, out var local);
            return local;
        }

        /// <summary>칸 가운데의 화면 좌표입니다. 칸을 못 찾으면 화면 가운데를 돌려줍니다.</summary>
        public Vector2 TargetScreen(FlyTarget target)
        {
            var rectTransform = _resolve(target);
            if (rectTransform == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return rectTransform.TransformPoint(rectTransform.rect.center);
        }

        private Vector2 TargetLocal(FlyTarget target) => ToLocal(TargetScreen(target));
    }
}
