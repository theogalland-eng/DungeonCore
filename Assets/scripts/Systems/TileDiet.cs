using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// ALLÈGEMENT DES TUILES ANIMÉES (au lancement de la partie) : Unity fait avancer chaque tuile animée à chaque image,
/// visible ou non. L'île en comptait ~45 000 :
///   - NoPathfinding (collisions seules, jamais affiché) : ~16 000 tuiles de mer animées pour rien → copies fixes
///     (même nom, même collision : les tests « nom contient sea » et les collisions restent identiques) ;
///   - la mer (T_Eau, ~29 000) : seule une bande le long des côtes reste animée (là où l'on regarde) ; le large
///     garde sa première image.
/// </summary>
public static class TileDiet
{
    private const int ShoreBand = 5;
    private static bool done;
    /// <summary>Éditeur : fabrique des copies fixes enregistrées comme fichiers (allègement fait une fois dans la scène).</summary>
    public static System.Func<string, Sprite, Tile.ColliderType, Tile> MakeCopy;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => done = false;

    public static string Run(bool force = false)
    {
        if (done && !force) return "déjà fait";
        done = true;
        Tilemap blocked = null, water = null;
        var land = new List<Tilemap>();
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name == "NoPathfinding") blocked = tm;
            else if (tm.name == "T_Eau") water = tm;
            else if (tm.name == "T_Sable" || tm.name == "T_Herbe" || tm.name == "T_Roche" || tm.name == "T_Jungle") land.Add(tm);
        }
        if (!force && blocked != null && blocked.GetComponent<TileDietBaked>() != null) return "déjà allégé dans la scène";
        int a = blocked != null ? Freeze(blocked, null) : 0;
        if (MakeCopy != null && blocked != null && blocked.GetComponent<TileDietBaked>() == null) blocked.gameObject.AddComponent<TileDietBaked>();
        int b = water != null ? Freeze(water, ShoreCells(water, land)) : 0;
        return $"tuiles figées : collisions {a}, large {b}";
    }

    private static bool Animated(TileBase t) => t is PhaseTile || t is AnimatedTileLite;

    /// <summary>Remplace les tuiles animées par des copies fixes (sauf les cases à garder animées).</summary>
    private static int Freeze(Tilemap tm, HashSet<Vector3Int> keep)
    {
        var bounds = tm.cellBounds;
        var block = tm.GetTilesBlock(bounds);
        var copies = new Dictionary<(string, Sprite, Tile.ColliderType), Tile>();
        int n = 0, i = 0;
        for (int z = bounds.zMin; z < bounds.zMax; z++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++, i++)
                {
                    var t = block[i];
                    if (t == null || !Animated(t)) continue;
                    var c = new Vector3Int(x, y, z);
                    if (keep != null && keep.Contains(c)) continue;
                    var sprite = tm.GetSprite(c);
                    var col = tm.GetColliderType(c);
                    var key = (t.name, sprite, col);
                    if (!copies.TryGetValue(key, out var copy))
                    {
                        if (MakeCopy != null) copy = MakeCopy(t.name, sprite, col);
                        else
                        {
                            copy = ScriptableObject.CreateInstance<Tile>();
                            copy.name = t.name;                    // les requêtes testent le nom (« sea »)
                            copy.sprite = sprite;
                            copy.colliderType = col;
                            copy.flags = TileFlags.None;
                        }
                        copies[key] = copy;
                    }
                    block[i] = copy;
                    n++;
                }
        if (n > 0) tm.SetTilesBlock(bounds, block);
        return n;
    }

    /// <summary>Cases de mer à moins de ShoreBand cases d'une plage (parcours en largeur depuis la côte).</summary>
    private static HashSet<Vector3Int> ShoreCells(Tilemap water, List<Tilemap> land)
    {
        var keep = new HashSet<Vector3Int>();
        bool Land(Vector3Int c) { foreach (var l in land) if (l.HasTile(c)) return true; return false; }
        bool Covered(Vector3Int c)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (!Land(c + new Vector3Int(dx, dy, 0))) return false;
            return true;
        }
        var frontier = new Queue<(Vector3Int c, int d)>();
        var b = water.cellBounds;
        var block = water.GetTilesBlock(b);
        int i = 0;
        for (int z = b.zMin; z < b.zMax; z++)
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++, i++)
                {
                    if (block[i] == null) continue;
                    var c = new Vector3Int(x, y, z);
                    // l'eau entièrement recouverte par la terre (elle et ses 8 voisines) ne se voit pas : figée
                    if (Covered(c)) continue;
                    bool coast = false;
                    for (int dx = -1; dx <= 1 && !coast; dx++)
                        for (int dy = -1; dy <= 1 && !coast; dy++)
                            if (Land(c + new Vector3Int(dx, dy, 0))) coast = true;
                    if (coast && keep.Add(c)) frontier.Enqueue((c, 0));
                }
        var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        while (frontier.Count > 0)
        {
            var (c, d) = frontier.Dequeue();
            if (d >= ShoreBand) continue;
            foreach (var dir in dirs)
            {
                var n = c + dir;
                if (!water.HasTile(n) || Covered(n) || !keep.Add(n)) continue;
                frontier.Enqueue((n, d + 1));
            }
        }
        return keep;
    }
}
