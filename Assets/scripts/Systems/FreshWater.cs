using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Points d'eau douce de l'île (les petits lacs du générateur) : où les animaux viennent boire.
/// Repérés au lancement : une case d'eau infranchissable qui n'est ni la mer, ni la lave, ni une falaise.
/// Chaque lac est une liste de « berges » (cases de terre ferme qui touchent l'eau).
/// </summary>
public static class FreshWater
{
    public class Lake
    {
        public Vector2 center;
        public readonly List<Vector2> shore = new List<Vector2>();
    }

    private static List<Lake> lakes;

    public static List<Lake> Lakes
    {
        get
        {
            if (lakes == null) Scan();
            return lakes;
        }
    }

    public static void Reset() => lakes = null;

    private static void Scan()
    {
        lakes = new List<Lake>();
        Tilemap blocked = null, lava = null, cliff = null, sol = null;
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name == "NoPathfinding") blocked = tm;
            else if (tm.name == "T_Lave") lava = tm;
            else if (tm.name == "T_Falaise") cliff = tm;
            else if (tm.name == "Sol") sol = tm;
        }
        if (blocked == null || sol == null) return;

        var water = new HashSet<Vector3Int>();
        foreach (var c in blocked.cellBounds.allPositionsWithin)
        {
            var t = blocked.GetTile(c);
            if (t == null || t.name.Contains("sea")) continue;
            if (lava != null && lava.HasTile(c)) continue;
            if (cliff != null && cliff.HasTile(c)) continue;
            water.Add(c);
        }
        // regroupe les cases d'eau en lacs
        var seen = new HashSet<Vector3Int>();
        var dirs = DungeonTerritory.Dirs;
        foreach (var start in water)
        {
            if (seen.Contains(start)) continue;
            var lake = new Lake();
            var q = new Queue<Vector3Int>();
            q.Enqueue(start); seen.Add(start);
            Vector2 sum = Vector2.zero; int n = 0;
            var shoreSet = new HashSet<Vector3Int>();
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                sum += (Vector2)sol.GetCellCenterWorld(c); n++;
                foreach (var d in dirs)
                {
                    var m = c + d;
                    if (water.Contains(m)) { if (seen.Add(m)) q.Enqueue(m); }
                    else if (!blocked.HasTile(m) && sol.HasTile(m)) shoreSet.Add(m);
                }
            }
            if (n < 3 || shoreSet.Count == 0) continue;
            lake.center = sum / n;
            foreach (var s in shoreSet) lake.shore.Add(sol.GetCellCenterWorld(s));
            lakes.Add(lake);
        }
    }

    /// <summary>Lac le plus proche (null s'il n'y en a pas).</summary>
    public static Lake Nearest(Vector2 p)
    {
        Lake best = null;
        float bd = float.MaxValue;
        foreach (var l in Lakes)
        {
            float d = (l.center - p).sqrMagnitude;
            if (d < bd) { bd = d; best = l; }
        }
        return best;
    }
}
