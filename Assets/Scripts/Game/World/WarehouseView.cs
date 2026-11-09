using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>창고 그림입니다. 오른쪽 격자에 칸별 보유·목표·빈 칸을, 그 아래에 칸을 넘친 자원을 보입니다.</summary>
    public sealed class WarehouseView : NodeView
    {
        [SerializeField] SpriteRenderer _pulse, _fill, _frame, _cfg;
        [SerializeField] PipGroup _pips;

        public override void Sync(Game g, ViewState v, float t)
        {
            var n = Node;
            Pulse(_pulse, n.Pulse);
            _fill.color = Palette.Ground;
            _frame.color = Palette.Ink;

            _pips.Begin();
            int usedF = 0, usedW = 0, total = n.SlotFood + n.SlotWeapon;
            int tgF = g.WarehouseTarget(n, Res.Food), tgW = g.WarehouseTarget(n, Res.Weapon);
            for (int i = 0; i < total; i++)
            {
                var k = i < n.SlotFood ? Res.Food : Res.Weapon;
                float cx = 23f + (i % 3) * 8f, cy = -9f + (i / 3) * 8f;
                int u = k == Res.Food ? usedF++ : usedW++;
                int have = n.Get(k), tg = k == Res.Food ? tgF : tgW;
                if (u < have) _pips.Slot(k, cx, cy, true, 3.1f);
                else if (u < tg) _pips.Request(k, cx, cy, 3.1f);
                else _pips.Slot(k, cx, cy, false, 3.1f);
            }
            // 칸을 줄여 칸보다 많이 든 초과분은 붉은 테두리로 격자 아래에 표시
            int overF = Mathf.Max(0, n.Food - n.SlotFood), overW = Mathf.Max(0, n.Weapon - n.SlotWeapon);
            int rowsUsed = Mathf.CeilToInt(total / 3f);
            for (int i = 0; i < overF + overW; i++)
            {
                var k = i < overF ? Res.Food : Res.Weapon;
                float cx = 23f + (i % 3) * 8f, cy = -9f + (rowsUsed + i / 3) * 8f + 3f;
                _pips.Slot(k, cx, cy, true, 3.1f);
                _pips.Over(k, cx, cy);
            }
            _pips.End();
            Cfg(_cfg, v);
        }
    }
}
