using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VOIX NON VERBALES : cris des monstres selon leur famille (gobelins qui jacassent, orcs qui grondent, rats qui
/// couinent, morts-vivants qui gémissent...) et des intrus (homme / femme) quand ils repèrent un ennemi, sont blessés,
/// meurent ou paniquent. Sons « vx_voix_événement_n » (Tools~/make_voices.py, remplaçables par de vrais enregistrements).
/// Chaque individu a sa hauteur de voix (les grands et les évolués plus graves) et ne crie pas sans arrêt.
/// </summary>
public static class Voices
{
    private static readonly Dictionary<int, float> nextCry = new Dictionary<int, float>();

    private static readonly Dictionary<string, string> FamilyVoice = new Dictionary<string, string>
    {
        { "goblin", "goblin" }, { "orc", "orc" }, { "rat", "rat" }, { "gnoll", "beast" },
        { "skeleton", "undead" }, { "zombie", "undead" }, { "ghost", "undead" }, { "lich", "undead" }, { "vampire", "undead" },
        { "imp", "imp" }, { "demon", "demon" }, { "slime", "slime" }, { "mushroom", "slime" }, { "lizard", "lizard" }, { "golem", "golem" },
        { "spider", "lizard" }, { "mimic", "demon" }, { "bat", "rat" },
    };

    private static readonly HashSet<string> Feminine = new HashSet<string>
    {
        "Élise", "Elise", "Maëlle", "Solène", "Iris", "Ophélie", "Aude", "Clémence", "Lise", "Anaïs", "Bérénice", "Zora", "Ada", "Brune", "Cyra", "Noa",
        "Brunhild", "Dagna", "Fenna", "Hilde", "Isolde", "Kaela", "Maelis", "Orla", "Roswitha", "Talia", "Vesna", "Yseult",
        "Soigneuse", "Prêtresse", "Archère", "Guerrière", "Aventurière",
    };

    /// <summary>Voix d'une famille de monstres (null : muette, on garde les bruitages génériques).</summary>
    public static string VoiceOf(string family) => family != null && FamilyVoice.TryGetValue(family, out var v) ? v : null;

    private static bool Ready(Object o, float cooldown)
    {
        int id = o.GetInstanceID();
        float now = Time.time;
        if (nextCry.TryGetValue(id, out float t) && now < t) return false;
        nextCry[id] = now + cooldown;
        return true;
    }

    /// <summary>Cri d'un monstre : "aggro", "hurt" ou "death". Renvoie false si la famille n'a pas de voix.</summary>
    public static bool Monster(GobelinAI g, string ev, float chance = 1f)
    {
        if (g == null) return false;
        var u = g.GetComponent<MonsterUnit>();
        string v = VoiceOf(u != null ? u.familyId : null);
        if (v == null) return false;
        if (ev != "death" && (Random.value > chance || !Ready(g, ev == "hurt" ? 1.8f : 6f))) return true;
        int stage = u != null ? u.stage : 1;
        // hauteur propre à l'individu, plus grave quand il évolue ou grandit
        float pitch = (0.92f + (Mathf.Abs(g.GetInstanceID()) % 17) / 17f * 0.18f) * (1.08f - 0.07f * stage) / Mathf.Clamp(g.transform.localScale.x, 0.8f, 1.6f);
        Sfx.Play($"vx_{v}_{ev}", g.transform.position, ev == "hurt" ? 0.6f : 0.85f, 0.04f, 0.12f, pitch);
        return true;
    }

    public static bool IsFemale(AdventurerCombat c)
    {
        if (c == null) return false;
        if (c.voice >= 0) return c.voice == 1;
        bool f = c.role == AdventurerAI.Role.Healer;
        if (!f && !string.IsNullOrEmpty(c.adventurerName))
            foreach (var w in c.adventurerName.Split(' ', ',', '«', '»'))
                if (Feminine.Contains(w)) { f = true; break; }
        c.voice = f ? 1 : 0;
        return f;
    }

    /// <summary>Cri d'un intrus, d'un compagnon ou du héros : "aggro", "hurt", "death" ou "fear".</summary>
    public static void Human(AdventurerCombat c, string ev, float chance = 1f)
    {
        if (c == null) return;
        if (ev != "death" && (Random.value > chance || !Ready(c, ev == "hurt" ? 1.5f : 8f))) return;
        string who = IsFemale(c) ? "woman" : "man";
        float pitch = 0.94f + (Mathf.Abs(c.GetInstanceID()) % 13) / 13f * 0.14f;
        Sfx.Play($"vx_{who}_{ev}", c.transform.position, ev == "hurt" ? 0.55f : 0.8f, 0.03f, 0.1f, pitch);
    }
}
