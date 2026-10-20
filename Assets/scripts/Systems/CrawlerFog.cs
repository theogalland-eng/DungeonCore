using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// BROUILLARD DE GUERRE du mode Aventurier : sous la montagne, on ne voit que la salle où se tient le héros
/// (murs et seuils compris). Les salles déjà visitées restent assombries, le reste est noir.
/// Dans un couloir, on voit quelques cases autour de soi. Un tilemap noir non éclairé, trié au-dessus du décor,
/// recouvre la zone de l'étage ; seules les cases qui changent d'état sont recolorées.
/// </summary>
public class CrawlerFog : MonoBehaviour
{
    public static CrawlerFog Instance { get; private set; }

    private const int SortingOrder = 62;            // au-dessus des personnages, murs et barres de vie ; sous les textes flottants
    private const float SeenAlpha = 0.7f;           // salle visitée : assombrie

    private Tilemap fog;
    private Tile tile;
    private CrawlerDungeon.Floor floor;
    private RectInt bounds;
    private readonly Dictionary<Vector3Int, byte> state = new Dictionary<Vector3Int, byte>();   // 0 inconnue, 1 vue, 2 visible
    private readonly HashSet<Vector3Int> visible = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> next = new HashSet<Vector3Int>();
    private Vector3Int lastCell = new Vector3Int(int.MinValue, 0, 0);
    private int lastRoom = -2;

    /// <summary>Salle où se tient le héros (null : couloir ou dehors).</summary>
    public RectInt? CurrentRoom { get; private set; }

    public static CrawlerFog Ensure()
    {
        if (Instance != null) return Instance;
        var b = DungeonBuilder.Instance;
        if (b == null) return null;
        var go = new GameObject("Brouillard de guerre");
        go.transform.SetParent(b.floorTilemap.transform.parent, false);
        go.transform.localPosition = b.floorTilemap.transform.localPosition;
        var f = go.AddComponent<CrawlerFog>();
        f.fog = go.AddComponent<Tilemap>();
        f.fog.tileAnchor = b.floorTilemap.tileAnchor;
        var r = go.AddComponent<TilemapRenderer>();
        r.sortingOrder = SortingOrder;
        var sh = Shader.Find("Sprites/Default");
        if (sh != null) r.sharedMaterial = new Material(sh);      // non éclairé : un noir franc
        f.tile = ScriptableObject.CreateInstance<Tile>();
        f.tile.sprite = SpriteUtil.Square;
        f.tile.flags = TileFlags.None;
        f.tile.colliderType = Tile.ColliderType.None;
        return f;
    }

    private void OnEnable() => Instance = this;
    private void OnDisable() { if (Instance == this) Instance = null; }

    /// <summary>Nouvel étage : tout redevient inconnu.</summary>
    public void SetFloor(CrawlerDungeon.Floor f, RectInt area)
    {
        floor = f;
        bounds = area;
        state.Clear();
        visible.Clear();
        lastCell = new Vector3Int(int.MinValue, 0, 0);
        lastRoom = -2;
        fog.ClearAllTiles();
        var block = new TileBase[area.width * area.height];
        for (int i = 0; i < block.Length; i++) block[i] = tile;
        fog.SetTilesBlock(new BoundsInt(area.x, area.y, 0, area.width, area.height, 1), block);
        foreach (var p in area.allPositionsWithin) fog.SetColor(new Vector3Int(p.x, p.y, 0), Color.black);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        floor = null;
        CurrentRoom = null;
        if (fog != null) fog.ClearAllTiles();
        gameObject.SetActive(false);
    }

    /// <summary>Appelé à chaque image avec la position du héros.</summary>
    public void Tick(Vector2 heroPos)
    {
        if (floor == null) return;
        var b = DungeonBuilder.Instance;
        var cell = b.floorTilemap.WorldToCell(heroPos);
        if (cell == lastCell) return;
        lastCell = cell;

        int roomIndex = -1;
        for (int i = 0; i < floor.rooms.Count; i++)
        {
            var rr = floor.rooms[i].rect;
            if (cell.x >= rr.xMin && cell.x < rr.xMax && cell.y >= rr.yMin && cell.y < rr.yMax) { roomIndex = i; break; }
        }
        // dans une salle, rien ne change tant qu'on y reste
        if (roomIndex >= 0 && roomIndex == lastRoom) return;
        lastRoom = roomIndex;
        CurrentRoom = roomIndex >= 0 ? floor.rooms[roomIndex].rect : (RectInt?)null;

        next.Clear();
        if (roomIndex >= 0)
        {
            var r = floor.rooms[roomIndex].rect;
            for (int x = r.xMin - 1; x <= r.xMax; x++)
                for (int y = r.yMin - 1; y <= r.yMax; y++) next.Add(new Vector3Int(x, y, 0));
            // les seuils : un bout de couloir à chaque sortie
            foreach (var c in new List<Vector3Int>(next)) if (Open(b, c)) Flood(b, c, 2, false);
        }
        else Flood(b, cell, 4, true);

        // ce qui n'est plus visible devient « vu » ; le nouveau visible est dévoilé
        foreach (var c in visible)
            if (!next.Contains(c)) { state[c] = 1; fog.SetColor(c, new Color(0f, 0f, 0f, SeenAlpha)); }
        foreach (var c in next)
        {
            state.TryGetValue(c, out byte s);
            if (s != 2) { state[c] = 2; fog.SetColor(c, Color.clear); }
        }
        visible.Clear();
        foreach (var c in next) visible.Add(c);
    }

    private bool Open(DungeonBuilder b, Vector3Int c) => !b.stoneTilemap.HasTile(c) && bounds.Contains(new Vector2Int(c.x, c.y));

    /// <summary>Cases ouvertes à moins de « steps » pas, et leurs murs voisins.</summary>
    private void Flood(DungeonBuilder b, Vector3Int from, int steps, bool includeRooms)
    {
        var q = new Queue<(Vector3Int c, int d)>();
        var seen = new HashSet<Vector3Int> { from };
        q.Enqueue((from, 0));
        while (q.Count > 0)
        {
            var (c, d) = q.Dequeue();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++) next.Add(c + new Vector3Int(dx, dy, 0));
            if (d >= steps) continue;
            foreach (var dir in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
            {
                var n = c + dir;
                if (!seen.Add(n) || !Open(b, n)) continue;
                if (!includeRooms && InRoom(n)) continue;          // depuis une salle : on ne dévoile pas la salle voisine
                q.Enqueue((n, d + 1));
            }
        }
    }

    private bool InRoom(Vector3Int c)
    {
        foreach (var r in floor.rooms)
            if (c.x >= r.rect.xMin && c.x < r.rect.xMax && c.y >= r.rect.yMin && c.y < r.rect.yMax) return true;
        return false;
    }
}
