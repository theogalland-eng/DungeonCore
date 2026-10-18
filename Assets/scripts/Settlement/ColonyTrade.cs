using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// COMMERCE SECRET AVEC LE NOYAU : quand la colonie tolère le donjon (relation ≥ 25, ou trêve), un colon
/// sert de contact et, la nuit venue, laisse des marchandises à l'entrée de la caverne contre du mana.
/// Chaque offre se conclut une fois par jour ; chaque échange enrichit la colonie et rapproche les deux camps.
/// </summary>
public static class ColonyTrade
{
    public class Offer
    {
        public string id, name, desc;
        public int mana, food;
        public System.Action apply;
    }

    public const float RelationNeeded = 25f;

    private static readonly Dictionary<string, int> usedDay = new Dictionary<string, int>();
    private static int digDay = -1;
    private static int owner;

    private static int Today => GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

    /// <summary>Les états du jour appartiennent à la partie en cours.</summary>
    private static void Sync()
    {
        int id = GameManager.Instance != null ? GameManager.Instance.GetInstanceID() : 0;
        if (id == owner) return;
        owner = id;
        usedDay.Clear();
        digDay = -1;
    }

    /// <summary>Pics et lanternes : creuser coûte moitié moins jusqu'à l'aube.</summary>
    public static float DigCostMult { get { Sync(); return digDay == Today ? 0.5f : 1f; } }

    public static readonly List<Offer> Offers = new List<Offer>
    {
        new Offer
        {
            id = "food", name = "Vivres de contrebande", mana = 35, food = 8,
            desc = "Viande salée et poisson séché déposés à l'entrée : vos monstres mangent à leur faim.",
            apply = () =>
            {
                int n = 0;
                foreach (var g in GobelinAI.All)
                    if (g != null && g.IsAlive() && g.Life != null) { g.Life.hunger = Mathf.Max(0f, g.Life.hunger - 70f); n++; }
                GameHUD.Toast($"{n} monstre(s) rassasiés", new Color(0.6f, 1f, 0.7f));
            },
        },
        new Offer
        {
            id = "rumor", name = "Rumeurs du port", mana = 20,
            desc = "Ce que l'on raconte à la taverne : quand vient la prochaine expédition, et qui.",
            apply = () =>
            {
                string r = Rumor();
                GameHUD.Toast(r, GameHUD.InfoColor);
                Chronicle.Add($"Rumeur du port : {r}");
            },
        },
        new Offer
        {
            id = "tools", name = "Pics et lanternes", mana = 40,
            desc = "Des outils de mineur « égarés » : creuser coûte moitié moins jusqu'à l'aube.",
            apply = () => { digDay = Today; GameHUD.Toast("Creuser coûte moitié moins aujourd'hui", new Color(0.6f, 1f, 0.7f)); },
        },
    };

    /// <summary>Le colon qui fait passer les marchandises (le plus ancien sur l'île).</summary>
    public static SettlerAI Contact
    {
        get
        {
            SettlerAI best = null;
            foreach (var s in SettlerAI.All)
                if (s != null && !s.IsDying && (best == null || s.arrivedDay < best.arrivedDay)) best = s;
            return best;
        }
    }

    public static bool Open(out string why)
    {
        why = null;
        var colony = SettlementManager.Instance;
        var rel = RelationSystem.Instance;
        if (colony == null || colony.stage == 0) { why = "Aucune colonie sur l'île"; return false; }
        if (colony.Outpost || colony.Population == 0) { why = "Plus personne avec qui commercer"; return false; }
        if (rel == null || (!rel.truce && rel.relation < RelationNeeded)) { why = $"La colonie doit vous tolérer (relation {RelationNeeded:0})"; return false; }
        return true;
    }

    public static bool CanBuy(Offer o, out string why)
    {
        Sync();
        if (!Open(out why)) return false;
        if (usedDay.TryGetValue(o.id, out int d) && d == Today) { why = "Déjà conclu aujourd'hui"; return false; }
        var colony = SettlementManager.Instance;
        if (o.food > 0 && colony.food < o.food) { why = "La colonie manque de vivres"; return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(o.mana)) { why = $"{o.mana} mana requis"; return false; }
        return true;
    }

    public static bool Buy(Offer o)
    {
        if (!CanBuy(o, out string why)) { GameHUD.Toast(why, GameHUD.WarningColor); return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(o.mana)) return false;
        usedDay[o.id] = Today;
        var colony = SettlementManager.Instance;
        colony.food -= o.food;
        colony.prosperity += 3f;
        colony.morale = Mathf.Min(100f, colony.morale + 2f);
        RelationSystem.Instance?.Nudge(2f);
        o.apply?.Invoke();
        Sfx.Play("coin");
        var c = Contact;
        string who = c != null ? c.settlerName : "Un colon";
        c?.Say("... personne ne m'a vu.");
        StoryManager.Instance?.PlayOnce("first_trade",
            new StoryManager.Line(StoryManager.NarratorName, $"À la nuit tombée, {who} se faufile jusqu'à la caverne, dépose un ballot et repart avec une poignée de cristaux."),
            new StoryManager.Line(StoryManager.SelfName, "Les hommes sont si prévisibles. Un peu de lumière, et ils oublient de quel côté ils sont."));
        return true;
    }

    private static string Rumor()
    {
        var exp = ExpeditionManager.Instance;
        var guild = GuildSystem.Instance;
        if (exp == null) return "Rien de neuf au port.";
        if (exp.ExpeditionActive) return "« Ceux qui sont là-dessous ne remonteront pas tous. »";
        string when = exp.NextExpeditionDay <= Today ? "dès aujourd'hui" : exp.NextExpeditionDay == Today + 1 ? "demain" : $"le jour {exp.NextExpeditionDay}";
        string who = guild != null && guild.Assessed ? $"des aventuriers de rang {GuildSystem.RankName(guild.officialRank)}" : "des aventuriers";
        var rel = RelationSystem.Instance;
        string mood = rel != null && rel.PunitivePending ? " On parle d'une expédition punitive." : "";
        return $"Une expédition est attendue {when} : {who}.{mood}";
    }
}
