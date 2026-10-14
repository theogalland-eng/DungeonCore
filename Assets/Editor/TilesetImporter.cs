#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Découpe les tilesets des packs craftpix (Assets/imports) et crée des Tile Palettes prêtes à peindre.
/// Menu : DungeonCore/5. Importer les tilesets.
///
/// - Planches 16 px (listées dans les fichiers Tiled .tmx) → sprites 16 px + Tiles + palette par pack
/// - Objets séparés (arbres, buissons, maisons, décors) → sprites nets, pivot en bas
/// Les planches d'animation (eau, feu, pièges) sont découpées en sprites mais sans Tiles.
/// </summary>
public static class TilesetImporter
{
    const string TileRoot = "Assets/Tiles/Packs";
    const string PaletteRoot = "Assets/Palettes/Packs";

    // Planches qui ne donnent pas de Tiles (animations / personnages)
    static readonly string[] NoTilePatterns = { "animation", "detilazation", "details_animation", "objects_animated", "cultist", "ghost", "discoverer",
        "swordsman", "orc", "slime", "boar", "deer", "fox", "hare", "grouse", "shadow", "spikes", "blades_trap", "fire_trap", "trap_saw", "trap_plate", "сhest", "chest", "leader" };

    // Planches de personnages (animations découpées par CharacterImporter)
    static readonly string[] CharacterSheetPatterns = { "cultist", "ghost", "discoverer", "swordsman", "leader", "orc", "slime",
        "boar", "deer", "fox", "hare", "grouse" };

    // Packs de personnages (Assets/imports/new) : leurs planches ne doivent jamais être redécoupées
    static readonly string[] CharacterPacks = { "208004", "299679", "393827", "415491", "463118", "500988", "543463", "550920",
        "710530", "846584", "870078", "894297", "900504", "934911" };

    static readonly string[] CharacterTmx = { "Characters.tmx", "Swordsman", "Orc", "Slime", "Animals" };

    [MenuItem("DungeonCore/5. Importer les tilesets")]
    public static string ImportAll()
    {
        var log = new List<string>();
        int sheets = 0, tiles = 0, singles = 0;

        // ── 1. Planches 16 px référencées par les cartes Tiled
        var byPack = new Dictionary<string, List<(string path, int size)>>();
        foreach (var tmx in Directory.GetFiles("Assets/imports", "*.tmx", SearchOption.AllDirectories))
        {
            string t = tmx.Replace('\\', '/');
            if (CharacterTmx.Any(c => t.Contains(c))) continue;
            string pack = PackName(t);
            string packDir = t.Substring(0, t.IndexOf(pack) + pack.Length);
            string xml = File.ReadAllText(t);
            foreach (Match m in Regex.Matches(xml, "tilewidth=\"(\\d+)\"[^>]*>\\s*<image source=\"([^\"]+)\""))
            {
                int size = int.Parse(m.Groups[1].Value);
                string file = Path.GetFileName(m.Groups[2].Value);
                string path = FindInPack(packDir, file);
                if (path == null) continue;
                if (!byPack.TryGetValue(pack, out var lst)) byPack[pack] = lst = new List<(string, int)>();
                if (!lst.Any(x => x.path == path)) lst.Add((path, size));
            }
        }

        // ── 2. Village : tuiles 32 px séparées + planche
        string village = Directory.GetDirectories("Assets/imports", "craftpix-net-504452*").FirstOrDefault()?.Replace('\\', '/');

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var kv in byPack)
            {
                foreach (var (path, size) in kv.Value)
                {
                    string fn = Path.GetFileName(path).ToLowerInvariant();
                    // Planches de personnages : découpées par CharacterImporter, ne pas y toucher
                    if (CharacterSheetPatterns.Any(p => fn.Contains(p)) || CharacterPacks.Any(p => path.Contains(p)) || path.Contains("/Characters/")) continue;
                    bool makeTiles = !NoTilePatterns.Any(p => fn.Contains(p));
                    SliceGridRaw(path, size, 16f);
                    sheets++;
                    if (!makeTiles) continue;
                    // les Tiles sont créées après réimport (voir plus bas)
                    pendingTiles.Add((kv.Key, path, size));
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();

        // Tiles & palettes (après réimport des planches)
        AssetDatabase.StartAssetEditing();
        var packTiles = new Dictionary<string, List<Tile[,]>>();
        try
        {
            foreach (var (pack, path, size) in pendingTiles)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
                if (sprites.Count == 0) continue;
                string dir = $"{TileRoot}/{Short(pack)}/{Path.GetFileNameWithoutExtension(path)}";
                EnsureFolder(dir);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int rows = tex.height / size, cols = tex.width / size;
                var grid = new Tile[rows, cols];
                foreach (var s in sprites)
                {
                    int c = Mathf.RoundToInt(s.rect.x / size), r = rows - 1 - Mathf.RoundToInt(s.rect.y / size);
                    if (r < 0 || r >= rows || c < 0 || c >= cols) continue;
                    grid[r, c] = TileAsset(s, $"{dir}/{s.name}.asset");
                    tiles++;
                }
                if (!packTiles.TryGetValue(pack, out var lst)) packTiles[pack] = lst = new List<Tile[,]>();
                lst.Add(grid);
            }

            // Village (32 px)
            if (village != null)
            {
                foreach (var f in Directory.GetFiles(village, "*.png", SearchOption.AllDirectories).Select(x => x.Replace('\\', '/')))
                {
                    string low = f.ToLowerInvariant();
                    if (low.Contains("coupon") || low.Contains("/psd/")) continue;
                    bool isTile = low.Contains("/1 tiles/") || low.Contains("/1.1 tiles/");
                    SetSingle(f, isTile ? 32f : 32f, isTile ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0.05f));
                    singles++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();

        // ── 3. Objets séparés : arbres, buissons, désert, morts-vivants, objets du village
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var pat in new[] { "craftpix-net-385863*", "craftpix-net-141354*", "craftpix-891121*" })
                foreach (var packDir in Directory.GetDirectories("Assets/imports", pat))
                    foreach (var f in Directory.GetFiles(packDir, "*.png", SearchOption.AllDirectories).Select(x => x.Replace('\\', '/')))
                    {
                        string low = f.ToLowerInvariant();
                        if (low.Contains("coupon") || low.Contains("preview") || low.Contains("/psd/") || low.Contains("_source")) continue;
                        SetSingle(f, 32f, new Vector2(0.5f, 0.08f));
                        singles++;
                    }
            foreach (var packDir in Directory.GetDirectories("Assets/imports", "craftpix-net-695666*"))
            {
                string sep = packDir + "/PNG/Objects_separately";
                if (!Directory.Exists(sep)) continue;
                foreach (var f in Directory.GetFiles(sep, "*.png").Select(x => x.Replace('\\', '/')))
                {
                    SetSingle(f, 16f, new Vector2(0.5f, 0.08f));
                    singles++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();

        // ── 4. Palettes
        int palettes = 0;
        foreach (var kv in packTiles)
        {
            BuildPalette(Short(kv.Key), kv.Value);
            palettes++;
        }
        palettes += BuildNaturePalettes();

        pendingTiles.Clear();
        WallBuilder.ConfigureVegetation();   // arbres/buissons : 16 px par unité, pivot au pied
        AssetDatabase.SaveAssets();
        return $"{sheets} planches découpées, {tiles} tiles, {singles} sprites d'objets, {palettes} palettes";
    }

    static readonly List<(string pack, string path, int size)> pendingTiles = new List<(string, string, int)>();

    static string PackName(string path)
    {
        var m = Regex.Match(path, "craftpix[^/]*");
        return m.Success ? m.Value : "pack";
    }

    /// <summary>Nom court lisible : "craftpix-net-160005-free-ruined-temple-top-down..." → "RuinedTemple".</summary>
    static string Short(string pack)
    {
        string p = pack.ToLowerInvariant();
        if (p.Contains("dungeon-tileset-pixel-top-down-for-indie")) return "Donjon2";
        if (p.Contains("top-down-dungeon-pixel-tileset-for-rpg")) return "Donjon3";
        if (p.Contains("fishing-village")) return "VillagePecheurs";
        if (p.Contains("market-square")) return "Marche";
        if (p.Contains("training-arena")) return "Arene";
        if (p.Contains("bridges")) return "Ponts";
        if (p.Contains("rpg-camp")) return "Campement";
        if (p.Contains("treasure")) return "Tresors";
        if (p.Contains("dungeon-props")) return "ObjetsDonjon2";
        if (p.Contains("plants-for-farm")) return "Plantes";
        if (p.Contains("ruined-temple")) return "Temple";
        if (p.Contains("dungeon-asset")) return "Donjon";
        if (p.Contains("dungeon-objects")) return "ObjetsDonjon";
        if (p.Contains("undead")) return "MortsVivants";
        if (p.Contains("desert")) return "Desert";
        if (p.Contains("village")) return "Village";
        return "Pack";
    }

    static string FindInPack(string packDir, string file)
    {
        string low = file.ToLowerInvariant();
        string best = null;
        foreach (var f in Directory.GetFiles(packDir, "*.png", SearchOption.AllDirectories))
        {
            string p = f.Replace('\\', '/');
            if (Path.GetFileName(p).ToLowerInvariant() != low) continue;
            if (best == null || (!p.Contains("/Tiled") && best.Contains("/Tiled"))) best = p;   // préférer le dossier PNG
        }
        return best;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            // Directory.CreateDirectory : fiable même pendant StartAssetEditing (pas de doublons "Dossier 1")
            if (!AssetDatabase.IsValidFolder(next) && !Directory.Exists(next)) Directory.CreateDirectory(next);
            cur = next;
        }
    }

    /// <summary>Découpe en grille (cases vides ignorées) sans recharger les sprites.</summary>
    static int SliceGridRaw(string path, int size, float ppu)
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(path));
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        Object.DestroyImmediate(tex);
        string baseName = Path.GetFileNameWithoutExtension(path);
        var metas = new List<SpriteMetaData>();
        for (int r = 0; r < h / size; r++)
            for (int c = 0; c < w / size; c++)
            {
                int x0 = c * size, y0 = h - (r + 1) * size;
                bool any = false;
                for (int y = y0; y < y0 + size && !any; y++)
                    for (int x = x0; x < x0 + size; x++)
                        if (px[y * w + x].a > 10) { any = true; break; }
                if (!any) continue;
                metas.Add(new SpriteMetaData { name = $"{baseName}_{r}_{c}", rect = new Rect(x0, y0, size, size), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            }
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return 0;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
#pragma warning disable 0618
        imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
        imp.SaveAndReimport();
        return metas.Count;
    }

    static void SetSingle(string path, float ppu, Vector2 pivot)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    static Tile TileAsset(Sprite s, string path)
    {
        var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (t != null) return t;
        t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = s;
        t.colliderType = Tile.ColliderType.None;
        t.flags = TileFlags.None;
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    static void BuildPalette(string name, List<Tile[,]> sheets)
    {
        EnsureFolder(PaletteRoot);
        string path = $"{PaletteRoot}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
        var paletteAsset = GridPaletteUtility.CreateNewPalette(PaletteRoot, name, GridLayout.CellLayout.Rectangle,
            GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        string realPath = AssetDatabase.GetAssetPath(paletteAsset);
        var root = PrefabUtility.LoadPrefabContents(realPath);
        try
        {
            var tm = root.GetComponentInChildren<Tilemap>();
            int yOffset = 0;
            foreach (var grid in sheets)
            {
                int rows = grid.GetLength(0), cols = grid.GetLength(1);
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        if (grid[r, c] != null) tm.SetTile(new Vector3Int(c, -(yOffset + r), 0), grid[r, c]);
                yOffset += rows + 2;
            }
            PrefabUtility.SaveAsPrefabAsset(root, realPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Palettes "Nature" (arbres + buissons) et "Village" à partir des sprites séparés.</summary>
    static int BuildNaturePalettes()
    {
        int n = 0;
        var groups = new Dictionary<string, List<string>>
        {
            { "Nature", Directory.GetDirectories("Assets/imports", "craftpix-net-385863*").Concat(Directory.GetDirectories("Assets/imports", "craftpix-net-141354*")).ToList() },
            { "VillageTuiles", Directory.GetDirectories("Assets/imports", "craftpix-net-504452*").ToList() },
        };
        foreach (var g in groups)
        {
            var files = new List<string>();
            foreach (var d in g.Value)
                files.AddRange(Directory.GetFiles(d, "*.png", SearchOption.AllDirectories).Select(x => x.Replace('\\', '/'))
                    .Where(f =>
                    {
                        string low = f.ToLowerInvariant();
                        string fn = Path.GetFileName(low);
                        if (low.Contains("coupon") || low.Contains("/psd/") || fn.Contains("_source") || fn.Contains("tileset")) return false;
                        if (g.Key == "Nature") return low.Contains("/trees_texture_shadow/") || (low.Contains("/assets_texture_shadow/"));
                        return low.Contains("/1 tiles/") || low.Contains("/1.1 tiles/");
                    }));
            if (files.Count == 0) continue;
            string dir = $"{TileRoot}/{g.Key}";
            EnsureFolder(dir);
            var tiles = new List<Tile>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var f in files)
                {
                    var s = AssetDatabase.LoadAssetAtPath<Sprite>(f);
                    if (s == null) continue;
                    tiles.Add(TileAsset(s, $"{dir}/{Path.GetFileNameWithoutExtension(f)}_{Mathf.Abs(f.GetHashCode()) % 10000}.asset"));
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            int cols = g.Key == "Nature" ? 10 : 16;
            int step = g.Key == "Nature" ? 5 : 1;   // les arbres débordent de leur case
            var grid = new Tile[(tiles.Count + cols - 1) / cols * step, cols * step];
            for (int i = 0; i < tiles.Count; i++) grid[(i / cols) * step, (i % cols) * step] = tiles[i];
            BuildPalette(g.Key, new List<Tile[,]> { grid });
            n++;
        }
        return n;
    }
}
#endif
