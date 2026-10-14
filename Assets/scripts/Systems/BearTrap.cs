using UnityEngine;

/// <summary>Piège à loup : se referme sur le premier intrus, le blesse et l'immobilise, puis se réarme.</summary>
public class BearTrap : MonoBehaviour
{
    public Sprite openSprite;
    public Sprite closedSprite;
    public SpriteRenderer spriteRenderer;
    public float damage = 25f;
    public float holdSeconds = 3.5f;
    public float rearmSeconds = 12f;
    public float radius = 0.35f;
    public LayerMask intruderLayer = 1 << 7;

    private float rearmAt;
    private bool armed = true;

    private void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        SetArmed(true);
    }

    private void Update()
    {
        if (!armed && Time.time >= rearmAt) SetArmed(true);
    }

    private void FixedUpdate()
    {
        if (!armed) return;
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, radius, intruderLayer))
        {
            var ai = h.GetComponent<AdventurerAI>();
            var d = h.GetComponent<IDamageable>();
            if (d == null || !d.IsAlive()) continue;
            d.TakeDamage(damage);
            var po = GetComponentInParent<PlacedObject>(); if (po != null) po.damageDealt += damage;
            if (ai != null) ai.Hold(holdSeconds);
            Sfx.Play("trap_snap", transform.position);
            FloatingText.Spawn(transform.position + Vector3.up * 0.7f, "Clac !", new Color(1f, 0.6f, 0.4f), 0.9f);
            CameraController.Instance?.Shake(0.05f, 0.12f);
            SetArmed(false);
            rearmAt = Time.time + rearmSeconds;
            break;
        }
    }

    private void SetArmed(bool on)
    {
        armed = on;
        if (spriteRenderer != null) spriteRenderer.sprite = on ? openSprite : closedSprite;
    }
}
