using System.Collections.Generic;
using Pathfinding;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// BOSS D'ÉTAGE NOMMÉ (mode Donjon) : on donne un nom à un monstre — un seul par étage. Il évolue aussitôt jusqu'à
/// sa forme ultime, devient le gardien de l'étage et prend son poste dans la dernière salle, devant l'escalier qui
/// descend (ou devant le cristal si celui-ci repose à cet étage). Un SCEAU ferme alors ce passage : seule la mort
/// du boss le brise. Il combat en trois phases comme les boss du mode Aventurier (CrawlerBoss).
/// </summary>
public static class FloorBoss
{
    /// <summary>Ce que garde le boss d'un étage : l'escalier de descente, sinon le cristal (s'il y repose).</summary>
    public static bool Target(int layer, out Vector2 pos, out DungeonStairs stairs, out bool core)
    {
        pos = default; stairs = null; core = false;
        foreach (var s in DungeonStairs.All)
            if (s != null && s.goesDown && s.layerIndex == layer) { stairs = s; pos = s.transform.position; return true; }
        var c = Object.FindFirstObjectByType<DungeonCore>();
        var layers = DungeonLayers.Instance;
        if (c != null && (layers == null || layers.IndexAt(c.transform.position) == layer)) { core = true; pos = c.transform.position; return true; }
        return false;
    }

    /// <summary>Entrée de l'étage (d'où viennent les intrus).</summary>
    private static Vector2 EntranceOf(int layer)
    {
        foreach (var s in DungeonStairs.All)
            if (s != null && !s.goesDown && s.layerIndex == layer) return s.transform.position;
        var t = DungeonTerritory.Instance;
        var b = DungeonBuilder.Instance;
        if (t != null && b != null) return b.floorTilemap.GetCellCenterWorld(t.EntranceCell);
        return Vector2.zero;
    }

    /// <summary>Poste du boss : dans la salle, quelques pas avant ce qu'il garde, du côté d'où l'on arrive.</summary>
    public static Vector2 Lair(int layer, Vector2 target)
    {
        Vector2 from = EntranceOf(layer);
        Vector2 dir = (from - target).sqrMagnitude > 0.01f ? (from - target).normalized : Vector2.down;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        for (float d = 2.6f; d >= 1f; d -= 0.4f)
        {
            Vector2 p = target + dir * d;
            if (graph == null) return p;
            var nn = graph.GetNearest(p, NNConstraint.Default);
            if (nn.node != null && nn.node.Walkable && Vector2.Distance((Vector3)nn.node.position, p) < 0.8f) return (Vector3)nn.node.position;
        }
        return target + dir * 1.5f;
    }

    /// <summary>Mana total : la promotion, plus les évolutions restantes jusqu'à la forme ultime.</summary>
    public static int Cost(GobelinAI g)
    {
        int cost = MonsterMind.GuardianCost;
        var u = g != null ? g.GetComponent<MonsterUnit>() : null;
        var stage = u != null ? u.nextStage : null;
        int guard = 0;
        while (stage != null && guard++ < 5)
        {
            var su = stage.GetComponent<MonsterUnit>();
            cost += u != null ? u.EffectiveEvolveCost : 0;
            u = su;
            stage = su != null ? su.nextStage : null;
        }
        return Mathf.RoundToInt(cost * Talents.BossCostMult);
    }

    /// <summary>Évolue jusqu'à la forme ultime, sans condition (la promotion paie tout d'un coup).</summary>
    public static GobelinAI EvolveToFinal(GobelinAI g)
    {
        var u = g.GetComponent<MonsterUnit>();
        int guard = 0;
        while (u != null && u.nextStage != null && guard++ < 5)
        {
            var next = u.EvolveFree();
            if (next == null) break;
            u = next;
        }
        return u != null ? u.GetComponent<GobelinAI>() : g;
    }

    /// <summary>Installe le boss (après promotion ou au chargement) : poste, phases, sceau.</summary>
    public static void Setup(MonsterMind m, bool placeAtLair)
    {
        if (m == null || CrawlerMode.Active) return;
        var g = m.GetComponent<GobelinAI>();
        if (g == null) return;
        int layer = DungeonLayers.Instance != null ? DungeonLayers.Instance.IndexAt(g.transform.position) : 0;
        if (Target(layer, out var target, out var stairs, out bool core))
        {
            if (placeAtLair)
            {
                Vector2 lair = Lair(layer, target);
                Fx.Play("smoke_puff", g.transform.position, 1f);
                g.transform.position = lair;
                var rb = g.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = lair;
                Fx.Play("smoke_puff", lair, 1.2f);
                g.maxHealth *= Talents.BossHpMult;
                g.currentHealth = g.maxHealth;
            }
            BossSeal.Create(g, stairs, core ? Object.FindFirstObjectByType<DungeonCore>() : null, target);
        }
        g.SetBehavior(GobelinAI.GoblinBehavior.Stay);
        var life = g.GetComponent<MonsterLife>();
        if (life != null) life.enabled = false;   // un boss ne quitte pas son poste pour manger ou dormir
        CrawlerBoss.Attach(g, layer);
    }
}

/// <summary>
/// SCEAU DU GARDIEN : un cercle de runes sur l'escalier (ou autour du cristal) gardé par un boss nommé.
/// Tant que le boss vit : l'escalier ne mène nulle part et le cristal ne peut être frappé.
/// </summary>
public class BossSeal : MonoBehaviour
{
    public static readonly List<BossSeal> All = new List<BossSeal>();

    public GobelinAI boss;
    public DungeonStairs stairs;
    public DungeonCore core;
    private NodeLink2 link;
    private float nextCheck, spin;
    private Transform ring;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public static bool IsSealed(DungeonStairs s)
    {
        foreach (var b in All) if (b != null && b.stairs != null && (b.stairs == s || b.stairs.partner == s)) return true;
        return false;
    }

    public static bool CoreSealed(DungeonCore c)
    {
        foreach (var b in All) if (b != null && b.core == c && c != null) return true;
        return false;
    }

    public static void Create(GobelinAI boss, DungeonStairs stairs, DungeonCore core, Vector2 at)
    {
        foreach (var b in All) if (b != null && b.boss == boss) return;
        var go = new GameObject("Sceau du gardien");
        go.transform.position = at;
        var s = go.AddComponent<BossSeal>();
        s.boss = boss;
        s.stairs = stairs;
        s.core = core;
        if (stairs != null)
        {
            s.link = stairs.GetComponent<NodeLink2>();
            if (s.link != null) s.link.enabled = false;   // l'escalier ne mène plus nulle part
        }
        // cercle de runes qui tourne lentement, lueur violette
        var ringGo = new GameObject("Runes");
        ringGo.transform.SetParent(go.transform, false);
        var sr = ringGo.AddComponent<SpriteRenderer>();
        sr.sprite = RingSprite();
        sr.color = new Color(0.75f, 0.45f, 1f, 0.85f);
        sr.sortingOrder = 2;
        ringGo.transform.localScale = Vector3.one * (core != null ? 2.4f : 1.6f);
        s.ring = ringGo.transform;
        var l = go.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
        l.lightType = Light2D.LightType.Point;
        l.color = new Color(0.65f, 0.35f, 1f);
        l.intensity = 0.9f;
        l.pointLightOuterRadius = 2.4f;
        NemesisSystem.AddTag(go.transform, "Scellé", new Color(0.8f, 0.6f, 1f));
    }

    private void Update()
    {
        spin += Time.deltaTime * 25f;
        if (ring != null) ring.localRotation = Quaternion.Euler(0, 0, spin);
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.4f;
        if (boss == null || !boss.IsAlive()) Break();
    }

    private void Break()
    {
        if (link != null) link.enabled = true;
        Fx.Play("shine", transform.position, 1.6f, 18f, 82, new Color(0.8f, 0.6f, 1f));
        Sfx.Play("core_hit", transform.position, 0.7f);
        GameHUD.Toast(core != null ? "Le boss est tombé : le sceau du cristal se brise !" : "Le boss est tombé : le sceau de l'escalier se brise", GameHUD.DangerColor);
        Chronicle.Add("Un gardien est tombé : son sceau s'est brisé.");
        Destroy(gameObject);
    }

    private static Sprite ringSprite;

    public static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        float c = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float ang = Mathf.Atan2(y - c, x - c);
                bool outer = d > 13.6f && d < 15.2f;
                bool inner = d > 10.2f && d < 11.2f;
                // runes : petites marques entre les deux cercles, une sur deux
                bool rune = d >= 11.8f && d <= 13f && Mathf.Repeat(ang * 8f / Mathf.PI, 2f) < 0.45f;
                tex.SetPixel(x, y, outer || inner || rune ? Color.white : Color.clear);
            }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 32f);
        return ringSprite;
    }
}

/// <summary>Fenêtre « Nommer un boss » : un champ pour le nom (ou une suggestion), le coût, et la confirmation.</summary>
public class NameBossDialog : MonoBehaviour
{
    private static NameBossDialog instance;
    private static readonly string[] Suggestions =
    {
        "Grakh", "Morvenna", "Vorth", "Sélène", "Kharn", "Nyx", "Brum", "Azhra", "Tarrasque", "Mordu", "Isolde", "Ossegrince", "Croc-Noir", "Vélyne", "Ombrefiel",
    };

    public static void Open(GobelinAI g)
    {
        if (g == null) return;
        if (!MonsterMind.CanPromote(g, out string why)) { GameHUD.Toast(why, GameHUD.WarningColor); return; }
        if (instance != null) Destroy(instance.gameObject);
        var go = new GameObject("NameBossDialog");
        instance = go.AddComponent<NameBossDialog>();
        instance.Build(g);
    }

    private void Build(GobelinAI g)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 170;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        gameObject.AddComponent<GraphicRaycaster>();
        var root = UIKit.Stretch(UIKit.Rect("Root", transform));
        var blocker = UIKit.Blocker(root, 0.55f);
        blocker.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var win = UIKit.PanelBox("Fenêtre", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 520));
        win.pivot = new Vector2(0.5f, 0.5f);
        var title = UIKit.LabelAt(win, "Nommer un boss d'étage", 36, UIKit.Gold, new Vector2(30, -22), new Vector2(820, 48));
        UIKit.StyleTitle(title);
        var u = g.GetComponent<MonsterUnit>();
        string fam = u != null ? u.familyName : g.gobelinName;
        var desc = UIKit.LabelAt(win,
            $"{fam} évoluera aussitôt jusqu'à sa forme ultime et deviendra le gardien de cet étage (un seul par étage).\n" +
            "Il prendra son poste devant l'escalier qui descend — ou devant le cristal — et un sceau fermera le passage : seule sa mort l'ouvrira.\n" +
            "Il combat en trois phases : rage et renforts, puis ondes de choc.", 20, UIKit.Sub, new Vector2(30, -82), new Vector2(840, 150));
        desc.textWrappingMode = TextWrappingModes.Normal;
        var input = UIKit.Input(win, "Son nom...", new Vector2(0, 1), new Vector2(30, -250), new Vector2(560, 70), 30);
        input.text = Suggestions[Random.Range(0, Suggestions.Length)];
        UIKit.BtnAt(win, "Autre nom", () => input.text = Suggestions[Random.Range(0, Suggestions.Length)], new Vector2(0, 1), new Vector2(610, -250), new Vector2(260, 70), 22);
        int cost = FloorBoss.Cost(g);
        UIKit.BtnAt(win, $"Nommer ({cost} mana)", () =>
        {
            string name = string.IsNullOrWhiteSpace(input.text) ? Suggestions[0] : input.text.Trim();
            if (MonsterMind.Promote(g, name)) Close();
        }, new Vector2(0.5f, 0), new Vector2(150, 30), new Vector2(380, 76), 24, new Color(0.6f, 0.45f, 0.12f, 1f));
        UIKit.BtnAt(win, "Annuler", Close, new Vector2(0.5f, 0), new Vector2(-250, 30), new Vector2(260, 76), 24);
    }

    private void Close()
    {
        PointerUtil.BlockWorldInput(0.2f);
        Destroy(gameObject);
    }
}
