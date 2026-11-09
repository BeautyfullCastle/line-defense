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
        private const string InkHex = "#23261F";

        private static readonly BuildKind[] s_kinds = { BuildKind.Stable, BuildKind.Fort, BuildKind.Warehouse };

        [SerializeField] private FlowLayout _flow;
        [SerializeField] private UiButton[] _build = new UiButton[3];
        [SerializeField] private Image[] _buildIcons = new Image[3];
        [SerializeField] private Text[] _counts = new Text[3];
        [SerializeField] private Text _cartInfo, _hint;
        [SerializeField] private LineChip _chipPrefab;

        private readonly List<LineChip> _chips = new List<LineChip>();
        private string _hintText = string.Empty;
        private int _signature = int.MinValue;
        private int _infoKey = int.MinValue;

        /// <summary>꼬리말 루트입니다.</summary>
        public RectTransform Root => (RectTransform)transform;

        /// <summary>건설 버튼을 눌렀을 때 호출됩니다.</summary>
        public Action<BuildKind> OnBuild;

        /// <summary>칩이 바뀌어 다시 배치해야 하는지 여부입니다.</summary>
        public bool NeedsLayout { get; private set; } = true;

        /// <summary>날아오는 중이라 아직 숫자에 넣지 않을 권한 수를 돌려줍니다.</summary>
        public Func<BuildKind, int> Pending;

        private void Awake()
        {
            for (int i = 0; i < 3; i++)
            {
                var kind = s_kinds[i];
                _build[i].Button.onClick.AddListener(() => OnBuild?.Invoke(kind));
            }
        }

        /// <summary>폭에 맞춰 칩과 안내 문구까지 배치하고 꼬리말 높이를 돌려줍니다.</summary>
        public float Layout(float canvasWidth)
        {
            NeedsLayout = false;
            var rectTransform = _hint.rectTransform;
            float hintWidth = canvasWidth - 32f;
            // 높이를 재려면 폭을 먼저 맞춰야 함
            if (!Mathf.Approximately(rectTransform.sizeDelta.x, hintWidth))
            {
                rectTransform.sizeDelta = new Vector2(hintWidth, rectTransform.sizeDelta.y);
            }

            SetSize(rectTransform, hintWidth, Mathf.Max(20f, Mathf.Ceil(_hint.preferredHeight)));
            SetSize(_cartInfo.rectTransform, Mathf.Min(420f, canvasWidth - 32f), canvasWidth < 520f ? 34f : 20f);
            float footerHeight = _flow.Layout(canvasWidth, 16f, 10f, 8f, 8f);
            SetSize(Root, 0f, footerHeight);
            return footerHeight;
        }

        /// <summary>건설 버튼의 권한 개수 칩입니다. 날아오는 권한 아이콘이 여기로 들어갑니다.</summary>
        public RectTransform CountRect(BuildKind kind) => (RectTransform)_counts[Array.IndexOf(s_kinds, kind)].rectTransform.parent;

        /// <summary>마차 적재·속도·창고 보관 안내 글자입니다.</summary>
        public RectTransform InfoRect => _cartInfo.rectTransform;

        /// <summary>토큰 수, 선택 상태, 칩, 안내 문구를 갱신합니다.</summary>
        public void Refresh(Game game, ViewState view)
        {
            for (int i = 0; i < 3; i++)
            {
                int tokenCount = Token(game, s_kinds[i]);
                _build[i].SetInteractable(tokenCount > 0);
                bool on = view.HasMode && view.Mode == s_kinds[i];
                _build[i].SetOn(on);
                var iconColor = on ? Palette.Panel : Palette.Ink;
                if (_buildIcons[i].color != iconColor)
                {
                    _buildIcons[i].color = iconColor;
                }
            }

            int speedPercent = Mathf.RoundToInt(100f * (1f + game.Balance.CART_SPD_UP * game.CartSpeedLevel));
            int pendingStable = Pending?.Invoke(BuildKind.Stable) ?? 0, pendingFort = Pending?.Invoke(BuildKind.Fort) ?? 0, pendingWarehouse = Pending?.Invoke(BuildKind.Warehouse) ?? 0;
            int key = HashCode.Combine(game.StableTokens - pendingStable, game.FortTokens - pendingFort, game.WarehouseTokens - pendingWarehouse, game.CartCapacity(), speedPercent, game.WarehouseCapacity(), game.CartTokens);
            if (key != _infoKey)
            {
                _infoKey = key;
                for (int i = 0; i < 3; i++)
                {
                    SetText(_counts[i], Mathf.Max(0, Token(game, s_kinds[i]) - (Pending?.Invoke(s_kinds[i]) ?? 0)).ToString());
                }

                string cartInfo = $"마차 적재 <color={InkHex}><b>{game.CartCapacity()}</b></color> · 속도 <color={InkHex}><b>{speedPercent}%</b></color> · 창고 보관 <color={InkHex}><b>{game.WarehouseCapacity()}</b></color>";
                if (game.CartTokens > 0)
                {
                    cartInfo += $"  <color={InkHex}><b>배치 대기 {game.CartTokens}</b></color>";
                }

                SetText(_cartInfo, cartInfo);
            }

            int currentSignature = Signature(game);
            if (currentSignature != _signature)
            {
                _signature = currentSignature;
                RebuildChips(game);
                NeedsLayout = true;
            }

            string hint = HintFor(game, view);
            if (hint != _hintText)
            {
                _hintText = hint;
                SetText(_hint, hint);
                NeedsLayout = true;
            }
        }

        private static int Signature(Game game)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + game.CartTokens;
                foreach (var line in game.Lines)
                {
                    hash = hash * 31 + line.Id;
                    hash = hash * 31 + line.Nodes.Count;
                    hash = hash * 31 + line.Carts.Count;
                    hash = hash * 31 + (line.Loop ? 1 : 0);
                    foreach (int id in line.Nodes)
                    {
                        var node = game.NodeById(id);
                        if (node != null && node.Type == NodeType.Fort)
                        {
                            hash = hash * 31 + 7;
                        }
                    }
                }
                return hash;
            }
        }

        private static int Token(Game game, BuildKind kind) => kind == BuildKind.Stable ? game.StableTokens : kind == BuildKind.Fort ? game.FortTokens : game.WarehouseTokens;

        private static string HintFor(Game game, ViewState view)
        {
            if (view.HasMode)
            {
                switch (view.Mode)
                {
                    case BuildKind.Stable:
                        return "개척지 안 빈 곳을 클릭해 마굿간을 놓으세요. 우클릭이나 Esc로 취소.";
                    case BuildKind.Warehouse:
                        return "노선들이 만날 자리에 창고를 놓으세요. 우클릭이나 Esc로 취소.";
                    default:
                        return "요새는 성벽 위에만 지을 수 있습니다. 성벽 근처를 클릭하면 성벽 선에 붙습니다. 우클릭이나 Esc로 취소.";
                }
            }
            if (game.CartTokens > 0)
            {
                return "마차가 배치를 기다립니다. 아래 노선 칩의 +마차를 누르세요.";
            }

            if (game.Lines.Count == 0)
            {
                return "① 아래에서 마굿간을 골라 성 안에 놓으세요.";
            }

            bool anyFort = false;
            foreach (var node in game.Nodes)
            {
                if (node.Type == NodeType.Fort)
                {
                    anyFort = true;
                    break;
                }
            }

            if (!anyFort)
            {
                return "② 요새를 침투로가 닿는 성벽 위에 놓으세요.";
            }

            bool allShort = true, lineHasFort = false;
            foreach (var line in game.Lines)
            {
                if (line.Nodes.Count >= 2)
                {
                    allShort = false;
                }

                foreach (int id in line.Nodes)
                {
                    var node = game.NodeById(id);
                    if (node != null && node.Type == NodeType.Fort)
                    {
                        lineHasFort = true;
                    }
                }
            }
            if (allShort)
            {
                return "③ 마굿간에서 농장·대장간·요새로 드래그해 길을 이으세요.";
            }

            if (!lineHasFort)
            {
                return "노선에 요새가 있어야 마차가 짐을 싣습니다. 길을 요새까지 이으세요.";
            }

            return "선 중간을 끌어 새 정거장에 놓으면 그 사이에 끼워 넣습니다. 길 끝 막대를 반대쪽 끝 정거장에 놓으면 순환 노선이 됩니다.";
        }

        private void RebuildChips(Game game)
        {
            // 칩 위치는 힌트 앞에 오도록 flow를 다시 구성
            _flow.Clear();
            for (int i = 0; i < 3; i++)
            {
                _flow.Add(_build[i].RectTransform);
            }

            _flow.Add(_cartInfo.rectTransform);
            int count = 0;
            foreach (var line in game.Lines)
            {
                if (count == _chips.Count)
                {
                    _chips.Add(Instantiate(_chipPrefab, transform, false));
                }

                var chip = _chips[count];
                chip.Bind(game, line);
                _flow.Add(chip.RectTransform, count == 0);
                count++;
            }
            for (int i = count; i < _chips.Count; i++)
            {
                _chips[i].Release();
            }

            _flow.Add(_hint.rectTransform, true);
        }

        private static void SetText(Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static void SetSize(RectTransform rectTransform, float width, float height)
        {
            var size = new Vector2(width, height);
            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }
    }
}
