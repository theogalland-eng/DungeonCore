using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// SAISONS : printemps, été, automne, hiver se succèdent (6 jours chacune). Elles se voient (herbe, feuillage, neige)
/// et se sentent :
///   - printemps / été : la faune se reproduit ; été : moissons de la colonie un peu meilleures ;
///   - automne : feuillage roux, récoltes abondantes, pluies plus fréquentes ;
///   - hiver : neige, la faune ne se reproduit plus, tout le monde a plus faim (intrus, héros, monstres),
///     la colonie produit moitié moins de vivres.
/// La saison découle du jour : rien à sauvegarder.
/// </summary>
public class Seasons : MonoBehaviour
{
    public enum Season { Spring, Summer, Autumn, Winter }
    public static Seasons Instance { get; private set; }

    [Tooltip("Jours par saison")] public int daysPerSeason = 6;

    public static readonly string[] Names = { "Printemps", "Été", "Automne", "Hiver" };
    public static Season Current => Instance != null ? Instance.SeasonOf(GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1) : Season.Spring;
    public static string CurrentName => Names[(int)Current];

    /// <summary>La faim monte plus vite en hiver.</summary>
    public static float HungerMult => Current == Season.Winter ? 1.3f : 1f;
    /// <summary>Vivres produits par la colonie.</summary>
    public static float FoodMult => Current == Season.Winter ? 0.5f : Current == Season.Autumn ? 1.25f : Current == Season.Summer ? 1.1f : 1f;
    public static bool Breeding => Current == Season.Spring || Current == Season.Summer;
    /// <summary>Probabilité de pluie au matin (l'automne est humide).</summary>
    public static float RainChance => Current == Season.Autumn ? 0.3f : Current == Season.Spring ? 0.2f : Current == Season.Winter ? 0.05f : 0.1f;

    public Season SeasonOf(int day) => (Season)(((Mathf.Max(1, day) - 1) / Mathf.Max(1, daysPerSeason)) % 4);

    // teintes : herbe, jungle, arbres
    private static readonly Color[] GrassTint = { Color.white, new Color(1.04f, 1.02f, 0.82f), new Color(1.08f, 0.86f, 0.55f), new Color(0.86f, 0.92f, 0.98f) };
    private static readonly Color[] JungleTint = { Color.white, new Color(1f, 1f, 0.9f), new Color(1.05f, 0.82f, 0.6f), new Color(0.78f, 0.86f, 0.92f) };
    private static readonly Color[] TreeTint = { Color.white, new Color(1.03f, 1.02f, 0.88f), new Color(1.12f, 0.78f, 0.5f), new Color(0.82f, 0.88f, 0.96f) };

    private Tilemap grass, jungle, snowMap;
    private TilemapRenderer snowRenderer;
    private readonly List<(SpriteRenderer sr, Color baseC)> trees = new List<(SpriteRenderer, Color)>();
    private float blend = 1f;
    private bool fromWinter;
    private Season shown;
    private Color fromG = Color.white, fromJ = Color.white, fromT = Color.white;
    private readonly List<Transform> flakes = new List<Transform>();
    private readonly List<SpriteRenderer> flakesR = new List<SpriteRenderer>();
    private float snow;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name == "T_Herbe") grass = tm;
            else if (tm.name == "T_Jungle") jungle = tm;
            else if (tm.name == "T_Neige") { snowMap = tm; snowRenderer = tm.GetComponent<TilemapRenderer>(); }
        }
        var forest = GameObject.Find("Forêt");
        if (forest != null)
            foreach (var sr in forest.GetComponentsInChildren<SpriteRenderer>()) trees.Add((sr, sr.color));
        var fx = new GameObject("Neige").transform;
        for (int i = 0; i < 80; i++)
        {
            var go = new GameObject("Flocon");
            go.transform.SetParent(fx, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteUtil.Square;
            sr.sortingOrder = 60;
            sr.enabled = false;
            flakes.Add(go.transform);
            flakesR.Add(sr);
        }
        shown = Current;
        Apply(shown, 1f);
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    private void OnNewDayUnsub() { if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay; }
    private void OnDisable() => OnNewDayUnsub();

    private void OnNewDay(int day)
    {
        var s = SeasonOf(day);
        if (s == shown) return;
        fromG = Tint(GrassTint, shown); fromJ = Tint(JungleTint, shown); fromT = Tint(TreeTint, shown);
        fromWinter = shown == Season.Winter;
        shown = s;
        blend = 0f;
        if (!CrawlerMode.Active) GameHUD.Toast($"Une nouvelle saison commence : {Names[(int)s]}", GameHUD.GoldColor);
        if (s == Season.Winter)
            StoryManager.Instance?.PlayOnce("season_winter", new StoryManager.Line(StoryManager.SelfName,
                "Le froid descend sur l'île. Les bêtes se terrent, les ventres crient famine... les miens aussi."));
    }

    private static Color Tint(Color[] arr, Season s) => arr[(int)s];

    private void Apply(Season s, float k)
    {
        var g = Color.Lerp(fromG, Tint(GrassTint, s), k);
        var j = Color.Lerp(fromJ, Tint(JungleTint, s), k);
        var t = Color.Lerp(fromT, Tint(TreeTint, s), k);
        if (grass != null) grass.color = Clamp(g);
        if (jungle != null) jungle.color = Clamp(j);
        if (snowMap != null)
        {
            // la neige se pose (ou fond) au rythme du changement de saison
            float a = s == Season.Winter ? k : (fromWinter ? 1f - k : 0f);
            snowMap.color = new Color(1f, 1f, 1f, a);
            if (snowRenderer != null) snowRenderer.enabled = a > 0.01f;
        }
        foreach (var (sr, c) in trees)
            if (sr != null) sr.color = new Color(Mathf.Min(1f, c.r * t.r), Mathf.Min(1f, c.g * t.g), Mathf.Min(1f, c.b * t.b), c.a);
    }

    private static Color Clamp(Color c) => new Color(Mathf.Min(1f, c.r), Mathf.Min(1f, c.g), Mathf.Min(1f, c.b), 1f);

    private void Update()
    {
        if (blend < 1f)
        {
            blend = Mathf.MoveTowards(blend, 1f, Time.unscaledDeltaTime / 6f);   // la saison s'installe en quelques secondes
            Apply(shown, blend);
        }

        // neige en hiver (à peine visible sous la montagne)
        var cam = Camera.main;
        if (cam == null) return;
        bool home = MainMenu.Instance != null && MainMenu.Instance.InHome;
        var relief = MountainRelief.Instance;
        float roof = relief != null && relief.DungeonView && !CrawlerMode.Active ? 0.25f : 1f;
        snow = Mathf.MoveTowards(snow, shown == Season.Winter && !home ? roof : 0f, Time.unscaledDeltaTime / 3f);
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        for (int i = 0; i < flakes.Count; i++)
        {
            flakesR[i].enabled = snow > 0.01f;
            if (!flakesR[i].enabled) continue;
            var f = flakes[i];
            float sway = Mathf.Sin(Time.time * 1.3f + i) * 0.15f;
            Vector3 p = f.position + new Vector3(sway, -1f, 0f) * (h * 0.18f * Time.deltaTime);
            if (p.y < c.y - h - 1f || Mathf.Abs(p.x - c.x) > w + 2f)
                p = new Vector3(c.x + Random.Range(-w - 1f, w + 1f), c.y + h + Random.Range(0f, 2f), 0f);
            f.position = p;
            float s = 0.05f * h / 6f * (0.7f + (i % 3) * 0.2f);
            f.localScale = new Vector3(s, s, 1f);
            flakesR[i].color = new Color(1f, 1f, 1f, 0.75f * snow);
        }
    }
}
