using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>농장·대장간 그림입니다. 가득 차면 바깥 고리가 깜빡이고, 오른쪽에 쌓인 생산품을 보입니다.</summary>
    public sealed class ProducerView : NodeView
    {
        [SerializeField] private Res _resource;
        [SerializeField] private SpriteRenderer _fill, _ring, _full;
        [SerializeField] private PipGroup _pips;

        public override void Sync(Game game, ViewState view, float t)
        {
            var node = Node;
            var color = Palette.ColorOf(_resource);
            _fill.color = Palette.Ground;
            _ring.color = color;
            bool full = node.Stock >= game.Balance.PROD_CAP;
            if (_full.gameObject.activeSelf != full)
            {
                _full.gameObject.SetActive(full);
            }

            if (full)
            {
                _full.color = Palette.WithAlpha(color, 0.4f + 0.3f * Mathf.Sin(t * 6f));
            }

            _pips.Begin();
            for (int i = 0; i < node.Stock; i++)
            {
                _pips.Dot(_resource, 22f + (i % 4) * 8f, -5f + (i / 4) * 9f, 3.2f, color);
            }

            _pips.End();
        }
    }
}
