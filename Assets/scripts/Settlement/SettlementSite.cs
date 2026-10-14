using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SettlementPlot
{
    public string buildingId;
    /// <summary>Emprise (cases).</summary>
    public RectInt rect;
}

/// <summary>
/// Données de la colonie calculées par le générateur d'île : point d'arrivée, ponton,
/// emplacements des bâtiments (des arbres peuvent s'y trouver : les colons les abattront).
/// </summary>
public class SettlementSite : MonoBehaviour
{
    [Tooltip("Case de terre ferme au pied du ponton")]
    public Vector3Int landing;
    [Tooltip("Cases du ponton initial (de la plage vers le large)")]
    public List<Vector3Int> dockCells = new List<Vector3Int>();
    [Tooltip("Cases ajoutées par le ponton du port (stade 2)")]
    public List<Vector3Int> pierExtension = new List<Vector3Int>();
    [Tooltip("Emplacements des barques amarrées (monde)")]
    public List<Vector3> moorings = new List<Vector3>();
    [Tooltip("Où accostent les navires des expéditions (monde)")]
    public Vector3 shipMooring;
    public List<SettlementPlot> plots = new List<SettlementPlot>();

    public Vector3 CellCenter(Vector3Int c) => new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);
    public Vector3 LandingWorld => CellCenter(landing);
}
