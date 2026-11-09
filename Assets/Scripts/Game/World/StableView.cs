using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>마굿간 그림입니다. 몸체는 노선 색, 오른쪽에 대기 중인 식량·무기를 보입니다.</summary>
    public sealed class StableView : NodeView
    {
        [SerializeField] private SpriteRenderer _body, _door, _configRing;
        [SerializeField] private PipGroup _pips;

        public override void Sync(Game game, ViewState view, float t)
        {
            var node = Node;
            var line = game.LineById(node.LineId);
            _body.color = Palette.LineColor(line != null ? line.ColorIndex : 0);
            _door.color = Palette.Ground;
            _pips.Begin();
            int i = 0;
            for (int k = 0; k < node.Food; k++, i++)
            {
                _pips.Dot(Res.Food, 22f + (i % 4) * 8f, -5f + (i / 4) * 9f, 3.1f, Palette.Food);
            }

            for (int k = 0; k < node.Weapon; k++, i++)
            {
                _pips.Dot(Res.Weapon, 22f + (i % 4) * 8f, -5f + (i / 4) * 9f, 3.1f, Palette.Weapon);
            }

            _pips.End();
            ShowConfigRing(_configRing, view);
        }
    }
}
