using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Piège animé générique :
///   Periodic   : s'active à intervalle régulier (pics, guillotine, colonne à lances)
///   Proximity  : attend qu'un intrus approche (bombe, piège spectral), puis se réarme
///   Continuous : effet permanent (lames rotatives, toile d'araignée)
/// Les dégâts tombent sur l'image « hitFrame » de l'animation.
/// </summary>
public class AnimatedTrap : MonoBehaviour
{
    public enum Trigger { Periodic, Proximity, Continuous }

    public Sprite[] frames;
    public float fps = 12f;
    public SpriteRenderer spriteRenderer;
    public Trigger trigger = Trigger.Periodic;

    [Header("Déclenchement")]
    public float interval = 3f;
    public float triggerRadius = 0.6f;
    public float rearmSeconds = 15f;
    public int hitFrame = 3;
    [Tooltip("Invisible pendant le réarmement (bombe)")]
    public bool hideWhileRearming;

    [Header("Effets")]
    public float damage = 20f;
    public float radius = 0.5f;
    public float dps;
    [Range(0.1f, 1f)] public float slowMult = 1f;
    public float slowSeconds;
    public float holdSeconds;
    public float shake;
    [Tooltip("Effet joué à l'impact (bibliothèque Fx) : explosion, smoke_ghost...")]
    public string hitFx;
    private string HitSound()
    {
        if (hitFx == "explosion") return "explosion";
        var po = GetComponentInParent<PlacedObject>();
        string id = po != null && po.itemId != null ? po.itemId : name.ToLowerInvariant();
        if (id.Contains("spike") || id.Contains("column")) return "trap_spikes";
        if (id.Contains("web")) return "trap_web";
        return "trap_blade";
    }
    public float hitFxSize = 1.5f;
    public LayerMask intruderLayer = 1 << 7;

    private float timer, t;
    private bool playing, hitDone, rearming;
    private float tick;
    private Light2D flash;

    private void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        flash = GetComponentInChildren<Light2D>();
        timer = trigger == Trigger.Periodic ? Random.Range(0f, interval) : 0f;
        ShowFrame(0);
    }

    private void ShowFrame(int i)
    {
        if (spriteRenderer != null && frames != null && frames.Length > 0) spriteRenderer.sprite = frames[Mathf.Clamp(i, 0, frames.Length - 1)];
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        switch (trigger)
        {
            case Trigger.Continuous:
                t += Time.deltaTime * fps;
                ShowFrame((int)t % frames.Length);
                tick -= Time.deltaTime;
                if (tick <= 0f) { tick = 0.5f; Apply(dps * 0.5f, false); }
                return;

            case Trigger.Periodic:
                if (!playing)
                {
                    timer -= Time.deltaTime;
                    if (timer <= 0f) Play();
                }
                break;

            case Trigger.Proximity:
                if (rearming)
                {
                    timer -= Time.deltaTime;
                    if (timer <= 0f) { rearming = false; if (spriteRenderer != null) spriteRenderer.enabled = true; ShowFrame(0); }
                    return;
                }
                if (!playing && Physics2D.OverlapCircle(transform.position, triggerRadius, intruderLayer) != null) Play();
                break;
        }

        if (!playing) return;
        t += Time.deltaTime * fps;
        int f = (int)t;
        if (!hitDone && f >= hitFrame)
        {
            hitDone = true;
            Apply(damage, true);
            Sfx.Play(HitSound(), transform.position);
            if (!string.IsNullOrEmpty(hitFx)) Fx.Play(hitFx, transform.position + Vector3.up * 0.2f, hitFxSize, 16f, 16, null, false, null, hitFx == "explosion" ? new Color(1f, 0.6f, 0.25f) : (Color?)null);
        }
        if (f >= frames.Length)
        {
            playing = false;
            ShowFrame(0);
            if (trigger == Trigger.Periodic) timer = interval;
            else
            {
                rearming = true;
                timer = rearmSeconds;
                if (hideWhileRearming && spriteRenderer != null) spriteRenderer.enabled = false;
            }
            return;
        }
        ShowFrame(f);
        if (flash != null) flash.intensity = hitDone ? Mathf.MoveTowards(flash.intensity, 0f, Time.deltaTime * 4f) : flash.intensity;
    }

    private void Play()
    {
        playing = true;
        hitDone = false;
        t = 0f;
    }

    private void Apply(float amount, bool burst)
    {
        foreach (var h in Phys.Circle(transform.position, radius, intruderLayer))
        {
            var d = h.GetComponent<IDamageable>();
            if (d == null || !d.IsAlive()) continue;
            if (amount > 0f) { d.TakeDamage(amount); var po = GetComponentInParent<PlacedObject>(); if (po != null) po.damageDealt += amount; }
            var ai = h.GetComponent<AdventurerAI>();
            if (ai != null)
            {
                if (slowSeconds > 0f) ai.Slow(slowMult, slowSeconds);
                if (holdSeconds > 0f) ai.Hold(holdSeconds);
            }
        }
        if (burst)
        {
            if (shake > 0f) CameraController.Instance?.Shake(shake, 0.2f);
            if (flash != null) flash.intensity = 2f;
        }
    }
}
