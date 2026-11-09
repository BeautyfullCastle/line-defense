using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>마차 그림입니다. 칸 수에 맞춰 몸체 크기가 바뀌고, 칸마다 실은 자원을 보입니다.</summary>
    public sealed class CartView : MonoBehaviour
    {
        private const float Cell = 8f;

        [SerializeField] private SpriteRenderer _body, _frame, _arrowFill, _arrowFrame;
        [SerializeField] private PipGroup _pips;

        internal int Stamp;

        /// <summary>마차 위치·방향·적재를 반영합니다. cap은 지금 마차 적재 칸 수입니다.</summary>
        public void Sync(Cart cart, Color lineColor, int capacity)
        {
            transform.localPosition = new Vector3(cart.Position.X, -cart.Position.Y, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -cart.Angle * Mathf.Rad2Deg);
            int cols = Mathf.Max(1, Mathf.CeilToInt(capacity / 2f));
            float cartWidth = cols * Cell + 4f, cartHeight = 2f * Cell + 4f;
            _body.size = new Vector2(cartWidth, cartHeight);
            _body.color = lineColor;
            // 테두리 스프라이트는 선 두께 2의 가운데가 가장자리에 오도록 1씩 큼
            _frame.size = new Vector2(cartWidth + 2f, cartHeight + 2f);
            _frame.color = Palette.Ground;

            _pips.Begin();
            int usedFoodSlots = 0, usedWeaponSlots = 0, total = cart.SlotFood + cart.SlotWeapon;
            for (int i = 0; i < total; i++)
            {
                var resource = i < cart.SlotFood ? Res.Food : Res.Weapon;
                float x = -cartWidth / 2f + 2f + Cell / 2f + (i % cols) * Cell;
                float y = -cartHeight / 2f + 2f + Cell / 2f + (i / cols) * Cell;
                int resourceSlotIndex = resource == Res.Food ? usedFoodSlots++ : usedWeaponSlots++;
                _pips.Slot(resource, x, y, resourceSlotIndex < (resource == Res.Food ? cart.Food : cart.Weapon), 2.7f, Palette.Ground);
            }
            _pips.End();

            // 다른 창고로 옮기는 짐을 실었으면 진행 방향 화살표
            bool arrow = cart.TransferFood + cart.TransferWeapon > 0 && cart.Food + cart.Weapon > 0;
            if (_arrowFill.gameObject.activeSelf != arrow)
            {
                _arrowFill.gameObject.SetActive(arrow);
                _arrowFrame.gameObject.SetActive(arrow);
            }
            if (!arrow)
            {
                return;
            }

            var arrowPosition = new Vector3(cartWidth / 2f + 3f, 0f, 0f);
            _arrowFill.transform.localPosition = arrowPosition;
            _arrowFrame.transform.localPosition = arrowPosition;
            _arrowFill.color = lineColor;
            _arrowFrame.color = Palette.Ground;
        }
    }
}
