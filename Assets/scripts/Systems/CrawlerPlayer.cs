using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mode Aventurier : le personnage du joueur (guerrier, archer, mage ou soigneuse).
/// Clavier : ZQSD / WASD / flèches pour bouger, Espace ou clic pour attaquer, R compétence, F potion, E interagir.
/// Mobile : joystick virtuel et boutons du HUD (CrawlerMode).
/// Les monstres le détectent et le frappent comme n'importe quel intrus (couche des aventuriers, AdventurerCombat).
/// </summary>
public class CrawlerPlayer : MonoBehaviour
{
    public AdventurerAI.Role role;
    public float speed = 3.6f;
    public float baseHp, baseDmg;
    public float skillCooldown = 8f;

    public AdventurerCombat Combat { get; private set; }
    public float SkillReady => Mathf.Clamp01(1f - (nextSkill - Time.time) / skillCooldown);
    public Vector2 Facing => facing;
    public Vector2 MoveInput => move;

    // Commandes venant des boutons tactiles
    [HideInInspector] public bool touchAttack, touchSkill, touchPotion, touchInteract;

    private Rigidbody2D rb;
    private Animator anim;
    private Vector2 facing = Vector2.down, move;
    private float attackAnim, nextSkill;
    private bool dead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        Combat = GetComponent<AdventurerCombat>();
    }

    /// <summary>Statistiques de départ de la classe (modifiées ensuite par l'équipement et le rang).</summary>
    public void Setup(AdventurerAI.Role r)
    {
        role = r;
        Combat.role = r;
        switch (r)
        {
            case AdventurerAI.Role.Archer: baseHp = 150; baseDmg = 15; Combat.attackRange = 5f; Combat.attackCooldown = 0.7f; skillCooldown = 8f; break;
            case AdventurerAI.Role.Mage: baseHp = 125; baseDmg = 24; Combat.attackRange = 4.5f; Combat.attackCooldown = 1.1f; Combat.splashRadius = 1.3f; skillCooldown = 10f; break;
            case AdventurerAI.Role.Healer: baseHp = 170; baseDmg = 12; Combat.attackRange = 1.4f; Combat.attackCooldown = 0.7f; skillCooldown = 12f; break;
            default: baseHp = 230; baseDmg = 20; Combat.attackRange = 1.35f; Combat.attackCooldown = 0.55f; skillCooldown = 8f; break;
        }
        ApplyStats(1f, 1f, true);
    }

    public void ApplyStats(float hpMult, float dmgMult, bool heal)
    {
        float ratio = Combat.maxHealth > 0f ? Combat.currentHealth / Combat.maxHealth : 1f;
        Combat.maxHealth = baseHp * hpMult;
        Combat.attackDamage = baseDmg * dmgMult;
        Combat.currentHealth = heal ? Combat.maxHealth : Combat.maxHealth * ratio;
    }

    private void Update()
    {
        if (dead || CrawlerMode.Instance == null || CrawlerMode.Instance.InputBlocked) { move = Vector2.zero; return; }
        var kb = Keyboard.current;
        Vector2 dir = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.zKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
            // gauche : A (QWERTY) ou Q (AZERTY)
            if (kb.aKey.isPressed || kb.qKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;
        }
        var pad = Gamepad.current;
        if (pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.05f) dir = pad.leftStick.ReadValue();
        if (VirtualJoystick.Value.sqrMagnitude > 0.02f) dir = VirtualJoystick.Value;
        move = Vector2.ClampMagnitude(dir, 1f);
        if (move.sqrMagnitude > 0.01f) facing = move.normalized;

        bool attack = touchAttack || (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerUtil.IsOverUI() && !PointerUtil.IsTouch) attack = true;
        bool skill = touchSkill || (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
        bool potion = touchPotion || (kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
        bool interact = touchInteract || (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
        touchAttack = touchSkill = touchPotion = touchInteract = false;

        if (attack) Attack();
        if (skill) Skill();
        if (potion) CrawlerMode.Instance.UsePotion();
        if (interact) CrawlerMode.Instance.Interact();
        if (attackAnim > 0f) attackAnim -= Time.deltaTime;
        // pas de récupération automatique : potions, prières, soigneurs ou repos à la taverne
        Animate();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        rb.linearVelocity = dead ? Vector2.zero : move * speed;
    }

    private void Animate()
    {
        if (anim == null) return;
        anim.SetFloat("Speed", move.sqrMagnitude);
        anim.SetFloat("MoveX", facing.x);
        anim.SetFloat("MoveY", facing.y);
        anim.SetBool("IsAttacking", attackAnim > 0f);
    }

    // ─────────────────────────────────────────────
    // COMBAT
    // ─────────────────────────────────────────────

    private static bool Visible(Vector2 a, Vector2 b) => AdventurerAI.ClearLine(a, b, 1 << 6);

    /// <summary>Cible : le monstre (ou le cristal) le plus proche à portée, de préférence devant soi.</summary>
    private IDamageable FindTarget(float range, out Transform t)
    {
        t = null;
        IDamageable best = null;
        float bestScore = float.MaxValue;
        foreach (var g in GobelinAI.All)
        {
            if (g == null || !g.IsAlive()) continue;
            Vector2 to = (Vector2)g.transform.position - rb.position;
            float d = to.magnitude;
            if (d > range || !Visible(rb.position, g.transform.position)) continue;
            float score = d + (Vector2.Angle(facing, to) > 100f ? 2f : 0f);
            if (score < bestScore) { bestScore = score; best = g; t = g.transform; }
        }
        if (best == null)
        {
            var core = CrawlerMode.Instance != null ? CrawlerMode.Instance.Core : null;
            if (core != null && core.currentHealth > 0f && Vector2.Distance(core.transform.position, rb.position) <= range + 0.6f &&
                Visible(rb.position, core.transform.position)) { best = core; t = core.transform; }
        }
        return best;
    }

    private void Attack()
    {
        if (Time.time - Combat.lastAttackTime < Combat.attackCooldown) return;
        var target = FindTarget(Combat.attackRange + 0.3f, out var t);
        attackAnim = 0.35f;
        if (target == null)
        {
            Combat.lastAttackTime = Time.time;
            if (role == AdventurerAI.Role.Warrior || role == AdventurerAI.Role.Healer) Sfx.Play("swing", transform.position, 0.6f);
            return;
        }
        Vector2 to = (Vector2)t.position - rb.position;
        if (to.sqrMagnitude > 0.001f) facing = to.normalized;
        Combat.Attack(target);
        if (target is GobelinAI g) g.OnAttackedBy(transform);
    }

    private void Skill()
    {
        if (Time.time < nextSkill) return;
        nextSkill = Time.time + skillCooldown;
        attackAnim = 0.5f;
        switch (role)
        {
            case AdventurerAI.Role.Warrior:
                // Tourbillon : frappe tout ce qui est autour
                Fx.Play("smoke_ring", transform.position, 2.4f, 16f, 16, new Color(1f, 0.9f, 0.7f, 0.8f));
                Sfx.Play("swing", transform.position);
                foreach (var g in new List<GobelinAI>(GobelinAI.All))
                    if (g != null && g.IsAlive() && Vector2.Distance(g.transform.position, rb.position) <= 1.9f) { g.TakeDamage(Combat.attackDamage * 1.6f); g.OnAttackedBy(transform); }
                break;
            case AdventurerAI.Role.Archer:
            {
                // Volée : trois flèches sur les trois cibles les plus proches
                var list = new List<GobelinAI>();
                foreach (var g in GobelinAI.All) if (g != null && g.IsAlive() && Vector2.Distance(g.transform.position, rb.position) <= 6f && Visible(rb.position, g.transform.position)) list.Add(g);
                list.Sort((a, b) => Vector2.Distance(a.transform.position, rb.position).CompareTo(Vector2.Distance(b.transform.position, rb.position)));
                for (int i = 0; i < Mathf.Min(3, list.Count); i++)
                {
                    var g = list[i];
                    Projectile.FireFx("arrow", transform.position + Vector3.up * 0.35f, g.transform.position + Vector3.up * 0.25f, 0.55f, true,
                        () => { if (g != null && g.IsAlive()) { g.TakeDamage(Combat.attackDamage * 1.3f); g.OnAttackedBy(transform); } }, 18f, 0.1f);
                }
                Sfx.Play("arrow_shoot", transform.position);
                break;
            }
            case AdventurerAI.Role.Mage:
                // Nova de feu autour de soi
                Fx.Play("explosion", transform.position, 3.2f, 18f, 16, null, false, null, new Color(1f, 0.55f, 0.2f));
                Sfx.Play("explosion", transform.position);
                CameraController.Instance?.Shake(0.1f, 0.3f);
                foreach (var g in new List<GobelinAI>(GobelinAI.All))
                    if (g != null && g.IsAlive() && Vector2.Distance(g.transform.position, rb.position) <= 2.6f) { g.TakeDamage(Combat.attackDamage * 1.8f); g.OnAttackedBy(transform); }
                break;
            default:
                // Prière : soins importants
                float heal = Combat.maxHealth * 0.45f;
                Combat.currentHealth = Mathf.Min(Combat.maxHealth, Combat.currentHealth + heal);
                Fx.Play("heal", transform.position, 1.6f, 14f, 16, null, false, transform, new Color(0.4f, 1f, 0.5f));
                Sfx.Play("heal", transform.position);
                FloatingText.Spawn(transform.position + Vector3.up * 0.8f, $"+{Mathf.RoundToInt(heal)}", new Color(0.45f, 1f, 0.5f), 1.2f);
                break;
        }
    }

    public void OnDied()
    {
        if (dead) return;
        dead = true;
        if (anim != null && UnitAnim.HasParam(anim, "Dead")) anim.SetTrigger("Dead");
        CrawlerMode.Instance?.OnPlayerDied();
    }

    public void Revive(Vector3 at)
    {
        dead = false;
        transform.position = at;
        rb.position = at;
        Combat.currentHealth = Combat.maxHealth;
        Combat.enabled = true;
        if (anim != null) { anim.Rebind(); anim.Update(0f); }
    }

    public bool IsDead => dead;
}
