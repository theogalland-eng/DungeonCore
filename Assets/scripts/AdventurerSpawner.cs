using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Fait apparaître les aventuriers sur la plage (point de spawn).
/// Les expéditions "normales" sont pilotées par l'ExpeditionManager ;
/// l'auto-spawn et les touches debug restent disponibles pour les tests.
/// </summary>
public class AdventurerSpawner : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("Préfab aventurier")]
    public GameObject adventurerPrefab;

    [Tooltip("Point de spawn (la plage). Vide = position de cet objet")]
    public Transform spawnPoint;

    [Tooltip("Cible (cœur du donjon)")]
    public Transform dungeonCore;

    [Header("Group Spawning")]
    [Range(0f, 1f)]
    public float groupSpawnChance = 0.3f;
    public int minGroupSize = 2;
    public int maxGroupSize = 4;
    [Tooltip("Rayon de spawn pour groupes")]
    public float groupSpawnRadius = 2f;

    [Header("Personality Ranges")]
    public Vector2 arroganceRange = new Vector2(0.2f, 0.8f);
    public Vector2 sociabilityRange = new Vector2(0.3f, 0.9f);

    [Header("Auto Spawn (désactivé par l'ExpeditionManager)")]
    public bool autoSpawn = false;
    public float spawnInterval = 30f;
    private float spawnTimer = 0f;

    [Header("Debug")]
    [Tooltip("Y = spawner un groupe d'aventuriers (éditeur / tests)")]
    public bool enableDebugSpawn = true;

    private static readonly string[] HealerNames =
        { "Élise", "Maëlle", "Solène", "Iris", "Ophélie", "Aude", "Clémence", "Lise", "Anaïs", "Bérénice" };

    private static readonly string[] FirstNames =
    {
        "Aldric", "Bran", "Cedric", "Doran", "Elric", "Faris", "Gareth", "Hugo", "Ivan", "Joren",
        "Kael", "Lucan", "Marek", "Noa", "Oswin", "Perrin", "Quentin", "Roland", "Soren", "Tobias",
        "Ulric", "Victor", "Wendel", "Yann", "Zora", "Ada", "Brune", "Cyra", "Elise", "Maëlle"
    };

    public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

    private void Start()
    {
        if (spawnPoint == null) spawnPoint = transform;
        if (dungeonCore == null)
        {
            var core = FindFirstObjectByType<DungeonCore>();
            if (core != null) dungeonCore = core.transform;
        }
        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (enableDebugSpawn && kb != null && kb.yKey.wasPressedThisFrame)
            SpawnGroup();

        if (autoSpawn)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                if (Random.value < groupSpawnChance) SpawnGroup();
                else SpawnAdventurer();
                spawnTimer = spawnInterval;
            }
        }
    }

    /// <summary>Spawner un aventurier individuel (stats du prefab).</summary>
    public AdventurerAI SpawnAdventurer()
    {
        if (adventurerPrefab == null)
        {
            Debug.LogError("[AdventurerSpawner] Préfab aventurier non assigné !");
            return null;
        }
        GameObject obj = Instantiate(adventurerPrefab, SpawnPosition, Quaternion.identity);
        return ConfigureAdventurer(obj, null, 2);
    }

    /// <summary>Spawner un groupe d'aventuriers (stats du prefab).</summary>
    public void SpawnGroup()
    {
        if (adventurerPrefab == null)
        {
            Debug.LogError("[AdventurerSpawner] Préfab aventurier non assigné !");
            return;
        }

        int groupSize = Random.Range(minGroupSize, maxGroupSize + 1);
        var groupMembers = new List<AdventurerAI>();

        for (int i = 0; i < groupSize; i++)
        {
            Vector2 offset = Random.insideUnitCircle * groupSpawnRadius;
            GameObject obj = Instantiate(adventurerPrefab, SpawnPosition + (Vector3)offset, Quaternion.identity);
            AdventurerAI ai = ConfigureAdventurer(obj, null, 2);
            if (ai != null) groupMembers.Add(ai);
        }

        if (AdventurerGroupSystem.Instance != null)
            AdventurerGroupSystem.Instance.RegisterGroupSpawn(groupMembers);
    }

    /// <summary>Spawner un membre d'expédition avec les stats de son type.</summary>
    public AdventurerAI SpawnForExpedition(AdventurerTier tier, int tierIndex, int index, int count, Vector3? origin = null,
                                           AdventurerAI.Role role = AdventurerAI.Role.Warrior, GameObject forcedPrefab = null)
    {
        GameObject prefab = tier.prefabOverride != null ? tier.prefabOverride : adventurerPrefab;
        var cat = DungeonCatalog.Get();
        GameObject rolePrefab = cat == null ? null :
            role == AdventurerAI.Role.Archer ? cat.archerPrefab : role == AdventurerAI.Role.Mage ? cat.magePrefab :
            role == AdventurerAI.Role.Healer ? cat.healerPrefab : null;
        if (forcedPrefab != null) prefab = forcedPrefab;
        else if (rolePrefab != null) prefab = rolePrefab;
        // Variantes d'apparence du palier (épéistes de différents niveaux)
        else if (tier.prefabVariants != null && tier.prefabVariants.Count > 0)
        {
            var v = tier.prefabVariants[Random.Range(0, tier.prefabVariants.Count)];
            if (v != null) prefab = v;
        }
        if (prefab == null) return null;

        // Formation en arc de cercle autour du point d'accostage
        Vector2 offset = count > 1
            ? (Vector2)(Quaternion.Euler(0, 0, 360f * index / count) * Vector2.right) * Mathf.Min(groupSpawnRadius, 0.6f * count)
            : Vector2.zero;
        Vector3 pos = (origin ?? SpawnPosition) + (Vector3)offset;
        // Pas dans l'eau : on se place sur la case praticable la plus proche (le ponton)
        if (AstarPath.active != null)
        {
            var nn = AstarPath.active.GetNearest(pos, Pathfinding.NNConstraint.Default);
            if (nn.node != null && nn.node.Walkable)
                pos = (Vector3)nn.node.position + (Vector3)(Random.insideUnitCircle * 0.12f);
        }
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);
        var ai = ConfigureAdventurer(obj, tier, tierIndex, role);
        if (ai != null && role != AdventurerAI.Role.Warrior) ai.ApplyRole(role);
        return ai;
    }

    private AdventurerAI ConfigureAdventurer(GameObject adventurerObj, AdventurerTier tier, int tierIndex, AdventurerAI.Role role = AdventurerAI.Role.Warrior)
    {
        AdventurerAI ai = adventurerObj.GetComponent<AdventurerAI>();
        if (ai == null)
        {
            Debug.LogError("[AdventurerSpawner] Pas de AdventurerAI sur le préfab !");
            return null;
        }

        ai.target = dungeonCore;
        ai.sociability = Random.Range(sociabilityRange.x, sociabilityRange.y);
        Vector2 arr = tier != null ? tier.arroganceRange : arroganceRange;
        ai.arrogance = Random.Range(arr.x, arr.y);

        AdventurerCombat combat = adventurerObj.GetComponent<AdventurerCombat>();
        string title = tier != null ? tier.displayName : "Aventurier";
        // Le rôle prime sur le rang : « Mage Élise », « Archer Hugo »… (les villageois restent des « chasseurs »)
        if (role == AdventurerAI.Role.Archer) title = tierIndex <= 1 ? "Chasseur" : "Archer";
        else if (role == AdventurerAI.Role.Mage) title = tierIndex >= 3 ? "Archimage" : "Mage";
        else if (role == AdventurerAI.Role.Healer) title = tierIndex >= 3 ? "Prêtresse" : "Soigneuse";
        string[] pool = role == AdventurerAI.Role.Healer ? HealerNames : FirstNames;
        string name = pool[Random.Range(0, pool.Length)];
        if (combat != null) combat.adventurerName = $"{title} {name}";

        ai.ApplyTier(tier, tierIndex);
        return ai;
    }

    private void OnDrawGizmos()
    {
        Vector3 p = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(p, 0.5f);
        Gizmos.color = new Color(0, 0, 1, 0.2f);
        Gizmos.DrawWireSphere(p, groupSpawnRadius);
    }
}
