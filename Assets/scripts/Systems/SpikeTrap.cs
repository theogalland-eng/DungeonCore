using UnityEngine;

/// <summary>Pics rétractables : jaillissent périodiquement et blessent les intrus sur la case.</summary>
public class SpikeTrap : MonoBehaviour
{
    public Sprite retractedSprite;
    public Sprite extendedSprite;
    public SpriteRenderer spriteRenderer;
    public float offDuration = 2.4f;
    public float onDuration = 0.9f;
    public float damage = 22f;
    public float radius = 0.45f;
    public LayerMask damageLayer = 1 << 7;

    private float timer;
    private bool extended;

    private void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        timer = Random.Range(0f, offDuration);
        SetExtended(false);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        SetExtended(!extended);
        timer = extended ? onDuration : offDuration;
        if (extended) Strike();
    }

    private void Strike()
    {
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, radius, damageLayer))
        {
            var d = h.GetComponent<IDamageable>();
            if (d != null && d.IsAlive()) d.TakeDamage(damage);
        }
    }

    private void SetExtended(bool on)
    {
        extended = on;
        if (spriteRenderer != null) spriteRenderer.sprite = on ? extendedSprite : retractedSprite;
    }
}
