using UnityEngine;

/// <summary>
/// Totem de guerre planté par un orc chaman au combat : les monstres de sa famille autour de lui frappent plus fort
/// (et, pour le chaman de sang, regagnent des PV). Il pulse de rouge puis s'effondre au bout de quelques secondes.
/// </summary>
public class WarTotem : MonoBehaviour
{
    public string family;
    public float mult = 1.15f, healPerSecond, life = 15f, radius = 3.5f;
    public Sprite[] frames;
    private SpriteRenderer sr;
    private float born, nextTick;

    public static WarTotem Plant(Vector2 pos, Sprite[] frames, string family, float mult, float healPct, float life, Transform parent)
    {
        var go = new GameObject("Totem de guerre");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.55f;   // dessiné à l'échelle des pièges : ramené à la taille d'un monstre
        var t = go.AddComponent<WarTotem>();
        t.sr = go.AddComponent<SpriteRenderer>();
        t.frames = frames;
        t.sr.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        t.sr.sortingOrder = 5;
        t.family = family; t.mult = mult; t.healPerSecond = healPct; t.life = life;
        var l = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        l.color = new Color(1f, 0.3f, 0.2f);
        l.pointLightOuterRadius = t.radius;
        l.intensity = 0.6f;
        return t;
    }

    private void OnEnable() => born = Time.time;

    private void Update()
    {
        float age = Time.time - born;
        if (age > life) { Destroy(gameObject); return; }
        if (sr != null)
        {
            if (frames != null && frames.Length > 1) sr.sprite = frames[Mathf.Repeat(age, 1f) < 0.5f ? 1 : 0];
            float a = Mathf.Clamp01(age / 0.4f) * Mathf.Clamp01((life - age) / 1f);
            sr.color = new Color(1f, 1f, 1f, a);
        }
        if (Time.time < nextTick) return;
        nextTick = Time.time + 0.5f;
        foreach (var g in GobelinAI.All)
        {
            if (g == null || !g.IsAlive() || Vector2.Distance(g.transform.position, transform.position) > radius) continue;
            var u = g.GetComponent<MonsterUnit>();
            if (u == null || u.familyId != family) continue;
            g.Buff(mult, 0.8f);
            if (healPerSecond > 0f && g.currentHealth < g.maxHealth) g.Heal(g.maxHealth * healPerSecond * 0.5f);
        }
    }
}
