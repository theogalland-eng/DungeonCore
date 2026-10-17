using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BROUILLARD DE GUERRE DES INTRUS : ils ne connaissent pas le donjon. Chaque EXPÉDITION a sa carte : ses membres
/// notent ce qu'ils voient (cases en vue, murs, objets encombrants, portes fermées) et la partagent entre eux.
/// Rien n'est su d'avance — sauf par un vétéran (Némésis) qui revient avec le souvenir de sa propre carte.
/// Sans chemin connu vers le cristal, ils explorent : ils suivent une galerie jusqu'au bout ou jusqu'au prochain
/// carrefour, puis choisissent de continuer ou de faire demi-tour ; une zone déjà vue et vide ne les rappelle pas.
/// </summary>
public class IntruderMap
{
    /// <summary>Sol repéré.</summary>
    public readonly HashSet<Vector3Int> Floor = new HashSet<Vector3Int>();
    /// <summary>Roche repérée.</summary>
    public readonly HashSet<Vector3Int> Walls = new HashSet<Vector3Int>();
    /// <summary>Cases encombrées (coffre, tonneau, statue...) : on les contourne.</summary>
    public readonly HashSet<Vector3Int> Blocked = new HashSet<Vector3Int>();
    /// <summary>Pièges repérés par l'expédition : on les contourne quand un autre passage existe.</summary>
    public readonly HashSet<Vector3Int> Traps = new HashSet<Vector3Int>();
    private bool avoidTraps;
    /// <summary>Case où l'on a vu le cristal (null : inconnue).</summary>
    public Vector3Int? CoreCell;
    /// <summary>Change à chaque découverte.</summary>
    public int Version;

    public const int SightRadius = 5;
    private static readonly Vector3Int[] Dirs = DungeonTerritory.Dirs;

    // ─────────────────────────────────────────────
    // SOUVENIRS (Némésis)
    // ─────────────────────────────────────────────

    /// <summary>La carte d'un vétéran : cases à plat (x, y), puis le cristal s'il l'avait vu.</summary>
    public void Remember(List<int> cells, bool coreKnown, int cx, int cy)
    {
        if (cells != null) for (int i = 0; i + 1 < cells.Count; i += 2) Floor.Add(new Vector3Int(cells[i], cells[i + 1], 0));
        if (coreKnown) CoreCell = new Vector3Int(cx, cy, 0);
        Version++;
    }

    public void Export(List<int> cells, out bool coreKnown, out int cx, out int cy)
    {
        foreach (var c in Floor) { cells.Add(c.x); cells.Add(c.y); }
        coreKnown = CoreCell.HasValue;
        cx = CoreCell.HasValue ? CoreCell.Value.x : 0;
        cy = CoreCell.HasValue ? CoreCell.Value.y : 0;
    }

    // ─────────────────────────────────────────────
    // OBSERVATION
    // ─────────────────────────────────────────────

    /// <summary>Porte fermée sur la case ?</summary>
    public static bool ClosedDoorAt(Vector3Int c)
    {
        var b = DungeonBuilder.Instance;
        if (b == null) return false;
        foreach (var d in DungeonDoor.All)
            if (d != null && !d.IsOpen && b.floorTilemap.WorldToCell(d.transform.position) == c) return true;
        return false;
    }

    private static bool BlockingObjectAt(Vector3Int c)
    {
        var b = DungeonBuilder.Instance;
        foreach (var p in PlacedObject.All)
            if (p != null && p.Blocking && b.floorTilemap.WorldToCell(p.transform.position) == c) return true;
        return false;
    }

    /// <summary>Ce que voit un intrus depuis sa position (ligne de vue, rayon de 5 cases ; une porte fermée arrête le regard).</summary>
    public void Observe(Vector2 pos, Transform core)
    {
        var b = DungeonBuilder.Instance;
        var t = DungeonTerritory.Instance;
        if (b == null || t == null) return;
        var c0 = b.floorTilemap.WorldToCell(pos);
        int before = Floor.Count + Walls.Count * 3 + Blocked.Count * 5 + (CoreCell.HasValue ? 7 : 0);

        var open = new Queue<Vector3Int>();
        var seen = new HashSet<Vector3Int> { c0 };
        open.Enqueue(c0);
        while (open.Count > 0)
        {
            var c = open.Dequeue();
            // on vise le bord de la case : un objet posé dessus ne la cache pas à elle-même
            Vector2 center = b.floorTilemap.GetCellCenterWorld(c);
            Vector2 toC = center - pos;
            Vector2 aim = toC.magnitude > 0.6f ? center - toC.normalized * 0.45f : center;
            bool visible = c == c0 || AdventurerAI.ClearLine(pos, aim, 1 << 6, false);
            if (!visible) continue;
            if (t.IsDungeonGround(c))
            {
                Floor.Add(c);
                Walls.Remove(c);
                if (BlockingObjectAt(c)) Blocked.Add(c); else Blocked.Remove(c);
            }
            // on ne voit pas à travers une porte fermée (on sait juste qu'elle est là)
            if (c != c0 && ClosedDoorAt(c)) continue;
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (Mathf.Max(Mathf.Abs(n.x - c0.x), Mathf.Abs(n.y - c0.y)) > SightRadius || !seen.Add(n)) continue;
                if (b.stoneTilemap.HasTile(n)) { Walls.Add(n); Floor.Remove(n); continue; }   // muraille (ou galerie rebouchée)
                if (t.IsDungeonGround(n)) open.Enqueue(n);
            }
        }
        // le cristal : vu, ou absent de l'endroit où on le croyait (il a été déplacé)
        if (core != null)
        {
            var cc = b.floorTilemap.WorldToCell(core.position);
            if (Vector2.Distance(core.position, pos) <= SightRadius + 0.5f && AdventurerAI.ClearLine(pos, core.position, 1 << 6)) CoreCell = cc;
            else if (CoreCell.HasValue && CoreCell.Value != cc && seen.Contains(CoreCell.Value) &&
                     AdventurerAI.ClearLine(pos, b.floorTilemap.GetCellCenterWorld(CoreCell.Value), 1 << 6)) CoreCell = null;
        }
        if (Floor.Count + Walls.Count * 3 + Blocked.Count * 5 + (CoreCell.HasValue ? 7 : 0) != before) Version++;
    }

    // ─────────────────────────────────────────────
    // CHEMINS
    // ─────────────────────────────────────────────

    /// <summary>Escalier sur la case : la case d'arrivée à l'autre étage.</summary>
    public static Vector3Int? StairsExit(Vector3Int c)
    {
        foreach (var st in DungeonStairs.All)
            if (st != null && st.partner != null && st.Cell == c) return st.partner.Cell;
        return null;
    }

    private bool Walkable(Vector3Int c) => Floor.Contains(c) && !Walls.Contains(c) && !Blocked.Contains(c) && !(avoidTraps && Traps.Contains(c));

    /// <summary>Case connue dont un voisin reste à découvrir (une porte fermée encore jamais franchie en est une).</summary>
    public bool IsFrontier(Vector3Int c)
    {
        if (!Walkable(c)) return false;
        var b = DungeonBuilder.Instance;
        var t = DungeonTerritory.Instance;
        var exit = StairsExit(c);
        if (exit.HasValue && !Floor.Contains(exit.Value)) return true;
        foreach (var d in Dirs)
        {
            var n = c + d;
            if (Floor.Contains(n) || Walls.Contains(n) || Blocked.Contains(n)) continue;
            if (t != null && (t.IsDungeonGround(n) || b.stoneTilemap.HasTile(n))) return true;   // l'extérieur ne compte pas
        }
        return false;
    }

    private IEnumerable<Vector3Int> Neighbours(Vector3Int c)
    {
        foreach (var d in Dirs) yield return c + d;
        var exit = StairsExit(c);
        if (exit.HasValue) yield return exit.Value;
    }

    /// <summary>Plus court chemin à travers le plan connu jusqu'à une case qui satisfait 'goal' (null : aucun).</summary>
    public List<Vector3Int> FindPath(Vector3Int from, Func<Vector3Int, bool> goal, int maxNodes = 6000)
    {
        // d'abord un chemin qui évite les pièges connus ; sinon on passe quand même
        if (Traps.Count > 0)
        {
            avoidTraps = true;
            var safe = Search(from, goal, maxNodes);
            avoidTraps = false;
            if (safe != null) return safe;
        }
        return Search(from, goal, maxNodes);
    }

    private List<Vector3Int> Search(Vector3Int from, Func<Vector3Int, bool> goal, int maxNodes)
    {
        var prev = new Dictionary<Vector3Int, Vector3Int> { [from] = from };
        var q = new Queue<Vector3Int>();
        q.Enqueue(from);
        int n = 0;
        while (q.Count > 0 && n++ < maxNodes)
        {
            var c = q.Dequeue();
            if (goal(c)) return Trace(prev, from, c);
            foreach (var m in Neighbours(c))
            {
                if (prev.ContainsKey(m) || !Walkable(m)) continue;
                prev[m] = c;
                q.Enqueue(m);
            }
        }
        return null;
    }

    /// <summary>Frontières accessibles, de la plus proche à la plus lointaine (les 'count' premières, avec leur chemin).</summary>
    public List<List<Vector3Int>> NearestFrontiers(Vector3Int from, int count)
    {
        if (Traps.Count > 0)
        {
            avoidTraps = true;
            var safe = Frontiers(from, count);
            avoidTraps = false;
            if (safe.Count > 0) return safe;
        }
        return Frontiers(from, count);
    }

    private List<List<Vector3Int>> Frontiers(Vector3Int from, int count)
    {
        var result = new List<List<Vector3Int>>();
        var prev = new Dictionary<Vector3Int, Vector3Int> { [from] = from };
        var q = new Queue<Vector3Int>();
        q.Enqueue(from);
        while (q.Count > 0 && result.Count < count && prev.Count < 6000)
        {
            var c = q.Dequeue();
            if (c != from && IsFrontier(c)) result.Add(Trace(prev, from, c));
            foreach (var m in Neighbours(c))
            {
                if (prev.ContainsKey(m) || !Walkable(m)) continue;
                prev[m] = c;
                q.Enqueue(m);
            }
        }
        return result;
    }

    private static List<Vector3Int> Trace(Dictionary<Vector3Int, Vector3Int> prev, Vector3Int from, Vector3Int to)
    {
        var list = new List<Vector3Int>();
        for (var x = to; ; x = prev[x]) { list.Add(x); if (x == from) break; }
        list.Reverse();
        return list;
    }

    /// <summary>Le chemin mène à un escalier inexploré : on le descend (l'arrivée devient connue).</summary>
    public void ExtendThroughStairs(List<Vector3Int> cells)
    {
        if (cells == null || cells.Count == 0) return;
        var exit = StairsExit(cells[cells.Count - 1]);
        if (!exit.HasValue || Floor.Contains(exit.Value)) return;
        Floor.Add(exit.Value);
        cells.Add(exit.Value);
        Version++;
    }

    /// <summary>Convertit une suite de cases en points, en coupant les lignes droites dégagées.</summary>
    public static List<Vector3> ToWaypoints(List<Vector3Int> cells, Vector2 start)
    {
        var b = DungeonBuilder.Instance;
        var pts = new List<Vector3>();
        foreach (var c in cells) pts.Add(b.floorTilemap.GetCellCenterWorld(c));
        if (pts.Count == 0) return pts;
        // lissage : on vise le point le plus lointain atteignable en ligne droite (sans raser les angles ni les objets)
        var smooth = new List<Vector3> { start };
        int i = 0;
        while (i < pts.Count - 1)
        {
            int best = i + 1;
            for (int j = Mathf.Min(pts.Count - 1, i + 6); j > i + 1; j--)
                if (!HasJump(pts, i, j) && Physics2D.CircleCast(pts[i], 0.3f, (pts[j] - pts[i]).normalized, Vector2.Distance(pts[i], pts[j]), 1 << 6).collider == null) { best = j; break; }
            smooth.Add(pts[best]);
            i = best;
        }
        if (pts.Count == 1) smooth.Add(pts[0]);
        return smooth;
    }

    private static bool HasJump(List<Vector3> pts, int i, int j)
    {
        for (int k = i; k < j; k++) if ((pts[k + 1] - pts[k]).sqrMagnitude > 4f) return true;
        return false;
    }
}
