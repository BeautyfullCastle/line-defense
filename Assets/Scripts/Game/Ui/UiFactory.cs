using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>uGUI 요소를 코드로 만드는 도우미입니다.</summary>
    public static class UiFactory
    {
        static Font _regular, _bold;
        static Sprite _round;

        /// <summary>일반 굵기 글꼴입니다. 리소스가 없으면 내장 글꼴을 씁니다.</summary>
        public static Font Regular => _regular != null ? _regular : (_regular = LoadFont("Fonts/NanumGothic-Regular"));

        /// <summary>굵은 글꼴입니다. 리소스가 없으면 일반 글꼴을 씁니다.</summary>
        public static Font Bold => _bold != null ? _bold : (_bold = LoadFont("Fonts/NanumGothic-Bold") ?? Regular);

        static Font LoadFont(string path)
        {
            var f = Resources.Load<Font>(path);
            if (f != null) return f;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>9분할 둥근 사각형 스프라이트입니다. 모서리 반지름은 캔버스 단위 8입니다.</summary>
        public static Sprite Round
        {
            get
            {
                if (_round != null) return _round;
                const int size = 32, r = 8;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(r - 0.5f - x, x - (size - r - 0.5f), 0f);
                        float dy = Mathf.Max(r - 0.5f - y, y - (size - r - 0.5f), 0f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                        px[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _round = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _round;
            }
        }

        /// <summary>빈 RectTransform 오브젝트를 만듭니다.</summary>
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>부모를 가득 채우도록 늘립니다.</summary>
        public static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        /// <summary>앵커 한 점과 피벗, 위치, 크기를 한 번에 지정합니다.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>색 배경 이미지를 붙입니다. rounded가 참이면 둥근 모서리를 씁니다.</summary>
        public static Image Fill(RectTransform rt, Color color, bool rounded = true, bool raycast = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            if (rounded)
            {
                img.sprite = Round;
                img.type = Image.Type.Sliced;
            }
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>테두리가 있는 둥근 카드를 만듭니다. 내용물은 반환값 아래에 붙입니다.</summary>
        public static RectTransform Card(Transform parent, string name, Color fill, Color border)
        {
            var outer = Rect(parent, name);
            Fill(outer, border);
            var inner = Rect(outer, "Fill");
            Stretch(inner, 1.5f, 1.5f, 1.5f, 1.5f);
            Fill(inner, fill, true, false);
            return outer;
        }

        /// <summary>글자 요소를 만듭니다.</summary>
        public static Text Label(Transform parent, string text, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? Bold : Regular;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.text = text;
            return t;
        }

        /// <summary>글자 한 줄의 폭을 캔버스 단위로 잰 값입니다.</summary>
        public static float TextWidth(string text, int size, bool bold)
        {
            var gen = new TextGenerator();
            var s = new TextGenerationSettings
            {
                font = bold ? Bold : Regular,
                fontSize = size,
                fontStyle = FontStyle.Normal,
                scaleFactor = 1f,
                generateOutOfBounds = true,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                verticalOverflow = VerticalWrapMode.Overflow,
                generationExtents = new Vector2(10000f, 10000f),
                pivot = new Vector2(0.5f, 0.5f),
                richText = true,
                lineSpacing = 1f,
                resizeTextForBestFit = false,
                textAnchor = TextAnchor.MiddleLeft,
                color = Color.black,
            };
            return gen.GetPreferredWidth(text, s) + 2f;
        }

        /// <summary>둥근 버튼을 만듭니다.</summary>
        public static UiButton Button(Transform parent, string label, Action onClick, float w, float h, int fontSize = 13, bool primary = false)
        {
            var outer = Rect(parent, "Button_" + label);
            outer.sizeDelta = new Vector2(w, h);
            var border = Fill(outer, primary ? Palette.Ink : Palette.Line);
            var inner = Rect(outer, "Fill");
            Stretch(inner, 1.5f, 1.5f, 1.5f, 1.5f);
            var fill = Fill(inner, primary ? Palette.Ink : Palette.Panel);
            var txt = Label(inner, label, fontSize, primary ? Palette.Ground : Palette.Ink, true, TextAnchor.MiddleCenter);
            Stretch(txt.rectTransform);
            var btn = outer.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            btn.colors = cb;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var ub = outer.gameObject.AddComponent<UiButton>();
            ub.Init(btn, border, fill, txt, primary);
            return ub;
        }
    }

    /// <summary>둥근 버튼의 상태(활성, 선택)를 바꾸는 도우미 컴포넌트입니다.</summary>
    public sealed class UiButton : MonoBehaviour
    {
        Button _btn;
        Image _border, _fill;
        Text _label;
        bool _primary;

        /// <summary>내부 요소를 연결합니다.</summary>
        public void Init(Button btn, Image border, Image fill, Text label, bool primary)
        {
            _btn = btn; _border = border; _fill = fill; _label = label; _primary = primary;
        }

        /// <summary>눌림 이벤트 대상 버튼입니다.</summary>
        public Button Button => _btn;

        /// <summary>버튼 크기를 지정합니다.</summary>
        public RectTransform Rt => (RectTransform)transform;

        /// <summary>글자를 바꿉니다.</summary>
        public void SetText(string s)
        {
            if (_label.text != s) _label.text = s;
        }

        /// <summary>눌릴 수 있는지 지정합니다. 비활성이면 글자도 흐려집니다.</summary>
        public void SetInteractable(bool on)
        {
            if (_btn.interactable == on) return;
            _btn.interactable = on;
            var c = _label.color;
            c.a = on ? 1f : 0.4f;
            _label.color = c;
        }

        /// <summary>선택 상태(검정 바탕에 밝은 글자)를 지정합니다.</summary>
        public void SetOn(bool on)
        {
            if (_primary) return;
            _border.color = on ? Palette.Ink : Palette.Line;
            _fill.color = on ? Palette.Ink : Palette.Panel;
            var c = on ? Palette.Panel : Palette.Ink;
            c.a = _label.color.a;
            _label.color = c;
        }

        /// <summary>글자 색을 바꿉니다.</summary>
        public void SetTextColor(Color c) => _label.color = c;
    }

    /// <summary>자식을 왼쪽에서 오른쪽으로 놓고 폭을 넘으면 줄바꿈하는 배치기입니다.</summary>
    public sealed class FlowPanel
    {
        struct Item
        {
            public RectTransform Rt;
            public bool BreakBefore;
        }

        readonly List<Item> _items = new List<Item>();

        /// <summary>항목을 추가합니다. 크기는 RectTransform.sizeDelta를 씁니다.</summary>
        public void Add(RectTransform rt, bool breakBefore = false)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            _items.Add(new Item { Rt = rt, BreakBefore = breakBefore });
        }

        /// <summary>항목을 모두 비웁니다. 오브젝트는 파괴하지 않습니다.</summary>
        public void Clear() => _items.Clear();

        /// <summary>폭에 맞춰 배치하고 필요한 높이를 돌려줍니다.</summary>
        public float Layout(float width, float padX, float padY, float gapX, float gapY)
        {
            float x = padX, y = padY, rowH = 0f, avail = width - padX;
            foreach (var it in _items)
            {
                if (!it.Rt.gameObject.activeSelf) continue;
                var s = it.Rt.sizeDelta;
                bool wrap = it.BreakBefore || (x > padX && x + s.x > avail);
                if (wrap && rowH > 0f)
                {
                    y += rowH + gapY;
                    x = padX;
                    rowH = 0f;
                }
                it.Rt.anchoredPosition = new Vector2(x, -y);
                x += s.x + gapX;
                rowH = Mathf.Max(rowH, s.y);
            }
            return y + rowH + padY;
        }
    }
}
