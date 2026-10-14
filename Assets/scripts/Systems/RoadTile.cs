using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Route pavée (tuile auto-adaptative 4 voisins) : le bord de la route s'efface en herbe là où elle s'arrête.
/// sprites[variante * 16 + masque], masque : bit0 = N, bit1 = E, bit2 = S, bit3 = O (1 = route voisine).
/// Générée par Tools / ReliefBuilder.BuildRoads à partir du pack « path and road ».
/// </summary>
[CreateAssetMenu(menuName = "DungeonCore/Route pavée")]
public class RoadTile : TileBase
{
    public Sprite[] sprites = new Sprite[32];
    public Color color = Color.white;

    private bool Has(ITilemap tm, Vector3Int p) => tm.GetTile(p) == this;

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        tilemap.RefreshTile(position);
        tilemap.RefreshTile(position + Vector3Int.up);
        tilemap.RefreshTile(position + Vector3Int.right);
        tilemap.RefreshTile(position + Vector3Int.down);
        tilemap.RefreshTile(position + Vector3Int.left);
    }

    public override void GetTileData(Vector3Int p, ITilemap tm, ref TileData data)
    {
        int m = (Has(tm, p + Vector3Int.up) ? 1 : 0) | (Has(tm, p + Vector3Int.right) ? 2 : 0) |
                (Has(tm, p + Vector3Int.down) ? 4 : 0) | (Has(tm, p + Vector3Int.left) ? 8 : 0);
        int v = ((p.x * 73856093) ^ (p.y * 19349663)) & 1;
        int i = v * 16 + m;
        data.sprite = sprites != null && i < sprites.Length ? sprites[i] : null;
        data.color = color;
        data.flags = TileFlags.LockColor;
        data.colliderType = Tile.ColliderType.None;
    }
}
