using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ARBRE DE TALENTS DU NOYAU : un vrai arbre qui pousse depuis le cristal. Trois branches maîtresses
/// (Architecte, Maître des bêtes, Ombre) de deux talents chacune, qui se divisent ensuite en deux
/// sous-branches de trois talents :
///   - ARCHITECTE → Pièges (mécanismes, lames, arsenal) · Bâtisseur (entretien, prestige, abîmes) ;
///   - MAÎTRE DES BÊTES → Meute (horde, faim, couvée) · Lignée (évolutions, fureur, rois des profondeurs) ;
///   - OMBRE → Terreur (torches, cauchemars, effroi) · Prédation (soif, embuscades, festin).
/// Un point par niveau gagné ; chaque talent demande celui qui le porte. Les effets sont lus par les systèmes
/// concernés (multiplicateurs ci-dessous). Sauvegardés avec la partie (identifiants stables).
/// </summary>
public static class Talents
{
    public enum Branch { Architect, Beastmaster, Shadow }

    public class Def
    {
        public string id, name, desc, parent, sub;
        public Branch branch;
        public Vector2 pos;   // place dans l'arbre (fenêtre des talents), 0,0 = le cristal
        public int tier;      // profondeur dans l'arbre (1 = près du tronc)
    }

    public static readonly string[] BranchNames = { "Architecte", "Maître des bêtes", "Ombre" };
    public static readonly string[] BranchIcons = { "house", "people", "skull" };

    private static Def D(string id, Branch b, string parent, string sub, float x, float y, int tier, string name, string desc) =>
        new Def { id = id, branch = b, parent = parent, sub = sub, pos = new Vector2(x, y), tier = tier, name = name, desc = desc };

    public static readonly List<Def> All = new List<Def>
    {
        // ── ARCHITECTE (à gauche)
        D("a1", Branch.Architect, null, null, -230, 150, 1, "Maçon des abîmes", "Creuser coûte 30 % de mana en moins."),
        D("a2", Branch.Architect, "a1", null, -400, 265, 2, "Ingénieur", "+3 pièges au maximum."),
        D("a4", Branch.Architect, "a2", "Pièges", -590, 270, 3, "Mécanismes retors", "Portes 50 % plus longues à forcer ; plaques de pression deux fois plus cruelles."),
        D("a6", Branch.Architect, "a4", "Pièges", -735, 355, 4, "Lames affûtées", "Poser un piège coûte 30 % de mana en moins."),
        D("a7", Branch.Architect, "a6", "Pièges", -850, 470, 5, "Arsenal", "+3 pièges au maximum (encore)."),
        D("a3", Branch.Architect, "a2", "Bâtisseur", -460, 420, 3, "Économe", "Entretien des pièges et décors -25 %."),
        D("a5", Branch.Architect, "a3", "Bâtisseur", -545, 560, 4, "Cathédrale des ténèbres", "Prestige du donjon ×1,5."),
        D("a8", Branch.Architect, "a5", "Bâtisseur", -630, 690, 5, "Architecte de l'abîme", "Escaliers et nouveaux étages 40 % moins chers."),

        // ── MAÎTRE DES BÊTES (au centre)
        D("b1", Branch.Beastmaster, null, null, 0, 210, 1, "Invocateur", "Invoquer un monstre coûte 15 % de mana en moins."),
        D("b2", Branch.Beastmaster, "b1", null, 0, 345, 2, "Chair robuste", "Vos monstres ont 15 % de PV en plus."),
        D("b4", Branch.Beastmaster, "b2", "Meute", -140, 460, 3, "Horde", "+3 monstres au maximum."),
        D("b3", Branch.Beastmaster, "b4", "Meute", -195, 590, 4, "Ventres pleins", "La faim de vos monstres monte 30 % moins vite."),
        D("b8", Branch.Beastmaster, "b3", "Meute", -230, 720, 5, "Couvée prolifique", "Chaque couvoir fait naître deux créatures chaque matin."),
        D("b5", Branch.Beastmaster, "b2", "Lignée", 140, 460, 3, "Sang ancien", "Évolutions -25 % de mana, expérience des monstres +25 %."),
        D("b7", Branch.Beastmaster, "b5", "Lignée", 195, 590, 4, "Fureur bestiale", "Vos monstres infligent 12 % de dégâts en plus."),
        D("b6", Branch.Beastmaster, "b7", "Lignée", 230, 720, 5, "Rois des profondeurs", "Nommer un boss coûte 30 % de mana en moins ; vos boss ont 25 % de PV en plus."),

        // ── OMBRE (à droite)
        D("c1", Branch.Shadow, null, null, 230, 150, 1, "Murmures", "Les intrus ont 10 % de chances en plus de fuir."),
        D("c2", Branch.Shadow, "c1", null, 400, 265, 2, "Ténèbres", "Dans le donjon, les intrus voient 15 % moins loin."),
        D("c5", Branch.Shadow, "c2", "Terreur", 590, 270, 3, "Torches mourantes", "Les torches des intrus s'éteignent après 40 s dans le donjon."),
        D("c6", Branch.Shadow, "c5", "Terreur", 735, 355, 4, "Cauchemars", "Les sorts du Noyau coûtent 25 % de mana en moins."),
        D("c8", Branch.Shadow, "c6", "Terreur", 850, 470, 5, "Effroi", "Dans le donjon, les intrus infligent 15 % de dégâts en moins."),
        D("c3", Branch.Shadow, "c2", "Prédation", 460, 420, 3, "Soif du cristal", "Drain de mana des intrus +25 %."),
        D("c4", Branch.Shadow, "c3", "Prédation", 545, 560, 4, "Tapis dans l'ombre", "Vos monstres postés s'embusquent en 2 s, et leur premier coup fait double dégâts."),
        D("c7", Branch.Shadow, "c4", "Prédation", 630, 690, 5, "Festin", "Un monstre qui dévore un cadavre rend 15 mana au cristal."),
    };

    public static Def Get(string id) => All.Find(d => d.id == id);

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
        if (d.parent != null && !owned.Contains(d.parent)) { why = $"Demande « {Get(d.parent)?.name} »"; return false; }
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
        Chronicle.Add($"Le Noyau acquiert un talent : {d.name}.");
        Sfx.Play("shine", null, 0.8f);
        OnChanged?.Invoke();
        return true;
    }

    public static void Reset() { owned.Clear(); OnChanged?.Invoke(); }
    public static void Capture(List<string> into) { into.Clear(); into.AddRange(owned); }
    public static void Restore(List<string> from)
    {
        owned.Clear();
        if (from != null) foreach (var s in from) owned.Add(s);
        // anciennes parties (arbre en échelle) : un talent dont le porteur manque reste acquis, on ajoute le porteur
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var d in All)
                if (owned.Contains(d.id) && d.parent != null && !owned.Contains(d.parent)) { owned.Add(d.parent); changed = true; }
        }
        OnChanged?.Invoke();
    }

    // ── Effets lus par les systèmes ──
    public static float DigCostMult => Has("a1") ? 0.7f : 1f;
    public static int TrapLimitBonus => (Has("a2") ? 3 : 0) + (Has("a7") ? 3 : 0);
    public static float ObjectUpkeepMult => Has("a3") ? 0.75f : 1f;
    public static float DoorHoldMult => Has("a4") ? 1.5f : 1f;
    public static float PlateDamageMult => Has("a4") ? 2f : 1f;
    public static float PrestigeMult => Has("a5") ? 1.5f : 1f;
    public static float TrapCostMult => Has("a6") ? 0.7f : 1f;
    public static float LayerCostMult => Has("a8") ? 0.6f : 1f;
    public static float SummonCostMult => Has("b1") ? 0.85f : 1f;
    public static float MonsterHpMult => Has("b2") ? 1.15f : 1f;
    public static float MonsterHungerMult => Has("b3") ? 0.7f : 1f;
    public static int MonsterLimitBonus => Has("b4") ? 3 : 0;
    public static float EvolveCostMult => Has("b5") ? 0.75f : 1f;
    public static float MonsterXpMult => Has("b5") ? 1.25f : 1f;
    public static float BossCostMult => Has("b6") ? 0.7f : 1f;
    public static float BossHpMult => Has("b6") ? 1.25f : 1f;
    public static float MonsterDamageMult => Has("b7") ? 1.12f : 1f;
    public static int HatchCount => Has("b8") ? 2 : 1;
    public static float IntruderFleeBonus => Has("c1") ? 0.1f : 0f;
    public static float IntruderSightMult => Has("c2") ? 0.85f : 1f;
    public static float ManaDrainMult => Has("c3") ? 1.25f : 1f;
    public static float AmbushDelay => Has("c4") ? 2f : 5f;
    public static float AmbushMult => Has("c4") ? 2f : 1.5f;
    public static bool TorchesDie => Has("c5");
    public static float SpellCostMult => Has("c6") ? 0.75f : 1f;
    public static int FeastMana => Has("c7") ? 15 : 0;
    public static float IntruderDamageMult => Has("c8") ? 0.85f : 1f;
}
