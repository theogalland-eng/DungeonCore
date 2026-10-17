#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Import des icônes pixel de l'interface (Tools~/make_ui_icons.py) : sprites nets, 16 px.</summary>
public static class UIArt
{
    const string Dir = "Assets/Resources/UI/Icons";

    [MenuItem("DungeonCore/13. Importer les icônes de l'interface")]
    public static string ImportIcons()
    {
        AssetDatabase.Refresh();
        int n = 0;
        foreach (var f in Directory.GetFiles(Dir, "*.png"))
        {
            var path = f.Replace(Path.DirectorySeparatorChar, '/');
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            n++;
        }
        return $"{n} icônes";
    }
}
#endif
