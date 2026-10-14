using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Coffre au trésor : appât. Les intrus cupides font un détour pour le piller,
/// restant plus longtemps dans le donjon (plus de mana drainé). Se remplit avec le temps.
/// </summary>
public class TreasureChest : MonoBehaviour
{
    public static readonly List<TreasureChest> All = new List<TreasureChest>();

    public float refillTime = 60f;
    public float lootDuration = 2.5f;
    public Color emptyTint = new Color(0.5f, 0.5f, 0.5f, 1f);

    public bool IsFull { get; private set; } = true;
    public AdventurerAI ReservedBy { get; set; }

    private float refillTimer;
    private SpriteRenderer sr;
    private Color baseColor;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    // ── Fouille en cours : le coffre tremble, une jauge dorée se remplit ──
    private Transform bar, barFill;
    private float lootingUntil;
    private Vector3 baseScale = Vector3.one;
    private bool scaleSaved;

    public void ShowLooting(float progress)
    {
        lootingUntil = Time.time + 0.2f;
        if (bar == null)
        {
            bar = new GameObject("Fouille").transform;
            bar.SetParent(transform, false);
            bar.localPosition = new Vector3(0f, 0.95f, 0f);
            var bg = bar.gameObject.AddComponent<SpriteRenderer>();
            bg.sprite = SpriteUtil.Square;
            bg.color = new Color(0.05f, 0.04f, 0.08f, 0.85f);
            bg.sortingOrder = 40;
            bar.localScale = new Vector3(0.8f, 0.12f, 1f);
            barFill = new GameObject("Jauge").transform;
            barFill.SetParent(bar, false);
            var f = barFill.gameObject.AddComponent<SpriteRenderer>();
            f.sprite = SpriteUtil.Square;
            f.color = new Color(1f, 0.82f, 0.3f);
            f.sortingOrder = 41;
        }
        bar.gameObject.SetActive(true);
        float p = Mathf.Clamp01(progress);
        barFill.localScale = new Vector3(p * 0.92f, 0.7f, 1f);
        barFill.localPosition = new Vector3(-0.46f + p * 0.46f, 0f, 0f);
        if (sr != null)
        {
            if (!scaleSaved) { baseScale = sr.transform.localScale; scaleSaved = true; }
            float k = Mathf.Sin(Time.time * 38f) * 0.04f;
            sr.transform.localScale = new Vector3(baseScale.x * (1f + k), baseScale.y * (1f - k), baseScale.z);
        }
        if (Random.value < 0.06f) Fx.Play("shine", transform.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.5f), 0f), 0.28f, 18f, 30, new Color(1f, 0.9f, 0.5f));
    }

    private void LateUpdate()
    {
        if (bar != null && bar.gameObject.activeSelf && Time.time > lootingUntil)
        {
            bar.gameObject.SetActive(false);
            if (sr != null && scaleSaved) sr.transform.localScale = baseScale;
        }
    }

    private void Update()
    {
        if (IsFull) return;
        refillTimer -= Time.deltaTime;
        if (refillTimer <= 0f)
        {
            IsFull = true;
            if (sr != null) sr.color = baseColor;
        }
    }

    private System.Collections.IEnumerator PopLoot(Sprite icon)
    {
        var go = new GameObject("Butin");
        go.transform.position = transform.position + Vector3.up * 0.3f;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = icon != null ? icon : SpriteUtil.Circle;
        r.color = icon != null ? Color.white : new Color(1f, 0.85f, 0.3f);
        r.sortingOrder = 42;
        float size = icon != null ? 0.6f / Mathf.Max(0.01f, icon.bounds.size.y) : 0.25f;
        go.transform.localScale = Vector3.one * size;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
        {
            go.transform.position = transform.position + Vector3.up * (0.3f + 0.9f * Mathf.Sin(t * Mathf.PI * 0.5f));
            r.color = new Color(r.color.r, r.color.g, r.color.b, t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
            yield return null;
        }
        Destroy(go);
    }

    public void Loot(AdventurerAI by)
    {
        if (!IsFull) return;
        IsFull = false;
        ReservedBy = null;
        refillTimer = refillTime;
        if (sr != null) sr.color = emptyTint;
        // Le butin vient du trésor du Noyau (objets pris aux intrus morts), sinon de richesses nées du mana
        var loot = by != null ? LootSystem.Instance?.TakeForChest() : null;
        FloatingText.Spawn(transform.position + Vector3.up * 0.6f, loot != null ? $"Emporte : {loot.name}" : "Trésor !", GameHUD.GoldColor, 1.1f);
        // le butin jaillit du coffre
        Sfx.Play("coin", transform.position);
        Fx.Play("shine", transform.position + Vector3.up * 0.3f, 0.7f, 16f, 30, new Color(1f, 0.85f, 0.4f));
        if (sr != null && scaleSaved) sr.transform.localScale = baseScale;
        if (bar != null) bar.gameObject.SetActive(false);
        StartCoroutine(PopLoot(loot != null ? loot.icon : null));
    }
}
