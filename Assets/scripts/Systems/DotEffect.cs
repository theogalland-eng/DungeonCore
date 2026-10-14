using UnityEngine;

/// <summary>Dégâts sur la durée (brûlure, poison) avec une teinte sur la cible.</summary>
public class DotEffect : MonoBehaviour
{
    private float dps, until, tick;
    private Color tint;
    private SpriteRenderer sr;
    private IDamageable target;

    public static void Apply(GameObject go, float dps, float seconds, Color tint)
    {
        var d = go.GetComponent<DotEffect>() ?? go.AddComponent<DotEffect>();
        d.dps = Mathf.Max(d.dps, dps);
        d.until = Mathf.Max(d.until, Time.time + seconds);
        d.tint = tint;
        d.enabled = true;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        target = GetComponent<IDamageable>();
    }

    private void Update()
    {
        if (Time.time > until || target == null || !target.IsAlive()) { dps = 0f; enabled = false; return; }
        tick -= Time.deltaTime;
        if (tick <= 0f)
        {
            tick = 0.5f;
            target.TakeDamage(dps * 0.5f);
        }
        if (sr != null && Time.frameCount % 20 < 6) sr.color = Color.Lerp(sr.color, tint, 0.3f);
    }
}
