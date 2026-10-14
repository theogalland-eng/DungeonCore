using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Piège roue tranchante : blesse les intrus qui passent sur sa case.
/// La roue (enfant) est animée ; la zone de dégâts suit sa position.
/// </summary>
public class SpinningBladeTrap : MonoBehaviour
{
    /// <summary>Tous les pièges actifs (limite de pièges, occupation des cases).</summary>
    public static readonly List<SpinningBladeTrap> All = new List<SpinningBladeTrap>();

    [Header("Blade Reference")]
    [Tooltip("Transform de la roue qui bouge (enfant avec animation). Vide = cet objet")]
    public Transform bladeTransform;

    [Header("Damage Settings")]
    public float damage = 20f;
    [Tooltip("Layer des victimes (aventuriers)")]
    public LayerMask damageLayer;
    [Tooltip("Cooldown entre 2 dégâts sur la même cible (s)")]
    public float damageCooldown = 0.5f;

    [Header("Detection Zone")]
    public float detectionRadius = 0.5f;

    private readonly Dictionary<int, float> lastDamageTime = new Dictionary<int, float>();

    private void OnEnable()
    {
        // Un seul composant actif par piège (évite les doublons sur les enfants)
        if (transform.parent != null && transform.parent.GetComponentInParent<SpinningBladeTrap>() != null)
        {
            enabled = false;
            return;
        }
        All.Add(this);
    }

    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        if (bladeTransform == null) bladeTransform = transform;
        if (damageLayer.value == 0) damageLayer = 1 << 7;   // Adventurer
    }

    private void FixedUpdate()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(bladeTransform.position, detectionRadius, damageLayer);
        foreach (var hit in hits) TryDamage(hit);
    }

    private void TryDamage(Collider2D victim)
    {
        int id = victim.GetInstanceID();
        if (lastDamageTime.TryGetValue(id, out float t) && Time.time - t < damageCooldown) return;

        IDamageable damageable = victim.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive())
        {
            damageable.TakeDamage(damage);
            lastDamageTime[id] = Time.time;
            var po = GetComponentInParent<PlacedObject>(); if (po != null) po.damageDealt += damage;
            Sfx.Play("trap_blade", transform.position);
        }
    }

    private void OnDrawGizmos()
    {
        Transform b = bladeTransform != null ? bladeTransform : transform;
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(b.position, detectionRadius);
    }
}
