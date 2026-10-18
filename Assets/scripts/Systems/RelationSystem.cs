using System;
using UnityEngine;

/// <summary>
/// Relations entre le Noyau et la colonie humaine (-100 haine … +100 entente).
///   - Tuer des intrus, et surtout des colons, dégrade la relation ; laisser repartir, épargner
///     ou verser des pots-de-vin (mana) l'améliore.
///   - À partir de +60, on peut proposer une TRÊVE : la colonie verse une offrande de mana chaque
///     matin et ses expéditions se font plus rares et plus petites. Tuer un habitant pendant la trêve
///     est une TRAHISON : trêve rompue, relation effondrée, expédition punitive.
///   - Haine : expéditions plus fortes. Raids sanglants et trahisons déclenchent une EXPÉDITION PUNITIVE.
/// </summary>
public class RelationSystem : MonoBehaviour
{
    public static RelationSystem Instance { get; private set; }

    [Range(-100f, 100f)] public float relation;
    public bool truce;
    public int truceSinceDay = -1;

    [Header("Réglages")]
    public float truceThreshold = 60f;
    public int truceCost = 150;
    public int baseBribe = 60;
    public float bribeGain = 12f;

    public event Action OnChanged;

    private int punitiveDay = -1;
    private int punitiveBonus;
    private int bribesToday;
    private int bribeDay = -1;

    public bool PunitivePending => punitiveDay >= 0;
    public int PunitiveDay => punitiveDay;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    private static bool ColonyExists => SettlementManager.Instance != null && SettlementManager.Instance.stage > 0 && !SettlementManager.Instance.Outpost;
    private static int Today => GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

    public string StateName
    {
        get
        {
            if (truce) return "Trêve";
            if (relation <= -50f) return "Haine";
            if (relation <= -15f) return "Hostilité";
            if (relation < 25f) return "Méfiance";
            if (relation < truceThreshold) return "Tolérance";
            return "Respect";
        }
    }

    public Color StateColor =>
        truce ? new Color(0.5f, 1f, 0.65f) :
        relation <= -50f ? new Color(1f, 0.35f, 0.3f) :
        relation <= -15f ? new Color(1f, 0.6f, 0.35f) :
        relation < 25f ? new Color(0.9f, 0.85f, 0.6f) : new Color(0.6f, 0.9f, 1f);

    /// <summary>Palier d'intrus en plus (haine).</summary>
    public int TierModifier => !truce && relation <= -50f ? 1 : 0;
    /// <summary>Membres en plus / en moins dans chaque expédition.</summary>
    public int PartyModifier => truce ? -1 : relation <= -15f ? 1 : 0;
    /// <summary>Jours d'attente supplémentaires entre deux expéditions.</summary>
    public float ExtraWaitDays => truce ? 1f : relation >= 25f ? 0.5f : 0f;
    public int DailyTribute => truce && SettlementManager.Instance != null ? 15 + 10 * SettlementManager.Instance.stage : 0;

    public int BribeCost
    {
        get
        {
            int stage = SettlementManager.Instance != null ? SettlementManager.Instance.stage : 0;
            int n = bribeDay == Today ? bribesToday : 0;
            return Mathf.RoundToInt((baseBribe + 20 * stage) * Mathf.Pow(1.5f, n));
        }
    }

    private void Change(float delta, string reason = null)
    {
        relation = Mathf.Clamp(relation + delta, -100f, 100f);
        if (!string.IsNullOrEmpty(reason) && Mathf.Abs(delta) >= 1f && ColonyExists)
            GameHUD.Toast($"Colonie : {reason} ({(delta > 0 ? "+" : "")}{delta:0})", delta > 0 ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.6f, 0.5f));
        OnChanged?.Invoke();
    }

    /// <summary>Petit pas de relation sans message (commerce secret).</summary>
    public void Nudge(float delta) => Change(delta);

    // ─────────────────────────────────────────────
    // ÉVÉNEMENTS
    // ─────────────────────────────────────────────

    public void OnIntruderKilled(AdventurerAI adv)
    {
        if (!ColonyExists || adv == null) return;
        if (adv.isLocal)
        {
            if (truce) { Betrayal("Vous avez tué un habitant pendant la trêve"); return; }
            Change(-8f, "un habitant a péri dans le donjon");
        }
        else Change(truce ? -2f : -3f);
    }

    public void OnIntruderLeft(AdventurerAI adv)
    {
        if (!ColonyExists || adv == null) return;
        if (adv.Spared) Change(adv.isLocal ? 8f : 5f, $"{adv.DisplayName} a été épargné");
        else Change(adv.isLocal ? 3f : 1.5f);
    }

    /// <summary>Laisse partir tous les intrus présents (les monstres s'écartent).</summary>
    public int SpareAll()
    {
        int n = 0;
        foreach (var a in AdventurerAI.All.ToArray())
            if (a != null && !a.Spared) { a.Spare(); n++; }
        if (n > 0)
        {
            GameHUD.Toast($"Vous laissez repartir {n} intrus : vos monstres s'écartent", new Color(0.6f, 1f, 0.7f));
            StoryManager.Instance?.PlayOnce("first_spare",
                new StoryManager.Line(StoryManager.SelfName, "Partez. Et racontez que le Noyau sait aussi faire preuve de clémence."));
        }
        return n;
    }

    public bool CanBribe(out string reason)
    {
        reason = null;
        if (!ColonyExists) { reason = "Aucune colonie sur l'île"; return false; }
        if (relation >= 100f) { reason = "Relation déjà au maximum"; return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(BribeCost)) { reason = $"{BribeCost} mana requis"; return false; }
        return true;
    }

    /// <summary>Cristaux de mana déposés en secret près du camp : les colons apprécient.</summary>
    public bool Bribe()
    {
        if (!CanBribe(out string reason)) { GameHUD.Toast(reason, GameHUD.WarningColor); return false; }
        int cost = BribeCost;
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(cost)) return false;
        if (bribeDay != Today) { bribeDay = Today; bribesToday = 0; }
        bribesToday++;
        var colony = SettlementManager.Instance;
        colony.morale = Mathf.Min(100f, colony.morale + 4f);
        colony.food += 6;
        Change(bribeGain, "des cristaux de mana déposés près du camp");
        StoryManager.Instance?.PlayOnce("first_bribe",
            new StoryManager.Line(StoryManager.NarratorName, "À l'aube, les colons découvrent des cristaux luminescents devant leurs tentes. Ils murmurent, inquiets... puis les ramassent."));
        return true;
    }

    public bool CanProposeTruce(out string reason)
    {
        reason = null;
        if (!ColonyExists) { reason = "Aucune colonie"; return false; }
        if (truce) { reason = "Trêve déjà en place"; return false; }
        if (relation < truceThreshold) { reason = $"Relation {relation:0}/{truceThreshold:0} requise"; return false; }
        if (PunitivePending) { reason = "Une expédition punitive est en route"; return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(truceCost)) { reason = $"{truceCost} mana requis"; return false; }
        return true;
    }

    public bool ProposeTruce()
    {
        if (!CanProposeTruce(out string reason)) { GameHUD.Toast(reason, GameHUD.WarningColor); return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(truceCost)) return false;
        truce = true;
        truceSinceDay = Today;
        GameHUD.Toast("Trêve conclue avec la colonie", new Color(0.5f, 1f, 0.65f));
        StoryManager.Instance?.Play(
            new StoryManager.Line(StoryManager.NarratorName, "Un vieil homme s'avance seul jusqu'à l'entrée de la caverne et y dépose une branche d'olivier."),
            new StoryManager.Line(StoryManager.SelfName, "Une trêve... Tant qu'ils ne franchissent pas mon seuil, je les laisserai vivre."),
            new StoryManager.Line(StoryManager.SystemName, "TRÊVE : offrande de mana chaque matin, expéditions plus rares et plus petites. Tuer un habitant de la colonie serait une trahison."));
        OnChanged?.Invoke();
        return true;
    }

    public void BreakTruce()
    {
        if (!truce) return;
        Betrayal("Vous avez rompu la trêve");
    }

    private void Betrayal(string why)
    {
        truce = false;
        truceSinceDay = -1;
        relation = Mathf.Min(relation, 0f);
        Change(-45f, why);
        SchedulePunitive(2, 3);
        StoryManager.Instance?.PlayOnce("betrayal",
            new StoryManager.Line(StoryManager.NarratorName, "La cloche du port sonne toute la nuit. Des pigeons s'envolent vers le continent, porteurs d'un seul mot : trahison."));
    }

    /// <summary>Un raid terrorise ou massacre la colonie.</summary>
    public void OnRaid(bool lethal, int killed)
    {
        if (truce) { Betrayal(lethal ? "Raid sanglant pendant la trêve" : "Raid pendant la trêve"); return; }
        if (lethal)
        {
            Change(-15f - 8f * killed, "massacre nocturne");
            if (killed > 0) SchedulePunitive(killed >= 3 ? 2 : 1, 1 + killed / 2);
        }
        else Change(-10f, "nuit de terreur");
    }

    // ─────────────────────────────────────────────
    // EXPÉDITION PUNITIVE
    // ─────────────────────────────────────────────

    public void SchedulePunitive(int tierBonus, int extra)
    {
        int day = Today + 1;
        punitiveDay = punitiveDay >= 0 ? Mathf.Min(punitiveDay, day) : day;
        punitiveBonus = Mathf.Max(punitiveBonus, tierBonus * 10 + extra);   // encodé : palier*10 + membres
        ExpeditionManager.Instance?.BringForward(punitiveDay);
        GameHUD.Toast($"La colonie appelle à la vengeance : expédition punitive attendue jour {punitiveDay} !", GameHUD.DangerColor);
        ReputationSystem.Instance?.Discover();
        OnChanged?.Invoke();
    }

    /// <summary>Consommé par l'ExpeditionManager au lancement d'une expédition.</summary>
    public bool TakePunitive(out int tierBonus, out int extraMembers)
    {
        tierBonus = extraMembers = 0;
        if (punitiveDay < 0 || Today < punitiveDay) return false;
        tierBonus = punitiveBonus / 10;
        extraMembers = punitiveBonus % 10;
        punitiveDay = -1;
        punitiveBonus = 0;
        OnChanged?.Invoke();
        return true;
    }

    public void AnnouncePunitive(AdventurerTier tier, int count)
    {
        GameHUD.Toast($"EXPÉDITION PUNITIVE : {count} × {tier.displayName} viennent venger la colonie !", GameHUD.DangerColor);
        CameraController.Instance?.Shake(0.15f, 0.6f);
        StoryManager.Instance?.PlayOnce("first_punitive",
            new StoryManager.Line(StoryManager.NarratorName, "Des bannières noires sur la mer. Ce ne sont pas des curieux : ce sont des vengeurs."),
            new StoryManager.Line(StoryManager.SelfName, "J'ai frappé la colonie... voici le prix du sang."));
    }

    // ─────────────────────────────────────────────
    // QUOTIDIEN
    // ─────────────────────────────────────────────

    private void OnNewDay(int day)
    {
        if (!ColonyExists) return;
        // la rancune (ou la gratitude) s'estompe doucement
        relation = Mathf.MoveTowards(relation, 0f, truce ? 0.5f : 2f);
        if (truce)
        {
            int tribute = DailyTribute;
            ManaSystem.Instance?.Add(tribute);
            GameHUD.Toast($"Offrande de la colonie : +{tribute} mana", new Color(0.6f, 1f, 0.7f));
            // une trêve s'use si la relation retombe trop
            if (relation < 20f)
            {
                truce = false;
                GameHUD.Toast("La trêve s'est éteinte : la colonie ne vous fait plus confiance", GameHUD.WarningColor);
            }
        }
        OnChanged?.Invoke();
    }

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    public void Capture(SaveData d)
    {
        d.relation = relation;
        d.truce = truce;
        d.truceSinceDay = truceSinceDay;
        d.punitiveDay = punitiveDay;
        d.punitiveBonus = punitiveBonus;
    }

    public void Restore(SaveData d)
    {
        relation = d.relation;
        truce = d.truce;
        truceSinceDay = d.truceSinceDay;
        punitiveDay = d.punitiveDay;
        punitiveBonus = d.punitiveBonus;
        if (punitiveDay >= 0) ExpeditionManager.Instance?.BringForward(punitiveDay);
        OnChanged?.Invoke();
    }
}
