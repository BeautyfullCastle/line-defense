using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>화면 아래 가운데에 뜨는 대사창입니다. 한 글자씩 찍고, 차단 대사는 클릭해야 넘어갑니다. 프리팹 Prefabs/UI/Dialogue에 붙습니다.</summary>
    public sealed class DialogueView : MonoBehaviour
    {
        const float CharsPerSec = 42f;
        const float AmbientHold = 4.5f;

        sealed class Item
        {
            public string Who, Text;
            public bool Block;
        }

        [SerializeField] Button _cardBtn;
        [SerializeField] Image _face;
        [SerializeField] Text _faceTxt, _who, _txt, _sizer, _more;
        [SerializeField] UiButton _skip;

        readonly List<Item> _q = new List<Item>();
        Item _cur;
        float _shown, _timer;
        int _shownChars;
        float _canvasW = 1280f, _bottomInset = 80f;

        RectTransform Rt => (RectTransform)transform;

        /// <summary>차단 대사가 표시 중인지 여부입니다.</summary>
        public bool Blocking => _cur != null && _cur.Block;

        /// <summary>표시 중인 대사가 있는지 여부입니다.</summary>
        public bool Active => _cur != null;

        void Awake()
        {
            _cardBtn.onClick.AddListener(Advance);
            _skip.Button.onClick.AddListener(Skip);
        }

        /// <summary>창 크기와 하단 여백이 바뀌면 다시 배치합니다.</summary>
        public void Relayout(float canvasW, float bottomInset)
        {
            _canvasW = canvasW; _bottomInset = bottomInset;
            if (_cur != null) Apply();
            else Pos(Rt, new Vector2(0f, bottomInset + 14f));
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
            Show(gameObject, false);
        }

        void Next()
        {
            if (_q.Count == 0)
            {
                _cur = null;
                Show(gameObject, false);
                return;
            }
            _cur = _q[0];
            _q.RemoveAt(0);
            _shown = 0f;
            _shownChars = 0;
            _timer = AmbientHold;
            Txt(_txt, string.Empty);
            Show(gameObject, true);
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

            Show(_face.gameObject, !narr);
            Show(_who.gameObject, !narr);
            if (!narr)
            {
                if (_face.color != col) _face.color = col;
                Txt(_faceTxt, face);
                Txt(_who, name);
                if (_who.color != col) _who.color = col;
                SetTop(_face.rectTransform, 14f, 12f, 40f, 40f);
                SetTop(_who.rectTransform, cx, 10f, cw, 16f);
            }

            Style(_txt, size, narr);
            Style(_sizer, size, narr);
            var txtCol = narr ? Palette.Muted : Palette.Ink;
            if (_txt.color != txtCol) _txt.color = txtCol;
            Txt(_sizer, it.Text);
            float ty = narr ? 12f : 28f;
            // 높이를 재려면 폭이 먼저 맞아야 함
            SetTop(_sizer.rectTransform, cx, ty, cw, _sizer.rectTransform.sizeDelta.y);
            float th = Mathf.Max(24f, Mathf.Ceil(_sizer.preferredHeight));
            SetTop(_sizer.rectTransform, cx, ty, cw, th);
            SetTop(_txt.rectTransform, cx, ty, cw, th);

            float h = ty + th + 12f;
            Show(_more.gameObject, it.Block);
            Show(_skip.gameObject, it.Block);
            if (it.Block)
            {
                SetTop(_more.rectTransform, cx, ty + th + 4f, 160f, 22f);
                SetTop(_skip.Rt, w - 14f - 64f, ty + th + 4f, 64f, 22f);
                h = ty + th + 4f + 22f + 10f;
            }
            Size(Rt, new Vector2(w, Mathf.Max(h, narr ? 0f : 64f)));
            Pos(Rt, new Vector2(0f, _bottomInset + 14f));
        }

        static void Style(Text t, int size, bool narr)
        {
            var anchor = narr ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            var style = narr ? FontStyle.Italic : FontStyle.Normal;
            if (t.fontSize != size) t.fontSize = size;
            if (t.alignment != anchor) t.alignment = anchor;
            if (t.fontStyle != style) t.fontStyle = style;
        }

        // 아래 도우미들은 값이 바뀔 때만 대입해 캔버스 갱신을 줄임
        static void Show(GameObject go, bool on)
        {
            if (go.activeSelf != on) go.SetActive(on);
        }

        static void Txt(Text t, string s)
        {
            if (t.text != s) t.text = s;
        }

        static void Pos(RectTransform rt, Vector2 p)
        {
            if (rt.anchoredPosition != p) rt.anchoredPosition = p;
        }

        static void Size(RectTransform rt, Vector2 s)
        {
            if (rt.sizeDelta != s) rt.sizeDelta = s;
        }

        // 앵커는 프리팹에서 왼쪽 위로 잡혀 있음
        static void SetTop(RectTransform rt, float x, float y, float w, float h)
        {
            Pos(rt, new Vector2(x, -y));
            Size(rt, new Vector2(w, h));
        }

        /// <summary>한 글자씩 찍기와 일반 대사 자동 넘김을 진행합니다.</summary>
        public void Tick(float dt)
        {
            var it = _cur;
            if (it == null) return;
            if (_shown < it.Text.Length)
            {
                _shown = Mathf.Min(it.Text.Length, _shown + dt * CharsPerSec);
                int n = Mathf.FloorToInt(_shown);
                if (n != _shownChars)
                {
                    _shownChars = n;
                    _txt.text = it.Text.Substring(0, n);
                }
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
                _shownChars = it.Text.Length;
                Txt(_txt, it.Text);
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
