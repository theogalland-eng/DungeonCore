using System.Collections.Generic;
using UnityEngine;

/// <summary>Cellule du donjon : peut enfermer un intrus vaincu (voir PrisonSystem).</summary>
public class PrisonCell : MonoBehaviour
{
    public static readonly List<PrisonCell> All = new List<PrisonCell>();

    public AdventurerAI occupant;
    public bool Free => occupant == null;
    public Vector3 Slot => transform.position + new Vector3(0f, 0.05f, 0f);

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void OnDestroy()
    {
        // cellule détruite : le prisonnier s'enfuit dans la nature (on le libère)
        if (occupant != null && PrisonSystem.Instance != null) PrisonSystem.Instance.Release(occupant, true);
    }
}
