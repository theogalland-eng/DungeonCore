using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Jet de flammes : s'allume périodiquement et brûle les intrus sur sa case.
/// Les images de flammes sont jouées par code (pas besoin d'Animator).
/// </summary>
public class FlameTrap : MonoBehaviour
{
    public Sprite[] flameFrames;
    public SpriteRenderer flameRenderer;
    public float offDuration = 2.2f;
    public float onDuration = 1.3f;
    public float damagePerTick = 18f;
    public float tickInterval = 0.4f;
    public float radius = 0.55f;
    public LayerMask damageLayer = 1 << 7;
    [Tooltip("Décalage de la zone de dégâts (la flamme monte au-dessus de la buse)")]
    public Vector2 damageOffset = new Vector2(0f, 0.35f);

    private float timer;
    private float tick;
    private bool active;
    private Light2D glow;

    /// <summary>La flamme jaillit en ce moment (dalle huilée voisine : elle s'embrase).</summary>
    public bool IsBurning => active;

    private void Start()
    {
        timer = Random.Range(0f, offDuration);
        glow = GetComponentInChildren<Light2D>();
        SetActive(false);
        OrientAlongCorridor();
    }

    /// <summary>Dans un couloir horizontal, la flamme crache vers la droite (dans l'axe du passage).</summary>
    private void OrientAlongCorridor()
    {
        var t = DungeonTerritory.Instance;
        var b = DungeonBuilder.Instance;
        if (t == null || b == null || flameRenderer == null) return;
        var c = b.floorTilemap.WorldToCell(transform.position);
        int h = 0, v = 0;
        for (int k = 1; k <= 3; k++)
        {
            if (t.IsInsideCell(c + new Vector3Int(k, 0, 0))) h++; if (t.IsInsideCell(c + new Vector3Int(-k, 0, 0))) h++;
            if (t.IsInsideCell(c + new Vector3Int(0, k, 0))) v++; if (t.IsInsideCell(c + new Vector3Int(0, -k, 0))) v++;
        }
        if (h <= v) return;
        var ft = flameRenderer.transform;
        ft.localRotation = Quaternion.Euler(0, 0, -90f);
        ft.localPosition = new Vector3(Mathf.Abs(ft.localPosition.y), 0f, 0f);
        damageOffset = new Vector2(Mathf.Abs(damageOffset.y), 0f);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SetActive(!active);
            timer = active ? onDuration : offDuration;
        }

        if (active && flameRenderer != null && flameFrames != null && flameFrames.Length > 0)
        {
            int f = Mathf.FloorToInt(Time.time * 12f) % flameFrames.Length;
            flameRenderer.sprite = flameFrames[f];
        }
    }

    private void FixedUpdate()
    {
        if (!active) return;
        tick -= Time.fixedDeltaTime;
        if (tick > 0f) return;
        tick = tickInterval;
        foreach (var h in Physics2D.OverlapCircleAll((Vector2)transform.position + damageOffset, radius, damageLayer))
        {
            var d = h.GetComponent<IDamageable>();
            if (d != null && d.IsAlive()) { d.TakeDamage(damagePerTick); Sfx.Play("trap_fire", transform.position, 0.7f, 0.08f, 0.6f); }
        }
    }

    private void SetActive(bool on)
    {
        active = on;
        if (flameRenderer != null) flameRenderer.enabled = on;
        if (glow != null) glow.enabled = on;
    }
}
