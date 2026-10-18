using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Temple de cristal : le prisme se charge puis tire un rayon dans l'axe du couloir.</summary>
public class PrismTrap : TrapKit
{
    public float interval = 3f, damage = 18f;
    private bool horizontal;
    private SpriteRenderer beam;

    protected override void Start()
    {
        base.Start();
        horizontal = HorizontalCorridor();
        var g = new GameObject("Rayon");
        g.transform.SetParent(transform, false);
        beam = g.AddComponent<SpriteRenderer>();
        beam.sprite = SpriteUtil.Square;
        beam.color = new Color(0.8f, 0.6f, 1f, 0f);
        beam.sortingOrder = 6;
        Light(g, new Color(0.7f, 0.5f, 1f), 1.2f, 0.6f);
    }

    private void Update()
    {
        clock += Time.deltaTime;
        float ph = clock % interval;
        int f = ph < interval - 0.9f ? 0 : ph < interval - 0.5f ? 1 : ph < interval - 0.25f ? 2 : 3;
        Show(f);
        bool firing = ph >= interval - 0.25f;
        if (beam == null) return;
        if (!firing) { beam.color = new Color(0.8f, 0.6f, 1f, 0f); return; }
        Vector2Int a = horizontal ? Vector2Int.left : Vector2Int.down, b = horizontal ? Vector2Int.right : Vector2Int.up;
        int na = FreeRun(a, 4), nb = FreeRun(b, 4);
        Vector2 p0 = CellCenter(a.x * na, a.y * na), p1 = CellCenter(b.x * nb, b.y * nb);
        beam.transform.position = (p0 + p1) / 2f + Vector2.up * 0.35f;
        beam.transform.localScale = horizontal ? new Vector3(Vector2.Distance(p0, p1) + 0.6f, 0.12f, 1f) : new Vector3(0.12f, Vector2.Distance(p0, p1) + 0.6f, 1f);
        beam.color = new Color(0.85f, 0.7f, 1f, 0.85f);
        for (float d = 0f; d <= Vector2.Distance(p0, p1); d += 0.4f)
            foreach (var v in Victims(p0 + (p1 - p0).normalized * d, 0.35f)) Hurt(v, damage, interval * 0.8f);
    }
}
