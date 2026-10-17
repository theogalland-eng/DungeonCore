using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tuile pleine en 9 phases (eau) : la texture se raccorde sur 3 x 3 cases. Animée (vaguelettes) si la feuille a
/// plusieurs images : sprites[image * 9 + phase]. Teintée case par case par le générateur (du large au lagon).
/// </summary>
public class PhaseTile : TileBase
{
    public Sprite[] sprites;
    public int frames = 1;
    public float fps = 3f;

    public override void GetTileData(Vector3Int p, ITilemap tm, ref TileData data)
    {
        int i = BlobTile.Phase(p);
        data.sprite = sprites != null && sprites.Length > i ? sprites[i] : null;
        data.color = Color.white;
        data.flags = TileFlags.None;
        data.colliderType = Tile.ColliderType.None;
    }

    public override bool GetTileAnimationData(Vector3Int p, ITilemap tm, ref TileAnimationData data)
    {
        if (frames <= 1 || sprites == null || sprites.Length < frames * 9) return false;
        int ph = BlobTile.Phase(p);
        var anim = new Sprite[frames];
        for (int f = 0; f < frames; f++) anim[f] = sprites[f * 9 + ph];
        data.animatedSprites = anim;
        data.animationSpeed = fps;
        // décalage par grande zone : la mer ne « clignote » pas d'un bloc
        data.animationStartTime = Mathf.Repeat((p.x / 6) * 0.37f + (p.y / 6) * 0.61f, 1f) * frames / fps;
        return true;
    }
}
