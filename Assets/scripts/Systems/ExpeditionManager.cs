using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Définition d'un type d'intrus. Les stats sont des multiplicateurs appliqués au prefab aventurier.
/// Assigne un prefabOverride quand tu auras des sprites dédiés (pêcheur, chevalier, héros...).
/// </summary>
[Serializable]
public class AdventurerTier
{
    public string displayName = "Aventurier";
    [Tooltip("Prefab spécifique (sinon le prefab de l'AdventurerSpawner, teinté)")]
    public GameObject prefabOverride;
    [Tooltip("Variantes d'apparence tirées au hasard (remplacent prefabOverride)")]
    public List<GameObject> prefabVariants = new List<GameObject>();
    public Color tint = Color.white;
    public float scale = 1f;

    [Header("Stats (multiplicateurs)")]
    public float healthMult = 1f;
    public float damageMult = 1f;
    public float speedMult = 1f;

    [Header("Groupe")]
    public Vector2Int partySize = new Vector2Int(1, 3);

    [Header("Mentalité")]
    [Tooltip("Ne sait pas qu'un donjon existe : flâne sur l'île puis explore par curiosité")]
    public bool unaware;
    public Vector2 arroganceRange = new Vector2(0.2f, 0.8f);
    [Tooltip("Nombre de replis avant d'abandonner et de quitter l'île")]
    public int maxRetreats = 1;
    [Tooltip("Ennemis visibles tolérés avant de fuir (base)")]
    public int courage = 3;
    [Tooltip("Chance de fuir quand le danger est là (sinon il tient bon, par bravoure ou malchance)")]
    [Range(0f, 1f)] public float fleeChance = 0.6f;
    [Tooltip("Attirance pour les trésors (0-1)")]
    [Range(0f, 1f)] public float greed = 0.5f;

    [Header("Récompenses")]
    public int manaReward = 50;
    public int xpReward = 25;

    public AdventurerTier Clone() => (AdventurerTier)MemberwiseClone();
}

/// <summary>
/// Organise l'arrivée des expéditions sur l'île selon le jour, la menace et la renommée.
/// </summary>
public class ExpeditionManager : MonoBehaviour
{
    public static ExpeditionManager Instance { get; private set; }

    [Header("Calendrier")]
    [Tooltip("Arrivée des premiers pêcheurs (le Noyau a le temps de s'installer)")]
    public int firstExpeditionDay = 3;
    [Tooltip("Moment de la journée où le bateau accoste (0-1) : tiré au hasard chaque jour, de jour comme de nuit")]
    [Range(0f, 1f)] public float arrivalTimeOfDay = 0.25f;
    private int rolledDay = -1;
    private float visitTimeOfDay = 0.55f;
    public float baseDaysBetweenExpeditions = 1.5f;
    [Tooltip("Une expédition qui dure plus longtemps (en jours) abandonne et quitte l'île")]
    public float maxExpeditionDays = 1.2f;
    [Tooltip("Chance qu'un visiteur isolé passe les jours sans expédition (une fois le donjon connu)")]
    [Range(0f, 1f)] public float visitorChance = 0.6f;
    [Tooltip("La renommée raccourcit l'attente entre expéditions")]
    public float renownPerDayReduction = 20f;

    [Header("Types d'intrus (index = palier de menace)")]
    public AdventurerTier[] tiers = DefaultTiers();

    [Header("Debug")]
    [Tooltip("Touche T : forcer une expédition")]
    public bool enableDebugKey = true;

    public bool ExpeditionActive => members.Count > 0;
    public int ActiveCount => members.Count;
    public int NextExpeditionDay { get; private set; }
    public AdventurerTier CurrentExpeditionTier { get; private set; }
    /// <summary>L'expédition en cours est l'équipe d'évaluation de la Guilde.</summary>
    public bool IsAssessment { get; private set; }
    /// <summary>La dernière expédition terminée n'était qu'une visite isolée.</summary>
    public bool LastWasVisit { get; private set; }
    /// <summary>Rang (Guilde) du meneur de l'expédition en cours (-1 : civils).</summary>
    public int CurrentRank { get; private set; } = -1;

    public event Action<AdventurerTier, int> OnExpeditionArrived;

    /// <summary>Une fois débarqués (et leur carte d'expédition partagée), les intrus installent leur camp.</summary>
    private System.Collections.IEnumerator PitchCampLater(List<AdventurerAI> group)
    {
        yield return new WaitForSeconds(2.5f);
        group.RemoveAll(a => a == null || a.IsDead);
        IntruderCamp.TryPitch(group);
    }
    public event Action<int, int> OnExpeditionEnded;   // (tués, enfuis)

    private readonly List<AdventurerAI> members = new List<AdventurerAI>();
    private int killedThisExpedition, fledThisExpedition;
    private AdventurerSpawner spawner;
    private int launchedDay = -1;
    private int visitCheckedDay = -1;
    private float expeditionStart;

    public static AdventurerTier[] DefaultTiers() => new[]
    {
        new AdventurerTier { displayName = "Pêcheur", tint = new Color(0.85f, 0.75f, 0.6f), scale = 0.95f,
            healthMult = 0.3f, damageMult = 0.3f, speedMult = 0.9f, partySize = new Vector2Int(2, 3),
            unaware = true, arroganceRange = new Vector2(0f, 0.3f), maxRetreats = 0, courage = 1, fleeChance = 0.85f, greed = 0.6f,
            manaReward = 40, xpReward = 20 },
        new AdventurerTier { displayName = "Villageois", tint = new Color(0.7f, 0.9f, 0.65f), scale = 1f,
            healthMult = 0.5f, damageMult = 0.5f, speedMult = 1f, partySize = new Vector2Int(2, 4),
            unaware = false, arroganceRange = new Vector2(0.1f, 0.5f), maxRetreats = 1, courage = 2, fleeChance = 0.7f, greed = 0.5f,
            manaReward = 60, xpReward = 30 },
        new AdventurerTier { displayName = "Aventurier", tint = Color.white, scale = 1f,
            healthMult = 1f, damageMult = 1f, speedMult = 1f, partySize = new Vector2Int(2, 4),
            unaware = false, arroganceRange = new Vector2(0.3f, 0.8f), maxRetreats = 2, courage = 3, fleeChance = 0.55f, greed = 0.8f,
            manaReward = 120, xpReward = 50 },
        new AdventurerTier { displayName = "Chevalier", tint = new Color(0.7f, 0.8f, 1f), scale = 1.1f,
            healthMult = 2.2f, damageMult = 1.8f, speedMult = 0.85f, partySize = new Vector2Int(3, 5),
            unaware = false, arroganceRange = new Vector2(0.6f, 1f), maxRetreats = 2, courage = 5, fleeChance = 0.35f, greed = 0.2f,
            manaReward = 300, xpReward = 120 },
        new AdventurerTier { displayName = "Héros", tint = new Color(1f, 0.85f, 0.35f), scale = 1.2f,
            healthMult = 5f, damageMult = 3.5f, speedMult = 1.15f, partySize = new Vector2Int(1, 2),
            unaware = false, arroganceRange = new Vector2(0.9f, 1f), maxRetreats = 3, courage = 8, fleeChance = 0.15f, greed = 0.3f,
            manaReward = 900, xpReward = 400 },
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        NextExpeditionDay = firstExpeditionDay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private void Start()
    {
        // Sprites dédiés par palier (générés par DungeonCore/4. Importer les personnages)
        var cat = DungeonCatalog.Get();
        if (cat != null)
            for (int i = 0; i < tiers.Length && i < cat.adventurerPrefabs.Count; i++)
                if (cat.adventurerPrefabs[i] != null && tiers[i].prefabOverride == null)
                {
                    if (i < cat.tierVariants.Count && cat.tierVariants[i] != null) tiers[i].prefabVariants = cat.tierVariants[i].prefabs;
                    tiers[i].prefabOverride = cat.adventurerPrefabs[i];
                    tiers[i].tint = Color.white;
                    tiers[i].scale = 1f;
                }

        spawner = FindFirstObjectByType<AdventurerSpawner>();
        if (spawner != null) spawner.autoSpawn = false;   // les arrivées sont gérées ici
        else Debug.LogWarning("[ExpeditionManager] Aucun AdventurerSpawner dans la scène");

        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    private void Update()
    {
        if (enableDebugKey && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            LaunchExpedition();

        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameManager.GameState.Playing) return;
        int today = gm.CurrentDay;
        // Heure d'arrivée imprévisible : le joueur ne sait jamais quand la prochaine voile surgira
        if (rolledDay != today)
        {
            rolledDay = today;
            arrivalTimeOfDay = UnityEngine.Random.Range(0.08f, 0.93f);
            visitTimeOfDay = UnityEngine.Random.Range(0.1f, 0.95f);
        }

        if (ExpeditionActive)
        {
            // Une expédition qui s'éternise finit par abandonner (évite tout blocage)
            if (Time.time - expeditionStart > maxExpeditionDays * gm.dayLengthSeconds)
            {
                expeditionStart = Time.time;
                foreach (var m in members.ToArray()) if (m != null) m.ForceLeave();
                GameHUD.Toast("L'expédition, épuisée, rebrousse chemin vers son bateau", GameHUD.InfoColor);
            }
            // Membres détruits sans notification
            members.RemoveAll(m => m == null);
            if (members.Count == 0) EndExpedition();
            return;
        }

        // Tempête : aucun bateau ne prend la mer
        if (IslandEvents.Instance != null && IslandEvents.Instance.BlocksBoats) return;

        // Expédition principale au bon moment de la journée
        if (today >= NextExpeditionDay && launchedDay != today && gm.DayProgress >= arrivalTimeOfDay)
        {
            launchedDay = today;
            LaunchExpedition();
            return;
        }

        // Jours calmes : un visiteur isolé (chasseur égaré, aventurier en quête de gloire...)
        var rep = ReputationSystem.Instance;
        if (visitCheckedDay != today && gm.DayProgress >= visitTimeOfDay && today >= firstExpeditionDay)
        {
            visitCheckedDay = today;
            var guild = GuildSystem.Instance;
            if (rep != null && rep.dungeonDiscovered && (guild == null || guild.Assessed) && launchedDay != today && UnityEngine.Random.value < visitorChance)
                LaunchVisitor();
        }
    }

    private void OnNewDay(int day) { }

    /// <summary>Chargement : reprend le calendrier (l'expédition en cours au moment de la sauvegarde est perdue).</summary>
    public void RestoreSchedule(int nextDay)
    {
        NextExpeditionDay = nextDay;
        launchedDay = -1;
    }

    /// <summary>Fixe la prochaine expédition (la Guilde décide du calendrier).</summary>
    public void ScheduleAt(int day)
    {
        NextExpeditionDay = day;
        launchedDay = -1;
    }

    /// <summary>Avance la prochaine expédition (expédition punitive).</summary>
    public void BringForward(int day)
    {
        if (day < NextExpeditionDay) NextExpeditionDay = day;
    }

    /// <summary>Le joueur appelle le prochain bateau sans attendre.</summary>
    public void CallNextExpedition()
    {
        if (ExpeditionActive) return;
        int today = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        NextExpeditionDay = today;
        launchedDay = today;
        LaunchExpedition();
    }

    /// <summary>Petite visite : un seul intrus du palier actuel.</summary>
    private ExpeditionBoat boat;
    public ExpeditionBoat CurrentBoat => boat;

    /// <summary>Le bateau accoste au ponton : petit (pêcheurs), moyen (aventuriers), galion (héros).</summary>
    private void SpawnBoat(int tierIndex, bool visitor)
    {
        if (spawner == null) return;
        // Taille du navire : palier des intrus et développement du port
        int colony = SettlementManager.Instance != null ? SettlementManager.Instance.stage : 0;
        int size = visitor ? 0 : tierIndex >= 4 || colony >= 4 ? 2 : tierIndex >= 2 || colony >= 2 ? 1 : 0;
        var art = SettlementArt.Get();
        Sprite[] frames = null;
        if (art != null) frames = art.Frames(size == 0 ? "boat_row" : size == 1 ? "boat_sail" : "boat_ship");
        if (frames == null || frames.Length == 0)
        {
            var cat = DungeonCatalog.Get();
            if (cat == null || cat.boatSprites == null || cat.boatSprites.Count == 0) return;
            frames = new[] { cat.boatSprites[Mathf.Clamp(size, 0, cat.boatSprites.Count - 1)] };
        }
        if (boat != null) boat.Leave();
        var site = SettlementManager.Instance != null ? SettlementManager.Instance.Site : null;
        Vector3 dock = spawner.spawnPoint != null ? spawner.spawnPoint.position : spawner.transform.position;
        boat = ExpeditionBoat.Create(frames, dock, site != null && site.shipMooring != Vector3.zero ? site.shipMooring : (Vector3?)null);
    }

    /// <summary>Un habitant de la colonie part explorer le donjon (à pied).</summary>
    public void LaunchLocalVisitor(Vector3 from)
    {
        if (spawner == null || tiers.Length < 2) return;
        var ai = spawner.SpawnForExpedition(tiers[1], 1, 0, 1, from);
        if (ai == null) return;
        ai.isLocal = true;
        members.Add(ai);
        if (!ExpeditionActiveBefore) { killedThisExpedition = fledThisExpedition = 0; expeditionStart = Time.time; isVisit = true; }
        GameHUD.Toast("Un habitant de la colonie s'aventure vers votre montagne", GameHUD.WarningColor);
    }

    private bool ExpeditionActiveBefore => members.Count > 1;

    /// <summary>
    /// Le CHAMPION DE LA GUILDE : un héros unique de rang SSS, envoyé quand votre donjon atteint le rang S.
    /// Le vaincre est l'un des trois piliers de la domination de l'île.
    /// </summary>
    public bool LaunchChampion()
    {
        if (spawner == null || ExpeditionActive) return false;
        int rank = GuildSystem.MaxRank;
        var tier = RankedTier(rank, false);
        tier.displayName = "Champion de la Guilde";
        tier.healthMult *= 2.2f;
        tier.damageMult *= 1.3f;
        tier.manaReward *= 3;
        tier.xpReward *= 3;
        int ti = Mathf.Clamp(GuildSystem.TierForRank(rank), 0, tiers.Length - 1);
        var ai = spawner.SpawnForExpedition(tier, ti, 0, 1, null, AdventurerAI.Role.Warrior);
        if (ai == null) return false;
        ai.ApplyRank(rank);
        ai.IsChampion = true;
        if (ai.combat != null) ai.combat.adventurerName = "Aldéric l'Invaincu, Champion de la Guilde";
        ai.arrogance = 1f;
        members.Add(ai);
        SpawnBoat(ti, true);
        killedThisExpedition = fledThisExpedition = 0;
        expeditionStart = Time.time;
        isVisit = true;
        GameHUD.Toast("La Guilde envoie son Champion : Aldéric l'Invaincu accoste", GameHUD.DangerColor);
        Chronicle.Add("Aldéric l'Invaincu, Champion de la Guilde, débarque sur l'île.");
        StoryManager.Instance?.Play(new StoryManager.Line("Aldéric l'Invaincu", "On dit qu'un cristal pense, sous cette montagne. Je suis venu le faire taire."));
        return true;
    }

    private void LaunchVisitor()
    {
        if (spawner == null) return;
        int tierIndex = Mathf.Clamp(ReputationSystem.Instance != null ? ReputationSystem.Instance.CurrentTier : 0, 0, tiers.Length - 1);
        var soloRole = tierIndex >= 2 && UnityEngine.Random.value < 0.4f
            ? (UnityEngine.Random.value < 0.5f ? AdventurerAI.Role.Archer : AdventurerAI.Role.Mage) : AdventurerAI.Role.Warrior;
        // Aventurier solitaire : souvent un cran sous le rang du donjon (il tente sa chance)
        var guild = GuildSystem.Instance;
        int rank = guild != null && guild.Assessed ? Mathf.Max(0, guild.officialRank - (UnityEngine.Random.value < 0.6f ? 1 : 0)) : -1;
        AdventurerTier tier = tiers[tierIndex];
        if (rank >= 0) { tierIndex = Mathf.Clamp(GuildSystem.TierForRank(rank), 0, tiers.Length - 1); tier = RankedTier(rank, false); }
        var ai = spawner.SpawnForExpedition(tier, tierIndex, 0, 1, null, soloRole);
        if (ai == null) return;
        ai.ApplyRank(rank);
        members.Add(ai);
        SpawnBoat(tierIndex, true);
        killedThisExpedition = fledThisExpedition = 0;
        expeditionStart = Time.time;
        isVisit = true;
        GameHUD.Toast(rank >= 0 ? $"Un aventurier solitaire de rang {GuildSystem.RankName(rank)} accoste" : $"Un visiteur solitaire accoste : {tier.displayName}", GameHUD.WarningColor);
    }

    /// <summary>
    /// Profil d'un aventurier classé : puissance selon le rang, acharnement selon l'hostilité de la Guilde,
    /// appât du gain selon la convoitise. L'équipe d'évaluation, elle, observe plus qu'elle ne pille.
    /// </summary>
    private AdventurerTier RankedTier(int rank, bool assessor)
    {
        int ti = Mathf.Clamp(GuildSystem.TierForRank(rank), 0, tiers.Length - 1);
        var t = tiers[ti].Clone();
        t.displayName = GuildSystem.WarriorTitle(rank);
        t.healthMult = GuildSystem.HpMult(rank);
        t.damageMult = GuildSystem.DmgMult(rank);
        t.manaReward = Mathf.RoundToInt(60 + 45 * rank + 8 * rank * rank);
        t.xpReward = Mathf.RoundToInt(25 + 18 * rank + 4 * rank * rank);
        var g = GuildSystem.Instance;
        float hostility = g != null ? g.hostility : 0f, greed = g != null ? g.greed : 0f;
        if (assessor)
        {
            t.greed = 0.15f;
            t.fleeChance = 0.75f;
            t.maxRetreats = 0;
            t.arroganceRange = new Vector2(0.2f, 0.5f);
        }
        else
        {
            t.greed = Mathf.Max(t.greed, 0.3f + greed * 0.7f);
            t.arroganceRange = new Vector2(Mathf.Min(1f, t.arroganceRange.x + hostility * 0.3f), Mathf.Min(1f, t.arroganceRange.y + hostility * 0.2f));
            t.fleeChance = Mathf.Clamp01(t.fleeChance * (1f - hostility * 0.4f));
            t.courage += Mathf.RoundToInt(hostility * 2f);
        }
        return t;
    }

    private bool isVisit;

    /// <summary>Fin de tempête : des naufragés (affaiblis, ignorant tout du donjon) s'échouent sur la plage.</summary>
    public void LaunchShipwreck()
    {
        if (spawner == null || ExpeditionActive || tiers.Length == 0 || CrawlerMode.Active) return;
        int n = UnityEngine.Random.Range(1, 4);
        var spawned = new List<AdventurerAI>();
        for (int i = 0; i < n; i++)
        {
            var ai = spawner.SpawnForExpedition(tiers[0], 0, i, n);
            if (ai == null) continue;
            ai.combat.currentHealth = ai.combat.maxHealth * 0.5f;
            members.Add(ai);
            spawned.Add(ai);
        }
        if (spawned.Count == 0) return;
        killedThisExpedition = fledThisExpedition = 0;
        expeditionStart = Time.time;
        isVisit = true;
        GameHUD.Toast(spawned.Count == 1 ? "Un naufragé s'échoue sur la plage..." : $"{spawned.Count} naufragés s'échouent sur la plage...", GameHUD.WarningColor);
        StoryManager.Instance?.PlayOnce("shipwreck", new StoryManager.Line(StoryManager.SelfName,
            "La tempête m'a apporté des survivants, trempés, épuisés... et perdus. Personne ne sait qu'ils sont ici."));
    }

    /// <summary>Lance une expédition adaptée à la menace actuelle.</summary>
    public void LaunchExpedition()
    {
        if (spawner == null || spawner.adventurerPrefab == null)
        {
            Debug.LogWarning("[ExpeditionManager] Pas de prefab aventurier");
            return;
        }

        var guild = GuildSystem.Instance;
        if (guild != null && guild.AssessmentDue) { LaunchAssessment(); return; }

        int tierIndex = ReputationSystem.Instance != null ? ReputationSystem.Instance.CurrentTier : 0;
        // Expédition punitive (colons massacrés, trêve trahie) : bien plus forte
        var rel = RelationSystem.Instance;
        int punitiveExtra = 0, bonusTier = 0;
        bool punitive = rel != null && rel.TakePunitive(out bonusTier, out punitiveExtra);
        bool ranked = guild != null && guild.Assessed;
        int leaderRank = -1;
        if (ranked)
        {
            // La Guilde envoie des aventuriers du rang du donjon (la vengeance en ajoute)
            leaderRank = guild.LeaderRank((punitive ? Mathf.Max(1, bonusTier * 2) : 0) + (rel != null ? rel.TierModifier : 0));
            tierIndex = GuildSystem.TierForRank(leaderRank);
        }
        else if (punitive) tierIndex = Mathf.Max(1, tierIndex + bonusTier);
        else if (rel != null) tierIndex += rel.TierModifier;
        tierIndex = Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
        AdventurerTier tier = ranked ? RankedTier(leaderRank, false) : tiers[tierIndex];
        CurrentExpeditionTier = tier;
        CurrentRank = leaderRank;
        IsAssessment = false;

        float renown = ReputationSystem.Instance != null ? ReputationSystem.Instance.renown : 0f;
        int count = UnityEngine.Random.Range(tier.partySize.x, tier.partySize.y + 1) + Mathf.FloorToInt(renown / 25f)
                    + (SettlementManager.Instance != null ? SettlementManager.Instance.PartyBonus : 0)
                    + punitiveExtra + (rel != null ? rel.PartyModifier : 0)
                    + (tierIndex >= 1 ? GameDifficulty.PartyBonus : 0) + (ranked ? guild.ExtraMembers : 0)
                    + (PrisonSystem.Instance != null ? PrisonSystem.Instance.TakeRescueBonus() : 0);
        count = Mathf.Clamp(count, 1, punitive ? 10 : 8);

        killedThisExpedition = 0;
        fledThisExpedition = 0;
        expeditionStart = Time.time;
        isVisit = false;

        SpawnBoat(tierIndex, false);
        var spawned = new List<AdventurerAI>();
        for (int i = 0; i < count; i++)
        {
            int memberTier = tierIndex;
            AdventurerTier mt = tiers[tierIndex];
            int memberRank = -1;
            if (ranked)
            {
                // Les héros viennent accompagnés d'équipiers un ou deux crans en dessous
                memberRank = leaderRank >= 7 && i > 0
                    ? leaderRank - 1 - (UnityEngine.Random.value < 0.5f ? 1 : 0)
                    : GuildSystem.MemberRank(leaderRank, i);
                memberTier = Mathf.Clamp(GuildSystem.TierForRank(memberRank), 0, tiers.Length - 1);
                mt = RankedTier(memberRank, false);
            }
            else if (tierIndex >= 3 && i > 0) { memberTier = tierIndex - 1; mt = tiers[memberTier]; }
            var ai = spawner.SpawnForExpedition(mt, memberTier, i, count, null, RoleFor(ranked ? Mathf.Max(2, tierIndex) : tierIndex, i, count));
            if (ai != null)
            {
                ai.ApplyRank(memberRank);
                members.Add(ai);
                spawned.Add(ai);
            }
        }

        // Une Némésis peut se joindre au groupe (un survivant qui revient, monté en rang)
        var nem = ranked ? NemesisSystem.Instance?.PickForExpedition() : null;
        if (nem != null)
        {
            int nr = Mathf.Clamp(Mathf.Max(nem.rank + 1, leaderRank - 1), 0, Mathf.Min(GuildSystem.MaxRank, guild.officialRank + 2));
            nem.rank = nr;
            int nt = Mathf.Clamp(GuildSystem.TierForRank(nr), 0, tiers.Length - 1);
            var ai = spawner.SpawnForExpedition(RankedTier(nr, false), nt, count, count + 1, null, (AdventurerAI.Role)nem.role, NemesisSystem.FindPrefab(nem.prefab));
            if (ai != null)
            {
                ai.ApplyRank(nr);
                ai.MakeNemesis(nem);
                members.Add(ai);
                spawned.Add(ai);
                nem.lastSeenDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
                StoryManager.Instance?.OnNemesisReturns(nem);
            }
        }

        if (AdventurerGroupSystem.Instance != null && spawned.Count > 1)
            AdventurerGroupSystem.Instance.RegisterGroupSpawn(spawned);

        guild?.BeginExpedition(false, spawned);
        if (GameManager.Instance != null) GameManager.Instance.statExpeditions++;
        if (punitive) rel.AnnouncePunitive(tier, spawned.Count);
        OnExpeditionArrived?.Invoke(tier, spawned.Count);
        StartCoroutine(PitchCampLater(new List<AdventurerAI>(spawned)));
    }

    /// <summary>
    /// L'équipe d'évaluation de la Guilde : un groupe complet (guerrier, soigneuse, archer, mage)
    /// du rang estimé d'après les récits. Elle mesure le donjon puis rentre faire son rapport.
    /// </summary>
    private void LaunchAssessment()
    {
        var guild = GuildSystem.Instance;
        int rank = guild.reportedRank;
        int tierIndex = Mathf.Clamp(GuildSystem.TierForRank(rank), 0, tiers.Length - 1);
        var tier = RankedTier(rank, true);
        CurrentExpeditionTier = tier;
        CurrentRank = rank;
        IsAssessment = true;
        killedThisExpedition = fledThisExpedition = 0;
        expeditionStart = Time.time;
        isVisit = false;
        SpawnBoat(tierIndex, false);
        var roles = new[] { AdventurerAI.Role.Warrior, AdventurerAI.Role.Healer, AdventurerAI.Role.Archer, AdventurerAI.Role.Mage };
        var spawned = new List<AdventurerAI>();
        for (int i = 0; i < roles.Length; i++)
        {
            var ai = spawner.SpawnForExpedition(tier, tierIndex, i, roles.Length, null, roles[i]);
            if (ai == null) continue;
            ai.ApplyRank(rank);
            ai.IsAssessor = true;
            members.Add(ai);
            spawned.Add(ai);
        }
        if (AdventurerGroupSystem.Instance != null && spawned.Count > 1)
            AdventurerGroupSystem.Instance.RegisterGroupSpawn(spawned);
        guild.BeginExpedition(true, spawned);
        if (GameManager.Instance != null) GameManager.Instance.statExpeditions++;
        OnExpeditionArrived?.Invoke(tier, spawned.Count);
    }

    /// <summary>Membres de l'expédition en cours (cinématiques).</summary>
    public IReadOnlyList<AdventurerAI> Members => members;

    private IntruderMap expeditionMap;

    /// <summary>Carte partagée par les membres de l'expédition en cours (vierge à chaque nouvelle expédition).</summary>
    public IntruderMap MapFor(AdventurerAI a)
    {
        if (expeditionMap == null) expeditionMap = new IntruderMap();
        return expeditionMap;
    }

    private static readonly AdventurerAI.Role[] PartyOrder =
    {
        AdventurerAI.Role.Warrior, AdventurerAI.Role.Healer, AdventurerAI.Role.Archer, AdventurerAI.Role.Mage,
        AdventurerAI.Role.Warrior, AdventurerAI.Role.Archer, AdventurerAI.Role.Mage, AdventurerAI.Role.Warrior,
    };

    /// <summary>
    /// Composition d'un groupe : pêcheurs sans rôle, villageois accompagnés d'un chasseur,
    /// aventuriers et chevaliers en vrais groupes (guerrier, soigneuse, archer, mage...).
    /// </summary>
    public static AdventurerAI.Role RoleFor(int tierIndex, int i, int count)
    {
        if (tierIndex <= 0) return AdventurerAI.Role.Warrior;
        if (tierIndex == 1) return i == 1 && count >= 3 ? AdventurerAI.Role.Archer : AdventurerAI.Role.Warrior;
        if (count == 2) return i == 0 ? AdventurerAI.Role.Warrior : (UnityEngine.Random.value < 0.5f ? AdventurerAI.Role.Archer : AdventurerAI.Role.Mage);
        if (count == 1) return AdventurerAI.Role.Warrior;
        // le héros (i = 0) mène son groupe ; pas de soigneuse à 3 si on peut avoir un mage
        return PartyOrder[i % PartyOrder.Length];
    }

    /// <summary>Appelé par un aventurier qui meurt ou quitte l'île.</summary>
    public void OnMemberGone(AdventurerAI ai, bool killed)
    {
        SettlementManager.Instance?.OnIntruderGone(killed);
        if (!members.Remove(ai)) return;
        if (killed) killedThisExpedition++;
        else fledThisExpedition++;

        if (members.Count == 0) EndExpedition();
    }

    private void EndExpedition()
    {
        expeditionMap = null;   // la prochaine expédition repart sans rien savoir
        if (boat != null) { boat.Leave(); boat = null; }
        LastWasVisit = isVisit;
        IsAssessment = false;
        if (isVisit)
        {
            // Une simple visite ne décale pas le calendrier des expéditions
            isVisit = false;
            OnExpeditionEnded?.Invoke(killedThisExpedition, fledThisExpedition);
            return;
        }
        var rep = ReputationSystem.Instance;
        if (fledThisExpedition == 0 && killedThisExpedition > 0 && rep != null)
            rep.OnExpeditionLost();

        float renown = rep != null ? rep.renown : 0f;
        float wait = Mathf.Max(1f, baseDaysBetweenExpeditions - renown / renownPerDayReduction
                               - (SettlementManager.Instance != null ? SettlementManager.Instance.ExpeditionIntervalReduction : 0f))
                     + (RelationSystem.Instance != null ? RelationSystem.Instance.ExtraWaitDays : 0f);
        wait = Mathf.Max(1f, wait * GameDifficulty.ExpeditionWait * (GuildSystem.Instance != null ? GuildSystem.Instance.WaitMult : 1f));
        int today = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        NextExpeditionDay = today + Mathf.CeilToInt(wait) + UnityEngine.Random.Range(0, 2);

        OnExpeditionEnded?.Invoke(killedThisExpedition, fledThisExpedition);
    }

    public int CountInside()
    {
        int n = 0;
        foreach (var m in members) if (m != null && m.IsInsideDungeon) n++;
        return n;
    }
}
