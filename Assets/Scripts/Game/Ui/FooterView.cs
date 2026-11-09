using System;
using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>하단 꼬리말(건설 버튼, 마차 정보, 노선 칩, 안내 문구)입니다. 프리팹 Prefabs/UI/Footer에 붙습니다.</summary>
    public sealed class FooterView : MonoBehaviour
    {
        const string InkHex = "#23261F";

        static readonly BuildKind[] Kinds = { BuildKind.Stable, BuildKind.Fort, BuildKind.Warehouse };

        [SerializeField] FlowLayout _flow;
        [SerializeField] UiButton[] _build = new UiButton[3];
        [SerializeField] Image[] _buildIcons = new Image[3];
        [SerializeField] Text[] _counts = new Text[3];
        [SerializeField] Text _cartInfo, _hint;
        [SerializeField] LineChip _chipPrefab;

        readonly List<LineChip> _chips = new List<LineChip>();
        string _hintText = string.Empty;
        int _sig = int.MinValue;
        int _infoKey = int.MinValue;

        /// <summary>꼬리말 루트입니다.</summary>
        public RectTransform Root => (RectTransform)transform;

        /// <summary>건설 버튼을 눌렀을 때 호출됩니다.</summary>
        public Action<BuildKind> OnBuild;

        /// <summary>칩이 바뀌어 다시 배치해야 하는지 여부입니다.</summary>
        public bool NeedsLayout { get; private set; } = true;

        /// <summary>날아오는 중이라 아직 숫자에 넣지 않을 권한 수를 돌려줍니다.</summary>
        public Func<BuildKind, int> Pending;

        void Awake()
        {
            for (int i = 0; i < 3; i++)
            {
                var k = Kinds[i];
                _build[i].Button.onClick.AddListener(() => OnBuild?.Invoke(k));
            }
        }

        /// <summary>폭에 맞춰 칩과 안내 문구까지 배치하고 꼬리말 높이를 돌려줍니다.</summary>
        public float Layout(float canvasW)
        {
            NeedsLayout = false;
            var hr = _hint.rectTransform;
            float hw = canvasW - 32f;
            // 높이를 재려면 폭을 먼저 맞춰야 함
            if (!Mathf.Approximately(hr.sizeDelta.x, hw)) hr.sizeDelta = new Vector2(hw, hr.sizeDelta.y);
            SetSize(hr, hw, Mathf.Max(20f, Mathf.Ceil(_hint.preferredHeight)));
            SetSize(_cartInfo.rectTransform, Mathf.Min(420f, canvasW - 32f), canvasW < 520f ? 34f : 20f);
            float h = _flow.Layout(canvasW, 16f, 10f, 8f, 8f);
            SetSize(Root, 0f, h);
            return h;
        }

        /// <summary>건설 버튼의 권한 개수 칩입니다. 날아오는 권한 아이콘이 여기로 들어갑니다.</summary>
        public RectTransform CountRect(BuildKind k) => (RectTransform)_counts[Array.IndexOf(Kinds, k)].rectTransform.parent;

        /// <summary>마차 적재·속도·창고 보관 안내 글자입니다.</summary>
        public RectTransform InfoRect => _cartInfo.rectTransform;

        /// <summary>토큰 수, 선택 상태, 칩, 안내 문구를 갱신합니다.</summary>
        public void Refresh(Game g, ViewState v)
        {
            for (int i = 0; i < 3; i++)
            {
                int tok = Token(g, Kinds[i]);
                _build[i].SetInteractable(tok > 0);
                bool on = v.HasMode && v.Mode == Kinds[i];
                _build[i].SetOn(on);
                var ic = on ? Palette.Panel : Palette.Ink;
                if (_buildIcons[i].color != ic) _buildIcons[i].color = ic;
            }

            int pct = Mathf.RoundToInt(100f * (1f + g.B.CART_SPD_UP * g.CartSpdLv));
            int p0 = Pending?.Invoke(BuildKind.Stable) ?? 0, p1 = Pending?.Invoke(BuildKind.Fort) ?? 0, p2 = Pending?.Invoke(BuildKind.Warehouse) ?? 0;
            int key = HashCode.Combine(g.TokStable - p0, g.TokFort - p1, g.TokWarehouse - p2, g.CartCap(), pct, g.WhCap(), g.TokCart);
            if (key != _infoKey)
            {
                _infoKey = key;
                for (int i = 0; i < 3; i++) SetText(_counts[i], Mathf.Max(0, Token(g, Kinds[i]) - (Pending?.Invoke(Kinds[i]) ?? 0)).ToString());
                string info = $"마차 적재 <color={InkHex}><b>{g.CartCap()}</b></color> · 속도 <color={InkHex}><b>{pct}%</b></color> · 창고 보관 <color={InkHex}><b>{g.WhCap()}</b></color>";
                if (g.TokCart > 0) info += $"  <color={InkHex}><b>배치 대기 {g.TokCart}</b></color>";
                SetText(_cartInfo, info);
            }

            int sig = Signature(g);
            if (sig != _sig)
            {
                _sig = sig;
                RebuildChips(g);
                NeedsLayout = true;
            }

            string hint = HintFor(g, v);
            if (hint != _hintText)
            {
                _hintText = hint;
                SetText(_hint, hint);
                NeedsLayout = true;
            }
        }

        static int Signature(Game g)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + g.TokCart;
                foreach (var l in g.Lines)
                {
                    h = h * 31 + l.Id;
                    h = h * 31 + l.Nodes.Count;
                    h = h * 31 + l.Carts.Count;
                    h = h * 31 + (l.Loop ? 1 : 0);
                    foreach (int id in l.Nodes)
                    {
                        var n = g.NodeById(id);
                        if (n != null && n.Type == NodeType.Fort) h = h * 31 + 7;
                    }
                }
                return h;
            }
        }

        static int Token(Game g, BuildKind k) => k == BuildKind.Stable ? g.TokStable : k == BuildKind.Fort ? g.TokFort : g.TokWarehouse;

        static string HintFor(Game g, ViewState v)
        {
            if (v.HasMode)
            {
                switch (v.Mode)
                {
                    case BuildKind.Stable: return "개척지 안 빈 곳을 클릭해 마굿간을 놓으세요. 우클릭이나 Esc로 취소.";
                    case BuildKind.Warehouse: return "노선들이 만날 자리에 창고를 놓으세요. 우클릭이나 Esc로 취소.";
                    default: return "요새는 성벽 위에만 지을 수 있습니다. 성벽 근처를 클릭하면 성벽 선에 붙습니다. 우클릭이나 Esc로 취소.";
                }
            }
            if (g.TokCart > 0) return "마차가 배치를 기다립니다. 아래 노선 칩의 +마차를 누르세요.";
            if (g.Lines.Count == 0) return "① 아래에서 마굿간을 골라 성 안에 놓으세요.";
            bool anyFort = false;
            foreach (var n in g.Nodes) if (n.Type == NodeType.Fort) { anyFort = true; break; }
            if (!anyFort) return "② 요새를 침투로가 닿는 성벽 위에 놓으세요.";
            bool allShort = true, lineHasFort = false;
            foreach (var l in g.Lines)
            {
                if (l.Nodes.Count >= 2) allShort = false;
                foreach (int id in l.Nodes)
                {
                    var n = g.NodeById(id);
                    if (n != null && n.Type == NodeType.Fort) lineHasFort = true;
                }
            }
            if (allShort) return "③ 마굿간에서 농장·대장간·요새로 드래그해 길을 이으세요.";
            if (!lineHasFort) return "노선에 요새가 있어야 마차가 짐을 싣습니다. 길을 요새까지 이으세요.";
            return "선 중간을 끌어 새 정거장에 놓으면 그 사이에 끼워 넣습니다. 길 끝 막대를 반대쪽 끝 정거장에 놓으면 순환 노선이 됩니다.";
        }

        void RebuildChips(Game g)
        {
            // 칩 위치는 힌트 앞에 오도록 flow를 다시 구성
            _flow.Clear();
            for (int i = 0; i < 3; i++) _flow.Add(_build[i].Rt);
            _flow.Add(_cartInfo.rectTransform);
            int count = 0;
            foreach (var l in g.Lines)
            {
                if (count == _chips.Count) _chips.Add(Instantiate(_chipPrefab, transform, false));
                var chip = _chips[count];
                chip.Bind(g, l);
                _flow.Add(chip.Rt, count == 0);
                count++;
            }
            for (int i = count; i < _chips.Count; i++) _chips[i].Release();
            _flow.Add(_hint.rectTransform, true);
        }

        static void SetText(Text t, string s)
        {
            if (t.text != s) t.text = s;
        }

        static void SetSize(RectTransform rt, float w, float h)
        {
            var s = new Vector2(w, h);
            if (rt.sizeDelta != s) rt.sizeDelta = s;
        }
    }
}
