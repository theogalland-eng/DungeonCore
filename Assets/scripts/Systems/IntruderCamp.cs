using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// CAMP DE BASE D'UNE EXPÉDITION : un groupe d'au moins trois intrus plante ses tentes et allume un feu près de la plage.
///   - les blessés s'y replient et s'y soignent deux fois plus vite ;
///   - la nuit, ceux qui sont dehors y dorment jusqu'à l'aube (AdventurerLife) ;
///   - RAID SUR LE CAMP (fenêtre Colonie, la nuit) : vos monstres fondent sur les dormeurs, puis rentrent avant l'aube.
/// Le camp est replié quand l'expédition a quitté l'île (ou n'y est plus).
/// </summary>
public class IntruderCamp : MonoBehaviour
{
    public static readonly List<IntruderCamp> All = new List<IntruderCamp>();

    public IntruderMap expedition;   // la carte partagée identifie l'expédition
    public Vector2 Center => transform.position;
    public bool RaidActive => raiders.Count > 0;

    private readonly List<GameObject> parts = new List<GameObject>();
    private float nextCheck, fade = -1f;

    private class Raider { public GobelinAI ai; public float arrivedAt = -1f, nextStrike; public bool home; }
    private readonly List<Raider> raiders = new List<Raider>();

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>Camp de l'expédition de cet intrus (null : aucun).</summary>
    public static IntruderCamp Of(AdventurerAI a)
    {
        if (a == null || a.Map == null) return null;
        foreach (var c in All) if (c != null && c.expedition == a.Map) return c;
        return null;
    }

    /// <summary>Une expédition débarque : si elle compte au moins trois membres, elle installe son camp.</summary>
    public static IntruderCamp TryPitch(List<AdventurerAI> members)
    {
        if (members == null || members.Count < 3 || CrawlerMode.Active) return null;
        var map = members[0].Map;
        foreach (var c in All) if (c != null && c.expedition == map) return c;
        var site = SettlementManager.Instance != null ? SettlementManager.Instance.Site : null;
        Vector2 landing = site != null ? site.LandingWorld : (Vector2)members[0].transform.position;
        var core = FindFirstObjectByType<DungeonCore>();
        Vector2 inland = core != null ? ((Vector2)core.transform.position - landing).normalized : Vector2.up;
        if (!FindSpot(landing + inland * 7f, out var spot) && !FindSpot(landing + inland * 4f, out spot)) return null;

        var go = new GameObject("Camp d'expédition");
        go.transform.position = spot;
        var camp = go.AddComponent<IntruderCamp>();
        camp.expedition = map;
        camp.Build();
        GameHUD.Toast("Une expédition installe son camp près de la plage", GameHUD.InfoColor);
        Chronicle.Add("Une expédition installe son camp près de la plage.");
        StoryManager.Instance?.PlayOnce("first_camp",
            new StoryManager.Line(StoryManager.SelfName, "Ils plantent des tentes. Ils comptent rester... Cette nuit, ils dormiront près de leur feu. Vulnérables."),
            new StoryManager.Line(StoryManager.SystemName, "Camp d'expédition : les blessés s'y soignent, les intrus y dorment la nuit. Fenêtre Colonie → Raid sur le camp, la nuit."));
        return camp;
    }

    /// <summary>Une case dégagée de terre ferme (pas d'arbre, pas d'eau, hors du donjon) près du point voulu.</summary>
    private static bool FindSpot(Vector2 near, out Vector2 spot)
    {
        spot = near;
        for (int i = 0; i < 40; i++)
        {
            Vector2 p = near + Random.insideUnitCircle * (1.5f + i * 0.15f);
            if (!WildAnimal.IsDrySpot(p)) continue;
            if (DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(p)) continue;
            if (Physics2D.OverlapCircle(p, 1.2f, (1 << 6) | (1 << 8)) != null) continue;   // murs, troncs
            var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
            if (graph != null)
            {
                var nn = graph.GetNearest(p, Pathfinding.NNConstraint.Default);
                if (nn.node == null || !nn.node.Walkable || Vector2.Distance((Vector3)nn.node.position, p) > 0.8f) continue;
            }
            spot = p;
            return true;
        }
        return false;
    }

    private void Build()
    {
        Part("Tente", PropArt.Get("tent"), new Vector2(-1.1f, 0.5f), false);
        Part("Tente", PropArt.Get("tent"), new Vector2(1.2f, 0.7f), true);
        var fire = Part("Feu de camp", PropArt.Get("campfire"), Vector2.zero, false);
        var l = fire.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = new Color(1f, 0.6f, 0.25f);
        l.intensity = 0.95f;
        l.pointLightOuterRadius = 3.2f;
        fire.AddComponent<LavaGlow>();
        Fx.Play("smoke_rise", (Vector3)Center + Vector3.up * 0.4f, 1f);
    }

    private GameObject Part(string name, Sprite s, Vector2 offset, bool flip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = 3;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        sr.flipX = flip;
        parts.Add(go);
        return go;
    }

    /// <summary>Place au coin du feu pour un intrus (en cercle).</summary>
    public Vector2 RestSpot(AdventurerAI a)
    {
        float ang = (a.GetInstanceID() % 7) / 7f * Mathf.PI * 2f;
        return Center + new Vector2(Mathf.Cos(ang) * 1.3f, Mathf.Sin(ang) * 0.9f - 0.3f);
    }

    private void Update()
    {
        UpdateRaid();
        if (fade >= 0f)
        {
            fade += Time.deltaTime;
            foreach (var p in parts)
                if (p != null) { var sr = p.GetComponent<SpriteRenderer>(); if (sr != null) { var c = sr.color; c.a = 1f - fade / 2f; sr.color = c; } }
            if (fade > 2f) Destroy(gameObject);
            return;
        }
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 3f;
        // l'expédition est partie : on replie le camp
        bool anyone = false;
        foreach (var a in AdventurerAI.All) if (a != null && !a.IsDead && a.Map == expedition) { anyone = true; break; }
        if (!anyone && !RaidActive) fade = 0f;
    }

    // ─────────────────────────────────────────────
    // RAID SUR LE CAMP
    // ─────────────────────────────────────────────

    public static IntruderCamp Raidable()
    {
        foreach (var c in All) if (c != null && c.fade < 0f && !c.RaidActive) return c;
        return null;
    }

    public bool CanRaid(out string reason)
    {
        reason = null;
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsNight) { reason = "Seulement la nuit"; return false; }
        if (RaidActive || (RaidSystem.Instance != null && RaidSystem.Instance.Active)) { reason = "Un raid est déjà en cours"; return false; }
        if (RaidSystem.Instance == null || RaidSystem.Instance.PickRaiders().Count == 0) { reason = "Aucun monstre disponible"; return false; }
        return true;
    }

    public bool LaunchRaid()
    {
        if (!CanRaid(out string why)) { GameHUD.Toast(why, GameHUD.WarningColor); return false; }
        var team = RaidSystem.Instance.PickRaiders();
        raiders.Clear();
        foreach (var g in team)
        {
            var r = new Raider { ai = g };
            raiders.Add(r);
            var rr = r;
            g.StartRaid(Center + Random.insideUnitCircle * 1.2f, _ => { rr.arrivedAt = Time.time; rr.nextStrike = Time.time + 0.3f; });
        }
        MonsterSelector.Instance?.DeselectAll();
        GameHUD.Toast($"Raid sur le camp : {team.Count} monstre(s) fondent sur les dormeurs", GameHUD.DangerColor);
        return true;
    }

    private void UpdateRaid()
    {
        if (raiders.Count == 0) return;
        bool anyOut = false;
        foreach (var r in raiders)
        {
            if (r.ai == null || !r.ai.IsAlive() || !r.ai.IsRaiding) continue;
            anyOut = true;
            if (r.home || r.arrivedAt < 0f) continue;
            if (Time.time >= r.nextStrike)
            {
                r.nextStrike = Time.time + 1.4f;
                // le dormeur le plus proche : surpris dans son sommeil, il encaisse double
                AdventurerAI best = null;
                float bd = 3.5f;
                foreach (var a in AdventurerAI.All)
                {
                    if (a == null || a.IsDead || a.IsInsideDungeon) continue;
                    float d = Vector2.Distance(a.transform.position, r.ai.transform.position);
                    if (d < bd) { bd = d; best = a; }
                }
                r.ai.RaidStrike(best != null ? (Vector2)best.transform.position : Center);
                if (best != null && best.combat != null)
                {
                    bool asleep = best.Life != null && best.Life.Activity == "dort au camp";
                    best.combat.TakeDamage(r.ai.attackDamage * (asleep ? 2f : 1f));
                    if (best.Life != null) best.Life.Interrupt();
                    best.Speak(asleep ? "À l'aide ! On nous attaque !" : "Aux armes !", new Color(1f, 0.6f, 0.4f), true);
                }
            }
            if (Time.time - r.arrivedAt > 8f || (GameManager.Instance != null && !GameManager.Instance.IsNight)) { r.home = true; r.ai.ReturnFromRaid(); }
        }
        if (!anyOut)
        {
            raiders.Clear();
            if (ReputationSystem.Instance != null) ReputationSystem.Instance.threat += 2f;
            GameHUD.Toast("Raid sur le camp terminé", GameHUD.WarningColor);
            Chronicle.Add("Vos monstres ont fondu sur le camp des intrus endormis.");
        }
    }
}
