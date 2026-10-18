using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ═════════════════════════════════════════════
// DALLE HUILÉE : on glisse sans contrôle ; une flamme proche l'embrase
// ═════════════════════════════════════════════
public class OilTrap : TrapKit
{
    private float t, burnUntil, nextCheck;
    private readonly Dictionary<int, float> slid = new Dictionary<int, float>();
    private Light2D fire;

    private void Update()
    {
        t += Time.deltaTime * 5f;
        Show((int)t % Mathf.Max(1, frames.Length));
        bool burning = Time.time < burnUntil;
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 1f;
            if (!burning && FireNear()) Ignite();
        }
        if (fire != null) fire.intensity = burning ? 0.9f + 0.2f * Mathf.Sin(Time.time * 17f) : 0f;
        foreach (var v in Victims(Pos, 0.45f))
        {
            int id = v.GetInstanceID();
            if (burning) { DotEffect.Apply(v.gameObject, 6f, 2f, new Color(1f, 0.55f, 0.2f)); }
            if (slid.TryGetValue(id, out float until) && Time.time < until) continue;
            slid[id] = Time.time + 3f;
            Slide(v);
        }
    }

    private bool FireNear()
    {
        if (MonsterHabitat.ThemeAt(Pos) == "lava") return Random.value < 0.08f;
        foreach (var f in FindObjectsByType<FlameTrap>(FindObjectsSortMode.None))
            if (f != null && f.isActiveAndEnabled && Vector2.Distance(f.transform.position, Pos) < 2.5f && f.IsBurning) return true;
        foreach (var f in FindObjectsByType<FireTrail>(FindObjectsSortMode.None))
            if (f != null && Vector2.Distance(f.transform.position, Pos) < 1.5f) return true;
        return false;
    }

    private void Ignite()
    {
        burnUntil = Time.time + 5f;
        if (fire == null) { var g = new GameObject("Feu"); g.transform.SetParent(transform, false); Light(g, new Color(1f, 0.5f, 0.15f), 1.6f, 0f); fire = g.GetComponent<Light2D>(); }
        Fx.Play("explosion", Pos, 1.1f, 14f, 16, null, false, null, new Color(1f, 0.55f, 0.2f));
        Sfx.Play("fireball", Pos, 0.6f);
    }

    private void Slide(AdventurerCombat v)
    {
        var rb = v.GetComponent<Rigidbody2D>();
        Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;
        Vector2 dir = vel.sqrMagnitude > 0.01f ? vel.normalized : ((Vector2)v.transform.position - Pos).normalized;
        if (dir == Vector2.zero) dir = Vector2.down;
        Vector2 to = (Vector2)v.transform.position + dir * 1.6f;
        if (Physics2D.OverlapCircle(to, 0.25f, 1 << 6) != null) to = (Vector2)v.transform.position + dir * 0.8f;
        if (Physics2D.OverlapCircle(to, 0.25f, 1 << 6) != null) return;
        if (rb != null) rb.position = to; else v.transform.position = to;
        Hold(v, 0.6f);
        v.GetComponent<AdventurerAI>()?.Speak("Ça glisse !", new Color(0.8f, 0.8f, 0.95f));
    }
}
