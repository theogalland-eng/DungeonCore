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
    public static readonly string[] StageNames = { "Aucune", "Camp de la Guilde", "Port", "Hameau", "Village", "Ville du donjon" };

    /// <summary>Prospérité nécessaire pour passer au stade (index = stade visé).</summary>
    public static readonly int[] StageProsperity = { 0, 0, 170, 430, 820, 1350 };

    /// <summary>Les chantiers prennent du temps : multiplicateur des durées de construction.</summary>
    public const float BuildTimeScale = 1.6f;

    /// <summary>Conditions de fondation (jours comptés depuis le classement du donjon par la Guilde) : le donjon doit avoir fait parler de lui.</summary>
    public const int FoundMinDay = 3;
    public const int FoundSurvivors = 3;
    public const float FoundRenown = 15f;
    /// <summary>Au plus tard, la Guilde s'installe ce nombre de jours après le classement.</summary>
    public const int FoundLatestDay = 9;

    public static readonly List<BuildingDef> Buildings = new List<BuildingDef>
    {
        // ── 1. Campement
        new BuildingDef { id = "campfire", name = "Feu de camp", stage = 1, w = 2, h = 2, wood = 6, buildTime = 10, sprite = "campfire_pot", light = true,
            description = "Les expéditions s'y reposent et s'y soignent." },
        new BuildingDef { id = "tent_a", name = "Tente", stage = 1, w = 3, h = 2, wood = 10, buildTime = 15, pop = 2, sprite = "tent_white" },
        new BuildingDef { id = "tent_b", name = "Tente", stage = 1, w = 3, h = 2, wood = 10, buildTime = 15, pop = 2, sprite = "tent_green" },
        new BuildingDef { id = "stockpile", name = "Réserve", stage = 1, w = 2, h = 2, wood = 6, buildTime = 8, sprite = "crate_stack" },

        // ── 2. Port
        new BuildingDef { id = "pier", name = "Ponton", stage = 2, w = 0, h = 0, wood = 20, buildTime = 25,
            description = "Plus grand ponton : de plus gros navires accostent." },
        new BuildingDef { id = "fisher_hut", name = "Cabane de pêcheur", stage = 2, w = 4, h = 3, wood = 25, stone = 4, buildTime = 35, pop = 3,
            sprite = "house_b", midSprite = "house_b_walls", foodPerDay = 4 },
        new BuildingDef { id = "fish_rack", name = "Séchoir à poissons", stage = 2, w = 3, h = 1, wood = 6, buildTime = 10, sprite = "fish_rack", foodPerDay = 3 },
        new BuildingDef { id = "tent_c", name = "Tente", stage = 2, w = 3, h = 2, wood = 10, buildTime = 15, pop = 2, sprite = "tent_red" },

        // ── 3. Hameau
        new BuildingDef { id = "house_1", name = "Taverne", stage = 3, w = 4, h = 3, wood = 34, stone = 10, buildTime = 45, pop = 3, sprite = "bld_tavern", light = true, prosperityPerDay = 3,
            description = "On y boit, on y raconte le donjon : les rumeurs y circulent." },
        new BuildingDef { id = "house_2", name = "Maison", stage = 3, w = 4, h = 3, wood = 30, stone = 8, buildTime = 40, pop = 4, sprite = "house_b", midSprite = "house_b_walls" },
        new BuildingDef { id = "crates", name = "Entrepôt", stage = 3, w = 2, h = 2, wood = 8, buildTime = 10, sprite = "crates_fish", prosperityPerDay = 1 },

        // ── 4. Village
        new BuildingDef { id = "field_1", name = "Champ", stage = 4, w = 4, h = 3, wood = 4, buildTime = 18, sprite = "field", foodPerDay = 6 },
        new BuildingDef { id = "field_2", name = "Champ", stage = 4, w = 4, h = 3, wood = 4, buildTime = 18, sprite = "field", foodPerDay = 6 },
        new BuildingDef { id = "stall_fruits", name = "Étal de fruits", stage = 4, w = 4, h = 2, wood = 14, stone = 4, buildTime = 25, sprite = "stall_fruits", prosperityPerDay = 3 },
        new BuildingDef { id = "stall_bread", name = "Boulangerie", stage = 4, w = 3, h = 2, wood = 12, stone = 4, buildTime = 25, sprite = "stall_bread", prosperityPerDay = 3, foodPerDay = 3 },
        new BuildingDef { id = "house_3", name = "Forge", stage = 4, w = 4, h = 3, wood = 30, stone = 16, buildTime = 45, pop = 2, sprite = "bld_forge", light = true, prosperityPerDay = 2,
            description = "Le forgeron répare les armures : les aventuriers repartent mieux équipés." },

        // ── 5. Ville
        new BuildingDef { id = "stall_weapons", name = "Armurerie", stage = 5, w = 4, h = 3, wood = 20, stone = 12, buildTime = 35, sprite = "stall_weapons", prosperityPerDay = 4,
            description = "Les aventuriers s'équipent : +15 % de PV." },
        new BuildingDef { id = "tent_magic", name = "Tour des mages", stage = 5, w = 3, h = 3, wood = 16, stone = 18, buildTime = 50, sprite = "bld_magetower", prosperityPerDay = 4,
            description = "Potions : les intrus se soignent plus vite." },
        new BuildingDef { id = "training", name = "Terrain d'entraînement", stage = 5, w = 4, h = 2, wood = 18, stone = 6, buildTime = 30, sprite = "dummy",
            description = "Les aventuriers s'entraînent : +15 % de dégâts." },
        new BuildingDef { id = "house_4", name = "Hall de la Guilde", stage = 5, w = 4, h = 3, wood = 35, stone = 20, buildTime = 55, pop = 4, sprite = "bld_guild", prosperityPerDay = 5,
            description = "La Guilde a pignon sur rue : contrats, primes et classements du donjon." },
        new BuildingDef { id = "tent_round", name = "Pavillon", stage = 5, w = 3, h = 2, wood = 12, buildTime = 20, pop = 2, sprite = "tent_round", prosperityPerDay = 2 },
    };

    public static BuildingDef Get(string id) => Buildings.Find(b => b.id == id);
}
