using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CHRONIQUES DE L'ÎLE : le journal des moments marquants, jour après jour — morts des intrus (avec leur caractère
/// et ce qui les a tués), bagarres fatales, Champion, couples et désertions de monstres, évolutions, colonie, saisons...
/// Consultable depuis le menu pause ; sauvegardé avec la partie (les 150 dernières entrées).
/// </summary>
public static class Chronicle
{
    public struct Entry { public int day; public string text; }

    private static readonly List<Entry> entries = new List<Entry>();
    public static IReadOnlyList<Entry> Entries => entries;
    private const int Max = 150;

    public static void Add(string text)
    {
        if (CrawlerMode.Active || string.IsNullOrEmpty(text)) return;
        int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        entries.Add(new Entry { day = day, text = text });
        if (entries.Count > Max) entries.RemoveAt(0);
    }

    public static void Reset() => entries.Clear();

    public static void Capture(List<string> into)
    {
        into.Clear();
        foreach (var e in entries) into.Add(e.day + "|" + e.text);
    }

    public static void Restore(List<string> from)
    {
        entries.Clear();
        if (from == null) return;
        foreach (var s in from)
        {
            int i = s.IndexOf('|');
            if (i <= 0 || !int.TryParse(s.Substring(0, i), out int d)) continue;
            entries.Add(new Entry { day = d, text = s.Substring(i + 1) });
        }
    }

    /// <summary>« Elise la Cupide » : le nom de l'intrus suivi de son trait le plus parlant (s'il est connu).</summary>
    public static string Who(AdventurerAI a)
    {
        if (a == null) return "un intrus";
        string n = a.combat != null ? a.combat.adventurerName.Split('·')[0].Trim() : a.name;
        if (a.Life != null)
            foreach (var t in a.Life.traits)
                if (a.Life.revealed.Contains(t)) return $"{n} <i>({AdventurerLife.TraitName(t).ToLowerInvariant()})</i>";
        return n;
    }
}
