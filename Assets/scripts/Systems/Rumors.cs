using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Ce qu'on raconte sur le donjon à la taverne : des rumeurs tirées de l'état réel du jeu
/// (monstres les plus meurtriers, pièges redoutés, trésors, gardiens, Némésis, prisonniers, salles...).
/// Sans « système », c'est la seule façon de savoir ce que les humains pensent de vous.
/// </summary>
public static class Rumors
{
    public static List<(string who, string text)> Generate(int max = 6)
    {
        var list = new List<(string, string)>();
        var guild = GuildSystem.Instance;

        if (guild != null && guild.Assessed)
            list.Add(($"Une aventurière de rang {GuildSystem.RankName(Mathf.Max(0, guild.officialRank - 1))}",
                $"Rang {GuildSystem.RankName(guild.officialRank)}, qu'ils disent. {Capital(GuildSystem.DungeonRankDesc(guild.officialRank))}. J'y retourne quand même demain."));
        else if (ReputationSystem.Instance != null && ReputationSystem.Instance.dungeonDiscovered)
            list.Add(("Un vieux pêcheur", "La Guilde n'a pas encore classé la grotte. Moi je vous le dis : il y a quelque chose de vivant, là-dessous."));

        // Le monstre le plus meurtrier
        var killer = MonsterUnit.All.Where(m => m != null && m.kills > 0).OrderByDescending(m => m.kills).FirstOrDefault();
        if (killer != null)
        {
            string name = killer.GetComponent<GobelinAI>()?.gobelinName ?? killer.familyName;
            list.Add(("Un mercenaire balafré", $"Méfiez-vous de « {name} ». Il a déjà tué {killer.kills} des nôtres. On l'entend avant de le voir."));
        }

        // Le piège le plus redouté
        var trap = PlacedObject.All.Where(p => p != null && p.kind == ItemKind.Trap && p.damageDealt > 0).OrderByDescending(p => p.damageDealt).FirstOrDefault();
        if (trap != null)
        {
            var item = DungeonCatalog.Get()?.Find(trap.itemId);
            list.Add(("Un chasseur", $"Regardez où vous mettez les pieds. {(item != null ? item.displayName : "Un piège")} m'a pris la moitié de mon sang, et je ne l'avais même pas vu."));
        }

        // Trésors
        float prestige = PlacedObject.TotalPrestige();
        if (TreasureChest.All.Count > 0 || prestige > 30f)
            list.Add(("Un marchand", prestige > 60f
                ? "Des coffres royaux, de l'or en tas, des trophées... Ce donjon est plus riche que le roi. Ou c'est un appât."
                : "Il y aurait des coffres, au fond. Personne n'en est encore revenu les bras chargés."));

        // Gardiens
        foreach (var g in GobelinAI.All)
        {
            var mind = g != null ? g.GetComponent<MonsterMind>() : null;
            if (mind == null || !mind.IsGuardian) continue;
            list.Add(("Le barde", $"J'ai composé une chanson sur {mind.GivenName}, {mind.Title.ToLowerInvariant()}. Personne n'a survécu pour m'en dire la fin."));
            break;
        }

        // Némésis
        var nem = NemesisSystem.Instance != null ? NemesisSystem.Instance.records.Find(r => !r.dead) : null;
        if (nem != null)
            list.Add(($"{nem.name} « {nem.epithet} »", nem.encounters > 1
                ? $"Je suis sorti vivant {nem.encounters} fois. Je connais ses pièges par cœur. La prochaine, je reviens avec le cristal."
                : "J'y suis entré, j'en suis ressorti. Et j'ai tout noté. Ce donjon ne me fera plus peur."));
        var fallen = NemesisSystem.Instance != null ? NemesisSystem.Instance.records.Find(r => r.dead) : null;
        if (fallen != null)
            list.Add(("La tavernière", $"On a gardé la chope de {fallen.name}. Il disait toujours qu'il reviendrait. Cette fois, il n'est pas revenu."));

        // Prisonniers
        if (PrisonSystem.Instance != null && PrisonSystem.Instance.prisoners.Count > 0)
            list.Add(("Une prêtresse de passage", "Certains disparus seraient vivants, enfermés là-dessous. J'ai entendu des cris, la nuit, près de l'entrée."));

        // Salles
        var rooms = ThemedRooms.Instance != null ? ThemedRooms.Instance.rooms : null;
        if (rooms != null && rooms.Count > 0)
        {
            var r = rooms[Random.Range(0, rooms.Count)];
            string what = r.kind == ThemedRooms.Kind.Crypt ? "une crypte où les morts se relèvent plus forts"
                : r.kind == ThemedRooms.Kind.Armory ? "une salle pleine d'armes, où les monstres frappent comme des soldats"
                : r.kind == ThemedRooms.Kind.Treasury ? "une salle au trésor qui brille jusque dans les couloirs"
                : r.kind == ThemedRooms.Kind.Sanctum ? "un sanctuaire où les créatures guérissent à vue d'œil"
                : "un jardin de champignons qui brillent dans le noir... et des choses gluantes qui y grouillent";
            list.Add(("Un jeune apprenti", $"Mon maître dit qu'il y a {what}."));
        }

        // Colonie, événements
        var colony = SettlementManager.Instance;
        if (colony != null && colony.stage > 0 && colony.morale < 35f)
            list.Add(("La tavernière", "Trop d'enterrements ces temps-ci. Les gens parlent de repartir sur le continent."));
        if (IslandEvents.Instance != null && IslandEvents.Instance.Current == IslandEvent.BloodMoon)
            list.Add(("Un ivrogne", "Vous avez vu la lune ? Rouge comme du sang. Personne ne descend dans le trou, ce soir. Personne."));
        if (list.Count < 3)
        {
            list.Add(("Un ivrogne", "Un donjon qui pousse tout seul dans une montagne... Moi je dis que la montagne est vivante. Hic."));
            list.Add(("Une aventurière", "Paraît que le cristal, au fond, pense. Comme vous et moi. Ça me donne froid dans le dos."));
        }

        // un peu de hasard dans l'ordre, sans perdre les plus importantes
        var first = list.Take(2).ToList();
        var rest = list.Skip(2).OrderBy(_ => Random.value).ToList();
        first.AddRange(rest);
        return first.Take(max).ToList();
    }

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
}
