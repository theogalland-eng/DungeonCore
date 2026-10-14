using UnityEngine;

/// <summary>
/// Peuple l'île d'animaux sauvages au lancement (hors du donjon, sur les cases praticables).
/// </summary>
public class WildlifeSpawner : MonoBehaviour
{
    public int count = 24;

    private void Start()
    {
        var cat = DungeonCatalog.Get();
        if (cat == null || cat.wildlifePrefabs == null || cat.wildlifePrefabs.Count == 0) return;
        var layers = DungeonLayers.Instance;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (layers == null || graph == null) return;

        Rect area = layers.WorldRect(0);
        // L'île principale = zone A* du débarcadère (pas d'animal isolé sur un îlot)
        uint mainArea = 0;
        var sp = FindFirstObjectByType<AdventurerSpawner>();
        if (sp != null)
        {
            var mn = graph.GetNearest(sp.transform.position, Pathfinding.NNConstraint.Default).node;
            if (mn != null) mainArea = mn.Area;
        }
        var parent = new GameObject("Faune").transform;
        int spawned = 0;
        for (int tries = 0; tries < count * 30 && spawned < count; tries++)
        {
            Vector2 p = new Vector2(Random.Range(area.xMin + 10, area.xMax - 10), Random.Range(area.yMin + 10, area.yMax - 10));
            var nn = graph.GetNearest(p, Pathfinding.NNConstraint.Default);
            if (nn.node == null || !nn.node.Walkable) continue;
            Vector2 pos = (Vector3)nn.node.position;
            if (Vector2.Distance(pos, p) > 1f) continue;
            if (mainArea != 0 && nn.node.Area != mainArea) continue;
            if (!WildAnimal.IsDrySpot(pos)) continue;
            if (DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(pos)) continue;
            var prefab = cat.wildlifePrefabs[Random.Range(0, cat.wildlifePrefabs.Count)];
            if (prefab == null) continue;
            Instantiate(prefab, pos, Quaternion.identity, parent);
            spawned++;
        }
    }
}
