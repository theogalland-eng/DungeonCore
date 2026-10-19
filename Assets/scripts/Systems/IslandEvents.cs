using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public enum IslandEvent { None, Storm, Fog, Eruption, BloodMoon, Rain }

/// <summary>
/// Événements imprévisibles de l'île (jamais annoncés à l'avance) :
///   - TEMPÊTE : pluie, éclairs, aucun bateau ; à la fin, des naufragés peuvent s'échouer sur la plage.
///   - BROUILLARD : les intrus voient deux fois moins loin et se perdent (ils paniquent plus vite).
///   - RÉVEIL DU VOLCAN : mana ×1,8, geysers, secousses qui ébranlent (et blessent) les intrus.
///   - LUNE DE SANG : la nuit rougit, les monstres frappent 25 % plus fort.
///   - PLUIE (fréquente en automne) : les torches des intrus restés dehors s'éteignent ; dans le donjon ils voient moins loin.
/// Et chaque matin, parfois, une BRUME qui se lève vers le milieu de matinée.
/// Pendant la tempête, chaque coup de tonnerre fait sursauter (et parfois fuir) les intrus du donjon ;
/// pendant le réveil du volcan, les coulées de lave avancent, il pleut des cendres et les monstres de feu s'enhardissent.
/// </summary>
public class IslandEvents : MonoBehaviour
{
    public static IslandEvents Instance { get; private set; }

    [Tooltip("Chance qu'un événement commence à l'aube (hors Facile : un peu plus)")]
    [Range(0f, 1f)] public float dailyChance = 0.2f;
    public int firstEventDay = 4;

    public IslandEvent Current { get; private set; }
    public int EndsDay { get; private set; } = -1;

    // Effets lus par les autres systèmes
    public bool BlocksBoats => Current == IslandEvent.Storm;
    public bool Raining => Current == IslandEvent.Rain || Current == IslandEvent.Storm;
    public bool MorningFog => morningFog && GameManager.Instance != null && GameManager.Instance.DayProgress < 0.16f;
    public float DetectionMult => Current == IslandEvent.Fog ? 0.5f : Current == IslandEvent.Storm ? 0.8f : MorningFog ? 0.75f : Current == IslandEvent.Rain ? 0.9f : 1f;
    /// <summary>Monstres de feu (diablotins, démons) pendant le réveil du volcan.</summary>
    public static float FireMult(string family) =>
        Instance != null && Instance.Current == IslandEvent.Eruption && (family == "imp" || family == "demon") ? 1.25f : 1f;
    private bool morningFog;
    public float FleeBonus => Current == IslandEvent.Fog ? 0.15f : 0f;
    public float ManaMult => Current == IslandEvent.Eruption ? 1.8f : 1f;
    public float MonsterDamageMult => Current == IslandEvent.BloodMoon && GameManager.Instance != null && GameManager.Instance.IsNight ? 1.25f : 1f;
    public static float Detection => Instance != null ? Instance.DetectionMult : 1f;

    public static readonly string[] Names = { "", "Tempête", "Brouillard", "Réveil du volcan", "Lune de sang", "Pluie" };
    public string Name => Names[(int)Current];

    // Pluie : un seul système de particules (un seul dessin, simulé par le moteur) au lieu de 110 sprites éclairés
    // déplacés un à un ; brume : une seule nappe non éclairée au lieu de 9 calques plein écran (très coûteux sur téléphone).
    private ParticleSystem rainPs;
    private ParticleSystemRenderer rainRenderer;
    private SpriteRenderer fogSr;
    private static Material unlitMat;
    private Transform fx;
    private float nextThunder, nextQuake, nextGeyser, flash;
    private float weather;       // 0 → 1 : intensité visuelle de l'événement
    private AudioSource rain;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
        fx = new GameObject("Météo").transform;
        if (unlitMat == null) { var sh = Shader.Find("Sprites/Default"); if (sh != null) unlitMat = new Material(sh); }
        var pgo = new GameObject("Pluie");
        pgo.transform.SetParent(fx, false);
        rainPs = pgo.AddComponent<ParticleSystem>();
        rainPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = rainPs.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.maxParticles = 240;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        var shape = rainPs.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        var vel = rainPs.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        var em = rainPs.emission;
        em.rateOverTime = 0f;
        rainRenderer = pgo.GetComponent<ParticleSystemRenderer>();
        rainRenderer.sharedMaterial = unlitMat;
        rainRenderer.sortingOrder = 60;
        rainPs.Play();

        var fgo = new GameObject("Brume");
        fgo.transform.SetParent(fx, false);
        fogSr = fgo.AddComponent<SpriteRenderer>();
        fogSr.sprite = SpriteUtil.Glow;
        if (unlitMat != null) fogSr.sharedMaterial = unlitMat;
        fogSr.sortingOrder = 59;
        fogSr.color = new Color(0.8f, 0.82f, 0.86f, 0f);
        fogSr.enabled = false;
        rain = gameObject.AddComponent<AudioSource>();
        rain.clip = Resources.Load<AudioClip>("Audio/Ambience/amb_rain");
        rain.loop = true;
        rain.volume = 0f;
        rain.ignoreListenerPause = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private void OnNewDay(int day)
    {
        morningFog = Current == IslandEvent.None && Random.value < 0.3f;
        if (Current != IslandEvent.None)
        {
            if (day >= EndsDay) End();
            return;
        }
        // pluie ordinaire, selon la saison
        if (day >= 2 && Random.value < Seasons.RainChance) { Begin(IslandEvent.Rain, day + 1); return; }
        if (day < firstEventDay) return;
        float chance = dailyChance + (int)GameDifficulty.Current * 0.03f;
        if (Random.value > chance) return;
        float r = Random.value;
        var e = r < 0.35f ? IslandEvent.Storm : r < 0.6f ? IslandEvent.Fog : r < 0.8f ? IslandEvent.BloodMoon : IslandEvent.Eruption;
        Begin(e, day + (e == IslandEvent.Storm ? Random.Range(1, 3) : 1));
    }

    public void Begin(IslandEvent e, int endsDay)
    {
        Current = e;
        EndsDay = endsDay;
        Chronicle.Add($"{Names[(int)e]} sur l'île.");
        nextThunder = Time.time + Random.Range(3f, 8f);
        nextQuake = Time.time + Random.Range(10f, 25f);
        switch (e)
        {
            case IslandEvent.Storm:
                GameHUD.Toast("Le ciel se déchire : une tempête s'abat sur l'île", GameHUD.WarningColor);
                StoryManager.Instance?.PlayOnce("event_storm", new StoryManager.Line(StoryManager.SelfName,
                    "La mer se soulève, le vent hurle dans mes galeries. Aucun bateau ne s'y risquera... ni ne pourra repartir."));
                break;
            case IslandEvent.Fog:
                GameHUD.Toast("Un brouillard épais recouvre l'île", GameHUD.InfoColor);
                StoryManager.Instance?.PlayOnce("event_fog", new StoryManager.Line(StoryManager.SelfName,
                    "Un brouillard à couper au couteau. Ceux qui entreront aujourd'hui ne verront mes monstres qu'au dernier moment."));
                break;
            case IslandEvent.Eruption:
                GameHUD.Toast("Le volcan gronde : le mana de la montagne bouillonne", GameHUD.ManaColor);
                StoryManager.Instance?.PlayOnce("event_eruption", new StoryManager.Line(StoryManager.SelfName,
                    "La montagne s'éveille avec moi. Je sens le magma pulser dans la roche... et le mana avec lui."));
                break;
            case IslandEvent.Rain:
                GameHUD.Toast("La pluie tombe : les torches des intrus ne tiendront pas", GameHUD.InfoColor);
                break;
            case IslandEvent.BloodMoon:
                GameHUD.Toast("Ce soir, la lune sera rouge", GameHUD.DangerColor);
                StoryManager.Instance?.PlayOnce("event_bloodmoon", new StoryManager.Line(StoryManager.SelfName,
                    "Une lune de sang. Mes créatures s'agitent : cette nuit, elles auront soif."));
                break;
        }
    }

    private void End()
    {
        var was = Current;
        Current = IslandEvent.None;
        EndsDay = -1;
        if (was == IslandEvent.Eruption) CoolLava();
        if (was == IslandEvent.Storm && Random.value < 0.6f) ExpeditionManager.Instance?.LaunchShipwreck();
        else if (was != IslandEvent.None) GameHUD.Toast("Le calme revient sur l'île", GameHUD.InfoColor);
    }

    // ─────────────────────────────────────────────
    // EFFETS VISUELS ET SONORES
    // ─────────────────────────────────────────────

    private void Update()
    {
        var cam = Camera.main;
        if (cam == null || fx == null) return;
        bool home = MainMenu.Instance != null && MainMenu.Instance.InHome;
        float target = home ? 0f : Current == IslandEvent.None ? 0f : 1f;
        weather = Mathf.MoveTowards(weather, target, Time.unscaledDeltaTime / 3f);
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        float dt = Time.deltaTime;

        // Pluie (tempête)
        // sous la montagne (vue donjon), on n'aperçoit la pluie qu'à peine
        var reliefV = MountainRelief.Instance;
        float roof = reliefV != null && reliefV.DungeonView ? 0.3f : 1f;
        bool ash = Current == IslandEvent.Eruption;
        float rainA = (Current == IslandEvent.Storm ? weather : Current == IslandEvent.Rain ? weather * 0.6f : ash ? weather * 0.5f : 0f) * roof;
        UpdateRain(rainA, ash, c, w, h);

        // Brume (brouillard, un peu pendant la tempête)
        morningFogA = Mathf.MoveTowards(morningFogA, MorningFog && !home ? 1f : 0f, Time.unscaledDeltaTime / 4f);
        float fogA = (Current == IslandEvent.Fog ? weather * 0.5f : Current == IslandEvent.Storm ? weather * 0.15f : 0f) * (roof < 1f ? 0.6f : 1f);
        fogA = Mathf.Max(fogA, morningFogA * 0.28f * (roof < 1f ? 0.4f : 1f));
        fogSr.enabled = fogA > 0.01f;
        if (fogSr.enabled)
        {
            float t = Time.time * 0.03f;
            fogSr.transform.position = new Vector3(c.x + Mathf.Sin(t) * w * 0.15f, c.y + Mathf.Cos(t * 0.8f) * h * 0.1f, 0f);
            fogSr.transform.localScale = new Vector3(w * 3.2f, h * 3.2f, 1f);   // une nappe douce qui couvre l'écran
            fogSr.color = new Color(0.82f, 0.84f, 0.88f, Mathf.Min(0.75f, fogA * 1.8f));
        }

        // Éclairs, secousses, geysers
        if (!home && Current == IslandEvent.Storm && Time.time > nextThunder)
        {
            nextThunder = Time.time + Random.Range(8f, 20f);
            flash = 1f;
            Sfx.Play("thunder", null, 0.8f, 0.1f, 1f);
            Thunderstruck();
        }
        if (!home && Current == IslandEvent.Eruption)
        {
            if (Time.time > nextQuake)
            {
                nextQuake = Time.time + Random.Range(18f, 40f);
                Quake();
            }
            if (Time.time > nextLava) { nextLava = Time.time + Random.Range(12f, 22f); AdvanceLava(); }
            var relief = MountainRelief.Instance;
            if (relief != null && relief.craterWorld != Vector2.zero && Time.time > nextGeyser)
            {
                nextGeyser = Time.time + Random.Range(2f, 5f);
                Fx.Play("magma_geyser", (Vector3)relief.craterWorld + new Vector3(Random.Range(-1f, 1f), 0.9f, 0f), 3.2f, 12f, 41, null, false, null, new Color(1f, 0.5f, 0.2f));
            }
        }
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 3f);

        // Lumière : plus sombre sous la tempête, rougeâtre pendant l'éruption / la lune de sang
        var gm = GameManager.Instance;
        if (gm != null)
        {
            Color tint = Color.white;
            float dim = 1f;
            if (Current == IslandEvent.Storm) { dim = 0.72f; tint = new Color(0.8f, 0.85f, 1f); }
            else if (Current == IslandEvent.Fog) { dim = 0.9f; tint = new Color(0.92f, 0.94f, 1f); }
            else if (Current == IslandEvent.Eruption) tint = new Color(1f, 0.82f, 0.7f);
            else if (Current == IslandEvent.BloodMoon && gm.IsNight) tint = new Color(1f, 0.55f, 0.5f);
            gm.weatherIntensity = Mathf.Lerp(1f, dim, weather) + flash * 1.4f;
            gm.weatherTint = Color.Lerp(Color.white, tint, weather);
        }

        WetTorches();
        if (rain.clip != null)
        {
            float v = (Current == IslandEvent.Storm ? 0.8f : Current == IslandEvent.Rain ? 0.5f : 0f) * (home ? 0f : weather * GameSettings.AmbienceVolume);
            rain.volume = Mathf.MoveTowards(rain.volume, v, Time.unscaledDeltaTime * 0.3f);
            if (rain.volume > 0f && !rain.isPlaying) rain.Play();
            else if (rain.volume <= 0f && rain.isPlaying) rain.Stop();
        }
    }

    private float morningFogA, nextLava, nextWet;

    private float rainRate = -1f, rainH = -1f;
    private bool rainAsh;

    /// <summary>Gouttes (ou cendres) émises au-dessus de l'écran ; on ne touche aux réglages que s'ils changent.</summary>
    private void UpdateRain(float a, bool ash, Vector2 c, float w, float h)
    {
        if (rainPs == null) return;
        rainPs.transform.position = new Vector3(c.x + (ash ? 0f : w * 0.25f), c.y + h + 1f, 0f);
        float rate = a > 0.01f ? (ash ? 45f : 170f) * Mathf.Clamp01(a * 1.6f) : 0f;
        if (Mathf.Abs(rate - rainRate) > 2f) { var em = rainPs.emission; em.rateOverTime = rate; rainRate = rate; }
        if (Mathf.Abs(h - rainH) < 0.25f && ash == rainAsh && rate > 0f) { var mm = rainPs.main; mm.startColor = RainColor(a, ash); return; }
        rainH = h; rainAsh = ash;
        var main = rainPs.main;
        var shape = rainPs.shape;
        shape.scale = new Vector3(w * 2.6f, 0.2f, 1f);
        var vel = rainPs.velocityOverLifetime;
        if (ash)
        {
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f * h, 0.15f * h);
            vel.y = new ParticleSystem.MinMaxCurve(-0.25f * h);
            main.startLifetime = (2f * h + 2f) / (0.25f * h);
            main.startSize = 0.05f * h / 6f;
            rainRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        else
        {
            vel.x = new ParticleSystem.MinMaxCurve(-0.4f * h);
            vel.y = new ParticleSystem.MinMaxCurve(-1.6f * h);
            main.startLifetime = (2f * h + 2f) / (1.6f * h);
            main.startSize = 0.025f * h / 6f;
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.velocityScale = 0.044f;   // longueur du trait ≈ celle des anciennes gouttes
            rainRenderer.lengthScale = 1f;
        }
        vel.z = new ParticleSystem.MinMaxCurve(0f);
        main.startColor = RainColor(a, ash);
    }

    private static Color RainColor(float a, bool ash) => ash ? new Color(0.45f, 0.42f, 0.45f, 0.6f) : new Color(0.7f, 0.8f, 1f, 0.35f);

    /// <summary>Pluie : les intrus dehors ont la torche mouillée (éteinte) ; un repos au camp la ranime.</summary>
    private void WetTorches()
    {
        if (Time.time < nextWet) return;
        nextWet = Time.time + 1f;
        foreach (var a in AdventurerAI.All)
        {
            if (a == null) continue;
            if (Raining && !a.IsInsideDungeon && !a.TorchWet)
            {
                a.TorchWet = true;
                a.Speak("Ma torche ! Elle est trempée...", new Color(0.75f, 0.85f, 1f));
            }
            else if (a.TorchWet && !Raining && a.State == AdventurerAI.AdventurerState.Healing) a.TorchWet = false;
        }
    }

    /// <summary>Coup de tonnerre : dans le donjon, les intrus sursautent ; les peureux détalent.</summary>
    private void Thunderstruck()
    {
        foreach (var a in new List<AdventurerAI>(AdventurerAI.All))
        {
            if (a == null || a.IsDead || !a.IsInsideDungeon) continue;
            if (a.Life != null && a.Life.Has(AdventurerLife.Trait.Coward) && Random.value < 0.5f)
            {
                a.Speak("Le ciel nous maudit !", new Color(0.9f, 0.9f, 0.65f), true);
                a.Life.Reveal(AdventurerLife.Trait.Coward);
                a.LifePanic();
            }
            else if (Random.value < 0.35f) { a.Hold(1f); a.Speak("Qu'est-ce que c'était ?!", new Color(0.8f, 0.85f, 1f)); }
        }
    }

    // ── Réveil du volcan : les coulées avancent, puis refroidissent ──
    private Tilemap lavaMap, blockedMap, solMap;
    private TileBase lavaTile, lavaCollider;
    private readonly List<Vector3Int> newLava = new List<Vector3Int>();
    private readonly List<GameObject> newGlow = new List<GameObject>();

    private bool FindLavaMaps()
    {
        if (lavaMap != null) return lavaTile != null;
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name == "T_Lave") lavaMap = tm;
            else if (tm.name == "NoPathfinding") blockedMap = tm;
            else if (tm.name == "Sol") solMap = tm;
        }
        if (lavaMap == null || blockedMap == null) return false;
        foreach (var c in lavaMap.cellBounds.allPositionsWithin)
            if (lavaMap.HasTile(c)) { lavaTile = lavaMap.GetTile(c); lavaCollider = blockedMap.GetTile(c); break; }
        return lavaTile != null;
    }

    private void AdvanceLava()
    {
        if (!FindLavaMaps() || lavaCollider == null) return;
        var cells = new List<Vector3Int>();
        foreach (var c in lavaMap.cellBounds.allPositionsWithin) if (lavaMap.HasTile(c)) cells.Add(c);
        if (cells.Count == 0) return;
        var core = FindFirstObjectByType<DungeonCore>();
        for (int tries = 0; tries < 30; tries++)
        {
            var from = cells[Random.Range(0, cells.Count)];
            var d = DungeonTerritory.Dirs[Random.Range(0, 4)];
            var n = from + d;
            if (lavaMap.HasTile(n) || blockedMap.HasTile(n) || solMap == null) continue;
            var ground = solMap.GetTile(n);
            if (ground == null || ground.name != "ground_soil") continue;              // roche des hautes terres seulement
            Vector2 wp = solMap.GetCellCenterWorld(n);
            if (DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(wp)) continue;
            if (core != null && Vector2.Distance(core.transform.position, wp) < 6f) continue;
            if (!NearRock(n)) continue;
            lavaMap.SetTile(n, lavaTile);
            blockedMap.SetTile(n, lavaCollider);
            newLava.Add(n);
            var g = new GameObject("Lave nouvelle");
            g.transform.position = wp;
            var l = g.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
            l.lightType = Light2D.LightType.Point; l.color = new Color(1f, 0.45f, 0.15f); l.intensity = 0.7f; l.pointLightOuterRadius = 2.4f;
            g.AddComponent<LavaGlow>();
            newGlow.Add(g);
            foreach (var a in AdventurerAI.All)
                if (a != null && a.combat != null && Vector2.Distance(a.transform.position, wp) < 0.8f) a.combat.TakeDamage(15f);
            Fx.Play("smoke_rise", wp, 1f);
            var tc = blockedMap.GetComponent<TilemapCollider2D>();
            if (tc != null) tc.ProcessTilemapChanges();
            AstarPath.active?.UpdateGraphs(new Bounds(wp, Vector3.one * 2f));
            return;
        }
    }

    private bool NearRock(Vector3Int c)
    {
        Tilemap rock = null;
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None)) if (tm.name == "T_Roche") { rock = tm; break; }
        return rock == null || rock.HasTile(c);
    }

    /// <summary>Fin de l'éruption : la lave nouvelle refroidit et redevient de la roche praticable.</summary>
    private void CoolLava()
    {
        if (lavaMap == null) return;
        foreach (var c in newLava)
        {
            lavaMap.SetTile(c, null);
            blockedMap.SetTile(c, null);
            Fx.Play("smoke_rise", solMap.GetCellCenterWorld(c), 1f);
        }
        foreach (var g in newGlow) if (g != null) Destroy(g);
        if (newLava.Count > 0)
        {
            var tc = blockedMap.GetComponent<TilemapCollider2D>();
            if (tc != null) tc.ProcessTilemapChanges();
            AstarPath.active?.Scan();
            GameHUD.Toast("Les coulées refroidissent", GameHUD.InfoColor);
        }
        newLava.Clear();
        newGlow.Clear();
    }

    /// <summary>La montagne tremble : les intrus dans le donjon sont secoués et blessés.</summary>
    private void Quake()
    {
        CameraController.Instance?.Shake(0.25f, 1.2f);
        Sfx.Play("explosion", null, 0.5f, 0.2f, 1f);
        int n = 0;
        foreach (var a in new List<AdventurerAI>(AdventurerAI.All))
        {
            if (a == null || !a.IsInsideDungeon || a.combat == null) continue;
            a.Hold(1.2f);
            a.combat.TakeDamage(a.combat.maxHealth * 0.06f);
            n++;
        }
        GameHUD.Toast(n > 0 ? "La montagne tremble ! Les intrus perdent l'équilibre" : "La montagne tremble...", GameHUD.WarningColor);
    }

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    public void Capture(SaveData d)
    {
        d.islandEvent = (int)Current;
        d.islandEventEnds = EndsDay;
    }

    public void Restore(SaveData d)
    {
        Current = (IslandEvent)Mathf.Clamp(d.islandEvent, 0, 5);
        EndsDay = d.islandEventEnds;
    }
}
