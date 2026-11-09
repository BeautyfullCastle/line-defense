using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// 요새 그림입니다. 육각형 몸체, 왼쪽 공격력·오른쪽 공격 속도 강화 표시,
    /// 아래 받침에 사기 막대와 식량·무기 칸을 보입니다.
    /// </summary>
    public sealed class FortView : NodeView
    {
        private const int PerRow = Game.SlotsPerRow;
        private const float MoraleBarHeight = 4f;

        [SerializeField] private SpriteRenderer _pulse, _body, _dot, _configRing, _tray;
        [SerializeField] private PipGroup _marks, _pips;
        [SerializeField] private Sprite _diamond, _chevrons;

        public override void Sync(Game game, ViewState view, float t)
        {
            var node = Node;
            bool starving = node.Food <= 0, outOfWeapons = node.Weapon <= 0 && node.Ammo < 1f;
            Pulse(_pulse, node.Pulse);
            _body.color = Palette.Ink;
            _dot.color = starving || outOfWeapons ? Palette.Danger : Palette.Ground;
            ShowConfigRing(_configRing, view);
            Marks(node);

            int capacity = Mathf.Max(1, Mathf.RoundToInt(game.Balance.FORT_CAP));
            int rowsPerResource = Mathf.CeilToInt(capacity / (float)PerRow);
            int rowCount = rowsPerResource * 2;
            float rowStartX = -(PerRow - 1) * 9f / 2f;
            float trayHeight = RowY(rowCount - 1, rowsPerResource) + 7f - 20f, trayWidth = (PerRow - 1) * 9f + 14f;
            _tray.size = new Vector2(trayWidth, trayHeight);
            _tray.transform.localPosition = ToLocal(rowStartX - 7f + trayWidth * 0.5f, 20f + trayHeight * 0.5f);
            _tray.color = Palette.WithAlpha(Palette.Line, 0.45f);

            _pips.Begin();
            Morale(game, node);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var resource = rowIndex < rowsPerResource ? Res.Food : Res.Weapon;
                int start = (rowIndex % rowsPerResource) * PerRow;
                int rowPipCount = Mathf.Min(PerRow, capacity - start);
                int have = node.Get(resource), resv = node.Reserved(resource);
                float y = RowY(rowIndex, rowsPerResource);
                _pips.Alpha = have <= 0 ? 0.55f + 0.45f * Mathf.Sin(t * 7f) : 1f;
                for (int i = 0; i < rowPipCount; i++)
                {
                    int slotIndex = start + i;
                    if (slotIndex >= have && slotIndex < have + resv)
                    {
                        _pips.Request(resource, rowStartX + i * 9f, y, 3.3f);
                    }
                    else
                    {
                        _pips.Slot(resource, rowStartX + i * 9f, y, slotIndex < have, 3.3f);
                    }
                }
            }
            _pips.End();
        }

        // 식량 줄 다음 무기 줄 사이에 틈을 둠
        private static float RowY(int rowIndex, int rowsPerResource) => 38f + rowIndex * 10f + (rowIndex >= rowsPerResource ? 4f : 0f);

        private void Marks(Node node)
        {
            _marks.Begin();
            for (int i = 0; i < node.AttackLevel; i++)
            {
                _marks.Shape(_diamond, -21f, -8f + i * 7f, 1f, Palette.Danger);
            }

            for (int i = 0; i < node.SpeedLevel; i++)
            {
                _marks.Shape(_chevrons, 0f, -8f + i * 7f, 1f, Palette.Weapon);
            }

            _marks.End();
        }

        // 식량을 먹을 때마다 오르는 사기를 칸 막대로 보임. 칸 수는 100%까지 먹어야 하는 횟수
        private void Morale(Game game, Node node)
        {
            int segmentCount = Mathf.Clamp(Mathf.RoundToInt(1f / Mathf.Max(0.01f, game.Balance.MORALE_UP)), 1, 10);
            float barWidth = (PerRow - 1) * 9f + 10f, gap = 1.5f;
            float segmentWidth = (barWidth - gap * (segmentCount - 1)) / segmentCount, litSegments = node.Morale * segmentCount;
            var moraleColor = node.Morale < 0.4f ? Palette.Danger : Palette.Ink;
            for (int i = 0; i < segmentCount; i++)
            {
                float fill = Mathf.Clamp01(litSegments - i), x = -barWidth / 2f + i * (segmentWidth + gap);
                _pips.Rect(x, 26f, segmentWidth, MoraleBarHeight, Palette.Ground);
                if (fill > 0f)
                {
                    _pips.Rect(x, 26f, segmentWidth * fill, MoraleBarHeight, moraleColor, 1);
                }
            }
        }
    }
}
