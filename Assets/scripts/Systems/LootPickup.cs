using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Butin tombé au sol : il brille, sautille, puis le Noyau l'aspire (il file vers le cristal).
/// Aucun geste requis sur mobile ; toucher l'objet l'absorbe tout de suite.
/// </summary>
public class LootPickup : MonoBehaviour
{
    private LootDef def;
    private SpriteRenderer sr;
    private Light2D glow;
    private Vector3 origin;
    private float t;
    private bool flying;
    private Vector3 flyFrom;
    private float flyT;

    public static LootPickup Spawn(LootDef def, Vector3 at)
    {
        var go = new GameObject("Butin : " + def.name);
        go.transform.position = at;
        var p = go.AddComponent<LootPickup>();
        p.def = def;
        p.origin = at;
        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sprite = def.icon;
        p.sr.sortingOrder = 22;
        p.glow = go.AddComponent<Light2D>(); Light2DCleanup.Attach(p.glow);
        p.glow.lightType = Light2D.LightType.Point;
        p.glow.color = LootSystem.RarityColors[def.rarity];
        p.glow.intensity = 0.9f;
        p.glow.pointLightOuterRadius = 1.3f;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.35f;
        FloatingText.Spawn(at + Vector3.up * 0.6f, "Butin !", LootSystem.RarityColors[def.rarity], 1f);
        return p;
    }

    private void Update()
    {
        t += Time.deltaTime;
        if (!flying)
        {
            // petit saut à l'apparition puis flottement
            float hop = t < 0.5f ? Mathf.Sin(t / 0.5f * Mathf.PI) * 0.5f : 0.08f * Mathf.Sin(t * 4f);
            transform.position = origin + Vector3.up * (0.15f + hop);
            glow.intensity = 0.7f + 0.3f * Mathf.Sin(t * 6f);
            if (t > 2.2f || TappedThisFrame()) { flying = true; flyFrom = transform.position; }
            return;
        }
        var core = FindCore();
        Vector3 dest = core != null ? core.position : origin;
        flyT += Time.deltaTime / 0.9f;
        float e = flyT * flyT * (3f - 2f * flyT);
        Vector3 mid = (flyFrom + dest) * 0.5f + Vector3.up * 1.5f;
        // courbe de Bézier vers le cristal
        Vector3 p = Vector3.Lerp(Vector3.Lerp(flyFrom, mid, e), Vector3.Lerp(mid, dest, e), e);
        transform.position = p;
        transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, e);
        if (flyT >= 1f)
        {
            LootSystem.Instance?.Collect(def, dest);
            Destroy(gameObject);
        }
    }

    private bool TappedThisFrame()
    {
        if (!PointerUtil.Available || !PointerUtil.PressedThisFrame || PointerUtil.IsOverUI()) return false;
        return Vector2.Distance(PointerUtil.WorldPos, transform.position) < 0.5f;
    }

    private static Transform core;
    private static Transform FindCore()
    {
        if (core == null) { var c = FindFirstObjectByType<DungeonCore>(); core = c != null ? c.transform : null; }
        return core;
    }
}
