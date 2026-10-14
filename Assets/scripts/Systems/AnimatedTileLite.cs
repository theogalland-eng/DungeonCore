using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tuile animée (mer, lave) sans paquet externe. Chaque case démarre à un instant différent
/// pour éviter l'effet de « clignotement » uniforme.
/// </summary>
[CreateAssetMenu(menuName = "DungeonCore/Tuile animée", fileName = "AnimatedTile")]
public class AnimatedTileLite : TileBase
{
    public Sprite[] frames;
    public float speed = 4f;
    public Tile.ColliderType colliderType = Tile.ColliderType.None;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        tileData.color = Color.white;
        tileData.flags = TileFlags.None;
        tileData.colliderType = colliderType;
        tileData.transform = Matrix4x4.identity;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
    {
        if (frames == null || frames.Length < 2) return false;
        data.animatedSprites = frames;
        data.animationSpeed = speed;
        uint h = (uint)(position.x * 73856093) ^ (uint)(position.y * 19349663);
        data.animationStartTime = (h % 1000) / 1000f * frames.Length / speed;
        return true;
    }
}
