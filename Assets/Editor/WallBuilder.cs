#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Murs auto-connectés, sols à motif et sprites d'arbres (menu DungeonCore/6).
/// - Atlas de murs : Assets/Art/Generated/Walls/walls_&lt;thème&gt;.png (générés par Tools~/make_walls.py)
///   → 128 sprites + une AutoWallTile par thème dans Assets/Tiles/Walls
/// - Sols : PatternTile (dalles de la forteresse, sol de la crypte)
/// - Arbres et buissons : 16 px par unité, pivot au pied du tronc (tri en profondeur)
/// </summary>
public static class WallBuilder
{
    const string AtlasDir = "Assets/Art/Generated/Walls";
    const string TileDir = "Assets/Tiles/Walls";

    [MenuItem("DungeonCore/6. Murs, sols et arbres")]
    public static string BuildAll()
    {
        int walls = ImportWalls();
        int floors = BuildFloors();
        int trees = ConfigureVegetation();
        AssetDatabase.SaveAssets();
        return $"{walls} murs, {floors} sols, {trees} sprites de végétation";
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path) && !Directory.Exists(path)) Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }

    // ─────────────────────────────────────────────
    // MURS
    // ─────────────────────────────────────────────

    static int ImportWalls()
    {
        EnsureFolder(TileDir);
        int n = 0;
        foreach (var f in Directory.GetFiles(AtlasDir, "walls_*.png").Select(x => x.Replace('\\', '/')))
        {
            string id = Path.GetFileNameWithoutExtension(f).Substring("walls_".Length);
            var imp = (TextureImporter)AssetImporter.GetAtPath(f);
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = 16;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            var metas = new List<SpriteMetaData>();
            for (int m = 0; m < 128; m++)
                metas.Add(new SpriteMetaData
                {
                    name = $"walls_{id}_{m}",
                    rect = new Rect((m % 16) * 16, 128 - (m / 16 + 1) * 16, 16, 16),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                });
#pragma warning disable 0618
            imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
            imp.SaveAndReimport();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(f).OfType<Sprite>().ToDictionary(s => s.name);
            string tp = $"{TileDir}/wall_{id}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<AutoWallTile>(tp);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<AutoWallTile>();
                AssetDatabase.CreateAsset(tile, tp);
            }
            tile.sprites = new Sprite[128];
            for (int m = 0; m < 128; m++) tile.sprites[m] = sprites.GetValueOrDefault($"walls_{id}_{m}");
            EditorUtility.SetDirty(tile);
            n++;
        }
        return n;
    }

    public static AutoWallTile Wall(string themeId) => AssetDatabase.LoadAssetAtPath<AutoWallTile>($"{TileDir}/wall_{themeId}.asset");

    /// <summary>Sprite représentatif d'un mur (bloc avec face) pour les icônes.</summary>
    public static Sprite WallIcon(string themeId)
    {
        var w = Wall(themeId);
        return w != null ? w.SpriteFor(8 | 2 | 1) : null;   // N, E, W murs, sud ouvert → face
    }

    // ─────────────────────────────────────────────
    // SOLS
    // ─────────────────────────────────────────────

    static string PackPng(string packPrefix, string file)
    {
        foreach (var d in Directory.GetDirectories("Assets/imports", packPrefix + "*"))
        {
            string p = $"{d}/PNG/{file}".Replace('\\', '/');
            if (File.Exists(p)) return p;
        }
        return null;
    }

    static Dictionary<string, Sprite> SheetSprites(string path) =>
        path == null ? new Dictionary<string, Sprite>() :
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());

    static PatternTile Pattern(string name, Sprite[] sprites, int width, bool random)
    {
        string tp = $"{TileDir}/{name}.asset";
        var t = AssetDatabase.LoadAssetAtPath<PatternTile>(tp);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<PatternTile>();
            AssetDatabase.CreateAsset(t, tp);
        }
        t.sprites = sprites.Where(s => s != null).ToArray();
        t.width = width;
        t.random = random;
        EditorUtility.SetDirty(t);
        return t;
    }

    public static PatternTile Floor(string id) => AssetDatabase.LoadAssetAtPath<PatternTile>($"{TileDir}/floor_{id}.asset");

    static int BuildFloors()
    {
        int n = 0;
        var donjon = SheetSprites(PackPng("craftpix-net-169442", "walls_floor.png"));
        if (donjon.Count > 0)
        {
            Pattern("floor_fortress", new[] { "walls_floor_21_0", "walls_floor_21_1", "walls_floor_22_0", "walls_floor_22_1" }
                .Select(k => donjon.GetValueOrDefault(k)).ToArray(), 2, false);
            n++;
        }
        var undead = SheetSprites(PackPng("craftpix-net-695666", "Ground_rocks.png"));
        if (undead.Count > 0)
        {
            // Dalles grises unies (souvent) + fissures (parfois)
            string[] keys = { "2_2", "2_2", "2_2", "2_2", "3_27", "3_30", "16_29", "2_2", "3_27", "3_30", "6_5", "6_9" };
            Pattern("floor_crypt", keys.Select(k => undead.GetValueOrDefault("Ground_rocks_" + k)).ToArray(), 1, true);
            n++;
        }
        return n;
    }

    // ─────────────────────────────────────────────
    // VÉGÉTATION
    // ─────────────────────────────────────────────

    public static string TreeDir => Directory.GetDirectories("Assets/imports", "craftpix-net-385863*").FirstOrDefault()?.Replace('\\', '/') + "/PNG/Assets_separately";
    public static string BushDir => Directory.GetDirectories("Assets/imports", "craftpix-net-141354*").FirstOrDefault()?.Replace('\\', '/') + "/PNG";

    /// <summary>Arbre (avec ombre) : Assets/imports/…/Trees_shadow/&lt;name&gt;.png</summary>
    public static Sprite Tree(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{TreeDir}/Trees_shadow/{name}.png");
    public static Sprite Bush(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{BushDir}/Assets_shadow/{name}.png");

    public static int ConfigureVegetation()
    {
        int n = 0;
        n += ConfigureFolder($"{TreeDir}/Trees_shadow", $"{TreeDir}/Trees");
        n += ConfigureFolder($"{BushDir}/Assets_shadow", $"{BushDir}/Assets");
        return n;
    }

    /// <summary>
    /// Pivot au pied du tronc : calculé sur la version sans ombre (bas de la silhouette),
    /// pour que le tri en profondeur se fasse au sol.
    /// </summary>
    static int ConfigureFolder(string shadowDir, string plainDir)
    {
        if (!Directory.Exists(shadowDir)) return 0;
        int n = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var f in Directory.GetFiles(shadowDir, "*.png").Select(x => x.Replace('\\', '/')))
            {
                string plain = $"{plainDir}/{Path.GetFileName(f)}";
                Vector2 pivot = FootPivot(File.Exists(plain) ? plain : f);
                var imp = (TextureImporter)AssetImporter.GetAtPath(f);
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 16;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
                n++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        return n;
    }

    static Vector2 FootPivot(string path)
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(path));
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        Object.DestroyImmediate(tex);
        // ligne opaque la plus basse (y = 0 en bas dans GetPixels)
        for (int y = 0; y < h; y++)
        {
            int minX = -1, maxX = -1;
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 40) { if (minX < 0) minX = x; maxX = x; }
            if (minX < 0) continue;
            // on remonte un peu : les racines dépassent sous le pied
            float footY = Mathf.Min(h - 1, y + Mathf.Max(1, h / 32));
            return new Vector2((minX + maxX + 1) * 0.5f / w, footY / h);
        }
        return new Vector2(0.5f, 0.1f);
    }
}
#endif
