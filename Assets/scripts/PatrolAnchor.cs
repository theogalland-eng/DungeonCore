using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Point d'ancrage de patrouille : les gobelins rattachés patrouillent autour.
/// Visible en jeu (losange) pour pouvoir le sélectionner et le déplacer.
/// </summary>
public class PatrolAnchor : MonoBehaviour
{
    [Header("Visual")]
    public Color anchorColor = new Color(0.3f, 0.9f, 1f, 0.8f);
    public float anchorSize = 0.5f;
    public bool showRadius = true;
    public float displayRadius = 3f;

    private readonly List<GobelinAI> attachedGoblins = new List<GobelinAI>();
    private SpriteRenderer marker;

    private void Awake()
    {
        var go = new GameObject("Marker");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * anchorSize;
        marker = go.AddComponent<SpriteRenderer>();
        marker.sprite = SpriteUtil.Diamond;
        marker.color = anchorColor;
        marker.sortingOrder = 20;
    }

    private void Update()
    {
        // Le marqueur est discret sauf en mode sélection
        bool selecting = DungeonBuilder.Instance == null || DungeonBuilder.Instance.GetCurrentMode() == DungeonBuilder.BuildMode.None;
        Color c = anchorColor;
        c.a = selecting ? anchorColor.a : 0.15f;
        if (marker != null) { marker.color = c; marker.enabled = !CrawlerMode.Active; }   // l'aventurier ne voit pas les ancres du Noyau
    }

    public void RegisterGoblin(GobelinAI goblin)
    {
        if (!attachedGoblins.Contains(goblin)) attachedGoblins.Add(goblin);
    }

    public void UnregisterGoblin(GobelinAI goblin)
    {
        attachedGoblins.Remove(goblin);
        attachedGoblins.RemoveAll(g => g == null);
        if (attachedGoblins.Count == 0) Destroy(gameObject);
    }

    public List<GobelinAI> GetAttachedGoblins()
    {
        attachedGoblins.RemoveAll(g => g == null);
        return attachedGoblins;
    }

    public int GetGoblinCount()
    {
        attachedGoblins.RemoveAll(g => g == null);
        return attachedGoblins.Count;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = anchorColor;
        Gizmos.DrawWireCube(transform.position, Vector3.one * anchorSize);
        if (showRadius)
        {
            Gizmos.color = new Color(anchorColor.r, anchorColor.g, anchorColor.b, 0.2f);
            Gizmos.DrawWireSphere(transform.position, displayRadius);
        }
    }

    private void OnDrawGizmosSelected()
    {
        attachedGoblins.RemoveAll(g => g == null);
        Gizmos.color = Color.cyan;
        foreach (var goblin in attachedGoblins)
            Gizmos.DrawLine(transform.position, goblin.transform.position);
    }
}
