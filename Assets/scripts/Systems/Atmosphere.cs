using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Atmosphère du donjon et de l'île (fumée des cheminées comprise) :
///   - sous la montagne, la lumière baisse : seuls le cristal, les torches et les intrus éclairent ;
///   - chaque intrus (et le héros du mode Aventurier) porte une torche : on voit sa lueur approcher dans le noir ;
///   - le sol creusé se couvre de petits détails (fissures, cailloux, ossements, mousse, flaques), toujours les mêmes
///     pour une case donnée ;
///   - la nuit, des lanternes s'allument aux portes de la colonie.
/// </summary>
public class Atmosphere : MonoBehaviour
{
    public static Atmosphere Instance { get; private set; }

    [Tooltip("Lumière ambiante sous la montagne (vue donjon), en fraction de la lumière du jour")]
    public float caveLight = 0.58f;
    [Tooltip("Lumière ambiante des étages du mode Aventurier")]
    public float crawlerCaveLight = 0.5f;
    [Range(0f, 1f)] public float decalChance = 0.16f;

    private readonly List<(Light2D l, float baseI, float seed)> torches = new List<(Light2D, float, float)>();
    private readonly HashSet<Transform> lit = new HashSet<Transform>();
    private readonly Dictionary<Vector3Int, GameObject> decals = new Dictionary<Vector3Int, GameObject>();
    private readonly Dictionary<Vector3Int, GameObject> shades = new Dictionary<Vector3Int, GameObject>();
    private Sprite aoTop, aoSide;
    private HashSet<Vector3Int> prevCells = new HashSet<Vector3Int>();
    private readonly List<Light2D> lanterns = new List<Light2D>();
    private Sprite[] decalSprites;
    private Transform decalRoot;
    private long territoryHash;
    private float nextScan, nextDecals, nextLanterns;

    public static void Ensure()
    {
        if (Instance == null) new GameObject("Atmosphère").AddComponent<Atmosphere>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        var list = new List<Sprite>(Resources.LoadAll<Sprite>("Decals"));
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        decalSprites = list.ToArray();
        decalRoot = new GameObject("Détails du sol").transform;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.caveIntensity = 1f;
    }

    private void Update()
    {
        UpdateDarkness();
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.5f; ScanTorchBearers(); }
        FlickerTorches();
        if (Time.unscaledTime >= nextDecals) { nextDecals = Time.unscaledTime + 1.5f; RefreshDecals(); }
        if (Time.unscaledTime >= nextLanterns) { nextLanterns = Time.unscaledTime + 4f; RefreshLanterns(); }
        UpdateLanterns();
    }

    // ── Pénombre souterraine ──
    private void UpdateDarkness()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        float target = 1f;
        if (CrawlerMode.Active) target = CrawlerMode.Instance != null && CrawlerMode.Instance.floor > 0 ? crawlerCaveLight : 1f;
        else if (MountainRelief.Instance != null && MountainRelief.Instance.DungeonView) target = caveLight;
        gm.caveIntensity = Mathf.MoveTowards(gm.caveIntensity, target, Time.unscaledDeltaTime * 0.8f);
    }

    // ── Torches des intrus et du héros ──
    private void ScanTorchBearers()
    {
        foreach (var a in AdventurerAI.All) if (a != null && !a.IsDead) Light(a.transform, 2.6f, 0.75f);
        if (CrawlerMode.Active && CrawlerMode.Instance != null)
        {
            if (CrawlerMode.Instance.Player != null) Light(CrawlerMode.Instance.Player.transform, 3.4f, 0.85f);
            foreach (var c in CrawlerCompanion.All) if (c != null) Light(c.transform, 2.4f, 0.6f);
        }
        torches.RemoveAll(t => t.l == null);
    }

    private void Light(Transform who, float radius, float intensity)
    {
        if (!lit.Add(who)) return;
        var go = new GameObject("Torche");
        go.transform.SetParent(who, false);
        go.transform.localPosition = new Vector3(0.15f, 0.35f, 0f);
        var l = go.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
        l.lightType = Light2D.LightType.Point;
        l.color = new Color(1f, 0.72f, 0.42f);
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = 0.2f;
        l.intensity = intensity;
        l.falloffIntensity = 0.7f;
        torches.Add((l, intensity, Random.value * 100f));
    }

    private void FlickerTorches()
    {
        float t = Time.time;
        // dehors en plein jour, la torche ne se voit presque pas ; dans le noir, elle compte
        var gm = GameManager.Instance;
        float dark = gm == null ? 1f : Mathf.Clamp01(1.25f - gm.caveIntensity + (gm.IsNight ? 0.5f : 0f));
        foreach (var (l, baseI, seed) in torches)
        {
            if (l == null) continue;
            float f = 0.82f + 0.3f * Mathf.PerlinNoise(t * 6f, seed);
            var bearer = l.transform.parent != null ? l.transform.parent.GetComponent<AdventurerAI>() : null;
            l.intensity = bearer != null && bearer.TorchWet ? 0f : baseI * f * dark;   // mouillée par la pluie
        }
    }

    // ── Détails du sol ──
    private void RefreshDecals()
    {
        var t = DungeonTerritory.Instance;
        if (t == null || decalSprites == null || decalSprites.Length == 0) return;
        long h = 17;
        int n = 0;
        foreach (var c in t.Cells) { h = h * 31 + c.x * 73856093L + c.y * 19349663L; n++; }
        h += n;
        if (h == territoryHash) return;
        territoryHash = h;
        var keep = new HashSet<Vector3Int>(t.Cells);
        var gone = new List<Vector3Int>();
        foreach (var kv in decals) if (!keep.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var c in gone) { if (decals[c] != null) Destroy(decals[c]); decals.Remove(c); }
        RefreshShades(t, keep);
        foreach (var c in keep)
        {
            if (decals.ContainsKey(c)) continue;
            uint r = Hash(c);
            if ((r % 1000) / 1000f >= decalChance) { decals[c] = null; continue; }
            var go = new GameObject("Détail");
            go.transform.SetParent(decalRoot, false);
            // léger décalage dans la case, retournement : la répétition ne se voit pas
            go.transform.position = t.CellCenter(c) + new Vector3(((r >> 10) % 7 - 3) * 0.05f, ((r >> 13) % 7 - 3) * 0.05f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = decalSprites[(int)((r >> 16) % (uint)decalSprites.Length)];
            sr.flipX = ((r >> 20) & 1) == 1;
            sr.sortingOrder = 1;
            sr.color = new Color(1f, 1f, 1f, 0.72f);
            decals[c] = go;
        }
    }

    // ── Ombre de contact au pied des murs : donne du relief à la vue de dessus ──
    private void RefreshShades(DungeonTerritory t, HashSet<Vector3Int> cells)
    {
        if (aoTop == null) { aoTop = Gradient(true); aoSide = Gradient(false); }
        // seules les cases ajoutées ou retirées, et leurs voisines, changent d'ombre
        var affected = new HashSet<Vector3Int>();
        void Touch(Vector3Int c) { affected.Add(c); foreach (var d in DungeonTerritory.Dirs) affected.Add(c + d); }
        foreach (var c in cells) if (!prevCells.Contains(c)) Touch(c);
        foreach (var c in prevCells) if (!cells.Contains(c)) Touch(c);
        prevCells = new HashSet<Vector3Int>(cells);
        foreach (var c in affected)
        {
            if (shades.TryGetValue(c, out var o)) { if (o != null) Destroy(o); shades.Remove(c); }
        }
        foreach (var c in affected)
        {
            if (!cells.Contains(c)) continue;
            bool up = t.IsStone(c + Vector3Int.up), left = t.IsStone(c + Vector3Int.left), right = t.IsStone(c + Vector3Int.right);
            if (!up && !left && !right) continue;
            var root = new GameObject("Ombre");
            root.transform.SetParent(decalRoot, false);
            root.transform.position = t.CellCenter(c);
            if (up) Shade(root.transform, aoTop, false, 0.58f);
            if (left) Shade(root.transform, aoSide, false, 0.36f);
            if (right) Shade(root.transform, aoSide, true, 0.36f);
            shades[c] = root;
        }
    }

    private static void Shade(Transform parent, Sprite s, bool flip, float alpha)
    {
        var go = new GameObject("o");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.flipX = flip;
        sr.sortingOrder = 1;
        sr.color = new Color(0.04f, 0.03f, 0.06f, alpha);
    }

    /// <summary>Dégradé de 16 px : opaque au bord du mur, transparent vers le centre de la case.</summary>
    private static Sprite Gradient(bool top)
    {
        const int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = top ? (S - 1 - y) / (float)S : x / (float)S;      // distance au mur (haut ou gauche)
                float a = Mathf.Clamp01(1f - d / 0.55f);
                a = Mathf.Round(a * a * 4f) / 4f;                               // paliers : rendu pixel art
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    private static uint Hash(Vector3Int c)
    {
        unchecked
        {
            uint h = (uint)(c.x * 374761393 + c.y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    // ── Lanternes de la colonie ──
    private void RefreshLanterns()
    {
        lanterns.RemoveAll(l => l == null);
        foreach (var b in FindObjectsByType<SettlementBuilding>(FindObjectsSortMode.None))
        {
            if (b.transform.Find("Lanterne") != null) continue;
            var go = new GameObject("Lanterne");
            go.transform.SetParent(b.transform, false);
            go.transform.localPosition = new Vector3(0.55f, 0.45f, 0f);
            var l = go.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(1f, 0.78f, 0.45f);
            l.pointLightOuterRadius = 2.4f;
            l.pointLightInnerRadius = 0.1f;
            l.intensity = 0f;
            lanterns.Add(l);
            // fumée de cheminée sur les maisons assez hautes (une sur deux environ)
            var sr = b.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.bounds.size.y > 2.2f && (Hash(Vector3Int.RoundToInt(b.transform.position)) & 1) == 0)
            {
                var bb = sr.bounds;
                var fx = Fx.Play("smoke_column", new Vector3(bb.center.x + bb.size.x * 0.22f, bb.max.y + 0.6f, 0f), 2.4f, 6f, 45,
                                 new Color(0.62f, 0.6f, 0.64f, 0.42f), true);
                if (fx != null) { fx.name = "Fumée"; fx.transform.SetParent(b.transform, true); }
            }
        }
    }

    private void UpdateLanterns()
    {
        var gm = GameManager.Instance;
        float target = gm != null && gm.IsNight ? 0.9f : 0f;
        float t = Time.time;
        for (int i = 0; i < lanterns.Count; i++)
        {
            var l = lanterns[i];
            if (l == null) continue;
            l.intensity = Mathf.MoveTowards(l.intensity, target * (0.88f + 0.2f * Mathf.PerlinNoise(t * 3f, i)), Time.deltaTime * 0.6f);
        }
    }
}
