#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TAMPONS DE LA CITÉ : importe les scènes découpées par Tools~/make_stamps.py (Assets/Art/Settlement/Stamps/&lt;id&gt;)
/// — sol plat (pivot en bas à gauche), morceaux posés par le milieu de leur bord inférieur, bandes d'animation —
/// et les range dans SettlementArt.stamps.
/// </summary>
public static class StampImporter
{
    public const string Dir = "Assets/Art/Settlement/Stamps";
    const float Ppu = 32f;   // tuile de 16 px = une demi-case

    [System.Serializable] class JPiece { public string file; public int x, y, w, h, frames, sortY, openFrame; public float fps; public string kind, names, role; public bool solid, cycle; }
    [System.Serializable] class JStamp { public string id; public int w, h, tile; public float[] door; public JPiece[] pieces; public float[] solids; }

    [MenuItem("DungeonCore/Importer les tampons de la cité")]
    public static void ImportMenu() => Debug.Log(Import());

    public static string Import()
    {
        if (!Directory.Exists(Dir)) return "pas de tampons (lancer Tools~/make_stamps.py)";
        AssetDatabase.Refresh();
        var art = ArtImporter.BuildSettlementArt();
        art.stamps.Clear();
        int pieces = 0;
        foreach (var json in Directory.GetDirectories(Dir).SelectMany(d => Directory.GetFiles(d, "*.json")).Select(f => f.Replace('\\', '/')).OrderBy(f => f))
        {
            var j = JsonUtility.FromJson<JStamp>(File.ReadAllText(json));
            string dir = Path.GetDirectoryName(json).Replace('\\', '/');
            var st = new SettlementArt.Stamp
            {
                id = j.id,
                size = new Vector2(j.w / Ppu, j.h / Ppu),
                door = j.door != null && j.door.Length == 2 ? new Vector2(j.door[0] / Ppu, j.door[1] / Ppu) : new Vector2(j.w / Ppu / 2f, 0f),
                ground = ImportSingle($"{dir}/ground.png", new Vector2(0f, 0f)),
            };
            foreach (var p in j.pieces)
            {
                var frames = p.frames > 1 ? ImportStrip($"{dir}/{p.file}", p.w, p.h) : new[] { ImportSingle($"{dir}/{p.file}", new Vector2(0.5f, 0f)) };
                if (frames == null || frames.Length == 0 || frames[0] == null) continue;
                st.pieces.Add(new SettlementArt.StampPiece
                {
                    frames = frames,
                    pos = new Vector2((p.x + p.w * 0.5f) / Ppu, p.y / Ppu),
                    size = new Vector2(p.w / Ppu, p.h / Ppu),
                    fps = p.fps,
                    solid = p.solid,
                    kind = p.kind,
                    names = p.names,
                    role = p.role,
                    sortY = p.sortY / Ppu,
                    openFrame = p.openFrame,
                    cycle = p.cycle,
                });
                pieces++;
            }
            if (j.solids != null)
                for (int k = 0; k + 3 < j.solids.Length; k += 4)
                    st.solids.Add(new Rect(j.solids[k] / Ppu, j.solids[k + 1] / Ppu, j.solids[k + 2] / Ppu, j.solids[k + 3] / Ppu));
            art.stamps.Add(st);
        }
        // pièces de l'enceinte (Tools~/make_citywalls.py)
        const string walls = "Assets/Art/Settlement/Walls";
        int wn = 0;
        if (Directory.Exists(walls))
            foreach (var f in Directory.GetFiles(walls, "*.png").Select(x => x.Replace(Path.DirectorySeparatorChar, '/')))
            {
                var sp = ImportSingle(f, new Vector2(0.5f, 0f));
                if (sp == null) continue;
                string n = Path.GetFileNameWithoutExtension(f);
                art.entries.RemoveAll(e => e.name == n);
                art.entries.Add(new SettlementArt.Entry { name = n, frames = new[] { sp } });
                wn++;
            }
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        return $"{wn} pièces d'enceinte, {art.stamps.Count} tampons, {pieces} morceaux : " + string.Join(", ", art.stamps.Select(s => $"{s.id} {s.size.x:0.#}x{s.size.y:0.#}"));
    }

    static TextureImporter Prepare(string path)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Sprite;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.spritePixelsPerUnit = Ppu;
        imp.alphaIsTransparency = true;
        return imp;
    }

    static Sprite ImportSingle(string path, Vector2 pivot)
    {
        var imp = Prepare(path);
        if (imp == null) return null;
        imp.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.Tight;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite[] ImportStrip(string path, int fw, int fh)
    {
        var imp = Prepare(path);
        if (imp == null) return null;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int n = tex != null ? tex.width / fw : 1;
        var metas = new List<SpriteMetaData>();
        string baseName = Path.GetFileNameWithoutExtension(path);
        for (int i = 0; i < n; i++)
            metas.Add(new SpriteMetaData { name = $"{baseName}_{i}", rect = new Rect(i * fw, 0, fw, fh), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0f) });
        imp.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 0618
        imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
        imp.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int k) ? k : 0).ToArray();
    }
}
#endif
