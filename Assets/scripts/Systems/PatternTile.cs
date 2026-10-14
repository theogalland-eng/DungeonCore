using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Sol à motif : répète un bloc de sprites (ex : dalles 2x2) ou tire une variante
/// pseudo-aléatoire stable par case (random = true).
/// </summary>
[CreateAssetMenu(menuName = "DungeonCore/Sol à motif", fileName = "PatternFloor")]
public class PatternTile : TileBase
{
    [Tooltip("Sprites du motif, ligne du haut d'abord")]
    public Sprite[] sprites;
    [Tooltip("Largeur du motif (en cases)")]
    public int width = 1;
    [Tooltip("Variante aléatoire (stable) au lieu d'un motif régulier")]
    public bool random;

    public override void GetTileData(Vector3Int p, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = Pick(p);
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.None;
        tileData.colliderType = Tile.ColliderType.None;
    }

    public Sprite Pick(Vector3Int p)
    {
        if (sprites == null || sprites.Length == 0) return null;
        if (random)
        {
            uint h = (uint)(p.x * 73856093) ^ (uint)(p.y * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return sprites[h % (uint)sprites.Length];
        }
        int w = Mathf.Max(1, width);
        int rows = Mathf.Max(1, sprites.Length / w);
        int x = ((p.x % w) + w) % w;
        int y = rows - 1 - (((p.y % rows) + rows) % rows);
        return sprites[Mathf.Clamp(y * w + x, 0, sprites.Length - 1)];
    }
}
