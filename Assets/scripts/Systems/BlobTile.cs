using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tuile de terrain auto-raccordée « blob » (47 cas) : un matériau (sable, herbe, roche, sentier...) sur son propre
/// tilemap. Le bord est dessiné dans la case, selon les 8 voisines du même matériau (coins comptés seulement si les
/// deux côtés adjacents sont pleins). Chaque cas existe en 9 phases (texture de période 3 x 3 cases) :
/// la phase vient de la position de la case, donc la texture se raccorde partout sans répétition serrée.
/// sprites[phase * 47 + index du masque] ; générées par Tools~/make_terrain.py.
/// Fonctionne aussi en jeu : poser une case met à jour ses voisines (sentiers qui naissent du passage).
/// </summary>
public class BlobTile : TileBase
{
    public Sprite[] sprites;
    public Color color = Color.white;
    [Tooltip("Autres tuiles considérées comme le même matériau (ex. : herbe foulée ↔ sentier)")]
    public TileBase[] sameAs;

    private static int[] maskToIndex;

    private static void BuildMap()
    {
        maskToIndex = new int[256];
        var seen = new System.Collections.Generic.SortedSet<int>();
        for (int m = 0; m < 256; m++) seen.Add(Reduce(m));
        int i = 0;
        var order = new int[256];
        foreach (var m in seen) order[m] = i++;
        for (int m = 0; m < 256; m++) maskToIndex[m] = order[Reduce(m)];
    }

    /// <summary>Un coin ne compte que si les deux côtés qui l'encadrent sont pleins.</summary>
    private static int Reduce(int m)
    {
        if ((m & 1) == 0 || (m & 4) == 0) m &= ~2;
        if ((m & 4) == 0 || (m & 16) == 0) m &= ~8;
        if ((m & 16) == 0 || (m & 64) == 0) m &= ~32;
        if ((m & 64) == 0 || (m & 1) == 0) m &= ~128;
        return m;
    }

    private bool Same(ITilemap tm, Vector3Int p)
    {
        var t = tm.GetTile(p);
        if (t == null) return false;
        if (t == this) return true;
        if (sameAs != null) foreach (var s in sameAs) if (s == t) return true;
        return false;
    }

    public static int Phase(Vector3Int p) => ((p.x % 3) + 3) % 3 + 3 * (((-p.y) % 3 + 3) % 3);

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                tilemap.RefreshTile(position + new Vector3Int(dx, dy, 0));
    }

    public override void GetTileData(Vector3Int p, ITilemap tm, ref TileData data)
    {
        if (maskToIndex == null) BuildMap();
        // bits (y vers le haut dans Unity = nord) : N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128
        int m = (Same(tm, p + new Vector3Int(0, 1, 0)) ? 1 : 0) | (Same(tm, p + new Vector3Int(1, 1, 0)) ? 2 : 0) |
                (Same(tm, p + new Vector3Int(1, 0, 0)) ? 4 : 0) | (Same(tm, p + new Vector3Int(1, -1, 0)) ? 8 : 0) |
                (Same(tm, p + new Vector3Int(0, -1, 0)) ? 16 : 0) | (Same(tm, p + new Vector3Int(-1, -1, 0)) ? 32 : 0) |
                (Same(tm, p + new Vector3Int(-1, 0, 0)) ? 64 : 0) | (Same(tm, p + new Vector3Int(-1, 1, 0)) ? 128 : 0);
        int i = Phase(p) * 47 + maskToIndex[m];
        data.sprite = sprites != null && i < sprites.Length ? sprites[i] : null;
        data.color = color;
        data.flags = TileFlags.LockColor | TileFlags.LockTransform;
        data.colliderType = Tile.ColliderType.None;
    }
}
