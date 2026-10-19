using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// RELIEF DES MURS : la roche est un gros tilemap dessiné d'un bloc sous les personnages. Pour qu'un monstre,
/// un intrus ou une statue posté juste au nord d'un mur soit masqué par ce mur (et passe devant quand il est au sud),
/// on recopie seulement les murs du bord des galeries dans un tilemap « avant-plan » trié case par case
/// (axe Y, même ordre que les personnages). Le reste de la roche, invisible derrière, reste en un seul bloc :
/// quelques centaines de tuiles triées au lieu de dizaines de milliers.
/// Les copies suivent les creusements et les murs posés (événement Tilemap.tilemapTileChanged).
/// </summary>
public class WallOcclusion : MonoBehaviour
{
    public const int SortingOrder = 3;      // ordre des personnages, accessoires et pièges debout
    public static WallOcclusion Instance { get; private set; }

    private Tilemap stone, placed, overlay;
    private readonly Dictionary<Sprite, Tile> tiles = new Dictionary<Sprite, Tile>();
    private readonly HashSet<Vector3Int> dirty = new HashSet<Vector3Int>();
    private static readonly Vector3Int[] Around8 =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
        new Vector3Int(1, 1, 0), new Vector3Int(-1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0),
    };

    public static WallOcclusion Create(Tilemap stone, Tilemap placed)
    {
        if (stone == null) return null;
        var go = new GameObject("Murs (avant-plan)");
        go.transform.SetParent(stone.transform.parent, false);
        go.transform.localPosition = stone.transform.localPosition;
        var w = go.AddComponent<WallOcclusion>();
        w.stone = stone;
        w.placed = placed;
        w.overlay = go.AddComponent<Tilemap>();
        w.overlay.tileAnchor = stone.tileAnchor;
        w.overlay.orientation = stone.orientation;
        var src = stone.GetComponent<TilemapRenderer>();
        var r = go.AddComponent<TilemapRenderer>();
        r.mode = TilemapRenderer.Mode.Individual;      // chaque tuile triée avec les personnages
        if (src != null) { r.sharedMaterial = src.sharedMaterial; r.sortingLayerID = src.sortingLayerID; }
        r.sortingOrder = SortingOrder;
        w.RebuildAll();
        return w;
    }

    private void OnEnable() { Instance = this; Tilemap.tilemapTileChanged += OnTilesChanged; }
    private void OnDisable() { if (Instance == this) Instance = null; Tilemap.tilemapTileChanged -= OnTilesChanged; }

    /// <summary>Une case de roche a changé de teinte : sa copie sera mise à jour.</summary>
    public void Mark(Vector3Int c) => dirty.Add(c);

    private void OnTilesChanged(Tilemap tm, Tilemap.SyncTile[] changes)
    {
        if (tm != stone && tm != placed) return;
        // le sprite des murs voisins change aussi (mur auto-connecté : jusqu'à deux cases au sud)
        foreach (var c in changes)
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    dirty.Add(c.position + new Vector3Int(dx, dy, 0));
    }

    private void LateUpdate()
    {
        if (dirty.Count == 0) return;
        foreach (var c in dirty) Refresh(c);
        dirty.Clear();
    }

    private bool IsWall(Vector3Int c) => stone.HasTile(c) || (placed != null && placed.HasTile(c));

    /// <summary>Mur du bord : au moins une case ouverte autour de lui.</summary>
    private bool OnEdge(Vector3Int c)
    {
        foreach (var d in Around8) if (!IsWall(c + d)) return true;
        return false;
    }

    private void Refresh(Vector3Int c)
    {
        Tilemap src = placed != null && placed.HasTile(c) ? placed : stone.HasTile(c) ? stone : null;
        if (src == null || !OnEdge(c)) { if (overlay.HasTile(c)) overlay.SetTile(c, null); return; }
        var sprite = src.GetSprite(c);
        if (sprite == null) { overlay.SetTile(c, null); return; }
        if (!tiles.TryGetValue(sprite, out var t))
        {
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = sprite;
            t.colliderType = Tile.ColliderType.None;
            t.flags = TileFlags.None;                  // couleur et matrice recopiées case par case
            tiles[sprite] = t;
        }
        overlay.SetTile(c, t);
        overlay.SetTransformMatrix(c, src.GetTransformMatrix(c));
        overlay.SetColor(c, src.GetColor(c) * src.color);
    }

    /// <summary>Recopie complète (chargement, changement d'étage).</summary>
    public void RebuildAll()
    {
        overlay.ClearAllTiles();
        foreach (var tm in new[] { stone, placed })
        {
            if (tm == null) continue;
            var b = tm.cellBounds;
            var block = tm.GetTilesBlock(b);
            int i = 0;
            for (int z = b.zMin; z < b.zMax; z++)
                for (int y = b.yMin; y < b.yMax; y++)
                    for (int x = b.xMin; x < b.xMax; x++, i++)
                        if (block[i] != null) Refresh(new Vector3Int(x, y, z));
        }
    }

    /// <summary>Les teintes d'étage ont changé (DungeonLayers) : on recolore les copies.</summary>
    public void Retint()
    {
        foreach (var c in overlay.cellBounds.allPositionsWithin)
            if (overlay.HasTile(c)) dirty.Add(c);
    }
}
