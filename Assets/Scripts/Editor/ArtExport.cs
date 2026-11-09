using System;
using System.Collections.Generic;
using System.IO;
using LineDefense.View;
using LineDefense.View.Ui;
using UnityEditor;
using UnityEngine;

namespace LineDefense.Editor
{
    // 코드로 그리던 도형을 PNG 스프라이트로 한 번 뽑아 내는 이전용 도구. 뽑은 뒤에는 PNG가 원본이 됨
    public static class ArtExport
    {
        const int Ppu = 4;
        const string WorldDir = "Assets/Art/Sprites/World";
        const string IconDir = "Assets/Art/Sprites/Icons";
        const string UiDir = "Assets/Art/Sprites/UI";

        static readonly string[] IconKeys =
            { "circle", "circleRing", "tri", "triRing", "disc", "stable", "fort", "wall", "cart", "speed", "wh", "whcap", "cap", "gold", "capup", "box" };

        struct Def
        {
            public string Name;
            public Rect Area;       // y 아래 방향 월드 좌표의 그릴 영역
            public float Border;    // 9분할 테두리(월드 단위). 0이면 없음
            public Action<ShapeBatch> Draw;
        }

        static readonly List<Vector2> Pts = new List<Vector2>();

        static Def D(string name, float x, float y, float w, float h, Action<ShapeBatch> draw, float border = 0f) =>
            new Def { Name = name, Area = new Rect(x, y, w, h), Border = border, Draw = draw };

        static Def Centered(string name, float half, Action<ShapeBatch> draw) => D(name, -half, -half, half * 2f, half * 2f, draw);

        static IEnumerable<Def> WorldDefs()
        {
            var w = Color.white;
            // 기본 도형. 크기는 변환 배율로 맞춤
            yield return Centered("Disc", 17f, b => b.FillCircle(0, 0, 16f, w, 64));
            yield return Centered("Tri", 17f, b => b.FillShape(ShapeKind.Triangle, 0, 0, 16f, w));
            yield return Centered("Hex", 17f, b => b.FillShape(ShapeKind.Hex, 0, 0, 16f, w));
            yield return D("Square", -2f, -2f, 4f, 4f, b => b.FillRect(-2f, -2f, 4f, 4f, w));
            yield return Centered("Diamond", 4f, b => b.Quad(new Vector2(0, -3f), new Vector2(3f, 0), new Vector2(0, 3f), new Vector2(-3f, 0), w));
            yield return D("Round3", -6f, -6f, 12f, 12f, b => b.FillRoundRect(-6f, -6f, 12f, 12f, 3f, w), 3.5f);
            yield return D("Round3Frame", -6f, -6f, 12f, 12f, b => b.StrokeRoundRect(-5f, -5f, 10f, 10f, 3f, 2f, w), 4f);
            yield return D("Round5", -8f, -8f, 16f, 16f, b => b.FillRoundRect(-8f, -8f, 16f, 16f, 5f, w), 5.5f);

            // 생산 건물
            yield return Centered("FarmRing", 16f, b => b.StrokeShape(ShapeKind.Circle, 0, 0, 12f, 5f, w));
            yield return Centered("ForgeRing", 18f, b => b.StrokeShape(ShapeKind.Triangle, 0, 0, 12f, 5f, w));
            yield return Centered("Ring", 22f, b => b.StrokeCircle(0, 0, 20f, 2f, w));
            yield return Centered("DenRing", 22f, b => b.StrokeCircle(0, 0, 20f, 3f, w));

            // 마굿간: 몸체(노선 색)와 문(땅 색)
            yield return D("StableBody", -17f, -18f, 34f, 30f, b =>
            {
                b.FillRoundRect(-12f, -8f, 24f, 19f, 3f, w);
                Pts.Clear(); Pts.Add(new Vector2(-15f, -6f)); Pts.Add(new Vector2(0f, -16f)); Pts.Add(new Vector2(15f, -6f));
                b.Polyline(Pts, 4f, w, false, true);
            });
            yield return D("StableDoor", -4.5f, 1.5f, 9f, 10f, b => b.FillRoundRect(-4f, 2f, 8f, 9f, 2f, w));

            // 창고: 바닥(땅 색)과 테두리·X(먹색)
            yield return Centered("WarehouseFill", 14f, b => b.FillRoundRect(-13f, -13f, 26f, 26f, 3f, w));
            yield return Centered("WarehouseFrame", 16f, b =>
            {
                b.StrokeRoundRect(-13f, -13f, 26f, 26f, 3f, 4f, w);
                b.Line(-8f, -8f, 8f, 8f, 2.5f, w);
                b.Line(8f, -8f, -8f, 8f, 2.5f, w);
            });
            yield return Centered("WarehouseGhost", 16f, b => b.StrokeRoundRect(-13f, -13f, 26f, 26f, 3f, 4f, w));

            // 선택 표시 점선 고리
            yield return Centered("CfgRing24", 26f, b => b.DashedCircle(0f, 0f, 24f, 2f, w, 4f, 3f));
            yield return Centered("CfgRing28", 30f, b => b.DashedCircle(0f, 0f, 28f, 2f, w, 4f, 3f));

            // 요새 강화 표시
            yield return D("Chevrons", 16f, -4f, 10f, 8f, b =>
            {
                Pts.Clear(); Pts.Add(new Vector2(18f, -3f)); Pts.Add(new Vector2(21f, 0f)); Pts.Add(new Vector2(18f, 3f));
                b.Polyline(Pts, 1.6f, w, false, false);
                Pts.Clear(); Pts.Add(new Vector2(21.5f, -3f)); Pts.Add(new Vector2(24.5f, 0f)); Pts.Add(new Vector2(21.5f, 3f));
                b.Polyline(Pts, 1.6f, w, false, false);
            });

            // 자원 칸: 빈 칸 테두리, 요청 칸 점선. 반지름 3.3 기준이며 배율로 맞춤
            yield return Centered("PipRingCircle", 4.5f, b => b.StrokeShape(ShapeKind.Circle, 0, 0, 3.3f * 0.85f, 1f, w));
            yield return Centered("PipRingTri", 5f, b => b.StrokeShape(ShapeKind.Triangle, 0, 0, 3.3f * 0.85f, 1f, w));
            yield return Centered("PipReqCircle", 5f, b => b.DashedShape(ShapeKind.Circle, 0, 0, 3.3f * 0.95f, 1.3f, w, 1.6f, 1.4f));
            yield return Centered("PipReqTri", 5.5f, b => b.DashedShape(ShapeKind.Triangle, 0, 0, 3.3f * 0.95f, 1.3f, w, 1.6f, 1.4f));
            yield return Centered("PipOverCircle", 6f, b => b.StrokeShape(ShapeKind.Circle, 0, 0, 4.4f, 1.2f, w));
            yield return Centered("PipOverTri", 6.5f, b => b.StrokeShape(ShapeKind.Triangle, 0, 0, 4.4f, 1.2f, w));

            // 마차 화살표: 채움(노선 색)과 테두리(땅 색)
            yield return D("ArrowFill", -1f, -6f, 9f, 12f, b => b.Tri(0f, -5f, 7f, 0f, 0f, 5f, w));
            yield return D("ArrowFrame", -1f, -7f, 10f, 14f, b =>
            {
                Pts.Clear(); Pts.Add(new Vector2(0f, -5f)); Pts.Add(new Vector2(7f, 0f)); Pts.Add(new Vector2(0f, 5f));
                b.Polyline(Pts, 1.5f, w, true, false);
            });

            // 몬스터 몸체(별 모양)
            yield return Centered("Monster", 12f, b =>
            {
                Pts.Clear();
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2f / 16f, r = i % 2 == 1 ? 6.5f : 10.5f;
                    Pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
                b.FillPoly(Pts, w);
            });

            // 침투로 끝의 굴과 침공 예고 화살표
            yield return D("DenMouth", -7f, -4f, 14f, 8f, b => b.FillSector(0f, 3f, 6f, Mathf.PI, Mathf.PI * 2f, w));
            yield return D("RouteArrow", -8f, -11f, 18f, 22f, b =>
            {
                b.Quad(new Vector2(-7, 0), new Vector2(5, -10), new Vector2(9, -10), new Vector2(-1, 0), w);
                b.Quad(new Vector2(-7, 0), new Vector2(5, 10), new Vector2(9, 10), new Vector2(-1, 0), w);
            });
        }

        [MenuItem("Tools/Line Defense/Export Art (one-time)")]
        public static void ExportAll()
        {
            Directory.CreateDirectory(WorldDir);
            Directory.CreateDirectory(IconDir);
            Directory.CreateDirectory(UiDir);
            var written = new List<(string Path, float Ppu, Vector2 Pivot, Vector4 Border)>();
            foreach (var d in WorldDefs()) written.Add(RenderWorld(d));
            foreach (string k in IconKeys)
            {
                var tex = IconFactory.Get(k == "box" ? "__box" : k).texture;
                string p = $"{IconDir}/{Pascal(k)}.png";
                File.WriteAllBytes(p, tex.EncodeToPNG());
                written.Add((p, 100f, new Vector2(0.5f, 0.5f), Vector4.zero));
            }
            var round = UiFactory.Round;
            string rp = $"{UiDir}/Round.png";
            File.WriteAllBytes(rp, round.texture.EncodeToPNG());
            written.Add((rp, 100f, new Vector2(0.5f, 0.5f), round.border));

            AssetDatabase.Refresh();
            foreach (var w in written) Configure(w.Path, w.Ppu, w.Pivot, w.Border);
            AssetDatabase.SaveAssets();
            Debug.Log($"ArtExport: {written.Count} sprites");
        }

        static string Pascal(string k) => char.ToUpperInvariant(k[0]) + k.Substring(1);

        static (string, float, Vector2, Vector4) RenderWorld(Def d)
        {
            int pw = Mathf.CeilToInt(d.Area.width * Ppu), ph = Mathf.CeilToInt(d.Area.height * Ppu);
            var batch = new ShapeBatch(d.Name);
            batch.Begin();
            d.Draw(batch);
            batch.End();

            var go = new GameObject("ArtExport") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<MeshFilter>().sharedMesh = batch.Mesh;
            var mat = new Material(Shader.Find("LineDefense/VertexColorUnlit"));
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = 31;
            var camGo = new GameObject("ArtExportCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = d.Area.height / 2f;
            cam.aspect = d.Area.width / d.Area.height;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 1 << 31;
            cam.transform.position = new Vector3(d.Area.x + d.Area.width / 2f, -(d.Area.y + d.Area.height / 2f), -10f);
            var rt = new RenderTexture(pw, ph, 0, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var read = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
            RenderTexture.active = null;
            // 흰 도형을 검은 바탕에 그렸으므로 밝기가 곧 덮인 비율. 색은 흰색, 알파는 밝기
            var px = read.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, px[i].r);
            read.SetPixels32(px);
            read.Apply();
            string path = $"{WorldDir}/{d.Name}.png";
            File.WriteAllBytes(path, read.EncodeToPNG());

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(read);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(mat);
            UnityEngine.Object.DestroyImmediate(batch.Mesh);

            // 스프라이트 피벗은 그릴 때의 원점
            var pivot = new Vector2(-d.Area.x / d.Area.width, (d.Area.y + d.Area.height) / d.Area.height);
            float bpx = d.Border * Ppu;
            return (path, Ppu, pivot, new Vector4(bpx, bpx, bpx, bpx));
        }

        static void Configure(string path, float ppu, Vector2 pivot, Vector4 border)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteBorder = border;
            s.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(s);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }
}
