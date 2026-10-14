using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Animation d'effet (une fois puis disparaît, ou en boucle), pouvant suivre une cible.</summary>
public class FxAnim : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 14f;
    public bool loop;
    public Transform follow;
    public Vector3 followOffset;
    [Tooltip("Fondu sur les dernières images (effets qui se dissipent)")]
    public bool fadeOut = true;

    private SpriteRenderer sr;
    private Light2D glow;
    private float t;
    private float baseLight;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        glow = GetComponent<Light2D>();
        if (glow != null) baseLight = glow.intensity;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) { Destroy(gameObject); return; }
        if (follow != null) transform.position = follow.position + followOffset;
        t += CinematicDirector.Dt * fps;
        int i = (int)t;
        if (i >= frames.Length)
        {
            if (!loop) { Destroy(gameObject); return; }
            t %= frames.Length;
            i = (int)t;
        }
        sr.sprite = frames[i];
        if (!loop && fadeOut)
        {
            float k = frames.Length > 3 ? Mathf.Clamp01((frames.Length - t) / 2f) : 1f;
            var c = sr.color;
            c.a = k;
            sr.color = c;
            if (glow != null) glow.intensity = baseLight * k;
        }
    }

    public void Stop() => Destroy(gameObject);
}
