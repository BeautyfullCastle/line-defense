using System;
using System.Collections.Generic;
using System.Globalization;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>마굿간, 창고, 요새를 눌렀을 때 왼쪽 위에 뜨는 설정창입니다. 프리팹 Prefabs/UI/Config에 붙고 열고 닫을 때 켜고 끕니다.</summary>
    public sealed class ConfigPanel : MonoBehaviour
    {
        private const string InkHex = "#23261F";
        private const string DangerHex = "#D23E2C";
        private const string MutedHex = "#6F7568";
        private const float Padding = 14f;
        private const float Cell = 18f;

        [SerializeField] private IconSet _icons;
        [SerializeField] private RectTransform _fort, _store;
        [SerializeField] private Text _fortTitle, _fortInfo, _fortGold;
        [SerializeField] private ConfigUpgradeRow _attackRow, _speedRow;
        [SerializeField] private RectTransform _head, _stepRow, _cellBox;
        [SerializeField] private Image _headColor;
        [SerializeField] private Text _headTitle, _stepLabel, _stepValue;
        [SerializeField] private UiButton _minus, _plus;
        [SerializeField] private ConfigCell _cellPrefab;
        [SerializeField] private Text _info0, _info1, _info2;
        [SerializeField] private Button _close;

        private readonly List<ConfigCell> _cells = new List<ConfigCell>();
        private Game _game;
        private ViewState _view;
        private bool _open;
        private float _canvasWidth = 1280f;
        private float _topInset = 60f;
        private float _timer;

        private RectTransform RectTransform => (RectTransform)transform;

        /// <summary>설정창이 열려 있는지 여부입니다.</summary>
        public bool IsOpen => _open;

        private void Awake()
        {
            _close.onClick.AddListener(Close);
            _attackRow.Button.Button.onClick.AddListener(() => Upgrade(true));
            _speedRow.Button.Button.onClick.AddListener(() => Upgrade(false));
            _minus.Button.onClick.AddListener(() => Step(-1));
            _plus.Button.onClick.AddListener(() => Step(1));
        }

        /// <summary>노드의 설정창을 엽니다.</summary>
        public void Open(Game game, ViewState view, Node node, float canvasWidth, float topInset)
        {
            _game = game;
            _view = view;
            _canvasWidth = canvasWidth;
            _topInset = topInset;
            view.ConfigNode = node;
            Refresh();
        }

        /// <summary>설정창을 닫습니다.</summary>
        public void Close()
        {
            if (_view != null)
            {
                _view.ConfigNode = null;
            }

            Hide();
        }

        /// <summary>창 크기와 머리말 높이가 바뀌었을 때 위치를 다시 잡습니다.</summary>
        public void Relayout(float canvasWidth, float topInset)
        {
            bool changed = !Mathf.Approximately(canvasWidth, _canvasWidth);
            _canvasWidth = canvasWidth;
            _topInset = topInset;
            if (!_open)
            {
                return;
            }

            if (changed)
            {
                Refresh();
            }
            else
            {
                PlaceCard();
            }
        }

        /// <summary>0.5초마다 내용을 갱신하고, 노드가 사라지면 닫습니다.</summary>
        public void Tick(float dt)
        {
            if (!_open || _view == null)
            {
                return;
            }

            _timer += dt;
            if (_timer < 0.5f)
            {
                return;
            }

            _timer = 0f;
            if (_view.ConfigNode == null || !_game.Nodes.Contains(_view.ConfigNode))
            {
                Close();
            }
            else
            {
                Refresh();
            }
        }

        private void Hide()
        {
            _open = false;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void PlaceCard()
        {
            var position = new Vector2(Padding, -(_topInset + Padding));
            if (RectTransform.anchoredPosition != position)
            {
                RectTransform.anchoredPosition = position;
            }
        }

        private void Refresh()
        {
            var node = _view.ConfigNode;
            if (node == null)
            {
                Hide();
                return;
            }
            _timer = 0f;
            float cardWidth = Mathf.Min(300f, _canvasWidth - 28f);
            float inner = cardWidth - Padding * 2f;
            // 숨겨진 상태에서는 글자 크기를 잴 수 없으므로 먼저 켬
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            _open = true;

            bool fort = node.Type == NodeType.Fort;
            bool stable = node.Type == NodeType.Stable;
            Line line = stable ? _game.LineById(node.LineId) : null;
            bool store = !fort && !(stable && line == null);
            Show(_fort.gameObject, fort);
            Show(_store.gameObject, store);

            // 마굿간인데 노선이 없으면 빈 카드만 남음
            float cardHeight = Padding * 2f - 2f;
            if (fort)
            {
                cardHeight = LayoutFort(node, inner);
            }
            else if (store)
            {
                cardHeight = LayoutStore(node, stable, line, inner);
            }

            var size = new Vector2(cardWidth, cardHeight);
            if (RectTransform.sizeDelta != size)
            {
                RectTransform.sizeDelta = size;
            }

            PlaceCard();
        }

        private static string RoundTo2(float value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

        private float LayoutFort(Node node, float inner)
        {
            float y = Padding - 2f;
            y = PlaceText(_fortTitle, "요새 강화", y, inner - 24f, 8f);
            y = PlaceText(_fortInfo, $"보유 골드 <color={InkHex}><b>{HudView.FormatNumber(_game.Gold)}</b></color> · 사기 {Mathf.RoundToInt(node.Morale * 100f)}% · 지금 한 발 피해 {_game.FortDamage(node)}", y, inner, 8f);
            var balance = _game.Balance;
            for (int upgradeIndex = 0; upgradeIndex < 2; upgradeIndex++)
            {
                bool attack = upgradeIndex == 0;
                int level = attack ? node.AttackLevel : node.SpeedLevel;
                int max = _game.MaxUpgradeLevel();
                bool atMax = level >= max;
                int price = _game.UpgradeCost(level);
                string description;
                if (atMax)
                {
                    description = attack ? $"공격력 {RoundTo2(balance.FORT_ATK * (1f + balance.ATK_UP_STEP * level))} (최대)" : $"공격 간격 {RoundTo2(balance.FORT_CD / (1f + balance.SPD_UP_STEP * level))}초 (최대)";
                }
                else if (attack)
                {
                    description = $"<color={InkHex}>▲ 공격력 {RoundTo2(AttackAt(balance, level))} → {RoundTo2(AttackAt(balance, level + 1))}</color>\n<color={DangerHex}>▼ 식량 1개 소모 {RoundTo2(FoodIntervalAt(balance, level))}초 → {RoundTo2(FoodIntervalAt(balance, level + 1))}초마다</color>";
                }
                else
                {
                    description = $"<color={InkHex}>▲ 공격 간격 {RoundTo2(IntervalAt(balance, level))}초 → {RoundTo2(IntervalAt(balance, level + 1))}초</color>\n<color={DangerHex}>▼ 계속 쏠 때 무기 소모 분당 {RoundTo2(60f / IntervalAt(balance, level) / ShotsPerWeaponAt(balance, level))} → {RoundTo2(60f / IntervalAt(balance, level + 1) / ShotsPerWeaponAt(balance, level + 1))}개</color>";
                }
                var row = attack ? _attackRow : _speedRow;
                float rowHeight = row.Show(attack ? "공격력" : "공격 속도", $"Lv {level} / {max}", atMax ? "최대" : $"강화 · {price}골드", !atMax && _game.Gold >= price, description, inner);
                SetPosition(row.RectTransform, Padding, y);
                y += rowHeight;
            }
            bool hint = _game.Gold < _game.UpgradeCost(0);
            Show(_fortGold.gameObject, hint);
            if (hint)
            {
                y = PlaceText(_fortGold, $"몬스터 1마리 처치 = {HudView.FormatNumber(balance.GOLD_PER_KILL)}골드", y, inner, 8f);
            }

            return y + Padding;
        }

        private static float AttackAt(Balance balance, int level) => balance.FORT_ATK * (1f + balance.ATK_UP_STEP * level);
        private static float FoodIntervalAt(Balance balance, int level) => balance.FORT_FOOD_EVERY / (1f + balance.ATK_FOOD_STEP * level);
        private static float IntervalAt(Balance balance, int level) => balance.FORT_CD / (1f + balance.SPD_UP_STEP * level);
        private static float ShotsPerWeaponAt(Balance balance, int level) => balance.SHOTS_PER_WEAPON / (1f + balance.SPD_AMMO_STEP * level);

        private float LayoutStore(Node node, bool stable, Line line, float inner)
        {
            int capacity = stable ? _game.CartCapacity() : _game.WarehouseCapacity();
            int food = stable ? line.CartFoodSlots : node.SlotFood;
            int weapon = capacity - food;
            float y = Padding - 2f;

            SetBox(_head, Padding, y, inner - 24f, 22f);
            y += 22f + 4f;
            Show(_headColor.gameObject, stable);
            if (stable)
            {
                var color = Palette.LineColor(line.ColorIndex);
                if (_headColor.color != color)
                {
                    _headColor.color = color;
                }
            }
            SetText(_headTitle, stable ? "마굿간 · 마차 칸 설정" : "창고 · 보관 칸 설정");
            SetX(_headTitle.rectTransform, stable ? 20f : 0f, 240f);

            SetBox(_stepRow, Padding, y, inner, 30f);
            y += 30f + 8f;
            SetText(_stepLabel, $"식량 칸 <color={MutedHex}>/ 무기 칸 {weapon}</color>");
            SetX(_stepLabel.rectTransform, 0f, inner - 118f);
            SetX(_minus.RectTransform, inner - 108f, 30f);
            SetText(_stepValue, food.ToString());
            SetX(_stepValue.rectTransform, inner - 74f, 36f);
            SetX(_plus.RectTransform, inner - 32f, 30f);
            _minus.SetInteractable(food > 0);
            _plus.SetInteractable(food < capacity);

            int heldFood = stable ? 0 : node.Food, heldWeapons = stable ? 0 : node.Weapon;
            y = LayoutCells(food, weapon, heldFood, heldWeapons, inner, y);

            if (stable)
            {
                int pend = 0;
                foreach (var cart in line.Carts)
                {
                    if (cart.SlotFood != line.CartFoodSlots)
                    {
                        pend++;
                    }
                }

                y = PlaceText(_info0, "마차가 이 마굿간을 지날 때 적용됩니다. 칸을 넘는 자원은 마굿간에 남고, 그 자원 칸이 비면 다시 싣습니다." + (pend > 0 ? $" 적용 대기 중인 마차 {pend}대." : string.Empty), y, inner, 8f);
                bool stored = node.Food + node.Weapon > 0;
                Show(_info1.gameObject, stored);
                if (stored)
                {
                    y = PlaceText(_info1, $"마굿간 보관: 식량 {node.Food} · 무기 {node.Weapon}", y, inner, 8f);
                }

                Show(_info2.gameObject, false);
            }
            else
            {
                y = PlaceText(_info0, $"보관 식량 <color={InkHex}><b>{node.Food}</b></color> · 무기 <color={InkHex}><b>{node.Weapon}</b></color>  |  목표 식량 <color={InkHex}><b>{_game.WarehouseTarget(node, Res.Food)}</b></color> · 무기 <color={InkHex}><b>{_game.WarehouseTarget(node, Res.Weapon)}</b></color>", y, inner, 8f);
                Show(_info1.gameObject, true);
                y = PlaceText(_info1, "바로 적용됩니다. 칸을 넘는 자원은 마차가 먼저 실어 갑니다. 목표 = 이 창고에서 출발하는 구간 요새들의 빈 칸 + 비축분. 목표보다 여유 있는 창고는 모자란 창고로 짐을 보냅니다.", y, inner, 8f);
                int overflowCount = Mathf.Max(0, node.Food - node.SlotFood) + Mathf.Max(0, node.Weapon - node.SlotWeapon);
                Show(_info2.gameObject, overflowCount > 0);
                if (overflowCount > 0)
                {
                    y = PlaceText(_info2, $"칸을 넘은 초과분 {overflowCount}개 · 다 실려 나갈 때까지 그 자원은 내리지 않습니다.", y, inner, 8f);
                }
            }
            return y + Padding;
        }

        private void Upgrade(bool attack)
        {
            var node = _view?.ConfigNode;
            if (node == null || _game == null)
            {
                return;
            }

            _game.UpgradeFort(node, attack);
            Refresh();
        }

        private void Step(int delta)
        {
            var node = _view?.ConfigNode;
            if (node == null || _game == null)
            {
                return;
            }

            bool stable = node.Type == NodeType.Stable;
            Line line = stable ? _game.LineById(node.LineId) : null;
            if (stable && line == null)
            {
                return;
            }

            int food = stable ? line.CartFoodSlots : node.SlotFood;
            Apply(stable, line, node, food + delta);
        }

        private void Apply(bool stable, Line line, Node node, int food)
        {
            int capacity = stable ? _game.CartCapacity() : _game.WarehouseCapacity();
            food = Mathf.Clamp(food, 0, capacity);
            if (stable)
            {
                _game.SetLineFoodSlots(line, food);
            }
            else
            {
                _game.SetWarehouseFoodSlots(node, food);
            }

            Refresh();
        }

        private float LayoutCells(int foodSlotCount, int weaponSlotCount, int heldFood, int heldWeapons, float inner, float y)
        {
            int totalSlots = foodSlotCount + weaponSlotCount, cols = Mathf.Max(1, Mathf.CeilToInt(totalSlots / 2f));
            float boxWidth = cols * Cell + 8f, boxHeight = 2f * Cell + 8f;
            SetBox(_cellBox, (inner - boxWidth) * 0.5f + Padding, y, boxWidth, boxHeight);
            while (_cells.Count < totalSlots)
            {
                _cells.Add(Instantiate(_cellPrefab, _cellBox, false));
            }

            int usedFoodSlots = 0, usedWeaponSlots = 0;
            for (int i = 0; i < totalSlots; i++)
            {
                bool food = i < foodSlotCount;
                bool full = food ? usedFoodSlots++ < heldFood : usedWeaponSlots++ < heldWeapons;
                var position = new Vector2(4f + Cell / 2f + (i % cols) * Cell, -(4f + Cell / 2f + (i / cols) * Cell));
                string key = (food ? "circle" : "tri") + (full ? string.Empty : "Ring");
                var color = food ? Palette.Food : Palette.Weapon;
                _cells[i].Show(position, full ? 14.9f : 12.5f, _icons.Get(key), full ? color : Palette.WithAlpha(color, 0.7f));
            }
            for (int i = totalSlots; i < _cells.Count; i++)
            {
                _cells[i].Hide();
            }

            return y + boxHeight + 8f;
        }

        // 글자를 넣고 폭에 맞는 높이로 놓은 뒤 다음 y를 돌려줌. 높이를 재려면 폭을 먼저 맞춰야 함
        private float PlaceText(Text label, string text, float y, float width, float gap)
        {
            var rectTransform = label.rectTransform;
            SetText(label, text);
            if (!Mathf.Approximately(rectTransform.sizeDelta.x, width))
            {
                rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);
            }

            float height = Mathf.Ceil(label.preferredHeight);
            SetBox(rectTransform, Padding, y, width, height);
            return y + height + gap;
        }

        private static void SetPosition(RectTransform rectTransform, float x, float y)
        {
            var position = new Vector2(x, -y);
            if (rectTransform.anchoredPosition != position)
            {
                rectTransform.anchoredPosition = position;
            }
        }

        private static void SetBox(RectTransform rectTransform, float x, float y, float width, float height)
        {
            SetPosition(rectTransform, x, y);
            var size = new Vector2(width, height);
            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }

        // 세로 가운데 기준 요소의 가로 위치와 폭만 바꿈
        private static void SetX(RectTransform rectTransform, float x, float width)
        {
            var position = new Vector2(x, 0f);
            if (rectTransform.anchoredPosition != position)
            {
                rectTransform.anchoredPosition = position;
            }

            var size = new Vector2(width, rectTransform.sizeDelta.y);
            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }

        private static void SetText(Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static void Show(GameObject targetObject, bool on)
        {
            if (targetObject.activeSelf != on)
            {
                targetObject.SetActive(on);
            }
        }
    }
}
