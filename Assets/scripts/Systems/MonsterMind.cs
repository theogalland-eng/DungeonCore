using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Esprit des monstres « éveillés » :
///   - GARDIEN D'ÉTAGE : un monstre de forme finale promu (un seul par étage) : nom, titre, PV ×2,2, dégâts ×1,5,
///     plus grand, et une réplique quand un groupe l'affronte.
///   - MONSTRES QUI PARLENT : la forme finale des familles intelligentes (liche, vampire, démon) et tous les gardiens.
///     Ils interpellent les intrus et, si on leur « parle », donnent leur avis sur le donjon.
/// </summary>
public class MonsterMind : MonoBehaviour
{
    public const int GuardianCost = 150;
    private static readonly string[] IntelligentFamilies = { "lich", "vampire", "demon", "ghost" };
    private static readonly string[] GuardianNames =
    {
        "Grakh", "Morvenna", "Ossuaire", "Vorth", "Sélène", "Kharn", "Nyx", "Brum", "Azhra", "Tarrasque", "Mordu", "Isolde",
    };

    public bool IsGuardian { get; private set; }
    public string Title { get; private set; }
    public string GivenName { get; private set; }

    private GobelinAI ai;
    private MonsterUnit unit;
    private float nextBark;
    private GameObject tag;

    public static bool IsIntelligent(MonsterUnit u) =>
        u != null && ((u.IsFinalForm && System.Array.IndexOf(IntelligentFamilies, u.familyId) >= 0) || (u.GetComponent<MonsterMind>()?.IsGuardian ?? false));

    public static MonsterMind For(GobelinAI g)
    {
        var m = g.GetComponent<MonsterMind>();
        return m != null ? m : g.gameObject.AddComponent<MonsterMind>();
    }

    private void Awake()
    {
        ai = GetComponent<GobelinAI>();
        unit = GetComponent<MonsterUnit>();
    }

    // ─────────────────────────────────────────────
    // GARDIENS
    // ─────────────────────────────────────────────

    public static MonsterMind GuardianOfLayer(int layer)
    {
        foreach (var g in GobelinAI.All)
        {
            if (g == null || !g.IsAlive()) continue;
            var m = g.GetComponent<MonsterMind>();
            if (m != null && m.IsGuardian && LayerOf(g) == layer) return m;
        }
        return null;
    }

    private static int LayerOf(Component c) => DungeonLayers.Instance != null ? DungeonLayers.Instance.IndexAt(c.transform.position) : 0;

    public static bool CanPromote(GobelinAI g, out string reason)
    {
        reason = null;
        var u = g != null ? g.GetComponent<MonsterUnit>() : null;
        if (u == null || u.summoned) { reason = "Seule une créature du donjon peut garder un étage"; return false; }
        var m = g.GetComponent<MonsterMind>();
        if (m != null && m.IsGuardian) { reason = "C'est déjà le boss de cet étage"; return false; }
        if (GuardianOfLayer(LayerOf(g)) != null) { reason = "Cet étage a déjà son boss nommé"; return false; }
        if (!CrawlerMode.Active && !FloorBoss.Target(LayerOf(g), out _, out _, out _))
        { reason = "Rien à garder ici : creusez l'escalier vers l'étage suivant (ou installez-y le cristal)"; return false; }
        int cost = FloorBoss.Cost(g);
        if (ManaSystem.Instance != null && ManaSystem.Instance.Current < cost) { reason = $"{cost} mana nécessaires"; return false; }
        return true;
    }

    public static bool Promote(GobelinAI g) => Promote(g, GuardianNames[Random.Range(0, GuardianNames.Length)]);

    /// <summary>Nomme le monstre : il évolue jusqu'à sa forme ultime et devient le boss de l'étage.</summary>
    public static bool Promote(GobelinAI g, string name)
    {
        if (!CanPromote(g, out string why)) { GameHUD.Toast(why, GameHUD.WarningColor); return false; }
        ManaSystem.Instance?.TrySpend(FloorBoss.Cost(g));
        var boss = CrawlerMode.Active ? g : FloorBoss.EvolveToFinal(g);
        var m = For(boss);
        m.MakeGuardian(name, true);
        FloorBoss.Setup(m, true);
        EvolutionFx.Play(boss.transform.position, new Color(1f, 0.8f, 0.35f), 2.2f);
        GameHUD.Toast($"{m.GivenName} devient {m.Title}", GameHUD.GoldColor);
        Chronicle.Add($"<color=#E8C77F>{m.GivenName} est nommé {m.Title}.</color>");
        StoryManager.Instance?.OnGuardianNamed(m);
        return true;
    }

    /// <summary>« de la Surface », « des Abîmes », « du Cœur du volcan »...</summary>
    private static string Of(string place)
    {
        if (string.IsNullOrEmpty(place)) return "de l'étage";
        if (place.StartsWith("Surface")) return "de la " + place;
        if (place.StartsWith("Cœur") || place.StartsWith("Coeur")) return "du " + place;
        if (place.EndsWith("s")) return "des " + place;
        return "de " + place;
    }

    /// <summary>Applique le statut de gardien (fresh = nouvelle promotion : soins complets).</summary>
    public void MakeGuardian(string name, bool fresh)
    {
        if (IsGuardian) return;
        IsGuardian = true;
        GivenName = name;
        var layers = DungeonLayers.Instance;
        int li = LayerOf(this);
        string where = layers != null && li < layers.layers.Count ? layers.layers[li].name : "";
        int cf = CrawlerMode.Active ? CrawlerMode.Instance.FloorAt(transform.position) : 0;
        Title = cf > 0 ? (cf >= CrawlerDungeon.FloorCount ? "Seigneur de l'antre" : $"Gardien de l'étage {cf}") : $"Boss {Of(where)}";
        ai.maxHealth *= 2.2f;
        if (fresh) ai.currentHealth = ai.maxHealth;
        ai.attackDamage *= 1.5f;
        ai.healingPerSecond *= 3f;
        transform.localScale *= 1.25f;
        ai.gobelinName = $"{name}, {Title}";
        RefreshTag();
        // chargement d'une partie : le boss reprend son rôle une fois les escaliers recréés
        if (!fresh && !CrawlerMode.Active) Invoke(nameof(RestoreBoss), 1f);
    }

    private void RestoreBoss() => FloorBoss.Setup(this, false);

    private void RefreshTag()
    {
        if (tag != null) Destroy(tag);
        if (!IsGuardian && !IsIntelligent(unit)) return;
        NemesisSystem.AddTag(transform, IsGuardian ? GivenName : ai.gobelinName, IsGuardian ? new Color(1f, 0.85f, 0.4f) : new Color(0.75f, 0.65f, 1f));
        tag = transform.Find("Nom")?.gameObject;
        if (tag != null) tag.transform.localPosition = new Vector3(0f, 0.75f, 0f);
    }

    // ─────────────────────────────────────────────
    // PAROLE
    // ─────────────────────────────────────────────

    private void Start()
    {
        if (!IsGuardian && IsIntelligent(unit)) RefreshTag();
    }

    private void Update()
    {
        if (ai == null || !ai.IsAlive() || Time.time < nextBark) return;
        if (!IsGuardian && !IsIntelligent(unit)) { nextBark = Time.time + 5f; return; }
        nextBark = Time.time + 1f;
        // le joueur (mode Aventurier) en vue ?
        if (CrawlerMode.Active && CrawlerMode.Instance.Player != null)
        {
            var pp = CrawlerMode.Instance.Player.transform.position;
            if (Vector2.Distance(pp, transform.position) <= 5f && AdventurerAI.ClearLine(transform.position, pp, 1 << 6))
            {
                Say(IsGuardian ? new[] { "Nul ne passe.", "Le cristal est à moi de le garder.", "Encore un héros ? Approche." }[Random.Range(0, 3)] : "...");
                nextBark = Time.time + Random.Range(18f, 30f);
                return;
            }
        }
        // un intrus en vue ?
        foreach (var a in AdventurerAI.All)
        {
            if (a == null || !a.IsInsideDungeon) continue;
            if (Vector2.Distance(a.transform.position, transform.position) > 5f) continue;
            if (!AdventurerAI.ClearLine(transform.position, a.transform.position, 1 << 6)) continue;
            Say(Bark(a));
            nextBark = Time.time + Random.Range(18f, 30f);
            break;
        }
    }

    private string Bark(AdventurerAI a)
    {
        string f = unit != null ? unit.familyId : "";
        string[] lines = IsGuardian
            ? new[] { "Nul ne passe.", "Tu es venu mourir chez moi ?", $"Rang {GuildSystem.RankName(a.Rank < 0 ? 0 : a.Rank)}... C'est tout ?", "Le Maître m'a confié cet étage. Recule." }
            : f == "lich" ? new[] { "Encore des âmes à cueillir...", "Levez-vous, mes serviteurs.", "La mort n'est qu'un début." }
            : f == "vampire" ? new[] { "Votre sang sent la peur.", "Approchez... plus près.", "Quelle délicieuse visite." }
            : f == "demon" ? new[] { "Brûlez !", "Vos dieux ne vous entendent pas, ici.", "Hah ! Des proies." }
            : new[] { "...partez...", "Vous n'auriez pas dû venir.", "Le froid vous prendra." };
        return lines[Random.Range(0, lines.Length)];
    }

    /// <summary>Bulle de parole au-dessus du monstre.</summary>
    public void Say(string text)
    {
        var go = new GameObject("Bulle");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = $"« {text} »";
        tmp.fontSize = 2.4f;
        tmp.fontStyle = FontStyles.Italic;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.95f, 0.85f);
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(15, 8, 20, 255);
        tmp.rectTransform.sizeDelta = new Vector2(7f, 1.5f);
        tmp.sortingOrder = 41;
        Destroy(go, 3.5f);
        Sfx.Play("growl", transform.position, 0.5f);
    }

    /// <summary>Conseils du monstre, tirés de l'état du donjon (bouton « Parler »).</summary>
    public List<string> Advice()
    {
        var tips = new List<string>();
        var t = DungeonTerritory.Instance;
        var gm = GameManager.Instance;
        int monsters = GobelinAI.All.Count, traps = PlacedObject.Count(ItemKind.Trap);
        if (traps == 0) tips.Add("Maître, nos couloirs sont nus. Un piège ou deux près de l'entrée briseraient leur élan.");
        else if (traps < monsters / 2) tips.Add("Nous nous battons bien, mais les pièges fatiguent l'ennemi avant qu'il nous atteigne. Il en faudrait davantage.");
        if (monsters < 4) tips.Add("Nous sommes trop peu. Invoquez d'autres créatures, ou le prochain groupe passera sans peine.");
        var g = GuildSystem.Instance;
        if (g != null && g.Assessed)
        {
            if (g.hostility > 0.6f) tips.Add("Les humains nous haïssent. Ils viendront plus nombreux, et plus déterminés. Préparez-vous.");
            if (g.greed > 0.5f) tips.Add("Vos trésors font parler d'eux sur le continent. Ils attirent les chasseurs de fortune... et leur or.");
        }
        if (NemesisSystem.Instance != null && NemesisSystem.Instance.AliveCount > 0)
        {
            var r = NemesisSystem.Instance.records.Find(x => !x.dead);
            if (r != null) tips.Add($"Celui qu'ils appellent « {r.epithet} » connaît nos pièges. Déplacez-les, ou il les désarmera encore.");
        }
        if (ManaSystem.Instance != null && ManaSystem.Instance.Current >= ManaSystem.Instance.Max * 0.9f)
            tips.Add("Le mana déborde du cristal. Ne le laissez pas se perdre : creusez, invoquez, bâtissez.");
        if (IslandEvents.Instance != null && IslandEvents.Instance.Current == IslandEvent.BloodMoon)
            tips.Add("Cette lune rouge... je sens mes forces décupler. Qu'ils viennent cette nuit.");
        if (gm != null && gm.IsNight) tips.Add("La nuit est notre alliée. Le mana afflue et les humains y voient mal.");
        if (tips.Count == 0) tips.Add("Tout est calme, Maître. Trop calme. Ils préparent quelque chose.");
        return tips;
    }

    public void Talk()
    {
        var tips = Advice();
        string name = IsGuardian ? GivenName : ai.gobelinName;
        var lines = new List<StoryManager.Line>();
        if (StoryManager.Instance != null && !StoryManager.Instance.HasFlag("first_talk"))
        {
            StoryManager.Instance.SetFlag("first_talk");
            lines.Add(new StoryManager.Line(StoryManager.SelfName, "Il... me regarde. Pas comme une bête. Comme quelqu'un qui attend des ordres."));
            lines.Add(new StoryManager.Line(name, "Maître. Vous m'avez donné la vie ; laissez-moi vous rendre service."));
        }
        lines.Add(new StoryManager.Line(name, tips[Random.Range(0, tips.Count)]));
        StoryManager.Instance?.Play(lines.ToArray());
    }

    public void OnDied()
    {
        if (!IsGuardian) return;
        GameHUD.Toast($"{GivenName}, {Title}, est tombé !", GameHUD.DangerColor);
        StoryManager.Instance?.Play(new StoryManager.Line(StoryManager.SelfName, $"{GivenName}... Tu as tenu ton étage jusqu'au bout."));
    }
}
