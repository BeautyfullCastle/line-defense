using System.Collections.Generic;
using LineDefense.Core;
using LineDefense.View.Ui;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>게임 상태를 ShapeBatch 메시 하나로 매 프레임 다시 그립니다.</summary>
    public sealed partial class WorldRenderer
    {
        const float Tau = Mathf.PI * 2f;
        const float RW = Game.RW;
        const float HwMax = Game.HW_MAX;
        const int ExpandEvery = Game.EXPAND_EVERY;
        const int ReqPerRow = Game.REQ_PER_ROW;

        readonly ShapeBatch b = new ShapeBatch("World");
        readonly List<Vector2> pts = new List<Vector2>(64);
        readonly List<Vector2> pts2 = new List<Vector2>(64);
        readonly List<(Vec2 pos, Vec2 endPos, Vec2 dir, LineEnd end, Line line)> _handles = new List<(Vec2 pos, Vec2 endPos, Vec2 dir, LineEnd end, Line line)>(2);
        CameraFit _camForLabels;

        /// <summary>메시를 담는 렌더러 오브젝트를 만듭니다.</summary>
        public WorldRenderer(Transform parent)
        {
            var go = new GameObject("World");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("LineDefense/VertexColorUnlit") ?? Shader.Find("Sprites/Default");
            mr.sharedMaterial = new Material(shader);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>현재 상태를 한 프레임 분량의 메시로 만듭니다.</summary>
        public void Render(Game g, ViewState v, CameraFit cam, Effects fx, WorldLabels labels)
        {
            _camForLabels = cam;
            b.Begin();
            labels.Begin();
            DrawTerrain(g, v, cam, labels);
            DrawRanges(g);
            DrawWallAndRoutes(g, cam, labels);
            DrawLines(g, v);
            DrawShots(fx);
            DrawNodes(g, v, labels);
            DrawCarts(g);
            DrawMonsters(g);
            DrawFx(fx, labels, cam);
            DrawGhost(g, v);
            b.End();
            labels.End();
        }

        static float TopY(float x) => RW - Mathf.Sqrt(Mathf.Max(0f, RW * RW - x * x));

        void DrawTerrain(Game g, ViewState v, CameraFit cam, WorldLabels labels)
        {
            float hw = v.HWd, d = v.Dd;
            // 성벽 원 전체를 안개색으로 깔고 개척지만 땅색으로 덮음
            b.FillCircle(0, RW, RW, Palette.Fog, 220);
            pts.Clear();
            const int arcSteps = 24;
            for (int i = 0; i <= arcSteps; i++)
            {
                float x = Mathf.Lerp(-hw, hw, i / (float)arcSteps);
                pts.Add(new Vector2(x, TopY(x)));
            }
            pts.Add(new Vector2(hw, d));
            pts.Add(new Vector2(-hw, d));
            b.FillPoly(pts, Palette.Ground);

            for (float gx = -Mathf.Ceil(hw / 40f) * 40f; gx <= hw; gx += 40f)
            {
                if (gx < -hw) continue;
                b.Line(gx, TopY(gx), gx, d, 1f, Palette.Grid);
            }
            for (float gy = 0f; gy <= d; gy += 40f)
            {
                float half = Mathf.Min(hw, Mathf.Sqrt(Mathf.Max(0f, RW * RW - (gy - RW) * (gy - RW))));
                b.Line(-half, gy, half, gy, 1f, Palette.Grid);
            }

            pts2.Clear();
            pts2.Add(new Vector2(-hw, d));
            for (int i = 0; i <= arcSteps; i++)
            {
                float x = Mathf.Lerp(-hw, hw, i / (float)arcSteps);
                pts2.Add(new Vector2(x, TopY(x)));
            }
            pts2.Add(new Vector2(hw, d));
            b.Dashed(pts2, 1.5f, Palette.WithAlpha(Palette.Muted, 0.55f), 10f, 8f, true);

            if (d + 34f < cam.WorldBottom)
            {
                string s = g.HW < HwMax ? $"미개척지 · {ExpandEvery}웨이브마다 개척" : "미개척지";
                labels.Add(cam, s, 0f, d + 30f, 12f, Palette.Muted);
            }
        }

        void DrawRanges(Game g)
        {
            float range = g.B.RANGE;
            foreach (var f in g.Nodes)
            {
                if (f.Type != NodeType.Fort) continue;
                b.FillCircle(f.Pos.x, f.Pos.y, range, Palette.WithAlpha(Palette.Ink, 0.06f), 64);
                b.DashedCircle(f.Pos.x, f.Pos.y, range, 1.2f, Palette.WithAlpha(Palette.Ink, 0.22f), 6f, 6f);
            }
        }

        void DrawWallAndRoutes(Game g, CameraFit cam, WorldLabels labels)
        {
            float t = g.T;
            float hx = cam.HalfW + 40f;
            float span = Mathf.Asin(Mathf.Min(1f, hx / RW));
            float a0 = -Mathf.PI * 0.5f - span, a1 = -Mathf.PI * 0.5f + span;

            foreach (var r in g.Routes)
            {
                float cx = Mathf.Cos(r.Angle), sy = Mathf.Sin(r.Angle);
                float fresh = Mathf.Max(0f, 1f - (t - r.Born) / 6f);
                pts.Clear();
                pts.Add(new Vector2(cx * (RW + 16f), RW + sy * (RW + 16f)));
                pts.Add(new Vector2(cx * (RW + Game.DEN), RW + sy * (RW + Game.DEN)));
                b.Dashed(pts, 3f, Palette.WithAlpha(Palette.Danger, 0.3f + fresh * 0.5f), 8f, 8f);
                float dx = cx * (RW + Game.DEN + 14f), dy = RW + sy * (RW + Game.DEN + 14f);
                b.FillCircle(dx, dy, 13f, Palette.Monster);
                b.FillSector(dx, dy + 3f, 6f, Mathf.PI, Tau, Palette.Outside);
                if (fresh > 0f) b.StrokeCircle(dx, dy, 18f + (1f - fresh) * 14f, 3f, Palette.WithAlpha(Palette.Danger, fresh));
                float st = g.RouteThreatForDisplay(r);
                string label = st < 1f ? $"신규 · 위협 {Mathf.RoundToInt(st * 100f)}%" : "위협 100%";
                labels.Add(cam, label, dx, dy - 20f, 12f, Palette.Danger, true);
            }

            b.StrokeArc(0f, RW, RW + 4f, a0, a1, 12f, Palette.Wall);
            float stepA = 22f / RW;
            for (float a = Mathf.Ceil(a0 / stepA) * stepA; a < a1; a += stepA)
            {
                b.Push(0f, RW, a);
                b.FillRect(RW + 9f, -3.5f, 6f, 7f, Palette.Wall);
                b.Pop();
            }
            if (g.Flash > 0f)
                b.StrokeArc(0f, RW, RW + 4f, a0, a1, 12f, Palette.WithAlpha(Palette.Danger, Mathf.Min(1f, g.Flash * 2.4f)));

            if (g.Phase == Phase.Countdown && !g.Over)
            {
                float pulse = 0.45f + 0.45f * Mathf.Sin(t * 5f);
                foreach (var r in g.Routes)
                {
                    b.Push(0f, RW, r.Angle);
                    for (int k = 0; k < 3; k++)
                    {
                        var col = Palette.WithAlpha(Palette.Danger, pulse * (1f - k * 0.25f));
                        float x = RW + 34f + k * 16f;
                        b.Quad(new Vector2(x - 7, 0), new Vector2(x + 5, -10), new Vector2(x + 9, -10), new Vector2(x - 1, 0), col);
                        b.Quad(new Vector2(x - 7, 0), new Vector2(x + 5, 10), new Vector2(x + 9, 10), new Vector2(x - 1, 0), col);
                    }
                    b.Pop();
                }
            }
        }

        /// <summary>두 정거장 사이에 쓰는 직각 꺾임점(45도 구간)을 구합니다. 꺾지 않으면 false입니다.</summary>
        public static bool Bend(Vec2 a, Vec2 c, out Vec2 bend)
        {
            float dx = c.x - a.x, dy = c.y - a.y, ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
            bend = default;
            if (ax < 1f || ay < 1f || Mathf.Abs(ax - ay) < 1f) return false;
            bend = ax > ay ? new Vec2(a.x + Mathf.Sign(dx) * (ax - ay), a.y) : new Vec2(a.x, a.y + Mathf.Sign(dy) * (ay - ax));
            return true;
        }

        void DrawLines(Game g, ViewState v)
        {
            foreach (var l in g.Lines)
            {
                if (l.Points.Count < 2) continue;
                var col = Palette.LineColor(l.ColorIndex);
                pts.Clear();
                foreach (var p in l.Points) pts.Add(p.Pos.ToV());
                b.Polyline(pts, 7f, col, l.Loop, true);
                g.Handles(l, _handles);
                foreach (var h in _handles)
                {
                    b.Line(h.endPos.x, h.endPos.y, h.pos.x, h.pos.y, 7f, col, true);
                    b.Line(h.pos.x - h.dir.y * 10f, h.pos.y + h.dir.x * 10f, h.pos.x + h.dir.y * 10f, h.pos.y - h.dir.x * 10f, 6f, col, true);
                }
            }
            if (!v.Dragging || v.DragLine == null) return;
            var dl = v.DragLine;
            var ids = dl.Nodes;
            bool tail = v.DragEnd == LineEnd.Tail;
            var endN = g.NodeById(tail ? ids[ids.Count - 1] : ids[0]);
            if (endN == null) return;
            int other = tail ? ids[0] : ids[ids.Count - 1];
            var tn = v.DragTarget;
            Vec2 to = tn != null && (g.CanJoin(dl, tn) || (ids.Count >= 3 && tn.Id == other)) ? tn.Pos : v.DragPos;
            pts.Clear();
            pts.Add(endN.Pos.ToV());
            if (Bend(endN.Pos, to, out var bp)) pts.Add(bp.ToV());
            pts.Add(to.ToV());
            b.Polyline(pts, 7f, Palette.WithAlpha(Palette.LineColor(dl.ColorIndex), 0.55f), false, false);
        }

        void DrawShots(Effects fx)
        {
            foreach (var s in fx.Shots)
                b.Line(s.A.x, s.A.y, s.B.x, s.B.y, 2f, Palette.WithAlpha(Palette.Ink, Mathf.Clamp01(s.Life / 0.18f)));
        }

        void DrawFx(Effects fx, WorldLabels labels, CameraFit cam)
        {
            foreach (var p in fx.Parts)
                b.FillRect(p.P.x - 2f, p.P.y - 2f, 4f, 4f, Palette.WithAlpha(p.Col, Mathf.Min(1f, p.Life * 2f)));
            foreach (var f in fx.Floats)
                labels.Add(cam, f.Text, f.P.x, f.P.y, 13f, Palette.WithAlpha(Palette.Food, Mathf.Min(1f, f.Life * 1.5f)), true);
        }

        void DrawGhost(Game g, ViewState v)
        {
            if (!v.HasMode || !v.HoverInside) return;
            bool ok = g.CanPlace(v.Mode, v.Hover);
            var col = ok ? Palette.Ink : Palette.Danger;
            float x = v.Hover.x, y = v.Hover.y;
            if (v.Mode == BuildKind.Fort)
            {
                var q = g.SnapWall(v.Hover, out float off);
                if (off < Game.WALL_SNAP * 2.5f) { x = q.x; y = q.y; }
                b.FillCircle(x, y, g.B.RANGE, Palette.WithAlpha(col, 0.07f), 64);
                b.FillShape(ShapeKind.Hex, x, y, 15f, Palette.WithAlpha(col, 0.55f));
            }
            else if (v.Mode == BuildKind.Warehouse)
            {
                b.StrokeRoundRect(x - 13f, y - 13f, 26f, 26f, 3f, 4f, Palette.WithAlpha(col, 0.55f));
            }
            else
            {
                var fill = ok ? Palette.LineColor(FreeColorIndex(g)) : Palette.Danger;
                b.FillRoundRect(x - 12f, y - 8f, 24f, 19f, 3f, Palette.WithAlpha(fill, 0.55f));
            }
        }

        /// <summary>아직 쓰지 않은 가장 앞 노선 색 인덱스입니다. 모두 쓰였으면 0입니다.</summary>
        public static int FreeColorIndex(Game g)
        {
            for (int i = 0; i < Game.LineColorCount; i++)
            {
                bool used = false;
                foreach (var l in g.Lines) if (l.ColorIndex == i) { used = true; break; }
                if (!used) return i;
            }
            return 0;
        }
    }
}
