using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>농장·대장간 그림입니다. 가득 차면 바깥 고리가 깜빡이고, 오른쪽에 쌓인 생산품을 보입니다.</summary>
    public sealed class ProducerView : NodeView
    {
        [SerializeField] Res _res;
        [SerializeField] SpriteRenderer _fill, _ring, _full;
        [SerializeField] PipGroup _pips;

        public override void Sync(Game g, ViewState v, float t)
        {
            var n = Node;
            var col = Palette.Of(_res);
            _fill.color = Palette.Ground;
            _ring.color = col;
            bool full = n.Stock >= g.B.PROD_CAP;
            if (_full.gameObject.activeSelf != full) _full.gameObject.SetActive(full);
            if (full) _full.color = Palette.WithAlpha(col, 0.4f + 0.3f * Mathf.Sin(t * 6f));
            _pips.Begin();
            for (int i = 0; i < n.Stock; i++) _pips.Dot(_res, 22f + (i % 4) * 8f, -5f + (i / 4) * 9f, 3.2f, col);
            _pips.End();
        }
    }
}
