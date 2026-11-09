using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>마차 그림입니다. 칸 수에 맞춰 몸체 크기가 바뀌고, 칸마다 실은 자원을 보입니다.</summary>
    public sealed class CartView : MonoBehaviour
    {
        const float Cell = 8f;

        [SerializeField] SpriteRenderer _body, _frame, _arrowFill, _arrowFrame;
        [SerializeField] PipGroup _pips;

        internal int Stamp;

        /// <summary>마차 위치·방향·적재를 반영합니다. cap은 지금 마차 적재 칸 수입니다.</summary>
        public void Sync(Cart c, Color lineColor, int cap)
        {
            transform.localPosition = new Vector3(c.Pos.x, -c.Pos.y, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -c.Angle * Mathf.Rad2Deg);
            int cols = Mathf.Max(1, Mathf.CeilToInt(cap / 2f));
            float cw = cols * Cell + 4f, chh = 2f * Cell + 4f;
            _body.size = new Vector2(cw, chh);
            _body.color = lineColor;
            // 테두리 스프라이트는 선 두께 2의 가운데가 가장자리에 오도록 1씩 큼
            _frame.size = new Vector2(cw + 2f, chh + 2f);
            _frame.color = Palette.Ground;

            _pips.Begin();
            int uf = 0, uw = 0, total = c.SlotFood + c.SlotWeapon;
            for (int i = 0; i < total; i++)
            {
                var k = i < c.SlotFood ? Res.Food : Res.Weapon;
                float x = -cw / 2f + 2f + Cell / 2f + (i % cols) * Cell;
                float y = -chh / 2f + 2f + Cell / 2f + (i / cols) * Cell;
                int u = k == Res.Food ? uf++ : uw++;
                _pips.Slot(k, x, y, u < (k == Res.Food ? c.Food : c.Weapon), 2.7f, Palette.Ground);
            }
            _pips.End();

            // 다른 창고로 옮기는 짐을 실었으면 진행 방향 화살표
            bool arrow = c.TransferFood + c.TransferWeapon > 0 && c.Food + c.Weapon > 0;
            if (_arrowFill.gameObject.activeSelf != arrow)
            {
                _arrowFill.gameObject.SetActive(arrow);
                _arrowFrame.gameObject.SetActive(arrow);
            }
            if (!arrow) return;
            var at = new Vector3(cw / 2f + 3f, 0f, 0f);
            _arrowFill.transform.localPosition = at;
            _arrowFrame.transform.localPosition = at;
            _arrowFill.color = lineColor;
            _arrowFrame.color = Palette.Ground;
        }
    }
}
