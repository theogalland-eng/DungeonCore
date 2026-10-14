using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Escalier reliant deux étages. Les chemins passent par un lien de pathfinding (NodeLink2) ;
/// les unités sont téléportées d'un escalier à l'autre.
/// </summary>
public class DungeonStairs : MonoBehaviour
{
    public static readonly List<DungeonStairs> All = new List<DungeonStairs>();

    public DungeonStairs partner;
    public bool goesDown;
    public int layerIndex;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public Vector3Int Cell => DungeonTerritory.Instance != null
        ? DungeonTerritory.Instance.WorldToCell(transform.position)
        : Vector3Int.zero;

    /// <summary>Position d'arrivée à côté de l'escalier partenaire.</summary>
    public Vector2 ExitPosition => partner != null ? (Vector2)partner.transform.position + Vector2.down * 0.9f : (Vector2)transform.position;

    public static DungeonStairs At(Vector2 worldPos, float radius = 0.5f)
    {
        foreach (var s in All)
            if (s != null && s.partner != null && Vector2.Distance(s.transform.position, worldPos) < radius) return s;
        return null;
    }
}
