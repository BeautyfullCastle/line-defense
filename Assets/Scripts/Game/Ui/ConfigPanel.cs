using System;
using System.Globalization;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>마굿간, 창고, 요새를 눌렀을 때 왼쪽 위에 뜨는 설정창입니다.</summary>
    public sealed class ConfigPanel
    {
        const string InkHex = "#23261F";
        const string DangerHex = "#D23E2C";
        const string MutedHex = "#6F7568";

        readonly Transform _parent;
        RectTransform _card;
        Game _g;
        ViewState _v;
        float _canvasW = 1280f;
        float _topInset = 60f;
        float _timer;

        /// <summary>설정창을 붙일 부모를 지정합니다.</summary>
        public ConfigPanel(Transform parent)
        {
            _parent = parent;
        }

        /// <summary>설정창이 열려 있는지 여부입니다.</summary>
        public bool IsOpen => _card != null;

        /// <summary>노드의 설정창을 엽니다.</summary>
        public void Open(Game g, ViewState v, Node n, float canvasW, float topInset)
        {
            _g = g; _v = v; _canvasW = canvasW; _topInset = topInset;
            v.CfgNode = n;
            Rebuild();
        }

        /// <summary>설정창을 닫습니다.</summary>
        public void Close()
        {
            if (_v != null) _v.CfgNode = null;
            Clear();
        }

        /// <summary>창 크기와 머리말 높이가 바뀌었을 때 위치를 다시 잡습니다.</summary>
        public void Relayout(float canvasW, float topInset)
        {
            bool changed = !Mathf.Approximately(canvasW, _canvasW);
            _canvasW = canvasW; _topInset = topInset;
            if (_card == null) return;
            if (changed) Rebuild(); else PlaceCard();
        }

        /// <summary>0.5초마다 내용을 새로 그리고, 노드가 사라지면 닫습니다.</summary>
        public void Tick(float dt)
        {
            if (_card == null || _v == null) return;
            _timer += dt;
            if (_timer < 0.5f) return;
            _timer = 0f;
            if (_v.CfgNode == null || !_g.Nodes.Contains(_v.CfgNode)) Close();
            else Rebuild();
        }

        void Clear()
        {
            if (_card != null) UnityEngine.Object.Destroy(_card.gameObject);
            _card = null;
        }

        void PlaceCard()
        {
            if (_card == null) return;
            _card.anchorMin = _card.anchorMax = new Vector2(0, 1);
            _card.pivot = new Vector2(0, 1);
            _card.anchoredPosition = new Vector2(14f, -(_topInset + 14f));
        }

        void Rebuild()
        {
            var n = _v.CfgNode;
            Clear();
            _timer = 0f;
            if (n == null) return;
            float w = Mathf.Min(300f, _canvasW - 28f);
            _card = UiFactory.Card(_parent, "Config", Palette.Panel, Palette.Line);
            var stack = new VStack(_card, w, 14f);
            stack.Gap(-2f);
            if (n.Type == NodeType.Fort) BuildFort(stack, n);
            else BuildStore(stack, n);
            _card.sizeDelta = new Vector2(w, stack.Height);
            AddCloseButton(w);
            PlaceCard();
        }

        void AddCloseButton(float w)
        {
            var rt = UiFactory.Rect(_card, "Close");
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            rt.sizeDelta = new Vector2(28f, 28f);
            var img = UiFactory.Fill(rt, new Color(1, 1, 1, 0.001f), false, true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Close);
            var t = UiFactory.Label(rt, "X", 15, Palette.Muted, true, TextAnchor.MiddleCenter);
            UiFactory.Stretch(t.rectTransform);
        }

        static string R2(float v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

        void BuildFort(VStack s, Node n)
        {
            var title = s.AddText("요새 강화", 15, Palette.Ink, true);
            title.rectTransform.sizeDelta = new Vector2(s.InnerWidth - 24f, title.rectTransform.sizeDelta.y);
            s.AddText($"보유 골드 <color={InkHex}><b>{HudView.Fmt(_g.Gold)}</b></color> · 사기 {Mathf.RoundToInt(n.Morale * 100f)}% · 지금 한 발 피해 {_g.FortDmg(n)}", 12, Palette.Muted);
            var b = _g.B;
            for (int o = 0; o < 2; o++)
            {
                bool attack = o == 0;
                int lv = attack ? n.AtkLv : n.SpdLv;
                int max = _g.UpgMax();
                bool atMax = lv >= max;
                int price = _g.UpgCost(lv);
                s.Divider(8f);
                var row = s.AddRect("Row", 28f, -1f, 4f);
                var name = UiFactory.Label(row, attack ? "공격력" : "공격 속도", 13, Palette.Ink, true);
                SetLeft(name.rectTransform, 0f, 70f);
                var lvl = UiFactory.Label(row, $"Lv {lv} / {max}", 12, Palette.Muted);
                SetLeft(lvl.rectTransform, 74f, 70f);
                string btnText = atMax ? "최대" : $"강화 · {price}골드";
                float bw = UiFactory.TextWidth(btnText, 12, true) + 22f;
                bool isAttack = attack;
                var btn = UiFactory.Button(row, btnText, () => { _g.UpgradeFort(n, isAttack); Rebuild(); }, bw, 26f, 12);
                btn.Rt.anchorMin = btn.Rt.anchorMax = new Vector2(1, 0.5f);
                btn.Rt.pivot = new Vector2(1, 0.5f);
                btn.Rt.anchoredPosition = Vector2.zero;
                btn.SetInteractable(!atMax && _g.Gold >= price);
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
                s.AddText(d, 12, Palette.Muted, false, TextAnchor.UpperLeft, 2f);
            }
            if (_g.Gold < _g.UpgCost(0))
                s.AddText($"몬스터 1마리 처치 = {HudView.Fmt(b.GOLD_PER_KILL)}골드", 12, Palette.Muted);
        }

        static float Atk(Balance b, int lv) => b.FORT_ATK * (1f + b.ATK_UP_STEP * lv);
        static float FoodEvery(Balance b, int lv) => b.FORT_FOOD_EVERY / (1f + b.ATK_FOOD_STEP * lv);
        static float Cd(Balance b, int lv) => b.FORT_CD / (1f + b.SPD_UP_STEP * lv);
        static float Spw(Balance b, int lv) => b.SHOTS_PER_WEAPON / (1f + b.SPD_AMMO_STEP * lv);

        static void SetLeft(RectTransform rt, float x, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(w, 26f);
        }

        void BuildStore(VStack s, Node n)
        {
            bool stable = n.Type == NodeType.Stable;
            Line line = stable ? _g.LineById(n.LineId) : null;
            if (stable && line == null) return;
            int cap = stable ? _g.CartCap() : _g.WhCap();
            int food = stable ? line.CartFoodSlots : n.SlotFood;
            int weapon = cap - food;

            var head = s.AddRect("Head", 22f, s.InnerWidth - 24f, 4f);
            float tx = 0f;
            if (stable)
            {
                var sq = UiFactory.Rect(head, "Color");
                SetLeft(sq, 0f, 12f);
                sq.sizeDelta = new Vector2(12f, 12f);
                UiFactory.Fill(sq, Palette.LineColor(line.ColorIndex), true, false);
                tx = 20f;
            }
            var title = UiFactory.Label(head, stable ? "마굿간 · 마차 칸 설정" : "창고 · 보관 칸 설정", 15, Palette.Ink, true);
            SetLeft(title.rectTransform, tx, 240f);

            var row = s.AddRect("Row", 30f, -1f, 8f);
            var lab = UiFactory.Label(row, $"식량 칸 <color={MutedHex}>/ 무기 칸 {weapon}</color>", 13, Palette.Ink);
            SetLeft(lab.rectTransform, 0f, s.InnerWidth - 118f);
            var minus = StepButton(row, "-", s.InnerWidth - 108f);
            var val = UiFactory.Label(row, food.ToString(), 16, Palette.Ink, true, TextAnchor.MiddleCenter);
            SetLeft(val.rectTransform, s.InnerWidth - 74f, 36f);
            var plus = StepButton(row, "+", s.InnerWidth - 32f);
            minus.SetInteractable(food > 0);
            plus.SetInteractable(food < cap);
            minus.Button.onClick.AddListener(() => Apply(stable, line, n, food - 1));
            plus.Button.onClick.AddListener(() => Apply(stable, line, n, food + 1));

            int haveF = stable ? 0 : n.Food, haveW = stable ? 0 : n.Weapon;
            MiniCells(s, food, weapon, haveF, haveW);

            if (stable)
            {
                int pend = 0;
                foreach (var c in line.Carts) if (c.SlotFood != line.CartFoodSlots) pend++;
                s.AddText("마차가 이 마굿간을 지날 때 적용됩니다. 칸을 넘는 자원은 마굿간에 남고, 그 자원 칸이 비면 다시 싣습니다." + (pend > 0 ? $" 적용 대기 중인 마차 {pend}대." : string.Empty), 12, Palette.Muted);
                if (n.Food + n.Weapon > 0) s.AddText($"마굿간 보관: 식량 {n.Food} · 무기 {n.Weapon}", 12, Palette.Muted);
            }
            else
            {
                s.AddText($"보관 식량 <color={InkHex}><b>{n.Food}</b></color> · 무기 <color={InkHex}><b>{n.Weapon}</b></color>  |  목표 식량 <color={InkHex}><b>{_g.WarehouseTarget(n, Res.Food)}</b></color> · 무기 <color={InkHex}><b>{_g.WarehouseTarget(n, Res.Weapon)}</b></color>", 12, Palette.Muted);
                s.AddText("바로 적용됩니다. 칸을 넘는 자원은 마차가 먼저 실어 갑니다. 목표 = 이 창고에서 출발하는 구간 요새들의 빈 칸 + 비축분. 목표보다 여유 있는 창고는 모자란 창고로 짐을 보냅니다.", 12, Palette.Muted);
                int ov = Mathf.Max(0, n.Food - n.SlotFood) + Mathf.Max(0, n.Weapon - n.SlotWeapon);
                if (ov > 0) s.AddText($"칸을 넘은 초과분 {ov}개 · 다 실려 나갈 때까지 그 자원은 내리지 않습니다.", 12, Palette.Danger, true);
            }
        }

        void Apply(bool stable, Line line, Node n, int food)
        {
            int cap = stable ? _g.CartCap() : _g.WhCap();
            food = Mathf.Clamp(food, 0, cap);
            if (stable) _g.SetLineFoodSlots(line, food); else _g.SetWarehouseFoodSlots(n, food);
            Rebuild();
        }

        static UiButton StepButton(RectTransform row, string label, float x)
        {
            var b = UiFactory.Button(row, label, null, 30f, 30f, 16);
            b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0, 0.5f);
            b.Rt.pivot = new Vector2(0, 0.5f);
            b.Rt.anchoredPosition = new Vector2(x, 0f);
            return b;
        }

        void MiniCells(VStack s, int sf, int sw, int haveF, int haveW)
        {
            const float cell = 18f;
            int cap = sf + sw, cols = Mathf.Max(1, Mathf.CeilToInt(cap / 2f));
            float w = cols * cell + 8f, h = 2f * cell + 8f;
            var box = s.AddRect("Cells", h, w, 8f);
            box.anchoredPosition = new Vector2((s.InnerWidth - w) * 0.5f + 14f, box.anchoredPosition.y);
            UiFactory.Fill(box, Palette.Line, true, false);
            int uf = 0, uw = 0;
            for (int i = 0; i < cap; i++)
            {
                bool food = i < sf;
                bool full = food ? uf++ < haveF : uw++ < haveW;
                var rt = UiFactory.Rect(box, "Cell");
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(4f + cell / 2f + (i % cols) * cell, -(4f + cell / 2f + (i / cols) * cell));
                float size = full ? 14.9f : 12.5f;
                rt.sizeDelta = new Vector2(size, size);
                var img = rt.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                string key = (food ? "circle" : "tri") + (full ? string.Empty : "Ring");
                img.sprite = IconFactory.Get(key);
                var col = food ? Palette.Food : Palette.Weapon;
                img.color = full ? col : Palette.WithAlpha(col, 0.7f);
            }
        }
    }
}
