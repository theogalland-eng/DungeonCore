#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Importe le terrain dessiné par Tools~/make_terrain.py : feuilles « blob » (47 cas x 9 phases) → BlobTile,
/// eau (9 phases) → PhaseTile, front de falaise et petits décors → Tile. Les assets de tuiles sont rangés
/// dans Assets/Art/Generated/Terrain/Tiles.
/// </summary>
public static class TerrainArt
{
    public const string Dir = "Assets/Art/Generated/Terrain";
    const string TileDir = Dir + "/Tiles";
    const int T = 16;

    static void EnsureDir()
    {
        if (!AssetDatabase.IsValidFolder(TileDir)) AssetDatabase.CreateFolder(Dir, "Tiles");
    }

    /// <summary>Découpe une feuille en grille (toutes les cases, même vides), ordre ligne par ligne depuis le haut.</summary>
    static Sprite[] SliceGrid(string path, int cols, int rows)
    {
        string baseName = Path.GetFileNameWithoutExtension(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) { AssetDatabase.ImportAsset(path); imp = (TextureImporter)AssetImporter.GetAtPath(path); }
        var metas = new List<SpriteMetaData>();
        int h = rows * T;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                metas.Add(new SpriteMetaData
                {
                    name = $"{baseName}_{r * cols + c}",
                    rect = new Rect(c * T, h - (r + 1) * T, T, T),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                });
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = T;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // pas de jour entre les cases
        settings.spriteExtrude = 0;
        imp.SetTextureSettings(settings);
#pragma warning disable 0618
        imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
        imp.SaveAndReimport();
        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        return metas.Select(m => all.TryGetValue(m.name, out var s) ? s : null).ToArray();
    }

    static T2 LoadOrCreate<T2>(string name) where T2 : ScriptableObject
    {
        EnsureDir();
        string path = $"{TileDir}/{name}.asset";
        var a = AssetDatabase.LoadAssetAtPath<T2>(path);
        if (a == null) { a = ScriptableObject.CreateInstance<T2>(); AssetDatabase.CreateAsset(a, path); }
        return a;
    }

    static readonly Dictionary<string, BlobTile> blobs = new Dictionary<string, BlobTile>();

    /// <summary>Tuile auto-raccordée d'un matériau : sand, rock, grass, jungle, lava, trampled, dirt, paved.</summary>
    public static BlobTile Blob(string name)
    {
        if (blobs.TryGetValue(name, out var cached) && cached != null) return cached;
        var sprites = SliceGrid($"{Dir}/terrain_{name}.png", 47, 9);
        var t = LoadOrCreate<BlobTile>("terrain_" + name);
        t.sprites = sprites;
        EditorUtility.SetDirty(t);
        blobs[name] = t;
        return t;
    }

    public static PhaseTile Water()
    {
        var sprites = SliceGrid($"{Dir}/water.png", 9, 4);
        var t = LoadOrCreate<PhaseTile>("water_sea");
        t.sprites = sprites;
        t.frames = 4;
        t.fps = 2.5f;
        EditorUtility.SetDirty(t);
        return t;
    }

    /// <summary>Sol d'une ambiance du donjon (Tools~/make_dungeon.py) : 9 phases raccordables. null si absent.</summary>
    public static PhaseTile DungeonFloor(string themeId)
    {
        string path = $"Assets/Art/Generated/Dungeon/floor_{themeId}.png";
        if (!File.Exists(path)) return null;
        var sprites = SliceGrid(path, 9, 1);
        var t = LoadOrCreate<PhaseTile>("floor9_" + themeId);
        t.sprites = sprites;
        t.frames = 1;
        EditorUtility.SetDirty(t);
        return t;
    }

    public static Tile[] Cliff()
    {
        var sprites = SliceGrid($"{Dir}/cliff.png", 5, 1);
        var tiles = new Tile[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            var t = LoadOrCreate<Tile>("cliff_" + i);
            t.sprite = sprites[i];
            t.colliderType = Tile.ColliderType.None;
            t.flags = TileFlags.None;
            EditorUtility.SetDirty(t);
            tiles[i] = t;
        }
        return tiles;
    }

    static readonly Dictionary<string, Tile> decors = new Dictionary<string, Tile>();

    /// <summary>Petit décor 16 px (touffe, fleurs, roseaux, nénuphar, récif...) en Tile sans collision.</summary>
    public static Tile Decor(string name)
    {
        if (decors.TryGetValue(name, out var cached) && cached != null) return cached;
        string path = $"{Dir}/decor_{name}.png";
        if (!File.Exists(path)) return null;
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = T;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        var t = LoadOrCreate<Tile>("decor_" + name);
        t.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        t.colliderType = Tile.ColliderType.None;
        t.flags = TileFlags.None;
        EditorUtility.SetDirty(t);
        decors[name] = t;
        return t;
    }

    /// <summary>
    /// Marqueur invisible du tilemap « Sol » : le sol visible est dessiné par les couches de terrain,
    /// mais le jeu se sert de « Sol » pour savoir où est la terre ferme (et la grève : nom en « gravel »).
    /// </summary>
    public static Tile Marker(string name)
    {
        var t = LoadOrCreate<Tile>(name);
        t.sprite = null;
        t.colliderType = Tile.ColliderType.None;
        t.flags = TileFlags.None;
        EditorUtility.SetDirty(t);
        return t;
    }

    public static void ClearCache() { blobs.Clear(); decors.Clear(); }
}
#endif
