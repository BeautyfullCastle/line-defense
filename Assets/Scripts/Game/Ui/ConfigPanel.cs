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
        const string InkHex = "#23261F";
        const string DangerHex = "#D23E2C";
        const string MutedHex = "#6F7568";
        const float Pad = 14f;
        const float Cell = 18f;

        [SerializeField] IconSet _icons;
        [SerializeField] RectTransform _fort, _store;
        [SerializeField] Text _fortTitle, _fortInfo, _fortGold;
        [SerializeField] ConfigUpgradeRow _atkRow, _spdRow;
        [SerializeField] RectTransform _head, _stepRow, _cellBox;
        [SerializeField] Image _headColor;
        [SerializeField] Text _headTitle, _stepLabel, _stepValue;
        [SerializeField] UiButton _minus, _plus;
        [SerializeField] ConfigCell _cellPrefab;
        [SerializeField] Text _info0, _info1, _info2;
        [SerializeField] Button _close;

        readonly List<ConfigCell> _cells = new List<ConfigCell>();
        Game _g;
        ViewState _v;
        bool _open;
        float _canvasW = 1280f;
        float _topInset = 60f;
        float _timer;

        RectTransform Rt => (RectTransform)transform;

        /// <summary>설정창이 열려 있는지 여부입니다.</summary>
        public bool IsOpen => _open;

        void Awake()
        {
            _close.onClick.AddListener(Close);
            _atkRow.Button.Button.onClick.AddListener(() => Upgrade(true));
            _spdRow.Button.Button.onClick.AddListener(() => Upgrade(false));
            _minus.Button.onClick.AddListener(() => Step(-1));
            _plus.Button.onClick.AddListener(() => Step(1));
        }

        /// <summary>노드의 설정창을 엽니다.</summary>
        public void Open(Game g, ViewState v, Node n, float canvasW, float topInset)
        {
            _g = g; _v = v; _canvasW = canvasW; _topInset = topInset;
            v.CfgNode = n;
            Refresh();
        }

        /// <summary>설정창을 닫습니다.</summary>
        public void Close()
        {
            if (_v != null) _v.CfgNode = null;
            Hide();
        }

        /// <summary>창 크기와 머리말 높이가 바뀌었을 때 위치를 다시 잡습니다.</summary>
        public void Relayout(float canvasW, float topInset)
        {
            bool changed = !Mathf.Approximately(canvasW, _canvasW);
            _canvasW = canvasW; _topInset = topInset;
            if (!_open) return;
            if (changed) Refresh(); else PlaceCard();
        }

        /// <summary>0.5초마다 내용을 갱신하고, 노드가 사라지면 닫습니다.</summary>
        public void Tick(float dt)
        {
            if (!_open || _v == null) return;
            _timer += dt;
            if (_timer < 0.5f) return;
            _timer = 0f;
            if (_v.CfgNode == null || !_g.Nodes.Contains(_v.CfgNode)) Close();
            else Refresh();
        }

        void Hide()
        {
            _open = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void PlaceCard()
        {
            var p = new Vector2(Pad, -(_topInset + Pad));
            if (Rt.anchoredPosition != p) Rt.anchoredPosition = p;
        }

        void Refresh()
        {
            var n = _v.CfgNode;
            if (n == null) { Hide(); return; }
            _timer = 0f;
            float w = Mathf.Min(300f, _canvasW - 28f);
            float inner = w - Pad * 2f;
            // 숨겨진 상태에서는 글자 크기를 잴 수 없으므로 먼저 켬
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _open = true;

            bool fort = n.Type == NodeType.Fort;
            bool stable = n.Type == NodeType.Stable;
            Line line = stable ? _g.LineById(n.LineId) : null;
            bool store = !fort && !(stable && line == null);
            Show(_fort.gameObject, fort);
            Show(_store.gameObject, store);

            // 마굿간인데 노선이 없으면 빈 카드만 남음
            float h = Pad * 2f - 2f;
            if (fort) h = LayoutFort(n, inner);
            else if (store) h = LayoutStore(n, stable, line, inner);
            var size = new Vector2(w, h);
            if (Rt.sizeDelta != size) Rt.sizeDelta = size;
            PlaceCard();
        }

        static string R2(float v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

        float LayoutFort(Node n, float inner)
        {
            float y = Pad - 2f;
            y = PlaceText(_fortTitle, "요새 강화", y, inner - 24f, 8f);
            y = PlaceText(_fortInfo, $"보유 골드 <color={InkHex}><b>{HudView.Fmt(_g.Gold)}</b></color> · 사기 {Mathf.RoundToInt(n.Morale * 100f)}% · 지금 한 발 피해 {_g.FortDmg(n)}", y, inner, 8f);
            var b = _g.B;
            for (int o = 0; o < 2; o++)
            {
                bool attack = o == 0;
                int lv = attack ? n.AtkLv : n.SpdLv;
                int max = _g.UpgMax();
                bool atMax = lv >= max;
                int price = _g.UpgCost(lv);
                string d;
                if (atMax)
                {
                    d = attack ? $"공격력 {R2(b.FORT_ATK * (1f + b.ATK_UP_STEP * lv))} (최대)" : $"공격 간격 {R2(b.FORT_CD / (1f + b.SPD_UP_STEP * lv))}초 (최대)";
                }
                else if (attack)
                {
                    d = $"<color={InkHex}>▲ 공격력 {R2(Atk(b, lv))} → {R2(Atk(b, lv + 1))}</color>\n<color={DangerHex}>▼ 식량 1개 소모 {R2(FoodEvery(b, lv))}초 → {R2(FoodEvery(b, lv + 1))}초마다</color>";
                }
                else
                {
                    d = $"<color={InkHex}>▲ 공격 간격 {R2(Cd(b, lv))}초 → {R2(Cd(b, lv + 1))}초</color>\n<color={DangerHex}>▼ 계속 쏠 때 무기 소모 분당 {R2(60f / Cd(b, lv) / Spw(b, lv))} → {R2(60f / Cd(b, lv + 1) / Spw(b, lv + 1))}개</color>";
                }
                var row = attack ? _atkRow : _spdRow;
                float rh = row.Show(attack ? "공격력" : "공격 속도", $"Lv {lv} / {max}", atMax ? "최대" : $"강화 · {price}골드", !atMax && _g.Gold >= price, d, inner);
                SetPos(row.Rt, Pad, y);
                y += rh;
            }
            bool hint = _g.Gold < _g.UpgCost(0);
            Show(_fortGold.gameObject, hint);
            if (hint) y = PlaceText(_fortGold, $"몬스터 1마리 처치 = {HudView.Fmt(b.GOLD_PER_KILL)}골드", y, inner, 8f);
            return y + Pad;
        }

        static float Atk(Balance b, int lv) => b.FORT_ATK * (1f + b.ATK_UP_STEP * lv);
        static float FoodEvery(Balance b, int lv) => b.FORT_FOOD_EVERY / (1f + b.ATK_FOOD_STEP * lv);
        static float Cd(Balance b, int lv) => b.FORT_CD / (1f + b.SPD_UP_STEP * lv);
        static float Spw(Balance b, int lv) => b.SHOTS_PER_WEAPON / (1f + b.SPD_AMMO_STEP * lv);

        float LayoutStore(Node n, bool stable, Line line, float inner)
        {
            int cap = stable ? _g.CartCap() : _g.WhCap();
            int food = stable ? line.CartFoodSlots : n.SlotFood;
            int weapon = cap - food;
            float y = Pad - 2f;

            SetBox(_head, Pad, y, inner - 24f, 22f);
            y += 22f + 4f;
            Show(_headColor.gameObject, stable);
            if (stable)
            {
                var col = Palette.LineColor(line.ColorIndex);
                if (_headColor.color != col) _headColor.color = col;
            }
            SetText(_headTitle, stable ? "마굿간 · 마차 칸 설정" : "창고 · 보관 칸 설정");
            SetX(_headTitle.rectTransform, stable ? 20f : 0f, 240f);

            SetBox(_stepRow, Pad, y, inner, 30f);
            y += 30f + 8f;
            SetText(_stepLabel, $"식량 칸 <color={MutedHex}>/ 무기 칸 {weapon}</color>");
            SetX(_stepLabel.rectTransform, 0f, inner - 118f);
            SetX(_minus.Rt, inner - 108f, 30f);
            SetText(_stepValue, food.ToString());
            SetX(_stepValue.rectTransform, inner - 74f, 36f);
            SetX(_plus.Rt, inner - 32f, 30f);
            _minus.SetInteractable(food > 0);
            _plus.SetInteractable(food < cap);

            int haveF = stable ? 0 : n.Food, haveW = stable ? 0 : n.Weapon;
            y = LayoutCells(food, weapon, haveF, haveW, inner, y);

            if (stable)
            {
                int pend = 0;
                foreach (var c in line.Carts) if (c.SlotFood != line.CartFoodSlots) pend++;
                y = PlaceText(_info0, "마차가 이 마굿간을 지날 때 적용됩니다. 칸을 넘는 자원은 마굿간에 남고, 그 자원 칸이 비면 다시 싣습니다." + (pend > 0 ? $" 적용 대기 중인 마차 {pend}대." : string.Empty), y, inner, 8f);
                bool stored = n.Food + n.Weapon > 0;
                Show(_info1.gameObject, stored);
                if (stored) y = PlaceText(_info1, $"마굿간 보관: 식량 {n.Food} · 무기 {n.Weapon}", y, inner, 8f);
                Show(_info2.gameObject, false);
            }
            else
            {
                y = PlaceText(_info0, $"보관 식량 <color={InkHex}><b>{n.Food}</b></color> · 무기 <color={InkHex}><b>{n.Weapon}</b></color>  |  목표 식량 <color={InkHex}><b>{_g.WarehouseTarget(n, Res.Food)}</b></color> · 무기 <color={InkHex}><b>{_g.WarehouseTarget(n, Res.Weapon)}</b></color>", y, inner, 8f);
                Show(_info1.gameObject, true);
                y = PlaceText(_info1, "바로 적용됩니다. 칸을 넘는 자원은 마차가 먼저 실어 갑니다. 목표 = 이 창고에서 출발하는 구간 요새들의 빈 칸 + 비축분. 목표보다 여유 있는 창고는 모자란 창고로 짐을 보냅니다.", y, inner, 8f);
                int ov = Mathf.Max(0, n.Food - n.SlotFood) + Mathf.Max(0, n.Weapon - n.SlotWeapon);
                Show(_info2.gameObject, ov > 0);
                if (ov > 0) y = PlaceText(_info2, $"칸을 넘은 초과분 {ov}개 · 다 실려 나갈 때까지 그 자원은 내리지 않습니다.", y, inner, 8f);
            }
            return y + Pad;
        }

        void Upgrade(bool attack)
        {
            var n = _v?.CfgNode;
            if (n == null || _g == null) return;
            _g.UpgradeFort(n, attack);
            Refresh();
        }

        void Step(int delta)
        {
            var n = _v?.CfgNode;
            if (n == null || _g == null) return;
            bool stable = n.Type == NodeType.Stable;
            Line line = stable ? _g.LineById(n.LineId) : null;
            if (stable && line == null) return;
            int food = stable ? line.CartFoodSlots : n.SlotFood;
            Apply(stable, line, n, food + delta);
        }

        void Apply(bool stable, Line line, Node n, int food)
        {
            int cap = stable ? _g.CartCap() : _g.WhCap();
            food = Mathf.Clamp(food, 0, cap);
            if (stable) _g.SetLineFoodSlots(line, food); else _g.SetWarehouseFoodSlots(n, food);
            Refresh();
        }

        float LayoutCells(int sf, int sw, int haveF, int haveW, float inner, float y)
        {
            int cap = sf + sw, cols = Mathf.Max(1, Mathf.CeilToInt(cap / 2f));
            float w = cols * Cell + 8f, h = 2f * Cell + 8f;
            SetBox(_cellBox, (inner - w) * 0.5f + Pad, y, w, h);
            while (_cells.Count < cap) _cells.Add(Instantiate(_cellPrefab, _cellBox, false));
            int uf = 0, uw = 0;
            for (int i = 0; i < cap; i++)
            {
                bool food = i < sf;
                bool full = food ? uf++ < haveF : uw++ < haveW;
                var pos = new Vector2(4f + Cell / 2f + (i % cols) * Cell, -(4f + Cell / 2f + (i / cols) * Cell));
                string key = (food ? "circle" : "tri") + (full ? string.Empty : "Ring");
                var col = food ? Palette.Food : Palette.Weapon;
                _cells[i].Show(pos, full ? 14.9f : 12.5f, _icons.Get(key), full ? col : Palette.WithAlpha(col, 0.7f));
            }
            for (int i = cap; i < _cells.Count; i++) _cells[i].Hide();
            return y + h + 8f;
        }

        // 글자를 넣고 폭에 맞는 높이로 놓은 뒤 다음 y를 돌려줌. 높이를 재려면 폭을 먼저 맞춰야 함
        float PlaceText(Text t, string s, float y, float w, float gap)
        {
            var rt = t.rectTransform;
            SetText(t, s);
            if (!Mathf.Approximately(rt.sizeDelta.x, w)) rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            float h = Mathf.Ceil(t.preferredHeight);
            SetBox(rt, Pad, y, w, h);
            return y + h + gap;
        }

        static void SetPos(RectTransform rt, float x, float y)
        {
            var p = new Vector2(x, -y);
            if (rt.anchoredPosition != p) rt.anchoredPosition = p;
        }

        static void SetBox(RectTransform rt, float x, float y, float w, float h)
        {
            SetPos(rt, x, y);
            var s = new Vector2(w, h);
            if (rt.sizeDelta != s) rt.sizeDelta = s;
        }

        // 세로 가운데 기준 요소의 가로 위치와 폭만 바꿈
        static void SetX(RectTransform rt, float x, float w)
        {
            var p = new Vector2(x, 0f);
            if (rt.anchoredPosition != p) rt.anchoredPosition = p;
            var s = new Vector2(w, rt.sizeDelta.y);
            if (rt.sizeDelta != s) rt.sizeDelta = s;
        }

        static void SetText(Text t, string s)
        {
            if (t.text != s) t.text = s;
        }

        static void Show(GameObject go, bool on)
        {
            if (go.activeSelf != on) go.SetActive(on);
        }
    }
}
