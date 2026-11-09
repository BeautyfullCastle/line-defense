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
        const int PerRow = Game.REQ_PER_ROW;
        const float MoraleH = 4f;

        [SerializeField] SpriteRenderer _pulse, _body, _dot, _cfg, _tray;
        [SerializeField] PipGroup _marks, _pips;
        [SerializeField] Sprite _diamond, _chevrons;

        public override void Sync(Game g, ViewState v, float t)
        {
            var n = Node;
            bool starving = n.Food <= 0, dry = n.Weapon <= 0 && n.Ammo < 1f;
            Pulse(_pulse, n.Pulse);
            _body.color = Palette.Ink;
            _dot.color = starving || dry ? Palette.Danger : Palette.Ground;
            Cfg(_cfg, v);
            Marks(n);

            int cap = Mathf.Max(1, Mathf.RoundToInt(g.B.FORT_CAP));
            int rowsPerRes = Mathf.CeilToInt(cap / (float)PerRow);
            int rowCount = rowsPerRes * 2;
            float x0 = -(PerRow - 1) * 9f / 2f;
            float trayH = RowY(rowCount - 1, rowsPerRes) + 7f - 20f, trayW = (PerRow - 1) * 9f + 14f;
            _tray.size = new Vector2(trayW, trayH);
            _tray.transform.localPosition = P(x0 - 7f + trayW * 0.5f, 20f + trayH * 0.5f);
            _tray.color = Palette.WithAlpha(Palette.Line, 0.45f);

            _pips.Begin();
            Morale(g, n);
            for (int ri = 0; ri < rowCount; ri++)
            {
                var k = ri < rowsPerRes ? Res.Food : Res.Weapon;
                int start = (ri % rowsPerRes) * PerRow;
                int cnt = Mathf.Min(PerRow, cap - start);
                int have = n.Get(k), resv = n.Reserved(k);
                float y = RowY(ri, rowsPerRes);
                _pips.Alpha = have <= 0 ? 0.55f + 0.45f * Mathf.Sin(t * 7f) : 1f;
                for (int i = 0; i < cnt; i++)
                {
                    int idx = start + i;
                    if (idx >= have && idx < have + resv) _pips.Request(k, x0 + i * 9f, y, 3.3f);
                    else _pips.Slot(k, x0 + i * 9f, y, idx < have, 3.3f);
                }
            }
            _pips.End();
        }

        // 식량 줄 다음 무기 줄 사이에 틈을 둠
        static float RowY(int ri, int rowsPerRes) => 38f + ri * 10f + (ri >= rowsPerRes ? 4f : 0f);

        void Marks(Node n)
        {
            _marks.Begin();
            for (int i = 0; i < n.AtkLv; i++) _marks.Shape(_diamond, -21f, -8f + i * 7f, 1f, Palette.Danger);
            for (int i = 0; i < n.SpdLv; i++) _marks.Shape(_chevrons, 0f, -8f + i * 7f, 1f, Palette.Weapon);
            _marks.End();
        }

        // 식량을 먹을 때마다 오르는 사기를 칸 막대로 보임. 칸 수는 100%까지 먹어야 하는 횟수
        void Morale(Game g, Node n)
        {
            int segs = Mathf.Clamp(Mathf.RoundToInt(1f / Mathf.Max(0.01f, g.B.MORALE_UP)), 1, 10);
            float bw = (PerRow - 1) * 9f + 10f, gap = 1.5f;
            float sw = (bw - gap * (segs - 1)) / segs, lit = n.Morale * segs;
            var mcol = n.Morale < 0.4f ? Palette.Danger : Palette.Ink;
            for (int i = 0; i < segs; i++)
            {
                float fill = Mathf.Clamp01(lit - i), x = -bw / 2f + i * (sw + gap);
                _pips.Rect(x, 26f, sw, MoraleH, Palette.Ground);
                if (fill > 0f) _pips.Rect(x, 26f, sw * fill, MoraleH, mcol, 1);
            }
        }
    }
}
