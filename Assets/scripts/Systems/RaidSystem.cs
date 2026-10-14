using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Raids nocturnes contre la colonie humaine.
///   - TERREUR : les monstres rôdent autour des maisons ; les colons fuient en hurlant.
///     Moral et prospérité en chute, vivres pillés ; la peur nourrit le Noyau (un peu de mana), personne ne meurt.
///   - SANGLANT : les monstres tuent des colons. Beaucoup de mana et d'expérience, mais la colonie
///     s'effondre et appelle une EXPÉDITION PUNITIVE de haut niveau. Une milice (stade ≥ 3) se défend.
/// Les monstres partent de leur ancre, traversent l'île et rentrent avant l'aube.
/// </summary>
public class RaidSystem : MonoBehaviour
{
    public static RaidSystem Instance { get; private set; }

    public int maxRaiders = 5;
    public float timeAtTarget = 7f;
    public int manaPerScared = 8;
    public int manaPerKill = 35;

    public bool Active => raiders.Count > 0;
    public bool Lethal { get; private set; }
    public int RaidsDone { get; set; }

    private class Raider
    {
        public GobelinAI ai;
        public Vector2 target;
        public float arrivedAt = -1f;
        public float nextAct;
        public int kills;
        public bool sentHome;
    }

    private readonly List<Raider> raiders = new List<Raider>();
    private readonly HashSet<SettlerAI> scaredSet = new HashSet<SettlerAI>();
    private int scared => scaredSet.Count;
    private int killed;
    private float startedAt;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNightChanged -= OnNightChanged;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNightChanged += OnNightChanged;
    }

    private static SettlementManager Colony => SettlementManager.Instance;

    public bool CanRaid(out string reason)
    {
        reason = null;
        var gm = GameManager.Instance;
        if (Colony == null || Colony.stage == 0) { reason = "Aucune colonie à attaquer"; return false; }
        if (gm == null || !gm.IsNight) { reason = "Les raids n'ont lieu que la nuit"; return false; }
        if (Active) { reason = "Un raid est déjà en cours"; return false; }
        var team = PickRaiders();
        if (team.Count == 0) { reason = "Aucun monstre disponible"; return false; }
        // Assez de nuit pour l'aller, le raid et le retour ?
        float needed = TravelSeconds(team) + timeAtTarget + 2f;
        float left = (1f - gm.DayProgress) * gm.dayLengthSeconds;
        if (left < needed) { reason = "Trop tard : l'aube arrivera avant qu'ils atteignent la colonie"; return false; }
        return true;
    }

    /// <summary>Temps de trajet estimé (secondes de jeu) du monstre le plus lent jusqu'à la colonie.</summary>
    public float TravelSeconds(List<GobelinAI> team)
    {
        var targets = Colony != null ? Colony.RaidTargets() : null;
        if (targets == null || targets.Count == 0) return 0f;
        float worst = 0f;
        foreach (var g in team)
        {
            float d = float.MaxValue;
            foreach (var t in targets) d = Mathf.Min(d, Vector2.Distance(g.transform.position, t));
            float speed = g.RaidSpeed;
            worst = Mathf.Max(worst, d * 1.35f / speed);   // les chemins ne sont pas des lignes droites
        }
        return worst;
    }

    /// <summary>Monstres choisis : la sélection du joueur, sinon les plus forts (jusqu'à 3).</summary>
    public List<GobelinAI> PickRaiders()
    {
        var list = new List<GobelinAI>();
        var sel = MonsterSelector.Instance;
        if (sel != null)
            foreach (var g in sel.SelectedGoblins) if (Usable(g)) list.Add(g);
        if (list.Count == 0)
        {
            var all = new List<GobelinAI>();
            foreach (var g in GobelinAI.All) if (Usable(g)) all.Add(g);
            all.Sort((a, b) => (b.maxHealth * b.attackDamage).CompareTo(a.maxHealth * a.attackDamage));
            for (int i = 0; i < all.Count && i < 3; i++) list.Add(all[i]);
        }
        if (list.Count > maxRaiders) list.RemoveRange(maxRaiders, list.Count - maxRaiders);
        return list;
    }

    private static bool Usable(GobelinAI g)
    {
        if (g == null || !g.IsAlive() || g.IsPossessed || g.IsRaiding || g.IsOnMission) return false;
        var u = g.GetComponent<MonsterUnit>();
        return u == null || !u.summoned;
    }

    public bool Launch(bool lethal)
    {
        if (!CanRaid(out string reason)) { GameHUD.Toast(reason, GameHUD.WarningColor); return false; }
        var targets = Colony.RaidTargets();
        var team = PickRaiders();
        Lethal = lethal;
        scaredSet.Clear();
        killed = 0;
        startedAt = Time.time;
        raiders.Clear();
        for (int i = 0; i < team.Count; i++)
        {
            var r = new Raider { ai = team[i], target = targets[i % targets.Count] + Random.insideUnitCircle * 0.8f };
            raiders.Add(r);
            var rr = r;
            team[i].StartRaid(r.target, g => OnArrive(rr));
        }
        MonsterSelector.Instance?.DeselectAll();
        GameHUD.Toast(lethal ? $"Raid sanglant : {team.Count} monstre(s) fondent sur la colonie" : $"Raid de terreur : {team.Count} monstre(s) partent effrayer la colonie",
            lethal ? GameHUD.DangerColor : GameHUD.WarningColor);
        StoryManager.Instance?.PlayOnce(lethal ? "first_raid_lethal" : "first_raid",
            new StoryManager.Line(StoryManager.SelfName, lethal ? "Cette nuit, pas de quartier. Qu'ils comprennent à qui appartient cette île." : "Cette nuit, ils n'entendront que des grognements... et leurs propres cris."),
            new StoryManager.Line(StoryManager.SystemName, lethal ? "RAID SANGLANT : beaucoup de mana, mais la colonie enverra une EXPÉDITION PUNITIVE de haut niveau." : "RAID DE TERREUR : le moral de la colonie s'effondre, sans mort. Vos monstres rentrent avant l'aube."));
        return true;
    }

    private void OnArrive(Raider r)
    {
        r.arrivedAt = Time.time;
        r.nextAct = Time.time + 0.4f;
        if (!Lethal && Colony != null)
        {
            // les grognements réveillent tout le camp
            int n = 0;
            foreach (var s in Colony.ScareAround(r.target, 14f)) if (scaredSet.Add(s)) n++;
            if (n > 0) ManaSystem.Instance?.Add(n * manaPerScared);
            r.ai.GetComponent<MonsterUnit>()?.AddXp(6 + 2 * n);
            FloatingText.Spawn(r.ai.transform.position + Vector3.up * 0.8f, n > 0 ? $"+{n * manaPerScared} (peur)" : "Grrr !", GameHUD.ManaColor, 1f);
        }
    }

    private void Update()
    {
        if (raiders.Count == 0) return;
        bool anyOut = false;
        foreach (var r in raiders)
        {
            if (r.ai == null || !r.ai.IsAlive()) continue;
            if (!r.ai.IsRaiding) continue;   // rentré au donjon
            anyOut = true;
            if (r.sentHome || r.arrivedAt < 0f) continue;

            if (Time.time >= r.nextAct)
            {
                r.nextAct = Time.time + (Lethal ? 2.2f : 1.6f);
                r.ai.RaidStrike(r.target);
                if (Lethal && Colony != null && r.kills < 1)
                {
                    var p = Colony.KillNearest(r.ai.transform.position, 14f);
                    if (p.HasValue)
                    {
                        r.kills++;
                        killed++;
                        ManaSystem.Instance?.Add(manaPerKill);
                        r.ai.GetComponent<MonsterUnit>()?.AddXp(15);
                        FloatingText.Spawn((Vector3)p.Value + Vector3.up * 0.6f, $"+{manaPerKill} mana", GameHUD.ManaColor, 1.1f);
                    }
                }
                else if (!Lethal && Colony != null)
                {
                    // les monstres continuent de rôder : quelques colons de plus s'enfuient
                    foreach (var s in Colony.ScareAround(r.target, 8f)) scaredSet.Add(s);
                }
                // la milice riposte
                float militia = Colony != null ? Colony.MilitiaDamage : 0f;
                if (militia > 0f)
                {
                    r.ai.TakeDamage(militia);
                    if (!r.ai.IsAlive()) continue;
                }
            }
            if (Time.time - r.arrivedAt > timeAtTarget) { r.sentHome = true; r.ai.ReturnFromRaid(); }
        }
        // sécurité : un raid qui s'éternise rentre
        if (anyOut && Time.time - startedAt > 90f) ReturnAll();
        if (!anyOut) Finish();
    }

    private void OnNightChanged(bool night)
    {
        if (!night && Active)
        {
            ReturnAll();
            GameHUD.Toast("L'aube se lève : vos monstres regagnent le donjon", GameHUD.InfoColor);
        }
    }

    public void ReturnAll()
    {
        foreach (var r in raiders)
            if (r.ai != null && r.ai.IsRaiding && !r.sentHome) { r.sentHome = true; r.ai.ReturnFromRaid(); }
    }

    private void Finish()
    {
        int lost = 0;
        foreach (var r in raiders) if (r.ai == null || !r.ai.IsAlive()) lost++;
        raiders.Clear();
        RaidsDone++;
        Colony?.OnRaid(Lethal, scared, killed);
        RelationSystem.Instance?.OnRaid(Lethal, killed);
        var rep = ReputationSystem.Instance;
        if (rep != null) rep.threat += Lethal ? 3f + 2f * killed : 1.5f;
        string losses = lost > 0 ? $", {lost} monstre(s) perdu(s)" : "";
        if (Lethal) GameHUD.Toast($"Raid terminé : {killed} colon(s) tué(s){losses}", GameHUD.DangerColor);
        else GameHUD.Toast($"Raid terminé : {scared} colon(s) terrorisé(s){losses}", GameHUD.WarningColor);
    }
}
