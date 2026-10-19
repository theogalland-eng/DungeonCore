using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Projectile visuel (flèche, boule de feu, trait de magie). Les dégâts sont appliqués par le tireur ;
/// un rappel optionnel se déclenche à l'impact (explosion...).
/// </summary>
public class Projectile : MonoBehaviour
{
    private Vector3 from, to;
    private float t, duration = 0.25f, arc = 0.25f;
    private Sprite[] frames;
    private float fps = 16f;
    private bool rotate;
    private SpriteRenderer sr;
    private Action onHit;

    /// <summary>Boule lumineuse simple (sorts des monstres).</summary>
    public static void Fire(Vector3 from, Vector3 to, Color color)
    {
        var go = new GameObject("Projectile");
        go.transform.position = from;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteUtil.Circle;
        sr.color = color;
        sr.sortingOrder = 12;
        go.transform.localScale = Vector3.one * 0.22f;
        AddLight(go, color, 1.2f);
        var p = go.AddComponent<Projectile>();
        p.from = from;
        p.to = to;
        p.sr = sr;
    }

    /// <summary>
    /// Projectile animé tiré de la bibliothèque d'effets (ex : « arrow », « fireball »).
    /// frameTo limite les images de vol (la fin de la bande peut être l'impact).
    /// </summary>
    public static Projectile FireFx(string fx, Vector3 from, Vector3 to, float height, bool rotate, Action onHit = null,
                                    float speed = 12f, float arc = 0.15f, int frameTo = -1, Color? light = null)
    {
        var f = Fx.Frames(fx);
        if (f == null || f.Length == 0)
        {
            Fire(from, to, light ?? Color.white);
            onHit?.Invoke();
            return null;
        }
        if (frameTo > 0 && frameTo < f.Length)
        {
            var sub = new Sprite[frameTo];
            Array.Copy(f, sub, frameTo);
            f = sub;
        }
        var go = new GameObject("Projectile_" + fx);
        go.transform.position = from;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = f[0];
        sr.sortingOrder = 12;
        float h = rotate ? f[0].bounds.size.y : Mathf.Max(f[0].bounds.size.x, f[0].bounds.size.y);
        if (h > 0f) go.transform.localScale = Vector3.one * (height / h);
        if (light.HasValue) AddLight(go, light.Value, 1.4f);
        var p = go.AddComponent<Projectile>();
        p.from = from;
        p.to = to;
        p.frames = f;
        p.rotate = rotate;
        p.sr = sr;
        p.onHit = onHit;
        p.arc = arc;
        p.duration = Mathf.Clamp(Vector3.Distance(from, to) / Mathf.Max(1f, speed), 0.12f, 0.8f);
        return p;
    }

    private static void AddLight(GameObject go, Color c, float radius)
    {
        var l = go.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
        l.lightType = Light2D.LightType.Point;
        l.color = c;
        l.intensity = 0.9f;
        l.pointLightOuterRadius = radius;
    }

    private void Update()
    {
        t += Time.deltaTime / duration;
        Vector3 prev = transform.position;
        transform.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * arc;
        if (frames != null && frames.Length > 1) sr.sprite = frames[(int)(t * duration * fps) % frames.Length];
        if (rotate)
        {
            Vector3 d = transform.position - prev;
            // les sprites de flèche pointent vers le haut
            if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        }
        else if (frames != null)
        {
            // la boule de feu regarde vers sa cible
            Vector3 d = to - from;
            sr.flipX = d.x < 0f;
        }
        if (t >= 1f)
        {
            onHit?.Invoke();
            Destroy(gameObject);
        }
    }
}
