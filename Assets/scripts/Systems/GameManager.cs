using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Chef d'orchestre de la partie : état du jeu, cycle jour/nuit, vitesse, game over.
/// Il est créé automatiquement dans toute scène contenant un DungeonCore et ajoute
/// les systèmes manquants (mana, réputation, expéditions, histoire, HUD, caméra...).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, GameOver }

    [Header("Cycle jour / nuit")]
    [Tooltip("Durée d'une journée complète en secondes (vitesse x1)")]
    public float dayLengthSeconds = 180f;
    [Range(0f, 0.8f)]
    [Tooltip("Part de la journée qui est la nuit")]
    public float nightPortion = 0.35f;
    [Tooltip("Intensité de la lumière globale la nuit (multiplicateur)")]
    public float nightLightMultiplier = 0.45f;
    public Color nightTint = new Color(0.55f, 0.6f, 1f);
    /// <summary>Météo (IslandEvents) : assombrissement / teinte appliqués par-dessus le cycle jour-nuit.</summary>
    [HideInInspector] public float weatherIntensity = 1f;
    /// <summary>Pénombre souterraine (Atmosphere) : 1 dehors, moins sous la montagne.</summary>
    [HideInInspector] public float caveIntensity = 1f;
    [HideInInspector] public Color weatherTint = Color.white;

    [Header("Vitesse")]
    public float[] speedSteps = { 1f, 2f, 3f, 5f };

    public GameState State { get; private set; } = GameState.Playing;
    public int CurrentDay { get; private set; } = 1;
    public float DayProgress => Mathf.Clamp01(dayTimer / dayLengthSeconds);
    public bool IsNight => DayProgress >= 1f - nightPortion;
    public float GameSpeed { get; private set; } = 1f;
    public bool IsPaused => pauseRequests > 0 || userPaused;
    public bool UserPaused => userPaused;

    // Statistiques de partie
    [HideInInspector] public int statKills;
    [HideInInspector] public int statFled;
    [HideInInspector] public int statExpeditions;
    [HideInInspector] public int statHeroKills;
    [HideInInspector] public int statEvolutions;

    public event Action<int> OnNewDay;
    public event Action<bool> OnNightChanged;
    public event Action OnGameOver;

    private float dayTimer;
    private bool wasNight;
    private int pauseRequests;
    private bool userPaused;

    private Light2D globalLight;
    private float baseLightIntensity = 1f;
    private Color baseLightColor = Color.white;

    // ─────────────────────────────────────────────
    // BOOTSTRAP AUTOMATIQUE
    // ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CreateIfNeeded();
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => CreateIfNeeded();

    private static void CreateIfNeeded()
    {
        if (Instance != null) return;
        if (FindFirstObjectByType<DungeonCore>() == null) return;   // pas une scène de jeu
        var existing = FindFirstObjectByType<GameManager>();
        if (existing != null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Talents.Reset();   // nouvelle partie (une sauvegarde les restaure ensuite)
        Chronicle.Reset();
        Instance = this;
        Time.timeScale = 1f;

        Application.targetFrameRate = 60;
        // Les monstres (et les intrus) se traversent entre alliés : plus de bouchons dans les couloirs
        Physics2D.IgnoreLayerCollision(8, 8, true);
        Physics2D.IgnoreLayerCollision(7, 7, true);
        EnsureSystems();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void EnsureSystems()
    {
        Ensure<DungeonLayers>();
        Ensure<DungeonTerritory>();
        Ensure<ManaSystem>();
        Ensure<ReputationSystem>();
        Ensure<ExpeditionManager>();
        Ensure<StoryManager>();
        Ensure<PossessionController>();
        Ensure<GameHUD>();
        Ensure<WildlifeSpawner>();
        Ensure<Seasons>();
        Ensure<Domination>();
        Ensure<CoreSpells>();
        Ensure<CinematicDirector>();
        Ensure<UpkeepSystem>();
        Ensure<SettlementManager>();
        Ensure<AmbientFx>();
        Ensure<LootSystem>();
        Ensure<RelationSystem>();
        Ensure<RaidSystem>();
        Ensure<GuildSystem>();
        Ensure<MusicManager>();
        Ensure<IslandEvents>();
        Ensure<NemesisSystem>();
        Ensure<ObjectiveSystem>();
        Ensure<Achievements>();
        Ensure<PrisonSystem>();
        Ensure<ThemedRooms>();
        Ensure<InteriorSystem>();
        Ensure<SaveSystem>();
        Ensure<MainMenu>();
        Ensure<ColonyWindow>();
        Ensure<TalentTreeWindow>();

        if (FindFirstObjectByType<PlayerLevel>() == null) gameObject.AddComponent<PlayerLevel>();

        Camera cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraController>() == null)
            cam.gameObject.AddComponent<CameraController>();
    }

    private T Ensure<T>() where T : Component
    {
        T existing = FindFirstObjectByType<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    private void Start()
    {
        foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (l.lightType == Light2D.LightType.Global)
            {
                globalLight = l;
                baseLightIntensity = l.intensity;
                baseLightColor = l.color;
                break;
            }
        }

        AdventurerAI.FixGraphCorners();
        Atmosphere.Ensure();
        dayTimer = dayLengthSeconds * 0.05f;
        OnNewDay?.Invoke(CurrentDay);
    }

    private void Update()
    {
        if (State != GameState.Playing || MainMenu.Frozen) return;   // l'accueil fige la partie

        dayTimer += Time.deltaTime;
        if (dayTimer >= dayLengthSeconds)
        {
            dayTimer -= dayLengthSeconds;
            CurrentDay++;
            OnNewDay?.Invoke(CurrentDay);
        }

        bool night = IsNight;
        if (night != wasNight)
        {
            wasNight = night;
            OnNightChanged?.Invoke(night);
        }

        UpdateLighting();
    }

    /// <summary>La cinématique d'intro pilote elle-même la lumière (coucher de soleil, nuits qui défilent...).</summary>
    public static bool LightingOverride;

    private void UpdateLighting()
    {
        if (globalLight == null || LightingOverride) return;

        // 0 = plein jour, 1 = pleine nuit, avec transitions douces
        float p = DayProgress;
        float nightStart = 1f - nightPortion;
        float fade = 0.06f;
        float n;
        if (p < nightStart - fade) n = Mathf.Clamp01(1f - p / fade);          // aube
        else if (p < nightStart) n = (p - (nightStart - fade)) / fade;          // crépuscule
        else n = 1f;
        n = Mathf.SmoothStep(0f, 1f, n);

        globalLight.intensity = Mathf.Lerp(baseLightIntensity, baseLightIntensity * nightLightMultiplier, n) * weatherIntensity * caveIntensity;
        globalLight.color = Color.Lerp(baseLightColor, baseLightColor * nightTint, n) * weatherTint;
    }

    // ─────────────────────────────────────────────
    // VITESSE & PAUSE
    // ─────────────────────────────────────────────

    public void SetSpeed(float speed)
    {
        GameSpeed = speed;
        userPaused = false;
        ApplyTimeScale();
    }

    public void ToggleUserPause()
    {
        userPaused = !userPaused;
        ApplyTimeScale();
    }

    /// <summary>Pause demandée par un système (dialogue...). À équilibrer avec PopPause.</summary>
    public void PushPause()
    {
        pauseRequests++;
        ApplyTimeScale();
    }

    public void PopPause()
    {
        pauseRequests = Mathf.Max(0, pauseRequests - 1);
        ApplyTimeScale();
    }

    private void ApplyTimeScale()
    {
        if (State == GameState.GameOver) { Time.timeScale = 0f; return; }
        Time.timeScale = IsPaused ? 0f : GameSpeed;
    }

    // ─────────────────────────────────────────────
    // FIN DE PARTIE
    // ─────────────────────────────────────────────

    public float DayTimer => dayTimer;

    /// <summary>Chargement d'une sauvegarde : reprend le jour et l'heure sans déclencher les événements.</summary>
    public void RestoreTime(int day, float timer)
    {
        CurrentDay = Mathf.Max(1, day);
        dayTimer = Mathf.Clamp(timer, 0f, dayLengthSeconds - 0.01f);
        wasNight = IsNight;
    }

    public void GameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;
        Time.timeScale = 0f;
        OnGameOver?.Invoke();
    }

    public void Restart()
    {
        MainMenu.StartMode = MainMenu.Boot.NewGame;   // « Renaître » : nouvelle partie directe
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
            ? SceneManager.GetActiveScene().name
            : SceneManager.GetActiveScene().path);
    }

    /// <summary>Debug : saute à un moment de la journée (0-1).</summary>
    public void DebugSetDayProgress(float p) => dayTimer = Mathf.Clamp01(p) * dayLengthSeconds;

    public string GetTimeOfDayLabel()
    {
        float p = DayProgress;
        if (IsNight) return "Nuit";
        if (p < 0.12f) return "Aube";
        if (p < 1f - nightPortion - 0.1f) return "Jour";
        return "Crépuscule";
    }
}
