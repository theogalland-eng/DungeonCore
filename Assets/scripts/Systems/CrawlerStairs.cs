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

    /// <summary>Gardien de l'étage : tant qu'il vit, la descente est scellée (cercle de runes).</summary>
    public GobelinAI guardian;
    public bool Sealed => down && guardian != null && guardian.IsAlive();
    public string GuardianName => guardian != null ? (guardian.GetComponent<MonsterMind>()?.GivenName ?? guardian.gobelinName) : "";
    private Transform ring;
    private GameObject seal;
    private float spin, nextCheck;

    /// <summary>Scelle la descente : seule la mort du gardien l'ouvre.</summary>
    public void Seal(GobelinAI g)
    {
        guardian = g;
        if (g == null || seal != null) return;
        seal = new GameObject("Sceau du gardien");
        seal.transform.SetParent(transform, false);
        var ringGo = new GameObject("Runes");
        ringGo.transform.SetParent(seal.transform, false);
        var sr = ringGo.AddComponent<SpriteRenderer>();
        sr.sprite = BossSeal.RingSprite();
        sr.color = new Color(0.75f, 0.45f, 1f, 0.85f);
        sr.sortingOrder = 2;
        ringGo.transform.localScale = Vector3.one * 1.6f;
        ring = ringGo.transform;
        var l = seal.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
        l.lightType = Light2D.LightType.Point;
        l.color = new Color(0.65f, 0.35f, 1f);
        l.intensity = 0.9f;
        l.pointLightOuterRadius = 2.4f;
    }

    private void Update()
    {
        if (seal == null) return;
        spin += Time.deltaTime * 25f;
        if (ring != null) ring.localRotation = Quaternion.Euler(0, 0, spin);
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.4f;
        if (guardian != null && guardian.IsAlive()) return;
        Fx.Play("shine", transform.position, 1.6f, 18f, 82, new Color(0.8f, 0.6f, 1f));
        Sfx.Play("core_hit", transform.position, 0.7f);
        GameHUD.Toast("Le gardien est tombé : le sceau de l'escalier se brise", GameHUD.GoldColor);
        Destroy(seal);
        seal = null;
    }

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
