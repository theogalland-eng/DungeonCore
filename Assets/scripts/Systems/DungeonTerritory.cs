using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Territoire du donjon, case par case.
/// Une case appartient au donjon si elle est reliée au cœur par des cases de sol de caverne
/// (pas de roche, et pas un sol extérieur comme l'herbe).
/// Remplace l'ancienne détection par collider (DungeonBoundary) qui ne couvrait qu'une bande.
/// </summary>
public class DungeonTerritory : MonoBehaviour
{
    public static DungeonTerritory Instance { get; private set; }

    [Tooltip("Roche (murs creusables). Auto : DungeonBuilder.stoneTilemap")]
    public Tilemap stoneTilemap;
    [Tooltip("Sol. Auto : DungeonBuilder.floorTilemap")]
    public Tilemap floorTilemap;
    [Tooltip("Tiles de sol du donjon (auto : sol du DungeonBuilder + sols des thèmes)")]
    public List<TileBase> dungeonFloorTiles = new List<TileBase>();
    [Tooltip("Taille max du territoire (sécurité)")]
    public int maxCells = 20000;

    private readonly HashSet<Vector3Int> cells = new HashSet<Vector3Int>();
    private HashSet<TileBase> floorSet = new HashSet<TileBase>();
    private bool initialized;
    private Vector3Int coreCell;
    private Transform core;

    public int CellCount { get { EnsureInit(); return cells.Count; } }
    public Vector3Int CoreCell { get { EnsureInit(); return coreCell; } }
    public IEnumerable<Vector3Int> Cells { get { EnsureInit(); return cells; } }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        EnsureInit();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void EnsureInit()
    {
        if (initialized) return;
        var builder = FindFirstObjectByType<DungeonBuilder>();
        if (stoneTilemap == null && builder != null) stoneTilemap = builder.stoneTilemap;
        if (floorTilemap == null && builder != null) floorTilemap = builder.floorTilemap;
        if (builder != null && builder.floorTile != null && !dungeonFloorTiles.Contains(builder.floorTile))
            dungeonFloorTiles.Add(builder.floorTile);
        foreach (var th in DungeonCatalog.Get().themes)
            if (th.floorTile != null && !dungeonFloorTiles.Contains(th.floorTile)) dungeonFloorTiles.Add(th.floorTile);
        floorSet = new HashSet<TileBase>(dungeonFloorTiles);
        var dc = FindFirstObjectByType<DungeonCore>();
        core = dc != null ? dc.transform : null;
        if (stoneTilemap == null || floorTilemap == null || core == null)
        {
            Debug.LogWarning("[DungeonTerritory] Tilemaps ou cœur introuvables");
            return;
        }
        initialized = true;
        Recompute();
    }

    /// <summary>Recalcule tout le territoire depuis le cœur (flood fill).</summary>
    /// <summary>Autres points de départ du donjon (mode Aventurier : étages non reliés par un escalier du Noyau).</summary>
    public static readonly List<Vector3Int> ExtraSeeds = new List<Vector3Int>();

    public void Recompute()
    {
        cells.Clear();
        coreCell = floorTilemap.WorldToCell(core.position);
        foreach (var c in FloodFrom(coreCell, null)) cells.Add(c);
        foreach (var s in ExtraSeeds)
            if (!cells.Contains(s) && IsDungeonGround(s))
                foreach (var c in FloodFrom(s, null)) cells.Add(c);
    }

    private HashSet<Vector3Int> FloodFrom(Vector3Int start, Vector3Int? blocked)
    {
        var visited = new HashSet<Vector3Int>();
        var queue = new Queue<Vector3Int>();
        visited.Add(start);
        queue.Enqueue(start);
        while (queue.Count > 0 && visited.Count < maxCells)
        {
            var c = queue.Dequeue();
            // Les escaliers relient les étages
            foreach (var st in DungeonStairs.All)
            {
                if (st == null || st.partner == null || st.Cell != c) continue;
                var pc = st.partner.Cell;
                if (!visited.Contains(pc) && pc != blocked && IsDungeonGround(pc))
                {
                    visited.Add(pc);
                    queue.Enqueue(pc);
                }
            }
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (visited.Contains(n)) continue;
                if (blocked.HasValue && n == blocked.Value) continue;
                if (!IsDungeonGround(n)) continue;
                visited.Add(n);
                queue.Enqueue(n);
            }
        }
        return visited;
    }

    public static readonly Vector3Int[] Dirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

    /// <summary>
    /// Le donjon reste-t-il d'un seul tenant si ces cases deviennent infranchissables ?
    /// (toutes les cases libres doivent rester reliées au cœur)
    /// </summary>
    public bool StaysConnected(HashSet<Vector3Int> blocked)
    {
        EnsureInit();
        if (!initialized) return true;
        int free = 0;
        foreach (var c in cells) if (!blocked.Contains(c)) free++;
        var visited = new HashSet<Vector3Int>();
        var queue = new Queue<Vector3Int>();
        visited.Add(coreCell);
        queue.Enqueue(coreCell);
        int reached = 1;
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var st in DungeonStairs.All)
            {
                if (st == null || st.partner == null || st.Cell != c) continue;
                var pc = st.partner.Cell;
                if (cells.Contains(pc) && !blocked.Contains(pc) && visited.Add(pc)) { reached++; queue.Enqueue(pc); }
            }
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (!cells.Contains(n) || blocked.Contains(n) || !visited.Add(n)) continue;
                reached++;
                queue.Enqueue(n);
            }
        }
        return reached >= free;
    }

    /// <summary>
    /// Point libre le plus proche (case marchable, sans mur ni objet encombrant) : pour dégager une créature
    /// apparue ou coincée dans un mur. dungeonOnly : seulement sur le sol du donjon.
    /// </summary>
    public static Vector2 FreeSpotNear(Vector2 p, bool dungeonOnly, int maxRadius = 6)
    {
        var t = Instance;
        var b = DungeonBuilder.Instance;
        if (b == null) return p;
        var c0 = b.floorTilemap.WorldToCell(p);
        for (int r = 0; r <= maxRadius; r++)
        {
            Vector2 best = p;
            float bestD = float.MaxValue;
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = c0 + new Vector3Int(dx, dy, 0);
                    if (b.stoneTilemap.HasTile(c)) continue;
                    if (dungeonOnly && t != null && !t.IsDungeonGround(c)) continue;
                    Vector2 center = b.floorTilemap.GetCellCenterWorld(c);
                    if (Physics2D.OverlapCircle(center, 0.3f, 1 << 6) != null) continue;
                    var node = AstarPath.active != null ? AstarPath.active.GetNearest(center).node : null;
                    if (node != null && !node.Walkable) continue;
                    float d = (center - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = center; }
                }
            if (bestD < float.MaxValue) return best;
        }
        return p;
    }

    /// <summary>Le point est-il pris dans un mur (ou un objet encombrant) ?</summary>
    public static bool InsideObstacle(Vector2 p) => Physics2D.OverlapPoint(p, 1 << 6) != null;

    /// <summary>La case est-elle un sol de caverne (sans roche, pas de l'herbe) ?</summary>
    public bool IsDungeonGround(Vector3Int c)
    {
        if (stoneTilemap.HasTile(c)) return false;
        TileBase t = floorTilemap.GetTile(c);
        return t != null && floorSet.Contains(t);
    }

    public bool IsStone(Vector3Int c) => stoneTilemap != null && stoneTilemap.HasTile(c);

    public bool IsInside(Vector2 worldPos)
    {
        EnsureInit();
        if (!initialized) return false;
        return cells.Contains(floorTilemap.WorldToCell(worldPos));
    }

    public bool IsInsideCell(Vector3Int c)
    {
        EnsureInit();
        return cells.Contains(c);
    }

    public Vector3Int WorldToCell(Vector2 worldPos) => floorTilemap.WorldToCell(worldPos);
    public Vector3 CellCenter(Vector3Int c) => floorTilemap.GetCellCenterWorld(c);

    public bool IsAdjacentToTerritory(Vector3Int c)
    {
        EnsureInit();
        foreach (var d in Dirs)
            if (cells.Contains(c + d)) return true;
        return false;
    }

    /// <summary>Appelé après avoir creusé une case.</summary>
    public void OnCellDug(Vector3Int c)
    {
        EnsureInit();
        if (IsAdjacentToTerritory(c)) cells.Add(c);
        // Creuser peut relier une poche de caverne isolée : on recalcule
        Recompute();
    }

    /// <summary>Une case de roche est-elle creusable (voisine du territoire, dans un étage débloqué) ?</summary>
    public bool CanDigCell(Vector3Int c)
    {
        if (!IsStone(c) || !IsAdjacentToTerritory(c)) return false;
        var layers = DungeonLayers.Instance;
        if (layers == null) return true;
        int i = layers.IndexAtCell(c);
        var l = layers.layers[i];
        // Pas de creusage hors de la zone de l'étage (bordure incluse)
        return l.unlocked && c.x > l.cellBounds.xMin && c.x < l.cellBounds.xMax - 1 && c.y > l.cellBounds.yMin && c.y < l.cellBounds.yMax - 1;
    }

    /// <summary>
    /// Vérifie si remblayer la case couperait le cœur de l'extérieur (entrée) ou d'une partie du donjon.
    /// </summary>
    public bool WouldFillDisconnect(Vector3Int c, Vector3Int entranceCell)
    {
        EnsureInit();
        if (c == coreCell || c == entranceCell) return true;
        var reach = FloodFrom(coreCell, c);
        // Aucune partie du donjon (dont l'entrée) ne doit être coupée du cœur
        return reach.Count < cells.Count - 1;
    }

    /// <summary>Case d'entrée du donjon : la plus proche de la plage (point d'arrivée des aventuriers).</summary>
    public Vector3Int EntranceCell
    {
        get
        {
            Vector2 beach;
            var spawner = FindFirstObjectByType<AdventurerSpawner>();
            if (spawner != null) beach = spawner.spawnPoint != null ? spawner.spawnPoint.position : spawner.transform.position;
            else
            {
                var gate = FindFirstObjectByType<DungeonBoundary>();
                beach = gate != null ? (Vector2)gate.transform.position : (Vector2)CellCenter(CoreCell);
            }
            // Seulement les cases de la surface (étage 0)
            var layers = DungeonLayers.Instance;
            Vector3Int best = CoreCell;
            float bestD = float.MaxValue;
            foreach (var c in cells)
            {
                if (layers != null && layers.IndexAtCell(c) != 0) continue;
                float d = ((Vector2)CellCenter(c) - beach).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }
    }

    public void OnCellFilled(Vector3Int c)
    {
        Recompute();
    }

    /// <summary>Case du territoire la plus proche d'une position (ex : l'entrée vue depuis la plage).</summary>
    public Vector3Int ClosestCellTo(Vector2 worldPos)
    {
        EnsureInit();
        Vector3Int best = coreCell;
        float bestD = float.MaxValue;
        foreach (var c in cells)
        {
            float d = ((Vector2)CellCenter(c) - worldPos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    public Vector3 RandomCellCenter()
    {
        EnsureInit();
        int i = Random.Range(0, cells.Count);
        foreach (var c in cells)
        {
            if (i-- <= 0) return CellCenter(c);
        }
        return CellCenter(coreCell);
    }
}
