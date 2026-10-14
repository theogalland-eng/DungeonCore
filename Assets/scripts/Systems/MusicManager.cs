using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Musique et ambiance sonore, choisies selon la situation, avec fondus enchaînés.
///
/// MUSIQUES : fichiers (.mp3 ou .ogg) dans Assets/Resources/Audio/Music. La catégorie est déduite
/// du nom (mots-clés, voir CategoryOf) : morning / midday / afternoon / night (calme selon l'heure),
/// attack_not critics (intrus), attack_critics (cœur menacé), hero, cinematic, general / menu.
/// Les fichiers « intro » sont réservés à la future cinématique d'introduction (ignorés ici).
/// Plusieurs morceaux par catégorie → tirage au hasard, avec des silences entre deux morceaux.
/// Catégorie absente → repli (critical → danger → night → day → general).
///
/// AMBIANCES (boucles) : Assets/Resources/Audio/Ambience/amb_island, amb_dungeon, amb_danger
/// (générées par Tools~/make_ambience.py en attendant mieux). La couche « danger » (battements de cœur)
/// monte quand des intrus sont dans le donjon — sans jamais prévenir de leur arrivée.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Tooltip("Durée des fondus enchaînés (s)")]
    public float crossfade = 3f;
    [Tooltip("Silence entre deux morceaux (min, max, en secondes)")]
    public Vector2 silenceBetweenTracks = new Vector2(8f, 25f);

    /// <summary>Silence de cinématique : la musique se coupe net (l'ambiance baisse), puis revient en fondu.</summary>
    public static bool Silence;

    private readonly Dictionary<string, List<AudioClip>> music = new Dictionary<string, List<AudioClip>>();
    private AudioSource musicA, musicB, ambA, ambB, danger;
    private AudioSource musicCur, ambCur;
    private string category = "";
    private string ambName = "";
    private float silenceUntil;
    private AudioClip lastClip;
    private float dangerLevel;
    private float nextCheck;

    private static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>
    {
        { "hero", "critical" }, { "critical", "danger" }, { "danger", "night" }, { "night", "general" },
        { "morning", "day" }, { "midday", "day" }, { "afternoon", "day" }, { "day", "general" },
        { "cinematic", "general" }, { "menu", "general" },
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        foreach (var c in Resources.LoadAll<AudioClip>("Audio/Music"))
        {
            string cat = CategoryOf(c.name);
            if (cat == null) continue;
            if (!music.TryGetValue(cat, out var list)) music[cat] = list = new List<AudioClip>();
            list.Add(c);
        }
        musicA = NewSource("Musique A", false);
        musicB = NewSource("Musique B", false);
        ambA = NewSource("Ambiance A", true);
        ambB = NewSource("Ambiance B", true);
        danger = NewSource("Ambiance danger", true);
        danger.clip = Resources.Load<AudioClip>("Audio/Ambience/amb_danger");
        if (danger.clip != null) { danger.volume = 0f; danger.Play(); }
        musicCur = musicA;
        ambCur = ambA;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioSource NewSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        s.volume = 0f;
        return s;
    }

    // ─────────────────────────────────────────────
    // SITUATION
    // ─────────────────────────────────────────────

    /// <summary>Catégorie d'un morceau d'après son nom (null : ignoré, ex. l'intro réservée).</summary>
    public static string CategoryOf(string fileName)
    {
        string n = fileName.ToLowerInvariant();
        if (n.Contains("intro")) return null;
        if (n.Contains("attack") || n.Contains("danger") || n.Contains("combat"))
            return n.Contains("not") || n.Contains("non") ? "danger" : n.Contains("critic") ? "critical" : "danger";
        if (n.Contains("hero") || n.Contains("heros")) return "hero";
        if (n.Contains("cinemat")) return "cinematic";
        if (n.Contains("night") || n.Contains("nuit")) return "night";
        if (n.Contains("afternoon")) return "afternoon";
        if (n.Contains("midday") || n.Contains("noon")) return "midday";
        if (n.Contains("morning") || n.Contains("dawn") || n.Contains("matin")) return "morning";
        if (n.Contains("menu")) return "menu";
        if (n.Contains("day") || n.Contains("jour")) return "day";
        if (n.Contains("general")) return "general";
        return n.Split('_')[0];
    }

    /// <summary>Morceaux « calmes » : on laisse finir le morceau en cours quand l'heure tourne.</summary>
    private static bool IsCalm(string cat) => cat == "morning" || cat == "midday" || cat == "afternoon" || cat == "day" || cat == "night" || cat == "general";

    private string WantedCategory()
    {
        var menu = MainMenu.Instance;
        if (menu != null && menu.InHome) return "menu";
        if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying) return "cinematic";
        if (CrawlerMode.Active && CrawlerMode.Instance.Player != null)
        {
            var pp = CrawlerMode.Instance.Player.transform.position;
            var cc = CrawlerMode.Instance.Core;
            if (cc != null && Vector2.Distance(cc.transform.position, pp) < 7f) return "critical";
            foreach (var g in GobelinAI.All)
                if (g != null && g.IsAlive() && Vector2.Distance(g.transform.position, pp) < 6f) return "danger";
        }
        var exp = ExpeditionManager.Instance;
        bool inside = ManaSystem.Instance != null && ManaSystem.Instance.IntrudersInside > 0;
        if (exp != null && exp.ExpeditionActive && (exp.CurrentRank >= 7 || (exp.CurrentExpeditionTier != null && exp.CurrentExpeditionTier.displayName == "Héros")) && inside)
            return "hero";
        // Cœur menacé : blessé, ou un intrus tout près du cristal
        var core = CoreRef;
        if (inside && core != null)
        {
            bool hurt = core.currentHealth < core.maxHealth * 0.6f;
            bool close = false;
            foreach (var a in AdventurerAI.All)
                if (a != null && a.IsInsideDungeon && Vector2.Distance(a.transform.position, core.transform.position) < 4f) { close = true; break; }
            if (hurt || close) return "critical";
        }
        if (inside || (RaidSystem.Instance != null && RaidSystem.Instance.Active)) return "danger";
        var gm = GameManager.Instance;
        if (gm == null) return "day";
        if (gm.IsNight) return "night";
        float p = gm.DayProgress;
        return p < 0.22f ? "morning" : p < 0.44f ? "midday" : "afternoon";
    }

    /// <summary>Résumé (tests) : morceaux par catégorie, catégorie voulue, morceau en cours.</summary>
    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in music) sb.Append($"{kv.Key}={kv.Value.Count} ");
        sb.Append($"| want={WantedCategory()} cur={playingCat} playing={(musicCur.isPlaying && musicCur.clip != null ? musicCur.clip.name : "-")}");
        return sb.ToString();
    }

    private DungeonCore core;
    private DungeonCore CoreRef => core != null ? core : (core = FindFirstObjectByType<DungeonCore>());

    private List<AudioClip> Clips(string cat)
    {
        for (int i = 0; i < 5 && cat != null; i++)
        {
            if (music.TryGetValue(cat, out var l) && l.Count > 0) return l;
            cat = Fallback.TryGetValue(cat, out var f) ? f : null;
        }
        return null;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float mVol = GameSettings.MusicVolume, aVol = GameSettings.AmbienceVolume;

        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.5f;
            UpdateMusicChoice();
            UpdateAmbienceChoice();
        }

        // Fondus : la source courante monte, l'autre s'éteint
        float step = dt / Mathf.Max(0.1f, crossfade);
        if (Silence)
        {
            musicA.volume = Mathf.MoveTowards(musicA.volume, 0f, dt / 0.25f);
            musicB.volume = Mathf.MoveTowards(musicB.volume, 0f, dt / 0.25f);
            ambCur.volume = Mathf.MoveTowards(ambCur.volume, aVol * 0.3f, dt / 0.25f);
            return;
        }
        Fade(musicCur, mVol, step);
        Fade(musicCur == musicA ? musicB : musicA, 0f, step);
        Fade(ambCur, musicCur.isPlaying ? aVol * 0.55f : aVol, step);
        Fade(ambCur == ambA ? ambB : ambA, 0f, step);

        // Couche de danger : cœur qui s'emballe quand des intrus rôdent dans le donjon
        var exp = ExpeditionManager.Instance;
        bool home = MainMenu.Instance != null && MainMenu.Instance.InHome;
        int inside = ManaSystem.Instance != null ? ManaSystem.Instance.IntrudersInside : 0;
        float target = home ? 0f : inside > 0 ? Mathf.Clamp01(0.55f + 0.15f * inside) : 0f;
        dangerLevel = Mathf.MoveTowards(dangerLevel, target, dt / 2.5f);
        if (danger.clip != null)
        {
            danger.volume = dangerLevel * aVol;
            danger.pitch = 1f + 0.12f * Mathf.Clamp01(inside / 4f);   // plus ils sont nombreux, plus le cœur bat vite
        }
    }

    private static void Fade(AudioSource s, float target, float step)
    {
        s.volume = Mathf.MoveTowards(s.volume, s.clip != null && s.isPlaying ? target : 0f, step);
        if (s.volume <= 0.001f && target <= 0f && s.isPlaying) s.Stop();
    }

    private string playingCat = "";

    private static bool IsCombat(string c) => c == "danger" || c == "critical" || c == "hero";

    /// <summary>
    /// Un morceau va toujours jusqu'au bout... sauf les musiques de combat : le combat coupe le calme,
    /// et quand le combat s'arrête (ou s'intensifie), sa musique s'interrompt aussi. Les cinématiques coupent tout.
    /// </summary>
    private void UpdateMusicChoice()
    {
        string want = WantedCategory();
        category = want;
        bool playing = musicCur.isPlaying && musicCur.clip != null;
        if (playing)
        {
            if (want == playingCat) return;
            bool cut = IsCombat(want) || want == "cinematic" || IsCombat(playingCat) || playingCat == "cinematic";
            if (!cut) return;   // le morceau calme se termine avant de laisser place au suivant
            if ((IsCombat(playingCat) || playingCat == "cinematic") && !IsCombat(want) && want != "cinematic")
            {
                // fin du combat : la musique s'éteint en fondu, puis un court silence avant le calme
                FadeOutCurrent();
                silenceUntil = Time.unscaledTime + 4f;
                return;
            }
            StartTrack(want);
            return;
        }
        if (Time.unscaledTime < silenceUntil && !IsCombat(want) && want != "cinematic") return;
        StartTrack(want);
    }

    private void FadeOutCurrent()
    {
        var other = musicCur == musicA ? musicB : musicA;
        other.Stop();
        other.clip = null;
        musicCur = other;
        playingCat = "";
    }

    private void StartTrack(string want)
    {
        var clips = Clips(want);
        if (clips == null)
        {
            // pas de musique pour cette situation : le morceau en cours s'éteint en fondu
            if (musicCur.isPlaying) FadeOutCurrent();
            return;
        }
        // un nouveau morceau (jamais deux fois de suite le même)
        AudioClip clip = clips[Random.Range(0, clips.Count)];
        if (clips.Count > 1 && clip == lastClip) clip = clips[(clips.IndexOf(clip) + 1) % clips.Count];
        lastClip = clip;
        var next = musicCur == musicA ? musicB : musicA;
        next.clip = clip;
        next.volume = 0f;
        next.Play();
        musicCur = next;
        playingCat = want;
        // à la fin du morceau : un silence avant le suivant (la peur vit aussi dans le silence)
        silenceUntil = Time.unscaledTime + clip.length + Random.Range(silenceBetweenTracks.x, silenceBetweenTracks.y);
    }

    private void UpdateAmbienceChoice()
    {
        bool home = MainMenu.Instance != null && MainMenu.Instance.InHome;
        var relief = MountainRelief.Instance;
        bool dungeonView = !home && (relief == null || relief.DungeonView);
        string want = dungeonView ? "amb_dungeon" : "amb_island";
        if (want == ambName) return;
        ambName = want;
        var clip = Resources.Load<AudioClip>("Audio/Ambience/" + want);
        if (clip == null) return;
        var next = ambCur == ambA ? ambB : ambA;
        next.clip = clip;
        next.volume = 0f;
        // les deux boucles font la même durée : on reprend au même instant pour un fondu fluide
        next.time = ambCur.clip != null && ambCur.isPlaying ? Mathf.Repeat(ambCur.time, clip.length) : Random.Range(0f, clip.length);
        next.Play();
        ambCur = next;
    }
}
