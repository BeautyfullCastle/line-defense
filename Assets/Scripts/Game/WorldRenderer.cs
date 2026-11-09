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
        private const float WallRadius = Game.WallRadius;
        private const float MaxHalfWidth = Game.MaxHalfWidth;
        private const int ExpandEvery = Game.ExpandEvery;

        // 그리는 순서. 스프라이트 프리팹의 정렬 순서(굴 5, 정거장 20대, 마차 30대, 몬스터 40대, 건설 미리보기 55)와 맞춤
        private const int GroundOrder = 0, LinesOrder = 10, FxOrder = 50;

        private readonly ShapeBatch _ground = new ShapeBatch("Ground");
        private readonly ShapeBatch _lines = new ShapeBatch("Lines");
        private readonly ShapeBatch _effectsBatch = new ShapeBatch("Fx");
        private readonly WorldSprites _sprites;
        private readonly List<Vector2> _points = new List<Vector2>(64);
        private readonly List<Vector2> _secondPoints = new List<Vector2>(64);
        private readonly List<Vec2> _centroidPs = new List<Vec2>(16);
        private float _wallGlow;
        private readonly List<(Vec2 Position, Vec2 EndPosition, Vec2 Direction, LineEnd End, Line Line)> _handles = new List<(Vec2 Position, Vec2 EndPosition, Vec2 Direction, LineEnd End, Line Line)>(2);

        /// <summary>메시 렌더러 세 개와 스프라이트 루트를 만듭니다.</summary>
        public WorldRenderer(Transform parent, WorldArt worldArt)
        {
            var root = new GameObject("World").transform;
            root.SetParent(parent, false);
            MeshLayer(root, _ground, worldArt.MeshMaterial, GroundOrder);
            MeshLayer(root, _lines, worldArt.MeshMaterial, LinesOrder);
            MeshLayer(root, _effectsBatch, worldArt.MeshMaterial, FxOrder);
            _sprites = new WorldSprites(worldArt, root);
        }

        private static void MeshLayer(Transform parent, ShapeBatch batch, Material material, int order)
        {
            var layerObject = new GameObject(batch.Mesh.name);
            layerObject.transform.SetParent(parent, false);
            layerObject.AddComponent<MeshFilter>().sharedMesh = batch.Mesh;
            var meshRenderer = layerObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = order;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        /// <summary>새 판을 시작할 때 이전 판의 스프라이트를 지웁니다.</summary>
        public void Clear() => _sprites.Clear();

        /// <summary>현재 상태를 한 프레임 분량으로 그립니다.</summary>
        public void Render(Game game, ViewState view, CameraFit cameraFit, Effects effects, WorldLabels labels)
        {
            _ground.Begin();
            _lines.Begin();
            _effectsBatch.Begin();
            labels.Begin();
            DrawTerrain(game, view, cameraFit, labels);
            DrawRanges(game);
            _wallGlow = effects.WallGlow;
            DrawWallAndRoutes(game, cameraFit, labels);
            DrawLines(game, view);
            DrawShots(effects);
            DrawNodeLabels(game, cameraFit, labels);
            DrawFx(effects, labels, cameraFit);
            DrawGhost(game, view);
            _sprites.Sync(game, view);
            _ground.End();
            _lines.End();
            _effectsBatch.End();
            labels.End();
        }

        private static float TopY(float x) => WallRadius - Mathf.Sqrt(Mathf.Max(0f, WallRadius * WallRadius - x * x));

        private void DrawTerrain(Game game, ViewState view, CameraFit cameraFit, WorldLabels labels)
        {
            float halfWidth = view.DisplayHalfWidth, depth = view.DisplayDepth;
            // 성벽 원 전체를 안개색으로 깔고 개척지만 땅색으로 덮음
            _ground.FillCircle(0, WallRadius, WallRadius, Palette.Fog, 220);
            _points.Clear();
            const int ArcSteps = 24;
            for (int i = 0; i <= ArcSteps; i++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, i / (float)ArcSteps);
                _points.Add(new Vector2(x, TopY(x)));
            }
            _points.Add(new Vector2(halfWidth, depth));
            _points.Add(new Vector2(-halfWidth, depth));
            _ground.FillPoly(_points, Palette.Ground);

            for (float gridX = -Mathf.Ceil(halfWidth / 40f) * 40f; gridX <= halfWidth; gridX += 40f)
            {
                if (gridX < -halfWidth)
                {
                    continue;
                }

                _ground.Line(gridX, TopY(gridX), gridX, depth, 1f, Palette.Grid);
            }
            for (float gridY = 0f; gridY <= depth; gridY += 40f)
            {
                float half = Mathf.Min(halfWidth, Mathf.Sqrt(Mathf.Max(0f, WallRadius * WallRadius - (gridY - WallRadius) * (gridY - WallRadius))));
                _ground.Line(-half, gridY, half, gridY, 1f, Palette.Grid);
            }

            _secondPoints.Clear();
            _secondPoints.Add(new Vector2(-halfWidth, depth));
            for (int i = 0; i <= ArcSteps; i++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, i / (float)ArcSteps);
                _secondPoints.Add(new Vector2(x, TopY(x)));
            }
            _secondPoints.Add(new Vector2(halfWidth, depth));
            _ground.Dashed(_secondPoints, 1.5f, Palette.WithAlpha(Palette.Muted, 0.55f), 10f, 8f, true);

            if (depth + 34f < cameraFit.WorldBottom)
            {
                string unexploredLabel = game.HalfWidth < MaxHalfWidth ? $"미개척지 · {ExpandEvery}웨이브마다 개척" : "미개척지";
                labels.Add(cameraFit, unexploredLabel, 0f, depth + 30f, 12f, Palette.Muted);
            }
        }

        private void DrawRanges(Game game)
        {
            float range = game.Balance.RANGE;
            foreach (var fort in game.Nodes)
            {
                if (fort.Type != NodeType.Fort)
                {
                    continue;
                }

                _ground.FillCircle(fort.Position.X, fort.Position.Y, range, Palette.WithAlpha(Palette.Ink, 0.06f), 64);
                _ground.DashedCircle(fort.Position.X, fort.Position.Y, range, 1.2f, Palette.WithAlpha(Palette.Ink, 0.22f), 6f, 6f);
            }
        }

        private void DrawWallAndRoutes(Game game, CameraFit cameraFit, WorldLabels labels)
        {
            float t = game.ElapsedTime;
            float visibleHalfWidth = cameraFit.HalfWidth + 40f;
            float span = Mathf.Asin(Mathf.Min(1f, visibleHalfWidth / WallRadius));
            float startAngle = -Mathf.PI * 0.5f - span, endAngle = -Mathf.PI * 0.5f + span;

            foreach (var route in game.Routes)
            {
                float directionX = Mathf.Cos(route.Angle), directionY = Mathf.Sin(route.Angle);
                float fresh = Mathf.Max(0f, 1f - (t - route.Born) / 6f);
                _points.Clear();
                _points.Add(new Vector2(directionX * (WallRadius + 16f), WallRadius + directionY * (WallRadius + 16f)));
                _points.Add(new Vector2(directionX * (WallRadius + Game.DenDistance), WallRadius + directionY * (WallRadius + Game.DenDistance)));
                _ground.Dashed(_points, 3f, Palette.WithAlpha(Palette.Danger, 0.3f + fresh * 0.5f), 8f, 8f);
                // 굴 그림은 DenView가 그림
                float dx = directionX * (WallRadius + Game.DenDistance + 14f), dy = WallRadius + directionY * (WallRadius + Game.DenDistance + 14f);
                float threat = game.RouteThreatForDisplay(route);
                string label = threat < 1f ? $"신규 · 위협 {Mathf.RoundToInt(threat * 100f)}%" : "위협 100%";
                labels.Add(cameraFit, label, dx, dy - 20f, 12f, Palette.Danger, true);
            }

            // 피해를 받으면 성벽 전체가 잠깐 붉게 물듦
            var wallColor = Color.Lerp(Palette.Wall, Palette.Danger, Mathf.Clamp01(_wallGlow / 0.45f) * 0.85f);
            _ground.StrokeArc(0f, WallRadius, WallRadius + 4f, startAngle, endAngle, 12f, wallColor);
            float angleStep = 22f / WallRadius;
            for (float angle = Mathf.Ceil(startAngle / angleStep) * angleStep; angle < endAngle; angle += angleStep)
            {
                _ground.Push(0f, WallRadius, angle);
                _ground.FillRect(WallRadius + 9f, -3.5f, 6f, 7f, wallColor);
                _ground.Pop();
            }
            if (game.Flash > 0f)
            {
                _ground.StrokeArc(0f, WallRadius, WallRadius + 4f, startAngle, endAngle, 12f, Palette.WithAlpha(Palette.Danger, Mathf.Min(1f, game.Flash * 2.4f)));
            }
        }

        private void DrawLines(Game game, ViewState view)
        {
            foreach (var line in game.Lines)
            {
                if (line.Points.Count < 2)
                {
                    continue;
                }

                var color = Palette.LineColor(line.ColorIndex);
                _points.Clear();
                foreach (var point in line.Points)
                {
                    _points.Add(point.Position.ToVector2());
                }

                _lines.Polyline(_points, 7f, color, line.Loop, true);
                game.Handles(line, _handles);
                foreach (var handle in _handles)
                {
                    _lines.Line(handle.EndPosition.X, handle.EndPosition.Y, handle.Position.X, handle.Position.Y, 7f, color, true);
                    _lines.Line(handle.Position.X - handle.Direction.Y * 10f, handle.Position.Y + handle.Direction.X * 10f, handle.Position.X + handle.Direction.Y * 10f, handle.Position.Y - handle.Direction.X * 10f, 6f, color, true);
                }
            }
            if (!view.Dragging || view.DragLine == null)
            {
                return;
            }

            var dragLine = view.DragLine;
            var nodeIds = dragLine.Nodes;
            if (view.DragSegment >= 0)
            {
                DrawInsertPreview(game, view, dragLine);
                return;
            }
            bool tail = view.DragEnd == LineEnd.Tail;
            var endN = game.NodeById(tail ? nodeIds[nodeIds.Count - 1] : nodeIds[0]);
            if (endN == null)
            {
                return;
            }

            int other = tail ? nodeIds[0] : nodeIds[nodeIds.Count - 1];
            var target = view.DragTarget;
            bool closing = target != null && nodeIds.Count >= 3 && target.Id == other;
            Vec2 targetPosition = target != null && (game.CanJoin(dragLine, target) || closing) ? target.Position : view.DragPosition;
            // 놓았을 때 Rebuild가 고를 꺾임점과 같게 하려고 목표를 포함한 무게중심을 씀
            // 순환을 닫을 때는 목표가 이미 노선에 있어 두 번 세지 않음
            _centroidPs.Clear();
            foreach (int id in nodeIds)
            {
                _centroidPs.Add(game.NodeById(id).Position);
            }

            if (!closing)
            {
                _centroidPs.Add(targetPosition);
            }

            var center = Game.Centroid(_centroidPs);
            // Rebuild와 같은 순서로 구간을 넘김. 머리 쪽은 새 정거장에서 기존 머리로 감
            Vec2 from = tail ? endN.Position : targetPosition, dest = tail ? targetPosition : endN.Position;
            _points.Clear();
            _points.Add(endN.Position.ToVector2());
            if (Game.Bend(from, dest, center, out var bendPoint))
            {
                _points.Add(bendPoint.ToVector2());
            }

            _points.Add(targetPosition.ToVector2());
            _lines.Polyline(_points, 7f, Palette.WithAlpha(Palette.LineColor(dragLine.ColorIndex), 0.55f), false, false);
        }

        // 구간을 끌 때: 앞 정거장 -> 커서(또는 끼울 정거장) -> 뒤 정거장을 반투명으로 그림
        private void DrawInsertPreview(Game game, ViewState view, Line line)
        {
            var nodeIds = line.Nodes;
            if (view.DragSegment >= Game.SegmentCount(line))
            {
                return;
            }

            var node = game.NodeById(nodeIds[view.DragSegment]);
            var nextNode = game.NodeById(nodeIds[(view.DragSegment + 1) % nodeIds.Count]);
            if (node == null || nextNode == null)
            {
                return;
            }

            var target = view.DragTarget;
            bool canInsert = target != null && !nodeIds.Contains(target.Id) && game.CanJoin(line, target);
            Vec2 insertPosition = canInsert ? target.Position : view.DragPosition;
            _centroidPs.Clear();
            foreach (int id in nodeIds)
            {
                _centroidPs.Add(game.NodeById(id).Position);
            }

            _centroidPs.Add(insertPosition);
            var center = Game.Centroid(_centroidPs);
            var color = Palette.WithAlpha(Palette.LineColor(line.ColorIndex), 0.55f);
            _points.Clear();
            _points.Add(node.Position.ToVector2());
            if (Game.Bend(node.Position, insertPosition, center, out var firstBend))
            {
                _points.Add(firstBend.ToVector2());
            }

            _points.Add(insertPosition.ToVector2());
            if (Game.Bend(insertPosition, nextNode.Position, center, out var secondBend))
            {
                _points.Add(secondBend.ToVector2());
            }

            _points.Add(nextNode.Position.ToVector2());
            _lines.Polyline(_points, 7f, color, false, false);
        }

        private void DrawShots(Effects effects)
        {
            foreach (var shot in effects.Shots)
            {
                _lines.Line(shot.A.x, shot.A.y, shot.B.x, shot.B.y, 2f, Palette.WithAlpha(Palette.Ink, Mathf.Clamp01(shot.Life / 0.18f)));
            }
        }

        private void DrawFx(Effects effects, WorldLabels labels, CameraFit cameraFit)
        {
            foreach (var particle in effects.Parts)
            {
                _effectsBatch.FillRect(particle.P.x - 2f, particle.P.y - 2f, 4f, 4f, Palette.WithAlpha(particle.Col, Mathf.Min(1f, particle.Life * 2f)));
            }

            foreach (var floatText in effects.Floats)
            {
                labels.Add(cameraFit, floatText.Text, floatText.P.x, floatText.P.y, floatText.Size > 0f ? floatText.Size : 15f, Palette.WithAlpha(floatText.Col, Mathf.Min(1f, floatText.Life * 1.5f)), true);
            }
        }

        // 요새 건설 미리보기의 사거리. 건물 모양은 GhostView가 그림
        private void DrawGhost(Game game, ViewState view)
        {
            if (!view.HasMode || !view.HoverInside || view.Mode != BuildKind.Fort)
            {
                return;
            }

            var color = game.CanPlace(view.Mode, view.Hover) ? Palette.Ink : Palette.Danger;
            var snappedPosition = game.SnapWall(view.Hover, out float off);
            var previewPosition = off < Game.WallSnap * 2.5f ? snappedPosition : view.Hover;
            _effectsBatch.FillCircle(previewPosition.X, previewPosition.Y, game.Balance.RANGE, Palette.WithAlpha(color, 0.07f), 64);
        }

        // 요새의 식량·무기 없음 경고와 창고의 노선 수 표시
        private void DrawNodeLabels(Game game, CameraFit cameraFit, WorldLabels labels)
        {
            float t = game.ElapsedTime;
            foreach (var node in game.Nodes)
            {
                float popProgress = Mathf.Min(1f, (t - node.Born) / 0.35f);
                float popScale = popProgress < 1f ? 0.4f + 0.6f * popProgress + Mathf.Sin(popProgress * Mathf.PI) * 0.25f : 1f;
                if (node.Type == NodeType.Fort)
                {
                    FortLabel(node, t, popScale, cameraFit, labels);
                }
                else if (node.Type == NodeType.Warehouse)
                {
                    WarehouseLabel(game, node, popScale, cameraFit, labels);
                }
            }
        }

        private static void FortLabel(Node node, float t, float popScale, CameraFit cameraFit, WorldLabels labels)
        {
            bool starving = node.Food <= 0, outOfWeapons = node.Weapon <= 0 && node.Ammo < 1f;
            if (!starving && !outOfWeapons)
            {
                return;
            }

            float alpha = 0.5f + 0.5f * Mathf.Sin(t * 7f);
            labels.Add(cameraFit, starving ? "식량 없음" : "무기 없음", node.Position.X, node.Position.Y - 22f * popScale, 13f * popScale, Palette.WithAlpha(Palette.Danger, alpha), true);
        }

        private static void WarehouseLabel(Game game, Node node, float popScale, CameraFit cameraFit, WorldLabels labels)
        {
            int lineCount = game.LineCountWith(node.Id);
            int lineLimit = Mathf.Max(0, Mathf.RoundToInt(game.Balance.WH_MAX_LINES));
            if (lineLimit > 0 ? lineCount < lineLimit : lineCount < 2)
            {
                labels.Add(cameraFit, lineLimit > 0 ? $"노선 {lineCount}/{lineLimit}" : $"노선 {lineCount}", node.Position.X, node.Position.Y + 28f * popScale, 11f * popScale, Palette.Muted);
            }
        }

        /// <summary>아직 쓰지 않은 가장 앞 노선 색 인덱스입니다. 모두 쓰였으면 0입니다.</summary>
        public static int FreeColorIndex(Game game)
        {
            for (int i = 0; i < Game.LineColorCount; i++)
            {
                bool used = false;
                foreach (var line in game.Lines)
                {
                    if (line.ColorIndex == i)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
