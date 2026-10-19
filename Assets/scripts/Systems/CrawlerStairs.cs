using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Mode Aventurier : escalier entre deux étages du donjon (E pour l'emprunter).</summary>
public class CrawlerStairs : MonoBehaviour
{
    public static readonly List<CrawlerStairs> All = new List<CrawlerStairs>();

    /// <summary>Étage où se trouve l'escalier.</summary>
    public int floor;
    public bool down;
    public int Target => down ? floor + 1 : floor - 1;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public static CrawlerStairs Create(Vector3 pos, int floor, bool down)
    {
        var cat = DungeonCatalog.Get();
        var go = new GameObject(down ? $"Descente étage {floor}" : $"Montée étage {floor}");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = down ? cat.stairsDownSprite : cat.stairsUpSprite;
        if (sr.sprite == null) { sr.sprite = SpriteUtil.Square; sr.color = down ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.6f, 0.5f, 0.4f); }
        sr.sortingOrder = 1;
        var s = go.AddComponent<CrawlerStairs>();
        s.floor = floor;
        s.down = down;
        // une lueur froide monte des profondeurs (on repère la descente de loin)
        var l = new GameObject("Lueur").AddComponent<Light2D>(); Light2DCleanup.Attach(l);
        l.transform.SetParent(go.transform, false);
        l.lightType = Light2D.LightType.Point;
        l.color = down ? new Color(0.55f, 0.6f, 1f) : new Color(1f, 0.85f, 0.6f);
        l.intensity = 0.8f;
        l.pointLightOuterRadius = 2.6f;
        l.pointLightInnerRadius = 0.3f;
        return s;
    }
}
