using System;
using UnityEngine;

/// <summary>
/// Ce que le monde extérieur sait (et craint) de votre donjon.
/// - Menace : augmente quand des intrus meurent ou ne reviennent pas → expéditions plus fortes.
/// - Renommée : augmente quand des survivants racontent leur aventure → expéditions plus fréquentes/nombreuses.
/// - Découverte : tant que personne ne sait qu'un Noyau existe, seuls des pêcheurs/villageois viennent.
/// </summary>
public class ReputationSystem : MonoBehaviour
{
    public static ReputationSystem Instance { get; private set; }

    [Header("Paliers de menace (index = palier d'aventuriers)")]
    public float[] tierThresholds = { 0f, 6f, 20f, 50f, 110f };

    [Header("Évolution")]
    [Tooltip("Menace perdue chaque jour (proportion)")]
    [Range(0f, 0.5f)] public float dailyThreatDecay = 0.04f;
    [Tooltip("Nombre d'expéditions disparues sans laisser de trace avant découverte")]
    public int lostExpeditionsBeforeDiscovery = 2;

    [Header("État (lecture)")]
    public float threat;
    public float renown;
    public bool dungeonDiscovered;
    public int lostExpeditions;

    public event Action<int> OnTierChanged;
    public event Action OnDiscovered;

    private int lastTier;

    public int CurrentTier
    {
        get
        {
            int tier = 0;
            for (int i = 0; i < tierThresholds.Length; i++)
                if (threat >= tierThresholds[i]) tier = i;
            // Une fois classé par la Guilde, le rang du donjon décide de qui vient
            var guild = GuildSystem.Instance;
            if (guild != null && guild.Assessed) return GuildSystem.TierForRank(guild.officialRank);
            // Tant que la Guilde n'a pas évalué le donjon, seuls les gens du coin viennent
            if (!dungeonDiscovered) tier = Mathf.Min(tier, 1);
            else tier = 1;
            return tier;
        }
    }

    public float ThreatToNextTier
    {
        get
        {
            int t = CurrentTier;
            if (t + 1 >= tierThresholds.Length) return 0f;
            return Mathf.Max(0f, tierThresholds[t + 1] - threat);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        lastTier = CurrentTier;
        if (GameManager.Instance != null)
            GameManager.Instance.OnNewDay += OnNewDay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null)
            GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private void OnNewDay(int day)
    {
        threat *= 1f - dailyThreatDecay;
        // Un donjon bien décoré fait parler de lui (s'il est connu)
        float prestige = PlacedObject.TotalPrestige();
        if (dungeonDiscovered && prestige > 0f)
        {
            renown += prestige * 0.1f;
            GameHUD.Toast($"Les récits sur votre donjon se répandent (+{prestige * 0.1f:0.#} renommée)", GameHUD.GoldColor);
        }
        CheckTier();
    }

    public void OnAdventurerKilled(AdventurerAI adv)
    {
        float tierBonus = adv != null ? adv.TierIndex : 0;
        threat += (2f + tierBonus * 1.5f) * GameDifficulty.ThreatGain;
        CheckTier();
    }

    public void OnAdventurerFled(AdventurerAI adv)
    {
        renown += 3f + (adv != null ? adv.TierIndex * 2f : 0f);
        threat += 0.5f;

        // Un survivant qui a vu l'intérieur (ou le cristal) révèle l'existence du donjon
        if (adv != null && (adv.HasSeenCore || adv.HasEnteredDungeon))
            Discover();
        CheckTier();
    }

    /// <summary>Une expédition entière n'est jamais revenue.</summary>
    public void OnExpeditionLost()
    {
        lostExpeditions++;
        threat += 3f;
        // un seul bateau disparu suffit à inquiéter le continent
        if (lostExpeditions >= Mathf.Min(1, lostExpeditionsBeforeDiscovery))
            Discover();
        CheckTier();
    }

    public void Discover()
    {
        if (dungeonDiscovered) return;
        dungeonDiscovered = true;
        OnDiscovered?.Invoke();
        CheckTier();
    }

    private void CheckTier()
    {
        int t = CurrentTier;
        if (t != lastTier)
        {
            bool up = t > lastTier;
            lastTier = t;
            if (up) OnTierChanged?.Invoke(t);
        }
    }
}
