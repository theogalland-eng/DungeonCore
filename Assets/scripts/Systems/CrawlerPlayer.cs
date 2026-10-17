using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mode Aventurier : le personnage du joueur (guerrier, archer, mage ou soigneuse).
/// Clavier : ZQSD / WASD / flèches pour bouger (Maj : courir), Espace ou clic pour attaquer, Ctrl ou clic droit : roulade,
/// R compétence, F potion, E interagir.
/// Mobile : joystick virtuel (poussé à fond : course) et boutons du HUD (CrawlerMode).
/// La course et la roulade consomment l'endurance, qui revient quand on souffle un peu.
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
    [HideInInspector] public bool touchAttack, touchSkill, touchPotion, touchInteract, touchRoll;

    // ── Endurance ──
    public float maxStamina = 100f;
    public float Stamina { get; private set; } = 100f;
    public float Stamina01 => Mathf.Clamp01(Stamina / maxStamina);
    /// <summary>La faim ralentit la récupération (CrawlerMode).</summary>
    [System.NonSerialized] public float StaminaRegenMult = 1f;
    [System.NonSerialized] public bool touchEat;
    /// <summary>À bout de souffle : plus de course tant que la jauge n'est pas remontée à 35 %.</summary>
    public bool Exhausted { get; private set; }
    public bool Running { get; private set; }
    public bool Rolling => Time.time < rollUntil;
    // ~3 s de course, une roulade = un tiers de la jauge ; il faut ~8 s pour la remplir après 1,3 s de souffle
    const float RunCost = 32f, RollCost = 34f, StaminaRegen = 12f, RegenDelay = 1.3f, RunMult = 1.5f;
    const float RollTime = 0.36f, RollSpeed = 8.5f, RollCooldown = 0.45f;

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private Vector2 facing = Vector2.down, move, rollDir;
    private float attackAnim, nextSkill, rollUntil, nextRoll, lastEffort;
    private bool dead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        Combat = GetComponent<AdventurerCombat>();
        look = gameObject.AddComponent<HeroLook>();
    }

    private HeroLook look;

    // ─────────────────────────────────────────────
    // GESTES : accroupi (fouiller, prier) ou bras tendu (ouvrir, boire)
    // ─────────────────────────────────────────────

    private float gestureUntil;
    public bool InGesture => Time.time < gestureUntil;

    /// <summary>Joue un geste (le héros s'arrête), puis exécute l'action à mi-geste. Un objet peut s'afficher au-dessus (potion...).</summary>
    public void Gesture(bool crouch, float duration, System.Action done = null, Sprite prop = null)
    {
        if (dead) { done?.Invoke(); return; }
        string st = crouch ? "Crouch" : "Use";
        gestureUntil = Time.time + duration;
        attackAnim = 0f;
        if (HasState(st)) anim.Play(st, 0, 0f);
        StartCoroutine(GestureRoutine(duration, done, prop));
    }

    private System.Collections.IEnumerator GestureRoutine(float duration, System.Action done, Sprite prop)
    {
        GameObject p = null;
        if (prop != null)
        {
            p = new GameObject("Geste");
            p.transform.SetParent(transform, false);
            var r = p.AddComponent<SpriteRenderer>();
            r.sprite = prop;
            r.sortingOrder = sr != null ? sr.sortingOrder + 1 : 10;
            float h = prop.bounds.size.y;
            p.transform.localScale = Vector3.one * (h > 0f ? 0.32f / h : 1f);
        }
        float half = duration * 0.55f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (p != null)
                // la fiole monte à la bouche puis bascule
                p.transform.localPosition = new Vector3(facing.x * 0.12f, 0.35f + 0.25f * Mathf.Sin(Mathf.Clamp01(t / half) * Mathf.PI * 0.5f), -0.01f);
            if (p != null) p.transform.localRotation = Quaternion.Euler(0f, 0f, -70f * Mathf.Clamp01((t - half) / (duration - half)) * Mathf.Sign(facing.x == 0 ? 1 : facing.x));
            if (done != null && t >= half) { var d = done; done = null; d(); }
            yield return null;
        }
        done?.Invoke();
        if (p != null) Destroy(p);
        if (!dead && anim != null && HasState("Idle")) anim.Play("Idle", 0, 0f);
    }

    /// <summary>Nom choisi à la création (ou nom de classe).</summary>
    public void ApplyLook()
    {
        look?.Refresh();
    }

    private bool HasState(string s) => anim != null && anim.HasState(0, Animator.StringToHash(s));

    /// <summary>Palier d'armure visible (1 à 9) : l'apparence suit l'équipement acheté à la forge.</summary>
    public int Tier { get; private set; }

    public void SetTier(int t)
    {
        t = Mathf.Clamp(t, 1, 9);
        if (t == Tier || anim == null) return;
        var cat = DungeonCatalog.Get();
        var list = cat == null ? null : role == AdventurerAI.Role.Archer ? cat.lookArcher : role == AdventurerAI.Role.Mage ? cat.lookMage :
                   role == AdventurerAI.Role.Healer ? cat.lookHealer : cat.lookWarrior;
        if (list == null || list.Count < t || list[t - 1] == null) return;
        bool first = Tier == 0;
        Tier = t;
        anim.runtimeAnimatorController = list[t - 1];
        anim.Update(0f);
        if (!first)
        {
            // nouvelle armure : éclat doré
            Fx.Play("shine", transform.position + Vector3.up * 0.3f, 1.4f, 16f, 20, new Color(1f, 0.9f, 0.55f));
            Sfx.Play("levelup", transform.position, 0.5f);
        }
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
        if (Rolling) { move = rollDir; Animate(); return; }
        if (InGesture) { move = Vector2.zero; Animate(); return; }
        if (move.sqrMagnitude > 0.01f) facing = move.normalized;

        // course : Maj, gâchette, ou joystick poussé à fond
        bool runHeld = (kb != null && kb.leftShiftKey.isPressed) || (pad != null && pad.leftStickButton.isPressed) ||
                       VirtualJoystick.Value.magnitude > 0.92f;
        UpdateStamina(runHeld);

        bool roll = touchRoll || (kb != null && (kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame)) ||
                    (pad != null && pad.rightShoulder.wasPressedThisFrame);
        var mouseR = Mouse.current;
        if (mouseR != null && mouseR.rightButton.wasPressedThisFrame && !PointerUtil.IsOverUI() && !PointerUtil.IsTouch) roll = true;
        touchRoll = false;
        if (roll) { Roll(); if (Rolling) { Animate(); return; } }

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
        bool eat = touchEat || (kb != null && kb.gKey.wasPressedThisFrame);
        touchEat = false;
        if (eat) CrawlerMode.Instance.Eat();
        if (interact) CrawlerMode.Instance.Interact();
        if (attackAnim > 0f) attackAnim -= Time.deltaTime;
        // pas de récupération automatique : potions, prières, soigneurs ou repos à la taverne
        Animate();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        if (dead) { rb.linearVelocity = Vector2.zero; return; }
        if (Rolling)
        {
            // élan au départ, freinage à la fin
            float k = Mathf.Clamp01((rollUntil - Time.time) / RollTime);
            rb.linearVelocity = rollDir * RollSpeed * (0.45f + 0.55f * k);
            return;
        }
        rb.linearVelocity = move * speed * (Running ? RunMult : 1f);
    }

    // ─────────────────────────────────────────────
    // ENDURANCE, COURSE, ROULADE
    // ─────────────────────────────────────────────

    private void UpdateStamina(bool runHeld)
    {
        Running = runHeld && !Exhausted && move.sqrMagnitude > 0.2f && Stamina > 0f;
        if (Running)
        {
            Stamina = Mathf.Max(0f, Stamina - RunCost * Time.deltaTime);
            lastEffort = Time.time;
            if (Stamina <= 0f) Exhausted = true;
        }
        else if (Time.time - lastEffort > RegenDelay)
            Stamina = Mathf.Min(maxStamina, Stamina + StaminaRegen * StaminaRegenMult * Time.deltaTime);
        if (Exhausted && Stamina >= maxStamina * 0.35f) Exhausted = false;
    }

    private void Roll()
    {
        if (Rolling || Time.time < nextRoll) return;
        if (Stamina < RollCost * 0.5f)
        {
            FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "À bout de souffle", new Color(0.8f, 0.8f, 0.9f), 0.8f);
            nextRoll = Time.time + 0.4f;
            return;
        }
        Stamina = Mathf.Max(0f, Stamina - RollCost);
        lastEffort = Time.time;
        if (Stamina <= 0f) Exhausted = true;
        rollDir = move.sqrMagnitude > 0.01f ? move.normalized : facing;
        facing = rollDir;
        rollUntil = Time.time + RollTime;
        nextRoll = rollUntil + RollCooldown;
        Combat.invulnerableUntil = Time.time + RollTime * 0.9f;
        attackAnim = 0f;
        if (HasState("Roll")) anim.Play("Roll", 0, 0f);
        Fx.Play("smoke_circle", transform.position + new Vector3(0f, -0.25f, 0f), 0.7f, 18f, 14, new Color(0.75f, 0.7f, 0.62f, 0.7f));
        Sfx.Play("swing", transform.position, 0.35f);
        StartCoroutine(AfterImages());
    }

    /// <summary>Images rémanentes bleutées pendant la roulade.</summary>
    private System.Collections.IEnumerator AfterImages()
    {
        while (Rolling && sr != null)
        {
            var ghost = new GameObject("Rémanence");
            ghost.transform.position = transform.position;
            ghost.transform.localScale = transform.lossyScale;
            var g = ghost.AddComponent<SpriteRenderer>();
            g.sprite = sr.sprite;
            g.flipX = sr.flipX;
            g.sortingLayerID = sr.sortingLayerID;
            g.sortingOrder = sr.sortingOrder - 1;
            StartCoroutine(FadeGhost(g));
            yield return new WaitForSeconds(0.05f);
        }
    }

    private static System.Collections.IEnumerator FadeGhost(SpriteRenderer g)
    {
        for (float t = 0f; t < 1f && g != null; t += Time.deltaTime / 0.25f)
        {
            g.color = new Color(0.55f, 0.8f, 1f, 0.5f * (1f - t));
            yield return null;
        }
        if (g != null) Destroy(g.gameObject);
    }

    private void Animate()
    {
        if (anim == null) return;
        anim.SetFloat("Speed", move.sqrMagnitude);
        anim.SetFloat("MoveX", facing.x);
        anim.SetFloat("MoveY", facing.y);
        anim.SetBool("IsAttacking", attackAnim > 0f);
        if (UnitAnim.HasParam(anim, "Running")) anim.SetBool("Running", Running);
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
        // le gibier aussi (chasser pour manger)
        if (best == null)
            foreach (var a in WildAnimal.All)
            {
                if (a == null || a.IsDead) continue;
                Vector2 to = (Vector2)a.transform.position - rb.position;
                float d = to.magnitude;
                if (d > range || !Visible(rb.position, a.transform.position)) continue;
                float score = d + (Vector2.Angle(facing, to) > 100f ? 2f : 0f);
                if (score < bestScore) { bestScore = score; best = a; t = a.transform; }
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
        if (Rolling || Time.time - Combat.lastAttackTime < Combat.attackCooldown) return;
        var target = FindTarget(Combat.attackRange + 0.3f, out var t);
        // en mouvement, il frappe sans s'arrêter (attaque en marchant ou en courant)
        string moving = move.sqrMagnitude > 0.05f ? (Running ? "RunAttack" : "WalkAttack") : null;
        if (moving != null && HasState(moving)) { attackAnim = 0f; anim.Play(moving, 0, 0f); }
        else attackAnim = 0.35f;
        if (target == null)
        {
            Combat.lastAttackTime = Time.time;
            if (role == AdventurerAI.Role.Warrior || role == AdventurerAI.Role.Healer) Sfx.Play("swing", transform.position, 0.6f);
            return;
        }
        Vector2 to = (Vector2)t.position - rb.position;
        if (to.sqrMagnitude > 0.001f) facing = to.normalized;
        Combat.Attack(target);
        if (target is GobelinAI g)
        {
            g.OnAttackedBy(transform);
            bool melee = role == AdventurerAI.Role.Warrior || role == AdventurerAI.Role.Healer;
            if (melee) HitFlashFx.Sparks(Vector3.Lerp(transform.position, t.position, 0.7f) + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.7f), 0.5f);
            if (!g.IsAlive())
            {
                HitFlashFx.Hitstop(0.09f);
                CameraController.Instance?.Shake(0.07f, 0.18f);
            }
            else if (melee) HitFlashFx.Hitstop(0.035f);
        }
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
                // Prière : à genoux, soins importants
                Gesture(true, 0.9f);
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
        if (anim != null && UnitAnim.HasParam(anim, "Dead")) anim.SetBool("Dead", true);
        CrawlerMode.Instance?.OnPlayerDied();
    }

    public void Revive(Vector3 at)
    {
        dead = false;
        transform.position = at;
        rb.position = at;
        Combat.currentHealth = Combat.maxHealth;
        Combat.enabled = true;
        Stamina = maxStamina;
        Exhausted = false;
        if (anim != null) { anim.Rebind(); anim.Update(0f); }
    }

    public bool IsDead => dead;
}
