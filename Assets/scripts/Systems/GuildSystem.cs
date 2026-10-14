using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La Guilde des Aventuriers (sur le continent) : elle classe les aventuriers ET les donjons
/// de F (débutants) à SSS (les héros, élite des nations, presque des demi-dieux).
///
/// Déroulé :
///   1. Des pêcheurs (ou des disparitions) signalent le donjon → la Guilde envoie, quelques jours plus tard,
///      une équipe d'ÉVALUATION (rang C par défaut, plus haut si les témoins parlent de monstres terrifiants,
///      de pièges ou de morts).
///   2. Au retour de l'équipe (ou à sa disparition), la Guilde CLASSE le donjon.
///   3. Ensuite, chaque expédition est du rang du donjon ; les récits des survivants et les morts
///      font évoluer ce classement, ainsi que la CONVOITISE (trésors, butin) et l'HOSTILITÉ (morts, blessés)
///      qui décident du nombre d'aventuriers et de leur acharnement.
/// </summary>
public class GuildSystem : MonoBehaviour
{
    public static GuildSystem Instance { get; private set; }

    public static readonly string[] RankNames = { "F", "E", "D", "C", "B", "A", "S", "SS", "SSS" };
    public static readonly string[] RankTitles = { "novices", "apprentis", "confirmés", "vétérans", "experts", "élite", "maîtres", "légendes", "héros" };
    public static readonly Color[] RankColors =
    {
        new Color(0.62f, 0.6f, 0.58f), new Color(0.55f, 0.8f, 0.5f), new Color(0.45f, 0.8f, 0.85f), new Color(0.5f, 0.62f, 1f),
        new Color(0.72f, 0.5f, 1f), new Color(1f, 0.55f, 0.35f), new Color(1f, 0.35f, 0.35f), new Color(1f, 0.8f, 0.3f), new Color(1f, 0.95f, 0.6f),
    };
    private static readonly float[] RankHp = { 0.5f, 0.65f, 0.8f, 1f, 1.3f, 1.75f, 2.4f, 3.4f, 5f };
    private static readonly float[] RankDmg = { 0.55f, 0.7f, 0.85f, 1f, 1.25f, 1.6f, 2.1f, 2.8f, 3.6f };
    /// <summary>Score de danger minimal pour chaque rang de donjon.</summary>
    private static readonly float[] ScoreThresholds = { 0f, 4f, 8f, 13f, 20f, 30f, 45f, 65f, 95f };

    public const int RankC = 3;
    public static int MaxRank => RankNames.Length - 1;
    public static string RankName(int r) => r < 0 ? "?" : RankNames[Mathf.Clamp(r, 0, MaxRank)];
    public static Color RankColor(int r) => RankColors[Mathf.Clamp(r, 0, MaxRank)];
    public static string RankTag(int r) => $"<color=#{ColorUtility.ToHtmlStringRGB(RankColor(r))}><b>{RankName(r)}</b></color>";
    public static float HpMult(int r) => RankHp[Mathf.Clamp(r, 0, MaxRank)];
    public static float DmgMult(int r) => RankDmg[Mathf.Clamp(r, 0, MaxRank)];
    /// <summary>Apparence et comportement : aventuriers (F-B), chevaliers d'élite (A-S), héros (SS-SSS).</summary>
    public static int TierForRank(int r) => r <= 4 ? 2 : r <= 6 ? 3 : 4;
    public static string WarriorTitle(int r) => r <= 4 ? "Aventurier" : r <= 6 ? "Chevalier" : r == 7 ? "Champion" : "Héros";

    private static readonly string[] DungeonDescs =
    {
        "inoffensif, un terrain d'entraînement pour novices",
        "peu dangereux, réservé aux apprentis",
        "dangereux pour les imprudents",
        "dangereux : aventuriers confirmés uniquement",
        "très dangereux : groupes expérimentés exigés",
        "mortel : réservé à l'élite",
        "une calamité : les maîtres de la Guilde s'en chargent",
        "une catastrophe : les nations s'en inquiètent",
        "l'apocalypse : seuls les héros peuvent l'affronter",
    };
    public static string DungeonRankDesc(int r) => DungeonDescs[Mathf.Clamp(r, 0, MaxRank)];

    public enum Stage { Unknown, Reported, Assessing, Assessed }

    [Header("Calendrier")]
    [Tooltip("Jours entre le signalement et l'arrivée de l'équipe d'évaluation (le temps de rentrer au continent)")]
    public int daysToAssessment = 3;

    [Header("État (lecture)")]
    public Stage stage = Stage.Unknown;
    public int officialRank = -1;
    public int reportedRank = RankC;
    public int assessDay = -1;
    public int assessedDay = -1;
    [Range(0f, 1f)] public float greed;
    [Range(0f, 1f)] public float hostility;
    public int guildDeaths;
    public string reportSummary = "";
    public bool reportedByWitness = true;

    public bool Assessed => stage == Stage.Assessed;
    public event Action OnChanged;

    // Rapport de l'expédition en cours (ce que les survivants raconteront)
    private int repMonsters, repMaxStage, repHurt, repLooted, repDead, repFled;
    private readonly List<AdventurerAI> assessors = new List<AdventurerAI>();
    private float assessStart = -1f;

    private int Today => GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (ReputationSystem.Instance != null) ReputationSystem.Instance.OnDiscovered += OnDiscovered;
        if (ExpeditionManager.Instance != null) ExpeditionManager.Instance.OnExpeditionEnded += OnExpeditionEnded;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (ReputationSystem.Instance != null) ReputationSystem.Instance.OnDiscovered -= OnDiscovered;
        if (ExpeditionManager.Instance != null) ExpeditionManager.Instance.OnExpeditionEnded -= OnExpeditionEnded;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    // ─────────────────────────────────────────────
    // DANGER RÉEL DU DONJON
    // ─────────────────────────────────────────────

    /// <summary>Puissance du donjon : monstres (selon leur stade), pièges, niveau, étages, réputation meurtrière.</summary>
    public float DangerScore()
    {
        float s = 0f;
        foreach (var m in MonsterUnit.All)
            if (m != null && !m.summoned) s += m.stage == 1 ? 1f : m.stage == 2 ? 2.4f : 4.5f;
        s += PlacedObject.Count(ItemKind.Trap) * 0.8f;
        if (PlayerLevel.Instance != null) s += (PlayerLevel.Instance.currentLevel - 1) * 0.6f;
        if (DungeonLayers.Instance != null)
        {
            int open = 0;
            foreach (var l in DungeonLayers.Instance.layers) if (l.unlocked) open++;
            s += Mathf.Max(0, open - 1) * 1.5f;
        }
        s += Mathf.Min(20f, guildDeaths * 0.6f);
        return s;
    }

    public static int RankFromScore(float score)
    {
        int r = 0;
        for (int i = 0; i < ScoreThresholds.Length; i++) if (score >= ScoreThresholds[i]) r = i;
        return r;
    }

    /// <summary>Ce que la Guilde mesurerait aujourd'hui (difficulté comprise).</summary>
    public int MeasuredRank => Mathf.Clamp(RankFromScore(DangerScore()) + GameDifficulty.RankBias, 0, MaxRank);

    // ─────────────────────────────────────────────
    // RAPPORTS DES INTRUS
    // ─────────────────────────────────────────────

    /// <summary>Nouvelle expédition : on repart d'un rapport vierge.</summary>
    public void BeginExpedition(bool assessment, List<AdventurerAI> members)
    {
        repMonsters = repMaxStage = repHurt = repLooted = repDead = repFled = 0;
        assessors.Clear();
        if (!assessment) return;
        assessors.AddRange(members);
        stage = Stage.Assessing;
        assessStart = Time.time;
        OnChanged?.Invoke();
    }

    /// <summary>Un survivant rentre : il racontera ce qu'il a vu.</summary>
    public void NoteSurvivor(AdventurerAI a)
    {
        if (a == null) return;
        repFled++;
        repMonsters = Mathf.Max(repMonsters, a.MonstersSeen);
        repMaxStage = Mathf.Max(repMaxStage, a.MaxMonsterStageSeen);
        if (a.DamageTakenFraction >= 0.35f) repHurt++;
        if (a.ChestsLooted > 0) { repLooted++; greed = Mathf.Clamp01(greed + 0.08f * a.ChestsLooted); }
        // les blessures graves nourrissent la rancune
        if (a.Rank >= 0 && a.DamageTakenFraction >= 0.6f) hostility = Mathf.Clamp01(hostility + 0.02f);
    }

    /// <summary>Un intrus meurt dans le donjon.</summary>
    public void NoteDeath(AdventurerAI a)
    {
        repDead++;
        if (a == null || a.Rank < 0) return;
        guildDeaths++;
        hostility = Mathf.Clamp01(hostility + (a.Rank >= 5 ? 0.16f : 0.09f));
    }

    // ─────────────────────────────────────────────
    // SIGNALEMENT → ÉVALUATION → CLASSEMENT
    // ─────────────────────────────────────────────

    /// <summary>Le monde apprend l'existence du donjon (survivant ou disparitions).</summary>
    private void OnDiscovered()
    {
        if (stage != Stage.Unknown) return;
        var parts = new List<string>();
        int r = RankC;
        bool fromWitness = repFled > 0;
        if (fromWitness)
        {
            // Les récits font monter l'estimation (C par défaut, B si les témoins sont terrifiés)
            int fear = 0;
            if (repMaxStage >= 2 || repMonsters >= 4) { fear++; parts.Add("des créatures terrifiantes"); }
            else if (repMonsters > 0) parts.Add(repMonsters == 1 ? "une créature tapie dans le noir" : "plusieurs créatures");
            if (repMaxStage >= 3 || repMonsters >= 8) fear++;
            if (repHurt > 0) { fear++; parts.Add("des pièges et des blessures"); }
            if (repDead > 0) { fear += 2; parts.Add(repDead == 1 ? "un compagnon qui n'est jamais ressorti" : "des compagnons qui ne sont jamais ressortis"); }
            if (fear >= 2) r++;
            if (repMonsters == 0 && repHurt == 0 && repDead == 0) { r--; parts.Add("une grotte étrange et une lueur au fond de la roche"); }
        }
        else
        {
            r++;
            parts.Add("des pêcheurs et des villageois disparus sans laisser de trace");
        }
        reportedRank = Mathf.Clamp(r + GameDifficulty.RankBias, 1, 5);
        reportSummary = string.Join(", ", parts);
        reportedByWitness = fromWitness;
        stage = Stage.Reported;
        // l'équipe d'évaluation part vite : le lendemain ou le surlendemain du rapport
        assessDay = Today + (UnityEngine.Random.value < 0.5f ? 1 : 2);
        ExpeditionManager.Instance?.ScheduleAt(assessDay);
        OnChanged?.Invoke();
        StoryManager.Instance?.OnGuildReported(fromWitness, reportSummary, reportedRank, assessDay);
    }

    /// <summary>L'équipe d'évaluation doit partir aujourd'hui.</summary>
    public bool AssessmentDue => stage == Stage.Reported && Today >= assessDay;

    private void Update()
    {
        // L'équipe d'évaluation ne vient pas pour conquérir : une fois le donjon « mesuré », elle rentre
        if (stage != Stage.Assessing || assessStart < 0f || GameManager.Instance == null) return;
        assessors.RemoveAll(a => a == null);
        bool inside = false;
        foreach (var a in assessors) if (a.IsInsideDungeon) inside = true;
        float limit = GameManager.Instance.dayLengthSeconds * 0.45f;
        if (assessors.Count > 0 && Time.time - assessStart > limit && !inside)
        {
            assessStart = -1f;
            foreach (var a in assessors) a.ForceLeave();
            GameHUD.Toast("L'équipe de la Guilde a vu ce qu'elle voulait voir : elle rentre faire son rapport", GameHUD.InfoColor);
        }
        else if (assessors.Count > 0 && Time.time - assessStart > limit * 1.6f)
        {
            assessStart = -1f;
            foreach (var a in assessors) a.ForceLeave();
        }
    }

    private void OnExpeditionEnded(int killed, int fled)
    {
        var exp = ExpeditionManager.Instance;
        if (exp != null && exp.LastWasVisit) return;
        if (stage == Stage.Assessing) { Verdict(killed, fled); return; }
        if (stage != Stage.Assessed) return;

        // Réévaluation : les survivants racontent, les disparitions inquiètent
        int old = officialRank;
        int measured = MeasuredRank;
        int target = fled == 0 && killed > 0 ? Mathf.Max(officialRank + 1, measured) : measured;
        if (target > officialRank) officialRank = Mathf.Min(target, officialRank + 1);
        else if (target < officialRank - 1 && killed == 0) officialRank--;
        officialRank = Mathf.Clamp(officialRank, 0, MaxRank);
        if (officialRank != old)
        {
            bool up = officialRank > old;
            GameHUD.Toast(up ? $"La Guilde reclasse votre donjon : rang {RankName(officialRank)}"
                             : $"La Guilde rétrograde votre donjon : rang {RankName(officialRank)}",
                up ? GameHUD.DangerColor : GameHUD.InfoColor);
            StoryManager.Instance?.OnGuildRankChanged(old, officialRank);
        }
        OnChanged?.Invoke();
    }

    private void Verdict(int killed, int fled)
    {
        int measured = MeasuredRank;
        int rank;
        string outcome;
        if (fled == 0 && killed > 0)
        {
            rank = Mathf.Max(reportedRank + 1, measured + 1);
            outcome = $"L'équipe de rang {RankName(reportedRank)} n'est jamais revenue.";
        }
        else if (killed > 0)
        {
            rank = Mathf.Max(measured + 1, reportedRank);
            outcome = killed == 1 ? "L'équipe est revenue... amputée d'un des siens." : $"L'équipe est revenue... sans {killed} des siens.";
        }
        else
        {
            rank = measured;
            outcome = "L'équipe est revenue au complet, carnets remplis.";
        }
        officialRank = Mathf.Clamp(rank, 0, MaxRank);
        stage = Stage.Assessed;
        assessedDay = Today;
        assessStart = -1f;
        assessors.Clear();
        ExpeditionManager.Instance?.ScheduleAt(Today + 2);
        OnChanged?.Invoke();
        StoryManager.Instance?.OnGuildVerdict(officialRank, outcome, killed, fled);
    }

    // ─────────────────────────────────────────────
    // EXPÉDITIONS CLASSÉES
    // ─────────────────────────────────────────────

    /// <summary>Rang du meneur de la prochaine expédition (hostilité et vengeance comprises).</summary>
    public int LeaderRank(int bonus)
    {
        int r = officialRank + (hostility >= 0.6f ? 1 : 0) + bonus;
        return Mathf.Clamp(r, 0, MaxRank);
    }

    /// <summary>Rang d'un équipier : celui du meneur, parfois un cran en dessous.</summary>
    public static int MemberRank(int leader, int i) => i == 0 ? leader : Mathf.Max(0, leader - (UnityEngine.Random.value < 0.5f ? 1 : 0));

    /// <summary>Membres supplémentaires : les trésors attirent, la rancune rassemble.</summary>
    public int ExtraMembers => (greed >= 0.5f ? 1 : 0) + (hostility >= 0.75f ? 1 : 0);

    /// <summary>Raccourcit l'attente entre expéditions (la convoitise fait se presser les chasseurs de trésors).</summary>
    public float WaitMult => 1f - 0.3f * greed;

    public static string Gauge(float v)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(v * 3f), 0, 3);
        return new string('●', n) + new string('○', 3 - n);
    }

    private void OnNewDay(int day)
    {
        hostility = Mathf.Max(0f, hostility * 0.96f - 0.005f);
        // Le prestige du donjon (trésors exposés, décor) nourrit les rumeurs de richesses
        float target = Mathf.Clamp01(PlacedObject.TotalPrestige() / 80f + TreasureChest.All.Count * 0.06f);
        greed = Mathf.Clamp01(Mathf.MoveTowards(greed, target, 0.05f));
        // Chargement d'une partie sauvegardée pendant l'évaluation : l'équipe revient
        if (stage == Stage.Assessing && assessors.Count == 0 && assessStart < 0f)
        {
            stage = Stage.Reported;
            assessDay = day;
            ExpeditionManager.Instance?.ScheduleAt(day);
        }
        OnChanged?.Invoke();
    }

    public string StatusLine()
    {
        switch (stage)
        {
            case Stage.Unknown: return "Donjon inconnu du monde";
            case Stage.Reported: return "Signalé à la Guilde : une évaluation se prépare";
            case Stage.Assessing: return $"Évaluation de la Guilde en cours (équipe de rang {RankName(reportedRank)})";
            default: return $"Donjon de rang {RankTag(officialRank)}  <size=85%>convoitise {Gauge(greed)} · hostilité {Gauge(hostility)}</size>";
        }
    }

    // ─────────────────────────────────────────────
    // PLAN DU DONJON (rapporté par les survivants)
    // ─────────────────────────────────────────────

    private readonly HashSet<Vector3Int> mapCells = new HashSet<Vector3Int>();
    public IEnumerable<Vector3Int> MapCells => mapCells;
    public int MapSize => mapCells.Count;
    public Vector3Int? MapCore { get; private set; }

    /// <summary>Un survivant raconte ce qu'il a vu : la Guilde complète son plan (et oublie les galeries murées).</summary>
    public void LearnMap(IEnumerable<Vector3Int> floor, IEnumerable<Vector3Int> walls, Vector3Int? core)
    {
        foreach (var c in floor) mapCells.Add(c);
        foreach (var c in walls) mapCells.Remove(c);
        MapCore = core;
        OnChanged?.Invoke();
    }

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    public void Capture(SaveData d)
    {
        d.guildStage = (int)(stage == Stage.Assessing ? Stage.Reported : stage);
        d.guildRank = officialRank;
        d.guildReportedRank = reportedRank;
        d.guildAssessDay = stage == Stage.Assessing ? Today + 1 : assessDay;
        d.guildAssessedDay = assessedDay;
        d.guildGreed = greed;
        d.guildHostility = hostility;
        d.guildDeaths = guildDeaths;
        d.guildReport = reportSummary;
        d.guildMap = new List<int>();
        foreach (var c in mapCells) { d.guildMap.Add(c.x); d.guildMap.Add(c.y); }
        d.guildCoreKnown = MapCore.HasValue;
        if (MapCore.HasValue) { d.guildCoreX = MapCore.Value.x; d.guildCoreY = MapCore.Value.y; }
    }

    public void Restore(SaveData d)
    {
        stage = (Stage)Mathf.Clamp(d.guildStage, 0, 3);
        officialRank = d.guildRank;
        reportedRank = d.guildReportedRank;
        assessDay = d.guildAssessDay;
        assessedDay = d.guildAssessedDay;
        greed = d.guildGreed;
        hostility = d.guildHostility;
        guildDeaths = d.guildDeaths;
        reportSummary = d.guildReport ?? "";
        mapCells.Clear();
        if (d.guildMap != null) for (int i = 0; i + 1 < d.guildMap.Count; i += 2) mapCells.Add(new Vector3Int(d.guildMap[i], d.guildMap[i + 1], 0));
        MapCore = d.guildCoreKnown ? new Vector3Int(d.guildCoreX, d.guildCoreY, 0) : (Vector3Int?)null;
        // anciennes sauvegardes : donjon déjà connu mais jamais évalué → évaluation prochaine
        if (stage == Stage.Unknown && ReputationSystem.Instance != null && ReputationSystem.Instance.dungeonDiscovered)
        {
            stage = Stage.Reported;
            assessDay = Today + 1;
        }
        if (stage == Stage.Reported) ExpeditionManager.Instance?.ScheduleAt(assessDay);
        OnChanged?.Invoke();
    }
}
