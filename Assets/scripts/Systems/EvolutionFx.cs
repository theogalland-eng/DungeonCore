using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Effet lumineux d'évolution / d'invocation : anneau qui s'étend + éclat de lumière.</summary>
public class EvolutionFx : MonoBehaviour
{
    private SpriteRenderer ring;
    private Light2D glow;
    private float t, maxScale;
    private Color color;

    /// <summary>Évolution : ailes de lumière (effet « revival ») + anneau doré.</summary>
    public static void Play(Vector3 pos)
    {
        Play(pos, new Color(1f, 0.86f, 0.4f), 1.4f);
        Fx.Play("revival", pos + Vector3.up * 0.35f, 2.2f, 16f, 16, null, false, null, new Color(1f, 0.9f, 0.5f));
    }

    public static void Play(Vector3 pos, Color color, float size)
    {
        Sfx.Play("evolve", pos);
        var go = new GameObject("EvolutionFx");
        go.transform.position = pos;
        var fx = go.AddComponent<EvolutionFx>();
        fx.color = color;
        fx.maxScale = size;
        fx.ring = go.AddComponent<SpriteRenderer>();
        fx.ring.sprite = SpriteUtil.Circle;
        fx.ring.sortingOrder = 15;
        fx.glow = go.AddComponent<Light2D>();
        fx.glow.lightType = Light2D.LightType.Point;
        fx.glow.color = color;
        fx.glow.pointLightOuterRadius = size * 2.5f;
        // étincelles qui montent
        for (int i = 0; i < 10; i++)
            FloatingText.Spawn(pos + (Vector3)(Random.insideUnitCircle * size * 0.5f), "+", color, 0.6f + Random.value * 0.4f);
    }

    private void Update()
    {
        t += CinematicDirector.Dt / 0.8f;
        float e = 1f - (1f - t) * (1f - t);
        transform.localScale = Vector3.one * Mathf.Lerp(0.2f, maxScale, e);
        ring.color = new Color(color.r, color.g, color.b, (1f - t) * 0.6f);
        glow.intensity = (1f - t) * 3f;
        if (t >= 1f) Destroy(gameObject);
    }
}
