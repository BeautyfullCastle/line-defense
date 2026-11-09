using System.IO;
using LineDefense.Core;
using LineDefense.View;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace LineDefense.Editor
{
    // 코드로 만들던 월드 그림을 프리팹으로 한 번 만들어 두는 이전용 도구. 만든 뒤에는 프리팹이 원본이 됨
    public static class PrefabBuild
    {
        const string WorldSprites = "Assets/Art/Sprites/World";
        const string WorldPrefabs = "Assets/Prefabs/World";
        const string ArtPath = "Assets/Art/WorldArt.asset";
        const string MatPath = "Assets/Art/Materials/WorldMesh.mat";

        static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{WorldSprites}/{name}.png");

        static SpriteRenderer SR(Transform parent, string name, string sprite, int order, Vector2 pos, float scale = 1f, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite == null ? null : S(sprite);
            r.sortingOrder = order;
            r.color = color ?? Color.white;
            return r;
        }

        static SpriteRenderer Sliced(Transform parent, string name, string sprite, int order, Vector2 size, Color color)
        {
            var r = SR(parent, name, sprite, order, Vector2.zero, 1f, color);
            r.drawMode = SpriteDrawMode.Sliced;
            r.size = size;
            return r;
        }

        static PipGroup Pips(Transform parent, string name, WorldArt art, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PipGroup>();
            var so = new SerializedObject(p);
            so.FindProperty("_art").objectReferenceValue = art;
            so.FindProperty("_order").intValue = order;
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Save<T>(GameObject root, string name) where T : Component
        {
            string path = $"{WorldPrefabs}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<T>();
        }

        static GameObject Root<T>(string name, out T view) where T : Component
        {
            var go = new GameObject(name);
            view = go.AddComponent<T>();
            return go;
        }

        [MenuItem("Tools/Line Defense/Build World Prefabs (one-time)")]
        public static void BuildWorld()
        {
            Directory.CreateDirectory(WorldPrefabs);
            Directory.CreateDirectory(Path.GetDirectoryName(MatPath));

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("LineDefense/VertexColorUnlit")) { name = "WorldMesh" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            var art = AssetDatabase.LoadAssetAtPath<WorldArt>(ArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<WorldArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }
            art.MeshMaterial = mat;
            art.Disc = S("Disc"); art.Tri = S("Tri");
            art.PipRingCircle = S("PipRingCircle"); art.PipRingTri = S("PipRingTri");
            art.PipReqCircle = S("PipReqCircle"); art.PipReqTri = S("PipReqTri");
            art.PipOverCircle = S("PipOverCircle"); art.PipOverTri = S("PipOverTri");
            art.Square = S("Square"); art.Diamond = S("Diamond"); art.Chevrons = S("Chevrons");
            art.Hex = S("Hex"); art.Round3 = S("Round3"); art.WarehouseGhost = S("WarehouseGhost");

            // 자원 칸 한 개
            var pipGo = new GameObject("Pip");
            pipGo.AddComponent<SpriteRenderer>().sprite = art.Disc;
            art.Pip = Save<SpriteRenderer>(pipGo, "Pip");

            art.Stable = BuildStable(art);
            art.Farm = BuildProducer(art, "Farm", Res.Food, "Disc", "FarmRing", Palette.Food);
            art.Forge = BuildProducer(art, "Forge", Res.Weapon, "Tri", "ForgeRing", Palette.Weapon);
            art.Warehouse = BuildWarehouse(art);
            art.Fort = BuildFort(art);
            art.Cart = BuildCart(art);
            art.Monster = BuildMonster();
            art.Den = BuildDen();
            art.Ghost = BuildGhost(art);
            EditorUtility.SetDirty(art);

            BuildAtlas("Assets/Art/World.spriteatlasv2", WorldSprites);
            BuildAtlas("Assets/Art/Ui.spriteatlasv2", "Assets/Art/Sprites/Icons", "Assets/Art/Sprites/UI");
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            AssetDatabase.SaveAssets();
            Debug.Log("PrefabBuild: world prefabs done");
        }

        static StableView BuildStable(WorldArt art)
        {
            var go = Root<StableView>("Stable", out var v);
            var t = go.transform;
            var body = SR(t, "Body", "StableBody", 21, Vector2.zero, 1f, Palette.LineColor(0));
            var door = SR(t, "Door", "StableDoor", 22, Vector2.zero, 1f, Palette.Ground);
            var cfg = SR(t, "CfgRing", "CfgRing24", 22, Vector2.zero, 1f, Palette.Ink);
            cfg.gameObject.SetActive(false);
            var pips = Pips(t, "Pips", art, 23);
            Set(v, "_body", body); Set(v, "_door", door); Set(v, "_cfg", cfg); Set(v, "_pips", pips);
            return Save<StableView>(go, "Stable");
        }

        static ProducerView BuildProducer(WorldArt art, string name, Res res, string fill, string ring, Color col)
        {
            var go = Root<ProducerView>(name, out var v);
            var t = go.transform;
            var full = SR(t, "FullRing", "Ring", 20, Vector2.zero, 21f / 20f, col);
            full.gameObject.SetActive(false);
            var f = SR(t, "Fill", fill, 21, Vector2.zero, 12f / 16f, Palette.Ground);
            var r = SR(t, "Ring", ring, 22, Vector2.zero, 1f, col);
            var pips = Pips(t, "Pips", art, 23);
            var so = new SerializedObject(v);
            so.FindProperty("_res").enumValueIndex = (int)res;
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(v, "_fill", f); Set(v, "_ring", r); Set(v, "_full", full); Set(v, "_pips", pips);
            return Save<ProducerView>(go, name);
        }

        static WarehouseView BuildWarehouse(WorldArt art)
        {
            var go = Root<WarehouseView>("Warehouse", out var v);
            var t = go.transform;
            var pulse = SR(t, "Pulse", "Ring", 20, Vector2.zero, 1f, Palette.Ink);
            pulse.gameObject.SetActive(false);
            var fill = SR(t, "Fill", "WarehouseFill", 21, Vector2.zero, 1f, Palette.Ground);
            var frame = SR(t, "Frame", "WarehouseFrame", 22, Vector2.zero, 1f, Palette.Ink);
            var cfg = SR(t, "CfgRing", "CfgRing24", 23, Vector2.zero, 1f, Palette.Ink);
            cfg.gameObject.SetActive(false);
            var pips = Pips(t, "Pips", art, 24);
            Set(v, "_pulse", pulse); Set(v, "_fill", fill); Set(v, "_frame", frame); Set(v, "_cfg", cfg); Set(v, "_pips", pips);
            return Save<WarehouseView>(go, "Warehouse");
        }

        static FortView BuildFort(WorldArt art)
        {
            var go = Root<FortView>("Fort", out var v);
            var t = go.transform;
            var pulse = SR(t, "Pulse", "Ring", 20, Vector2.zero, 1f, Palette.Ink);
            pulse.gameObject.SetActive(false);
            var tray = Sliced(t, "Tray", "Round5", 20, new Vector2(41f, 37f), Palette.WithAlpha(Palette.Line, 0.45f));
            var body = SR(t, "Body", "Hex", 21, Vector2.zero, 15f / 16f, Palette.Ink);
            var marks = Pips(t, "Marks", art, 22);
            var cfg = SR(t, "CfgRing", "CfgRing28", 22, Vector2.zero, 1f, Palette.Ink);
            cfg.gameObject.SetActive(false);
            var dot = SR(t, "Dot", "Disc", 23, Vector2.zero, 5f / 16f, Palette.Ground);
            var pips = Pips(t, "Pips", art, 24);
            Set(v, "_pulse", pulse); Set(v, "_body", body); Set(v, "_dot", dot); Set(v, "_cfg", cfg); Set(v, "_tray", tray);
            Set(v, "_marks", marks); Set(v, "_pips", pips); Set(v, "_diamond", S("Diamond")); Set(v, "_chevrons", S("Chevrons"));
            return Save<FortView>(go, "Fort");
        }

        static CartView BuildCart(WorldArt art)
        {
            var go = Root<CartView>("Cart", out var v);
            var t = go.transform;
            var body = Sliced(t, "Body", "Round3", 30, new Vector2(20f, 20f), Palette.LineColor(0));
            var frame = Sliced(t, "Frame", "Round3Frame", 31, new Vector2(22f, 22f), Palette.Ground);
            var pips = Pips(t, "Pips", art, 32);
            var af = SR(t, "ArrowFill", "ArrowFill", 32, new Vector2(13f, 0f), 1f, Palette.LineColor(0));
            var ar = SR(t, "ArrowFrame", "ArrowFrame", 33, new Vector2(13f, 0f), 1f, Palette.Ground);
            af.gameObject.SetActive(false);
            ar.gameObject.SetActive(false);
            Set(v, "_body", body); Set(v, "_frame", frame); Set(v, "_pips", pips); Set(v, "_arrowFill", af); Set(v, "_arrowFrame", ar);
            return Save<CartView>(go, "Cart");
        }

        static MonsterView BuildMonster()
        {
            var go = Root<MonsterView>("Monster", out var v);
            var t = go.transform;
            var body = SR(t, "Body", "Monster", 40, Vector2.zero, 1f, Palette.Monster);
            var el = SR(t, "EyeL", "Disc", 41, new Vector2(-2.5f, 1f), 1.6f / 16f, Palette.Ground);
            var er = SR(t, "EyeR", "Disc", 41, new Vector2(2.5f, 1f), 1.6f / 16f, Palette.Ground);
            var hb = SR(t, "HpBack", "Square", 42, new Vector2(0f, 15.5f), 1f, Palette.Line);
            hb.transform.localScale = new Vector3(5f, 0.75f, 1f);
            var hf = SR(t, "HpFill", "Square", 43, new Vector2(0f, 15.5f), 1f, Palette.Danger);
            hf.transform.localScale = new Vector3(5f, 0.75f, 1f);
            hb.gameObject.SetActive(false);
            hf.gameObject.SetActive(false);
            Set(v, "_body", body); Set(v, "_eyeL", el); Set(v, "_eyeR", er); Set(v, "_hpBack", hb); Set(v, "_hpFill", hf);
            return Save<MonsterView>(go, "Monster");
        }

        static DenView BuildDen()
        {
            var go = Root<DenView>("Den", out var v);
            var t = go.transform;
            var den = new GameObject("Den").transform;
            den.SetParent(t, false);
            den.localPosition = new Vector3(Game.RW + Game.DEN + 14f, 0f, 0f);
            var body = SR(den, "Body", "Disc", 5, Vector2.zero, 13f / 16f, Palette.Monster);
            var mouth = SR(den, "Mouth", "DenMouth", 6, Vector2.zero, 1f, Palette.Outside);
            var fresh = SR(den, "Fresh", "DenRing", 6, Vector2.zero, 1f, Palette.Danger);
            fresh.gameObject.SetActive(false);
            var arrows = new SpriteRenderer[3];
            for (int k = 0; k < 3; k++)
            {
                arrows[k] = SR(t, "Arrow" + k, "RouteArrow", 5, new Vector2(Game.RW + 34f + k * 16f, 0f), 1f, Palette.Danger);
                arrows[k].gameObject.SetActive(false);
            }
            Set(v, "_den", den); Set(v, "_body", body); Set(v, "_mouth", mouth); Set(v, "_fresh", fresh);
            var so = new SerializedObject(v);
            var arr = so.FindProperty("_arrows");
            arr.arraySize = 3;
            for (int k = 0; k < 3; k++) arr.GetArrayElementAtIndex(k).objectReferenceValue = arrows[k];
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save<DenView>(go, "Den");
        }

        static GhostView BuildGhost(WorldArt art)
        {
            var go = Root<GhostView>("Ghost", out var v);
            var sr = SR(go.transform, "Sprite", "Hex", 55, Vector2.zero, 15f / 16f, Palette.WithAlpha(Palette.Ink, 0.55f));
            Set(v, "_sprite", sr); Set(v, "_fort", art.Hex); Set(v, "_warehouse", art.WarehouseGhost); Set(v, "_stable", art.Round3);
            return Save<GhostView>(go, "Ghost");
        }

        [MenuItem("Tools/Line Defense/Build Game Prefab And Scene (one-time)")]
        public static void BuildGame()
        {
            var go = new GameObject("Game");
            var gc = go.AddComponent<GameController>();
            Set(gc, "_art", AssetDatabase.LoadAssetAtPath<WorldArt>(ArtPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Game.prefab");
            Object.DestroyImmediate(go);

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            foreach (var old in Object.FindObjectsByType<GameController>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("PrefabBuild: game prefab placed in Main scene");
        }

        static void BuildAtlas(string path, params string[] folders)
        {
            var atlas = SpriteAtlasAsset.Load(path);
            bool created = atlas == null;
            if (created) atlas = new SpriteAtlasAsset();
            var objs = new Object[folders.Length];
            for (int i = 0; i < folders.Length; i++) objs[i] = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folders[i]);
            if (created) atlas.Add(objs);
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path);
            var imp = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            imp.packingSettings = new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4, blockOffset = 1 };
            imp.textureSettings = new SpriteAtlasTextureSettings { generateMipMaps = false, filterMode = FilterMode.Bilinear, readable = false, sRGB = true };
            var ps = imp.GetPlatformSettings("DefaultTexturePlatform");
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            ps.maxTextureSize = 2048;
            imp.SetPlatformSettings(ps);
            imp.SaveAndReimport();
        }
    }
}
