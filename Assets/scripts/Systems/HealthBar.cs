using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Petite barre de vie au-dessus d'une unité (masquée quand PV pleins).
/// </summary>
public class HealthBar : MonoBehaviour
{
    private Func<float> getter;
    private Transform fill;
    private SpriteRenderer bgRenderer, fillRenderer;
    private float width;
    private float visibleTimer;
    private float lastValue = 1f;

    public static HealthBar Attach(Transform owner, Func<float> getter, Color color, float yOffset = 0.75f, float width = 0.7f)
    {
        var root = new GameObject("HealthBar");
        root.transform.SetParent(owner, false);
        // Compense l'échelle du propriétaire pour garder une taille constante
        Vector3 s = owner.lossyScale;
        root.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f);
        root.transform.localPosition = new Vector3(0f, yOffset / Mathf.Max(0.01f, s.y), 0f);

        var hb = root.AddComponent<HealthBar>();
        hb.getter = getter;
        hb.width = width;

        var bg = new GameObject("Bg");
        bg.transform.SetParent(root.transform, false);
        bg.transform.localScale = new Vector3(width + 0.06f, 0.14f, 1f);
        hb.bgRenderer = bg.AddComponent<SpriteRenderer>();
        hb.bgRenderer.sprite = SpriteUtil.Square;
        hb.bgRenderer.color = new Color(0f, 0f, 0f, 0.75f);
        hb.bgRenderer.sortingOrder = 60;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(root.transform, false);
        hb.fill = fillGo.transform;
        hb.fillRenderer = fillGo.AddComponent<SpriteRenderer>();
        hb.fillRenderer.sprite = SpriteUtil.Square;
        hb.fillRenderer.color = color;
        hb.fillRenderer.sortingOrder = 61;

        hb.Refresh(1f);
        hb.SetVisible(false);
        return hb;
    }

    private void LateUpdate()
    {
        if (getter == null) return;
        float v = Mathf.Clamp01(getter());
        if (!Mathf.Approximately(v, lastValue))
        {
            if (v < lastValue) visibleTimer = 3f;
            lastValue = v;
            Refresh(v);
        }
        visibleTimer -= Time.deltaTime;
        SetVisible(v < 0.999f && (visibleTimer > 0f || v < 0.6f));
    }

    private void Refresh(float v)
    {
        fill.localScale = new Vector3(width * v, 0.08f, 1f);
        fill.localPosition = new Vector3(-width * (1f - v) / 2f, 0f, 0f);
    }

    private void SetVisible(bool visible)
    {
        if (bgRenderer.enabled != visible)
        {
            bgRenderer.enabled = visible;
            fillRenderer.enabled = visible;
        }
    }
}
