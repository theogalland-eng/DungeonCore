using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OÙ VIVENT LES MONSTRES (mode Donjon) :
///   - COMMUNS (rat, slime, gobelin, squelette) : partout ; sur un étage à thème, ils naissent sous la VARIANTE
///     du lieu (MonsterVariant : slime de feu dans la Fournaise, gobelin des glaces dans la Caverne gelée...) ;
///   - NATIFS : toutes les autres familles, qu'on ne peut invoquer que sur un étage de leur thème, où elles
///     ont +15 % de PV et de dégâts (« chez elles »). Envoyées ailleurs, elles perdent ce bonus.
/// Thèmes : volcanic (surface), lava, aqua, ice, fungal, crystal, crypt, fortress, catacomb.
/// </summary>
public static class MonsterHabitat
{
    public static readonly HashSet<string> Commons = new HashSet<string> { "rat", "slime", "goblin", "skeleton" };

    private static readonly Dictionary<string, string[]> Natives = new Dictionary<string, string[]>
    {
        { "orc", new[] { "volcanic", "fortress" } },
        { "gnoll", new[] { "volcanic" } },
        { "boar", new[] { "volcanic" } },
        { "imp", new[] { "lava" } },
        { "demon", new[] { "lava" } },
        { "lizard", new[] { "aqua" } },
        { "zombie", new[] { "aqua", "crypt", "catacomb" } },
        { "ghost", new[] { "ice", "crypt" } },
        { "golem", new[] { "ice", "crystal", "fortress" } },
        { "mushroom", new[] { "fungal" } },
        { "temple_spirit", new[] { "crystal" } },
        { "lich", new[] { "crypt", "catacomb" } },
        { "vampire", new[] { "catacomb" } },
        { "spider", new[] { "fungal", "catacomb" } },
        { "bat", new[] { "volcanic", "catacomb" } },
    };

    public static bool IsCommon(string family) => family != null && Commons.Contains(family);

    /// <summary>Thème de l'étage à cette position (« volcanic » par défaut).</summary>
    public static string ThemeAt(Vector2 p)
    {
        var l = DungeonLayers.Instance;
        var t = l != null ? l.ThemeAt(p) : null;
        return t != null && !string.IsNullOrEmpty(t.id) ? t.id : "volcanic";
    }

    public static string ThemeName(string id)
    {
        var cat = DungeonCatalog.Get();
        if (cat != null) foreach (var t in cat.themes) if (t.id == id) return t.displayName;
        return id;
    }

    public static bool IsHome(string family, string theme) =>
        family != null && Natives.TryGetValue(family, out var homes) && System.Array.IndexOf(homes, theme) >= 0;

    /// <summary>Peut-on invoquer cette famille sur un étage de ce thème ?</summary>
    public static bool Allowed(string family, string theme) =>
        string.IsNullOrEmpty(family) || IsCommon(family) || !Natives.ContainsKey(family) || IsHome(family, theme);

    /// <summary>« Fournaise » ou « Crypte hantée, Catacombes » (vide pour les communs).</summary>
    public static string HomesText(string family)
    {
        if (family == "mimic") return "salle du trésor";
        if (family == null || !Natives.TryGetValue(family, out var homes)) return "";
        var names = new List<string>();
        foreach (var h in homes) names.Add(ThemeName(h));
        return string.Join(", ", names);
    }

    public static string FamilyOf(CatalogItem item)
    {
        var u = item != null && item.prefab != null ? item.prefab.GetComponent<MonsterUnit>() : null;
        return u != null ? u.familyId : null;
    }
}
