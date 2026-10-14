using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Relief 2,5D de la montagne (vue « île ») :
///   1. exporte l'empreinte de la montagne (roche + sol du donjon à la surface) vers Temp/relief_mask.json ;
///   2. lance Tools~/make_relief.py (hauteurs, terrasses, falaises, cratère, projection oblique) ;
///   3. importe les bandes et crée Environment/Relief (triées comme les personnages) + MountainRelief.
/// </summary>
public static class ReliefBuilder
{
    const string Dir = "Assets/Art/Generated/Relief";

    [MenuItem("DungeonCore/8. Relief de l'île (2.5D)")]
    public static string BuildAll()
    {
        string log = ExportMask();
        var p = new System.Diagnostics.Process();
        // Le lanceur « py » choisit le Python 3 le plus récent (celui qui a numpy/scipy/Pillow)
        bool hasPy = File.Exists(@"C:\Windows\py.exe");
        p.StartInfo.FileName = hasPy ? "py" : "python";
        p.StartInfo.Arguments = (hasPy ? "-3 " : "") + "\"Tools~/make_relief.py\"";
        p.StartInfo.WorkingDirectory = Path.GetFullPath(".");
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;
        // Unity lance Python sans le dossier « site-packages » de l'utilisateur : on le lui indique
        string roaming = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        var sites = new List<string>();
        foreach (var dir in Directory.Exists(Path.Combine(roaming, "Python")) ? Directory.GetDirectories(Path.Combine(roaming, "Python")) : new string[0])
            if (Directory.Exists(Path.Combine(dir, "site-packages"))) sites.Add(Path.Combine(dir, "site-packages"));
        if (sites.Count > 0) p.StartInfo.EnvironmentVariables["PYTHONPATH"] = string.Join(";", sites);
        p.Start();
        string outp = p.StandardOutput.ReadToEnd();
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        // Python lancé depuis Unity peut ne pas trouver numpy/scipy : on garde alors les images déjà générées
        // (lancer « python "Tools~/make_relief.py" » dans un terminal, puis ce menu)
        if (p.ExitCode != 0)
        {
            Debug.LogWarning("[Relief] make_relief.py n'a pas pu tourner depuis Unity : " + err);
            if (!File.Exists(Path.Combine(Dir, "manifest.json"))) return "make_relief.py a échoué et aucune image n'existe : " + err;
            return log + " | images existantes réutilisées | " + BuildObjects();
        }
        return log + " | " + outp.Trim() + " | " + BuildObjects();
    }

    /// <summary>Textures des tuiles (roche volcanique, éboulis, falaise) pour habiller le relief au pixel près.</summary>
    static void ExportTextures()
    {
        var island = CatalogBuilder.Sprites(CatalogBuilder.ArtPath);
        var snow = CatalogBuilder.Sprites("Assets/imports/snow-expansion.png");
        void Save(Sprite sp, string name)
        {
            if (sp == null) return;
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sp.texture)));
            var r = sp.rect;
            var px = tex.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            var outT = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            outT.SetPixels(px);
            outT.Apply();
            File.WriteAllBytes($"Temp/relief_tex_{name}.png", outT.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(outT);
        }
        foreach (var n in new[] { "volcanic_a", "volcanic_b", "scree", "gravel_a" })
            if (island.TryGetValue("island_" + n, out var sp)) Save(sp, n);
        if (snow.TryGetValue("snow-expansion_155", out var cliff)) Save(cliff, "cliff");
        if (snow.TryGetValue("snow-expansion_195", out var stoneA)) Save(stoneA, "stone");
    }

    public static string ExportMask()
    {
        ExportTextures();
        var builder = Object.FindFirstObjectByType<DungeonBuilder>();
        var layers = Object.FindFirstObjectByType<DungeonLayers>();
        var stone = builder.stoneTilemap;
        var floor = builder.floorTilemap;
        var floorSet = new HashSet<TileBase>();
        if (builder.floorTile != null) floorSet.Add(builder.floorTile);
        foreach (var th in DungeonCatalog.Get().themes) if (th.floorTile != null) floorSet.Add(th.floorTile);

        RectInt b = layers != null && layers.layers.Count > 0 ? layers.layers[0].cellBounds : new RectInt(-100, -100, 200, 200);
        bool In(Vector3Int c) => b.Contains(new Vector2Int(c.x, c.y)) &&
                                 (stone.HasTile(c) || (floor.GetTile(c) != null && floorSet.Contains(floor.GetTile(c))));
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in b.allPositionsWithin)
        {
            var c = new Vector3Int(p.x, p.y, 0);
            if (!In(c)) continue;
            minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y); maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
        }
        int w = maxX - minX + 1, h = maxY - minY + 1;
        var rows = new List<string>();
        var entrances = new List<int>();
        for (int y = 0; y < h; y++)
        {
            var sb = new StringBuilder(w);
            for (int x = 0; x < w; x++)
            {
                var c = new Vector3Int(minX + x, minY + y, 0);
                bool m = In(c);
                sb.Append(m ? '1' : '0');
                // entrée : sol du donjon qui donne sur l'extérieur (sol praticable hors montagne) au sud
                if (m && !stone.HasTile(c))
                {
                    var s = c + Vector3Int.down;
                    if (!In(s) && floor.HasTile(s)) { entrances.Add(c.x); entrances.Add(c.y); }
                }
            }
            rows.Add(sb.ToString());
        }
        var json = new StringBuilder();
        json.Append("{\"minX\":").Append(minX).Append(",\"minY\":").Append(minY).Append(",\"w\":").Append(w).Append(",\"h\":").Append(h);
        json.Append(",\"entrances\":[").Append(string.Join(",", entrances)).Append("],\"rows\":[");
        for (int i = 0; i < rows.Count; i++) { if (i > 0) json.Append(','); json.Append('"').Append(rows[i]).Append('"'); }
        json.Append("]}");
        File.WriteAllText("Temp/relief_mask.json", json.ToString());
        return $"empreinte {w}x{h} cases, {entrances.Count / 2} entrée(s)";
    }

    [System.Serializable]
    class Band { public string file; public int x0, w, h, bottom, pivot; }
    [System.Serializable]
    class Manifest { public int minX, minY, px, canvasH, extra; public List<Band> bands; public float[] crater; }

    public static string BuildObjects()
    {
        AssetDatabase.Refresh();
        var man = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Dir, "manifest.json")));
        var env = GameObject.Find("Environment") ?? new GameObject("Environment");
        var old = env.transform.Find("Relief");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Relief");
        root.transform.SetParent(env.transform, false);
        var relief = root.AddComponent<MountainRelief>();
        var grid = Object.FindFirstObjectByType<Grid>();
        Vector3 origin = grid != null ? grid.CellToWorld(new Vector3Int(man.minX, man.minY, 0)) : new Vector3(man.minX, man.minY, 0);
        float ppu = man.px;
        foreach (var band in man.bands)
        {
            string path = $"{Dir}/{band.file}";
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.spritePixelsPerUnit = ppu;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            // pivot = pied de la bande : Unity trie par la position du pivot (axe Y)
            settings.spritePivot = new Vector2(0f, (band.pivot - band.bottom) / (float)band.h);
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var go = new GameObject(band.file.Replace(".png", ""));
            go.transform.SetParent(root.transform, false);
            go.transform.position = origin + new Vector3(band.x0 / ppu, band.pivot / ppu, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            relief.bands.Add(sr);
        }
        if (man.crater != null && man.crater.Length == 2)
            relief.craterWorld = grid != null ? (Vector2)grid.CellToWorld(Vector3Int.zero) + new Vector2(man.crater[0], man.crater[1]) : new Vector2(man.crater[0], man.crater[1]);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        return $"{relief.bands.Count} bandes posées";
    }
}
