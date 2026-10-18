using System;
using UnityEngine;

/// <summary>
/// Niveau du Noyau. Gagne de l'XP en tuant ou en faisant fuir des intrus.
/// Chaque niveau augmente le nombre de monstres/pièges, la réserve et l'absorption de mana.
/// </summary>
public class PlayerLevel : MonoBehaviour
{
    [Header("Level")]
    public int currentLevel = 1;
    public int currentXP = 0;
    public int xpForNextLevel = 100;
    public float xpMultiplier = 1.5f;

    [Header("Monster Limits")]
    public int baseMonsterLimit = 5;
    public int monstersPerLevel = 2;

    [Header("Trap Limits")]
    public int baseTrapLimit = 4;
    public int trapsPerLevel = 2;

    public static PlayerLevel Instance { get; private set; }

    public event Action<int> OnLevelUp;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddXP(int amount)
    {
        if (amount <= 0) return;
        currentXP += amount;
        while (currentXP >= xpForNextLevel) LevelUp();
    }

    private void LevelUp()
    {
        currentLevel++;
        currentXP -= xpForNextLevel;
        xpForNextLevel = XpFor(currentLevel);
        OnLevelUp?.Invoke(currentLevel);
        if (!CrawlerMode.Active) GameHUD.Toast("Un point de talent à dépenser (bouton Talents)", GameHUD.GoldColor);
    }

    /// <summary>
    /// XP pour passer du niveau l au suivant : 60, 106, 164, 234... (≈ 20 000 XP cumulés pour le niveau 20,
    /// l'horizon de fin de partie avec les chevaliers et les héros).
    /// </summary>
    public static int XpFor(int l) { int k = Mathf.Max(0, l - 1); return 60 + 40 * k + 6 * k * k; }

    private void Start()
    {
        if (currentLevel <= 1 && currentXP == 0) xpForNextLevel = XpFor(1);
    }

    public int GetMaxMonsters() => baseMonsterLimit + (currentLevel - 1) * monstersPerLevel + Talents.MonsterLimitBonus;
    public int GetMaxTraps() => baseTrapLimit + (currentLevel - 1) * trapsPerLevel + Talents.TrapLimitBonus;

    public int GetCurrentMonsterCount()
    {
        int n = 0;
        foreach (var g in GobelinAI.All)
        {
            if (g == null) continue;
            var u = g.GetComponent<MonsterUnit>();
            if (u == null || !u.summoned) n++;   // les squelettes invoqués ne comptent pas
        }
        return n;
    }
    public int GetCurrentTrapCount() => PlacedObject.Count(ItemKind.Trap);

    // Plus de plafond : c'est la réserve de mana et l'entretien journalier qui limitent la taille du donjon
    public bool CanSpawnMonster() => true;
    public bool CanPlaceTrap() => true;

    public void OnAdventurerKilled() => AddXP(50);
}
