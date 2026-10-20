using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// Mode Aventurier : un compagnon recruté au hall de la Guilde. Il suit le joueur (chemin A* dans les couloirs),
/// attaque les monstres en vue et, s'il est soigneur, soigne le groupe. Mort, il est perdu.
/// </summary>
public class CrawlerCompanion : MonoBehaviour
{
    public static readonly List<CrawlerCompanion> All = new List<CrawlerCompanion>();

    public AdventurerAI.Role role;
    public string displayName, classLabel;
    public bool female;
    public int rank;
    public int slot;
    public float speed = 3.4f;

    public AdventurerCombat Combat { get; private set; }
    public bool IsDead => dead;

    private Rigidbody2D rb;
    private Animator anim;
    private Seeker seeker;
    private Path path;
    private int wp;
    private float nextRepath, nextHeal, attackAnim, stuckT;
    private Vector2 lastDest, move, facing = Vector2.down, lastPos;
    private bool dead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        seeker = GetComponent<Seeker>();
        Combat = GetComponent<AdventurerCombat>();
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public void Setup(AdventurerAI.Role r, string name, int rk, float hp, float dmg)
    {
        role = r;
        displayName = name;
        rank = rk;
        Combat.role = r;
        Combat.adventurerName = name;
        switch (r)
        {
            case AdventurerAI.Role.Archer: Combat.attackRange = 4.8f; Combat.attackCooldown = 0.9f; break;
            case AdventurerAI.Role.Mage: Combat.attackRange = 4.3f; Combat.attackCooldown = 1.3f; Combat.splashRadius = 1.1f; break;
            case AdventurerAI.Role.Healer: Combat.attackRange = 1.3f; Combat.attackCooldown = 0.9f; break;
            default: Combat.attackRange = 1.3f; Combat.attackCooldown = 0.75f; break;
        }
        Combat.maxHealth = hp;
        Combat.currentHealth = hp;
        Combat.attackDamage = dmg;
    }

    private Transform Leader => CrawlerMode.Instance != null && CrawlerMode.Instance.Player != null ? CrawlerMode.Instance.Player.transform : null;

    private void Update()
    {
        move = Vector2.zero;
        var mode = CrawlerMode.Instance;
        var leader = Leader;
        if (dead || leader == null || mode == null || mode.Frozen) { Animate(); return; }
        Vector2 me = rb.position, lp = leader.position;

        // trop loin (téléporté, escalier, porte) : il rejoint le groupe
        if (Vector2.Distance(me, lp) > 14f) { WarpTo(lp + Offset()); return; }

        if (attackAnim > 0f) attackAnim -= Time.deltaTime;
        if (role == AdventurerAI.Role.Healer && TryHeal()) { Animate(); return; }

        var target = FindTarget(me, lp);
        bool ranged = role == AdventurerAI.Role.Archer || role == AdventurerAI.Role.Mage;
        // blessé (et pas guerrier) : il se replie derrière le héros
        if (target != null && role != AdventurerAI.Role.Warrior && Combat.currentHealth < Combat.maxHealth * 0.35f)
        {
            Vector2 away = (lp - (Vector2)target.transform.position).normalized;
            Vector2 safe = lp + away * 1.8f;
            if (Vector2.Distance(me, safe) > 0.6f) { Steer(safe); Animate(); return; }
        }
        if (target != null)
        {
            Vector2 tp = target.transform.position;
            float d = Vector2.Distance(me, tp);
            facing = (tp - me).normalized;
            // tireurs et soigneuse : garder ses distances
            if ((ranged || role == AdventurerAI.Role.Healer) && d < 1.8f)
            {
                Vector2 back = me + (me - tp).normalized * 1.2f;
                if (AdventurerAI.ClearLine(me, back, 1 << 6)) { move = (back - me).normalized; Animate(); return; }
            }
            if (role == AdventurerAI.Role.Healer && d > Combat.attackRange) { Vector2 slotPos2 = lp + Offset(); if (Vector2.Distance(me, slotPos2) > 1.2f) Steer(slotPos2); Animate(); return; }
            if (d <= Combat.attackRange && AdventurerAI.ClearLine(me, tp, 1 << 6))
            {
                if (Combat.CanAttack()) { Combat.Attack(target); target.OnAttackedBy(transform); attackAnim = 0.35f; }
            }
            else Steer(tp);
        }
        else
        {
            Vector2 slotPos = lp + Offset();
            if (Vector2.Distance(me, lp) > 1.9f || Vector2.Distance(me, slotPos) > 1.2f) Steer(slotPos);
        }
        Animate();
    }

    /// <summary>Place dans la formation : le guerrier devant le héros, les tireurs derrière sur les côtés,
    /// la soigneuse au centre, en retrait.</summary>
    private Vector2 Offset()
    {
        var p = CrawlerMode.Instance.Player;
        Vector2 f = p != null ? p.Facing : Vector2.down;
        Vector2 side = new Vector2(-f.y, f.x);
        float s = slot == 0 ? -0.8f : slot == 1 ? 0.8f : 0f;
        switch (role)
        {
            case AdventurerAI.Role.Warrior: return f * 0.9f + side * (slot == 1 ? -0.6f : 0.6f);
            case AdventurerAI.Role.Healer: return -f * 1.8f + side * s * 0.4f;
            default: return -f * 1.1f + side * (s == 0f ? 0.8f : s);
        }
    }

    private GobelinAI FindTarget(Vector2 me, Vector2 lp)
    {
        // le guerrier protège : priorité au monstre qui s'en prend à un tireur, à la soigneuse ou au héros
        if (role == AdventurerAI.Role.Warrior)
        {
            GobelinAI threat = null;
            float td = 6f;
            foreach (var g in GobelinAI.All)
            {
                if (g == null || !g.IsAlive() || g.MissionTarget == null || g.MissionTarget == transform) continue;
                var mate = g.MissionTarget.GetComponent<CrawlerCompanion>();
                bool onWeak = (mate != null && mate.role != AdventurerAI.Role.Warrior) || g.MissionTarget.GetComponent<CrawlerPlayer>() != null;
                if (!onWeak) continue;
                float d = Vector2.Distance(me, g.transform.position);
                if (d < td && AdventurerAI.ClearLine(me, g.transform.position, 1 << 6)) { td = d; threat = g; }
            }
            if (threat != null) return threat;
        }
        GobelinAI best = null;
        float bestD = float.MaxValue;
        float reach = role == AdventurerAI.Role.Archer || role == AdventurerAI.Role.Mage ? 6f : 5f;
        foreach (var g in GobelinAI.All)
        {
            if (g == null || !g.IsAlive()) continue;
            Vector2 gp = g.transform.position;
            float d = Vector2.Distance(me, gp);
            if (d > reach || Vector2.Distance(lp, gp) > 8f || !AdventurerAI.ClearLine(me, gp, 1 << 6)) continue;
            if (d < bestD) { bestD = d; best = g; }
        }
        return best;
    }

    private bool TryHeal()
    {
        if (Time.time < nextHeal) return false;
        AdventurerCombat worst = null;
        float ratio = 0.65f;
        void Consider(AdventurerCombat c)
        {
            if (c == null || c.currentHealth <= 0f || Vector2.Distance(c.transform.position, rb.position) > 5f) return;
            float r = c.currentHealth / c.maxHealth;
            if (r < ratio) { ratio = r; worst = c; }
        }
        Consider(CrawlerMode.Instance.Player != null ? CrawlerMode.Instance.Player.Combat : null);
        foreach (var c in All) if (c != null && !c.dead) Consider(c.Combat);
        if (worst == null) return false;
        nextHeal = Time.time + 4.5f;
        float heal = Combat.attackDamage * 2.2f + worst.maxHealth * 0.12f;
        worst.currentHealth = Mathf.Min(worst.maxHealth, worst.currentHealth + heal);
        attackAnim = 0.45f;
        facing = ((Vector2)worst.transform.position - rb.position).normalized;
        Fx.Play("heal", worst.transform.position, 1.4f, 14f, 16, null, false, worst.transform, new Color(0.4f, 1f, 0.5f));
        Sfx.Play("heal", worst.transform.position, 0.7f);
        FloatingText.Spawn(worst.transform.position + Vector3.up * 0.8f, $"+{Mathf.RoundToInt(heal)}", new Color(0.45f, 1f, 0.5f), 1.1f);
        return true;
    }

    /// <summary>Se dirige vers un point : en ligne droite s'il est en vue, sinon par le chemin A*.</summary>
    private void Steer(Vector2 dest)
    {
        Vector2 me = rb.position;
        if (AdventurerAI.ClearLine(me, dest, 1 << 6)) { move = (dest - me).normalized; path = null; }
        else
        {
            if (seeker != null && Time.time >= nextRepath && seeker.IsDone() && (path == null || Vector2.Distance(dest, lastDest) > 0.6f))
            {
                nextRepath = Time.time + 0.4f;
                lastDest = dest;
                seeker.StartPath(me, dest, p => { if (!p.error) { path = p; wp = AdventurerAI.FirstWaypoint(p, rb.position); } });
            }
            if (path != null && path.vectorPath != null && wp < path.vectorPath.Count)
            {
                Vector2 w = path.vectorPath[wp];
                if (Vector2.Distance(me, w) < AdventurerAI.MaxWaypointReach) wp++;
                if (wp < path.vectorPath.Count) move = ((Vector2)path.vectorPath[wp] - me).normalized;
            }
        }
        if (move.sqrMagnitude > 0.01f) facing = move;
        // coincé : on se recentre sur sa case
        stuckT = Vector2.Distance(me, lastPos) < 0.01f && move.sqrMagnitude > 0.01f ? stuckT + Time.deltaTime : 0f;
        lastPos = me;
        if (stuckT > 1.2f) { stuckT = 0f; path = null; move = ((Vector2)new Vector3(Mathf.Floor(me.x) + 0.5f, Mathf.Floor(me.y) + 0.5f) - me).normalized; }
    }

    private void FixedUpdate()
    {
        if (rb != null) rb.linearVelocity = dead ? Vector2.zero : move * speed;
    }

    private void Animate()
    {
        if (anim == null) return;
        anim.SetFloat("Speed", move.sqrMagnitude);
        anim.SetFloat("MoveX", facing.x);
        anim.SetFloat("MoveY", facing.y);
        anim.SetBool("IsAttacking", attackAnim > 0f);
    }

    public void WarpTo(Vector2 p)
    {
        transform.position = p;
        if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
        path = null;
    }

    public void OnDied()
    {
        if (dead) return;
        dead = true;

        if (anim != null && UnitAnim.HasParam(anim, "Dead")) anim.SetTrigger("Dead");
        CrawlerMode.Instance?.OnCompanionDied(this);
        Destroy(gameObject, 2.5f);
    }
}
