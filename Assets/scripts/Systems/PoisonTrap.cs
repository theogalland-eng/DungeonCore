using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Évent toxique : nuage permanent qui empoisonne et ralentit les intrus alentour.</summary>
public class PoisonTrap : MonoBehaviour
{
    public float damagePerTick = 3f;
    public float tickInterval = 0.5f;
    public float radius = 1.1f;
    [Range(0.1f, 1f)] public float slowMult = 0.55f;
    public LayerMask intruderLayer = 1 << 7;
    public SpriteRenderer cloudRenderer;

    private float tick;
    private Light2D glow;
    private Vector3 baseScale;

    private void Start()
    {
        if (cloudRenderer == null) cloudRenderer = GetComponentInChildren<SpriteRenderer>();
        glow = GetComponentInChildren<Light2D>();
        baseScale = cloudRenderer != null ? cloudRenderer.transform.localScale : Vector3.one;
    }

    private void Update()
    {
        // Le nuage respire
        float p = Mathf.Sin(Time.time * 2.3f + transform.position.x);
        if (cloudRenderer != null) cloudRenderer.transform.localScale = baseScale * (1f + 0.05f * p);
        if (glow != null) glow.intensity = 0.55f + 0.15f * p;
    }

    private void FixedUpdate()
    {
        tick -= Time.fixedDeltaTime;
        if (tick > 0f) return;
        tick = tickInterval;
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, radius, intruderLayer))
        {
            var d = h.GetComponent<IDamageable>();
            if (d != null && d.IsAlive()) d.TakeDamage(damagePerTick);
            var ai = h.GetComponent<AdventurerAI>();
            if (ai != null) ai.Slow(slowMult, tickInterval + 0.3f);
        }
    }
}
