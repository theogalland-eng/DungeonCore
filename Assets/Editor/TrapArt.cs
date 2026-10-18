using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import des pièges dessinés par Tools~/make_traps.py (Assets/Art/Generated/Traps/nom_LxH.png) :
/// bandes d'images découpées en L × H, PPU 16, filtre « point », pivot (8/L, 8/H) — la case du piège
/// est le carré 16 × 16 en bas à gauche de chaque image (les pièges hauts et muraux débordent vers le haut ou la droite).
/// </summary>
public static class TrapArt
{
    public const string Dir = "Assets/Art/Generated/Traps";
    private static readonly Regex Grid = new Regex(@"^(.*)_(\d+)x(\d+)$");

    public static void ImportAll()
    {
        if (!Directory.Exists(Dir)) return;
        AssetDatabase.Refresh();
        foreach (var f in Directory.GetFiles(Dir, "*.png").Select(x => x.Replace('\\', '/')))
        {
            var m = Grid.Match(Path.GetFileNameWithoutExtension(f));
            if (!m.Success) continue;
            string baseName = m.Groups[1].Value;
            int fw = int.Parse(m.Groups[2].Value), fh = int.Parse(m.Groups[3].Value);
            var imp = (TextureImporter)AssetImporter.GetAtPath(f);
            if (imp == null) continue;
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(f));
            int cols = tex.width / fw;
            Object.DestroyImmediate(tex);
            var pivot = new Vector2(8f / fw, 8f / fh);
            var metas = new List<SpriteMetaData>();
            for (int c = 0; c < cols; c++)
                metas.Add(new SpriteMetaData { name = $"{baseName}_{c}", rect = new Rect(c * fw, 0, fw, fh), alignment = (int)SpriteAlignment.Custom, pivot = pivot });
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = 16f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
#pragma warning disable 0618
            imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
            imp.SaveAndReimport();
        }
    }

    public static Sprite[] Frames(string name)
    {
        if (!Directory.Exists(Dir)) return new Sprite[0];
        foreach (var f in Directory.GetFiles(Dir, "*.png").Select(x => x.Replace('\\', '/')))
        {
            var m = Grid.Match(Path.GetFileNameWithoutExtension(f));
            if (!m.Success || m.Groups[1].Value != name) continue;
            return AssetDatabase.LoadAllAssetsAtPath(f).OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int i) ? i : 0).ToArray();
        }
        return new Sprite[0];
    }
}
