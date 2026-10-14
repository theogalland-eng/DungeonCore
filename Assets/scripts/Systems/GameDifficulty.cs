using UnityEngine;

public enum Difficulty { Facile, Moyen, Difficile, Impitoyable }

/// <summary>
/// Niveau de difficulté choisi en démarrant une partie (sauvegardé avec elle).
/// Facile = l'équilibrage d'origine ; chaque cran rend les intrus plus coriaces et nombreux,
/// le mana plus rare, l'entretien plus cher, et la Guilde plus prompte à réagir.
/// </summary>
public static class GameDifficulty
{
    public static Difficulty Current = Difficulty.Moyen;

    public static readonly string[] Names = { "Facile", "Moyen", "Difficile", "Impitoyable" };
    public static readonly string[] Descriptions =
    {
        "Pour découvrir le jeu. Les intrus sont peu nombreux et fuient facilement, le mana coule à flots.",
        "L'expérience prévue. Les aventuriers se défendent, la Guilde réagit vite, le mana se gère.",
        "Pour les Noyaux aguerris. Groupes plus nombreux, mieux classés, et qui fuient rarement.",
        "Aucune pitié. Des intrus féroces, un mana rare, et une seule vie : pas de sauvegarde manuelle.",
    };
    public static readonly Color[] Colors =
    {
        new Color(0.55f, 0.9f, 0.6f), new Color(0.65f, 0.8f, 1f), new Color(1f, 0.7f, 0.35f), new Color(1f, 0.35f, 0.35f),
    };

    private static int I => Mathf.Clamp((int)Current, 0, 3);
    public static string Name => Names[I];
    public static Color Color => Colors[I];

    /// <summary>PV des intrus.</summary>
    public static float IntruderHp => new[] { 1f, 1.2f, 1.45f, 1.8f }[I];
    /// <summary>Dégâts des intrus.</summary>
    public static float IntruderDamage => new[] { 1f, 1.15f, 1.35f, 1.6f }[I];
    /// <summary>Chance de fuir des intrus (multiplicateur).</summary>
    public static float FleeMult => new[] { 1f, 0.9f, 0.78f, 0.62f }[I];
    /// <summary>Régénération de mana.</summary>
    public static float ManaRegen => new[] { 1f, 0.9f, 0.8f, 0.7f }[I];
    /// <summary>Coût d'entretien quotidien.</summary>
    public static float Upkeep => new[] { 1f, 1.1f, 1.25f, 1.45f }[I];
    /// <summary>Menace gagnée par intrus tué.</summary>
    public static float ThreatGain => new[] { 1f, 1.2f, 1.4f, 1.7f }[I];
    /// <summary>Attente entre deux expéditions.</summary>
    public static float ExpeditionWait => new[] { 1f, 0.9f, 0.8f, 0.7f }[I];
    /// <summary>Membres supplémentaires par expédition.</summary>
    public static int PartyBonus => new[] { 0, 0, 1, 2 }[I];
    /// <summary>Rangs supplémentaires accordés par la Guilde (évaluation plus sévère).</summary>
    public static int RankBias => new[] { -1, 0, 0, 1 }[I];
    /// <summary>Sauvegardes manuelles autorisées (Impitoyable : sauvegarde automatique uniquement).</summary>
    public static bool ManualSaves => Current != Difficulty.Impitoyable;
}
