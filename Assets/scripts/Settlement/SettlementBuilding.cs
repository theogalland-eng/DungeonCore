using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Bâtiment de la colonie : chantier (cadre de rondins, silhouette translucide, barre de progression)
/// puis construction achevée (collision, lumière, production).
/// </summary>
public class SettlementBuilding : MonoBehaviour
{
    public enum BuildState { Site, Complete }

    public BuildingDef def;
    public RectInt rect;
    public BuildState state = BuildState.Site;
    public float progress;          // 0..1
    public bool paid;               // bois et pierre livrés
    public int buildersPresent;

    private SpriteRenderer main;
    private StampVisual stampVisual;
    public StampVisual StampVisual => stampVisual;
    private readonly List<GameObject> scaffold = new List<GameObject>();
    private Sprite[] frames;
    private float animT;
    private Light2D fireLight;

    public bool IsComplete => state == BuildState.Complete;
    public Vector3 Center => new Vector3(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f, 0f);
    /// <summary>Point où se tient un ouvrier (devant, côté sud).</summary>
    public Vector3 WorkSpot(int i) => new Vector3(rect.x + 0.5f + (i % Mathf.Max(1, rect.width)), rect.y - 0.5f, 0f);

    public static SettlementBuilding Create(BuildingDef def, RectInt rect, Transform parent, bool instant)
    {
        var go = new GameObject(def.name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(rect.x + rect.width * 0.5f, rect.y + 0.15f, 0f);
        var b = go.AddComponent<SettlementBuilding>();
        b.def = def;
        b.rect = rect;
        // modèle de l'atelier de la cité (retouché à la main) : copié à sa place, porte et emprise suivent le modèle
        var ws = CityWorkshop.Instance;
        var model = ws != null ? ws.Template(def.id) : null;
        var ct = model != null ? model.GetComponent<CityTemplate>() : null;
        // tampon : la scène est centrée sur sa parcelle ; le bâtiment se tient au seuil de sa porte
        var st = SettlementArt.Get()?.GetStamp(def.stamp);
        if (ct != null)
        {
            var copy = ws.Spawn(model, go.transform);
            var delta = copy.transform.position - ct.islandOrigin;
            b.rect = new RectInt(ct.rect.x + Mathf.RoundToInt(delta.x), ct.rect.y + Mathf.RoundToInt(delta.y), ct.rect.width, ct.rect.height);
            var at = copy.transform.position;
            go.transform.position = at + ct.doorLocal;
            copy.transform.position = at;   // la copie ne bouge pas quand on déplace son parent
            b.stampVisual = copy.GetComponent<StampVisual>() ?? copy.AddComponent<StampVisual>();
        }
        else if (st != null)
        {
            var bl = new Vector3(rect.x + (rect.width - st.size.x) * 0.5f, rect.y, 0f);
            go.transform.position = bl + (Vector3)st.door + new Vector3(0f, 0.3f, 0f);
            b.stampVisual = StampVisual.Create(st, go.transform, bl, true);
        }
        b.Build(instant);
        return b;
    }

    private void Build(bool instant)
    {
        var art = SettlementArt.Get();
        if (stampVisual != null)
        {
            if (instant) { Complete(); return; }
            stampVisual.SetColliders(false);
            AddScaffold(art);
            SetGhost();
            HealthBar.Attach(transform, () => IsComplete ? 1f : progress, new Color(0.95f, 0.75f, 0.3f));
            return;
        }
        frames = art != null ? art.Frames(def.sprite) : null;
        var visual = new GameObject("Sprite");
        visual.transform.SetParent(transform, false);
        main = visual.AddComponent<SpriteRenderer>();
        main.sortingOrder = 3;
        main.spriteSortPoint = SpriteSortPoint.Pivot;
        if (frames != null && frames.Length > 0) main.sprite = frames[0];

        // Plusieurs mannequins pour le terrain d'entraînement
        if (def.id == "training" && main.sprite != null)
            for (int i = 1; i < 3; i++)
            {
                var d = new GameObject("Mannequin");
                d.transform.SetParent(transform, false);
                d.transform.localPosition = new Vector3(i * 1.2f - 1.2f, 0f, 0f);
                var sr = d.AddComponent<SpriteRenderer>();
                sr.sprite = main.sprite;
                sr.sortingOrder = 3;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                main.transform.localPosition = new Vector3(1.2f, 0f, 0f);
            }

        if (instant) { Complete(); return; }

        // Chantier : cadre de rondins + silhouette
        AddScaffold(art);
        SetGhost();
        HealthBar.Attach(transform, () => IsComplete ? 1f : progress, new Color(0.95f, 0.75f, 0.3f));
    }

    private void AddScaffold(SettlementArt art)
    {
        var logH = art != null ? art.Sprite("log_h") : null;
        var logV = art != null ? art.Sprite("log_v") : null;
        if (logH == null || logV == null || rect.width <= 0) return;
        float x0 = rect.x, x1 = rect.xMax, y0 = rect.y, y1 = rect.yMax;
        AddLog(logH, new Vector3((x0 + x1) * 0.5f, y0 + 0.1f), new Vector3(rect.width / logH.bounds.size.x, 1, 1));
        AddLog(logH, new Vector3((x0 + x1) * 0.5f, y1 - 0.2f), new Vector3(rect.width / logH.bounds.size.x, 1, 1));
        AddLog(logV, new Vector3(x0 + 0.15f, (y0 + y1) * 0.5f), new Vector3(1, rect.height / logV.bounds.size.y, 1));
        AddLog(logV, new Vector3(x1 - 0.15f, (y0 + y1) * 0.5f), new Vector3(1, rect.height / logV.bounds.size.y, 1));
    }

    private void AddLog(Sprite s, Vector3 worldPos, Vector3 scale)
    {
        var go = new GameObject("Rondin");
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = 2;
        scaffold.Add(go);
    }

    private void SetGhost()
    {
        if (stampVisual != null) { stampVisual.SetAlpha(Mathf.Lerp(0.18f, 0.6f, progress)); return; }
        if (main == null) return;
        var art = SettlementArt.Get();
        // Maisons : murs sans toit à mi-chantier
        if (progress >= 0.5f && !string.IsNullOrEmpty(def.midSprite) && art != null && art.Sprite(def.midSprite) != null)
        {
            main.sprite = art.Sprite(def.midSprite);
            main.color = Color.white;
            return;
        }
        main.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.18f, 0.6f, progress));
    }

    /// <summary>Un ouvrier travaille : fait avancer le chantier.</summary>
    public void Work(float dt)
    {
        if (IsComplete || !paid) return;
        progress = Mathf.Min(1f, progress + dt / Mathf.Max(1f, def.BuildSeconds));
        SetGhost();
        if (progress >= 1f)
        {
            Complete();
            SettlementManager.Instance?.OnBuildingComplete(this);
        }
    }

    public void Complete()
    {
        state = BuildState.Complete;
        progress = 1f;
        foreach (var g in scaffold) if (g != null) Destroy(g);
        scaffold.Clear();
        if (main != null)
        {
            main.color = Color.white;
            if (frames != null && frames.Length > 0) main.sprite = frames[0];
        }
        if (stampVisual != null)
        {
            // collisions au pied de chaque morceau (bâtiment, clôtures, arbres...) : les chemins les contournent
            stampVisual.SetAlpha(1f);
            stampVisual.SetColliders(true);
            AstarPath.active?.UpdateGraphs(new Bounds(Center, new Vector3(rect.width + 2, rect.height + 2, 1)), 0.1f);
        }
        else if (rect.width > 0 && def.id != "field_1" && def.id != "field_2")
        {
            // Collision au sol (le bas du bâtiment) : les chemins le contournent
            var col = gameObject.AddComponent<BoxCollider2D>();
            gameObject.layer = 6;
            col.size = new Vector2(Mathf.Max(0.8f, rect.width - 0.6f), Mathf.Max(0.6f, rect.height - 0.8f));
            col.offset = new Vector2(0f, col.size.y * 0.5f);
            AstarPath.active?.UpdateGraphs(new Bounds(Center, new Vector3(rect.width + 2, rect.height + 2, 1)), 0.1f);
        }
        if (def.light)
        {
            var lgo = new GameObject("Lueur");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            fireLight = lgo.AddComponent<Light2D>(); Light2DCleanup.Attach(fireLight);
            fireLight.lightType = Light2D.LightType.Point;
            fireLight.color = new Color(1f, 0.6f, 0.3f);
            fireLight.intensity = 1.1f;
            fireLight.pointLightOuterRadius = 5f;
            fireLight.pointLightInnerRadius = 0.5f;
        }
    }

    private void Update()
    {
        if (!IsComplete || frames == null || frames.Length < 2 || main == null) return;
        animT += Time.deltaTime * 8f;
        main.sprite = frames[(int)animT % frames.Length];
        if (fireLight != null) fireLight.intensity = 1f + Mathf.PerlinNoise(Time.time * 3f, 0f) * 0.4f;
    }
}
