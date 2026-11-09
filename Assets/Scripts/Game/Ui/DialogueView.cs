using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>화면 아래 가운데에 뜨는 대사창입니다. 한 글자씩 찍고, 차단 대사는 클릭해야 넘어갑니다. 프리팹 Prefabs/UI/Dialogue에 붙습니다.</summary>
    public sealed class DialogueView : MonoBehaviour
    {
        private const float CharsPerSec = 42f;
        private const float AmbientHold = 4.5f;

        private sealed class Item
        {
            public string Speaker, Text;
            public bool Block;
        }

        [SerializeField] private Button _cardButton;
        [SerializeField] private Image _face;
        [SerializeField] private Text _faceLabel, _speaker, _text, _sizer, _more;
        [SerializeField] private UiButton _skip;

        private readonly List<Item> _queue = new List<Item>();
        private Item _current;
        private float _shown, _timer;
        private int _shownChars;
        private float _canvasWidth = 1280f, _bottomInset = 80f;

        private RectTransform RectTransform => (RectTransform)transform;

        /// <summary>차단 대사가 표시 중인지 여부입니다.</summary>
        public bool Blocking => _current != null && _current.Block;

        /// <summary>표시 중인 대사가 있는지 여부입니다.</summary>
        public bool Active => _current != null;

        private void Awake()
        {
            _cardButton.onClick.AddListener(Advance);
            _skip.Button.onClick.AddListener(Skip);
        }

        /// <summary>창 크기와 하단 여백이 바뀌면 다시 배치합니다.</summary>
        public void Relayout(float canvasWidth, float bottomInset)
        {
            _canvasWidth = canvasWidth;
            _bottomInset = bottomInset;
            if (_current != null)
            {
                Apply();
            }
            else
            {
                SetPosition(RectTransform, new Vector2(0f, bottomInset + 14f));
            }
        }

        /// <summary>대사를 큐에 넣습니다. 차단 대사는 앞선 일반 대사를 밀어냅니다.</summary>
        public void Say(string[][] lines, bool block)
        {
            if (lines == null || lines.Length == 0)
            {
                return;
            }

            if (block)
            {
                _queue.RemoveAll(i => !i.Block);
                if (_current != null && !_current.Block)
                {
                    _current = null;
                }
            }
            else if (_queue.Count > 4)
            {
                return;
            }

            foreach (var pair in lines)
            {
                if (pair == null || pair.Length < 2)
                {
                    continue;
                }

                _queue.Add(new Item { Speaker = pair[0], Text = pair[1], Block = block });
            }
            if (_current == null)
            {
                Next();
            }
        }

        /// <summary>대사창 안의 모든 대사를 비웁니다.</summary>
        public void Clear()
        {
            _queue.Clear();
            _current = null;
            Show(gameObject, false);
        }

        private void Next()
        {
            if (_queue.Count == 0)
            {
                _current = null;
                Show(gameObject, false);
                return;
            }
            _current = _queue[0];
            _queue.RemoveAt(0);
            _shown = 0f;
            _shownChars = 0;
            _timer = AmbientHold;
            SetText(_text, string.Empty);
            Show(gameObject, true);
            Apply();
        }

        private static void Speaker(string speakerKey, out string name, out string face, out Color color)
        {
            switch (speakerKey)
            {
                case "gen":
                    name = "이영지 장군";
                    face = "장";
                    color = Palette.SpeakerGeneral;
                    break;
                case "aide":
                    name = "부관 서하";
                    face = "서";
                    color = Palette.SpeakerAide;
                    break;
                case "msg":
                    name = "황실 전령";
                    face = "령";
                    color = Palette.SpeakerMessenger;
                    break;
                default:
                    name = string.Empty;
                    face = string.Empty;
                    color = Palette.Muted;
                    break;
            }
        }

        private void Apply()
        {
            var item = _current;
            bool isNarration = item.Speaker == "narr";
            Speaker(item.Speaker, out string name, out string face, out Color color);
            float cardWidth = Mathf.Min(640f, _canvasWidth - 32f);
            float contentX = isNarration ? 14f : 66f, contentWidth = cardWidth - contentX - 14f;
            int size = item.Block ? 15 : 14;

            Show(_face.gameObject, !isNarration);
            Show(_speaker.gameObject, !isNarration);
            if (!isNarration)
            {
                if (_face.color != color)
                {
                    _face.color = color;
                }

                SetText(_faceLabel, face);
                SetText(_speaker, name);
                if (_speaker.color != color)
                {
                    _speaker.color = color;
                }

                SetTop(_face.rectTransform, 14f, 12f, 40f, 40f);
                SetTop(_speaker.rectTransform, contentX, 10f, contentWidth, 16f);
            }

            Style(_text, size, isNarration);
            Style(_sizer, size, isNarration);
            var textColor = isNarration ? Palette.Muted : Palette.Ink;
            if (_text.color != textColor)
            {
                _text.color = textColor;
            }

            SetText(_sizer, item.Text);
            float textY = isNarration ? 12f : 28f;
            // 높이를 재려면 폭이 먼저 맞아야 함
            SetTop(_sizer.rectTransform, contentX, textY, contentWidth, _sizer.rectTransform.sizeDelta.y);
            float textHeight = Mathf.Max(24f, Mathf.Ceil(_sizer.preferredHeight));
            SetTop(_sizer.rectTransform, contentX, textY, contentWidth, textHeight);
            SetTop(_text.rectTransform, contentX, textY, contentWidth, textHeight);

            float cardHeight = textY + textHeight + 12f;
            Show(_more.gameObject, item.Block);
            Show(_skip.gameObject, item.Block);
            if (item.Block)
            {
                SetTop(_more.rectTransform, contentX, textY + textHeight + 4f, 160f, 22f);
                SetTop(_skip.RectTransform, cardWidth - 14f - 64f, textY + textHeight + 4f, 64f, 22f);
                cardHeight = textY + textHeight + 4f + 22f + 10f;
            }
            SetSize(RectTransform, new Vector2(cardWidth, Mathf.Max(cardHeight, isNarration ? 0f : 64f)));
            SetPosition(RectTransform, new Vector2(0f, _bottomInset + 14f));
        }

        private static void Style(Text label, int size, bool isNarration)
        {
            var anchor = isNarration ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            var style = isNarration ? FontStyle.Italic : FontStyle.Normal;
            if (label.fontSize != size)
            {
                label.fontSize = size;
            }

            if (label.alignment != anchor)
            {
                label.alignment = anchor;
            }

            if (label.fontStyle != style)
            {
                label.fontStyle = style;
            }
        }

        // 아래 도우미들은 값이 바뀔 때만 대입해 캔버스 갱신을 줄임
        private static void Show(GameObject targetObject, bool on)
        {
            if (targetObject.activeSelf != on)
            {
                targetObject.SetActive(on);
            }
        }

        private static void SetText(Text label, string value)
        {
            if (label.text != value)
            {
                label.text = value;
            }
        }

        private static void SetPosition(RectTransform rectTransform, Vector2 position)
        {
            if (rectTransform.anchoredPosition != position)
            {
                rectTransform.anchoredPosition = position;
            }
        }

        private static void SetSize(RectTransform rectTransform, Vector2 size)
        {
            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }

        // 앵커는 프리팹에서 왼쪽 위로 잡혀 있음
        private static void SetTop(RectTransform rectTransform, float x, float y, float width, float height)
        {
            SetPosition(rectTransform, new Vector2(x, -y));
            SetSize(rectTransform, new Vector2(width, height));
        }

        /// <summary>한 글자씩 찍기와 일반 대사 자동 넘김을 진행합니다.</summary>
        public void Tick(float dt)
        {
            var item = _current;
            if (item == null)
            {
                return;
            }

            if (_shown < item.Text.Length)
            {
                _shown = Mathf.Min(item.Text.Length, _shown + dt * CharsPerSec);
                int visibleCharCount = Mathf.FloorToInt(_shown);
                if (visibleCharCount != _shownChars)
                {
                    _shownChars = visibleCharCount;
                    _text.text = item.Text.Substring(0, visibleCharCount);
                }
            }
            else if (!item.Block)
            {
                _timer -= dt;
                if (_timer <= 0f)
                {
                    Advance();
                }
            }
        }

        /// <summary>글이 덜 찍혔으면 모두 보여주고, 다 찍혔으면 다음 대사로 넘어갑니다.</summary>
        public void Advance()
        {
            var item = _current;
            if (item == null)
            {
                return;
            }

            if (_shown < item.Text.Length)
            {
                _shown = item.Text.Length;
                _shownChars = item.Text.Length;
                SetText(_text, item.Text);
                return;
            }
            Next();
        }

        /// <summary>차단 대사를 모두 건너뜁니다.</summary>
        public void Skip()
        {
            _queue.RemoveAll(i => i.Block);
            _current = null;
            Next();
        }
    }
}
