using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ARBRE DE TALENTS DU NOYAU : un point par niveau gagné, à dépenser dans trois branches de cinq talents
/// (chaque talent demande les précédents de sa branche) :
///   - ARCHITECTE : creuser, pièges, portes, entretien, prestige ;
///   - MAÎTRE DES BÊTES : invocations, robustesse, faim, nombre, évolutions ;
///   - OMBRE : peur, obscurité, drain de mana, embuscades, torches qui meurent.
/// Les effets sont lus par les systèmes concernés (multiplicateurs ci-dessous). Sauvegardés avec la partie.
/// </summary>
public static class Talents
{
    public enum Branch { Architect, Beastmaster, Shadow }

    public class Def
    {
        public string id, name, desc;
        public Branch branch;
        public int tier;   // 1 à 5
    }

    public static readonly string[] BranchNames = { "Architecte", "Maître des bêtes", "Ombre" };
    public static readonly string[] BranchIcons = { "house", "people", "skull" };

    public static readonly List<Def> All = new List<Def>
    {
        new Def { id = "a1", branch = Branch.Architect, tier = 1, name = "Maçon des abîmes", desc = "Creuser coûte 30 % de mana en moins." },
        new Def { id = "a2", branch = Branch.Architect, tier = 2, name = "Ingénieur", desc = "+3 pièges au maximum." },
        new Def { id = "a3", branch = Branch.Architect, tier = 3, name = "Économe", desc = "Entretien des pièges et décors -25 %." },
        new Def { id = "a4", branch = Branch.Architect, tier = 4, name = "Mécanismes retors", desc = "Portes 50 % plus longues à forcer ; plaques de pression deux fois plus cruelles." },
        new Def { id = "a5", branch = Branch.Architect, tier = 5, name = "Cathédrale des ténèbres", desc = "Prestige du donjon ×1,5." },

        new Def { id = "b1", branch = Branch.Beastmaster, tier = 1, name = "Invocateur", desc = "Invoquer un monstre coûte 15 % de mana en moins." },
        new Def { id = "b2", branch = Branch.Beastmaster, tier = 2, name = "Chair robuste", desc = "Vos monstres ont 15 % de PV en plus." },
        new Def { id = "b3", branch = Branch.Beastmaster, tier = 3, name = "Ventres pleins", desc = "La faim de vos monstres monte 30 % moins vite." },
        new Def { id = "b4", branch = Branch.Beastmaster, tier = 4, name = "Horde", desc = "+3 monstres au maximum." },
        new Def { id = "b5", branch = Branch.Beastmaster, tier = 5, name = "Sang ancien", desc = "Évolutions -25 % de mana, expérience des monstres +25 %." },

        new Def { id = "c1", branch = Branch.Shadow, tier = 1, name = "Murmures", desc = "Les intrus ont 10 % de chances en plus de fuir." },
        new Def { id = "c2", branch = Branch.Shadow, tier = 2, name = "Ténèbres", desc = "Dans le donjon, les intrus voient 15 % moins loin." },
        new Def { id = "c3", branch = Branch.Shadow, tier = 3, name = "Soif du cristal", desc = "Drain de mana des intrus +25 %." },
        new Def { id = "c4", branch = Branch.Shadow, tier = 4, name = "Tapis dans l'ombre", desc = "Vos monstres postés s'embusquent en 2 s, et leur premier coup fait double dégâts." },
        new Def { id = "c5", branch = Branch.Shadow, tier = 5, name = "Torches mourantes", desc = "Les torches des intrus s'éteignent après 40 s dans le donjon." },
    };

    private static readonly HashSet<string> owned = new HashSet<string>();
    public static event System.Action OnChanged;

    public static bool Has(string id) => owned.Contains(id);
    public static int Spent => owned.Count;
    public static int Earned => PlayerLevel.Instance != null ? Mathf.Max(0, PlayerLevel.Instance.currentLevel - 1) : 0;
    public static int Points => Mathf.Max(0, Earned - Spent);

    public static int OwnedIn(Branch b)
    {
        int n = 0;
        foreach (var d in All) if (d.branch == b && owned.Contains(d.id)) n++;
        return n;
    }

    public static bool CanBuy(Def d, out string why)
    {
        why = null;
        if (owned.Contains(d.id)) { why = "Acquis"; return false; }
        if (OwnedIn(d.branch) < d.tier - 1) { why = "Talent précédent requis"; return false; }
        if (Points <= 0) { why = "Aucun point (un par niveau)"; return false; }
        return true;
    }

    public static bool Buy(Def d)
    {
        if (!CanBuy(d, out string why)) { GameHUD.Toast(why, GameHUD.WarningColor); return false; }
        owned.Add(d.id);
        // effets immédiats
        if (d.id == "b2")
            foreach (var g in GobelinAI.All) if (g != null && g.IsAlive()) { g.maxHealth *= 1.15f; g.currentHealth *= 1.15f; }
        GameHUD.Toast($"Talent acquis : {d.name}", GameHUD.GoldColor);
        Sfx.Play("shine", null, 0.8f);
        OnChanged?.Invoke();
        return true;
    }

    public static void Reset() { owned.Clear(); OnChanged?.Invoke(); }
    public static void Capture(List<string> into) { into.Clear(); into.AddRange(owned); }
    public static void Restore(List<string> from) { owned.Clear(); if (from != null) foreach (var s in from) owned.Add(s); OnChanged?.Invoke(); }

    // ── Effets lus par les systèmes ──
    public static float DigCostMult => Has("a1") ? 0.7f : 1f;
    public static int TrapLimitBonus => Has("a2") ? 3 : 0;
    public static float ObjectUpkeepMult => Has("a3") ? 0.75f : 1f;
    public static float DoorHoldMult => Has("a4") ? 1.5f : 1f;
    public static float PlateDamageMult => Has("a4") ? 2f : 1f;
    public static float PrestigeMult => Has("a5") ? 1.5f : 1f;
    public static float SummonCostMult => Has("b1") ? 0.85f : 1f;
    public static float MonsterHpMult => Has("b2") ? 1.15f : 1f;
    public static float MonsterHungerMult => Has("b3") ? 0.7f : 1f;
    public static int MonsterLimitBonus => Has("b4") ? 3 : 0;
    public static float EvolveCostMult => Has("b5") ? 0.75f : 1f;
    public static float MonsterXpMult => Has("b5") ? 1.25f : 1f;
    public static float IntruderFleeBonus => Has("c1") ? 0.1f : 0f;
    public static float IntruderSightMult => Has("c2") ? 0.85f : 1f;
    public static float ManaDrainMult => Has("c3") ? 1.25f : 1f;
    public static float AmbushDelay => Has("c4") ? 2f : 5f;
    public static float AmbushMult => Has("c4") ? 2f : 1.5f;
    public static bool TorchesDie => Has("c5");
}
