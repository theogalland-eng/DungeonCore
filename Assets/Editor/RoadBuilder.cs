using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Routes pavées : le sentier de terre du générateur d'île (gravier teinté de brun) devient une route de pavés
/// sur un tilemap « Routes » (au-dessus du sol, sous les arbres). Les touffes d'herbe du tilemap Decor sont retirées.
/// </summary>
public static class RoadBuilder
{
    const string Atlas = "Assets/Art/Generated/Roads/road_tiles.png";
    const string TilePath = "Assets/Tiles/Settlement/road.asset";

    [MenuItem("DungeonCore/9. Routes pavées")]
    public static string Build()
    {
        var tile = MakeTile();
        var grid = Object.FindFirstObjectByType<Grid>();
        Tilemap sol = null, decor = null;
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name == "Sol") sol = tm;
            if (tm.name == "Decor") decor = tm;
        }
        if (grid == null || sol == null) return "Sol introuvable";
        var roads = GetOrCreate(grid, "Routes");
        roads.ClearAllTiles();
        int n = 0;
        foreach (var p in sol.cellBounds.allPositionsWithin)
        {
            var t = sol.GetTile(p);
            if (t == null || !t.name.StartsWith("island_gravel")) continue;
            // le sentier est teinté de brun (le gravier « naturel » ne l'est pas)
            var c = sol.GetColor(p);
            if (!(c.r > 0.62f && c.r < 0.92f && c.g > 0.5f && c.g < 0.78f && c.b > 0.36f && c.b < 0.64f && c.r - c.b > 0.18f)) continue;
            roads.SetTile(p, tile);
            if (decor != null) decor.SetTile(p, null);
            n++;
        }
        // Lissage : on retire les « dents » (case isolée collée à un tronçon épais)
        int removed = 0;
        var cells = new List<Vector3Int>();
        foreach (var p in roads.cellBounds.allPositionsWithin) if (roads.HasTile(p)) cells.Add(p);
        int Count(Vector3Int p) => (roads.HasTile(p + Vector3Int.up) ? 1 : 0) + (roads.HasTile(p + Vector3Int.down) ? 1 : 0) +
                                   (roads.HasTile(p + Vector3Int.left) ? 1 : 0) + (roads.HasTile(p + Vector3Int.right) ? 1 : 0);
        foreach (var p in cells)
        {
            if (Count(p) != 1) continue;
            Vector3Int nb = p;
            foreach (var d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right }) if (roads.HasTile(p + d)) nb = p + d;
            if (Count(nb) >= 3)
            {
                roads.SetTile(p, null);
                removed++;
                // le sol reprend l'aspect d'un voisin qui n'est pas du sentier (herbe, en général)
                foreach (var d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                {
                    var t = sol.GetTile(p + d);
                    if (t == null || t.name.StartsWith("island_gravel") || roads.HasTile(p + d)) continue;
                    sol.SetTile(p, t);
                    sol.SetTileFlags(p, TileFlags.None);
                    sol.SetColor(p, sol.GetColor(p + d));
                    break;
                }
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(roads.gameObject.scene);
        return $"{n - removed} cases pavées ({removed} dents retirées)";
    }

    static Tilemap GetOrCreate(Grid grid, string name)
    {
        var t = grid.transform.Find(name);
        if (t != null) return t.GetComponent<Tilemap>();
        var go = new GameObject(name);
        go.transform.SetParent(grid.transform, false);
        var tm = go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        r.sortingOrder = 1;
        r.mode = TilemapRenderer.Mode.Chunk;
        return tm;
    }

    static RoadTile MakeTile()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(Atlas);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < 32; i++)
        {
            int col = i % 8, row = i / 8;   // ligne 0 en haut de l'image
            metas.Add(new SpriteMetaData { name = $"road_{i}", rect = new Rect(col * 16, 64 - (row + 1) * 16, 16, 16), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
        }
#pragma warning disable 618
        imp.spritesheet = metas.ToArray();
#pragma warning restore 618
        imp.SaveAndReimport();
        var sprites = new Sprite[32];
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Atlas))
            if (o is Sprite s && s.name.StartsWith("road_")) sprites[int.Parse(s.name.Substring(5))] = s;
        // l'atlas range les masques 0-7 puis 8-15 sur deux lignes par variante
        var ordered = new Sprite[32];
        for (int v = 0; v < 2; v++)
            for (int m = 0; m < 16; m++)
            {
                int row = v * 2 + m / 8, col = m % 8;
                ordered[v * 16 + m] = sprites[row * 8 + col];
            }
        var tile = AssetDatabase.LoadAssetAtPath<RoadTile>(TilePath);
        if (tile == null) { tile = ScriptableObject.CreateInstance<RoadTile>(); AssetDatabase.CreateAsset(tile, TilePath); }
        tile.sprites = ordered;
        tile.color = new Color(0.86f, 0.76f, 0.66f);
        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();
        return tile;
    }
}
