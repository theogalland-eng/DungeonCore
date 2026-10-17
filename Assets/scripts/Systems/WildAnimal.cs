using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// Faune de l'île (cerfs, lièvres, renards, tétras, sangliers), vivante et chassable :
///   - troupeaux : les cerfs et les sangliers vivent en petits groupes autour d'un point qui dérive ;
///   - eau douce : à l'aube et au crépuscule, les herbivores vont boire ensemble au lac le plus proche ;
///   - nuit : les herbivores dorment serrés ; le renard sort chasser les lièvres et les tétras ;
///   - danger : fuite devant les humains et les monstres ; le sanglier blessé charge son agresseur ;
///   - chasse : un intrus affamé peut les tuer ; la carcasse donne quelques portions de viande.
/// </summary>
[RequireComponent(typeof(Seeker))]
public class WildAnimal : MonoBehaviour, IDamageable
{
    public static readonly List<WildAnimal> All = new List<WildAnimal>();
    public static readonly List<WildAnimal> Carcasses = new List<WildAnimal>();

    public enum Species { Deer, Hare, Fox, Boar, Grouse }

    /// <summary>Petit groupe d'animaux : un centre qui dérive, et un lac où boire.</summary>
    public class Herd
    {
        public Vector2 center;
        public FreshWater.Lake lake;
        public Vector2 drinkSpot;
        public float nextDrift;
        public int members;
    }

    public float walkSpeed = 0.8f;
    public float runSpeed = 2.4f;
    public float wanderRadius = 6f;
    public float fearRadius = 3.5f;

    public Species species;
    public Herd herd;
    public float maxHealth = 20f;
    private float health;
    public bool IsDead { get; private set; }
    /// <summary>Portions de viande restantes sur la carcasse.</summary>
    public int Meat { get; private set; }
    /// <summary>Un chasseur s'occupe de cette proie / carcasse.</summary>
    [System.NonSerialized] public Object reservedBy;

    private Seeker seeker;
    private Animator animator;
    private SpriteRenderer sr;
    private Path path;
    private int wp;
    private Vector2 home;
    private Vector2 facing = Vector2.down;
    private Vector2 velocity;
    private float nextDecision, nextThreatScan, rotUntil;
    private bool fleeing, sleeping, drinking;
    private bool hasSpeed, hasX, hasY;
    private Transform enraged;        // sanglier : il charge celui qui l'a blessé
    private float enragedUntil, nextGore;
    private WildAnimal prey;          // renard
    private float eatUntil;
    private Vector2 lastThreat;

    public bool IsPrey => species == Species.Hare || species == Species.Grouse;
    public bool IsHerbivore => species != Species.Fox;
    public bool IsAlive() => !IsDead;
    public float GetCurrentHealth() => health;
    public string GetName() => Name(species);

    public static string Name(Species s) =>
        s == Species.Deer ? "Cerf" : s == Species.Hare ? "Lièvre" : s == Species.Fox ? "Renard" : s == Species.Boar ? "Sanglier" : "Tétras";

    public static Species SpeciesOf(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("deer")) return Species.Deer;
        if (n.Contains("hare")) return Species.Hare;
        if (n.Contains("fox")) return Species.Fox;
        if (n.Contains("boar")) return Species.Boar;
        return Species.Grouse;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() { All.Remove(this); Carcasses.Remove(this); }

    private void Start()
    {
        seeker = GetComponent<Seeker>();
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        home = transform.position;
        species = SpeciesOf(name);
        maxHealth = species == Species.Deer ? 34f : species == Species.Boar ? 60f : species == Species.Fox ? 22f : 10f;
        health = maxHealth;
        if (species == Species.Boar) { runSpeed = 2.8f; fearRadius = 2.6f; }
        if (species == Species.Hare) { runSpeed = 3.2f; fearRadius = 4f; }
        if (species == Species.Deer) { runSpeed = 3f; fearRadius = 4.5f; }
        hasSpeed = UnitAnim.HasParam(animator, "Speed");
        hasX = UnitAnim.HasParam(animator, "MoveX");
        hasY = UnitAnim.HasParam(animator, "MoveY");
        nextDecision = Time.time + Random.Range(0f, 4f);
    }

    // ─────────────────────────────────────────────
    // CYCLE
    // ─────────────────────────────────────────────

    private enum Phase { Day, Dawn, Dusk, Night }

    private static Phase CurrentPhase()
    {
        var gm = GameManager.Instance;
        if (gm == null) return Phase.Day;
        float t = gm.DayProgress, night = 1f - gm.nightPortion;
        if (gm.IsNight) return Phase.Night;
        if (t < 0.1f) return Phase.Dawn;
        if (t > night - 0.1f) return Phase.Dusk;
        return Phase.Day;
    }

    private void Update()
    {
        if (IsDead) { UpdateCarcass(); return; }
        Vector2 pos = transform.position;
        Phase phase = CurrentPhase();

        // sanglier furieux : il fonce sur son agresseur
        if (enraged != null && Time.time < enragedUntil)
        {
            var d = enraged.GetComponent<IDamageable>();
            if (d != null && d.IsAlive())
            {
                Charge(d);
                Animate();
                return;
            }
        }
        enraged = null;

        // danger ?
        if (Time.time >= nextThreatScan)
        {
            nextThreatScan = Time.time + 0.25f;
            float r = sleeping ? fearRadius * 0.6f : fearRadius;
            if (FindThreat(pos, r, out lastThreat))
            {
                if (!fleeing || Random.value < 0.3f)
                {
                    fleeing = true; sleeping = false; drinking = false; prey = null;
                    Vector2 away = (pos - lastThreat).normalized;
                    GoTo(pos + away * 6f + Random.insideUnitCircle * 2f);
                    if (herd != null) herd.center = Vector2.Lerp(herd.center, pos + away * 5f, 0.5f);   // tout le troupeau s'éloigne
                }
            }
            else if (fleeing && (path == null || wp >= path.vectorPath.Count)) { fleeing = false; nextDecision = 0f; }
        }

        if (!fleeing)
        {
            if (species == Species.Fox && phase == Phase.Night) Hunt(pos);
            else if (Time.time >= nextDecision) Decide(pos, phase);
        }

        Move(fleeing ? runSpeed : prey != null ? runSpeed * 0.9f : walkSpeed);
        if (Time.time < eatUntil) velocity = Vector2.zero;
        Animate();
    }

    private bool FindThreat(Vector2 pos, float r, out Vector2 threat)
    {
        threat = Vector2.zero;
        float r2 = r * r;
        foreach (var a in AdventurerAI.All)
            if (a != null && !a.IsDead && ((Vector2)a.transform.position - pos).sqrMagnitude < r2) { threat = a.transform.position; return true; }
        foreach (var g in GobelinAI.All)
            if (g != null && ((Vector2)g.transform.position - pos).sqrMagnitude < r2) { threat = g.transform.position; return true; }
        var hero = CrawlerMode.Instance != null ? CrawlerMode.Instance.Player : null;
        if (hero != null && ((Vector2)hero.transform.position - pos).sqrMagnitude < r2) { threat = hero.transform.position; return true; }
        // les proies fuient le renard
        if (IsPrey)
            foreach (var f in All)
                if (f != null && f.species == Species.Fox && !f.IsDead && ((Vector2)f.transform.position - pos).sqrMagnitude < r2 * 0.7f)
                { threat = f.transform.position; return true; }
        return false;
    }

    private void Decide(Vector2 pos, Phase phase)
    {
        nextDecision = Time.time + Random.Range(3f, 7f);
        drinking = false;
        Vector2 anchor = herd != null ? herd.center : home;

        // le centre du troupeau dérive lentement : les bêtes se déplacent ensemble
        if (herd != null && Time.time >= herd.nextDrift)
        {
            herd.nextDrift = Time.time + Random.Range(18f, 35f);
            Vector2 c = herd.center + Random.insideUnitCircle * 5f;
            if (WalkableDry(c) && Vector2.Distance(c, home) < 22f) herd.center = c;
        }

        if (IsHerbivore && (phase == Phase.Dawn || phase == Phase.Dusk))
        {
            // à boire, tous ensemble
            var lake = herd != null ? herd.lake : FreshWater.Nearest(pos);
            if (lake != null && lake.shore.Count > 0 && Vector2.Distance(lake.center, pos) < 45f)
            {
                Vector2 spot = herd != null && herd.drinkSpot != Vector2.zero ? herd.drinkSpot : lake.shore[Random.Range(0, lake.shore.Count)];
                if (herd != null && herd.drinkSpot == Vector2.zero) herd.drinkSpot = spot;
                if (Vector2.Distance(pos, spot) > 1.6f) GoTo(spot + Random.insideUnitCircle * 0.9f);
                else { path = null; drinking = true; facing = (lake.center - pos).normalized; nextDecision = Time.time + Random.Range(5f, 9f); }
                return;
            }
        }
        if (herd != null && phase == Phase.Day) herd.drinkSpot = Vector2.zero;

        if (IsHerbivore && phase == Phase.Night)
        {
            // la nuit : serrés les uns contre les autres, immobiles
            if (Vector2.Distance(pos, anchor) > 1.6f) GoTo(anchor + Random.insideUnitCircle * 1.1f);
            else { path = null; sleeping = true; nextDecision = Time.time + Random.Range(8f, 14f); }
            return;
        }
        sleeping = false;
        if (species == Species.Fox && phase != Phase.Night)
        {
            // le jour, le renard somnole dans son coin
            if (Random.value < 0.7f) { path = null; return; }
        }
        float radius = herd != null ? 2.5f : wanderRadius;
        if (Random.value < 0.65f) GoTo(anchor + Random.insideUnitCircle * radius);
        else path = null;   // broute / se repose
    }

    // ─────────────────────────────────────────────
    // RENARD : CHASSE NOCTURNE
    // ─────────────────────────────────────────────

    private void Hunt(Vector2 pos)
    {
        if (Time.time < eatUntil) return;
        if (prey == null || prey.IsDead || Vector2.Distance(prey.transform.position, pos) > 12f)
        {
            prey = null;
            float bd = 10f;
            foreach (var a in All)
                if (a != null && a.IsPrey && !a.IsDead)
                {
                    float d = Vector2.Distance(a.transform.position, pos);
                    if (d < bd) { bd = d; prey = a; }
                }
            if (prey == null) { if (Time.time >= nextDecision) Decide(pos, Phase.Day); return; }
        }
        if (Vector2.Distance(prey.transform.position, pos) < 0.45f)
        {
            prey.TakeDamage(999f);
            prey.Meat = 0;           // dévoré sur place
            prey.rotUntil = Time.time + 20f;
            eatUntil = Time.time + 6f;
            prey = null;
            return;
        }
        if (Time.time >= nextDecision) { nextDecision = Time.time + 0.6f; GoTo(prey.transform.position); }
    }

    // ─────────────────────────────────────────────
    // DÉGÂTS, MORT, CARCASSE
    // ─────────────────────────────────────────────

    public void TakeDamage(float amount) => TakeDamage(amount, null);

    public void TakeDamage(float amount, Transform from)
    {
        if (IsDead) return;
        health -= amount;
        FloatingText.Spawn(transform.position + Vector3.up * 0.4f, Mathf.RoundToInt(amount).ToString(), new Color(1f, 0.85f, 0.4f), 0.7f);
        if (sr != null) HitFlashFx.Flash(sr);
        if (health <= 0f) { Die(); return; }
        UnitAnim.Hurt(animator);
        if (species == Species.Boar && from != null)
        {
            enraged = from; enragedUntil = Time.time + 6f;
            FloatingText.Spawn(transform.position + Vector3.up * 0.6f, "Grrr !", new Color(1f, 0.5f, 0.35f), 0.9f);
        }
        else if (from != null)
        {
            fleeing = true;
            Vector2 away = ((Vector2)transform.position - (Vector2)from.position).normalized;
            GoTo((Vector2)transform.position + away * 7f);
        }
    }

    private void Charge(IDamageable target)
    {
        Vector2 pos = transform.position;
        Vector2 tp = ((Component)target).transform.position;
        Vector2 d = tp - pos;
        if (d.magnitude < 0.55f)
        {
            velocity = Vector2.zero;
            if (Time.time >= nextGore)
            {
                nextGore = Time.time + 1.1f;
                target.TakeDamage(9f);
                Sfx.Play("hit_claw", tp, 0.7f);
            }
            return;
        }
        velocity = d.normalized * runSpeed * 1.2f;
        transform.position = pos + velocity * Time.deltaTime;
        facing = velocity.normalized;
    }

    private void Die()
    {
        IsDead = true;
        health = 0f;
        velocity = Vector2.zero;
        path = null;
        Meat = species == Species.Deer || species == Species.Boar ? 3 : 1;
        rotUntil = Time.time + 240f;
        if (herd != null) herd.members--;
        Carcasses.Add(this);
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        if (UnitAnim.HasParam(animator, "Dead")) animator.SetBool("Dead", true);
        if (hasSpeed) animator.SetFloat("Speed", 0f);
        Sfx.Play("death_monster", transform.position, 0.4f);
    }

    /// <summary>Un chasseur prélève une portion (renvoie false s'il n'y a plus rien).</summary>
    public bool TakeMeat()
    {
        if (!IsDead || Meat <= 0) return false;
        Meat--;
        if (Meat <= 0) rotUntil = Mathf.Min(rotUntil, Time.time + 15f);
        return true;
    }

    private void UpdateCarcass()
    {
        if (Time.time < rotUntil) return;
        if (sr != null)
        {
            var c = sr.color;
            c.a -= Time.deltaTime * 0.5f;
            sr.color = c;
            if (c.a > 0f) return;
        }
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    // DÉPLACEMENT
    // ─────────────────────────────────────────────

    private static bool WalkableDry(Vector2 p)
    {
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph == null) return true;
        var nn = graph.GetNearest(p, NNConstraint.Default);
        return nn.node != null && nn.node.Walkable && IsDrySpot((Vector3)nn.node.position) &&
               (DungeonTerritory.Instance == null || !DungeonTerritory.Instance.IsInside(p));
    }

    private void GoTo(Vector2 target)
    {
        sleeping = false;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph != null)
        {
            var nn = graph.GetNearest(target, NNConstraint.Default);
            if (nn.node == null) return;
            var here = graph.GetNearest(transform.position, NNConstraint.Default).node;
            // Même île (pas de traversée vers un îlot), jamais au bord de la mer ni sur le ponton
            if (here != null && nn.node.Area != here.Area) return;
            target = (Vector3)nn.node.position;
            if (!IsDrySpot(target)) return;
        }
        // Les animaux n'entrent pas dans le donjon
        if (DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(target)) return;
        seeker.StartPath(transform.position, target, p => { if (!p.error) { path = p; wp = 0; } });
    }

    private static UnityEngine.Tilemaps.Tilemap seaMap, solMap;
    private static UnityEngine.Tilemaps.TileBase seaFloor;

    /// <summary>Case sèche : pas de mer dans un rayon d'une case et pas de ponton (fond marin sous les planches).
    /// Les berges des lacs restent permises : on y vient boire.</summary>
    public static bool IsDrySpot(Vector2 p)
    {
        if (seaMap == null || solMap == null)
        {
            foreach (var tm in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
            {
                if (tm.name == "NoPathfinding") seaMap = tm;
                else if (tm.name == "Sol") solMap = tm;
            }
            var art = SettlementArt.Get();
            seaFloor = art != null ? art.seaFloor : null;
            if (seaMap == null || solMap == null) return true;
        }
        var c = seaMap.WorldToCell(p);
        if (seaMap.HasTile(c)) return false;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var n = c + new Vector3Int(dx, dy, 0);
                var t = seaMap.GetTile(n);
                if (t != null && t.name.Contains("sea")) return false;
                if (seaFloor != null && solMap.GetTile(n) == seaFloor) return false;
            }
        return true;
    }

    private void Move(float speed)
    {
        velocity = Vector2.zero;
        if (path == null || wp >= path.vectorPath.Count) return;
        Vector2 pos = transform.position;
        Vector2 target = path.vectorPath[wp];
        if (Vector2.Distance(pos, target) < 0.2f) { wp++; return; }
        velocity = (target - pos).normalized * speed;
        transform.position = pos + velocity * Time.deltaTime;
        facing = velocity.normalized;
    }

    private void Animate()
    {
        if (animator == null) return;
        if (hasSpeed) animator.SetFloat("Speed", velocity.sqrMagnitude);
        if (hasX) animator.SetFloat("MoveX", facing.x);
        if (hasY) animator.SetFloat("MoveY", facing.y);
        if (!hasX && sr != null && Mathf.Abs(facing.x) > 0.1f) sr.flipX = facing.x < 0;
        // endormi : l'animation ralentit ; en train de boire : tête baissée (animation au repos plus lente)
        animator.speed = sleeping ? 0.25f : drinking ? 0.6f : 1f;
    }
}
