using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ARBRES DE COMPÉTENCES DES CLASSES (mode Aventurier) : trois voies de trois talents par classe, de coût 1, 2 et 3
/// (chaque talent demande le précédent de sa voie). Points : un par nouvel étage atteint, un par rang validé
/// à la Guilde. Les effets sont des clés additionnées (dégâts, PV, critique, puissance de la compétence...)
/// lues par CrawlerMode et CrawlerPlayer.
/// </summary>
public static class CrawlerSkills
{
    public class Node
    {
        public string id, name, desc;
        public int branch, tier;   // tier 1..3 = coût
        public Dictionary<string, float> fx;
    }

    public class Tree
    {
        public string[] branches;
        public List<Node> nodes = new List<Node>();
    }

    private static Node N(string id, int branch, int tier, string name, string desc, params (string, float)[] fx)
    {
        var n = new Node { id = id, branch = branch, tier = tier, name = name, desc = desc, fx = new Dictionary<string, float>() };
        foreach (var (k, v) in fx) n.fx[k] = v;
        return n;
    }

    private static readonly Tree[] Trees =
    {
        new Tree   // Guerrier
        {
            branches = new[] { "Rempart", "Furie", "Tourbillon" },
            nodes =
            {
                N("w1", 0, 1, "Peau de fer", "Dégâts subis -10 %.", ("taken", 0.10f)),
                N("w2", 0, 2, "Endurci", "PV +20 %.", ("hp", 0.20f)),
                N("w3", 0, 3, "Inébranlable", "Dégâts subis encore -15 %.", ("taken", 0.15f)),
                N("w4", 1, 1, "Tranchant", "Dégâts +12 %.", ("dmg", 0.12f)),
                N("w5", 1, 2, "Rage", "Sous la moitié de vos PV, dégâts +30 %.", ("rage", 0.30f)),
                N("w6", 1, 3, "Soif de sang", "Chaque coup vous rend 8 % des dégâts infligés.", ("lifesteal", 0.08f)),
                N("w7", 2, 1, "Large lame", "Rayon du Tourbillon +35 %.", ("skillRad", 0.35f)),
                N("w8", 2, 2, "Élan", "Recharge de la compétence -25 %.", ("skillCd", 0.25f)),
                N("w9", 2, 3, "Tempête d'acier", "Puissance du Tourbillon +60 %.", ("skillPow", 0.60f)),
            },
        },
        new Tree   // Archer
        {
            branches = new[] { "Œil de faucon", "Pas léger", "Volée" },
            nodes =
            {
                N("a1", 0, 1, "Visée", "Critique +8 %.", ("crit", 0.08f)),
                N("a2", 0, 2, "Tir précis", "Dégâts +15 %.", ("dmg", 0.15f)),
                N("a3", 0, 3, "Mortel", "Critique +12 %.", ("crit", 0.12f)),
                N("a4", 1, 1, "Agile", "Vitesse +10 %.", ("speed", 0.10f)),
                N("a5", 1, 2, "Insaisissable", "Esquive +10 %.", ("dodge", 0.10f)),
                N("a6", 1, 3, "Second souffle", "Endurance récupérée +40 %.", ("stamina", 0.40f)),
                N("a7", 2, 1, "Carquois garni", "La Volée tire deux flèches de plus.", ("arrows", 2f)),
                N("a8", 2, 2, "Réflexes", "Recharge de la compétence -25 %.", ("skillCd", 0.25f)),
                N("a9", 2, 3, "Pluie de flèches", "Puissance de la Volée +60 %, portée +30 %.", ("skillPow", 0.60f), ("skillRad", 0.30f)),
            },
        },
        new Tree   // Mage
        {
            branches = new[] { "Pyromancie", "Arcanes", "Nova" },
            nodes =
            {
                N("m1", 0, 1, "Braises", "Dégâts +15 %.", ("dmg", 0.15f)),
                N("m2", 0, 2, "Embrasement", "Explosion des boules de feu +40 % de rayon.", ("splash", 0.40f)),
                N("m3", 0, 3, "Fournaise", "Dégâts +20 %.", ("dmg", 0.20f)),
                N("m4", 1, 1, "Afflux", "Recharge de la compétence -20 %.", ("skillCd", 0.20f)),
                N("m5", 1, 2, "Concentration", "Endurance récupérée +30 %.", ("stamina", 0.30f)),
                N("m6", 1, 3, "Bouclier de mana", "Dégâts subis -20 %.", ("taken", 0.20f)),
                N("m7", 2, 1, "Onde", "Rayon de la Nova +35 %.", ("skillRad", 0.35f)),
                N("m8", 2, 2, "Cataclysme", "Puissance de la Nova +60 %.", ("skillPow", 0.60f)),
                N("m9", 2, 3, "Écho", "Recharge de la compétence -25 %.", ("skillCd", 0.25f)),
            },
        },
        new Tree   // Soigneuse
        {
            branches = new[] { "Foi", "Prière", "Châtiment" },
            nodes =
            {
                N("h1", 0, 1, "Grâce", "Les potions soignent 50 % de plus.", ("potion", 0.50f)),
                N("h2", 0, 2, "Bénédiction", "PV +20 %.", ("hp", 0.20f)),
                N("h3", 0, 3, "Bouclier de foi", "Dégâts subis -20 %.", ("taken", 0.20f)),
                N("h4", 1, 1, "Ferveur", "La Prière soigne 40 % de plus.", ("skillPow", 0.40f)),
                N("h5", 1, 2, "Communion", "La Prière soigne aussi tout le groupe.", ("partyHeal", 1f)),
                N("h6", 1, 3, "Psaume", "Recharge de la compétence -30 %.", ("skillCd", 0.30f)),
                N("h7", 2, 1, "Marteau", "Dégâts +15 %.", ("dmg", 0.15f)),
                N("h8", 2, 2, "Justice", "Chaque coup vous rend 10 % des dégâts infligés.", ("lifesteal", 0.10f)),
                N("h9", 2, 3, "Jugement", "Critique +10 %.", ("crit", 0.10f)),
            },
        },
    };

    private static readonly HashSet<string> owned = new HashSet<string>();

    public static Tree For(AdventurerAI.Role role) => Trees[Mathf.Clamp((int)role, 0, Trees.Length - 1)];
    public static bool Has(string id) => owned.Contains(id);
    public static void Reset() => owned.Clear();

    public static int Earned
    {
        get
        {
            var cm = CrawlerMode.Instance;
            return cm == null ? 0 : cm.deepest + cm.rank;
        }
    }

    public static int Spent
    {
        get
        {
            int n = 0;
            foreach (var t in Trees) foreach (var nd in t.nodes) if (owned.Contains(nd.id)) n += nd.tier;
            return n;
        }
    }

    public static int Points => Mathf.Max(0, Earned - Spent);

    /// <summary>Somme d'un effet sur les talents acquis.</summary>
    public static float Get(string key)
    {
        float v = 0f;
        foreach (var t in Trees)
            foreach (var nd in t.nodes)
                if (owned.Contains(nd.id) && nd.fx.TryGetValue(key, out float x)) v += x;
        return v;
    }

    public static bool CanLearn(Node n, out string why)
    {
        why = null;
        if (owned.Contains(n.id)) { why = "Acquis"; return false; }
        var cm = CrawlerMode.Instance;
        if (cm != null && cm.Player != null && For(cm.Player.role).nodes.IndexOf(n) < 0) { why = "Autre classe"; return false; }
        foreach (var t in Trees)
            foreach (var o in t.nodes)
                if (o.branch == n.branch && o.tier == n.tier - 1 && t.nodes.Contains(n) && !owned.Contains(o.id)) { why = "Talent précédent requis"; return false; }
        if (Points < n.tier) { why = $"{n.tier} point(s) requis"; return false; }
        return true;
    }

    public static bool Learn(Node n)
    {
        if (!CanLearn(n, out string why)) { GameHUD.Toast(why, GameHUD.WarningColor); return false; }
        owned.Add(n.id);
        Sfx.Play("levelup", null, 0.8f);
        GameHUD.Toast($"Talent appris : {n.name}", GameHUD.GoldColor);
        CrawlerMode.Instance?.OnSkillsChanged();
        return true;
    }
}
