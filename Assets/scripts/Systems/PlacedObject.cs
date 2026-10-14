using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tout objet posé par le joueur (piège, décor). Occupe une case.
/// </summary>
public class PlacedObject : MonoBehaviour
{
    public static readonly List<PlacedObject> All = new List<PlacedObject>();

    public ItemKind kind = ItemKind.Decor;
    public string itemId;
    public int cost;
    public float prestige;
    [Tooltip("Mana prélevé chaque matin pour faire fonctionner ce piège")]
    public int upkeepPerDay;
    [Tooltip("Donnée libre (trophée : identifiant de l'objet exposé)")]
    public string data;

    /// <summary>Entretien impayé : le piège est à l'arrêt jusqu'au lendemain.</summary>
    public bool Starved { get; private set; }
    /// <summary>Dégâts infligés par ce piège (rumeurs de la taverne).</summary>
    [System.NonSerialized] public float damageDealt;
    /// <summary>Désamorcé par un intrus qui le connaît (Némésis) : inactif quelques instants.</summary>
    public bool Disarmed { get; private set; }

    public void SetStarved(bool starved)
    {
        Starved = starved;
        ApplyActive();
    }

    public void Disarm(float seconds)
    {
        Disarmed = true;
        ApplyActive();
        CancelInvoke(nameof(Rearm));
        Invoke(nameof(Rearm), seconds);
    }

    private void Rearm()
    {
        Disarmed = false;
        ApplyActive();
    }

    private void ApplyActive()
    {
        bool off = Starved || Disarmed;
        foreach (var mb in GetComponents<MonoBehaviour>())
            if (mb != this && !(mb is DungeonDoor)) mb.enabled = !off;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.color = off ? new Color(0.45f, 0.45f, 0.5f, sr.color.a) : new Color(1f, 1f, 1f, sr.color.a);
    }

    public static int TotalUpkeep()
    {
        int t = 0;
        foreach (var p in All) if (p != null) t += p.upkeepPerDay;
        return t;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    // ─────────────────────────────────────────────
    // OBJETS ENCOMBRANTS : monstres et aventuriers ne passent pas au travers
    // (torches, ossements, bannières, cristaux... restent franchissables)
    // ─────────────────────────────────────────────

    public static readonly HashSet<string> BlockingIds = new HashSet<string>
    {
        "chest", "chest_royal", "barrel", "supplies", "rack", "statue_knight", "gargoyle", "sarcophagus", "sarcophagus_open",
        "coffin", "cell", "fountain", "statue_fire", "skull", "demon_altar", "stalagmites", "stalagmites_gray", "mushrooms_long",
    };
    public static bool IsBlocking(string id) => id != null && BlockingIds.Contains(id);

    /// <summary>Cette case est-elle rendue infranchissable par l'objet ?</summary>
    public bool Blocking { get; private set; }

    /// <summary>Cases déjà encombrées.</summary>
    public static HashSet<Vector3Int> BlockedCells()
    {
        var set = new HashSet<Vector3Int>();
        var b = DungeonBuilder.Instance;
        if (b == null) return set;
        foreach (var p in All) if (p != null && p.Blocking) set.Add(b.floorTilemap.WorldToCell(p.transform.position));
        return set;
    }

    private void Start()
    {
        if (!IsBlocking(itemId) || Blocking) return;
        var b = DungeonBuilder.Instance;
        var t = DungeonTerritory.Instance;
        if (b == null) return;
        var cell = b.floorTilemap.WorldToCell(transform.position);
        // sécurité (sauvegardes anciennes) : un objet qui couperait le donjon reste franchissable
        if (t != null && t.IsInsideCell(cell))
        {
            var blocked = BlockedCells();
            blocked.Add(cell);
            if (!t.StaysConnected(blocked)) return;
        }
        Blocking = true;
        var go = new GameObject("Obstacle");
        go.transform.SetParent(transform, false);
        go.layer = 6;
        go.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
        AstarPath.active?.UpdateGraphs(new Bounds(transform.position, new Vector3(2.5f, 2.5f, 1f)), 0.05f);
    }

    private void OnDestroy()
    {
        if (Blocking && AstarPath.active != null)
            AstarPath.active.UpdateGraphs(new Bounds(transform.position, new Vector3(2.5f, 2.5f, 1f)), 0.1f);
    }

    public static int Count(ItemKind k)
    {
        int n = 0;
        foreach (var p in All) if (p != null && p.kind == k) n++;
        return n;
    }

    public static float TotalPrestige()
    {
        float t = 0f;
        foreach (var p in All) if (p != null) t += p.prestige * ThemedRooms.PrestigeMult(p);
        return t;
    }
}
