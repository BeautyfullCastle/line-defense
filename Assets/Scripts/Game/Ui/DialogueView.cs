using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>화면 아래 가운데에 뜨는 대사창입니다. 한 글자씩 찍고, 차단 대사는 클릭해야 넘어갑니다.</summary>
    public sealed class DialogueView
    {
        const float CharsPerSec = 42f;
        const float AmbientHold = 4.5f;

        sealed class Item
        {
            public string Who, Text;
            public bool Block;
        }

        readonly RectTransform _root;
        readonly Image _face;
        readonly Text _faceTxt, _who, _txt, _sizer, _more;
        readonly UiButton _skip;
        readonly List<Item> _q = new List<Item>();
        Item _cur;
        float _shown, _timer;
        float _canvasW = 1280f, _bottomInset = 80f;

        /// <summary>차단 대사가 표시 중인지 여부입니다.</summary>
        public bool Blocking => _cur != null && _cur.Block;

        /// <summary>표시 중인 대사가 있는지 여부입니다.</summary>
        public bool Active => _cur != null;

        /// <summary>대사창 요소를 만듭니다. 처음에는 숨겨져 있습니다.</summary>
        public DialogueView(Transform parent)
        {
            _root = UiFactory.Card(parent, "Dialogue", Palette.Panel, Palette.Line);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            var btn = _root.gameObject.AddComponent<Button>();
            btn.targetGraphic = _root.GetComponent<Image>();
            btn.transition = Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.onClick.AddListener(Advance);

            var faceRt = UiFactory.Rect(_root, "Face");
            _face = faceRt.gameObject.AddComponent<Image>();
            _face.sprite = IconFactory.Get("disc");
            _face.raycastTarget = false;
            _faceTxt = UiFactory.Label(faceRt, string.Empty, 19, Palette.Panel, true, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_faceTxt.rectTransform);

            _who = UiFactory.Label(_root, string.Empty, 12, Palette.Ink, true);
            _sizer = UiFactory.Label(_root, string.Empty, 15, Color.clear);
            _txt = UiFactory.Label(_root, string.Empty, 15, Palette.Ink);
            _more = UiFactory.Label(_root, "클릭해서 계속 >", 11, Palette.Muted);
            _skip = UiFactory.Button(_root, "건너뛰기", Skip, 64f, 22f, 11);
            _root.gameObject.SetActive(false);
        }

        /// <summary>창 크기와 하단 여백이 바뀌면 다시 배치합니다.</summary>
        public void Relayout(float canvasW, float bottomInset)
        {
            _canvasW = canvasW; _bottomInset = bottomInset;
            if (_cur != null) Apply();
            else _root.anchoredPosition = new Vector2(0f, bottomInset + 14f);
        }

        /// <summary>대사를 큐에 넣습니다. 차단 대사는 앞선 일반 대사를 밀어냅니다.</summary>
        public void Say(string[][] lines, bool block)
        {
            if (lines == null || lines.Length == 0) return;
            if (block)
            {
                _q.RemoveAll(i => !i.Block);
                if (_cur != null && !_cur.Block) _cur = null;
            }
            else if (_q.Count > 4) return;
            foreach (var pair in lines)
            {
                if (pair == null || pair.Length < 2) continue;
                _q.Add(new Item { Who = pair[0], Text = pair[1], Block = block });
            }
            if (_cur == null) Next();
        }

        /// <summary>대사창 안의 모든 대사를 비웁니다.</summary>
        public void Clear()
        {
            _q.Clear();
            _cur = null;
            _root.gameObject.SetActive(false);
        }

        void Next()
        {
            if (_q.Count == 0)
            {
                _cur = null;
                _root.gameObject.SetActive(false);
                return;
            }
            _cur = _q[0];
            _q.RemoveAt(0);
            _shown = 0f;
            _timer = AmbientHold;
            _txt.text = string.Empty;
            _root.gameObject.SetActive(true);
            Apply();
        }

        static void Speaker(string who, out string name, out string face, out Color col)
        {
            switch (who)
            {
                case "gen": name = "이영지 장군"; face = "장"; col = Palette.SpGen; break;
                case "aide": name = "부관 서하"; face = "서"; col = Palette.SpAide; break;
                case "msg": name = "황실 전령"; face = "령"; col = Palette.SpMsg; break;
                default: name = string.Empty; face = string.Empty; col = Palette.Muted; break;
            }
        }

        void Apply()
        {
            var it = _cur;
            bool narr = it.Who == "narr";
            Speaker(it.Who, out string name, out string face, out Color col);
            float w = Mathf.Min(640f, _canvasW - 32f);
            float cx = narr ? 14f : 66f, cw = w - cx - 14f;
            int size = it.Block ? 15 : 14;

            _face.gameObject.SetActive(!narr);
            _who.gameObject.SetActive(!narr);
            if (!narr)
            {
                _face.color = col;
                _faceTxt.text = face;
                _who.text = name;
                _who.color = col;
                SetTop(_face.rectTransform, 14f, 12f, 40f, 40f);
                SetTop(_who.rectTransform, cx, 10f, cw, 16f);
            }

            foreach (var t in new[] { _txt, _sizer })
            {
                t.fontSize = size;
                t.alignment = narr ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
                t.fontStyle = narr ? FontStyle.Italic : FontStyle.Normal;
            }
            _txt.color = narr ? Palette.Muted : Palette.Ink;
            _sizer.text = it.Text;
            float ty = narr ? 12f : 28f;
            SetTop(_sizer.rectTransform, cx, ty, cw, 10f);
            float th = Mathf.Max(24f, Mathf.Ceil(_sizer.preferredHeight));
            SetTop(_sizer.rectTransform, cx, ty, cw, th);
            SetTop(_txt.rectTransform, cx, ty, cw, th);

            float h = ty + th + 12f;
            _more.gameObject.SetActive(it.Block);
            _skip.gameObject.SetActive(it.Block);
            if (it.Block)
            {
                SetTop(_more.rectTransform, cx, ty + th + 4f, 160f, 22f);
                SetTop(_skip.Rt, w - 14f - 64f, ty + th + 4f, 64f, 22f);
                h = ty + th + 4f + 22f + 10f;
            }
            _root.sizeDelta = new Vector2(w, Mathf.Max(h, narr ? 0f : 64f));
            _root.anchoredPosition = new Vector2(0f, _bottomInset + 14f);
        }

        static void SetTop(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>한 글자씩 찍기와 일반 대사 자동 넘김을 진행합니다.</summary>
        public void Tick(float dt)
        {
            var it = _cur;
            if (it == null) return;
            if (_shown < it.Text.Length)
            {
                _shown = Mathf.Min(it.Text.Length, _shown + dt * CharsPerSec);
                _txt.text = it.Text.Substring(0, Mathf.FloorToInt(_shown));
            }
            else if (!it.Block)
            {
                _timer -= dt;
                if (_timer <= 0f) Advance();
            }
        }

        /// <summary>글이 덜 찍혔으면 모두 보여주고, 다 찍혔으면 다음 대사로 넘어갑니다.</summary>
        public void Advance()
        {
            var it = _cur;
            if (it == null) return;
            if (_shown < it.Text.Length)
            {
                _shown = it.Text.Length;
                _txt.text = it.Text;
                return;
            }
            Next();
        }

        /// <summary>차단 대사를 모두 건너뜁니다.</summary>
        public void Skip()
        {
            _q.RemoveAll(i => i.Block);
            _cur = null;
            Next();
        }
    }
}
