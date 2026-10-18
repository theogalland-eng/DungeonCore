using System;
using UnityEngine;

/// <summary>
/// Le mana du Noyau.
/// - L'orbe absorbe naturellement le mana ambiant (régénération passive, plus forte la nuit).
/// - Les intrus présents DANS le donjon sont drainés lentement (intérêt à les laisser vivre !).
/// - Les intrus tués libèrent une grande quantité de mana.
/// </summary>
public class ManaSystem : MonoBehaviour
{
    public static ManaSystem Instance { get; private set; }

    [Header("Réserve")]
    public float startMana = 300f;
    public float baseMaxMana = 500f;
    public float maxManaPerLevel = 250f;

    [Header("Absorption")]
    [Tooltip("Mana absorbé par seconde dans l'air")]
    public float passiveRegenPerSecond = 2f;
    public float regenPerLevel = 0.5f;
    [Tooltip("Multiplicateur de régénération la nuit")]
    public float nightRegenMultiplier = 1.5f;
    [Tooltip("Mana drainé par seconde pour chaque intrus dans le donjon")]
    public float intruderDrainPerSecond = 1.5f;
    [Tooltip("Mana par seconde gagné par point de prestige (décors, trophées)")]
    public float prestigeRegenPerPoint = 0.02f;

    public event Action<int, int> OnManaChanged;   // (actuel, max)

    private float mana;
    private int lastNotified = int.MinValue;

    public const float StartMax = 150f, PerLevel = 60f;
    public int Current => Mathf.FloorToInt(mana);
    public int Max
    {
        get
        {
            int level = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
            // 150 au niveau 1 : c'est la réserve (et l'entretien journalier) qui limite la taille de l'armée
            return Mathf.RoundToInt(StartMax + (level - 1) * PerLevel);
        }
    }

    /// <summary>Régénération totale actuelle (mana / seconde).</summary>
    public float CurrentRegen { get; private set; }
    public int IntrudersInside { get; private set; }
    public float PrestigeRegen { get; private set; }

    /// <summary>Valeur exacte (sauvegarde).</summary>
    public float Raw => mana;
    public void SetRaw(float v)
    {
        mana = Mathf.Max(0f, v);
        Notify();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        mana = StartMax;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (MainMenu.Frozen) return;
        int level = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
        float regen = passiveRegenPerSecond + (level - 1) * regenPerLevel;
        if (GameManager.Instance != null && GameManager.Instance.IsNight)
            regen *= nightRegenMultiplier;

        // Un donjon prestigieux attire le mana ambiant
        PrestigeRegen = PlacedObject.TotalPrestige() * prestigeRegenPerPoint;
        regen += PrestigeRegen;
        regen *= GameDifficulty.ManaRegen * (IslandEvents.Instance != null ? IslandEvents.Instance.ManaMult : 1f);

        int inside = 0;
        float drain = 0f;
        var layers = DungeonLayers.Instance;
        foreach (var a in AdventurerAI.All)
        {
            if (a == null || !a.IsInsideDungeon) continue;
            inside++;
            var theme = layers != null ? layers.ThemeAt(a.transform.position) : null;
            drain += intruderDrainPerSecond * (theme != null ? theme.manaDrainMult : 1f) * Talents.ManaDrainMult;
        }
        IntrudersInside = inside;
        regen += drain;

        CurrentRegen = regen;

        if (mana < Max)
            mana = Mathf.Min(Max, mana + regen * Time.deltaTime);

        Notify();
    }

    private void Notify()
    {
        int cur = Current;
        if (cur != lastNotified)
        {
            lastNotified = cur;
            OnManaChanged?.Invoke(cur, Max);
        }
    }

    public bool CanAfford(int cost) => Current >= cost;

    /// <summary>Dépense du mana si possible. Affiche un message sinon.</summary>
    public bool TrySpend(int cost, bool showMessage = true)
    {
        if (Current < cost)
        {
            if (showMessage) GameHUD.Toast($"Pas assez de mana ({cost} requis)", GameHUD.WarningColor);
            return false;
        }
        mana -= cost;
        Notify();
        return true;
    }

    /// <summary>Ajoute (ou retire si négatif) du mana, borné à la réserve max.</summary>
    public void Add(float amount)
    {
        mana = Mathf.Clamp(mana + amount, 0f, Mathf.Max(Max, mana));
        if (amount > 0) mana = Mathf.Min(mana, Max);
        Notify();
    }
}
