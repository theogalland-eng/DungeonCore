#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Importe les illustrations découpées par Tools~/crop_assets.py et Tools~/crop_dungeon.py :
///   Assets/Art/Settlement (colonie) et Assets/Art/Dungeon (pièges, décors).
/// Un fichier « nom_LxH.png » est une bande d'animation découpée en images L×H.
/// Crée aussi Resources/SettlementArt, les tuiles du ponton et la mer animée.
/// </summary>
public static class ArtImporter
{
    public const string SettlementDir = "Assets/Art/Settlement";
    public const string DungeonDir = "Assets/Art/Dungeon";
    public const string LootDir = "Assets/Resources/Loot";
    public const string FxDir = "Assets/Resources/FX";
    public const string InteriorDir = "Assets/Resources/Interiors";
    static readonly Regex GridName = new Regex(@"^(.*)_(\d+)x(\d+)$");

    [MenuItem("DungeonCore/7. Importer colonie, pièges et décors")]
    public static string ImportAll()
    {
        int n = 0;
        n += ImportFolder(SettlementDir);
        n += ImportFolder(DungeonDir);
        n += ImportFolder(LootDir);
        n += ImportFolder(FxDir);
        n += ImportFolder(InteriorDir);
        ImportSea();
        var art = BuildSettlementArt();
        AssetDatabase.SaveAssets();
        return $"{n} images, colonie : {art.entries.Count} éléments";
    }

    /// <summary>Largeur visée (en cases) des façades de bâtiments importées.</summary>
    static readonly System.Collections.Generic.Dictionary<string, float> BuildingWidth = new System.Collections.Generic.Dictionary<string, float>
    {
        { "bld_tavern", 4.4f }, { "bld_guild", 4.2f }, { "bld_forge", 4.6f }, { "bld_magetower", 3.1f },
        { "bld_glass", 3.6f }, { "bld_windmill", 3.4f }, { "bld_farmhouse", 3.8f },
    };

    static float PpuFor(string dir, string name)
    {
        // Pièges : une image = une case de large (ils ne débordent plus sur les murs voisins)
        if (name.StartsWith("trap_"))
        {
            var gm = GridName.Match(name);
            if (gm.Success) return Mathf.Max(32f, int.Parse(gm.Groups[2].Value));
        }
        if (name.StartsWith("pier") || name.StartsWith("deck") || name == "field" || name.StartsWith("stump")) return 16f;
        // Portes (une case de large, voûte qui déborde sur le mur du fond) et escaliers (un peu plus d'une case)
        if (name == "door_skull") return 44f;
        if (name.StartsWith("stairs_")) return 36f;
        // Décors de caverne : un peu plus petits que 32 px/case (le pack est dessiné plus grand)
        if (name.StartsWith("cave_demon_head")) return 56f;
        if (name.StartsWith("cave_mushrooms_long")) return 48f;
        if (name.StartsWith("cave_")) return 40f;
        // Bâtiments des nouveaux packs (façades complètes) : mis à l'échelle de leur parcelle
        if (BuildingWidth.TryGetValue(name, out float units))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}.png");
            if (tex != null) return tex.width / units;
        }
        return 32f;
    }

    static Vector2 PivotFor(string dir, string name)
    {
        if (name.StartsWith("loot_") || dir == FxDir) return new Vector2(0.5f, 0.5f);
        if (name.StartsWith("door_")) return new Vector2(0.5f, 0f);
        if (name.StartsWith("stairs_")) return new Vector2(0.5f, 0.5f);
        if (dir == InteriorDir) return name.StartsWith("floor_") || name.EndsWith("_rug") || name == "mag_circle" ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
        // pièges plats centrés ; pièges hauts posés au bas de leur case (ne dépassent que vers le haut)
        if (name.StartsWith("trap_") && !name.StartsWith("trap_spikes")) return new Vector2(0.5f, 0f);
        if (name.StartsWith("trap_") || name.StartsWith("pier") || name.StartsWith("deck") || name.StartsWith("boat") || name == "web") return new Vector2(0.5f, 0.5f);
        if (name.StartsWith("stump")) return new Vector2(0.5f, 0.2f);
        return new Vector2(0.5f, 0.04f);   // posé au sol
    }

    static int ImportFolder(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        int n = 0;
        AssetDatabase.Refresh();
        foreach (var f in Directory.GetFiles(dir, "*.png").Select(x => x.Replace('\\', '/')))
        {
            string name = Path.GetFileNameWithoutExtension(f);
            var imp = (TextureImporter)AssetImporter.GetAtPath(f);
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            // sols d'intérieur : motifs répétés (SpriteRenderer en mode « Tiled »)
            bool floor = dir == InteriorDir && name.StartsWith("floor_");
            imp.wrapMode = floor ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.spritePixelsPerUnit = PpuFor(dir, name);
            Vector2 pivot = PivotFor(dir, name);
            var m = GridName.Match(name);
            if (m.Success)
            {
                string baseName = m.Groups[1].Value;
                int fw = int.Parse(m.Groups[2].Value), fh = int.Parse(m.Groups[3].Value);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(f));
                int cols = tex.width / fw, rows = tex.height / fh;
                var px = tex.GetPixels32();
                int w = tex.width;
                Object.DestroyImmediate(tex);
                var metas = new List<SpriteMetaData>();
                int idx = 0;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        int x0 = c * fw, y0 = (rows - 1 - r) * fh;
                        bool any = false;
                        for (int y = y0; y < y0 + fh && !any; y++)
                            for (int x = x0; x < x0 + fw; x++)
                                if (px[y * w + x].a > 8) { any = true; break; }
                        if (!any) continue;
                        metas.Add(new SpriteMetaData { name = $"{baseName}_{idx++}", rect = new Rect(x0, y0, fw, fh), alignment = (int)SpriteAlignment.Custom, pivot = pivot });
                    }
                imp.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 0618
                imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
            }
            else
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                if (floor) settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                imp.SetTextureSettings(settings);
            }
            imp.SaveAndReimport();
            n++;
        }
        return n;
    }

    /// <summary>Images d'une illustration (bande d'animation ou image seule), par nom sans suffixe.</summary>
    public static Sprite[] Frames(string dir, string name)
    {
        foreach (var f in Directory.GetFiles(dir, "*.png").Select(x => x.Replace('\\', '/')))
        {
            string fn = Path.GetFileNameWithoutExtension(f);
            var m = GridName.Match(fn);
            string baseName = m.Success ? m.Groups[1].Value : fn;
            if (baseName != name) continue;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(f).OfType<Sprite>().ToList();
            if (!m.Success) return sprites.Take(1).ToArray();
            return sprites.OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int i) ? i : 0).ToArray();
        }
        return new Sprite[0];
    }

    public static Sprite Single(string dir, string name) => Frames(dir, name).FirstOrDefault();

    static Tile MakeTile(string path, Sprite s, Tile.ColliderType col)
    {
        var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, path); }
        t.sprite = s;
        t.colliderType = col;
        t.flags = TileFlags.None;
        EditorUtility.SetDirty(t);
        return t;
    }

    // ─────────────────────────────────────────────
    // MER ANIMÉE
    // ─────────────────────────────────────────────

    public const string SeaPath = "Assets/Art/Generated/sea_anim.png";

    static void ImportSea()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(SeaPath);
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < 8; i++)
            metas.Add(new SpriteMetaData { name = $"sea_anim_{i}", rect = new Rect(i * 16, 0, 16, 16), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
#pragma warning disable 0618
        imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
        imp.SaveAndReimport();
        var frames = AssetDatabase.LoadAllAssetsAtPath(SeaPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        SeaTile("Assets/Tiles/Generated/sea_anim_blocked.asset", frames, Tile.ColliderType.Grid);
        SeaTile("Assets/Tiles/Generated/sea_anim_floor.asset", frames, Tile.ColliderType.None);
    }

    static AnimatedTileLite SeaTile(string path, Sprite[] frames, Tile.ColliderType col)
    {
        var t = AssetDatabase.LoadAssetAtPath<AnimatedTileLite>(path);
        if (t == null) { t = ScriptableObject.CreateInstance<AnimatedTileLite>(); AssetDatabase.CreateAsset(t, path); }
        t.frames = frames;
        t.speed = 3f;
        t.colliderType = col;
        EditorUtility.SetDirty(t);
        return t;
    }

    public static AnimatedTileLite SeaBlocked() => AssetDatabase.LoadAssetAtPath<AnimatedTileLite>("Assets/Tiles/Generated/sea_anim_blocked.asset");
    public static AnimatedTileLite SeaFloor() => AssetDatabase.LoadAssetAtPath<AnimatedTileLite>("Assets/Tiles/Generated/sea_anim_floor.asset");

    // ─────────────────────────────────────────────
    // COLONIE
    // ─────────────────────────────────────────────

    public static SettlementArt BuildSettlementArt()
    {
        const string path = "Assets/Resources/SettlementArt.asset";
        var art = AssetDatabase.LoadAssetAtPath<SettlementArt>(path);
        if (art == null) { art = ScriptableObject.CreateInstance<SettlementArt>(); AssetDatabase.CreateAsset(art, path); }
        art.entries.Clear();
        var names = new HashSet<string>();
        foreach (var f in Directory.GetFiles(SettlementDir, "*.png"))
        {
            string fn = Path.GetFileNameWithoutExtension(f);
            var m = GridName.Match(fn);
            names.Add(m.Success ? m.Groups[1].Value : fn);
        }
        foreach (var n in names.OrderBy(x => x))
            art.entries.Add(new SettlementArt.Entry { name = n, frames = Frames(SettlementDir, n) });
        if (!AssetDatabase.IsValidFolder("Assets/Tiles/Settlement")) AssetDatabase.CreateFolder("Assets/Tiles", "Settlement");
        art.pierLeft = MakeTile("Assets/Tiles/Settlement/pier_left.asset", Single(SettlementDir, "pier_left"), Tile.ColliderType.None);
        art.pierRight = MakeTile("Assets/Tiles/Settlement/pier_right.asset", Single(SettlementDir, "pier_right"), Tile.ColliderType.None);
        art.pierPlatform = MakeTile("Assets/Tiles/Settlement/pier_platform.asset", Single(SettlementDir, "pier_platform"), Tile.ColliderType.None);
        art.seaFloor = SeaFloor();
        art.stumps = new[] { "stump_0", "stump_1", "stump_2" }.Select(s => Single(SettlementDir, s)).Where(s => s != null).ToArray();
        EditorUtility.SetDirty(art);
        return art;
    }
}
#endif
