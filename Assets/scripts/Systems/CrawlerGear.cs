using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ÉQUIPEMENT DU MODE AVENTURIER : trois emplacements (arme, armure, bijou) et cinq raretés
/// (commun, peu commun, rare, épique, légendaire). Chaque objet a une qualité principale selon
/// l'emplacement et 0 à 3 qualités secondaires selon la rareté ; plus l'étage est profond, plus il est fort.
/// Butin : monstres (rarement), coffres (souvent), gardiens (toujours, rare au moins), primes des contrats.
/// </summary>
public static class CrawlerGear
{
    public enum Slot { Weapon, Armor, Trinket }
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
    public enum Stat { Damage, Health, Crit, Speed, Stamina, Dodge }

    public class Item
    {
        public string name;
        public Slot slot;
        public Rarity rarity;
        public int level;   // étage d'origine
        public readonly Dictionary<Stat, float> stats = new Dictionary<Stat, float>();

        public float Get(Stat s) => stats.TryGetValue(s, out float v) ? v : 0f;
        public int Value => Mathf.RoundToInt((12 + 9 * level) * RarityValue[(int)rarity]);
        public string ColoredName => $"<color=#{ColorUtility.ToHtmlStringRGB(RarityColor(rarity))}>{name}</color>";

        public string StatLine(Item compare = null)
        {
            var parts = new List<string>();
            foreach (Stat s in System.Enum.GetValues(typeof(Stat)))
            {
                float v = Get(s), c = compare != null ? compare.Get(s) : v;
                if (v <= 0f && c <= 0f) continue;
                string txt = $"+{Mathf.RoundToInt(v * 100)} % {StatNames[(int)s]}";
                if (compare != null && Mathf.Abs(v - c) > 0.004f)
                    txt += v > c ? $" <color=#9CF0A8>(+{Mathf.RoundToInt((v - c) * 100)})</color>" : $" <color=#FF8A7A>({Mathf.RoundToInt((v - c) * 100)})</color>";
                if (v <= 0f) txt = $"<color=#8E89A3>{StatNames[(int)s]}</color> <color=#FF8A7A>(-{Mathf.RoundToInt(c * 100)})</color>";
                parts.Add(txt);
            }
            return string.Join("  ·  ", parts);
        }
    }

    public static readonly string[] SlotNames = { "Arme", "Armure", "Bijou" };
    public static readonly string[] RarityNames = { "Commun", "Peu commun", "Rare", "Épique", "Légendaire" };
    public static readonly string[] StatNames = { "dégâts", "PV", "critique", "vitesse", "endurance", "esquive" };
    private static readonly float[] RarityPower = { 1f, 1.25f, 1.55f, 1.9f, 2.4f };
    private static readonly float[] RarityValue = { 1f, 1.8f, 3.2f, 5.5f, 9f };
    private static readonly int[] RarityAffixes = { 0, 1, 2, 3, 3 };

    public static Color RarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Uncommon: return new Color(0.45f, 0.9f, 0.45f);
            case Rarity.Rare: return new Color(0.4f, 0.65f, 1f);
            case Rarity.Epic: return new Color(0.78f, 0.45f, 1f);
            case Rarity.Legendary: return new Color(1f, 0.75f, 0.25f);
            default: return new Color(0.85f, 0.83f, 0.8f);
        }
    }

    private static readonly string[][] WeaponBases =
    {
        new[] { "Épée", "Hache d'armes", "Masse", "Fauchon" },
        new[] { "Arc", "Arc long", "Arbalète", "Arc de corne" },
        new[] { "Bâton", "Baguette", "Grimoire", "Orbe" },
        new[] { "Sceptre", "Masse bénie", "Bâton de pèlerin", "Encensoir" },
    };
    private static readonly string[][] ArmorBases =
    {
        new[] { "Cotte de mailles", "Plastron", "Harnois" },
        new[] { "Cuir clouté", "Brigandine", "Mantelet" },
        new[] { "Robe", "Mante", "Tunique runique" },
        new[] { "Aube", "Cotte bénie", "Manteau de pèlerin" },
    };
    private static readonly string[] TrinketBases = { "Anneau", "Amulette", "Talisman", "Broche", "Médaillon" };
    private static readonly string[] Suffix = { "du Loup", "de l'Ours", "de la Vipère", "du Renard", "du Marcheur", "de l'Ombre" };
    private static readonly string[] Legendary =
    {
        "Croc-de-Braise", "Murmure de Lune", "Larme du Noyau", "Dernier Serment", "Cœur de Givre", "Chant des Abysses", "Aube Écarlate", "Veille-Mort",
    };

    /// <summary>Rareté tirée selon l'étage (et un plancher éventuel : gardien, prime).</summary>
    public static Rarity RollRarity(int floor, System.Random rng, Rarity min = Rarity.Common)
    {
        double r = rng.NextDouble() * (1.0 - floor * 0.035);
        Rarity got = r < 0.03 ? Rarity.Legendary : r < 0.12 ? Rarity.Epic : r < 0.32 ? Rarity.Rare : r < 0.62 ? Rarity.Uncommon : Rarity.Common;
        return got < min ? min : got;
    }

    public static Item Roll(int floor, AdventurerAI.Role role, System.Random rng, Rarity min = Rarity.Common, Slot? forceSlot = null)
    {
        floor = Mathf.Clamp(floor, 1, CrawlerDungeon.FloorCount);
        var it = new Item
        {
            slot = forceSlot ?? (Slot)rng.Next(3),
            rarity = RollRarity(floor, rng, min),
            level = floor,
        };
        float power = RarityPower[(int)it.rarity];
        // qualité principale
        Stat main;
        float mainVal;
        switch (it.slot)
        {
            case Slot.Weapon: main = Stat.Damage; mainVal = (0.08f + 0.03f * floor) * power; break;
            case Slot.Armor: main = Stat.Health; mainVal = (0.1f + 0.035f * floor) * power; break;
            default:
                main = new[] { Stat.Crit, Stat.Speed, Stat.Stamina, Stat.Dodge }[rng.Next(4)];
                mainVal = SecondaryValue(main, floor) * power * 1.3f;
                break;
        }
        it.stats[main] = mainVal;
        // qualités secondaires
        Stat best = main;
        float bestVal = 0f;
        for (int i = 0; i < RarityAffixes[(int)it.rarity]; i++)
        {
            var s = (Stat)rng.Next(6);
            float v = SecondaryValue(s, floor) * (0.7f + (float)rng.NextDouble() * 0.6f) * (it.rarity == Rarity.Legendary ? 1.4f : 1f);
            it.stats[s] = it.Get(s) + v;
            if (s != main && v > bestVal) { bestVal = v; best = s; }
        }
        // nom
        int ri = Mathf.Clamp((int)role, 0, 3);
        string bas = it.slot == Slot.Weapon ? WeaponBases[ri][rng.Next(WeaponBases[ri].Length)]
                   : it.slot == Slot.Armor ? ArmorBases[ri][rng.Next(ArmorBases[ri].Length)]
                   : TrinketBases[rng.Next(TrinketBases.Length)];
        if (it.rarity == Rarity.Legendary) it.name = $"{bas} « {Legendary[rng.Next(Legendary.Length)]} »";
        else if (it.rarity == Rarity.Common) it.name = bas;
        else it.name = $"{bas} {Suffix[(int)(best == main && it.slot != Slot.Trinket ? main : best)]}";
        return it;
    }

    private static float SecondaryValue(Stat s, int floor)
    {
        switch (s)
        {
            case Stat.Damage: return 0.04f + 0.012f * floor;
            case Stat.Health: return 0.05f + 0.015f * floor;
            case Stat.Crit: return 0.03f + 0.006f * floor;
            case Stat.Speed: return 0.03f + 0.004f * floor;
            case Stat.Stamina: return 0.08f + 0.015f * floor;
            default: return 0.025f + 0.005f * floor;   // esquive
        }
    }
}
