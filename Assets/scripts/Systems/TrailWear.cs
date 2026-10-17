using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Sentiers qui naissent du passage : au départ, aucun chemin ne mène à la grotte. Chaque fois qu'un aventurier
/// ou un colon entre dans une case de terre ferme, elle s'use : herbe foulée, puis sentier de terre.
/// Quand la colonie devient un hameau, ses ouvriers pavent chaque jour une partie des sentiers (en partant du ponton).
/// Les tuiles sont auto-raccordées (BlobTile) : le sentier se dessine de lui-même, avec des bords organiques.
/// </summary>
public class TrailWear : MonoBehaviour
{
    public static TrailWear Instance { get; private set; }

    public BlobTile trampled, dirt, paved;
    [Tooltip("Passages pour que l'herbe soit foulée")] public int trampleAt = 4;
    [Tooltip("Passages pour un vrai sentier de terre")] public int dirtAt = 14;
    [Tooltip("Stade de la colonie à partir duquel les sentiers sont pavés (3 = Hameau)")] public int paveFromStage = 3;
    public int pavedPerDay = 28;

    private Tilemap trail, roads, sol, decor, grass;
    private readonly Dictionary<Vector3Int, int> wear = new Dictionary<Vector3Int, int>();
    private readonly HashSet<Vector3Int> pavedCells = new HashSet<Vector3Int>();
    private readonly Dictionary<int, Vector3Int> lastCell = new Dictionary<int, Vector3Int>();
    private readonly List<Transform> walkers = new List<Transform>();
    private float nextSample, nextScan;

    private void Awake() { Instance = this; }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private void Start()
    {
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name == "Sentier") trail = tm;
            else if (tm.name == "Routes") roads = tm;
            else if (tm.name == "Sol") sol = tm;
            else if (tm.name == "Decor") decor = tm;
            else if (tm.name == "T_Herbe") grass = tm;
        }
        if (trail == null || sol == null || trampled == null) { enabled = false; return; }
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    /// <summary>Terre ferme de la surface (hors grotte, eau, ponton) : marquée « ground_* » sur le tilemap Sol.</summary>
    private bool IsGround(Vector3Int c)
    {
        var t = sol.GetTile(c);
        return t != null && t.name.StartsWith("ground_");
    }

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 2f;
            walkers.Clear();
            foreach (var a in FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None)) walkers.Add(a.transform);
            foreach (var s in FindObjectsByType<SettlerAI>(FindObjectsSortMode.None)) walkers.Add(s.transform);
        }
        if (Time.time < nextSample) return;
        nextSample = Time.time + 0.25f;
        foreach (var w in walkers)
        {
            if (w == null) continue;
            var c = sol.WorldToCell(w.position);
            int id = w.GetInstanceID();
            if (lastCell.TryGetValue(id, out var prev) && prev == c) continue;   // on compte les passages, pas l'attente
            lastCell[id] = c;
            if (!IsGround(c)) continue;
            Step(c, 1);
        }
    }

    private void Step(Vector3Int c, int amount)
    {
        wear.TryGetValue(c, out int n);
        int before = n;
        n += amount;
        wear[c] = n;
        if (before < trampleAt && n >= trampleAt) Apply(c);
        else if (before < dirtAt && n >= dirtAt) Apply(c);
    }

    private void Apply(Vector3Int c)
    {
        int n = wear.TryGetValue(c, out int v) ? v : 0;
        if (n >= dirtAt)
        {
            trail.SetTile(c, dirt);
            // l'herbe haute et les fleurs disparaissent sous les pas
            var d = decor != null ? decor.GetTile(c) : null;
            if (d != null && (d.name.Contains("tuft") || d.name.Contains("flowers") || d.name.Contains("fern"))) decor.SetTile(c, null);
        }
        // l'herbe se couche ; sur la roche ou le sable, seule la terre battue finit par se voir
        else if (n >= trampleAt && (grass == null || grass.HasTile(c))) trail.SetTile(c, trampled);
        if (pavedCells.Contains(c) && roads != null && paved != null) roads.SetTile(c, paved);
    }

    private void OnNewDay(int day)
    {
        var sm = SettlementManager.Instance;
        if (sm == null || sm.Site == null || sm.stage < paveFromStage || roads == null || paved == null) return;
        // les ouvriers pavent d'abord ce qui est près du ponton
        var todo = new List<Vector3Int>();
        foreach (var kv in wear) if (kv.Value >= dirtAt && !pavedCells.Contains(kv.Key)) todo.Add(kv.Key);
        var land = sm.Site.landing;
        todo.Sort((a, b) => ((a - land).sqrMagnitude).CompareTo((b - land).sqrMagnitude));
        int k = 0;
        foreach (var c in todo)
        {
            if (k++ >= pavedPerDay) break;
            pavedCells.Add(c);
            roads.SetTile(c, paved);
        }
    }

    public int WornCells => wear.Count;

    // ── Sauvegarde : x, y, usure, pavé ──
    public void Capture(List<int> into)
    {
        into.Clear();
        foreach (var kv in wear)
        {
            if (kv.Value < trampleAt) continue;
            into.Add(kv.Key.x); into.Add(kv.Key.y); into.Add(kv.Value); into.Add(pavedCells.Contains(kv.Key) ? 1 : 0);
        }
    }

    public void Restore(List<int> from)
    {
        Clear();
        if (from == null) return;
        for (int i = 0; i + 3 < from.Count; i += 4)
        {
            var c = new Vector3Int(from[i], from[i + 1], 0);
            wear[c] = from[i + 2];
            if (from[i + 3] == 1) pavedCells.Add(c);
            if (trail != null) Apply(c);
        }
    }

    public void Clear()
    {
        wear.Clear();
        pavedCells.Clear();
        if (trail != null) trail.ClearAllTiles();
        if (roads != null) roads.ClearAllTiles();
    }
}
