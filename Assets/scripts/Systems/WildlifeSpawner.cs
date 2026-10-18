using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Peuple l'île d'animaux sauvages (hors du donjon, sur les cases praticables), en petits groupes :
/// cerfs et sangliers en troupeaux, lièvres et tétras par deux, renards solitaires. Chaque troupeau a son lac.
/// Chaque matin, la faune se renouvelle lentement (naissances près des survivants) ; une espèce chassée
/// jusqu'au dernier ne revient qu'au bout de quelques jours (des bêtes arrivent d'ailleurs).
/// </summary>
public class WildlifeSpawner : MonoBehaviour
{
    public int count = 24;
    [Tooltip("Naissances possibles par jour (toutes espèces)")] public int birthsPerDay = 3;
    [Tooltip("Jours avant le retour d'une espèce disparue")] public int recolonizeDays = 3;

    private Transform parent;
    private Rect area;
    private uint mainArea;
    private readonly Dictionary<WildAnimal.Species, GameObject> prefabs = new Dictionary<WildAnimal.Species, GameObject>();
    private readonly Dictionary<WildAnimal.Species, int> target = new Dictionary<WildAnimal.Species, int>();
    private readonly Dictionary<WildAnimal.Species, int> extinctSince = new Dictionary<WildAnimal.Species, int>();

    private void Start()
    {
        var cat = DungeonCatalog.Get();
        if (cat == null || cat.wildlifePrefabs == null || cat.wildlifePrefabs.Count == 0) return;
        var layers = DungeonLayers.Instance;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (layers == null || graph == null) return;
        FreshWater.Reset();

        area = layers.WorldRect(0);
        // L'île principale = zone A* du débarcadère (pas d'animal isolé sur un îlot)
        var sp = FindFirstObjectByType<AdventurerSpawner>();
        if (sp != null)
        {
            var mn = graph.GetNearest(sp.transform.position, Pathfinding.NNConstraint.Default).node;
            if (mn != null) mainArea = mn.Area;
        }
        foreach (var p in cat.wildlifePrefabs)
            if (p != null) prefabs[WildAnimal.SpeciesOf(p.name)] = p;
        parent = new GameObject("Faune").transform;

        // répartition : des troupeaux plutôt que des bêtes isolées
        var plan = new List<(WildAnimal.Species sp, int min, int max)>
        {
            (WildAnimal.Species.Deer, 3, 5), (WildAnimal.Species.Deer, 3, 4), (WildAnimal.Species.Boar, 2, 3),
            (WildAnimal.Species.Boar, 2, 3), (WildAnimal.Species.Hare, 2, 2), (WildAnimal.Species.Hare, 1, 2),
            (WildAnimal.Species.Hare, 2, 2), (WildAnimal.Species.Grouse, 2, 4), (WildAnimal.Species.Fox, 1, 1),
            (WildAnimal.Species.Fox, 1, 1),
        };
        int spawned = 0;
        foreach (var (s, min, max) in plan)
        {
            if (spawned >= count || !prefabs.ContainsKey(s)) continue;
            if (!RandomSpot(out var at)) continue;
            int n = Random.Range(min, max + 1);
            var herd = new WildAnimal.Herd { center = at, lake = FreshWater.Nearest(at) };
            for (int i = 0; i < n; i++) if (Spawn(s, at + Random.insideUnitCircle * 1.2f, herd)) spawned++;
            target[s] = (target.TryGetValue(s, out int t) ? t : 0) + n;
        }
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private bool RandomSpot(out Vector2 pos)
    {
        pos = Vector2.zero;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph == null) return false;
        for (int tries = 0; tries < 60; tries++)
        {
            Vector2 p = new Vector2(Random.Range(area.xMin + 10, area.xMax - 10), Random.Range(area.yMin + 10, area.yMax - 10));
            var nn = graph.GetNearest(p, Pathfinding.NNConstraint.Default);
            if (nn.node == null || !nn.node.Walkable) continue;
            Vector2 q = (Vector3)nn.node.position;
            if (Vector2.Distance(q, p) > 1f) continue;
            if (mainArea != 0 && nn.node.Area != mainArea) continue;
            if (!WildAnimal.IsDrySpot(q)) continue;
            if (DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(q)) continue;
            pos = q;
            return true;
        }
        return false;
    }

    private bool Spawn(WildAnimal.Species s, Vector2 at, WildAnimal.Herd herd)
    {
        if (!prefabs.TryGetValue(s, out var prefab)) return false;
        var go = Instantiate(prefab, at, Quaternion.identity, parent);
        var a = go.GetComponent<WildAnimal>();
        if (a != null)
        {
            a.herd = herd;
            if (herd != null) herd.members++;
        }
        return true;
    }

    private void OnNewDay(int day)
    {
        var alive = new Dictionary<WildAnimal.Species, List<WildAnimal>>();
        foreach (var a in WildAnimal.All)
        {
            if (a == null || a.IsDead) continue;
            if (!alive.TryGetValue(a.species, out var l)) alive[a.species] = l = new List<WildAnimal>();
            l.Add(a);
        }
        int births = 0;
        foreach (var kv in target)
        {
            if (births >= birthsPerDay) break;
            alive.TryGetValue(kv.Key, out var list);
            int n = list != null ? list.Count : 0;
            if (n >= kv.Value) { extinctSince.Remove(kv.Key); continue; }
            if (n == 0)
            {
                // disparue : quelques bêtes reviennent d'ailleurs au bout de quelques jours
                if (!extinctSince.ContainsKey(kv.Key)) { extinctSince[kv.Key] = day; continue; }
                if (day - extinctSince[kv.Key] < recolonizeDays || !RandomSpot(out var at)) continue;
                var herd = new WildAnimal.Herd { center = at, lake = FreshWater.Nearest(at) };
                Spawn(kv.Key, at, herd); Spawn(kv.Key, at + Vector2.right * 0.6f, herd);
                extinctSince.Remove(kv.Key);
                births += 2;
                continue;
            }
            // naissance près d'un adulte, dans son troupeau (au printemps et en été seulement)
            if (!Seasons.Breeding || Random.value > 0.6f) continue;
            var parentAnimal = list[Random.Range(0, list.Count)];
            Spawn(kv.Key, (Vector2)parentAnimal.transform.position + Random.insideUnitCircle * 0.6f, parentAnimal.herd);
            births++;
        }
    }
}
