using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Pathfinding;

/// <summary>
/// IA des intrus (pêcheurs, villageois, aventuriers, chevaliers, héros).
///
/// Cycle : [Flâne sur l'île si non averti] → Avance vers le cœur → Combat
///         → (danger) Repli → Soins sur la plage → [Groupe] → Retour à l'assaut
///         → après trop de replis : quitte l'île (et raconte ce qu'il a vu).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(AdventurerCombat))]
public class AdventurerAI : MonoBehaviour
{
    /// <summary>Tous les intrus vivants (évite les FindObjectsByType coûteux).</summary>
    public static readonly List<AdventurerAI> All = new List<AdventurerAI>();
    /// <summary>Un intrus vient de mourir (nécromanciens qui relèvent les morts...).</summary>
    public static event System.Action<AdventurerAI> AnyDied;

    public enum AdventurerState
    {
        Wandering,      // Non averti : flâne sur la plage
        Advancing,      // Se dirige vers le cœur (ou explore la caverne)
        Fighting,
        Retreating,
        Healing,
        WaitingGroup,
        Leaving,        // Quitte l'île
        Looting,        // Pille un coffre (appât)
        Busy            // Occupé par sa vie (manger, boire, chasser, se bagarrer...) : AdventurerLife
    }
    private AdventurerState state = AdventurerState.Advancing;
    public AdventurerState State => state;

    [Header("Cible")]
    public Transform target;  // Cœur du donjon

    [Header("Déplacement")]
    public float speed = 3f;
    public float fleeSpeed = 5f;
    public float nextWpDistance = 0.2f;

    [Header("Combat & Détection")]
    public float detectionRange = 5f;
    public float fieldOfViewAngle = 120f;
    public LayerMask enemyLayer;
    [Tooltip("Couches qui bloquent la vue (murs). Vide = couche 6")]
    public LayerMask obstacleLayer;

    [Header("Sentiment de Sécurité")]
    [FormerlySerializedAs("healthThreshold")]
    [Range(0f, 100f)] public float baseHealthThreshold = 30f;
    [FormerlySerializedAs("dangerousEnemyCount")]
    public int baseDangerousEnemyCount = 3;
    [FormerlySerializedAs("safeDistanceFromDungeon")]
    public float baseSafeDistance = 10f;

    [Header("Personality Traits (Set at Spawn)")]
    [Range(0f, 1f)] public float arrogance = 0.5f;
    [Range(0f, 1f)] public float sociability = 0.5f;

    [Tooltip("PV régénérés par seconde en zone sûre")]
    public float healingPerSecond = 5f;

    [Header("Exploration (intrus non avertis)")]
    public float wanderRadius = 5f;
    [Tooltip("Temps passé à flâner avant d'aller voir la caverne (min, max)")]
    public Vector2 curiosityDelay = new Vector2(8f, 20f);
    [Tooltip("Distance à laquelle un intrus non averti remarque le cristal")]
    public float coreDiscoveryRange = 4f;

    [Header("Components")]
    public Animator animator;
    public AdventurerCombat combat;

    // ── Vie (faim, traits, humeurs) : AdventurerLife ──
    public AdventurerLife Life { get; private set; }
    [System.NonSerialized] public float moodSpeedMult = 1f;
    /// <summary>Torche éteinte par la pluie : dans le donjon, il voit moins loin.</summary>
    [System.NonSerialized] public bool TorchWet;
    /// <summary>Le Champion de la Guilde (objectif de domination).</summary>
    [System.NonSerialized] public bool IsChampion;
    /// <summary>Aveuglé par les Ombres du Noyau jusqu'à cet instant.</summary>
    [System.NonSerialized] public float BlindUntil;
    // pour la chronique : qui l'a frappé en dernier
    private GobelinAI lastMonsterHit;
    private float lastMonsterHitAt = -99f;
    [System.NonSerialized] public AdventurerAI LastBrawler;
    [System.NonSerialized] public float LastBrawlHitAt = -99f;
    [Tooltip("Ajouté à la probabilité de fuir (peureux +, courageux -)")]
    [System.NonSerialized] public float fleeBias;
    /// <summary>Direction de marche (pour placer la formation derrière le guerrier).</summary>
    public Vector2 Heading => lastMoveDirection;
    public Transform CurrentEnemy => currentEnemy;
    public bool IsFighting => state == AdventurerState.Fighting;
    public float HealthPct => combat != null && combat.maxHealth > 0f ? combat.currentHealth / combat.maxHealth : 0f;

    // ── Type / personnalité ──
    public int TierIndex { get; private set; } = 2;
    public AdventurerTier Tier { get; private set; }
    public bool IsAware { get; private set; } = true;
    public bool HasSeenCore { get; private set; }
    public bool HasEnteredDungeon => hasAttemptedDungeon;
    public bool IsInsideDungeon { get; private set; }
    public string DisplayName => combat != null ? combat.adventurerName : name;

    // ── Rang de la Guilde (-1 : civil, pêcheur, villageois) ──
    public int Rank { get; private set; } = -1;
    /// <summary>Membre de l'équipe d'évaluation de la Guilde.</summary>
    public bool IsAssessor { get; set; }

    // ── Ce qu'il a vu (son récit s'il rentre vivant) ──
    public int MonstersSeen => seenMonsters.Count;
    public int MaxMonsterStageSeen { get; private set; }
    public float DamageTakenFraction => combat != null && combat.maxHealth > 0f ? damageTaken / combat.maxHealth : 0f;
    public int ChestsLooted => lootedChests.Count;
    private readonly HashSet<int> seenMonsters = new HashSet<int>();
    /// <summary>Pièges repérés (cases) : une Némésis s'en souviendra.</summary>
    public readonly HashSet<Vector2Int> SeenTraps = new HashSet<Vector2Int>();
    public NemesisRecord Nemesis { get; private set; }
    public bool IsPrisoner { get; private set; }

    /// <summary>Assommé et enfermé : plus d'IA, plus de collisions, assis derrière les barreaux.</summary>
    public void BecomePrisoner(Vector3 pos)
    {
        IsPrisoner = true;
        CancelInvoke();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (combat == null) combat = GetComponent<AdventurerCombat>();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.position = pos;
        transform.position = pos;
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (combat != null) { combat.currentHealth = Mathf.Max(1f, combat.maxHealth * 0.1f); combat.enabled = false; }
        foreach (var hb in GetComponentsInChildren<HealthBar>(true)) hb.gameObject.SetActive(false);
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingOrder = 3;
            sr.color = new Color(0.7f, 0.68f, 0.75f, sr.color.a);
        }
        if (animator != null) { animator.SetFloat("Speed", 0f); animator.SetBool("IsAttacking", false); }
        myGroup?.RemoveMember(this);
        enabled = false;
    }
    private HashSet<Vector2Int> knownTraps;

    /// <summary>Ce survivant revient : surnom, rang gagné, mémoire des pièges.</summary>
    public void MakeNemesis(NemesisRecord r)
    {
        Nemesis = r;
        knownTraps = NemesisSystem.TrapSet(r);
        // le vétéran revient avec le souvenir de sa carte : son expédition en profite
        if (Map == null) Map = ExpeditionManager.Instance != null ? ExpeditionManager.Instance.MapFor(this) : new IntruderMap();
        Map.Remember(r.map, r.coreKnown, r.coreX, r.coreY);
        if (combat != null)
        {
            combat.maxHealth *= 1.2f;
            combat.currentHealth = combat.maxHealth;
            combat.adventurerName = $"{RoleName(role)} {r.name} « {r.epithet} »" + (Rank >= 0 ? $" · rang {GuildSystem.RankName(Rank)}" : "");
        }
        arrogance = Mathf.Min(1f, arrogance + 0.25f);
        fleeChance *= 0.7f;
        NemesisSystem.AddTag(transform, $"{r.name} « {r.epithet} »", new Color(1f, 0.45f, 0.4f));
    }

    /// <summary>La Némésis désamorce les pièges qu'elle a déjà vus.</summary>
    private void NemesisTick()
    {
        if (knownTraps == null || knownTraps.Count == 0 || !IsInsideDungeon || IsHeld) return;
        foreach (var p in PlacedObject.All)
        {
            if (p == null || p.kind != ItemKind.Trap || p.Disarmed || p.Starved) continue;
            if (Vector2.Distance(p.transform.position, rb.position) > 1.4f) continue;
            var c = new Vector2Int(Mathf.FloorToInt(p.transform.position.x), Mathf.FloorToInt(p.transform.position.y));
            if (!knownTraps.Contains(c)) continue;
            Hold(1.3f);
            p.Disarm(30f);
            FloatingText.Spawn(transform.position + Vector3.up * 0.9f, "Je connais ce piège !", new Color(1f, 0.55f, 0.45f), 1.2f);
            Sfx.Play("trap_snap", p.transform.position, 0.6f);
            break;
        }
    }
    private float damageTaken;

    /// <summary>Rang de la Guilde : la puissance suit le rang (F = 0,5× … SSS = 5× les PV d'un aventurier de rang C).</summary>
    public void ApplyRank(int rank)
    {
        Rank = rank;
        if (rank < 0 || combat == null) return;
        if (combat != null && !combat.adventurerName.Contains("rang")) combat.adventurerName += $" · rang {GuildSystem.RankName(rank)}";
        ApplyRankLook(rank);
    }

    /// <summary>L'armure se voit : rang F = tenue de toile (palier 1) … SSS = armure dorée (palier 9).</summary>
    private void ApplyRankLook(int rank)
    {
        var cat = DungeonCatalog.Get();
        var anim = GetComponentInChildren<Animator>();
        if (cat == null || anim == null) return;
        var list = role == Role.Archer ? cat.lookArcher : role == Role.Mage ? cat.lookMage : role == Role.Healer ? cat.lookHealer : cat.lookWarrior;
        int t = Mathf.Clamp(rank + 1, 1, 9);
        if (list == null || list.Count < t || list[t - 1] == null) return;
        anim.runtimeAnimatorController = list[t - 1];
        anim.Update(0f);
    }

    /// <summary>Note les monstres visibles (une fois chacun) : de quoi raconter des histoires terrifiantes.</summary>
    private void ObserveTick()
    {
        if (isDead || !IsInsideDungeon) return;
        Map.Observe(rb.position, dungeonCoreTarget);
        if (!HasSeenCore && CanSeeCore()) { HasSeenCore = true; coreSeenAt = Time.time; }
        var hits = Phys.Circle(rb.position, detectionRange * IslandEvents.Detection, enemyLayer);
        foreach (var h in hits)
        {
            var u = h.GetComponentInParent<MonsterUnit>();
            if (u == null || seenMonsters.Contains(u.GetInstanceID()) || !HasLineOfSight(h.transform)) continue;
            seenMonsters.Add(u.GetInstanceID());
            MaxMonsterStageSeen = Mathf.Max(MaxMonsterStageSeen, u.stage);
        }
        foreach (var p in PlacedObject.All)
        {
            if (p == null || p.kind != ItemKind.Trap) continue;
            if (Vector2.Distance(p.transform.position, rb.position) > detectionRange * 0.8f) continue;
            if (!ClearLine(rb.position, p.transform.position, obstacleLayer)) continue;
            SeenTraps.Add(new Vector2Int(Mathf.FloorToInt(p.transform.position.x), Mathf.FloorToInt(p.transform.position.y)));
            // l'éclaireur (archer, chanceux) repère le piège et prévient toute l'expédition
            bool scout = role == Role.Archer || (Life != null && Life.Has(AdventurerLife.Trait.Lucky));
            if (scout && Map != null && DungeonBuilder.Instance != null && Random.value < 0.35f)
            {
                var cell = DungeonBuilder.Instance.floorTilemap.WorldToCell(p.transform.position);
                if (Map.Traps.Add(cell)) { Map.Version++; Speak("Attention, un piège !", new Color(1f, 0.85f, 0.5f)); }
            }
        }
        NemesisTick();
    }

    public enum Role { Warrior, Archer, Mage, Healer }
    public Role role = Role.Warrior;
    public bool IsRanged => role == Role.Archer || role == Role.Mage;

    [Header("Soins (soigneuse)")]
    public float healRange = 5f;
    public float healCooldown = 3.5f;
    private float nextHeal;

    public static string RoleName(Role r) =>
        r == Role.Archer ? "Archer" : r == Role.Mage ? "Mage" : r == Role.Healer ? "Soigneuse" : "Guerrier";

    /// <summary>Spécialise l'intrus (après ApplyTier) : portée, dégâts, résistance.</summary>
    public void ApplyRole(Role r)
    {
        role = r;
        if (combat == null) combat = GetComponent<AdventurerCombat>();
        combat.role = r;
        switch (r)
        {
            case Role.Archer:
                combat.maxHealth *= 0.75f; combat.attackDamage *= 0.85f; combat.attackRange = 4.2f; combat.attackCooldown *= 1.25f;
                speed *= 1.05f;
                break;
            case Role.Mage:
                combat.maxHealth *= 0.6f; combat.attackDamage *= 1.5f; combat.attackRange = 3.8f; combat.attackCooldown *= 1.9f;
                combat.splashRadius = 1.3f;
                break;
            case Role.Healer:
                combat.maxHealth *= 0.75f; combat.attackDamage *= 0.45f;
                break;
        }
        combat.currentHealth = combat.maxHealth;
    }

    private bool AllyEngaging(Transform enemy)
    {
        foreach (var a in All)
            if (a != this && a != null && !a.isDead && a.role != Role.Healer && !a.IsFleeing() &&
                Vector2.Distance(a.transform.position, enemy.position) < 2.5f) return true;
        return false;
    }

    /// <summary>La soigneuse soigne l'allié le plus blessé à portée (elle-même comprise).</summary>
    private void UpdateHealer()
    {
        if (role != Role.Healer || isDead || Time.time < nextHeal) return;
        AdventurerAI best = null;
        float worst = 0.82f;
        foreach (var a in All)
        {
            if (a == null || a.isDead || a.combat == null || a.combat.maxHealth <= 0f) continue;
            float pct = a.combat.currentHealth / a.combat.maxHealth;
            if (pct >= worst) continue;
            if (Vector2.Distance(a.transform.position, rb.position) > healRange) continue;
            if (a != this && !HasLineOfSight(a.transform)) continue;
            worst = pct;
            best = a;
        }
        if (best == null) return;
        nextHeal = Time.time + healCooldown;
        float amount = 10f + best.combat.maxHealth * 0.18f;
        best.combat.currentHealth = Mathf.Min(best.combat.maxHealth, best.combat.currentHealth + amount);
        attackAnimTimer = 0.5f;
        Vector2 d = (Vector2)best.transform.position - rb.position;
        if (d.sqrMagnitude > 0.0001f) lastMoveDirection = d.normalized;
        Sfx.Play("heal", best.transform.position, 0.6f);
        Fx.Play("heal", best.transform.position + Vector3.down * 0.1f, 1.1f, 16f, 4, null, false, best.transform, new Color(0.4f, 1f, 0.5f));
        FloatingText.Spawn(best.transform.position + Vector3.up * 0.7f, $"+{Mathf.RoundToInt(amount)}", new Color(0.45f, 1f, 0.5f), 1f);
    }
    [Tooltip("Habitant de la colonie (visiteur à pied)")]
    public bool isLocal;
    /// <summary>Épargné par le Noyau : les monstres le laissent repartir.</summary>
    public bool Spared { get; private set; }

    /// <summary>Le Noyau laisse partir cet intrus (geste de paix envers la colonie).</summary>
    public void Spare()
    {
        if (isDead || departed || Spared) return;
        Spared = true;
        IsAware = true;
        currentEnemy = null;
        FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "Épargné", new Color(0.6f, 1f, 0.7f), 1.1f);
        LeaveIsland();
    }

    private int maxRetreats = 1;
    private float fleeChance = 0.6f;
    private float greed = 0.5f;
    private float braveUntil;          // "tient bon" jusqu'à cet instant
    private bool lastStand;            // a déjà refusé de fuir à PV critiques
    private float themeDamageAccum;
    private TreasureChest targetChest;
    private float lootTimer;
    private readonly HashSet<TreasureChest> lootedChests = new HashSet<TreasureChest>();
    private int retreatCount;
    private int manaReward = 100;
    private int xpReward = 50;

    // Seuils calculés
    private float healthThreshold;
    private int dangerousEnemyCount;

    // État
    private bool hasAttemptedDungeon;
    private bool hasRetreated;
    private bool triedGroup;
    private bool isDead;
    public bool IsDead => isDead;
    private bool departed;
    private AdventurerGroup myGroup;
    private Transform currentEnemy;
    private Transform dungeonCoreTarget;
    private Vector2 lastMoveDirection = Vector2.down;
    private Vector2 movement;
    private Vector2 spawnPoint;
    private Vector2 safeZonePosition;
    private float wanderEndTime;
    private float nextWanderPick;
    private float waitGroupSince;
    private float attackAnimTimer;
    private Vector2 stuckCheckPos;
    /// <summary>Compteur de blocages (tests).</summary>
    public static int StuckEvents;
    /// <summary>Distance max de validation d'un point de chemin (moins d'une demi-case).</summary>
    public const float MaxWaypointReach = 0.35f;
    private float stuckCheckTime;
    private Collider2D bodyCol;
    private bool slipping;

    // Pathfinding
    private Path path;
    private int currWp;
    private Seeker seeker;
    private Rigidbody2D rb;
    private Transform followTarget;
    private Vector2 destination;
    private bool hasDestination;
    private Vector2 lastPathDest = Vector2.positiveInfinity;
    private float lastPathTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        seeker = GetComponent<Seeker>();
        combat = GetComponent<AdventurerCombat>();
        if (animator == null) animator = GetComponent<Animator>();
        bodyCol = GetComponent<Collider2D>();
        spawnPoint = rb.position;
    }

    /// <summary>
    /// Un intrus qui fuit se faufile entre les monstres : sans ça, un seul monstre posté
    /// dans un couloir d'une case bloque physiquement la retraite et l'intrus meurt coincé.
    /// </summary>
    private void UpdateSlipThrough()
    {
        bool slip = IsFleeing();
        if (slip == slipping || bodyCol == null) return;
        slipping = slip;
        bodyCol.excludeLayers = slip ? (LayerMask)(1 << 8) : (LayerMask)0;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>Appliqué par le spawner juste après l'Instantiate.</summary>
    public void ApplyTier(AdventurerTier tier, int tierIndex)
    {
        Tier = tier;
        TierIndex = tierIndex;
        if (tier == null) return;

        var colony = SettlementManager.Instance;
        combat.maxHealth *= tier.healthMult * (colony != null ? colony.AdventurerHpMult : 1f) * GameDifficulty.IntruderHp;
        combat.currentHealth = combat.maxHealth;
        combat.attackDamage *= tier.damageMult * (colony != null ? colony.AdventurerDmgMult : 1f) * GameDifficulty.IntruderDamage;
        speed *= tier.speedMult;
        fleeSpeed *= tier.speedMult;
        IsAware = !tier.unaware;
        maxRetreats = tier.maxRetreats;
        fleeChance = tier.fleeChance * GameDifficulty.FleeMult;
        greed = tier.greed;
        baseDangerousEnemyCount = tier.courage;
        manaReward = tier.manaReward;
        xpReward = tier.xpReward;
        transform.localScale *= tier.scale;
        combat.SetTint(tier.tint);
    }

    private void Start()
    {
        if (target == null)
        {
            var core = FindFirstObjectByType<DungeonCore>();
            if (core != null) target = core.transform;
        }
        if (target == null)
        {
            Debug.LogError($"[{DisplayName}] Pas de cible !");
            enabled = false;
            return;
        }
        dungeonCoreTarget = target;
        if (obstacleLayer.value == 0) obstacleLayer = 1 << 6;
        // la carte de son expédition (vierge : on ne sait rien du donjon d'avance)
        if (Map == null) Map = ExpeditionManager.Instance != null ? ExpeditionManager.Instance.MapFor(this) : new IntruderMap();

        CalculateThresholdsFromArrogance();

        if (IsAware)
        {
            state = AdventurerState.Advancing;
            SetFollow(dungeonCoreTarget);
        }
        else
        {
            state = AdventurerState.Wandering;
            wanderEndTime = Time.time + Random.Range(curiosityDelay.x, curiosityDelay.y);
            PickWanderPoint();
        }

        HealthBar.Attach(transform, () => combat.currentHealth / combat.maxHealth, new Color(0.9f, 0.25f, 0.2f));
        if (!isLocal && !IsPrisoner) Life = GetComponent<AdventurerLife>() ?? gameObject.AddComponent<AdventurerLife>();
        InvokeRepeating(nameof(RepathTick), Random.Range(0f, 0.5f), 0.5f);
        InvokeRepeating(nameof(ObserveTick), Random.Range(0f, 0.7f), 0.7f);
    }

    /// <summary>Arrogance élevée = plus téméraire.</summary>
    private void CalculateThresholdsFromArrogance()
    {
        healthThreshold = baseHealthThreshold * (1f - arrogance * 0.75f);
        dangerousEnemyCount = Mathf.Max(1, baseDangerousEnemyCount + Mathf.RoundToInt(arrogance * 2f));
    }

    // ═══════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════

    private float nextWallCheck;

    private void FixedUpdate()
    {
        if (dungeonCoreTarget == null || isDead) return;
        // pris dans un mur ou sous un objet encombrant : on le dégage
        if (Time.time >= nextWallCheck)
        {
            nextWallCheck = Time.time + 0.5f;
            if (DungeonTerritory.InsideObstacle(rb.position))
            {
                Vector2 free = DungeonTerritory.FreeSpotNear(rb.position, false);
                rb.position = free;
                transform.position = free;
                path = null;
            }
        }

        UpdateDungeonPresence();

        if (IsAware && (state == AdventurerState.Advancing || state == AdventurerState.Fighting))
        {
            if (Time.time >= braveUntil && IsInDanger())
                DecideToFlee();
        }

        // Régénération passive lente hors du donjon
        if (!IsInsideDungeon && state != AdventurerState.Healing && state != AdventurerState.Fighting)
            Heal(healingPerSecond * 0.3f * Time.fixedDeltaTime);

        ApplyThemeEffects();
        UpdateSlipThrough();
        UpdateHealer();

        switch (state)
        {
            case AdventurerState.Looting: UpdateLooting(); break;
            case AdventurerState.Wandering: UpdateWandering(); break;
            case AdventurerState.Advancing: UpdateAdvancing(); break;
            case AdventurerState.Fighting: UpdateFighting(); break;
            case AdventurerState.Retreating: UpdateRetreating(); break;
            case AdventurerState.Healing: UpdateHealing(); break;
            case AdventurerState.WaitingGroup: UpdateWaitingGroup(); break;
            case AdventurerState.Leaving: UpdateLeaving(); break;
            case AdventurerState.Busy: if (Life == null || !Life.BusyTick()) EndBusy(); break;
        }

        if (attackAnimTimer > 0f) attackAnimTimer -= Time.fixedDeltaTime;
        CheckStuck();
        UpdateAnimator();
    }

    /// <summary>Détecte un intrus qui n'avance plus (obstacle, destination inaccessible).</summary>
    private void CheckStuck()
    {
        bool shouldMove = state == AdventurerState.Advancing || state == AdventurerState.Retreating ||
                          state == AdventurerState.Leaving || state == AdventurerState.Wandering;
        bool atCore = dungeonCoreTarget != null && Vector2.Distance(rb.position, dungeonCoreTarget.position) <= combat.attackRange + 0.6f;
        if (!shouldMove || currentEnemy != null || IsHeld || atCore)
        {
            stuckCheckTime = Time.time;
            stuckCheckPos = rb.position;
            return;
        }
        if (Time.time - stuckCheckTime < 1.5f) return;

        bool stuck = Vector2.Distance(rb.position, stuckCheckPos) < 0.25f;
        stuckCheckTime = Time.time;
        stuckCheckPos = rb.position;
        if (!stuck) return;

        switch (state)
        {
            case AdventurerState.Leaving:
                if (!IsInsideDungeon) Depart();
                else RequestPathNow();
                break;
            case AdventurerState.Retreating:
                if (!IsInsideDungeon) { Stop(); state = AdventurerState.Healing; }
                else RequestPathNow();
                break;
            case AdventurerState.Wandering:
                PickWanderPoint();
                break;
            case AdventurerState.Advancing:
                // Un monstre bouche le passage : on l'affronte plutôt que de piétiner
                var blocker = NearestMonster(1.3f);
                if (blocker != null) { IsAware = true; EngageEnemy(blocker); break; }
                // Recentrage dans sa case (loin de l'arête du mur), puis nouveau chemin
                Vector2 cell = new Vector2(Mathf.Floor(rb.position.x) + 0.5f, Mathf.Floor(rb.position.y) + 0.5f);
                rb.position = Vector2.MoveTowards(rb.position, cell, 0.25f);
                StuckEvents++;
                if (exploring) nextPlan = 0f;   // exploration : nouveau plan
                else RequestPathNow();
                break;
        }
    }

    private Transform NearestMonster(float radius)
    {
        Transform best = null;
        float bd = radius;
        foreach (var g in GobelinAI.All)
        {
            if (g == null || !g.IsAlive()) continue;
            float d = Vector2.Distance(g.transform.position, rb.position);
            if (d < bd) { bd = d; best = g.transform; }
        }
        return best;
    }

    private void UpdateDungeonPresence()
    {
        bool inside = DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(rb.position);
        if (inside && !hasAttemptedDungeon)
        {
            hasAttemptedDungeon = true;
            StoryManager.Instance?.OnIntruderEntered(this);
        }
        IsInsideDungeon = inside;
        // talent « Torches mourantes » : au bout de 40 s sous la montagne, la flamme s'éteint
        if (inside) { if (enteredAt < 0f) enteredAt = Time.time; else if (Talents.TorchesDie && !TorchWet && Time.time - enteredAt > 40f) { TorchWet = true; Speak("Ma torche s'éteint...", new Color(0.8f, 0.8f, 0.9f)); } }
        else enteredAt = -1f;
    }
    private float enteredAt = -1f;

    // ═══════════════════════════════════════════
    // ÉTATS
    // ═══════════════════════════════════════════

    private void UpdateWandering()
    {
        Transform monster = DetectNearestEnemy();
        if (monster != null)
        {
            OnSurprised(monster);
            return;
        }

        if (Time.time >= wanderEndTime)
        {
            // La curiosité l'emporte : il va voir cette caverne...
            state = AdventurerState.Advancing;
            SetFollow(dungeonCoreTarget);
            DLog.Log($"[{DisplayName}] Curieux, entre dans la caverne");
            return;
        }

        if (!hasDestination || ReachedPathEnd() || Time.time > nextWanderPick)
            PickWanderPoint();

        MoveAlongPath(speed * 0.6f);
    }

    private void UpdateAdvancing()
    {
        Transform detected = DetectNearestEnemy();
        if (detected != null)
        {
            if (!IsAware) OnSurprised(detected);
            else EngageEnemy(detected);
            return;
        }

        if (!IsAware && CanSeeCore())
        {
            DiscoverCore();
            return;
        }

        if (IsInsideDungeon && TryFindChest()) return;
        if (FollowFormation()) return;

        // L'équipe d'évaluation mesure, elle ne conquiert pas : un tour d'horizon, puis elle rentre
        if (IsAssessor && IsInsideDungeon)
        {
            if (insideSince < 0f) insideSince = Time.time;
            if ((HasSeenCore && Time.time - coreSeenAt > 6f) || Time.time - insideSince > 75f)
            {
                FloatingText.Spawn(transform.position + Vector3.up * 0.8f, HasSeenCore ? "On en a assez vu." : "Rapport !", new Color(0.8f, 0.9f, 1f), 1.1f);
                LeaveIsland();
                return;
            }
        }

        float distToCore = Vector2.Distance(rb.position, dungeonCoreTarget.position);
        // Le cristal aussi doit être en vue : plus de tirs à travers la roche
        if (IsAware && !IsAssessor && distToCore <= combat.attackRange && (distToCore <= 1.3f || HasLineOfSight(dungeonCoreTarget)))
        {
            AttackTarget(dungeonCoreTarget);
        }
        else if (!IsInsideDungeon || (IsAware && !IsAssessor && distToCore < IntruderMap.SightRadius && HasLineOfSight(dungeonCoreTarget)))
        {
            // dehors : on rejoint l'entrée de la caverne (elle se voit de loin) ; dedans : le cristal est en vue, on fonce
            SetFollow(dungeonCoreTarget);
            MoveAlongPath(IsAware ? speed : speed * 0.7f);
        }
        else ExploreTick(IsAware ? speed : speed * 0.7f);
    }

    // ═══════════════════════════════════════════
    // EXPLORATION (brouillard de guerre)
    // ═══════════════════════════════════════════

    /// <summary>Carte de son expédition (partagée avec ses compagnons de voyage).</summary>
    [System.NonSerialized] public IntruderMap Map;

    private bool exploring, goalIsFrontier;
    private Vector3Int exploreGoal;
    private Vector2 exploreHeading;
    private float nextPlan, nextGoalCheck, insideSince = -1f, coreSeenAt;

    /// <summary>
    /// Dans le donjon, l'intrus ne connaît que ce que son expédition a vu. Il garde son cap : il suit la galerie
    /// choisie jusqu'au bout (ou au prochain carrefour) avant de choisir de nouveau — continuer ou faire demi-tour.
    /// </summary>
    private float fallbackUntil;

    private void ExploreTick(float moveSpeed)
    {
        // en route vers la lueur du cristal (plus rien d'inconnu) : on ne recalcule pas à chaque pas
        if (Time.time < fallbackUntil) { MoveAlongPath(moveSpeed); return; }
        bool replan = !exploring || path == null || ReachedPathEnd() || Time.time >= nextPlan;
        // la galerie visée est maintenant connue (rien de plus à découvrir là-bas) : nouveau choix
        if (!replan && goalIsFrontier && Time.time >= nextGoalCheck)
        {
            nextGoalCheck = Time.time + 0.5f;
            if (!Map.IsFrontier(exploreGoal)) replan = true;
        }
        // le cristal vient d'être repéré : on y va
        if (!replan && goalIsFrontier && IsAware && !IsAssessor && Map.CoreCell.HasValue) replan = true;
        if (replan) PlanExploration();
        if (state != AdventurerState.Advancing) return;
        MoveAlongPath(moveSpeed);
        if (movement.sqrMagnitude > 0.01f) exploreHeading = Vector2.Lerp(exploreHeading, movement.normalized, 0.1f);
    }

    private void PlanExploration()
    {
        nextPlan = Time.time + 12f;   // sécurité : on ne garde pas un plan éternellement
        var b = DungeonBuilder.Instance;
        if (b == null) return;
        var me = b.floorTilemap.WorldToCell(rb.position);
        // un premier regard avant de décider (à l'entrée, la carte est encore vide)
        if (!Map.Floor.Contains(me)) Map.Observe(rb.position, dungeonCoreTarget);
        if (!Map.Floor.Contains(me)) { nextPlan = Time.time + 0.4f; path = null; return; }
        List<Vector3Int> cells = null;
        goalIsFrontier = false;
        // cristal repéré : on y va par le chemin connu (les évaluateurs, eux, ne le cherchent pas)
        if (IsAware && !IsAssessor && Map.CoreCell.HasValue)
        {
            var goal = Map.CoreCell.Value;
            cells = Map.FindPath(me, c => Mathf.Abs(c.x - goal.x) + Mathf.Abs(c.y - goal.y) <= 1);
        }
        if (cells == null)
        {
            var frontiers = Map.NearestFrontiers(me, 6);
            if (frontiers.Count == 0)
            {
                if (IsAssessor)
                {
                    FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "Relevé terminé.", new Color(0.85f, 0.85f, 1f), 1.4f);
                    LeaveIsland();
                    return;
                }
                // plus rien d'inconnu à portée : il tente sa chance vers la lueur du cristal
                exploring = false;
                SetFollow(dungeonCoreTarget);
                fallbackUntil = Time.time + 4f;
                return;
            }
            // on garde le cap : la galerie qui prolonge sa marche est préférée à un demi-tour
            Vector2 heading = exploreHeading.sqrMagnitude > 0.01f ? exploreHeading.normalized : lastMoveDirection;
            float bestScore = float.MaxValue;
            foreach (var f in frontiers)
            {
                Vector2 to = (Vector2)(Vector3)(f[f.Count - 1] - me);
                float dot = to.sqrMagnitude > 0.01f ? Vector2.Dot(heading, to.normalized) : 0f;
                float score = f.Count - 4f * dot + Random.Range(0f, 0.8f);
                if (score < bestScore) { bestScore = score; cells = f; }
            }
            exploreGoal = cells[cells.Count - 1];
            goalIsFrontier = true;
            Map.ExtendThroughStairs(cells);
        }
        exploring = true;
        followTarget = null;
        hasDestination = false;   // pas de recalcul A* : on suit le plan connu
        seeker?.CancelCurrentPathRequest();
        var pts = IntruderMap.ToWaypoints(cells, rb.position);
        path = ABPath.FakePath(pts);
        currWp = pts.Count > 1 ? 1 : 0;
    }

    private void UpdateFighting()
    {
        IDamageable enemyDmg = currentEnemy != null ? currentEnemy.GetComponent<IDamageable>() : null;
        if (enemyDmg == null || !enemyDmg.IsAlive())
        {
            ResumeAttack();
            return;
        }

        float dist = Vector2.Distance(rb.position, currentEnemy.position);
        if (dist > detectionRange * 1.6f)
        {
            ResumeAttack();
            return;
        }

        // Blessé : on recule derrière le guerrier (la soigneuse s'occupe de lui)
        if (FallBackBehindTank(dist)) return;
        // Tireurs : garder ses distances (on recule si le monstre colle)
        if (IsRanged && dist < 2.1f && Kite()) return;

        // La soigneuse reste en retrait tant qu'un allié tient la ligne
        if (role == Role.Healer && dist > 1.6f && AllyEngaging(currentEnemy))
        {
            Stop();
            Vector2 look = (Vector2)currentEnemy.position - rb.position;
            if (look.sqrMagnitude > 0.0001f) lastMoveDirection = look.normalized;
            return;
        }

        if (dist <= combat.attackRange && HasLineOfSight(currentEnemy))
        {
            AttackTarget(currentEnemy);
        }
        else
        {
            SetFollow(currentEnemy);
            MoveAlongPath(speed);
        }
    }

    private void UpdateRetreating()
    {
        bool arrived = Vector2.Distance(rb.position, safeZonePosition) < 1.5f || ReachedPathEnd();
        if (arrived && !IsInsideDungeon)
        {
            Stop();
            state = AdventurerState.Healing;
            DLog.Log($"[{DisplayName}] Zone sûre, soins...");
            return;
        }
        MoveAlongPath(fleeSpeed);
    }

    private void UpdateHealing()
    {
        Stop();
        // Au camp de la colonie (potions du mage), les soins sont plus rapides
        var colony = SettlementManager.Instance;
        float campMult = colony != null && colony.CampfirePosition.HasValue ? (colony.HasBuilding("tent_magic") ? 2.5f : 1.6f) : 1f;
        var ownCamp = IntruderCamp.Of(this);
        if (ownCamp != null && Vector2.Distance(ownCamp.Center, rb.position) < 3f) campMult = Mathf.Max(campMult, 2f);
        Heal(healingPerSecond * campMult * Time.fixedDeltaTime);
        if (combat.currentHealth < combat.maxHealth) return;

        // Pleinement soigné : que faire ?
        if (retreatCount > maxRetreats)
        {
            LeaveIsland();
        }
        else if (!triedGroup && myGroup == null && sociability > 0.3f && AdventurerGroupSystem.Instance != null)
        {
            triedGroup = true;
            state = AdventurerState.WaitingGroup;
            waitGroupSince = Time.time;
            if (!AdventurerGroupSystem.Instance.TryJoinOrCreateGroup(this))
                ResumeAttack();
        }
        else
        {
            ResumeAttack();
        }
    }

    private void UpdateWaitingGroup()
    {
        Stop();
        Heal(healingPerSecond * Time.fixedDeltaTime);
        if (Time.time - waitGroupSince > 45f)
            ResumeAttack();   // sécurité : le groupe ne s'est jamais formé
    }

    private void UpdateLeaving()
    {
        if (Vector2.Distance(rb.position, spawnPoint) < 1f || (ReachedPathEnd() && !IsInsideDungeon))
        {
            Depart();
            return;
        }
        MoveAlongPath(fleeSpeed);
    }

    // ═══════════════════════════════════════════
    // TRANSITIONS
    // ═══════════════════════════════════════════

    private void EngageEnemy(Transform enemy)
    {
        // le groupe se concentre sur la cible du guerrier quand elle est en vue
        var tank = PartyTank();
        if (tank != null && tank != this && tank.currentEnemy != null && tank.currentEnemy != enemy)
        {
            var td = tank.currentEnemy.GetComponent<IDamageable>();
            if (td != null && td.IsAlive() && Vector2.Distance(tank.currentEnemy.position, rb.position) <= detectionRange && HasLineOfSight(tank.currentEnemy))
                enemy = tank.currentEnemy;
        }
        currentEnemy = enemy;
        if (state != AdventurerState.Fighting) Voices.Human(combat, "aggro", 0.55f);
        state = AdventurerState.Fighting;
        SetFollow(enemy);
    }

    private void ResumeAttack()
    {
        currentEnemy = null;
        state = AdventurerState.Advancing;
        if (IsInsideDungeon) { exploring = false; path = null; }   // l'exploration reprendra au prochain pas
        else SetFollow(dungeonCoreTarget);
    }

    /// <summary>Un intrus non averti tombe nez à nez avec un monstre.</summary>
    private void OnSurprised(Transform monster)
    {
        IsAware = true;
        if (Random.value > fleeChance + (0.3f - arrogance * 0.4f))
        {
            FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "À l'attaque !", new Color(1f, 0.6f, 0.4f), 1f);
            EngageEnemy(monster);
        }
        else
        {
            FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "!", new Color(1f, 0.9f, 0.3f), 1.2f);
            StartRetreat();
        }
    }

    /// <summary>Un intrus non averti aperçoit le cristal : il file prévenir tout le monde.</summary>
    private void DiscoverCore()
    {
        HasSeenCore = true;
        IsAware = true;
        FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "!!", new Color(1f, 0.5f, 0.3f), 1.4f);
        StoryManager.Instance?.OnCoreSpotted(this);
        LeaveIsland();
    }

    private void StartRetreat()
    {
        if (state == AdventurerState.Retreating || state == AdventurerState.Healing ||
            state == AdventurerState.Leaving || state == AdventurerState.WaitingGroup) return;

        retreatCount++;
        hasRetreated = true;
        currentEnemy = null;
        state = AdventurerState.Retreating;

        Vector2 offset = Random.insideUnitCircle * 2f;
        safeZonePosition = spawnPoint + offset;
        // La colonie a un feu de camp : on s'y replie pour se soigner
        var camp = SettlementManager.Instance != null ? SettlementManager.Instance.CampfirePosition : (Vector2?)null;
        if (camp.HasValue) safeZonePosition = camp.Value + offset * 0.5f;
        else
        {
            // à défaut de colonie : le camp de l'expédition
            var own = IntruderCamp.Of(this);
            if (own != null) safeZonePosition = own.Center + offset * 0.5f;
        }
        SetDestination(safeZonePosition);

        DLog.Log($"[{DisplayName}] RETRAITE ({retreatCount}/{maxRetreats + 1})");
    }

    /// <summary>Ordre de repli général (expédition qui abandonne).</summary>
    public void ForceLeave()
    {
        if (isDead || departed || state == AdventurerState.Leaving) return;
        LeaveIsland();
    }

    /// <summary>Qui a décidé du départ (tests).</summary>
    [System.NonSerialized] public string LeaveReason;

    private void LeaveIsland()
    {
#if UNITY_EDITOR
        var st = new System.Diagnostics.StackTrace();
        LeaveReason = st.FrameCount > 2 ? st.GetFrame(1).GetMethod().Name + "<" + st.GetFrame(2).GetMethod().Name + (st.FrameCount > 3 ? "<" + st.GetFrame(3).GetMethod().DeclaringType?.Name + "." + st.GetFrame(3).GetMethod().Name : "") : "?";
#endif
        state = AdventurerState.Leaving;
        currentEnemy = null;
        SetDestination(spawnPoint);
        DLog.Log($"[{DisplayName}] Quitte l'île");
    }

    /// <summary>Remonte dans le bateau : survivant qui va raconter son histoire.</summary>
    private void Depart()
    {
        if (departed || isDead) return;
        departed = true;

        GuildSystem.Instance?.NoteSurvivor(this);
        NemesisSystem.Instance?.OnSurvivor(this);
        ReputationSystem.Instance?.OnAdventurerFled(this);
        RelationSystem.Instance?.OnIntruderLeft(this);
        PlayerLevel.Instance?.AddXP(xpReward / 2);
        if (GameManager.Instance != null) GameManager.Instance.statFled++;
        myGroup?.RemoveMember(this);
        ExpeditionManager.Instance?.OnMemberGone(this, false);
        StoryManager.Instance?.OnAdventurerFled(this);
        Destroy(gameObject);
    }

    /// <summary>Appelé par AdventurerCombat à la mort.</summary>
    public void HandleDeath()
    {
        AdventurerLife.OnDied(this);
        if (IsChampion) Domination.Instance?.OnChampionSlain();
        string cause;
        if (LastBrawler != null && Time.time - LastBrawlHitAt < 6f) cause = $"tué par son compagnon {Chronicle.Who(LastBrawler)} au cours d'une bagarre";
        else if (lastMonsterHit != null && Time.time - lastMonsterHitAt < 6f) cause = $"tombé sous les coups de {lastMonsterHit.gobelinName}";
        else cause = IsInsideDungeon ? "mort dans les profondeurs du donjon" : "mort sur l'île";
        Chronicle.Add($"{Chronicle.Who(this)} est {cause}.");
        // mort dans le donjon : le corps reste... et nourrit vos monstres
        if (IsInsideDungeon) Corpse.Spawn(rb.position, 3);
        if (!isDead) AnyDied?.Invoke(this);
        if (isDead || departed) return;
        isDead = true;

        GuildSystem.Instance?.NoteDeath(this);
        NemesisSystem.Instance?.OnDeath(this);
        ManaSystem.Instance?.Add(manaReward);
        FloatingText.Spawn(transform.position + Vector3.up * 0.6f, $"+{manaReward} mana", GameHUD.ManaColor, 1.6f);
        PlayerLevel.Instance?.AddXP(xpReward);
        ReputationSystem.Instance?.OnAdventurerKilled(this);
        LootSystem.Instance?.OnIntruderKilled(this);
        RelationSystem.Instance?.OnIntruderKilled(this);
        if (GameManager.Instance != null) { GameManager.Instance.statKills++; if (TierIndex >= 4) GameManager.Instance.statHeroKills++; }
        myGroup?.RemoveMember(this);
        ExpeditionManager.Instance?.OnMemberGone(this, true);
        StoryManager.Instance?.OnAdventurerKilled(this);
    }

    // ═══════════════════════════════════════════
    // CHANCE, THÈMES, TRÉSORS
    // ═══════════════════════════════════════════

    /// <summary>
    /// Danger détecté : fuir ou tenir bon ? Jet de chance selon le type d'intrus
    /// (un pêcheur fuit presque toujours, un héros presque jamais).
    /// </summary>
    private void DecideToFlee()
    {
        float hp = combat.currentHealth / combat.maxHealth * 100f;
        float chance = Mathf.Clamp01(fleeChance - (arrogance - 0.5f) * 0.3f);
        // Thème de l'étage (ex : crypte hantée → la peur gagne)
        var theme = IsInsideDungeon && DungeonLayers.Instance != null ? DungeonLayers.Instance.ThemeAt(rb.position) : null;
        if (theme != null) chance = Mathf.Clamp01(chance + theme.intruderFleeBonus);
        if (IslandEvents.Instance != null) chance = Mathf.Clamp01(chance + IslandEvents.Instance.FleeBonus);
        chance = Mathf.Clamp01(chance + fleeBias + Talents.IntruderFleeBonus);
        // À l'agonie, même les braves hésitent... une dernière fois
        if (hp <= healthThreshold * 0.5f && !lastStand)
        {
            lastStand = true;
            chance = Mathf.Clamp01(chance + 0.25f);
        }

        if (Random.value < chance)
        {
            Voices.Human(combat, "fear");
            FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "Fuyez !", new Color(0.8f, 0.9f, 1f), 1f);
            StartRetreat();
        }
        else
        {
            // Bravoure (ou malchance) : il reste se battre un moment
            braveUntil = Time.time + Random.Range(6f, 12f);
            FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "Je ne recule pas !", new Color(1f, 0.55f, 0.35f), 1f);
        }
    }

    private void ApplyThemeEffects()
    {
        if (!IsInsideDungeon || DungeonLayers.Instance == null) return;
        var theme = DungeonLayers.Instance.ThemeAt(rb.position);
        if (theme == null || theme.intruderDamagePerSecond <= 0f) return;
        themeDamageAccum += theme.intruderDamagePerSecond * Time.fixedDeltaTime;
        if (themeDamageAccum >= 3f)
        {
            float d = themeDamageAccum;
            themeDamageAccum = 0f;
            combat.TakeDamage(d);
        }
    }

    /// <summary>Un intrus cupide repère un coffre et fait un détour pour le piller.</summary>
    private bool TryFindChest()
    {
        if (TreasureChest.All.Count == 0 || IsAssessor) return false;
        TreasureChest best = null;
        float bestD = 7f;
        foreach (var c in TreasureChest.All)
        {
            if (c == null || !c.IsFull || lootedChests.Contains(c)) continue;
            if (c.ReservedBy != null && c.ReservedBy != this) continue;
            float d = Vector2.Distance(rb.position, c.transform.position);
            // un trésor mis en scène (salle du trésor) se repère de bien plus loin
            if (ThemedRooms.InTreasury(c.transform.position)) d *= 0.55f;
            if (d < bestD && HasLineOfSight(c.transform)) { bestD = d; best = c; }
        }
        if (best == null) return false;
        lootedChests.Add(best);   // une seule tentative par coffre
        // un coffre attire presque tout le monde (les plus cupides n'hésitent jamais)
        if (Random.value > 0.6f + greed * 0.4f) return false;

        targetChest = best;
        best.ReservedBy = this;
        lootTimer = 0f;
        state = AdventurerState.Looting;
        SetDestination(ChestApproach(best));
        FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "Un trésor !", GameHUD.GoldColor, 1f);
        return true;
    }

    /// <summary>Case libre à côté du coffre, du côté de l'intrus (le coffre lui-même est encombrant).</summary>
    private Vector2 ChestApproach(TreasureChest c)
    {
        var b = DungeonBuilder.Instance;
        if (b == null) return (Vector2)c.transform.position + Vector2.down * 0.5f;
        var cell = b.floorTilemap.WorldToCell(c.transform.position);
        Vector2 best = (Vector2)c.transform.position + Vector2.down * 0.9f;
        float bd = float.MaxValue;
        foreach (var d in DungeonTerritory.Dirs)
        {
            var n = cell + d;
            if (b.stoneTilemap.HasTile(n)) continue;
            Vector2 p = b.floorTilemap.GetCellCenterWorld(n);
            if (Physics2D.OverlapCircle(p, 0.3f, 1 << 6) != null) continue;
            float dist = Vector2.Distance(p, rb.position);
            if (dist < bd) { bd = dist; best = p; }
        }
        return best;
    }

    private void UpdateLooting()
    {
        if (targetChest == null || !targetChest.IsFull)
        {
            ResumeAttack();
            return;
        }
        Transform monster = DetectNearestEnemy();
        if (monster != null)
        {
            targetChest.ReservedBy = null;
            EngageEnemy(monster);
            return;
        }
        if (Vector2.Distance(rb.position, targetChest.transform.position) > 1.1f)
        {
            MoveAlongPath(speed);
            return;
        }
        Stop();
        // il fouille : face au coffre, gestes répétés, le coffre tremble et une jauge se remplit
        Vector2 look = (Vector2)targetChest.transform.position - rb.position;
        if (look.sqrMagnitude > 0.0001f) lastMoveDirection = look.normalized;
        lootTimer += Time.fixedDeltaTime;
        attackAnimTimer = Mathf.Repeat(lootTimer, 0.6f) < 0.3f ? 0.1f : 0f;
        targetChest.ShowLooting(lootTimer / targetChest.lootDuration);
        if (lootTimer >= targetChest.lootDuration)
        {
            targetChest.Loot(this);
            targetChest = null;
            AdventurerLife.OnLooted(this);
            arrogance = Mathf.Min(1f, arrogance + 0.15f);   // la fortune rend téméraire
            ResumeAttack();
        }
    }

    // ═══════════════════════════════════════════
    // DANGER
    // ═══════════════════════════════════════════

    private bool IsInDanger()
    {
        float hp = combat.currentHealth / combat.maxHealth * 100f;
        if (hp <= healthThreshold) return true;

        int enemies = 0;
        var hits = Phys.Circle(rb.position, detectionRange * IslandEvents.Detection, enemyLayer);
        foreach (var h in hits)
        {
            IDamageable d = h.GetComponent<IDamageable>();
            if (d != null && d.IsAlive() && HasLineOfSight(h.transform)) enemies++;
        }
        if (enemies == 0) return false;

        int allies = 0;
        foreach (var a in All)
            if (a != this && a != null && !a.IsFleeing() &&
                Vector2.Distance(a.transform.position, rb.position) <= detectionRange) allies++;

        // Blessé = plus prudent
        float effective = enemies - allies * 0.5f + (hp < 60f ? 1f : 0f);
        return effective >= dangerousEnemyCount;
    }

    // ═══════════════════════════════════════════
    // COMBAT
    // ═══════════════════════════════════════════

    private void AttackTarget(Transform t)
    {
        Stop();
        Vector2 dir = ((Vector2)t.position - rb.position);
        if (dir.sqrMagnitude > 0.0001f) lastMoveDirection = dir.normalized;

        IDamageable dmg = t.GetComponent<IDamageable>();
        var theme = IsInsideDungeon && DungeonLayers.Instance != null ? DungeonLayers.Instance.ThemeAt(rb.position) : null;
        float cdMult = theme != null ? theme.intruderAttackCooldownMult : 1f;
        if (dmg != null && dmg.IsAlive() && Time.time - combat.lastAttackTime >= combat.attackCooldown * cdMult)
        {
            combat.Attack(dmg);
            attackAnimTimer = 0.4f;
        }
    }

    public void OnAttacked(Transform attacker)
    {
        var mg = attacker != null ? attacker.GetComponent<GobelinAI>() : null;
        if (mg != null) { lastMonsterHit = mg; lastMonsterHitAt = Time.time; }
        if (attacker == null || isDead || Spared) return;
        if (state == AdventurerState.Retreating || state == AdventurerState.Leaving) return;

        IDamageable d = attacker.GetComponent<IDamageable>();
        if (d == null || !d.IsAlive()) return;

        if (!IsAware) OnSurprised(attacker);
        else if (state != AdventurerState.Fighting) EngageEnemy(attacker);
        // un tireur ou la soigneuse est pris à partie : le guerrier vient s'interposer
        if (role != Role.Warrior)
        {
            var tank = PartyTank();
            if (tank != null && tank != this && Vector2.Distance(tank.transform.position, rb.position) < 7f &&
                (tank.currentEnemy == null || tank.currentEnemy == attacker || Random.value < 0.6f))
                tank.Protect(attacker);
        }
    }

    /// <summary>Le guerrier se place entre le monstre et son allié (et l'attaque).</summary>
    public void Protect(Transform attacker)
    {
        if (isDead || IsFleeing() || attacker == null) return;
        if (state == AdventurerState.Busy) EndBusy();
        if (currentEnemy != attacker)
        {
            currentEnemy = attacker;
            state = AdventurerState.Fighting;
            SetFollow(attacker);
            Speak("À moi !", new Color(1f, 0.8f, 0.45f));
        }
    }

    /// <summary>Appelé quand il prend des dégâts (piège, monstre...).</summary>
    public void OnDamaged(float amount = 0f)
    {
        if (isDead) return;
        damageTaken += amount;
        // un piège vient de frapper ici : l'expédition s'en souviendra
        if (Map != null && DungeonBuilder.Instance != null)
            foreach (var p in PlacedObject.All)
                if (p != null && p.kind == ItemKind.Trap && Vector2.Distance(p.transform.position, rb.position) < 0.9f)
                {
                    if (Map.Traps.Add(DungeonBuilder.Instance.floorTilemap.WorldToCell(p.transform.position))) Map.Version++;
                    break;
                }
        Life?.OnHurt(amount);
        if (!IsAware && state != AdventurerState.Leaving && state != AdventurerState.Retreating)
        {
            IsAware = true;
            if (Random.value < fleeChance) StartRetreat();
        }
    }

    private void Heal(float amount)
    {
        if (combat.currentHealth < combat.maxHealth)
            combat.currentHealth = Mathf.Min(combat.maxHealth, combat.currentHealth + amount);
    }

    public bool IsFleeing() =>
        state == AdventurerState.Retreating || state == AdventurerState.Healing || state == AdventurerState.Leaving;

    // ═══════════════════════════════════════════
    // GROUPES
    // ═══════════════════════════════════════════

    public void SetGroup(AdventurerGroup group) => myGroup = group;

    public void OnGroupReady()
    {
        if (state == AdventurerState.WaitingGroup)
        {
            DLog.Log($"[{DisplayName}] Groupe prêt ! ATTAQUE");
            ResumeAttack();
        }
    }

    public AdventurerGroup GetGroup() => myGroup;

    public bool CanJoinGroup() =>
        !IsInsideDungeon && hasAttemptedDungeon && hasRetreated && sociability > 0.3f && myGroup == null;

    public float GetSociability() => sociability;
    public bool HasAttemptedDungeon() => hasAttemptedDungeon;
    public bool HasRetreated() => hasRetreated;

    // ═══════════════════════════════════════════
    // DÉTECTION
    // ═══════════════════════════════════════════

    private Transform DetectNearestEnemy()
    {
        float sight = detectionRange * IslandEvents.Detection * (TorchWet && IsInsideDungeon ? 0.75f : 1f) * (IsInsideDungeon ? Talents.IntruderSightMult : 1f) * (Time.time < BlindUntil ? 0.5f : 1f);
        var hits = Phys.Circle(rb.position, sight, enemyLayer);
        float closestDist = Mathf.Infinity;
        Transform closest = null;

        foreach (var hit in hits)
        {
            if (!IsInFieldOfView(hit.transform)) continue;
            if (!HasLineOfSight(hit.transform)) continue;
            IDamageable d = hit.GetComponent<IDamageable>();
            if (d == null || !d.IsAlive()) continue;

            float dist = Vector2.Distance(rb.position, hit.transform.position);
            // tapi dans l'ombre : invisible au-delà de deux pas (le chanceux a parfois l'œil)
            if (d is GobelinAI g && g.IsAmbushing && dist > 1.6f && !(Life != null && Life.Has(AdventurerLife.Trait.Lucky) && dist < 3f)) continue;
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = hit.transform;
            }
        }
        return closest;
    }

    private bool CanSeeCore()
    {
        return Vector2.Distance(rb.position, dungeonCoreTarget.position) <= coreDiscoveryRange &&
               HasLineOfSight(dungeonCoreTarget);
    }

    private bool IsInFieldOfView(Transform t)
    {
        Vector2 dir = ((Vector2)t.position - rb.position).normalized;
        return Vector2.Angle(lastMoveDirection, dir) <= fieldOfViewAngle / 2f;
    }

    /// <summary>
    /// Ligne de vue « épaisse » : un rayon de 0,1 case. Un rayon infiniment fin se faufilait
    /// entre deux blocs de mur posés en diagonale et laissait tirer à travers la roche.
    /// </summary>
    private bool HasLineOfSight(Transform t) => ClearLine(rb.position, t.position, obstacleLayer);

    /// <summary>Ligne de vue : les murs, et les portes tant qu'elles sont fermées.</summary>
    public static bool ClearLine(Vector2 from, Vector2 to, LayerMask walls) => ClearLine(from, to, walls, true);

    public static bool ClearLine(Vector2 from, Vector2 to, LayerMask walls, bool doors)
    {
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < 0.05f) return true;
        int mask = walls | (doors ? DungeonDoor.LosMask : 0);
        return Physics2D.CircleCast(from, 0.1f, d / len, len, mask).collider == null;
    }

    // ═══════════════════════════════════════════
    // PATHFINDING & MOUVEMENT
    // ═══════════════════════════════════════════

    private void SetFollow(Transform t)
    {
        exploring = false;
        if (followTarget == t && hasDestination) return;
        followTarget = t;
        hasDestination = t != null;
        RequestPathNow();
    }

    private void SetDestination(Vector2 d)
    {
        exploring = false;
        followTarget = null;
        destination = d;
        hasDestination = true;
        RequestPathNow();
    }

    private Vector2 CurrentDestination => followTarget != null ? (Vector2)followTarget.position : destination;

    private void RequestPathNow()
    {
        if (!hasDestination || seeker == null) return;
        seeker.CancelCurrentPathRequest();
        lastPathDest = CurrentDestination;
        lastPathTime = Time.time;
        seeker.StartPath(rb.position, lastPathDest, OnPathComplete);
    }

    private void RepathTick()
    {
        if (!hasDestination || isDead || !seeker.IsDone()) return;
        Vector2 d = CurrentDestination;
        if (path == null || Vector2.Distance(d, lastPathDest) > 0.75f || Time.time - lastPathTime > 3f)
        {
            lastPathDest = d;
            lastPathTime = Time.time;
            seeker.StartPath(rb.position, d, OnPathComplete);
        }
    }

    private void OnPathComplete(Path p)
    {
        if (p.error) return;
        path = p;
        currWp = FirstWaypoint(p, rb.position);
    }

    /// <summary>
    /// Premier point à viser d'un nouveau chemin : le point 0 est le centre de la case où l'on se trouve déjà.
    /// Y retourner à chaque recalcul faisait piétiner les intrus (aller-retour contre les murs dans les virages).
    /// </summary>
    public static int FirstWaypoint(Path p, Vector2 pos)
    {
        var v = p.vectorPath;
        if (v == null || v.Count < 2) return 0;
        // pas de saut d'escalier (portail : points très éloignés)
        if (Vector2.Distance(v[0], v[1]) > 1.5f) return 0;
        // on est déjà dans la case de départ : on file directement vers la suivante
        return Mathf.Abs(pos.x - v[0].x) <= 0.5f && Mathf.Abs(pos.y - v[0].y) <= 0.5f ? 1 : 0;
    }

    /// <summary>
    /// Désactive les diagonales qui rasent les angles de mur (cutCorners) : dans un couloir d'une case,
    /// elles faisaient buter les personnages contre l'arête du mur à chaque virage.
    /// </summary>
    public static void FixGraphCorners()
    {
        if (AstarPath.active == null || AstarPath.active.data.graphs == null) return;
        bool rescan = false;
        foreach (var g in AstarPath.active.data.graphs)
            if (g is GridGraph gg && gg.cutCorners) { gg.cutCorners = false; rescan = true; }
        if (rescan) AstarPath.active.Scan();
    }

    private bool ReachedPathEnd() => path != null && currWp >= path.vectorPath.Count;

    // Portes et pièges : immobilisation / ralentissement
    private float holdUntil, slowUntil, slowMult = 1f;
    public bool IsHeld => Time.time < holdUntil;

    /// <summary>Immobilise l'intrus (porte à forcer, piège à loup...).</summary>
    public void Hold(float seconds) => holdUntil = Mathf.Max(holdUntil, Time.time + seconds);

    /// <summary>Ralentit l'intrus (gaz toxique...).</summary>
    public void Slow(float mult, float seconds)
    {
        slowMult = Time.time < slowUntil ? Mathf.Min(slowMult, mult) : mult;
        slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
    }

    private void MoveAlongPath(float moveSpeed)
    {
        if (IsHeld) { Stop(); return; }
        if (Time.time < slowUntil) moveSpeed *= slowMult;
        moveSpeed *= moodSpeedMult;
        if (path == null || path.vectorPath == null || currWp >= path.vectorPath.Count)
        {
            Stop();
            return;
        }

        // Sauter les points déjà atteints (et emprunter les escaliers)
        // Un point est validé de près seulement : avec une grande distance (1 case sur les prefabs),
        // l'intrus visait deux cases plus loin et coupait chaque virage... droit dans l'arête du mur.
        float reach = Mathf.Min(nextWpDistance, MaxWaypointReach);
        while (currWp < path.vectorPath.Count - 1 &&
               Vector2.Distance(rb.position, path.vectorPath[currWp]) < reach)
        {
            if (PathPortal.TryTeleport(rb, path, currWp)) currWp += 2;
            else currWp++;
            if (currWp >= path.vectorPath.Count) { Stop(); return; }
        }

        Vector2 wp = path.vectorPath[currWp];
        if (currWp == path.vectorPath.Count - 1 && Vector2.Distance(rb.position, wp) < 0.15f)
        {
            currWp++;
            Stop();
            return;
        }

        Vector2 dir = (wp - rb.position).normalized;
        var theme = IsInsideDungeon && DungeonLayers.Instance != null ? DungeonLayers.Instance.ThemeAt(rb.position) : null;
        if (theme != null) moveSpeed *= theme.intruderSpeedMult;
        movement = dir * moveSpeed;
        rb.linearVelocity = movement;
        if (movement.sqrMagnitude > 0.01f) lastMoveDirection = movement.normalized;
    }

    private void Stop()
    {
        rb.linearVelocity = Vector2.zero;
        movement = Vector2.zero;
    }

    private void PickWanderPoint()
    {
        nextWanderPick = Time.time + Random.Range(4f, 8f);
        for (int i = 0; i < 12; i++)
        {
            Vector2 candidate = spawnPoint + Random.insideUnitCircle * wanderRadius;
            if (DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(candidate)) continue;
            if (IsWalkable(candidate))
            {
                SetDestination(candidate);
                return;
            }
        }
        SetDestination(spawnPoint);
    }

    private static bool IsWalkable(Vector2 pos)
    {
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph == null) return true;
        var nn = graph.GetNearest(pos);
        return nn.node != null && nn.node.Walkable && Vector2.Distance((Vector3)nn.node.position, pos) < 0.8f;
    }

    // ═══════════════════════════════════════════
    // ANIMATOR
    // ═══════════════════════════════════════════

    // ═══════════════════════════════════════════
    // GROUPE : FORMATION ET TACTIQUE
    // ═══════════════════════════════════════════

    /// <summary>Compagnons d'expédition (même carte partagée), vivants et pas en fuite.</summary>
    public void Party(List<AdventurerAI> into)
    {
        into.Clear();
        if (Map == null) return;
        foreach (var a in All)
            if (a != null && a != this && !a.isDead && a.Map == Map && !a.IsFleeing() && !a.Spared) into.Add(a);
    }

    private static readonly List<AdventurerAI> partyBuf = new List<AdventurerAI>();

    /// <summary>Le guerrier de tête du groupe (le plus robuste), ou null.</summary>
    public AdventurerAI PartyTank()
    {
        if (role == Role.Warrior && !IsFleeing()) return this;
        Party(partyBuf);
        AdventurerAI best = null;
        float bestHp = 0f;
        foreach (var a in partyBuf)
            if (a.role == Role.Warrior && a.combat != null && a.combat.currentHealth > bestHp &&
                Vector2.Distance(a.transform.position, rb.position) < 12f) { bestHp = a.combat.currentHealth; best = a; }
        return best;
    }

    private float nextSlotUpdate;

    /// <summary>
    /// Formation : le guerrier ouvre la marche, les tireurs suivent à deux pas derrière lui (l'archer à gauche,
    /// le mage à droite), la soigneuse au centre, un peu plus loin.
    /// </summary>
    private bool FollowFormation()
    {
        if (role == Role.Warrior || !IsInsideDungeon || !IsAware || IsAssessor) return false;
        var tank = PartyTank();
        if (tank == null || tank == this || tank.state != AdventurerState.Advancing) return false;
        float distToCore = Vector2.Distance(rb.position, dungeonCoreTarget.position);
        if (distToCore <= combat.attackRange + 0.5f) return false;   // le cristal est à portée : on tire
        Vector2 h = tank.Heading.sqrMagnitude > 0.01f ? tank.Heading.normalized : Vector2.up;
        Vector2 side = new Vector2(-h.y, h.x);
        float back = role == Role.Healer ? 2.4f : 1.6f;
        float lateral = role == Role.Archer ? 0.7f : role == Role.Mage ? -0.7f : 0f;
        Vector2 slot = (Vector2)tank.transform.position - h * back + side * lateral;
        if (!ClearLine(tank.transform.position, slot, obstacleLayer)) slot = (Vector2)tank.transform.position - h * 1.1f;
        float d = Vector2.Distance(rb.position, slot);
        if (d < 0.45f) { Stop(); lastMoveDirection = h; return true; }
        if (Time.time >= nextSlotUpdate || path == null) { nextSlotUpdate = Time.time + 0.5f; SetDestination(slot); }
        MoveAlongPath(speed * (d > 3f ? 1.15f : 1f));
        return true;
    }

    /// <summary>Tireur au contact : il recule (en gardant la ligne de vue), puis reprend le tir.</summary>
    private bool Kite()
    {
        Vector2 away = (rb.position - (Vector2)currentEnemy.position).normalized;
        Vector2 to = rb.position + away * 1.2f;
        if (!ClearLine(rb.position, to, obstacleLayer))
        {
            // mur derrière : on essaie en biais
            Vector2 alt = rb.position + (away + new Vector2(-away.y, away.x)).normalized * 1.2f;
            if (!ClearLine(rb.position, alt, obstacleLayer)) return false;
            to = alt;
        }
        movement = (to - rb.position).normalized * speed * moodSpeedMult;
        rb.linearVelocity = movement;
        lastMoveDirection = -away;    // il recule en faisant face
        return true;
    }

    /// <summary>Blessé (et pas le guerrier) : se replie derrière le guerrier qui tient la ligne.</summary>
    private bool FallBackBehindTank(float distToEnemy)
    {
        if (role == Role.Warrior || HealthPct > 0.35f || distToEnemy > 3f) return false;
        var tank = PartyTank();
        if (tank == null || tank == this || !tank.IsFighting || tank.currentEnemy == null) return false;
        Vector2 fromEnemy = ((Vector2)tank.transform.position - (Vector2)tank.currentEnemy.position).normalized;
        Vector2 spot = (Vector2)tank.transform.position + fromEnemy * 1.8f;
        if (Vector2.Distance(rb.position, spot) < 0.5f) return false;   // à l'abri : il peut tirer / soigner
        if (Time.time >= nextSlotUpdate || path == null) { nextSlotUpdate = Time.time + 0.4f; SetDestination(spot); }
        MoveAlongPath(speed * 1.1f);
        return true;
    }

    // ═══════════════════════════════════════════
    // API POUR LA VIE DE L'INTRUS (AdventurerLife)
    // ═══════════════════════════════════════════

    public bool CanBeBusy => !isDead && !Spared && !IsPrisoner && !departed &&
        (state == AdventurerState.Advancing || state == AdventurerState.Wandering || state == AdventurerState.Healing || state == AdventurerState.WaitingGroup);
    public bool IsBusy => state == AdventurerState.Busy;

    public bool BeginBusy()
    {
        if (!CanBeBusy) return false;
        currentEnemy = null;
        state = AdventurerState.Busy;
        path = null;
        Stop();
        return true;
    }

    public void EndBusy()
    {
        if (state != AdventurerState.Busy) return;
        Stop();
        if (IsAware || IsInsideDungeon) ResumeAttack();
        else { state = AdventurerState.Wandering; PickWanderPoint(); }
    }

    public void LifeGoTo(Vector2 p) => SetDestination(p);
    public void LifeFollow(Transform t) => SetFollow(t);
    public void LifeMove(float mult = 1f) => MoveAlongPath(speed * mult);
    public void LifeStop() => Stop();
    public bool LifeArrived => ReachedPathEnd();
    public void LifeFace(Vector2 p) { Vector2 d = p - rb.position; if (d.sqrMagnitude > 0.0001f) lastMoveDirection = d.normalized; }
    public void LifeAttack(Transform t) => AttackTarget(t);
    public void LifeGesture(float seconds) => attackAnimTimer = seconds;
    public void LifeLeave() => LeaveIsland();
    public void LifePanic() => StartRetreat();
    public bool LifeSees(Transform t) => t != null && HasLineOfSight(t);
    public Transform LifeNearestMonster() => DetectNearestEnemy();
    public void Embolden(float seconds) => braveUntil = Mathf.Max(braveUntil, Time.time + seconds);
    public void LifeEngage(Transform monster) { if (state == AdventurerState.Busy) state = AdventurerState.Advancing; EngageEnemy(monster); }

    private float nextSpeak;
    /// <summary>Petite phrase au-dessus de la tête (limitée pour ne pas inonder l'écran).</summary>
    public void Speak(string text, Color c, bool force = false)
    {
        if (!force && Time.time < nextSpeak) return;
        nextSpeak = Time.time + 2.5f;
        SpeechBubble.Show(transform, text, c);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float moveSpeed = movement.sqrMagnitude;
        animator.SetFloat("Speed", moveSpeed);

        Vector2 facing = moveSpeed > 0.01f ? movement : lastMoveDirection;
        animator.SetFloat("MoveX", facing.x);
        animator.SetFloat("MoveY", facing.y);
        animator.SetBool("IsAttacking", attackAnimTimer > 0f);
    }

    // ═══════════════════════════════════════════
    // GIZMOS
    // ═══════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (currentEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentEnemy.position);
        }
        if (Application.isPlaying && hasDestination)
        {
            Gizmos.color = IsFleeing() ? Color.green : Color.cyan;
            Gizmos.DrawLine(transform.position, CurrentDestination);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f,
            $"{state}{(IsAware ? "" : " (non averti)")}\nReplis {retreatCount}/{maxRetreats + 1}");
#endif
    }
}
