using System.Collections.Generic;

/// <summary>
/// RANGEMENT DU PANNEAU DE CONSTRUCTION : pièges et décors regroupés en sections (avec un en-tête),
/// triés dans chaque section par niveau requis puis par coût.
/// </summary>
public static class CatalogSections
{
    private static readonly HashSet<string> Mechanisms = new HashSet<string>
        { "door_wood", "lever_door", "lever", "rune_door", "pressure_plate", "door_iron", "door_skull" };

    private static readonly HashSet<string> Useful = new HashSet<string>
        { "food_cache", "supplies", "nest", "anvil", "forge", "cell" };
    private static readonly HashSet<string> Lights = new HashSet<string>
        { "torch", "wall_torch", "brazier", "candelabra" };
    private static readonly HashSet<string> Nature = new HashSet<string>
        { "crystal_blue", "crystal_red", "crystal_yellow", "crystal_green", "crystal_black", "mushrooms_long", "mushroom_slime", "mushrooms_small",
          "stalagmites", "stalagmites_gray", "magma_vent" };
    private static readonly HashSet<string> Macabre = new HashSet<string>
        { "bones", "skull_pile", "coffin", "hanging", "sarcophagus", "sarcophagus_open", "demon_hand" };
    private static readonly HashSet<string> Treasure = new HashSet<string>
        { "chest", "chest_royal", "gold", "gold_pile", "fountain", "statue_fire", "demon_altar", "skull" };

    /// <summary>Sections de l'onglet (null : onglet sans sections, liste simple).</summary>
    public static List<(string title, List<CatalogItem> items)> Group(CatalogCategory tab, List<CatalogItem> items)
    {
        string[] order;
        System.Func<CatalogItem, string> sectionOf;
        if (tab == CatalogCategory.Trap)
        {
            order = new[] { "Pièges au sol", "Pièges muraux", "Pièges de l'étage", "Portes et mécanismes" };
            sectionOf = it =>
                Mechanisms.Contains(it.id) || it.displayName == "Pierre runique" ? order[3]
                : TrapRules.ThemeOnly.ContainsKey(it.id) ? order[2]
                : TrapRules.WallMounted.Contains(it.id) ? order[1]
                : order[0];
        }
        else if (tab == CatalogCategory.Decor)
        {
            order = new[] { "Utile au donjon", "Lumières", "Cristaux et nature", "Macabre", "Mobilier et trophées", "Trésors et prestige" };
            sectionOf = it =>
                Useful.Contains(it.id) ? order[0] : Lights.Contains(it.id) ? order[1] : Nature.Contains(it.id) ? order[2]
                : Macabre.Contains(it.id) ? order[3] : Treasure.Contains(it.id) ? order[5] : order[4];
        }
        else return null;

        var groups = new List<(string, List<CatalogItem>)>();
        foreach (var title in order)
        {
            var list = items.FindAll(it => sectionOf(it) == title);
            if (list.Count == 0) continue;
            list.Sort((a, b) => a.unlockLevel != b.unlockLevel ? a.unlockLevel.CompareTo(b.unlockLevel) : a.cost.CompareTo(b.cost));
            groups.Add((title, list));
        }
        return groups;
    }
}
