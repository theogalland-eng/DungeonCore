using UnityEngine;

/// <summary>
/// Invocation de monstres par le Noyau (outil "Gobelin" de la barre d'outils).
/// Chaque invocation se rattache à un point d'ancrage proche, ou en crée un nouveau.
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }

    [Header("References")]
    public DungeonBuilder dungeonBuilder;
    public Transform monstersParent;
    public Transform anchorsParent;

    [Header("Goblin Spawning")]
    public GameObject gobelinPrefab;
    public Transform dungeonCore;

    [Tooltip("Coût d'un gobelin")]
    public int cost1Goblin = 50;
    [HideInInspector] public int cost2Goblins = 90;
    [HideInInspector] public int cost3Goblins = 120;

    [Header("Spawn Settings")]
    public float spawnRadius = 0.4f;
    [Tooltip("Distance pour rejoindre une ancre existante au lieu d'en créer une")]
    public float joinAnchorDistance = 2f;

    private int summonCounter;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (monstersParent == null) monstersParent = new GameObject("Monsters").transform;
        if (anchorsParent == null)
        {
            var existing = GameObject.Find("PatrolAnchors");
            anchorsParent = existing != null ? existing.transform : new GameObject("PatrolAnchors").transform;
        }
        if (dungeonBuilder == null) dungeonBuilder = FindFirstObjectByType<DungeonBuilder>();
        if (dungeonCore == null)
        {
            var core = FindFirstObjectByType<DungeonCore>();
            if (core != null) dungeonCore = core.transform;
        }
    }

    /// <summary>Peut-on invoquer ici ? (raison dans 'reason' sinon)</summary>
    public bool CanSummonAt(Vector2 worldPos, out string reason) => CanSummonAt(worldPos, cost1Goblin, out reason);

    public bool CanSummonAt(Vector2 worldPos, int cost, out string reason)
    {
        reason = null;
        if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInside(worldPos))
        {
            reason = "Les monstres naissent dans le donjon";
            return false;
        }
        if (PlayerLevel.Instance != null && !PlayerLevel.Instance.CanSpawnMonster())
        {
            reason = $"Limite de monstres atteinte ({PlayerLevel.Instance.GetMaxMonsters()}) — montez de niveau";
            return false;
        }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(cost))
        {
            reason = $"Pas assez de mana ({cost} requis)";
            return false;
        }
        return true;
    }

    /// <summary>Recrée un monstre sans coût ni message (chargement d'une sauvegarde).</summary>
    public GobelinAI SpawnFree(GameObject prefab, Vector2 pos, Vector2 anchorPos, string displayName)
    {
        if (prefab == null) return null;
        if (monstersParent == null) Start();
        PatrolAnchor anchor = null;
        foreach (var a in FindObjectsByType<PatrolAnchor>(FindObjectsSortMode.None))
            if (Vector2.Distance(a.transform.position, anchorPos) < 0.05f) { anchor = a; break; }
        if (anchor == null)
        {
            var anchorObj = new GameObject($"Anchor_{System.Guid.NewGuid().ToString().Substring(0, 6)}");
            anchorObj.transform.position = anchorPos;
            anchorObj.transform.SetParent(anchorsParent);
            anchor = anchorObj.AddComponent<PatrolAnchor>();
        }
        if (DungeonTerritory.InsideObstacle(pos)) pos = DungeonTerritory.FreeSpotNear(pos, true);
        var go = Instantiate(prefab, pos, Quaternion.identity, monstersParent);
        go.name = displayName;
        var ai = go.GetComponent<GobelinAI>();
        if (ai != null)
        {
            ai.dungeonCore = dungeonCore;
            ai.gobelinName = displayName;
            ai.anchorPoint = anchor.transform;
            anchor.RegisterGoblin(ai);
        }
        return ai;
    }

    public GobelinAI TrySummonAt(Vector2 worldPos) => TrySummonAt(worldPos, gobelinPrefab, cost1Goblin, "Gobelin");

    public GobelinAI TrySummonAt(Vector2 worldPos, GameObject prefab, int cost, string displayName)
    {
        if (prefab == null) { GameHUD.Toast("Monstre indisponible", GameHUD.WarningColor); return null; }
        if (!CanSummonAt(worldPos, cost, out string reason))
        {
            GameHUD.Toast(reason, GameHUD.WarningColor);
            return null;
        }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(cost)) return null;

        // Ancre proche ou nouvelle ancre
        PatrolAnchor anchor = null;
        float best = joinAnchorDistance;
        foreach (var a in FindObjectsByType<PatrolAnchor>(FindObjectsSortMode.None))
        {
            float d = Vector2.Distance(a.transform.position, worldPos);
            if (d < best) { best = d; anchor = a; }
        }
        if (anchor == null)
        {
            var anchorObj = new GameObject($"Anchor_{System.Guid.NewGuid().ToString().Substring(0, 6)}");
            anchorObj.transform.position = worldPos;
            anchorObj.transform.SetParent(anchorsParent);
            anchor = anchorObj.AddComponent<PatrolAnchor>();
        }

        // léger décalage aléatoire... jamais dans un mur ni dans un objet encombrant
        Vector2 spawnPos = worldPos + Random.insideUnitCircle * spawnRadius;
        if (Physics2D.OverlapCircle(spawnPos, 0.25f, 1 << 6) != null) spawnPos = worldPos;
        if (DungeonTerritory.InsideObstacle(spawnPos)) spawnPos = DungeonTerritory.FreeSpotNear(worldPos, true);
        GameObject goblin = Instantiate(prefab, spawnPos, Quaternion.identity, monstersParent);
        summonCounter++;
        goblin.name = $"{displayName} {summonCounter}";

        GobelinAI ai = goblin.GetComponent<GobelinAI>();
        if (ai != null)
        {
            ai.dungeonCore = dungeonCore;
            ai.gobelinName = goblin.name;
            ai.anchorPoint = anchor.transform;
            anchor.RegisterGoblin(ai);
        }

        FloatingText.Spawn((Vector3)worldPos + Vector3.up * 0.5f, $"-{cost}", GameHUD.ManaColor, 1f);
        // naissance dans un tourbillon de fumée et de mana
        Sfx.Play("summon", worldPos, 0.8f);
        Fx.Play("smoke_circle", (Vector3)worldPos, 1.4f, 20f, 16, new Color(0.75f, 0.65f, 1f, 0.85f));
        Fx.Play("mana", (Vector3)worldPos + Vector3.down * 0.1f, 1.2f, 18f, 2, null, false, null, new Color(0.6f, 0.5f, 1f));
        StoryManager.Instance?.OnMonsterSummoned();
        return ai;
    }
}
