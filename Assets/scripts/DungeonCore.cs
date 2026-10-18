using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Le Noyau du donjon (l'orbe = le joueur). S'il est détruit, la partie est perdue.
/// </summary>
public class DungeonCore : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public string coreName = "Dungeon Core";
    public float maxHealth = 1000f;
    public float currentHealth;

    [Header("Régénération")]
    [Tooltip("PV régénérés par seconde quand le cœur n'est plus attaqué")]
    public float regenPerSecond = 2f;
    [Tooltip("Délai sans dégâts avant de régénérer (s)")]
    public float regenDelay = 8f;

    [Header("Events")]
    public UnityEvent onDamageTaken;
    public UnityEvent onCoreDestroyed;

    [Header("Visual Feedback")]
    public SpriteRenderer spriteRenderer;
    public Color damageColor = Color.red;
    public float damageFlashDuration = 0.1f;
    [Tooltip("Pulsation lumineuse de l'orbe")]
    public bool pulse = true;

    private Color originalColor;
    /// <summary>Taille du cristal selon le niveau du Noyau (CoreForm).</summary>
    [HideInInspector] public float growth = 1f;
    public void SetTint(Color c) { originalColor = c; if (spriteRenderer != null) spriteRenderer.color = c; }
    private float lastDamageTime = -999f;
    private float lastAlertTime = -999f;
    private Vector3 baseScale;

    public float HealthPercent01 => maxHealth > 0 ? currentHealth / maxHealth : 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (currentHealth > 0 && currentHealth < maxHealth && Time.time - lastDamageTime > regenDelay)
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * Time.deltaTime);

        if (IsRelocating || cinematicControl) return;
        if (pulse)
        {
            float s = 1f + Mathf.Sin(Time.time * 2f) * 0.03f;
            transform.localScale = baseScale * growth * s;
        }
    }

    public bool IsAlive() => currentHealth > 0f;

    public bool IsRelocating { get; private set; }

    /// <summary>La cinématique contrôle l'échelle de l'orbe (pas de pulsation).</summary>
    [HideInInspector] public bool cinematicControl;

    /// <summary>Le cœur se dissout puis se reforme ailleurs (déplacement du donjon).</summary>
    public void RelocateTo(Vector3 target, System.Action onArrived)
    {
        if (IsRelocating) return;
        StartCoroutine(RelocateRoutine(target, onArrived));
    }

    private IEnumerator RelocateRoutine(Vector3 target, System.Action onArrived)
    {
        IsRelocating = true;
        var glow = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
        float baseIntensity = glow != null ? glow.intensity : 0f;
        FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "Le cœur se dissout…", new Color(0.5f, 0.85f, 1f), 1.2f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
        {
            transform.localScale = baseScale * (1f - t);
            if (glow != null) glow.intensity = baseIntensity * (1f + 2f * t);
            yield return null;
        }
        transform.localScale = Vector3.zero;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        onArrived?.Invoke();
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
        {
            transform.localScale = baseScale * Mathf.SmoothStep(0f, 1f, t);
            if (glow != null) glow.intensity = baseIntensity * (3f - 2f * t);
            yield return null;
        }
        transform.localScale = baseScale;
        if (glow != null) glow.intensity = baseIntensity;
        IsRelocating = false;
    }

    private float nextSealText;

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0) return;
        // un boss nommé garde l'étage : tant qu'il vit, son sceau protège le cristal
        if (BossSeal.CoreSealed(this))
        {
            if (Time.time >= nextSealText)
            {
                nextSealText = Time.time + 1.2f;
                FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "Scellé", new Color(0.8f, 0.6f, 1f), 1f);
            }
            return;
        }

        currentHealth -= amount;
        lastDamageTime = Time.time;
        Haptics.Pulse();
        Sfx.Play("core_hit", transform.position, 0.8f, 0.1f, 0.15f);
        FloatingText.Spawn(transform.position + Vector3.up * 0.6f, Mathf.RoundToInt(amount).ToString(), new Color(1f, 0.3f, 0.3f), 1f);

        if (spriteRenderer != null)
        {
            StopCoroutine(nameof(DamageFlash));
            StartCoroutine(nameof(DamageFlash));
        }

        // Alerte générale (au plus une fois toutes les 3 s)
        if (Time.time - lastAlertTime > 3f)
        {
            lastAlertTime = Time.time;
            NotifyAllGoblins();
            GameHUD.Toast("Le cœur est attaqué !", GameHUD.DangerColor);
            CameraController.Instance?.Shake(0.15f, 0.25f);
        }

        onDamageTaken?.Invoke();

        if (currentHealth <= 0) Die();
    }

    /// <summary>Tous les gobelins viennent défendre le cœur.</summary>
    private void NotifyAllGoblins()
    {
        foreach (GobelinAI goblin in GobelinAI.All.ToArray())
            if (goblin != null) goblin.DefendCore(transform);
    }

    public float GetCurrentHealth() => currentHealth;
    public string GetName() => coreName;
    public float GetHealthPercentage() => HealthPercent01 * 100f;

    private void Die()
    {
        Debug.Log($"{coreName} détruit ! GAME OVER");
        onCoreDestroyed?.Invoke();
        // Mode Aventurier : briser le cristal, c'est la victoire du joueur
        if (CrawlerMode.Active) { CrawlerMode.Instance.Victory(); return; }
        Sfx.Play("core_break");
        if (spriteRenderer != null) spriteRenderer.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        GameManager.Instance?.GameOver();
    }

    private IEnumerator DamageFlash()
    {
        spriteRenderer.color = damageColor;
        yield return new WaitForSeconds(damageFlashDuration);
        spriteRenderer.color = originalColor;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"{coreName}\nHP: {currentHealth}/{maxHealth}");
#endif
    }
}
