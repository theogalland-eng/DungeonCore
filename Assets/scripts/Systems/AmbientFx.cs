using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ambiance visuelle autour de la caméra :
///   - lucioles la nuit (forêt), ombres de nuages qui dérivent le jour,
///   - poussière en suspension dans les étages souterrains, reflets sur la mer,
///   - post-traitement léger : bloom (orbe, torches, lave), vignette, couleurs.
/// Tout est recyclé (pas d'allocation) pour rester fluide sur mobile.
/// </summary>
public class AmbientFx : MonoBehaviour
{
    public bool postProcessing = true;
    public int fireflyCount = 26;
    public int cloudCount = 5;
    public int dustCount = 30;

    private class Particle
    {
        public Transform t;
        public SpriteRenderer sr;
        public Light2D light;
        public Vector2 vel;
        public float phase;
    }

    private readonly List<Particle> fireflies = new List<Particle>();
    private readonly List<Particle> clouds = new List<Particle>();
    private readonly List<Particle> dust = new List<Particle>();
    private readonly List<Particle> glints = new List<Particle>();
    private Camera cam;
    private Transform root;
    private UnityEngine.Tilemaps.Tilemap sea;
    private float nightBlend;

    private void Start()
    {
        cam = Camera.main;
        root = new GameObject("Ambiance").transform;
        foreach (var tm in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
            if (tm.name == "NoPathfinding") sea = tm;

        for (int i = 0; i < fireflyCount; i++)
            fireflies.Add(Make("Luciole", SpriteUtil.Circle, new Color(0.85f, 1f, 0.5f, 0f), 0.07f, 25, i % 4 == 0));
        for (int i = 0; i < cloudCount; i++)
        {
            var c = Make("Nuage", SpriteUtil.Circle, new Color(0.05f, 0.07f, 0.12f, 0f), 1f, 25, false);
            c.t.localScale = new Vector3(Random.Range(9f, 16f), Random.Range(5f, 8f), 1f);
            c.vel = new Vector2(Random.Range(0.35f, 0.6f), Random.Range(-0.1f, 0.1f));
            clouds.Add(c);
        }
        for (int i = 0; i < dustCount; i++)
            dust.Add(Make("Poussière", SpriteUtil.Square, new Color(1f, 0.95f, 0.85f, 0f), 0.04f, 24, false));
        for (int i = 0; i < 14; i++)
        {
            var g = Make("Reflet", SpriteUtil.Diamond, new Color(1f, 1f, 1f, 0f), 0.14f, 1, false);
            g.sr.enabled = false;           // visible seulement une fois placé sur l'eau
            g.phase = Mathf.PI + Random.value;
            glints.Add(g);
        }

        if (postProcessing) SetupPost();
        SetParticlesVisible(GameSettings.Ambient);
    }

    /// <summary>Option « particules d'ambiance » (lucioles, nuages, poussière, reflets).</summary>
    public void SetParticlesVisible(bool v)
    {
        foreach (var list in new[] { fireflies, clouds, dust, glints })
            foreach (var p in list) if (p.t != null) p.t.gameObject.SetActive(v);
    }

    private Particle Make(string name, Sprite s, Color c, float scale, int order, bool withLight)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = c;
        sr.sortingOrder = order;
        go.transform.localScale = Vector3.one * scale;
        var p = new Particle { t = go.transform, sr = sr, phase = Random.value * 10f };
        if (withLight)
        {
            p.light = go.AddComponent<Light2D>(); Light2DCleanup.Attach(p.light);
            p.light.lightType = Light2D.LightType.Point;
            p.light.color = new Color(0.8f, 1f, 0.45f);
            p.light.pointLightOuterRadius = 1.4f;
            p.light.intensity = 0f;
        }
        Respawn(p, true);
        return p;
    }

    private void Respawn(Particle p, bool anywhere)
    {
        if (cam == null) return;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        p.t.position = c + new Vector2(Random.Range(-w, w) * 1.1f, Random.Range(-h, h) * 1.1f);
        p.vel = Random.insideUnitCircle * 0.3f;
        p.phase = Random.value * 10f;
    }

    private void Update()
    {
        if (cam == null) return;
        float dt = Time.deltaTime;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        var gm = GameManager.Instance;
        bool night = gm != null && gm.IsNight;
        nightBlend = Mathf.MoveTowards(nightBlend, night ? 1f : 0f, dt * 0.3f);
        int layer = DungeonLayers.Instance != null ? DungeonLayers.Instance.CurrentView : 0;
        bool surface = layer == 0;
        bool zoomedIn = h < 14f;

        // Lucioles (surface, nuit)
        foreach (var p in fireflies)
        {
            p.phase += dt;
            p.vel += Random.insideUnitCircle * dt * 0.6f;
            p.vel = Vector2.ClampMagnitude(p.vel, 0.45f);
            p.t.position += (Vector3)(p.vel * dt);
            if (Outside(p.t.position, c, w, h)) Respawn(p, false);
            float a = surface && zoomedIn ? nightBlend * (0.35f + 0.65f * Mathf.Max(0f, Mathf.Sin(p.phase * 2.2f))) : 0f;
            p.sr.color = new Color(0.85f, 1f, 0.5f, a);
            if (p.light != null) p.light.intensity = a * 0.9f;
        }

        // Ombres de nuages (surface, jour)
        foreach (var p in clouds)
        {
            p.t.position += (Vector3)(p.vel * dt);
            if (p.t.position.x - p.t.localScale.x > c.x + w + 4f) p.t.position = new Vector3(c.x - w - p.t.localScale.x, c.y + Random.Range(-h, h), 0f);
            if (Mathf.Abs(p.t.position.y - c.y) > h + 10f) p.t.position = new Vector3(p.t.position.x, c.y + Random.Range(-h, h), 0f);
            p.sr.color = new Color(0.05f, 0.07f, 0.12f, surface ? 0.13f * (1f - nightBlend) : 0f);
        }

        // Poussière (souterrain)
        foreach (var p in dust)
        {
            p.phase += dt;
            p.t.position += (Vector3)(new Vector2(Mathf.Sin(p.phase * 0.7f) * 0.08f, 0.05f) * dt);
            if (Outside(p.t.position, c, w, h)) Respawn(p, false);
            p.sr.color = new Color(1f, 0.95f, 0.85f, !surface ? 0.18f + 0.12f * Mathf.Sin(p.phase * 1.3f) : 0f);
        }

        // Reflets sur l'eau
        foreach (var p in glints)
        {
            p.phase += dt * 1.6f;
            if (p.phase > Mathf.PI)
            {
                p.phase = 0f;
                Respawn(p, false);
                bool onSea = sea != null && surface && sea.HasTile(sea.WorldToCell(p.t.position));
                p.sr.enabled = onSea;
            }
            float s = Mathf.Sin(p.phase);
            p.t.localScale = new Vector3(0.18f * s, 0.07f * s, 1f);
            p.sr.color = new Color(1f, 1f, 1f, 0.55f * s * (1f - nightBlend * 0.6f));
        }
    }

    private static bool Outside(Vector2 p, Vector2 c, float w, float h) =>
        Mathf.Abs(p.x - c.x) > w * 1.3f || Mathf.Abs(p.y - c.y) > h * 1.3f;

    // ─────────────────────────────────────────────
    // POST-TRAITEMENT
    // ─────────────────────────────────────────────

    private void SetupPost()
    {
        if (cam == null) return;
        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data != null) data.renderPostProcessing = true;
        var go = new GameObject("PostProcess");
        go.transform.SetParent(root, false);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.65f);
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.28f);
        vig.smoothness.Override(0.45f);
        var col = profile.Add<ColorAdjustments>(true);
        col.saturation.Override(12f);
        col.contrast.Override(8f);
        vol.profile = profile;
        vol.enabled = GameSettings.PostProcessing;
        GraphicsQuality.Apply();   // halo coupé et post-traitement allégé selon la qualité graphique
    }
}
