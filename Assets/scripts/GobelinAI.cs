using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

/// <summary>
/// IA des gobelins (monstres du donjon).
/// - Comportements donnés par le joueur : Patrouille / Tenir la position / Déplacement.
/// - Missions automatiques : chasser un intrus, défendre le cœur, aider un allié.
/// - Les monstres ne peuvent PAS quitter le territoire du donjon.
/// - Peut être possédé par le Noyau (mode exploration / combat direct).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Seeker))]
public class GobelinAI : MonoBehaviour, IDamageable
{
    /// <summary>Tous les gobelins vivants.</summary>
    public static readonly List<GobelinAI> All = new List<GobelinAI>();

    public enum GoblinBehavior
    {
        Patrol,   // Patrouille autour de l'ancre (défaut)
        Stay,     // Tient la position
        MoveTo    // Se déplace vers une position cible
    }

    [HideInInspector] public GoblinBehavior currentBehavior = GoblinBehavior.Patrol;

    private Vector2 moveToTarget;
    private bool returnToStay;

    private bool isOnMission = false;

    private enum MissionType { None, ChaseEnemy, DefendCore, HelpAlly }
    private MissionType currentMission = MissionType.None;

    private enum CombatState { Approaching, Attacking, Pursuing }
    private CombatState combatState = CombatState.Approaching;

    [Header("Anchor & Patrolling")]
    public Transform anchorPoint;
    public float patrolRadius = 3f;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float nextWaypointDistance = 0.5f;

    [Header("Vision & Detection")]
    public float fieldOfViewAngle = 120f;
    public float detectionRange = 5f;
    public LayerMask adventurerLayer;
    [Tooltip("Couches qui bloquent la vue (murs). Vide = couche 6")]
    public LayerMask obstacleLayer;

    [Header("Group Alert")]
    public float alertRadius = 6f;
    public LayerMask goblinLayer;

    [Header("Combat")]
    public float attackDamage = 10f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    private float lastAttackTime = -999f;

    [Header("Pursuit")]
    public float pursuitDuration = 5f;
    private float pursuitTimer = 0f;

    [Header("Stats")]
    public string gobelinName = "Gobelin";
    public float maxHealth = 80f;
    public float currentHealth = 80f;

    [Header("Healing")]
    [Tooltip("PV régénérés par seconde DANS le donjon (hors combat)")]
    public float healingPerSecond = 2f;

    [Tooltip("Transform du cœur du donjon (auto si vide)")]
    public Transform dungeonCore;

    [Header("Possession")]
    [Tooltip("Multiplicateur de dégâts quand le Noyau possède ce monstre")]
    public float possessedDamageMult = 1.5f;
    [Tooltip("Multiplicateur de vitesse quand possédé")]
    public float possessedSpeedMult = 2.2f;

    [Header("Components")]
    public Animator animator;

    public bool IsPossessed { get; private set; }
    public bool IsOnMission => isOnMission;

    // Composants
    private Rigidbody2D rb;
    private Seeker seeker;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private Path path;
    private int currentWaypoint = 0;
    private Vector2 movement;
    private Vector2 lastMoveDirection = Vector2.down;
    private Vector2 lastPathTarget = Vector2.positiveInfinity;

    private Vector2 currentPatrolTarget;
    private Vector2 savedAnchorPosition;

    private Transform missionTarget;
    /// <summary>Celui qu'il poursuit (null : aucun).</summary>
    public Transform MissionTarget => missionTarget;
    private Vector2 missionDestination;
    private float attackAnimTimer;
    private float outOfCombatTimer;
    private bool isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        seeker = GetComponent<Seeker>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        if (animator == null) animator = GetComponent<Animator>();
        currentHealth = maxHealth;
        if (GetComponent<MonsterVariant>() == null) gameObject.AddComponent<MonsterVariant>();
    }

    /// <summary>L'ennemi poursuivi en ce moment (pouvoirs à distance des variantes), ou null.</summary>
    public Transform CombatTarget => isOnMission && currentMission == MissionType.ChaseEnemy ? missionTarget : null;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        if (GetComponent<MonsterMind>() == null && MonsterMind.IsIntelligent(GetComponent<MonsterUnit>())) gameObject.AddComponent<MonsterMind>();
        // la vie du monstre (besoins, humeur, liens) — pas pour les invocations ni dans le mode Aventurier
        var mu = GetComponent<MonsterUnit>();
        if (!CrawlerMode.Active && mu != null && !mu.summoned) life = GetComponent<MonsterLife>() ?? gameObject.AddComponent<MonsterLife>();
        if (!CrawlerMode.Active && Talents.MonsterHpMult > 1f) { maxHealth *= Talents.MonsterHpMult; currentHealth *= Talents.MonsterHpMult; }
        if (dungeonCore == null)
        {
            var core = FindFirstObjectByType<DungeonCore>();
            if (core != null) dungeonCore = core.transform;
        }
        if (obstacleLayer.value == 0) obstacleLayer = 1 << 6;

        EnsureAnchor();
        savedAnchorPosition = anchorPoint.position;

        HealthBar.Attach(transform, () => currentHealth / maxHealth, new Color(0.35f, 0.85f, 0.3f));
        SetNewPatrolTarget();
        InvokeRepeating(nameof(UpdatePath), Random.Range(0f, 0.5f), 0.5f);
    }

    /// <summary>
    /// Tout gobelin doit avoir une ancre avec PatrolAnchor (pour la sélection / le rattachement).
    /// </summary>
    private void EnsureAnchor()
    {
        if (anchorPoint == null || anchorPoint == transform)
        {
            var go = new GameObject($"Anchor_{gobelinName}");
            go.transform.position = transform.position;
            var parent = GameObject.Find("PatrolAnchors");
            if (parent == null) parent = new GameObject("PatrolAnchors");
            go.transform.SetParent(parent.transform);
            anchorPoint = go.transform;
        }
        var pa = anchorPoint.GetComponent<PatrolAnchor>();
        if (pa == null) pa = anchorPoint.gameObject.AddComponent<PatrolAnchor>();
        pa.RegisterGoblin(this);
    }

    // ═══════════════════════════════════════════
    // API PUBLIQUE : CONTRÔLE JOUEUR
    // ═══════════════════════════════════════════

    public void SetBehavior(GoblinBehavior behavior)
    {
        currentBehavior = behavior;

        if (isOnMission)
        {
            isOnMission = false;
            currentMission = MissionType.None;
            missionTarget = null;
        }

        // tenir la position : le monstre abandonne repas et querelles en cours
        if (behavior == GoblinBehavior.Stay && life != null && !life.Sleeping) life.Interrupt();

        switch (behavior)
        {
            case GoblinBehavior.Stay:
                StopMoving();
                savedAnchorPosition = rb.position;
                break;

            case GoblinBehavior.Patrol:
                SetNewPatrolTarget();
                break;
        }
    }

    /// <summary>Se déplace vers une position, puis patrouille autour.</summary>
    public void MoveToPosition(Vector2 worldPosition)
    {
        returnToStay = false;
        if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInside(worldPosition))
            return;   // hors du donjon : impossible

        currentBehavior = GoblinBehavior.MoveTo;
        moveToTarget = worldPosition;

        if (isOnMission)
        {
            isOnMission = false;
            currentMission = MissionType.None;
            missionTarget = null;
        }

        ForceRecalculatePath(moveToTarget);
    }

    // ═══════════════════════════════════════════
    // UPDATE PRINCIPAL
    // ═══════════════════════════════════════════

    // ═══════════════════════════════════════════
    // RAID NOCTURNE (hors du donjon, vers la colonie)
    // ═══════════════════════════════════════════

    private class RaidOrder
    {
        public Vector2 target;
        public System.Action<GobelinAI> onArrive;
        public bool arrived, returning;
        public float nextRepath;
    }
    private RaidOrder raid;

    public bool IsRaiding => raid != null;
    public bool RaidArrived => raid != null && raid.arrived;

    public void StartRaid(Vector2 target, System.Action<GobelinAI> onArrive)
    {
        if (isDead || IsPossessed) return;
        isOnMission = false;
        currentMission = MissionType.None;
        missionTarget = null;
        raid = new RaidOrder { target = target, onArrive = onArrive };
        ForceRecalculatePath(target);
        FloatingText.Spawn(transform.position + Vector3.up * 0.7f, "Raid !", new Color(1f, 0.45f, 0.35f), 1f);
    }

    /// <summary>Fin du raid : retour à l'ancre dans le donjon.</summary>
    public void ReturnFromRaid()
    {
        if (raid == null) return;
        raid.returning = true;
        raid.arrived = false;
        Vector2 home = anchorPoint != null ? (Vector2)anchorPoint.position : savedAnchorPosition;
        ForceRecalculatePath(home);
    }

    private void UpdateRaid()
    {
        Vector2 dest = raid.returning ? (anchorPoint != null ? (Vector2)anchorPoint.position : savedAnchorPosition) : raid.target;
        if (Time.time > raid.nextRepath && seeker.IsDone())
        {
            raid.nextRepath = Time.time + 2.5f;
            lastPathTarget = dest;
            seeker.StartPath(rb.position, dest, OnPathComplete);
        }
        float d = Vector2.Distance(rb.position, dest);
        if (!raid.returning)
        {
            if (!raid.arrived && d < 1.8f)
            {
                raid.arrived = true;
                raid.onArrive?.Invoke(this);
            }
            if (raid.arrived) { StopMoving(); FaceTowards(raid.target); return; }
            FollowPath(RaidSpeed);
            return;
        }
        if (d < 1.2f && IsInsideDungeon())
        {
            raid = null;
            SetBehavior(GoblinBehavior.Patrol);
            return;
        }
        FollowPath(RaidSpeed);
    }

    /// <summary>Course nocturne : les monstres traversent l'île au pas de charge.</summary>
    public const float RaidSpeedMult = 2.2f;
    public float RaidSpeed => Mathf.Max(chaseSpeed * RaidSpeedMult, 4.5f);

    /// <summary>Petit coup porté hors du donjon (raid) : animation d'attaque.</summary>
    public void RaidStrike(Vector2 at)
    {
        FaceTowards(at);
        attackAnimTimer = 0.4f;
    }

    private float nextWallCheck;

    /// <summary>Pris dans un mur ou sous un objet encombrant (apparition, pose d'un coffre...) : on le dégage.</summary>
    private void UnstuckFromWalls()
    {
        if (Time.time < nextWallCheck) return;
        nextWallCheck = Time.time + 0.5f;
        if (!DungeonTerritory.InsideObstacle(rb.position)) return;
        Vector2 free = DungeonTerritory.FreeSpotNear(rb.position, raid == null);
        rb.position = free;
        transform.position = free;
        path = null;
    }

    /// <summary>Peut franchir une porte fermée : seulement sur ordre du Noyau (déplacement commandé, possession, raid).</summary>
    public bool MayCrossDoors => IsPossessed || currentBehavior == GoblinBehavior.MoveTo || raid != null;
    private int doorAccess = -1;

    private void SyncDoorAccess()
    {
        int want = MayCrossDoors ? 1 : 0;
        if (want == doorAccess) return;
        doorAccess = want;
        if (seeker != null) seeker.traversableTags = want == 1 ? ~0 : ~(1 << DungeonDoor.DoorTag);
        DungeonDoor.SetPassable(this, want == 1);
        path = null;   // nouveau chemin avec (ou sans) les portes
    }

    private void FixedUpdate()
    {
        UpdateAmbush();
        if (isDead) return;
        if (attackAnimTimer > 0f) attackAnimTimer -= Time.fixedDeltaTime;
        UnstuckFromWalls();
        SyncDoorAccess();

        if (IsPossessed)
        {
            CheckStairs();
            UpdateAnimator();
            return;   // contrôlé par PossessionController
        }

        if (raid != null)
        {
            UpdateRaid();
            CheckStuck();
            UpdateAnimator();
            return;
        }

        // Régénération passive dans le donjon, hors combat
        outOfCombatTimer += Time.fixedDeltaTime;
        if (IsInsideDungeon() && currentHealth < maxHealth && !isOnMission && outOfCombatTimer > 4f)
            currentHealth = Mathf.Min(maxHealth, currentHealth + healingPerSecond * ThemedRooms.RegenMult(rb.position, Family) * Time.fixedDeltaTime);

        // vie du monstre : manger, dormir, se quereller, déserter... quand il n'a ni ordre ni combat
        MonsterSociety.Tick();
        if (life != null)
        {
            if (!IdleForLife) life.Interrupt();
            else if (life.Tick()) { CheckStuck(); UpdateAnimator(); return; }
        }

        if (isOnMission) UpdateMission();
        else UpdateBehavior();

        CheckStuck();
        UpdateAnimator();
    }

    private void UpdateBehavior()
    {
        switch (currentBehavior)
        {
            case GoblinBehavior.Patrol:
                UpdatePatrol();
                break;

            case GoblinBehavior.Stay:
                StopMoving();
                Transform detected = DetectAdventurer();
                if (detected != null)
                {
                    // Tient la position : attaque seulement à portée
                    if (Vector2.Distance(rb.position, detected.position) <= attackRange && HasLineOfSight(detected))
                        StartMission(MissionType.ChaseEnemy, detected);
                    else
                        FaceTowards(detected.position);
                }
                break;

            case GoblinBehavior.MoveTo:
                UpdateMoveTo();
                break;
        }
    }

    private void UpdatePatrol()
    {
        Transform detected = DetectAdventurer();
        if (detected != null)
        {
            StartMission(MissionType.ChaseEnemy, detected);
            return;
        }

        if (Vector2.Distance(rb.position, currentPatrolTarget) < 0.5f || (path != null && currentWaypoint >= path.vectorPath.Count))
            SetNewPatrolTarget();

        FollowPath(patrolSpeed);
    }

    private void UpdateMoveTo()
    {
        if (Vector2.Distance(rb.position, moveToTarget) < 0.4f || (path != null && currentWaypoint >= path.vectorPath.Count))
        {
            StopMoving();
            savedAnchorPosition = anchorPoint != null ? (Vector2)anchorPoint.position : moveToTarget;
            if (returnToStay)
            {
                returnToStay = false;
                SetBehavior(GoblinBehavior.Stay);
            }
            else SetBehavior(GoblinBehavior.Patrol);
            return;
        }

        Transform detected = DetectAdventurer();
        if (detected != null)
        {
            StartMission(MissionType.ChaseEnemy, detected);
            return;
        }

        FollowPath(chaseSpeed);
    }

    // ═══════════════════════════════════════════
    // MISSIONS
    // ═══════════════════════════════════════════

    private void UpdateMission()
    {
        switch (currentMission)
        {
            case MissionType.ChaseEnemy: UpdateChaseEnemy(); break;
            case MissionType.DefendCore: UpdateDefendCore(); break;
            case MissionType.HelpAlly: UpdateHelpAlly(); break;
        }
    }

    private void UpdateChaseEnemy()
    {
        IDamageable targetDmg = missionTarget != null ? missionTarget.GetComponent<IDamageable>() : null;
        if (targetDmg == null || !targetDmg.IsAlive())
        {
            EndMission();
            return;
        }
        var sparedAdv = missionTarget.GetComponent<AdventurerAI>();
        if (sparedAdv != null && sparedAdv.Spared)
        {
            EndMission();
            return;
        }

        // Les monstres ne quittent pas le donjon
        if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInside(missionTarget.position))
        {
            EndMission();
            return;
        }

        outOfCombatTimer = 0f;
        float dist = Vector2.Distance(rb.position, missionTarget.position);
        // une charge ou un bond en cours va jusqu'au bout
        if (tacticPhase > 0) { StepTactic(dist); return; }
        // presque mort : il file chercher de l'aide (une fois)
        if (UpdateFleeToAllies()) return;
        // tireurs (diablotins, liches) : ils reculent si on les colle
        if (attackRange >= 2.5f && dist < 1.8f && KiteFrom(missionTarget)) { if (CanAttack() && dist <= attackRange) Attack(); return; }

        switch (combatState)
        {
            case CombatState.Approaching:
                if (dist <= attackRange && HasLineOfSight(missionTarget))
                {
                    combatState = CombatState.Attacking;
                }
                else if (dist <= detectionRange * 1.5f && HasLineOfSight(missionTarget))
                {
                    if (!TryStartTactic(dist))
                    {
                        if (!PackSlot(dist)) FollowPath(chaseSpeed);
                        Flank(dist);
                    }
                }
                else
                {
                    StartPursuit();
                }
                break;

            case CombatState.Attacking:
                if (dist > attackRange || !HasLineOfSight(missionTarget))
                {
                    combatState = CombatState.Approaching;
                }
                else
                {
                    StopMoving();
                    FaceTowards(missionTarget.position);
                    if (CanAttack()) Attack();

                    AdventurerAI advAI = missionTarget.GetComponent<AdventurerAI>();
                    if (advAI != null && advAI.IsFleeing())
                        StartPursuit();
                }
                break;

            case CombatState.Pursuing:
                pursuitTimer -= Time.fixedDeltaTime;
                if (pursuitTimer <= 0f)
                {
                    EndMission();
                    return;
                }
                if (dist <= attackRange && HasLineOfSight(missionTarget))
                {
                    combatState = CombatState.Attacking;
                    return;
                }
                if (HasLineOfSight(missionTarget) && dist <= detectionRange)
                    combatState = CombatState.Approaching;

                FollowPath(chaseSpeed);
                break;
        }
    }

    private void UpdateDefendCore()
    {
        if (missionTarget == null)
        {
            EndMission();
            return;
        }

        Transform detected = DetectAdventurer();
        if (detected != null)
        {
            StartMission(MissionType.ChaseEnemy, detected);
            return;
        }

        if (Vector2.Distance(rb.position, missionTarget.position) < 2f)
        {
            EndMission();
            return;
        }

        FollowPath(chaseSpeed);
    }

    private void UpdateHelpAlly()
    {
        Transform detected = DetectAdventurer();
        if (detected != null)
        {
            StartMission(MissionType.ChaseEnemy, detected);
            return;
        }

        if (Vector2.Distance(rb.position, missionDestination) < 1.5f)
        {
            EndMission();
            return;
        }

        FollowPath(chaseSpeed);
    }

    private void StartMission(MissionType mission, Transform target = null)
    {
        if (mission == MissionType.ChaseEnemy && (currentMission != MissionType.ChaseEnemy || !isOnMission)) Voices.Monster(this, "aggro", 0.6f);
        isOnMission = true;
        currentMission = mission;
        missionTarget = target;
        combatState = CombatState.Approaching;
        pursuitTimer = 0f;

        DLog.Log($"[{gobelinName}] MISSION : {mission}");

        if (mission == MissionType.ChaseEnemy)
            AlertNearbyGoblins();

        if (target != null)
            ForceRecalculatePath(target.position);
        else if (mission == MissionType.HelpAlly)
            ForceRecalculatePath(missionDestination);
    }

    private void EndMission()
    {
        if (tacticPhase > 0) EndTactic();
        fledOnce = false;
        fleeTo = null;
        isOnMission = false;
        currentMission = MissionType.None;
        missionTarget = null;
        combatState = CombatState.Approaching;
        pursuitTimer = 0f;

        switch (currentBehavior)
        {
            case GoblinBehavior.Patrol:
                anchorPoint.position = savedAnchorPosition;
                SetNewPatrolTarget();
                break;
            case GoblinBehavior.Stay:
                ForceRecalculatePath(savedAnchorPosition);
                currentBehavior = GoblinBehavior.MoveTo;
                moveToTarget = savedAnchorPosition;
                returnToStay = true;
                break;
            case GoblinBehavior.MoveTo:
                ForceRecalculatePath(moveToTarget);
                break;
        }
    }

    private void StartPursuit()
    {
        pursuitTimer = pursuitDuration;
        combatState = CombatState.Pursuing;
    }

    // ═══════════════════════════════════════════
    // APPELS EXTERNES
    // ═══════════════════════════════════════════

    public void ReceiveAlert(Vector2 allyPosition)
    {
        if (isOnMission || IsPossessed || currentBehavior == GoblinBehavior.Stay) return;
        missionDestination = allyPosition;
        StartMission(MissionType.HelpAlly);
    }

    public void DefendCore(Transform core)
    {
        if (IsPossessed || currentMission == MissionType.ChaseEnemy || currentBehavior == GoblinBehavior.Stay) return;
        StartMission(MissionType.DefendCore, core);
    }

    /// <summary>Un aventurier vient de le frapper : riposte.</summary>
    public void OnAttackedBy(Transform attacker)
    {
        outOfCombatTimer = 0f;
        GetComponent<MonsterVariant>()?.OnAttackedBy(attacker);
        if (IsPossessed || isDead || attacker == null) return;
        if (ambushing) { ambushing = false; RevealAmbush(); }
        if (currentMission == MissionType.ChaseEnemy && missionTarget != null)
        {
            // provocation : le guerrier qui frappe détourne le monstre des tireurs et de la soigneuse
            var att = attacker.GetComponent<AdventurerAI>();
            var cur = missionTarget.GetComponent<AdventurerAI>();
            if (att != null && cur != null && att != cur && att.role == AdventurerAI.Role.Warrior && cur.role != AdventurerAI.Role.Warrior
                && tacticPhase == 0 && Random.value < 0.6f)
            {
                missionTarget = attacker;
                combatState = CombatState.Approaching;
            }
            return;
        }
        StartMission(MissionType.ChaseEnemy, attacker);
    }

    // ═══════════════════════════════════════════
    // POSSESSION (mode exploration)
    // ═══════════════════════════════════════════

    public void SetPossessed(bool possessed)
    {
        if (tacticPhase > 0) EndTactic();
        IsPossessed = possessed;
        isOnMission = false;
        currentMission = MissionType.None;
        missionTarget = null;
        StopMoving();
        if (!possessed)
        {
            savedAnchorPosition = rb.position;
            anchorPoint.position = rb.position;
            currentBehavior = GoblinBehavior.Patrol;
            SetNewPatrolTarget();
        }
    }

    /// <summary>Déplacement direct (joueur). dir normalisé ou nul.</summary>
    // ── Compétence de famille en possession (bouton Compétence) ──
    private float manualSkillReady, dashUntil, dashStart;
    private Vector2 dashDir;
    private Vector3 dashScale;
    private bool dashLeap, dashHit;
    public const float SkillCooldown = 6f;

    public string SkillName
    {
        get
        {
            string f = Family;
            if (f == "goblin" || f == "orc" || f == "boar" || f == "rat") return "Charge";
            if (f == "gnoll" || f == "lizard" || f == "vampire") return "Bond";
            if (attackRange >= 2.5f) return "Salve";
            return "Rugissement";
        }
    }

    public float SkillCooldown01 => Time.time >= manualSkillReady ? 0f : Mathf.Clamp01((manualSkillReady - Time.time) / SkillCooldown);

    public bool ManualSkill()
    {
        if (Time.time < manualSkillReady) return false;
        manualSkillReady = Time.time + SkillCooldown;
        string skill = SkillName;
        Vector2 dir = lastMoveDirection.sqrMagnitude > 0.01f ? lastMoveDirection.normalized : Vector2.down;
        switch (skill)
        {
            case "Charge":
            case "Bond":
                dashDir = dir; dashLeap = skill == "Bond"; dashHit = false; dashScale = transform.localScale;
                dashStart = Time.time; dashUntil = Time.time + (dashLeap ? 0.4f : 0.3f);
                Sfx.Play("swing", transform.position, 0.7f);
                if (spriteRenderer != null) HitFlashFx.Flash(spriteRenderer, 0.1f);
                break;
            case "Salve":
            {
                Transform best = null; float bd = 7f;
                foreach (var h in Physics2D.OverlapCircleAll(rb.position, 7f, adventurerLayer))
                {
                    var d = h.GetComponent<IDamageable>();
                    if (d == null || !d.IsAlive() || !HasLineOfSight(h.transform)) continue;
                    float dist = Vector2.Distance(h.transform.position, rb.position);
                    if (dist < bd) { bd = dist; best = h.transform; }
                }
                if (best == null) { manualSkillReady = Time.time + 0.5f; return false; }
                var target = best.GetComponent<IDamageable>();
                float dmg = attackDamage * possessedDamageMult * 1.6f;
                Projectile.FireFx("fireball", transform.position + Vector3.up * 0.3f, best.position + Vector3.up * 0.2f, 0.5f, false,
                    () => { if (target.IsAlive()) { target.TakeDamage(dmg); OnDealtDamage(target, dmg); } }, 10f, 0.2f, 10, new Color(1f, 0.55f, 0.2f));
                attackAnimTimer = 0.4f;
                break;
            }
            default:
            {
                // rugissement : les intrus proches se figent, les plus faibles s'enfuient
                CameraController.Instance?.Shake(0.1f, 0.4f);
                Sfx.Play("growl", transform.position, 1f);
                FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "GRAAAH !", new Color(1f, 0.5f, 0.4f), 1.1f);
                foreach (var a in AdventurerAI.All)
                {
                    if (a == null || a.IsDead || Vector2.Distance(a.transform.position, rb.position) > 3.5f) continue;
                    if (Random.value < 0.35f) a.LifePanic(); else a.Hold(1.8f);
                }
                attackAnimTimer = 0.4f;
                break;
            }
        }
        return true;
    }

    public void ManualMove(Vector2 dir)
    {
        // charge / bond en cours : l'élan l'emporte sur le joystick
        if (Time.time < dashUntil)
        {
            float k = (Time.time - dashStart) / (dashUntil - dashStart);
            Vector2 dv = dashDir * chaseSpeed * possessedSpeedMult * (dashLeap ? 2.6f : 3f);
            if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInside(rb.position + dashDir * 0.4f)) dv = Vector2.zero;
            movement = dv;
            rb.linearVelocity = dv;
            lastMoveDirection = dashDir;
            if (dashLeap) transform.localScale = dashScale * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
            if (!dashHit)
                foreach (var h in Physics2D.OverlapCircleAll(rb.position, 0.7f, adventurerLayer))
                {
                    var d = h.GetComponent<IDamageable>();
                    if (d == null || !d.IsAlive()) continue;
                    dashHit = true;
                    float dmg = attackDamage * possessedDamageMult * 1.7f;
                    d.TakeDamage(dmg);
                    OnDealtDamage(d, dmg);
                    h.GetComponent<AdventurerAI>()?.Hold(0.8f);
                    HitFlashFx.Hitstop(0.06f);
                    break;
                }
            return;
        }
        if (dashLeap) { transform.localScale = dashScale; dashLeap = false; }
        Vector2 v = Vector2.ClampMagnitude(dir, 1f) * chaseSpeed * possessedSpeedMult;
        // Ne peut pas sortir du donjon
        if (DungeonTerritory.Instance != null && v.sqrMagnitude > 0.0001f)
        {
            Vector2 next = rb.position + v.normalized * 0.35f;
            if (!DungeonTerritory.Instance.IsInside(next))
            {
                Vector2 nx = rb.position + new Vector2(v.x, 0).normalized * 0.35f;
                Vector2 ny = rb.position + new Vector2(0, v.y).normalized * 0.35f;
                bool okX = Mathf.Abs(v.x) > 0.01f && DungeonTerritory.Instance.IsInside(nx);
                bool okY = Mathf.Abs(v.y) > 0.01f && DungeonTerritory.Instance.IsInside(ny);
                v = new Vector2(okX ? v.x : 0f, okY ? v.y : 0f);
            }
        }
        movement = v;
        rb.linearVelocity = v;
        if (v.sqrMagnitude > 0.01f) lastMoveDirection = v.normalized;
    }

    /// <summary>Attaque manuelle : frappe l'intrus le plus proche devant soi.</summary>
    public bool ManualAttack()
    {
        if (!CanAttack()) return false;
        lastAttackTime = Time.time;
        attackAnimTimer = 0.35f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, attackRange * 1.3f, adventurerLayer);
        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (var h in hits)
        {
            IDamageable d = h.GetComponent<IDamageable>();
            if (d == null || !d.IsAlive() || !HasLineOfSight(h.transform)) continue;
            Vector2 to = (Vector2)h.transform.position - rb.position;
            float angle = Vector2.Angle(lastMoveDirection, to);
            float score = to.magnitude + (angle > 100f ? 3f : 0f);
            if (score < bestScore) { bestScore = score; best = h.transform; }
        }
        if (best == null) return false;

        FaceTowards(best.position);
        var bestDmg = best.GetComponent<IDamageable>();
        float pd = attackDamage * possessedDamageMult * ThemeDamageMult();
        bestDmg.TakeDamage(pd);
        OnDealtDamage(bestDmg, pd);
        best.GetComponent<AdventurerAI>()?.OnAttacked(transform);
        return true;
    }

    // ═══════════════════════════════════════════
    // PATHFINDING
    // ═══════════════════════════════════════════

    private void UpdatePath()
    {
        if (IsPossessed || isDead || raid != null || !seeker.IsDone()) return;

        Vector2 targetPos;
        if (isOnMission)
        {
            if (missionTarget != null) targetPos = missionTarget.position;
            else if (currentMission == MissionType.HelpAlly) targetPos = missionDestination;
            else return;
        }
        else
        {
            switch (currentBehavior)
            {
                case GoblinBehavior.Patrol: targetPos = currentPatrolTarget; break;
                case GoblinBehavior.MoveTo: targetPos = moveToTarget; break;
                default: return;
            }
        }

        if (path != null && Vector2.Distance(targetPos, lastPathTarget) < 0.75f) return;

        lastPathTarget = targetPos;
        seeker.StartPath(rb.position, targetPos, OnPathComplete);
    }

    private void OnPathComplete(Path p)
    {
        if (p.error) return;
        path = p;
        currentWaypoint = AdventurerAI.FirstWaypoint(p, rb.position);
    }

    private void ForceRecalculatePath(Vector2 targetPos)
    {
        lastPathTarget = targetPos;
        path = null;
        seeker.CancelCurrentPathRequest();
        seeker.StartPath(rb.position, targetPos, OnPathComplete);
    }

    // ═══════════════════════════════════════════
    // MOUVEMENT
    // ═══════════════════════════════════════════

    private void FollowPath(float speed)
    {
        if (path == null || currentWaypoint >= path.vectorPath.Count)
        {
            StopMoving();
            return;
        }

        while (currentWaypoint < path.vectorPath.Count - 1 &&
               Vector2.Distance(rb.position, path.vectorPath[currentWaypoint]) < Mathf.Min(nextWaypointDistance, AdventurerAI.MaxWaypointReach))
        {
            if (PathPortal.TryTeleport(rb, path, currentWaypoint)) { currentWaypoint += 2; lastPathTarget = Vector2.positiveInfinity; }
            else currentWaypoint++;
            if (currentWaypoint >= path.vectorPath.Count) { StopMoving(); return; }
        }

        Vector2 wp = path.vectorPath[currentWaypoint];
        if (currentWaypoint == path.vectorPath.Count - 1 && Vector2.Distance(rb.position, wp) < 0.15f)
        {
            currentWaypoint++;
            StopMoving();
            return;
        }

        Vector2 dir = (wp - rb.position).normalized;
        movement = dir * speed;
        rb.linearVelocity = movement;
        if (movement.sqrMagnitude > 0.01f) lastMoveDirection = movement.normalized;
    }

    private void StopMoving()
    {
        rb.linearVelocity = Vector2.zero;
        movement = Vector2.zero;
    }

    private void FaceTowards(Vector2 pos)
    {
        Vector2 d = pos - rb.position;
        if (d.sqrMagnitude > 0.0001f) lastMoveDirection = d.normalized;
    }

    private void SetNewPatrolTarget()
    {
        Vector2 center = anchorPoint != null ? (Vector2)anchorPoint.position : rb.position;
        for (int i = 0; i < 12; i++)
        {
            Vector2 candidate = center + Random.insideUnitCircle * patrolRadius;
            if (TrySnapToWalkable(candidate, out Vector2 snapped))
            {
                currentPatrolTarget = snapped;
                ForceRecalculatePath(currentPatrolTarget);
                return;
            }
        }
        currentPatrolTarget = TrySnapToWalkable(center, out Vector2 c2) ? c2 : rb.position;
        ForceRecalculatePath(currentPatrolTarget);
    }

    /// <summary>Ramène un point au centre de la case praticable la plus proche (dans le donjon).</summary>
    private bool TrySnapToWalkable(Vector2 position, out Vector2 snapped)
    {
        snapped = position;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph == null) return IsPositionWalkable(position);
        var nn = graph.GetNearest(position, NNConstraint.Default);
        if (nn.node == null) return false;
        snapped = (Vector3)nn.node.position;
        if (Vector2.Distance(snapped, position) > 1.5f) return false;
        return DungeonTerritory.Instance == null || DungeonTerritory.Instance.IsInside(snapped);
    }

    private bool IsPositionWalkable(Vector2 position)
    {
        if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInside(position)) return false;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph == null) return true;
        GraphNode node = graph.GetNearest(position).node;
        return node != null && node.Walkable;
    }

    // ═══════════════════════════════════════════
    // DÉTECTION
    // ═══════════════════════════════════════════

    private Transform DetectAdventurer()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, detectionRange, adventurerLayer);
        float closestDist = Mathf.Infinity;
        Transform closest = null;

        foreach (var hit in hits)
        {
            if (!IsInFieldOfView(hit.transform)) continue;
            if (!HasLineOfSight(hit.transform)) continue;
            IDamageable d = hit.GetComponent<IDamageable>();
            if (d == null || !d.IsAlive()) continue;
            if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInside(hit.transform.position)) continue;
            var adv = hit.GetComponent<AdventurerAI>();
            if (adv != null && adv.Spared) continue;

            float dist = Vector2.Distance(rb.position, hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = hit.transform;
            }
        }
        return closest;
    }

    private bool IsInFieldOfView(Transform target)
    {
        Vector2 dir = ((Vector2)target.position - rb.position).normalized;
        return Vector2.Angle(lastMoveDirection, dir) <= fieldOfViewAngle / 2f;
    }

    private bool HasLineOfSight(Transform target)
    {
        return AdventurerAI.ClearLine(rb.position, target.position, obstacleLayer);
    }

    // ═══════════════════════════════════════════
    // ALERTE
    // ═══════════════════════════════════════════

    private void AlertNearbyGoblins()
    {
        foreach (var ally in All)
        {
            if (ally == this || ally == null || ally.isOnMission) continue;
            if (Vector2.Distance(ally.transform.position, transform.position) <= alertRadius)
                ally.ReceiveAlert(rb.position);
        }
    }

    // ═══════════════════════════════════════════
    // COMBAT
    // ═══════════════════════════════════════════

    // ═══════════════════════════════════════════
    // TACTIQUES DE FAMILLE (déplacements en 8 directions)
    //   gobelin / orc : charge (temps d'armement, ruée en ligne droite, coup renforcé)
    //   gnoll / lézard : bond (s'accroupit, saute sur la cible, frappe à l'atterrissage)
    //   rat : contournement (approche de biais pour prendre l'intrus de flanc)
    // ═══════════════════════════════════════════

    /// <summary>Nombre de charges et de bonds lancés depuis le début (statistique, tests).</summary>
    public static int TacticsUsed;
    private int tacticPhase;            // 0 aucune, 1 armement, 2 action
    private bool tacticLeap;
    private float tacticTimer, tacticReadyAt;
    private Vector2 tacticDir, tacticFrom, tacticTo;
    private Vector3 tacticScale;

    private bool TryStartTactic(float dist)
    {
        if (IsPossessed || Time.time < tacticReadyAt || missionTarget == null) return false;
        string f = Family;
        bool charge = (f == "goblin" || f == "orc") && dist > 2.4f && dist < 5.5f;
        bool leap = (f == "gnoll" || f == "lizard") && dist > 2f && dist < 4.6f;
        if (!charge && !leap) return false;
        TacticsUsed++;
        tacticLeap = leap;
        tacticPhase = 1;
        tacticTimer = leap ? 0.22f : 0.32f;
        tacticScale = transform.localScale;
        StopMoving();
        FaceTowards(missionTarget.position);
        if (spriteRenderer != null) HitFlashFx.Flash(spriteRenderer, 0.12f);   // signal lisible pour le joueur
        return true;
    }

    private void StepTactic(float dist)
    {
        tacticTimer -= Time.fixedDeltaTime;
        if (missionTarget == null) { EndTactic(); return; }
        if (tacticPhase == 1)
        {
            StopMoving();
            FaceTowards(missionTarget.position);
            // ramassé sur lui-même avant de partir
            if (tacticLeap) transform.localScale = new Vector3(tacticScale.x * 1.12f, tacticScale.y * 0.85f, tacticScale.z);
            if (tacticTimer > 0f) return;
            tacticPhase = 2;
            tacticFrom = rb.position;
            tacticDir = ((Vector2)missionTarget.position - rb.position).normalized;
            tacticTo = (Vector2)missionTarget.position - tacticDir * 0.6f;
            tacticTimer = tacticLeap ? 0.42f : 0.38f;
            Sfx.Play("swing", transform.position, 0.6f);
            return;
        }

        if (tacticLeap)
        {
            float k = 1f - Mathf.Clamp01(tacticTimer / 0.42f);
            rb.MovePosition(Vector2.Lerp(tacticFrom, tacticTo, k));
            movement = tacticDir * chaseSpeed;
            lastMoveDirection = tacticDir;
            float hop = Mathf.Sin(k * Mathf.PI);
            transform.localScale = tacticScale * (1f + 0.28f * hop);
            if (tacticTimer > 0f) return;
            if (Vector2.Distance(rb.position, missionTarget.position) <= attackRange * 1.1f) StrikeWithBonus(1.35f);
            Fx.Play("smoke_ring", transform.position, 0.8f);
            EndTactic();
            return;
        }

        // charge : ruée droite, s'arrête au contact
        movement = tacticDir * chaseSpeed * 3.1f;
        rb.linearVelocity = movement;
        lastMoveDirection = tacticDir;
        if (dist <= attackRange * 0.9f) { StrikeWithBonus(1.4f); EndTactic(); return; }
        if (tacticTimer <= 0f) EndTactic();
    }

    private void StrikeWithBonus(float mult)
    {
        float baseDmg = attackDamage;
        attackDamage *= mult;
        Attack();
        attackDamage = baseDmg;
        HitFlashFx.Hitstop(0.05f);
    }

    private void EndTactic()
    {
        if (tacticPhase > 0 && tacticScale != Vector3.zero) transform.localScale = tacticScale;
        tacticPhase = 0;
        tacticReadyAt = Time.time + (tacticLeap ? 5f : 4.5f) + Random.Range(0f, 1.2f);
        StopMoving();
    }

    /// <summary>Rat : approche en biais, chacun de son côté, pour encercler l'intrus.</summary>
    private void Flank(float dist)
    {
        if (Family != "rat" || missionTarget == null || dist < attackRange || dist > 4.5f || movement.sqrMagnitude < 0.01f) return;
        Vector2 to = ((Vector2)missionTarget.position - rb.position).normalized;
        float side = (GetInstanceID() & 1) == 0 ? 1f : -1f;
        Vector2 perp = new Vector2(-to.y, to.x) * side;
        float lateral = Mathf.InverseLerp(attackRange, 4.5f, dist);   // de biais de loin, droit au but de près
        movement = (to + perp * 0.9f * lateral).normalized * chaseSpeed * 1.08f;
        rb.linearVelocity = movement;
        lastMoveDirection = movement.normalized;
    }

    // ── Meute, tireurs, mur de boucliers, fuite et embuscade ──

    private bool IsShield { get { string f = Family; return f == "orc" || f == "golem"; } }
    private static readonly System.Collections.Generic.HashSet<string> Fearless =
        new System.Collections.Generic.HashSet<string> { "skeleton", "zombie", "golem", "slime", "ghost", "temple_spirit", "lich" };
    private bool fledOnce;
    private float fleeUntil;
    private Transform fleeTo;

    /// <summary>À moins d'un quart de ses PV, il fuit vers le monstre le plus proche et donne l'alerte (une fois par combat).</summary>
    private bool UpdateFleeToAllies()
    {
        if (fleeTo != null)
        {
            if (Time.time > fleeUntil || Vector2.Distance(rb.position, fleeTo.position) < 1.4f)
            {
                fleeTo = null;
                AlertNearbyGoblins();
                if (missionTarget != null) foreach (var g in All) if (g != this && g != null && Vector2.Distance(g.transform.position, rb.position) < 3f) g.OnAttackedBy(missionTarget);
                return false;
            }
            FollowPath(chaseSpeed * 1.15f);
            return true;
        }
        float fleeAt = life != null ? life.FleeThreshold : 0.25f;
        if (fledOnce || IsPossessed || maxHealth <= 0f || fleeAt <= 0f || currentHealth / maxHealth > fleeAt || Fearless.Contains(Family) || IsGuardianUnit) return false;
        GobelinAI best = null;
        float bd = 12f;
        foreach (var g in All)
        {
            if (g == this || g == null || g.isDead || g.currentMission == MissionType.ChaseEnemy) continue;
            float d = Vector2.Distance(g.transform.position, rb.position);
            if (d < bd && d > 2f) { bd = d; best = g; }
        }
        if (best == null) return false;
        fledOnce = true;
        fleeTo = best.transform;
        fleeUntil = Time.time + 4f;
        ForceRecalculatePath(best.transform.position);
        FloatingText.Spawn(transform.position + Vector3.up * 0.7f, "À l'aide !", new Color(0.8f, 0.9f, 1f), 0.9f);
        return true;
    }

    private bool IsGuardianUnit { get { var m = GetComponent<MonsterMind>(); return m != null && m.IsGuardian; } }

    /// <summary>Recule face à la cible (sans traverser les murs).</summary>
    private bool KiteFrom(Transform t)
    {
        Vector2 away = (rb.position - (Vector2)t.position).normalized;
        Vector2 to = rb.position + away * 1.2f;
        if (!AdventurerAI.ClearLine(rb.position, to, obstacleLayer))
        {
            Vector2 alt = rb.position + (away + new Vector2(-away.y, away.x)).normalized * 1.2f;
            if (!AdventurerAI.ClearLine(rb.position, alt, obstacleLayer)) return false;
            to = alt;
        }
        movement = (to - rb.position).normalized * chaseSpeed;
        rb.linearVelocity = movement;
        lastMoveDirection = -away;
        return true;
    }

    private static readonly System.Collections.Generic.List<GobelinAI> packBuf = new System.Collections.Generic.List<GobelinAI>();

    /// <summary>
    /// Meute : les monstres d'une même famille qui chassent la même proie se répartissent autour d'elle
    /// (chacun son angle) au lieu de faire la queue dans le couloir.
    /// </summary>
    private bool PackSlot(float dist)
    {
        if (missionTarget == null || dist > 3.5f || dist < attackRange * 0.8f || attackRange >= 2.5f) return false;
        string f = Family;
        if (string.IsNullOrEmpty(f) || f == "rat") return false;
        packBuf.Clear();
        foreach (var g in All)
            if (g != null && !g.isDead && g.missionTarget == missionTarget && g.Family == f) packBuf.Add(g);
        if (packBuf.Count < 2) return false;
        packBuf.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        int idx = packBuf.IndexOf(this);
        Vector2 tp = missionTarget.position;
        Vector2 baseDir = (rb.position - tp).normalized;
        float spread = Mathf.Min(140f, 55f * (packBuf.Count - 1));
        float ang = (packBuf.Count == 1 ? 0f : -spread * 0.5f + spread * idx / (packBuf.Count - 1)) * Mathf.Deg2Rad;
        // l'angle est mesuré depuis la direction moyenne de la meute : on encercle sans tourner autour
        Vector2 avg = Vector2.zero;
        foreach (var g in packBuf) avg += ((Vector2)g.transform.position - tp).normalized;
        if (avg.sqrMagnitude < 0.01f) avg = baseDir;
        avg.Normalize();
        Vector2 dir = new Vector2(avg.x * Mathf.Cos(ang) - avg.y * Mathf.Sin(ang), avg.x * Mathf.Sin(ang) + avg.y * Mathf.Cos(ang));
        Vector2 slot = tp + dir * attackRange * 0.85f;
        if (!AdventurerAI.ClearLine(tp, slot, obstacleLayer)) return false;
        Vector2 to = slot - rb.position;
        if (to.magnitude < 0.2f) { StopMoving(); FaceTowards(tp); return true; }
        if (!AdventurerAI.ClearLine(rb.position, slot, obstacleLayer)) return false;
        movement = to.normalized * chaseSpeed;
        rb.linearVelocity = movement;
        lastMoveDirection = movement.normalized;
        return true;
    }

    // Embuscade : un monstre posté (« Tenir ») et au repos depuis un moment se tapit dans l'ombre.
    private bool ambushing;
    private float stillSince = -1f;
    /// <summary>Tapis dans l'ombre : les intrus ne le voient qu'à bout portant.</summary>
    public bool IsAmbushing => ambushing;
    private static readonly System.Collections.Generic.HashSet<string> Ambushers =
        new System.Collections.Generic.HashSet<string> { "goblin", "gnoll", "lizard", "vampire", "imp", "rat", "orc" };

    /// <summary>Déguisés en objets (mimiques) : toujours à l'affût dès qu'ils sont immobiles, sans se rendre transparents.</summary>
    private static readonly System.Collections.Generic.HashSet<string> Disguised =
        new System.Collections.Generic.HashSet<string> { "mimic" };

    private void UpdateAmbush()
    {
        bool disguised = Disguised.Contains(Family);
        bool calm = (currentBehavior == GoblinBehavior.Stay || disguised) && !isOnMission && !IsPossessed && movement.sqrMagnitude < 0.01f
                    && (Ambushers.Contains(Family) || disguised);
        if (!calm) { stillSince = -1f; if (ambushing) { ambushing = false; RevealAmbush(); } return; }
        if (stillSince < 0f) stillSince = Time.time;
        if (!ambushing && Time.time - stillSince > Talents.AmbushDelay)
        {
            ambushing = true;
            if (spriteRenderer != null && !disguised) { var c = spriteRenderer.color; c.a = 0.55f; spriteRenderer.color = c; }   // repère pour le joueur
        }
    }

    private void RevealAmbush()
    {
        stillSince = -1f;
        if (spriteRenderer != null) { var c = spriteRenderer.color; c.a = baseColor.a; spriteRenderer.color = c; }
    }

    // ── API pour la vie du monstre (MonsterLife) ──
    private MonsterLife life;
    public MonsterLife Life => life;
    public bool InCombat => isOnMission && currentMission == MissionType.ChaseEnemy;
    /// <summary>Libre de vivre sa vie : ni mission, ni ordre de déplacement, ni possession, ni raid.</summary>
    public bool IdleForLife => !isDead && !isOnMission && !IsPossessed && raid == null && currentBehavior != GoblinBehavior.MoveTo && tacticPhase == 0;
    public void LifeGoTo(Vector2 p) => ForceRecalculatePath(p);
    public void LifeMove(float mult = 1f) => FollowPath(patrolSpeed * mult);
    public void LifeStop() => StopMoving();
    public void LifeFace(Vector2 p) => FaceTowards(p);
    public void LifeGesture(float t) => attackAnimTimer = t;
    /// <summary>Un monstre posté (« Tenir ») qui s'est absenté (repas, querelle) regagne son poste.</summary>
    public void ReturnToPost()
    {
        if (currentBehavior != GoblinBehavior.Stay || Vector2.Distance(rb.position, savedAnchorPosition) < 1f) return;
        MoveToPosition(savedAnchorPosition);
        returnToStay = true;
    }
    /// <summary>Un intrus en vue (rayon réduit pour un monstre endormi).</summary>
    public Transform LifeDetect(float rangeMult)
    {
        foreach (var h in Physics2D.OverlapCircleAll(rb.position, detectionRange * rangeMult, adventurerLayer))
        {
            var d = h.GetComponent<IDamageable>();
            if (d != null && d.IsAlive() && HasLineOfSight(h.transform)) return h.transform;
        }
        return null;
    }

    public bool CanAttack() => Time.time - lastAttackTime >= attackCooldown;

    /// <summary>Change de proie en plein combat (chauves-souris qui fondent sur les tireurs).</summary>
    public void Retarget(Transform t)
    {
        if (t == null || !isOnMission || currentMission != MissionType.ChaseEnemy) return;
        missionTarget = t;
    }

    // galvanisé par un chaman de sa famille
    private float buffMult = 1f, buffUntil;
    public void Buff(float mult, float seconds) { buffMult = Mathf.Max(Time.time < buffUntil ? buffMult : 1f, mult); buffUntil = Time.time + seconds; }
    private float BuffMult => Time.time < buffUntil ? buffMult : 1f;

    public void Attack()
    {
        if (missionTarget == null) return;
        lastAttackTime = Time.time;
        IDamageable damageable = missionTarget.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive())
        {
            attackAnimTimer = 0.4f;
            float ambushMult = (ambushing ? Talents.AmbushMult : 1f) * (life != null ? life.DamageMult : 1f) * IslandEvents.FireMult(Family);
            if (ambushing) { ambushing = false; RevealAmbush(); FloatingText.Spawn(transform.position + Vector3.up * 0.7f, Disguised.Contains(Family) ? "Surprise !" : "Embuscade !", new Color(1f, 0.55f, 0.35f), 1f); }
            float dmg = ambushMult * attackDamage * ThemeDamageMult() * (IslandEvents.Instance != null ? IslandEvents.Instance.MonsterDamageMult : 1f)
                        * ThemedRooms.DamageMult(rb.position, Family) * BuffMult;
            damageable.TakeDamage(dmg);
            Sfx.Play("hit_claw", transform.position, 0.8f);
            OnDealtDamage(damageable, dmg);
            AdventurerAI adventurerAI = missionTarget != null ? missionTarget.GetComponent<AdventurerAI>() : null;
            if (adventurerAI != null && damageable.IsAlive()) adventurerAI.OnAttacked(transform);
        }
    }

    private MonsterTraits traits;
    private MonsterUnit unit;

    /// <summary>Pouvoirs au contact + expérience si le coup achève l'intrus.</summary>
    private void OnDealtDamage(IDamageable target, float dmg)
    {
        if (traits == null) traits = GetComponent<MonsterTraits>();
        if (unit == null) unit = GetComponent<MonsterUnit>();
        if (traits != null) traits.OnHit(target, dmg);
        GetComponent<MonsterVariant>()?.OnHit(target, dmg);
        if (!target.IsAlive()) life?.OnKill();
        if (unit != null && !target.IsAlive())
        {
            var adv = (target as Component) != null ? ((Component)target).GetComponent<AdventurerAI>() : null;
            unit.OnKill(adv != null ? adv.TierIndex : 0);
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    private Vector2 stuckPos;
    private float stuckTime;

    /// <summary>Un monstre qui veut bouger mais n'avance plus change de cible.</summary>
    private void CheckStuck()
    {
        bool wantsToMove = movement.sqrMagnitude > 0.01f;
        if (!wantsToMove || combatState == CombatState.Attacking)
        {
            stuckTime = Time.time;
            stuckPos = rb.position;
            return;
        }
        if (Time.time - stuckTime < 1.5f) return;
        bool stuck = Vector2.Distance(rb.position, stuckPos) < 0.2f;
        stuckTime = Time.time;
        stuckPos = rb.position;
        if (!stuck) return;

        if (raid != null) { raid.nextRepath = 0f; path = null; }
        else if (isOnMission) ForceRecalculatePath(missionTarget != null ? (Vector2)missionTarget.position : missionDestination);
        else if (currentBehavior == GoblinBehavior.Patrol) SetNewPatrolTarget();
        else if (currentBehavior == GoblinBehavior.MoveTo)
        {
            // Destination inaccessible : on s'arrête ici
            if (anchorPoint != null) anchorPoint.position = rb.position;
            savedAnchorPosition = rb.position;
            SetBehavior(returnToStay ? GoblinBehavior.Stay : GoblinBehavior.Patrol);
        }
    }

    private string Family
    {
        get { if (unit == null) unit = GetComponent<MonsterUnit>(); return unit != null ? unit.familyId : ""; }
    }

    private float ThemeDamageMult()
    {
        var theme = DungeonLayers.Instance != null ? DungeonLayers.Instance.ThemeAt(rb.position) : null;
        if (theme == null) return Talents.MonsterDamageMult;
        float m = theme.monsterDamageMult;
        if (theme.undeadDamageMult != 1f)
        {
            if (unit == null) unit = GetComponent<MonsterUnit>();
            string f = unit != null ? unit.familyId : "";
            if (f == "skeleton" || f == "zombie" || f == "lich" || f == "ghost" || f == "vampire" || f == "temple_spirit") m *= theme.undeadDamageMult;
        }
        return m * Talents.MonsterDamageMult;
    }

    private float stairsCooldown;

    /// <summary>Monstre possédé : emprunter un escalier en marchant dessus.</summary>
    private void CheckStairs()
    {
        stairsCooldown -= Time.fixedDeltaTime;
        if (stairsCooldown > 0f) return;
        var st = DungeonStairs.At(rb.position, 0.45f);
        if (st == null) return;
        rb.position = st.ExitPosition;
        transform.position = rb.position;
        stairsCooldown = 1.2f;
        DungeonLayers.Instance?.SyncViewTo(rb.position);
        FloatingText.Spawn(transform.position + Vector3.up * 0.6f, st.goesDown ? "Descente" : "Montée", GameHUD.InfoColor, 1f);
    }

    private bool IsInsideDungeon()
    {
        if (DungeonTerritory.Instance != null) return DungeonTerritory.Instance.IsInside(rb.position);
        return true;
    }

    // ═══════════════════════════════════════════
    // IDAMAGEABLE
    // ═══════════════════════════════════════════

    public bool IsAlive() => currentHealth > 0f;
    public float GetCurrentHealth() => currentHealth;
    public string GetName() => gobelinName;

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        // mur de boucliers : un orc ou un golem campé sur ses appuis encaisse mieux
        if (IsShield && movement.sqrMagnitude < 0.05f) amount *= 0.65f;
        amount *= ThemedRooms.DamageTakenMult(rb != null ? rb.position : (Vector2)transform.position);
        var variant = GetComponent<MonsterVariant>();
        if (variant != null) { amount = variant.Incoming(amount); if (amount <= 0f) return; }
        if (ambushing) { ambushing = false; RevealAmbush(); }
        currentHealth -= amount;
        outOfCombatTimer = 0f;
        if (currentHealth > 0f) Voices.Monster(this, "hurt", 0.4f);
        FloatingText.Spawn(transform.position + Vector3.up * 0.5f, Mathf.RoundToInt(amount).ToString(), new Color(1f, 0.4f, 0.4f), 0.9f);
        if (spriteRenderer != null && gameObject.activeInHierarchy)
        {
            StopCoroutine(nameof(Flash));
            StartCoroutine(nameof(Flash));
            HitFlashFx.Flash(spriteRenderer);
        }
        if (currentHealth <= 0) Die();
        else UnitAnim.Hurt(animator);
    }

    private IEnumerator Flash()
    {
        spriteRenderer.color = new Color(1f, 0.4f, 0.4f);
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = baseColor;
    }

    private void Die()
    {
        isDead = true;
        if (!Voices.Monster(this, "death")) Sfx.Play("death_monster", transform.position, 0.9f);
        GetComponent<MonsterVariant>()?.OnDeath();
        GetComponent<MonsterMind>()?.OnDied();
        var mm = GetComponent<MonsterMind>();
        if (mm != null && mm.IsGuardian) Chronicle.Add($"<color=#FF8A7A>Le gardien {mm.GivenName} est tombé.</color>");
        else if (unit != null && unit.stage >= 3) Chronicle.Add($"{gobelinName} est tombé en défendant le donjon.");
        if (life != null) { MonsterSociety.OnDied(life); life.Interrupt(); }
        if (CrawlerMode.Active) CrawlerMode.Instance.OnMonsterKilled(this);
        if (anchorPoint != null)
        {
            var pa = anchorPoint.GetComponent<PatrolAnchor>();
            if (pa != null) pa.UnregisterGoblin(this);
        }
        if (IsPossessed) PossessionController.Instance?.OnPossessedDied(this);
        var mu = GetComponent<MonsterUnit>();
        if ((mu == null || !mu.summoned) && !CrawlerMode.Active) GameHUD.Toast($"{gobelinName} est tombé au combat", GameHUD.WarningColor);
        Fx.Play("smoke_rise", transform.position + Vector3.up * 0.3f, 1.2f, 16f, 16, new Color(0.5f, 0.45f, 0.55f, 0.8f));
        UnitAnim.Die(gameObject, this);
    }

    // ═══════════════════════════════════════════
    // ANIMATOR
    // ═══════════════════════════════════════════

    private bool animParamsChecked, hasSpeed, hasMoveX, hasMoveY, hasAttack;

    private void UpdateAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        if (!animParamsChecked)
        {
            animParamsChecked = true;
            foreach (var p in animator.parameters)
            {
                if (p.name == "Speed") hasSpeed = true;
                else if (p.name == "MoveX") hasMoveX = true;
                else if (p.name == "MoveY") hasMoveY = true;
                else if (p.name == "IsAttacking") hasAttack = true;
            }
        }
        float moveSpeed = movement.sqrMagnitude;
        Vector2 facing = moveSpeed > 0.01f ? movement : lastMoveDirection;
        if (hasSpeed) animator.SetFloat("Speed", moveSpeed);
        animator.speed = life != null && life.Sleeping ? 0.25f : 1f;
        if (hasMoveX) animator.SetFloat("MoveX", facing.x);
        if (hasMoveY) animator.SetFloat("MoveY", facing.y);
        if (hasAttack) animator.SetBool("IsAttacking", attackAnimTimer > 0f);
        // Sprites sans animation de direction : on retourne l'image
        if (!hasMoveX && spriteRenderer != null && Mathf.Abs(facing.x) > 0.1f) spriteRenderer.flipX = facing.x < 0f;
    }

    // ═══════════════════════════════════════════
    // GIZMOS
    // ═══════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        if (anchorPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(anchorPoint.position, patrolRadius);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, alertRadius);

        if (missionTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, missionTarget.position);
        }

#if UNITY_EDITOR
        string statusText = isOnMission ? $"MISSION: {currentMission}\n{combatState}" : $"{currentBehavior}";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, statusText);
#endif
    }
}
