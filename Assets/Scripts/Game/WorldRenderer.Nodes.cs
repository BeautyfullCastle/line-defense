using LineDefense.Core;
using LineDefense.View.Ui;
using UnityEngine;

namespace LineDefense.View
{
    public sealed partial class WorldRenderer
    {
        static ShapeKind KindOf(Res r) => r == Res.Food ? ShapeKind.Circle : ShapeKind.Triangle;

        // 찬 칸은 크고 진한 채움, 빈 칸은 작고 흐린 테두리
        void SlotIcon(Res k, float x, float y, bool full, float r, Color? onColor = null)
        {
            var col = onColor ?? Palette.Of(k);
            if (full)
            {
                b.FillShape(KindOf(k), x, y, r * 1.15f, col);
                return;
            }
            float prev = b.Alpha;
            b.Alpha = prev * 0.45f;
            if (onColor == null) b.FillShape(KindOf(k), x, y, r * 0.85f, Palette.Ground);
            b.StrokeShape(KindOf(k), x, y, r * 0.85f, 1f, col);
            b.Alpha = prev;
        }

        // 요청 칸(창고 목표량, 요새 예약)은 점선 테두리
        void ReqIcon(Res k, float x, float y, float r)
        {
            b.FillShape(KindOf(k), x, y, r * 0.95f, Palette.Ground);
            b.DashedShape(KindOf(k), x, y, r * 0.95f, 1.3f, Palette.Of(k), 1.6f, 1.4f);
        }

        void CfgRing(ViewState v, Node n, float r)
        {
            if (v.CfgNode == n) b.DashedCircle(0f, 0f, r, 2f, Palette.Ink, 4f, 3f);
        }

        void DrawNodes(Game g, ViewState v, WorldLabels labels)
        {
            float t = g.T;
            foreach (var n in g.Nodes)
            {
                float pop = Mathf.Min(1f, (t - n.Born) / 0.35f);
                float sc = pop < 1f ? 0.4f + 0.6f * pop + Mathf.Sin(pop * Mathf.PI) * 0.25f : 1f;
                b.Push(n.Pos.x, n.Pos.y, 0f, sc);
                switch (n.Type)
                {
                    case NodeType.Farm:
                    case NodeType.Forge:
                        DrawProducer(g, n, t);
                        break;
                    case NodeType.Stable:
                        DrawStable(g, v, n);
                        break;
                    case NodeType.Warehouse:
                        DrawWarehouse(g, v, n, labels);
                        break;
                    case NodeType.Fort:
                        DrawFort(g, v, n, t);
                        break;
                }
                b.Pop();
                if (n.Type == NodeType.Fort) FortLabel(n, t, sc, labels);
                else if (n.Type == NodeType.Warehouse) WarehouseLabel(g, n, sc, labels);
            }
        }

        void FortLabel(Node n, float t, float sc, WorldLabels labels)
        {
            bool starving = n.Food <= 0, dry = n.Weapon <= 0 && n.Ammo < 1f;
            if (!starving && !dry) return;
            float a = 0.5f + 0.5f * Mathf.Sin(t * 7f);
            labels.Add(_camForLabels, starving ? "식량 없음" : "무기 없음", n.Pos.x, n.Pos.y - 22f * sc, 13f * sc, Palette.WithAlpha(Palette.Danger, a), true);
        }

        void WarehouseLabel(Game g, Node n, float sc, WorldLabels labels)
        {
            int nl = g.LineCountWith(n.Id);
            int lim = Mathf.Max(0, Mathf.RoundToInt(g.B.WH_MAX_LINES));
            if (lim > 0 ? nl < lim : nl < 2)
                labels.Add(_camForLabels, lim > 0 ? $"노선 {nl}/{lim}" : $"노선 {nl}", n.Pos.x, n.Pos.y + 28f * sc, 11f * sc, Palette.Muted);
        }

        void DrawProducer(Game g, Node n, float t)
        {
            bool farm = n.Type == NodeType.Farm;
            var kind = farm ? ShapeKind.Circle : ShapeKind.Triangle;
            var col = farm ? Palette.Food : Palette.Weapon;
            b.FillShape(kind, 0, 0, 12f, Palette.Ground);
            b.StrokeShape(kind, 0, 0, 12f, 5f, col);
            if (n.Stock >= g.B.PROD_CAP)
                b.StrokeCircle(0, 0, 21f, 2f, Palette.WithAlpha(col, 0.4f + 0.3f * Mathf.Sin(t * 6f)));
            for (int i = 0; i < n.Stock; i++)
                b.FillShape(kind, 22f + (i % 4) * 8f, -5f + (i / 4) * 9f, 3.2f, col);
        }

        void DrawStable(Game g, ViewState v, Node n)
        {
            var l = g.LineById(n.LineId);
            var col = Palette.LineColor(l != null ? l.ColorIndex : 0);
            b.FillRoundRect(-12f, -8f, 24f, 19f, 3f, col);
            pts.Clear();
            pts.Add(new Vector2(-15f, -6f));
            pts.Add(new Vector2(0f, -16f));
            pts.Add(new Vector2(15f, -6f));
            b.Polyline(pts, 4f, col, false, true);
            b.FillRoundRect(-4f, 2f, 8f, 9f, 2f, Palette.Ground);
            int i = 0;
            for (int k = 0; k < n.Food; k++, i++) b.FillShape(ShapeKind.Circle, 22f + (i % 4) * 8f, -5f + (i / 4) * 9f, 3.1f, Palette.Food);
            for (int k = 0; k < n.Weapon; k++, i++) b.FillShape(ShapeKind.Triangle, 22f + (i % 4) * 8f, -5f + (i / 4) * 9f, 3.1f, Palette.Weapon);
            CfgRing(v, n, 24f);
        }

        void DrawWarehouse(Game g, ViewState v, Node n, WorldLabels labels)
        {
            if (n.Pulse > 0f) b.StrokeCircle(0, 0, 18f + (1f - n.Pulse * 2f) * 10f, 2f, Palette.WithAlpha(Palette.Ink, Mathf.Min(1f, n.Pulse)));
            b.FillRoundRect(-13f, -13f, 26f, 26f, 3f, Palette.Ground);
            b.StrokeRoundRect(-13f, -13f, 26f, 26f, 3f, 4f, Palette.Ink);
            b.Line(-8f, -8f, 8f, 8f, 2.5f, Palette.Ink);
            b.Line(8f, -8f, -8f, 8f, 2.5f, Palette.Ink);

            int usedF = 0, usedW = 0, total = n.SlotFood + n.SlotWeapon;
            int tgF = g.WarehouseTarget(n, Res.Food), tgW = g.WarehouseTarget(n, Res.Weapon);
            for (int i = 0; i < total; i++)
            {
                var k = i < n.SlotFood ? Res.Food : Res.Weapon;
                float cx = 23f + (i % 3) * 8f, cy = -9f + (i / 3) * 8f;
                int u = k == Res.Food ? usedF++ : usedW++;
                int have = n.Get(k), tg = k == Res.Food ? tgF : tgW;
                if (u < have) SlotIcon(k, cx, cy, true, 3.1f);
                else if (u < tg) ReqIcon(k, cx, cy, 3.1f);
                else SlotIcon(k, cx, cy, false, 3.1f);
            }
            // 칸을 줄여 칸보다 많이 든 초과분은 붉은 테두리로 격자 아래에 표시
            int overF = Mathf.Max(0, n.Food - n.SlotFood), overW = Mathf.Max(0, n.Weapon - n.SlotWeapon);
            int rowsUsed = Mathf.CeilToInt(total / 3f);
            for (int i = 0; i < overF + overW; i++)
            {
                var k = i < overF ? Res.Food : Res.Weapon;
                float cx = 23f + (i % 3) * 8f, cy = -9f + (rowsUsed + i / 3) * 8f + 3f;
                SlotIcon(k, cx, cy, true, 3.1f);
                b.StrokeShape(KindOf(k), cx, cy, 4.4f, 1.2f, Palette.Danger);
            }
            CfgRing(v, n, 24f);
        }

        void DrawFort(Game g, ViewState v, Node n, float t)
        {
            bool starving = n.Food <= 0, dry = n.Weapon <= 0 && n.Ammo < 1f;
            if (n.Pulse > 0f) b.StrokeCircle(0, 0, 18f + (1f - n.Pulse * 2f) * 10f, 2f, Palette.WithAlpha(Palette.Ink, Mathf.Min(1f, n.Pulse)));
            b.FillShape(ShapeKind.Hex, 0, 0, 15f, Palette.Ink);
            for (int i = 0; i < n.AtkLv; i++)
            {
                float yy = -8f + i * 7f;
                b.Quad(new Vector2(-21f, yy - 3f), new Vector2(-18f, yy), new Vector2(-21f, yy + 3f), new Vector2(-24f, yy), Palette.Danger);
            }
            for (int i = 0; i < n.SpdLv; i++)
            {
                float yy = -8f + i * 7f;
                pts.Clear();
                pts.Add(new Vector2(18f, yy - 3f)); pts.Add(new Vector2(21f, yy)); pts.Add(new Vector2(18f, yy + 3f));
                b.Polyline(pts, 1.6f, Palette.Weapon, false, false);
                pts.Clear();
                pts.Add(new Vector2(21.5f, yy - 3f)); pts.Add(new Vector2(24.5f, yy)); pts.Add(new Vector2(21.5f, yy + 3f));
                b.Polyline(pts, 1.6f, Palette.Weapon, false, false);
            }
            CfgRing(v, n, 28f);
            b.FillCircle(0, 0, 5f, starving || dry ? Palette.Danger : Palette.Ground);

            int cap = Mathf.Max(1, Mathf.RoundToInt(g.B.FORT_CAP));
            int rowsPerRes = Mathf.CeilToInt(cap / (float)ReqPerRow);
            int rowCount = rowsPerRes * 2;
            // 식량 줄 다음 무기 줄 사이에 틈을 둠
            float RowY(int ri) => 38f + ri * 10f + (ri >= rowsPerRes ? 4f : 0f);
            float x0 = -(ReqPerRow - 1) * 9f / 2f;
            float prev = b.Alpha;
            b.Alpha = prev * 0.45f;
            b.FillRoundRect(x0 - 7f, 20f, (ReqPerRow - 1) * 9f + 14f, RowY(rowCount - 1) + 7f - 20f, 5f, Palette.Line);
            b.Alpha = prev;

            int segs = Mathf.Clamp(Mathf.RoundToInt(1f / Mathf.Max(0.01f, g.B.MORALE_UP)), 1, 10);
            float bw = (ReqPerRow - 1) * 9f + 10f, gap = 1.5f;
            float sw = (bw - gap * (segs - 1)) / segs, lit = n.Morale * segs;
            var mcol = n.Morale < 0.4f ? Palette.Danger : Palette.Ink;
            for (int i = 0; i < segs; i++)
            {
                float fill = Mathf.Clamp01(lit - i), x = -bw / 2f + i * (sw + gap);
                b.FillRect(x, 26f, sw, 4f, Palette.Ground);
                if (fill > 0f) b.FillRect(x, 26f, sw * fill, 4f, mcol);
            }

            for (int ri = 0; ri < rowCount; ri++)
            {
                var k = ri < rowsPerRes ? Res.Food : Res.Weapon;
                int start = (ri % rowsPerRes) * ReqPerRow;
                int cnt = Mathf.Min(ReqPerRow, cap - start);
                int have = n.Get(k), resv = n.Reserved(k);
                float y = RowY(ri);
                if (have <= 0) b.Alpha = prev * (0.55f + 0.45f * Mathf.Sin(t * 7f));
                for (int i = 0; i < cnt; i++)
                {
                    int idx = start + i;
                    if (idx >= have && idx < have + resv) ReqIcon(k, x0 + i * 9f, y, 3.3f);
                    else SlotIcon(k, x0 + i * 9f, y, idx < have, 3.3f);
                }
                b.Alpha = prev;
            }
        }

        void DrawCarts(Game g)
        {
            int cap = g.CartCap();
            int cols = Mathf.Max(1, Mathf.CeilToInt(cap / 2f));
            const float cell = 8f;
            float cw = cols * cell + 4f, chh = 2f * cell + 4f;
            foreach (var l in g.Lines)
            {
                if (l.Points.Count < 2) continue;
                var col = Palette.LineColor(l.ColorIndex);
                foreach (var c in l.Carts)
                {
                    b.Push(c.Pos.x, c.Pos.y, c.Angle);
                    b.FillRoundRect(-cw / 2f, -chh / 2f, cw, chh, 3f, col);
                    b.StrokeRoundRect(-cw / 2f, -chh / 2f, cw, chh, 3f, 2f, Palette.Ground);
                    int uf = 0, uw = 0, total = c.SlotFood + c.SlotWeapon;
                    for (int i = 0; i < total; i++)
                    {
                        var k = i < c.SlotFood ? Res.Food : Res.Weapon;
                        float x = -cw / 2f + 2f + cell / 2f + (i % cols) * cell;
                        float y = -chh / 2f + 2f + cell / 2f + (i / cols) * cell;
                        int u = k == Res.Food ? uf++ : uw++;
                        SlotIcon(k, x, y, u < (k == Res.Food ? c.Food : c.Weapon), 2.7f, Palette.Ground);
                    }
                    // 다른 창고로 옮기는 짐을 실었으면 진행 방향 화살표
                    if (c.TransferFood + c.TransferWeapon > 0 && c.Food + c.Weapon > 0)
                    {
                        b.Tri(cw / 2f + 3f, -5f, cw / 2f + 10f, 0f, cw / 2f + 3f, 5f, col);
                        pts.Clear();
                        pts.Add(new Vector2(cw / 2f + 3f, -5f)); pts.Add(new Vector2(cw / 2f + 10f, 0f)); pts.Add(new Vector2(cw / 2f + 3f, 5f));
                        b.Polyline(pts, 1.5f, Palette.Ground, true, false);
                    }
                    b.Pop();
                }
            }
        }

        void DrawMonsters(Game g)
        {
            foreach (var m in g.Mons)
            {
                float sz = m.Size > 0f ? m.Size : 1f;
                b.Push(m.Pos.x, m.Pos.y + Mathf.Sin(m.Bob) * 1.5f);
                float prev = b.Alpha;
                b.Alpha = m.Alpha;
                pts.Clear();
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Tau / 16f, r = (i % 2 == 1 ? 6.5f : 10.5f) * sz;
                    pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
                b.FillPoly(pts, m.Hit > 0f ? Palette.Danger : Palette.Monster);
                b.FillCircle(-2.5f, -1f, 1.6f, Palette.Ground);
                b.FillCircle(2.5f, -1f, 1.6f, Palette.Ground);
                if (m.Hp < m.MaxHp)
                {
                    b.FillRect(-10f, -17f, 20f, 3f, Palette.Line);
                    b.FillRect(-10f, -17f, 20f * Mathf.Clamp01(m.Hp / m.MaxHp), 3f, Palette.Danger);
                }
                b.Alpha = prev;
                b.Pop();
            }
        }
    }
}
