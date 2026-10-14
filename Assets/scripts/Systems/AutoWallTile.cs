using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Mur de roche auto-connecté : choisit son sprite selon ses voisins
/// (dessus du mur, bords éclairés, face visible quand le sud est ouvert).
/// Les 128 variantes sont générées par Tools~/make_walls.py puis DungeonCore/6. Murs et sols.
/// Masque : 1 N, 2 E, 4 S, 8 W, 16 SS (deux cases au sud), 32 SW, 64 SE (bit = voisin mur).
/// </summary>
[CreateAssetMenu(menuName = "DungeonCore/Mur auto-connecté", fileName = "AutoWall")]
public class AutoWallTile : TileBase
{
    public Sprite[] sprites = new Sprite[128];
    public Tile.ColliderType colliderType = Tile.ColliderType.Grid;

    // Cases dont le sprite dépend de celle-ci (inverse des voisins du masque)
    private static readonly Vector3Int[] Dependents =
    {
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 2, 0), new Vector3Int(-1, 1, 0), new Vector3Int(1, 1, 0),
    };

    private static bool Wall(ITilemap tm, Vector3Int p) => tm.GetTile(p) != null;

    public static int Mask(ITilemap tm, Vector3Int p)
    {
        int m = 0;
        if (Wall(tm, p + Vector3Int.up)) m |= 1;
        if (Wall(tm, p + Vector3Int.right)) m |= 2;
        if (Wall(tm, p + Vector3Int.down)) m |= 4;
        if (Wall(tm, p + Vector3Int.left)) m |= 8;
        if (Wall(tm, p + new Vector3Int(0, -2, 0))) m |= 16;
        if (Wall(tm, p + new Vector3Int(-1, -1, 0))) m |= 32;
        if (Wall(tm, p + new Vector3Int(1, -1, 0))) m |= 64;
        return m;
    }

    public Sprite SpriteFor(int mask)
    {
        if (sprites == null || sprites.Length == 0) return null;
        var s = mask < sprites.Length ? sprites[mask] : null;
        return s != null ? s : sprites[sprites.Length - 1];
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = SpriteFor(Mask(tilemap, position));
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.LockTransform;
        tileData.colliderType = colliderType;
    }

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        tilemap.RefreshTile(position);
        foreach (var d in Dependents) tilemap.RefreshTile(position + d);
    }

    /// <summary>Rafraîchit les voisins d'une case modifiée (utile après avoir retiré un mur).</summary>
    public static void RefreshAround(Tilemap tm, Vector3Int c)
    {
        tm.RefreshTile(c);
        foreach (var d in Dependents) tm.RefreshTile(c + d);
    }
}
