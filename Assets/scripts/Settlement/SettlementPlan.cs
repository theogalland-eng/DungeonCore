using System.Collections.Generic;

/// <summary>Un bâtiment de la colonie humaine.</summary>
public class BuildingDef
{
    public string id, name;
    public int stage;
    /// <summary>Emprise au sol (cases). Le sprite est posé au milieu du bord inférieur.</summary>
    public int w, h;
    public int wood, stone;
    public float buildTime;
    /// <summary>Colons logés.</summary>
    public int pop;
    public string sprite;
    /// <summary>Scène complète d'un pack (SettlementArt.stamps) à la place d'un sprite seul : bâtiment, cour, habitants.</summary>
    public string stamp;
    /// <summary>Sprite intermédiaire du chantier (ex : maison sans toit).</summary>
    public string midSprite;
    /// <summary>Nourriture produite chaque jour (champs, séchoir).</summary>
    public int foodPerDay;
    /// <summary>Prospérité apportée chaque jour.</summary>
    public int prosperityPerDay;
    public bool light;
    public string description;
    /// <summary>Durée réelle du chantier (secondes de jeu).</summary>
    public float BuildSeconds => buildTime * SettlementPlan.BuildTimeScale;
}

/// <summary>
/// Plan de développement de la colonie : stades et bâtiments.
/// Partagé par le générateur d'île (emplacements) et le SettlementManager (construction).
/// </summary>
public static class SettlementPlan
{
    public static readonly string[] StageNames = { "Aucune", "Camp de la Guilde", "Port de pêche", "Hameau", "Village", "Bourg fortifié", "Cité du Donjon" };

    /// <summary>Prospérité nécessaire pour passer au stade (index = stade visé).</summary>
    public static readonly int[] StageProsperity = { 0, 0, 170, 430, 820, 1350, 2000 };

    /// <summary>Dernier stade : la cité du plan (celle du mode Aventurier).</summary>
    public static int FinalStage => StageNames.Length - 1;
    /// <summary>Stade où la cité se fortifie : remparts de pierre, portes et rues pavées (palissade au stade précédent).</summary>
    public const int FortifiedStage = 5;
    public const int PalisadeStage = 4;

    /// <summary>Les chantiers prennent du temps : multiplicateur des durées de construction.</summary>
    public const float BuildTimeScale = 1.6f;

    /// <summary>Conditions de fondation (jours comptés depuis le classement du donjon par la Guilde) : le donjon doit avoir fait parler de lui.</summary>
    public const int FoundMinDay = 3;
    public const int FoundSurvivors = 3;
    public const float FoundRenown = 15f;
    /// <summary>Au plus tard, la Guilde s'installe ce nombre de jours après le classement.</summary>
    public const int FoundLatestDay = 9;

    /// <summary>Maisons de la cité : (stade, sprite) — les quartiers se remplissent au fil des stades.</summary>
    static readonly (int stage, string sprite)[] Homes =
    {
        (3, "town_1"), (4, "house_b"), (4, "town_2"), (4, "house_a"), (4, "town_4"),
        (5, "town_3"), (5, "bld_farmhouse"), (5, "town_1"), (5, "town_2"), (5, "house_b"),
        (3, "house_a"), (4, "town_4"), (5, "house_b"), (5, "town_1"), (6, "bld_farmhouse"), (6, "town_3"),
        (6, "house_a"), (6, "town_2"), (6, "town_4"), (6, "house_b"),
    };
    public static int HomeCount => Homes.Length;

    public static readonly List<BuildingDef> Buildings = BuildDefs();

    static List<BuildingDef> BuildDefs()
    {
        var l = new List<BuildingDef>
        {
            // ── 1. Camp de la Guilde
            new BuildingDef { id = "campfire", name = "Feu de camp", stage = 1, w = 2, h = 2, wood = 6, buildTime = 10, sprite = "campfire_pot", light = true,
                description = "Les expéditions s'y reposent et s'y soignent." },
            new BuildingDef { id = "tent_a", name = "Tente", stage = 1, w = 3, h = 2, wood = 10, buildTime = 15, pop = 2, sprite = "tent_white" },
            new BuildingDef { id = "tent_b", name = "Tente", stage = 1, w = 3, h = 2, wood = 10, buildTime = 15, pop = 2, sprite = "tent_green" },
            new BuildingDef { id = "stockpile", name = "Réserve", stage = 1, w = 2, h = 2, wood = 6, buildTime = 8, sprite = "crate_stack" },

            // ── 2. Port de pêche
            new BuildingDef { id = "pier", name = "Ponton", stage = 2, w = 0, h = 0, wood = 20, buildTime = 25,
                description = "Plus grand ponton : de plus gros navires accostent." },
            new BuildingDef { id = "fisher_hut", name = "Cabane de pêcheur", stage = 2, w = 4, h = 3, wood = 25, stone = 4, buildTime = 35, pop = 3,
                sprite = "house_b", midSprite = "house_b_walls", foodPerDay = 4 },
            new BuildingDef { id = "fish_rack", name = "Séchoir à poissons", stage = 2, w = 3, h = 1, wood = 6, buildTime = 10, sprite = "fish_rack", foodPerDay = 3 },
            new BuildingDef { id = "tent_c", name = "Tente", stage = 2, w = 3, h = 2, wood = 10, buildTime = 15, pop = 2, sprite = "tent_red" },

            // ── 3. Hameau
            new BuildingDef { id = "house_1", name = "Taverne", stage = 3, w = 12, h = 8, wood = 40, stone = 12, buildTime = 50, pop = 3, stamp = "tavern", light = true, prosperityPerDay = 3,
                description = "On y boit, on y raconte le donjon : les rumeurs y circulent." },
            new BuildingDef { id = "house_3", name = "Forge", stage = 3, w = 10, h = 7, wood = 30, stone = 16, buildTime = 45, pop = 2, stamp = "forge", light = true, prosperityPerDay = 2,
                description = "Le forgeron répare les armures : les aventuriers repartent mieux équipés." },
            new BuildingDef { id = "house_2", name = "Maison", stage = 3, w = 4, h = 3, wood = 30, stone = 8, buildTime = 40, pop = 4, sprite = "house_b", midSprite = "house_b_walls" },
            new BuildingDef { id = "crates", name = "Entrepôt", stage = 3, w = 2, h = 2, wood = 8, buildTime = 10, sprite = "crates_fish", prosperityPerDay = 1 },
            new BuildingDef { id = "field_1", name = "Champ", stage = 3, w = 4, h = 3, wood = 4, buildTime = 18, sprite = "field", foodPerDay = 6 },
            new BuildingDef { id = "field_2", name = "Champ", stage = 3, w = 4, h = 3, wood = 4, buildTime = 18, sprite = "field", foodPerDay = 6 },

            // ── 4. Village
            new BuildingDef { id = "market", name = "Place du marché", stage = 4, w = 12, h = 8, wood = 30, stone = 14, buildTime = 45, stamp = "market", prosperityPerDay = 6,
                description = "Marchands et musiciens : la colonie prospère." },
            new BuildingDef { id = "farm", name = "Ferme", stage = 4, w = 22, h = 16, wood = 40, stone = 8, buildTime = 60, pop = 4, stamp = "farm", foodPerDay = 16,
                description = "Moulin, grange, champs, vergers, enclos et mare : de quoi nourrir une ville." },
            new BuildingDef { id = "training", name = "Terrain d'entraînement", stage = 4, w = 12, h = 8, wood = 24, stone = 6, buildTime = 35, stamp = "training",
                description = "Les aventuriers s'entraînent : +15 % de dégâts." },
            new BuildingDef { id = "glass", name = "Verrerie", stage = 4, w = 11, h = 7, wood = 22, stone = 12, buildTime = 40, pop = 2, stamp = "glass", prosperityPerDay = 3 },
            new BuildingDef { id = "chapel", name = "Vieille chapelle", stage = 4, w = 12, h = 8, wood = 12, stone = 30, buildTime = 50, stamp = "chapel", prosperityPerDay = 2,
                description = "Une chapelle en ruine relevée par les colons : les blessés y sont soignés." },
            new BuildingDef { id = "stall_fruits", name = "Étal de fruits", stage = 4, w = 4, h = 2, wood = 14, stone = 4, buildTime = 25, sprite = "stall_fruits", prosperityPerDay = 3 },
            new BuildingDef { id = "stall_bread", name = "Boulangerie", stage = 4, w = 3, h = 2, wood = 12, stone = 4, buildTime = 25, sprite = "stall_bread", prosperityPerDay = 3, foodPerDay = 3 },

            // ── 5. Bourg fortifié
            new BuildingDef { id = "house_4", name = "Hall de la Guilde", stage = 5, w = 10, h = 7, wood = 35, stone = 24, buildTime = 55, pop = 4, stamp = "guild", prosperityPerDay = 5,
                description = "La Guilde a pignon sur rue : contrats, primes et classements du donjon." },
            new BuildingDef { id = "tent_magic", name = "Tour des mages", stage = 5, w = 13, h = 10, wood = 16, stone = 30, buildTime = 60, stamp = "magetower", prosperityPerDay = 4,
                description = "Potions : les intrus se soignent plus vite." },
            new BuildingDef { id = "library", name = "Bibliothèque", stage = 5, w = 11, h = 7, wood = 24, stone = 20, buildTime = 50, pop = 1, stamp = "library", prosperityPerDay = 3,
                description = "Bestiaire : les intrus connaissent mieux vos monstres et vos pièges." },
            new BuildingDef { id = "prison", name = "Prison", stage = 5, w = 13, h = 8, wood = 16, stone = 34, buildTime = 55, stamp = "prison",
                description = "Gardes aux portes : vos raids sur la ville sont plus durs." },
            new BuildingDef { id = "stall_weapons", name = "Armurerie", stage = 5, w = 4, h = 3, wood = 20, stone = 12, buildTime = 35, sprite = "stall_weapons", prosperityPerDay = 4,
                description = "Les aventuriers s'équipent : +15 % de PV." },

            // ── 6. Cité du Donjon
            new BuildingDef { id = "manor", name = "Manoir des nobles", stage = 6, w = 17, h = 11, wood = 40, stone = 50, buildTime = 80, pop = 3, stamp = "manor", prosperityPerDay = 8,
                description = "Les nobles financent des héros de rang S." },
            new BuildingDef { id = "tent_round", name = "Pavillon", stage = 6, w = 3, h = 2, wood = 12, buildTime = 20, pop = 2, sprite = "tent_round", prosperityPerDay = 2 },

            // ── Mode Aventurier : la maison du héros (terrain acheté, bâtie sur commande)
            new BuildingDef { id = "hero_home", name = "Maison du héros", stage = -1, w = 14, h = 10, stamp = "hero_home", buildTime = 340, light = true,
                description = "Votre maison : on y dort gratuitement, coffre et armoire." },
        };
        for (int i = 0; i < Homes.Length; i++)
            l.Add(new BuildingDef
            {
                id = "home_" + (i + 1), name = "Maison", stage = Homes[i].stage, w = 4, h = 3, wood = 28, stone = 8, buildTime = 40, pop = 4,
                sprite = Homes[i].sprite, midSprite = Homes[i].sprite == "house_b" ? "house_b_walls" : Homes[i].sprite == "house_a" ? "house_a_walls" : null,
            });
        return l;
    }

    public static BuildingDef Get(string id) => Buildings.Find(b => b.id == id);
}
