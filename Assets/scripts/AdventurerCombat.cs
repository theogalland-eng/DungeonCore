using System.Collections;
using UnityEngine;

/// <summary>
/// Stats et combat des aventuriers qui attaquent le donjon.
/// </summary>
public class AdventurerCombat : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public string adventurerName = "Adventurer";
    public float maxHealth = 100f;
    public float currentHealth;
    public float attackDamage = 10f;
    public float attackCooldown = 1f;
    [HideInInspector] public float lastAttackTime = -999f;

    [Header("Attack")]
    public float attackRange = 1.5f;
    [Tooltip("Rôle du combattant : les archers et les mages attaquent à distance")]
    public AdventurerAI.Role role = AdventurerAI.Role.Warrior;
    [Tooltip("Rayon d'explosion des sorts (mage)")]
    public float splashRadius;

    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private Coroutine flashRoutine;
    private AdventurerAI ai;
    public AdventurerAI AI => ai;

    private void Awake()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        ai = GetComponent<AdventurerAI>();
    }

    /// <summary>Teinte selon le type (en attendant des sprites dédiés).</summary>
    public void SetTint(Color tint)
    {
        baseColor = tint;
        if (spriteRenderer != null) spriteRenderer.color = tint;
    }

    public bool IsAlive() => currentHealth > 0f;
    public float GetCurrentHealth() => currentHealth;
    public string GetName() => adventurerName;

    /// <summary>Jusqu'à cet instant, les coups ne portent pas (roulade du héros).</summary>
    [System.NonSerialized] public float invulnerableUntil;

    // Modificateurs d'humeur et d'état (AdventurerLife) : ivresse, faim, chance...
    [System.NonSerialized] public float damageMult = 1f;
    [System.NonSerialized] public float missChance;
    [System.NonSerialized] public float dodgeChance;
    [System.NonSerialized] public float critChance;
    /// <summary>Dégâts subis (talents défensifs du mode Aventurier) et vol de vie.</summary>
    [System.NonSerialized] public float damageTakenMult = 1f, lifeSteal;
    /// <summary>Voix (Voices) : -1 à déterminer, 0 homme, 1 femme.</summary>
    [System.NonSerialized] public int voice = -1;

    // armure rongée par l'acide (goule) : dégâts subis majorés un moment
    private float corrodeMult = 1f, corrodeUntil;
    public void Corrode(float mult, float seconds)
    {
        if (Time.time >= corrodeUntil || mult > corrodeMult) corrodeMult = mult;
        corrodeUntil = Time.time + seconds;
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;
        if (Time.time < invulnerableUntil)
        {
            FloatingText.Spawn(transform.position + Vector3.up * 0.6f, "Esquive !", new Color(0.7f, 0.9f, 1f), 0.8f);
            return;
        }
        if (dodgeChance > 0f && Random.value < dodgeChance)
        {
            FloatingText.Spawn(transform.position + Vector3.up * 0.6f, "Esquive !", new Color(0.7f, 0.9f, 1f), 0.8f);
            return;
        }

        amount *= damageTakenMult * (Time.time < corrodeUntil ? corrodeMult : 1f);
        currentHealth -= amount;
        if (currentHealth > 0f) Voices.Human(this, "hurt", 0.45f);
        if (GetComponent<CrawlerPlayer>() != null) Haptics.Pulse();
        Sfx.Play("hit_flesh", transform.position, 0.6f);
        FloatingText.Spawn(transform.position + Vector3.up * 0.5f, Mathf.RoundToInt(amount).ToString(), new Color(1f, 0.85f, 0.4f), 0.9f);

        if (spriteRenderer != null && gameObject.activeInHierarchy)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
            HitFlashFx.Flash(spriteRenderer);
        }

        if (currentHealth <= 0f) Die();
        else
        {
            UnitAnim.Hurt(GetComponent<Animator>());
            if (ai != null) ai.OnDamaged(amount);
        }
    }

    private IEnumerator Flash()
    {
        spriteRenderer.color = new Color(1f, 0.35f, 0.35f);
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = baseColor;
        flashRoutine = null;
    }

    private void Die()
    {
        // Mode Aventurier : c'est le joueur qui tombe (il se réveillera à la taverne)
        var player = GetComponent<CrawlerPlayer>();
        if (player != null) { currentHealth = 0f; Voices.Human(this, "death"); player.OnDied(); return; }
        var mate = GetComponent<CrawlerCompanion>();
        if (mate != null) { currentHealth = 0f; Voices.Human(this, "death"); mate.OnDied(); enabled = false; return; }
        // Captures activées et cellule libre : assommé plutôt que tué
        if (ai != null && PrisonSystem.Instance != null && PrisonSystem.Instance.TryCapture(ai)) { currentHealth = Mathf.Max(1f, currentHealth); return; }
        DLog.Log($"{adventurerName} est mort");
        Voices.Human(this, "death");
        if (ai != null) ai.HandleDeath();
        else PlayerLevel.Instance?.OnAdventurerKilled();
        enabled = false;
        UnitAnim.Die(gameObject, ai);
    }

    public bool CanAttack() => Time.time - lastAttackTime >= attackCooldown;

    /// <summary>Attaque n'importe quelle cible qui implémente IDamageable.</summary>
    public void Attack(IDamageable target)
    {
        if (!CanAttack() || target == null || !target.IsAlive()) return;
        lastAttackTime = Time.time;
        var targetT = (target as Component)?.transform;
        Vector3 from = transform.position + Vector3.up * 0.35f;
        Vector3 to = targetT != null ? targetT.position + Vector3.up * 0.25f : from;
        switch (role)
        {
            case AdventurerAI.Role.Archer:
                // la flèche part, les dégâts tombent à l'impact
                Sfx.Play("arrow_shoot", from, 0.8f);
                Projectile.FireFx("arrow", from, to, 0.55f, true, () => { Sfx.Play("arrow_hit", to, 0.8f); if (target.IsAlive()) Hit(target); }, 16f, 0.12f);
                break;
            case AdventurerAI.Role.Mage:
                Sfx.Play("fireball", from, 0.8f);
                Projectile.FireFx("fireball", from, to, 0.55f, false, () =>
                {
                    Sfx.Play("explosion", to, 0.7f);
                    Fx.Play("explosion", to, 1.3f, 18f, 16, null, false, null, new Color(1f, 0.55f, 0.2f));
                    if (target.IsAlive()) Hit(target);
                    if (splashRadius > 0f)
                        foreach (var g in GobelinAI.All.ToArray())
                            if (g != null && g.IsAlive() && (object)g != target && Vector2.Distance(g.transform.position, to) <= splashRadius
                                && AdventurerAI.ClearLine(to, g.transform.position, 1 << 6))
                                g.TakeDamage(attackDamage * 0.5f);
                }, 9f, 0.2f, 10, new Color(1f, 0.55f, 0.2f));
                break;
            default:
                Sfx.Play("swing", from, 0.6f);
                Hit(target);
                Sfx.Play("hit_sword", to, 0.8f);
                break;
        }
    }

    private void Hit(IDamageable target)
    {
        if (missChance > 0f && Random.value < missChance)
        {
            var t = (target as Component)?.transform;
            FloatingText.Spawn((t != null ? t.position : transform.position) + Vector3.up * 0.5f, "Raté", new Color(0.75f, 0.72f, 0.8f), 0.8f);
            return;
        }
        float dmg = attackDamage * damageMult;
        if (ai != null && ai.IsInsideDungeon) dmg *= Talents.IntruderDamageMult;
        if (critChance > 0f && Random.value < critChance)
        {
            dmg *= 2f;
            var tt = (target as Component)?.transform;
            FloatingText.Spawn((tt != null ? tt.position : transform.position) + Vector3.up * 0.7f, "Critique !", new Color(1f, 0.8f, 0.3f), 0.9f);
        }
        if (target is WildAnimal beast) beast.TakeDamage(dmg, transform);
        else target.TakeDamage(dmg);
        if (lifeSteal > 0f && currentHealth > 0f) currentHealth = Mathf.Min(maxHealth, currentHealth + dmg * lifeSteal);
        if (target is GobelinAI goblin) goblin.OnAttackedBy(transform);
        DLog.Log($"{adventurerName} attaque {target.GetName()} pour {dmg} dégâts");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
