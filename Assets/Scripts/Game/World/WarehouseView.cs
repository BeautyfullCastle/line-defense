using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>창고 그림입니다. 오른쪽 격자에 칸별 보유·목표·빈 칸을, 그 아래에 칸을 넘친 자원을 보입니다.</summary>
    public sealed class WarehouseView : NodeView
    {
        [SerializeField] private SpriteRenderer _pulse, _fill, _frame, _configRing;
        [SerializeField] private PipGroup _pips;

        public override void Sync(Game game, ViewState view, float t)
        {
            var node = Node;
            Pulse(_pulse, node.Pulse);
            _fill.color = Palette.Ground;
            _frame.color = Palette.Ink;

            _pips.Begin();
            int usedFoodSlots = 0, usedWeaponSlots = 0, total = node.SlotFood + node.SlotWeapon;
            int foodTarget = game.WarehouseTarget(node, Res.Food), weaponTarget = game.WarehouseTarget(node, Res.Weapon);
            for (int i = 0; i < total; i++)
            {
                var resource = i < node.SlotFood ? Res.Food : Res.Weapon;
                float slotX = 23f + (i % 3) * 8f, slotY = -9f + (i / 3) * 8f;
                int resourceSlotIndex = resource == Res.Food ? usedFoodSlots++ : usedWeaponSlots++;
                int have = node.Get(resource), targetCount = resource == Res.Food ? foodTarget : weaponTarget;
                if (resourceSlotIndex < have)
                {
                    _pips.Slot(resource, slotX, slotY, true, 3.1f);
                }
                else if (resourceSlotIndex < targetCount)
                {
                    _pips.Request(resource, slotX, slotY, 3.1f);
                }
                else
                {
                    _pips.Slot(resource, slotX, slotY, false, 3.1f);
                }
            }
            // 칸을 줄여 칸보다 많이 든 초과분은 붉은 테두리로 격자 아래에 표시
            int overflowFood = Mathf.Max(0, node.Food - node.SlotFood), overflowWeapons = Mathf.Max(0, node.Weapon - node.SlotWeapon);
            int rowsUsed = Mathf.CeilToInt(total / 3f);
            for (int i = 0; i < overflowFood + overflowWeapons; i++)
            {
                var resource = i < overflowFood ? Res.Food : Res.Weapon;
                float overflowX = 23f + (i % 3) * 8f, overflowY = -9f + (rowsUsed + i / 3) * 8f + 3f;
                _pips.Slot(resource, overflowX, overflowY, true, 3.1f);
                _pips.Over(resource, overflowX, overflowY);
            }
            _pips.End();
            ShowConfigRing(_configRing, view);
        }
    }
}
