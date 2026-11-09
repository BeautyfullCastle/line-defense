using System.Collections.Generic;
using LineDefense.Core;
using LineDefense.View.Ui;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// 게임 상태를 매 프레임 다시 그립니다. 모양이 플레이에 따라 바뀌는 것(땅, 성벽, 침투로, 노선, 사거리, 효과)은
    /// 메시 세 장(바닥, 노선, 효과)으로 만들고, 모양이 정해진 것(건물, 마차, 몬스터, 굴)은 WorldSprites의 프리팹으로 그립니다.
    /// </summary>
    public sealed class WorldRenderer
    {
        const float RW = Game.RW;
        const float HwMax = Game.HW_MAX;
        const int ExpandEvery = Game.EXPAND_EVERY;

        // 그리는 순서. 스프라이트 프리팹의 정렬 순서(굴 5, 정거장 20대, 마차 30대, 몬스터 40대, 건설 미리보기 55)와 맞춤
        const int GroundOrder = 0, LinesOrder = 10, FxOrder = 50;

        readonly ShapeBatch _ground = new ShapeBatch("Ground");
        readonly ShapeBatch _lines = new ShapeBatch("Lines");
        readonly ShapeBatch _fx = new ShapeBatch("Fx");
        readonly WorldSprites _sprites;
        readonly List<Vector2> pts = new List<Vector2>(64);
        readonly List<Vector2> pts2 = new List<Vector2>(64);
        readonly List<Vec2> _centroidPs = new List<Vec2>(16);
        float _wallGlow;
        readonly List<(Vec2 pos, Vec2 endPos, Vec2 dir, LineEnd end, Line line)> _handles = new List<(Vec2 pos, Vec2 endPos, Vec2 dir, LineEnd end, Line line)>(2);

        /// <summary>메시 렌더러 세 개와 스프라이트 루트를 만듭니다.</summary>
        public WorldRenderer(Transform parent, WorldArt art)
        {
            var root = new GameObject("World").transform;
            root.SetParent(parent, false);
            MeshLayer(root, _ground, art.MeshMaterial, GroundOrder);
            MeshLayer(root, _lines, art.MeshMaterial, LinesOrder);
            MeshLayer(root, _fx, art.MeshMaterial, FxOrder);
            _sprites = new WorldSprites(art, root);
        }

        static void MeshLayer(Transform parent, ShapeBatch b, Material mat, int order)
        {
            var go = new GameObject(b.Mesh.name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>새 판을 시작할 때 이전 판의 스프라이트를 지웁니다.</summary>
        public void Clear() => _sprites.Clear();

        /// <summary>현재 상태를 한 프레임 분량으로 그립니다.</summary>
        public void Render(Game g, ViewState v, CameraFit cam, Effects fx, WorldLabels labels)
        {
            _ground.Begin(); _lines.Begin(); _fx.Begin();
            labels.Begin();
            DrawTerrain(g, v, cam, labels);
            DrawRanges(g);
            _wallGlow = fx.WallGlow;
            DrawWallAndRoutes(g, cam, labels);
            DrawLines(g, v);
            DrawShots(fx);
            DrawNodeLabels(g, cam, labels);
            DrawFx(fx, labels, cam);
            DrawGhost(g, v);
            _sprites.Sync(g, v);
            _ground.End(); _lines.End(); _fx.End();
            labels.End();
        }

        static float TopY(float x) => RW - Mathf.Sqrt(Mathf.Max(0f, RW * RW - x * x));

        void DrawTerrain(Game g, ViewState v, CameraFit cam, WorldLabels labels)
        {
            float hw = v.HWd, d = v.Dd;
            // 성벽 원 전체를 안개색으로 깔고 개척지만 땅색으로 덮음
            _ground.FillCircle(0, RW, RW, Palette.Fog, 220);
            pts.Clear();
            const int arcSteps = 24;
            for (int i = 0; i <= arcSteps; i++)
            {
                float x = Mathf.Lerp(-hw, hw, i / (float)arcSteps);
                pts.Add(new Vector2(x, TopY(x)));
            }
            pts.Add(new Vector2(hw, d));
            pts.Add(new Vector2(-hw, d));
            _ground.FillPoly(pts, Palette.Ground);

            for (float gx = -Mathf.Ceil(hw / 40f) * 40f; gx <= hw; gx += 40f)
            {
                if (gx < -hw) continue;
                _ground.Line(gx, TopY(gx), gx, d, 1f, Palette.Grid);
            }
            for (float gy = 0f; gy <= d; gy += 40f)
            {
                float half = Mathf.Min(hw, Mathf.Sqrt(Mathf.Max(0f, RW * RW - (gy - RW) * (gy - RW))));
                _ground.Line(-half, gy, half, gy, 1f, Palette.Grid);
            }

            pts2.Clear();
            pts2.Add(new Vector2(-hw, d));
            for (int i = 0; i <= arcSteps; i++)
            {
                float x = Mathf.Lerp(-hw, hw, i / (float)arcSteps);
                pts2.Add(new Vector2(x, TopY(x)));
            }
            pts2.Add(new Vector2(hw, d));
            _ground.Dashed(pts2, 1.5f, Palette.WithAlpha(Palette.Muted, 0.55f), 10f, 8f, true);

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
                _ground.FillCircle(f.Pos.x, f.Pos.y, range, Palette.WithAlpha(Palette.Ink, 0.06f), 64);
                _ground.DashedCircle(f.Pos.x, f.Pos.y, range, 1.2f, Palette.WithAlpha(Palette.Ink, 0.22f), 6f, 6f);
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
                _ground.Dashed(pts, 3f, Palette.WithAlpha(Palette.Danger, 0.3f + fresh * 0.5f), 8f, 8f);
                // 굴 그림은 DenView가 그림
                float dx = cx * (RW + Game.DEN + 14f), dy = RW + sy * (RW + Game.DEN + 14f);
                float st = g.RouteThreatForDisplay(r);
                string label = st < 1f ? $"신규 · 위협 {Mathf.RoundToInt(st * 100f)}%" : "위협 100%";
                labels.Add(cam, label, dx, dy - 20f, 12f, Palette.Danger, true);
            }

            // 피해를 받으면 성벽 전체가 잠깐 붉게 물듦
            var wallCol = Color.Lerp(Palette.Wall, Palette.Danger, Mathf.Clamp01(_wallGlow / 0.45f) * 0.85f);
            _ground.StrokeArc(0f, RW, RW + 4f, a0, a1, 12f, wallCol);
            float stepA = 22f / RW;
            for (float a = Mathf.Ceil(a0 / stepA) * stepA; a < a1; a += stepA)
            {
                _ground.Push(0f, RW, a);
                _ground.FillRect(RW + 9f, -3.5f, 6f, 7f, wallCol);
                _ground.Pop();
            }
            if (g.Flash > 0f)
                _ground.StrokeArc(0f, RW, RW + 4f, a0, a1, 12f, Palette.WithAlpha(Palette.Danger, Mathf.Min(1f, g.Flash * 2.4f)));

        }

        void DrawLines(Game g, ViewState v)
        {
            foreach (var l in g.Lines)
            {
                if (l.Points.Count < 2) continue;
                var col = Palette.LineColor(l.ColorIndex);
                pts.Clear();
                foreach (var p in l.Points) pts.Add(p.Pos.ToV());
                _lines.Polyline(pts, 7f, col, l.Loop, true);
                g.Handles(l, _handles);
                foreach (var h in _handles)
                {
                    _lines.Line(h.endPos.x, h.endPos.y, h.pos.x, h.pos.y, 7f, col, true);
                    _lines.Line(h.pos.x - h.dir.y * 10f, h.pos.y + h.dir.x * 10f, h.pos.x + h.dir.y * 10f, h.pos.y - h.dir.x * 10f, 6f, col, true);
                }
            }
            if (!v.Dragging || v.DragLine == null) return;
            var dl = v.DragLine;
            var ids = dl.Nodes;
            if (v.DragSeg >= 0)
            {
                DrawInsertPreview(g, v, dl);
                return;
            }
            bool tail = v.DragEnd == LineEnd.Tail;
            var endN = g.NodeById(tail ? ids[ids.Count - 1] : ids[0]);
            if (endN == null) return;
            int other = tail ? ids[0] : ids[ids.Count - 1];
            var tn = v.DragTarget;
            bool closing = tn != null && ids.Count >= 3 && tn.Id == other;
            Vec2 to = tn != null && (g.CanJoin(dl, tn) || closing) ? tn.Pos : v.DragPos;
            // 놓았을 때 Rebuild가 고를 꺾임점과 같게 하려고 목표를 포함한 무게중심을 씀
            // 순환을 닫을 때는 목표가 이미 노선에 있어 두 번 세지 않음
            _centroidPs.Clear();
            foreach (int id in ids) _centroidPs.Add(g.NodeById(id).Pos);
            if (!closing) _centroidPs.Add(to);
            var center = Game.Centroid(_centroidPs);
            // Rebuild와 같은 순서로 구간을 넘김. 머리 쪽은 새 정거장에서 기존 머리로 감
            Vec2 from = tail ? endN.Pos : to, dest = tail ? to : endN.Pos;
            pts.Clear();
            pts.Add(endN.Pos.ToV());
            if (Game.Bend(from, dest, center, out var bp)) pts.Add(bp.ToV());
            pts.Add(to.ToV());
            _lines.Polyline(pts, 7f, Palette.WithAlpha(Palette.LineColor(dl.ColorIndex), 0.55f), false, false);
        }

        // 구간을 끌 때: 앞 정거장 -> 커서(또는 끼울 정거장) -> 뒤 정거장을 반투명으로 그림
        void DrawInsertPreview(Game g, ViewState v, Line dl)
        {
            var ids = dl.Nodes;
            if (v.DragSeg >= Game.SegmentCount(dl)) return;
            var a = g.NodeById(ids[v.DragSeg]);
            var c = g.NodeById(ids[(v.DragSeg + 1) % ids.Count]);
            if (a == null || c == null) return;
            var tn = v.DragTarget;
            bool ok = tn != null && !ids.Contains(tn.Id) && g.CanJoin(dl, tn);
            Vec2 mid = ok ? tn.Pos : v.DragPos;
            _centroidPs.Clear();
            foreach (int id in ids) _centroidPs.Add(g.NodeById(id).Pos);
            _centroidPs.Add(mid);
            var center = Game.Centroid(_centroidPs);
            var col = Palette.WithAlpha(Palette.LineColor(dl.ColorIndex), 0.55f);
            pts.Clear();
            pts.Add(a.Pos.ToV());
            if (Game.Bend(a.Pos, mid, center, out var b0)) pts.Add(b0.ToV());
            pts.Add(mid.ToV());
            if (Game.Bend(mid, c.Pos, center, out var b1)) pts.Add(b1.ToV());
            pts.Add(c.Pos.ToV());
            _lines.Polyline(pts, 7f, col, false, false);
        }

        void DrawShots(Effects fx)
        {
            foreach (var s in fx.Shots)
                _lines.Line(s.A.x, s.A.y, s.B.x, s.B.y, 2f, Palette.WithAlpha(Palette.Ink, Mathf.Clamp01(s.Life / 0.18f)));
        }

        void DrawFx(Effects fx, WorldLabels labels, CameraFit cam)
        {
            foreach (var p in fx.Parts)
                _fx.FillRect(p.P.x - 2f, p.P.y - 2f, 4f, 4f, Palette.WithAlpha(p.Col, Mathf.Min(1f, p.Life * 2f)));
            foreach (var f in fx.Floats)
                labels.Add(cam, f.Text, f.P.x, f.P.y, f.Size > 0f ? f.Size : 15f, Palette.WithAlpha(f.Col, Mathf.Min(1f, f.Life * 1.5f)), true);
        }

        // 요새 건설 미리보기의 사거리. 건물 모양은 GhostView가 그림
        void DrawGhost(Game g, ViewState v)
        {
            if (!v.HasMode || !v.HoverInside || v.Mode != BuildKind.Fort) return;
            var col = g.CanPlace(v.Mode, v.Hover) ? Palette.Ink : Palette.Danger;
            var q = g.SnapWall(v.Hover, out float off);
            var p = off < Game.WALL_SNAP * 2.5f ? q : v.Hover;
            _fx.FillCircle(p.x, p.y, g.B.RANGE, Palette.WithAlpha(col, 0.07f), 64);
        }

        // 요새의 식량·무기 없음 경고와 창고의 노선 수 표시
        void DrawNodeLabels(Game g, CameraFit cam, WorldLabels labels)
        {
            float t = g.T;
            foreach (var n in g.Nodes)
            {
                float pop = Mathf.Min(1f, (t - n.Born) / 0.35f);
                float sc = pop < 1f ? 0.4f + 0.6f * pop + Mathf.Sin(pop * Mathf.PI) * 0.25f : 1f;
                if (n.Type == NodeType.Fort) FortLabel(n, t, sc, cam, labels);
                else if (n.Type == NodeType.Warehouse) WarehouseLabel(g, n, sc, cam, labels);
            }
        }

        static void FortLabel(Node n, float t, float sc, CameraFit cam, WorldLabels labels)
        {
            bool starving = n.Food <= 0, dry = n.Weapon <= 0 && n.Ammo < 1f;
            if (!starving && !dry) return;
            float a = 0.5f + 0.5f * Mathf.Sin(t * 7f);
            labels.Add(cam, starving ? "식량 없음" : "무기 없음", n.Pos.x, n.Pos.y - 22f * sc, 13f * sc, Palette.WithAlpha(Palette.Danger, a), true);
        }

        static void WarehouseLabel(Game g, Node n, float sc, CameraFit cam, WorldLabels labels)
        {
            int nl = g.LineCountWith(n.Id);
            int lim = Mathf.Max(0, Mathf.RoundToInt(g.B.WH_MAX_LINES));
            if (lim > 0 ? nl < lim : nl < 2)
                labels.Add(cam, lim > 0 ? $"노선 {nl}/{lim}" : $"노선 {nl}", n.Pos.x, n.Pos.y + 28f * sc, 11f * sc, Palette.Muted);
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
