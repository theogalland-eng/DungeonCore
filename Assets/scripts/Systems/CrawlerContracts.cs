using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CONTRATS DE LA GUILDE (mode Aventurier) : le maître de guilde propose trois contrats, renouvelés à chaque
/// retour en ville ; on en mène deux à la fois. Chasse (tuer N créatures d'une famille), exploration (atteindre
/// un étage), pillage (ouvrir des coffres), prime (abattre un gardien). La prime se touche au hall : de l'or
/// et un objet d'équipement dont la rareté dépend de la difficulté.
/// </summary>
public static class CrawlerContracts
{
    public enum Kind { Hunt, Reach, Loot, Guardian }

    public class Contract
    {
        public Kind kind;
        public string family;   // chasse
        public int goal, progress, minFloor, gold;
        public CrawlerGear.Rarity reward;
        public bool Done => progress >= goal;

        public string Title
        {
            get
            {
                switch (kind)
                {
                    case Kind.Hunt: return $"Chasse : {goal} {FamilyPlural(family)}";
                    case Kind.Reach: return $"Exploration : atteindre l'étage {goal}";
                    case Kind.Loot: return $"Pillage : {goal} coffres (étage {minFloor} ou plus)";
                    default: return $"Prime : abattre le gardien de l'étage {minFloor}";
                }
            }
        }

        public string Progress => kind == Kind.Reach || kind == Kind.Guardian ? (Done ? "fait" : "à faire") : $"{Mathf.Min(progress, goal)}/{goal}";
        public string Reward => $"{gold} or + objet <color=#{ColorUtility.ToHtmlStringRGB(CrawlerGear.RarityColor(reward))}>{CrawlerGear.RarityNames[(int)reward].ToLower()}</color>";
    }

    public const int MaxActive = 2;
    public static readonly List<Contract> Offered = new List<Contract>();
    public static readonly List<Contract> Active = new List<Contract>();

    private static readonly Dictionary<string, string> Plurals = new Dictionary<string, string>
    {
        { "rat", "rats" }, { "slime", "slimes" }, { "goblin", "gobelins" }, { "mushroom", "champignons" }, { "orc", "orcs" },
        { "skeleton", "squelettes" }, { "zombie", "zombies" }, { "gnoll", "gnolls" }, { "imp", "diablotins" }, { "ghost", "spectres" },
        { "lizard", "hommes-lézards" }, { "golem", "golems" }, { "lich", "liches" }, { "vampire", "vampires" }, { "demon", "démons" },
    };
    public static string FamilyPlural(string id) => id != null && Plurals.TryGetValue(id, out var p) ? p : id;

    public static void Reset() { Offered.Clear(); Active.Clear(); }

    /// <summary>Trois nouveaux contrats, adaptés à l'étage le plus profond atteint.</summary>
    public static void Roll(int deepest, System.Random rng)
    {
        Offered.Clear();
        int depth = Mathf.Clamp(deepest, 1, CrawlerDungeon.FloorCount);
        var kinds = new List<Kind> { Kind.Hunt, Kind.Hunt, Kind.Reach, Kind.Loot, Kind.Guardian };
        while (Offered.Count < 3 && kinds.Count > 0)
        {
            var k = kinds[rng.Next(kinds.Count)];
            kinds.Remove(k);
            var c = new Contract { kind = k };
            switch (k)
            {
                case Kind.Hunt:
                {
                    int fl = Mathf.Clamp(depth - rng.Next(0, 2), 1, CrawlerDungeon.FloorCount);
                    var fams = CrawlerDungeon.FamiliesOn(fl);
                    c.family = fams[rng.Next(fams.Length)];
                    if (Offered.Exists(o => o.kind == Kind.Hunt && o.family == c.family)) continue;
                    c.goal = 4 + rng.Next(0, 4) + fl / 2;
                    c.minFloor = fl;
                    c.gold = 40 + 22 * fl + 6 * c.goal;
                    c.reward = fl >= 6 ? CrawlerGear.Rarity.Rare : CrawlerGear.Rarity.Uncommon;
                    break;
                }
                case Kind.Reach:
                    if (depth >= CrawlerDungeon.FloorCount) continue;
                    c.goal = Mathf.Min(CrawlerDungeon.FloorCount, depth + 1 + rng.Next(0, 2));
                    c.minFloor = c.goal;
                    c.gold = 60 + 35 * c.goal;
                    c.reward = c.goal >= 7 ? CrawlerGear.Rarity.Epic : CrawlerGear.Rarity.Rare;
                    break;
                case Kind.Loot:
                    c.minFloor = Mathf.Max(1, depth - 1);
                    c.goal = 2 + rng.Next(0, 2);
                    c.gold = 50 + 25 * c.minFloor;
                    c.reward = CrawlerGear.Rarity.Rare;
                    break;
                default:
                {
                    // le prochain gardien (5, 8, 10) que l'on n'a pas encore dépassé de loin
                    int g = depth < 6 ? 5 : depth < 9 ? 8 : 10;
                    c.minFloor = g;
                    c.goal = 1;
                    c.gold = 150 + 40 * g;
                    c.reward = g >= 8 ? CrawlerGear.Rarity.Legendary : CrawlerGear.Rarity.Epic;
                    break;
                }
            }
            Offered.Add(c);
        }
    }

    public static bool Accept(Contract c)
    {
        if (Active.Count >= MaxActive) { GameHUD.Toast($"Deux contrats à la fois, pas plus", GameHUD.WarningColor); return false; }
        Offered.Remove(c);
        Active.Add(c);
        if (c.kind == Kind.Reach && CrawlerMode.Instance != null && CrawlerMode.Instance.deepest >= c.goal) c.progress = 1;
        Sfx.Play("coin", null, 0.6f);
        GameHUD.Toast($"Contrat accepté : {c.Title}", GameHUD.InfoColor);
        return true;
    }

    // ── Avancement (appelé par CrawlerMode)
    public static void OnKill(string family, int floor, bool guardian)
    {
        foreach (var c in Active)
        {
            if (c.Done) continue;
            if (c.kind == Kind.Hunt && c.family == family) Step(c);
            else if (c.kind == Kind.Guardian && guardian && floor == c.minFloor) Step(c);
        }
    }

    public static void OnFloor(int floor)
    {
        foreach (var c in Active) if (!c.Done && c.kind == Kind.Reach && floor >= c.goal) Step(c);
    }

    public static void OnChest(int floor)
    {
        foreach (var c in Active) if (!c.Done && c.kind == Kind.Loot && floor >= c.minFloor) Step(c);
    }

    private static void Step(Contract c)
    {
        c.progress++;
        if (c.Done)
        {
            GameHUD.Toast($"Contrat rempli : {c.Title} — touchez la prime au hall de la Guilde", GameHUD.GoldColor);
            Sfx.Play("shine", null, 0.7f);
        }
    }
}
