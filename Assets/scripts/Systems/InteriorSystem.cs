using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// INTÉRIEURS : un clic (ou un toucher) sur un bâtiment achevé de la colonie qui a un intérieur
/// (Taverne, Forge, Hall de la Guilde, Tour des mages) fait entrer la caméra dedans.
/// Les intérieurs sont assemblés à la volée loin de l'île (mobilier, murs, sol, habitants animés, lumières)
/// à partir de Resources/Interiors (Tools~/crop_interiors.py). Le jeu est en pause pendant la visite.
/// Chaque lieu a sa fonction : rumeurs (taverne), registre de la Guilde, équipement (forge), bestiaire (mages).
/// MODE AVENTURIER : on y entre physiquement (EnterPhysical) — on marche dans la pièce (murs et meubles solides),
/// on parle aux habitants (points d'interaction) et on ressort par la porte.
/// </summary>
public class InteriorSystem : MonoBehaviour
{
    public static InteriorSystem Instance { get; private set; }

    public bool Inside => current != null;
    public string CurrentId => current?.id;
    public event Action<string> OnEntered, OnExited;

    private class Prop { public string sprite; public float x, y; public int order = 3; public bool flip; public float scale = 1f; }
    private class Npc { public string anim; public float x, y; public float fps = 7f; public bool flip; }
    /// <summary>Point d'interaction du mode Aventurier (habitant, tableau...).</summary>
    public class Spot { public string key, label; public Vector3 local; public Vector3 World; }
    private class Layout
    {
        public string id, title, floor, wall;
        public float w = 12f, h = 7f;
        public Color light = new Color(1f, 0.78f, 0.55f);
        public readonly List<Prop> props = new List<Prop>();
        public readonly List<Npc> npcs = new List<Npc>();
        public readonly List<Vector3> lamps = new List<Vector3>();   // x, y, rayon
        public readonly List<Spot> spots = new List<Spot>();
        /// <summary>Pièce complète d'un pack (Tools~/make_stamps.py, intérieurs) à la place du sol, du mur et des meubles posés à la main.</summary>
        public string stamp;
        /// <summary>Habitants à qui parler dans une pièce-tampon : repérés par le nom de leur couche, sinon position relative (0..1).</summary>
        public readonly List<(string key, string label, string near, Vector2 fallback)> stampSpots = new List<(string, string, string, Vector2)>();
        public Vector2 doorLocal = new Vector2(float.NaN, 0f);
    }

    private static readonly Dictionary<string, Layout> Layouts = BuildLayouts();
    private readonly Dictionary<string, GameObject> built = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite[]> anims = new Dictionary<string, Sprite[]>();
    private Layout current;
    private Vector3 camBack;
    private float camSizeBack;
    private bool reliefBack;
    private Canvas canvas;
    private Image fade;
    private RectTransform panel;
    private Vector2 pressPos;
    private float pressTime;
    private bool pressing;

    public static bool HasInterior(string buildingId) => Layouts.ContainsKey(buildingId);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        foreach (var s in Resources.LoadAll<Sprite>("Interiors"))
        {
            sprites[s.name] = s;
            int us = s.name.LastIndexOf('_');
            if (us > 0 && int.TryParse(s.name.Substring(us + 1), out _))
            {
                string key = s.name.Substring(0, us);
                anims.TryGetValue(key, out var arr);
                var l = arr != null ? new List<Sprite>(arr) : new List<Sprite>();
                l.Add(s);
                anims[key] = l.ToArray();
            }
        }
        foreach (var k in new List<string>(anims.Keys))
            Array.Sort(anims[k], (a, b) => Index(a.name).CompareTo(Index(b.name)));
        BuildUI();
    }

    private static int Index(string n) => int.TryParse(n.Substring(n.LastIndexOf('_') + 1), out int i) ? i : 0;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // ─────────────────────────────────────────────
    // PLANS DES INTÉRIEURS
    // ─────────────────────────────────────────────

    private static Dictionary<string, Layout> BuildLayouts()
    {
        var d = new Dictionary<string, Layout>();
        Layout L(string id, string title, string floor, string wall, float w, float h)
        {
            var l = new Layout { id = id, title = title, floor = floor, wall = wall, w = w, h = h };
            d[id] = l;
            return l;
        }
        void P(Layout l, string s, float x, float y, int order = 3, bool flip = false) => l.props.Add(new Prop { sprite = s, x = x, y = y, order = order, flip = flip });
        void N(Layout l, string a, float x, float y, float fps = 7f, bool flip = false) => l.npcs.Add(new Npc { anim = a, x = x, y = y, fps = fps, flip = flip });
        void S(Layout l, string key, string label, float x, float y) => l.spots.Add(new Spot { key = key, label = label, local = new Vector3(x, y, 0f) });

        // Taverne : comptoir, tables, barde, buveurs
        var t = L("house_1", "La Taverne du Donjon", "floor_wood", "wall_tavern", 12f, 7f);
        P(t, "tav_rug", 0f, -0.6f, 1);
        P(t, "tav_shelf_bottles", -3.6f, 2.1f); P(t, "tav_shelf", -1.9f, 2.1f); P(t, "tav_stairs", 5f, 1.2f);
        P(t, "tav_bar", -2.8f, 0.9f);
        P(t, "tav_barrel_big", -5.2f, 1.5f); P(t, "tav_barrel", -5.3f, 0.4f);
        P(t, "tav_long_table", 1.6f, -0.2f); P(t, "tav_round_table2", 3.6f, -2.2f); P(t, "tav_table_cloth2", -1.2f, -2.4f);
        P(t, "tav_booth", 4.4f, 0.4f); P(t, "tav_plant", 5.4f, -2.6f); P(t, "tav_plant", -5.4f, -2.6f); P(t, "tav_jugs", 1.6f, 0.15f, 4);
        N(t, "npc_host", -2.8f, 1.35f, 6f); N(t, "npc_bard", 0.2f, -1.7f, 6f); N(t, "npc_drinker1", 1.1f, 0.25f, 5f);
        N(t, "npc_drinker2", 2.2f, 0.25f, 5f, true); N(t, "npc_dancer", -3.2f, -1.2f, 8f); N(t, "npc_sleeper", 3.6f, -1.9f, 4f);
        t.lamps.Add(new Vector3(-3f, 1.5f, 5f)); t.lamps.Add(new Vector3(2.5f, -1f, 6f));
        S(t, "tavern_host", "Parler à la tavernière", -2.8f, 0.1f); S(t, "tavern_bard", "Écouter le barde", 0.2f, -2.3f);
        S(t, "tavern_drinkers", "Trinquer avec les buveurs", 1.6f, -0.75f);

        // Hall de la Guilde : tableau des contrats, maître de guilde, lecteurs
        var g = L("house_4", "Hall de la Guilde des Aventuriers", "floor_wood", "wall_guild", 12f, 7f);
        P(g, "gld_rug", 0f, -0.8f, 1);
        P(g, "gld_board", -1.3f, 2.1f); P(g, "gld_board2", 1.3f, 2.1f); P(g, "gld_banner", 0f, 2.35f);
        P(g, "gld_bookshelf", -4.6f, 2.0f); P(g, "gld_cabinet", 4.6f, 2.0f); P(g, "gld_armor", 3.0f, 2.0f);
        P(g, "gld_weapons", -3.0f, 2.0f); P(g, "gld_long_desk", 0f, 0.3f); P(g, "gld_desk", -4.2f, -1.6f); P(g, "gld_chest", 4.6f, -2.2f);
        P(g, "gld_dragon_skull", 4.2f, -0.6f); P(g, "gld_vase", -5.4f, -2.6f); P(g, "gld_vase", 5.5f, 0.9f);
        N(g, "npc_guildmaster", 0f, 0.75f, 6f); N(g, "npc_talkers", 2.3f, -1.8f, 5f); N(g, "npc_reader", -4.2f, -1.2f, 5f);
        g.lamps.Add(new Vector3(0f, 1f, 6f)); g.lamps.Add(new Vector3(-4.2f, -1f, 4f));
        S(g, "guild_master", "Guichet du maître de guilde", 0f, -0.35f); S(g, "guild_board", "Tableau des recrues (former un groupe)", -1.3f, 1.55f);
        S(g, "guild_talkers", "Écouter les aventuriers", 2.3f, -2.4f);

        // Forge : fourneau, enclume, trempe
        var f = L("house_3", "La Forge", "floor_stone", "wall_smith", 11f, 6.5f);
        f.light = new Color(1f, 0.62f, 0.35f);
        N(f, "fx_forge", -3.6f, 1.2f, 9f);
        P(f, "smt_anvil", -1.2f, 0.2f); P(f, "smt_quench", 1.2f, 0.6f); P(f, "smt_rack", 3.6f, 1.9f); P(f, "smt_hide", 1.6f, 1.9f);
        P(f, "smt_workbench", 2.8f, -1.8f); P(f, "smt_grindstone", -1.4f, -2.2f); P(f, "smt_barrels", 4.6f, 0.2f);
        P(f, "smt_coal", -4.6f, -1.2f); P(f, "smt_wood", -3.2f, -2.2f); P(f, "smt_armor_crate", 4.4f, -0.9f);
        N(f, "npc_apprentice", -0.2f, 0.9f, 7f);
        f.lamps.Add(new Vector3(-3.6f, 1f, 6f));
        S(f, "forge_smith", "Parler à la forgeronne", -0.2f, 0.25f); S(f, "forge_rack", "Râtelier d'armes", 3.6f, 1.35f);

        // Tour des mages : bibliothèques, cercle, cristaux
        var m = L("tent_magic", "La Tour des Mages", "floor_stone", "wall_mage", 10f, 7f);
        m.light = new Color(0.65f, 0.7f, 1f);
        P(m, "mag_circle", 0f, -0.6f, 1); P(m, "mag_rug", 0f, -2.4f, 1);
        P(m, "mag_bookshelves", -2.2f, 2.0f); P(m, "mag_bookshelf", 2.6f, 2.0f); P(m, "mag_shelf", 4.3f, 1.9f);
        P(m, "mag_orb", 0f, -0.5f); P(m, "mag_crystal", -3.2f, -0.8f); P(m, "mag_hourglass", 3.2f, -0.8f); P(m, "mag_skull", -4.2f, 0.6f);
        P(m, "mag_desk", 2.6f, -2.2f);
        N(m, "npc_reader", 2.6f, -1.75f, 5f);
        m.lamps.Add(new Vector3(0f, -0.4f, 5f));
        S(m, "mage_seller", "Parler à l'apothicaire", 2.6f, -2.75f); S(m, "mage_orb", "Consulter l'orbe de scrutation", 0f, -1.2f);
        // pièces complètes des packs : bibliothèque, prison, manoir, maison du héros
        Layout ST(string id, string title, string stamp)
        {
            var l = new Layout { id = id, title = title, stamp = stamp, w = 10f, h = 8f };
            d[id] = l;
            return l;
        }
        var lib = ST("library", "La Bibliothèque", "int_library");
        lib.stampSpots.Add(("library_keeper", "Parler au bibliothécaire", "Librarian", new Vector2(0.5f, 0.75f)));
        var pri = ST("prison", "La Prison", "int_prison");
        pri.light = new Color(0.85f, 0.85f, 1f);
        pri.stampSpots.Add(("prison_chief", "Parler au chef des gardes", "Chief", new Vector2(0.75f, 0.7f)));
        var man = ST("manor", "Le Manoir des nobles", "int_manor");
        man.stampSpots.Add(("manor_lord", "Saluer le seigneur", "Characters1", new Vector2(0.5f, 0.7f)));
        var hom = ST("hero_home", "Votre maison", "int_home");
        hom.stampSpots.Add(("home_bed", "Dormir (gratuit)", null, new Vector2(0.5f, 0.6f)));
        return d;
    }

    // ─────────────────────────────────────────────
    // ENTRER / SORTIR
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (Inside)
        {
            if (Physical) { PhysicalTick(); return; }
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Exit();
            return;
        }
        DetectBuildingTap();
    }

    private bool CanEnter()
    {
        if (MainMenu.Instance != null && (MainMenu.Instance.InHome || MainMenu.Instance.InPause)) return false;
        if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying) return false;
        if (StoryManager.Instance != null && StoryManager.Instance.IsShowing) return false;
        if (PossessionController.Instance != null && PossessionController.Instance.IsPossessing) return false;
        if (DungeonBuilder.Instance != null && DungeonBuilder.Instance.GetCurrentMode() != DungeonBuilder.BuildMode.None) return false;
        return true;
    }

    /// <summary>Toucher court (sans glisser) sur un bâtiment achevé qui a un intérieur.</summary>
    private void DetectBuildingTap()
    {
        var p = Pointer.current;
        if (p == null || !CanEnter() || CrawlerMode.Active) return;
        if (p.press.wasPressedThisFrame && !PointerUtil.IsOverUI())
        {
            pressing = true;
            pressPos = p.position.ReadValue();
            pressTime = Time.unscaledTime;
        }
        if (!p.press.wasReleasedThisFrame || !pressing) return;
        pressing = false;
        if (Time.unscaledTime - pressTime > 0.35f || Vector2.Distance(pressPos, p.position.ReadValue()) > 12f) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 world = cam.ScreenToWorldPoint(pressPos);
        var colony = SettlementManager.Instance;
        if (colony == null) return;
        foreach (var b in colony.Buildings)
        {
            if (b == null || !b.IsComplete || !HasInterior(b.def.id)) continue;
            if (b.StampVisual != null) { if (!b.StampVisual.Contains(world)) continue; }
            else
            {
                var sr = b.GetComponentInChildren<SpriteRenderer>();
                if (sr == null || !sr.bounds.Contains(new Vector3(world.x, world.y, sr.bounds.center.z))) continue;
            }
            Enter(b.def.id);
            return;
        }
    }

    public void Enter(string id)
    {
        if (Inside || !Layouts.TryGetValue(id, out var layout)) return;
        StartCoroutine(EnterRoutine(layout));
    }

    private System.Collections.IEnumerator EnterRoutine(Layout layout)
    {
        current = layout;
        GameManager.Instance?.PushPause();
        yield return FadeTo(1f, 0.35f);
        var root = Build(layout);
        root.SetActive(true);
        var cam = Camera.main;
        camBack = cam.transform.position;
        camSizeBack = cam.orthographicSize;
        reliefBack = MountainRelief.Instance == null || MountainRelief.Instance.UserDungeonView;
        if (CameraController.Instance != null) CameraController.Instance.cinematic = true;
        // la pièce entière (mur du fond compris) tient à gauche ; l'encart d'informations occupe la droite
        float size = Mathf.Max(layout.h * 0.72f + 0.6f, layout.w * 1.42f / cam.aspect * 0.5f);
        cam.orthographicSize = size;
        cam.transform.position = new Vector3(root.transform.position.x + size * cam.aspect * 0.28f, root.transform.position.y + 0.5f, cam.transform.position.z);
        GameHUD.Instance?.SetPanelsVisible(false);
        ShowPanel(layout);
        Sfx.Play("door_break", null, 0.35f);
        yield return FadeTo(0f, 0.35f);
        OnEntered?.Invoke(layout.id);
        Greet(layout.id);
    }

    // ─────────────────────────────────────────────
    // MODE AVENTURIER : ENTRER À PIED
    // ─────────────────────────────────────────────

    /// <summary>On marche dans la pièce (pas de pause, pas d'encart) ; l'intérieur est construit à la volée.</summary>
    public bool Physical { get; private set; }
    public IReadOnlyList<Spot> Spots => current != null ? current.spots : (IReadOnlyList<Spot>)Array.Empty<Spot>();
    /// <summary>Rectangle (monde) de la pièce visitée : la caméra reste dedans.</summary>
    public Rect RoomRect { get; private set; }
    public Vector3 DoorInside { get; private set; }
    public string CurrentTitle => current?.title;
    public bool Moving => moving;
    private Vector3 outsideDoor;
    private List<Transform> walkers = new List<Transform>();
    private bool moving;

    public void EnterPhysical(string id, Vector3 outside, List<Transform> party)
    {
        if (Inside || moving || !Layouts.TryGetValue(id, out var layout)) return;
        StartCoroutine(EnterPhysicalRoutine(layout, outside, party));
    }

    private System.Collections.IEnumerator EnterPhysicalRoutine(Layout layout, Vector3 outside, List<Transform> party)
    {
        moving = true;
        outsideDoor = outside;
        walkers = party;
        yield return FadeTo(1f, 0.25f);
        var root = Build(layout);
        root.SetActive(true);
        current = layout;
        Physical = true;
        Vector3 o = root.transform.position;
        RoomRect = new Rect(o.x - layout.w / 2f, o.y - layout.h / 2f, layout.w, layout.h + 1.5f);
        DoorInside = o + (float.IsNaN(layout.doorLocal.x) ? new Vector3(0f, -layout.h / 2f + 0.55f, 0f) : new Vector3(layout.doorLocal.x, layout.doorLocal.y + 0.25f, 0f));
        Place(DoorInside);
        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(DoorInside.x, DoorInside.y + 1.5f, cam.transform.position.z);
        Sfx.Play("door_break", null, 0.35f);
        yield return FadeTo(0f, 0.25f);
        moving = false;
        OnEntered?.Invoke(layout.id);
    }

    private void Place(Vector3 at)
    {
        for (int i = 0; i < walkers.Count; i++)
        {
            var t = walkers[i];
            if (t == null) continue;
            Vector3 p = at + (i == 0 ? Vector3.zero : new Vector3((i % 2 == 1 ? -0.7f : 0.7f) * ((i + 1) / 2), -0.1f, 0f));
            t.position = p;
            var rb = t.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
        }
    }

    private void PhysicalTick()
    {
        if (moving || walkers.Count == 0 || walkers[0] == null) return;
        // on ressort en franchissant le seuil
        Vector3 p = walkers[0].position;
        if (p.y < DoorInside.y - 0.45f && Mathf.Abs(p.x - DoorInside.x) < 1f) ExitPhysical();
    }

    public void ExitPhysical()
    {
        if (!Inside || !Physical || moving) return;
        StartCoroutine(ExitPhysicalRoutine());
    }

    private System.Collections.IEnumerator ExitPhysicalRoutine()
    {
        moving = true;
        var was = current;
        yield return FadeTo(1f, 0.25f);
        if (built.TryGetValue(was.id, out var root)) root.SetActive(false);
        Place(outsideDoor);
        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(outsideDoor.x, outsideDoor.y, cam.transform.position.z);
        current = null;
        Physical = false;
        Sfx.Play("door_break", null, 0.3f);
        yield return FadeTo(0f, 0.25f);
        moving = false;
        OnExited?.Invoke(was.id);
    }

    /// <summary>Cinématique : la pièce est construite et affichée (sans le joueur ni l'encart).</summary>
    public Transform ShowForCinematic(string id)
    {
        if (!Layouts.TryGetValue(id, out var layout)) return null;
        var root = Build(layout);
        root.SetActive(true);
        return root.transform;
    }

    public void HideCinematic(string id)
    {
        if (current != null && current.id == id) return;   // le joueur y est : on ne cache rien
        if (built.TryGetValue(id, out var root)) root.SetActive(false);
    }

    public void Exit()
    {
        if (Physical) { ExitPhysical(); return; }
        if (!Inside) return;
        StartCoroutine(ExitRoutine());
    }

    private System.Collections.IEnumerator ExitRoutine()
    {
        var was = current;
        yield return FadeTo(1f, 0.3f);
        if (panel != null) Destroy(panel.gameObject);
        if (built.TryGetValue(was.id, out var root)) root.SetActive(false);
        var cam = Camera.main;
        cam.transform.position = camBack;
        cam.orthographicSize = camSizeBack;
        if (CameraController.Instance != null) CameraController.Instance.cinematic = false;
        MountainRelief.Instance?.SetUserView(reliefBack);
        GameHUD.Instance?.SetPanelsVisible(true);
        current = null;
        GameManager.Instance?.PopPause();
        yield return FadeTo(0f, 0.3f);
        OnExited?.Invoke(was.id);
    }

    // ─────────────────────────────────────────────
    // CONSTRUCTION DE LA PIÈCE
    // ─────────────────────────────────────────────

    private GameObject Build(Layout l)
    {
        if (built.TryGetValue(l.id, out var existing)) return existing;
        int k = built.Count;
        var root = new GameObject("Intérieur_" + l.id);
        root.transform.position = new Vector3(600f + k * 40f, 600f, 0f);
        Vector3 o = root.transform.position;
        var stamp = string.IsNullOrEmpty(l.stamp) ? null : SettlementArt.Get()?.GetStamp(l.stamp);
        if (stamp != null) return BuildStamp(l, stamp, root, o);

        // fond noir autour de la pièce
        var back = Quad(root.transform, "Fond", new Color(0.03f, 0.02f, 0.04f), new Vector2(l.w + 30f, l.h + 30f), Vector2.zero, -10);

        // sol répété
        if (sprites.TryGetValue(l.floor, out var fl))
        {
            var sr = new GameObject("Sol").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root.transform, false);
            sr.sprite = fl;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(l.w, l.h);
            sr.sortingOrder = 0;
        }
        // mur du fond (bandeau répété sur toute la largeur)
        if (sprites.TryGetValue(l.wall, out var wall))
        {
            float ww = wall.bounds.size.x, wh = wall.bounds.size.y;
            int n = Mathf.CeilToInt(l.w / ww);
            float start = -l.w / 2f;
            for (int i = 0; i < n; i++)
            {
                var sr = new GameObject("Mur").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root.transform, false);
                sr.sprite = wall;
                sr.sortingOrder = 2;
                float x = start + i * ww + ww / 2f;
                sr.transform.localPosition = new Vector3(x, l.h / 2f - 0.05f, 0f);
                // la bande est posée par le bas : le dernier morceau est rogné par un cache noir à droite
            }
            Quad(root.transform, "CacheDroit", new Color(0.03f, 0.02f, 0.04f), new Vector2(ww, wh + 1f), new Vector2(l.w / 2f + ww / 2f, l.h / 2f + wh / 2f), 3);
        }
        // bordures latérales et basse (épaisseur de mur sombre)
        var edge = new Color(0.12f, 0.08f, 0.07f);
        Quad(root.transform, "BordG", edge, new Vector2(0.35f, l.h + 1.6f), new Vector2(-l.w / 2f - 0.17f, 0.8f), 4);
        Quad(root.transform, "BordD", edge, new Vector2(0.35f, l.h + 1.6f), new Vector2(l.w / 2f + 0.17f, 0.8f), 4);
        Quad(root.transform, "BordBasG", edge, new Vector2(l.w / 2f - 0.8f, 0.35f), new Vector2(-l.w / 4f - 0.4f, -l.h / 2f - 0.17f), 4);
        Quad(root.transform, "BordBasD", edge, new Vector2(l.w / 2f - 0.8f, 0.35f), new Vector2(l.w / 4f + 0.4f, -l.h / 2f - 0.17f), 4);

        // murs solides (mode Aventurier) : la porte est au milieu du bas
        Solid(root.transform, "MurHaut", new Vector2(0f, l.h / 2f + 0.25f), new Vector2(l.w + 2f, 0.9f));
        Solid(root.transform, "MurGauche", new Vector2(-l.w / 2f - 0.4f, 0f), new Vector2(0.8f, l.h + 2f));
        Solid(root.transform, "MurDroit", new Vector2(l.w / 2f + 0.4f, 0f), new Vector2(0.8f, l.h + 2f));
        Solid(root.transform, "MurBasG", new Vector2(-l.w / 4f - 0.45f, -l.h / 2f - 0.4f), new Vector2(l.w / 2f - 0.9f, 0.8f));
        Solid(root.transform, "MurBasD", new Vector2(l.w / 4f + 0.45f, -l.h / 2f - 0.4f), new Vector2(l.w / 2f - 0.9f, 0.8f));
        Solid(root.transform, "Seuil", new Vector2(0f, -l.h / 2f - 1.2f), new Vector2(2f, 0.6f));
        // paillasson de sortie
        Quad(root.transform, "Paillasson", new Color(0.35f, 0.2f, 0.12f, 0.9f), new Vector2(1.3f, 0.35f), new Vector2(0f, -l.h / 2f + 0.1f), 1);

        foreach (var p in l.props)
        {
            if (!sprites.TryGetValue(p.sprite, out var s)) continue;
            var sr = new GameObject(p.sprite).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root.transform, false);
            sr.transform.localPosition = new Vector3(p.x, p.y, 0f);
            sr.sprite = s;
            sr.flipX = p.flip;
            sr.sortingOrder = p.order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (p.order >= 3) FeetSort.Attach(sr.gameObject);   // meuble trié à son pied (le comptoir cache qui se tient derrière)
            // meubles debout : leur pied est solide (les tapis et cercles au sol ne le sont pas)
            if (p.order >= 3 && p.y < l.h / 2f - 0.6f)
            {
                var col = sr.gameObject.AddComponent<BoxCollider2D>();
                sr.gameObject.layer = 6;
                var bs = s.bounds.size;
                col.size = new Vector2(bs.x * 0.8f, Mathf.Clamp(bs.y * 0.3f, 0.25f, 0.6f));
                col.offset = new Vector2(0f, s.bounds.min.y + col.size.y * 0.5f);
            }
        }
        foreach (var n in l.npcs)
        {
            if (!anims.TryGetValue(n.anim, out var frames) || frames.Length == 0) continue;
            var go = new GameObject(n.anim);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(n.x, n.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.flipX = n.flip;
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (!n.anim.StartsWith("fx_")) FeetSort.Attach(go);   // habitant trié à ses pieds
            var a = go.AddComponent<LoopAnim>();
            a.frames = frames;
            a.fps = n.fps;
            a.phase = UnityEngine.Random.value * 10f;
            if (!n.anim.StartsWith("fx_"))
            {
                var col = go.AddComponent<CircleCollider2D>();
                go.layer = 6;
                col.radius = 0.22f;
                col.offset = new Vector2(0f, 0.2f);
            }
        }
        foreach (var sp in l.spots) sp.World = o + sp.local;
        // lumière d'ambiance (la nuit ne doit pas plonger la pièce dans le noir) + lampes chaudes
        var amb = new GameObject("Ambiance").AddComponent<Light2D>(); Light2DCleanup.Attach(amb);
        amb.transform.SetParent(root.transform, false);
        amb.lightType = Light2D.LightType.Point;
        amb.pointLightOuterRadius = Mathf.Max(l.w, l.h) * 0.9f;
        amb.pointLightInnerRadius = Mathf.Max(l.w, l.h) * 0.5f;
        amb.intensity = 0.9f;
        amb.color = Color.Lerp(Color.white, l.light, 0.35f);
        foreach (var lp in l.lamps)
        {
            var li = new GameObject("Lampe").AddComponent<Light2D>(); Light2DCleanup.Attach(li);
            li.transform.SetParent(root.transform, false);
            li.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
            li.lightType = Light2D.LightType.Point;
            li.pointLightOuterRadius = lp.z;
            li.pointLightInnerRadius = 0.5f;
            li.intensity = 0.9f;
            li.color = l.light;
            li.gameObject.AddComponent<TorchFlicker>();
        }
        root.SetActive(false);
        built[l.id] = root;
        return root;
    }

    /// <summary>Pièce-tampon : la scène d'intérieur du pack, murs solides d'après ses cases de sol, sortie par le bas.</summary>
    private GameObject BuildStamp(Layout l, SettlementArt.Stamp st, GameObject root, Vector3 o)
    {
        l.w = st.size.x; l.h = st.size.y;
        var bl = new Vector2(-l.w / 2f, -l.h / 2f);
        l.doorLocal = bl + st.door;
        Quad(root.transform, "Fond", new Color(0.03f, 0.02f, 0.04f), new Vector2(l.w + 30f, l.h + 30f), Vector2.zero, -10);
        var v = StampVisual.Create(st, root.transform, o + (Vector3)bl, true);
        foreach (var r in st.solids) Solid(root.transform, "Mur", bl + r.center, r.size);
        // on ne sort de la pièce que par la porte
        Solid(root.transform, "MurHaut", new Vector2(0f, l.h / 2f + 0.4f), new Vector2(l.w + 2f, 0.8f));
        Solid(root.transform, "MurGauche", new Vector2(-l.w / 2f - 0.4f, 0f), new Vector2(0.8f, l.h + 2f));
        Solid(root.transform, "MurDroit", new Vector2(l.w / 2f + 0.4f, 0f), new Vector2(0.8f, l.h + 2f));
        Solid(root.transform, "Seuil", new Vector2(l.doorLocal.x, l.doorLocal.y - 1.4f), new Vector2(2.6f, 0.6f));
        l.spots.Clear();
        foreach (var (key, label, near, fb) in l.stampSpots)
        {
            Vector2 pos = bl + new Vector2(fb.x * l.w, fb.y * l.h);
            if (!string.IsNullOrEmpty(near))
                foreach (var pc in st.pieces)
                    if (pc.names != null && pc.names.Contains(near)) { pos = bl + pc.pos + new Vector2(0f, -0.45f); break; }
            l.spots.Add(new Spot { key = key, label = label, local = pos, World = o + (Vector3)pos });
        }
        var amb = new GameObject("Ambiance").AddComponent<Light2D>(); Light2DCleanup.Attach(amb);
        amb.transform.SetParent(root.transform, false);
        amb.lightType = Light2D.LightType.Point;
        amb.pointLightOuterRadius = Mathf.Max(l.w, l.h) * 0.9f;
        amb.pointLightInnerRadius = Mathf.Max(l.w, l.h) * 0.5f;
        amb.intensity = 1f;
        amb.color = Color.Lerp(Color.white, l.light, 0.3f);
        root.SetActive(false);
        built[l.id] = root;
        return root;
    }

    private static void Solid(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.layer = 6;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    private static GameObject Quad(Transform parent, string name, Color c, Vector2 size, Vector2 pos, int order)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        sr.transform.localPosition = pos;
        sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        sr.sprite = SpriteUtil.Square;
        sr.color = c;
        sr.sortingOrder = order;
        return sr.gameObject;
    }

    // ─────────────────────────────────────────────
    // INTERFACE
    // ─────────────────────────────────────────────

    private void BuildUI()
    {
        var go = new GameObject("Interior_Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();
        fade = UIKit.Image(UIKit.Stretch(UIKit.Rect("Fondu", go.transform)), new Color(0f, 0f, 0f, 0f), false, false);
        fade.raycastTarget = false;
    }

    private System.Collections.IEnumerator FadeTo(float a, float t)
    {
        float from = fade.color.a;
        for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / t)
        {
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, a, k));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, a);
    }

    private void ShowPanel(Layout l)
    {
        panel = UIKit.Stretch(UIKit.Rect("Panneau", canvas.transform));
        var title = UIKit.LabelAt(panel, l.title, 44, UIKit.Gold, new Vector2(0, -24), new Vector2(1200, 60), TextAlignmentOptions.Center, new Vector2(0.5f, 1));
        title.rectTransform.pivot = new Vector2(0.5f, 1);
        title.fontStyle = FontStyles.SmallCaps;
        var exit = UIKit.BtnAt(panel, "Sortir", Exit, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(260, 70), 30, UIKit.Accent);
        ((RectTransform)exit.transform).pivot = new Vector2(0.5f, 0);

        // encart d'informations à droite
        var box = UIKit.PanelBox("Infos", panel, new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(520, 640));
        box.pivot = new Vector2(1, 0.5f);
        string head, body;
        Content(l.id, out head, out body);
        UIKit.LabelAt(box, CrawlerMode.Active ? ShopTitle(l.id) : head, 30, UIKit.Gold, new Vector2(24, -18), new Vector2(480, 44));
        if (CrawlerMode.Active) { CrawlerMode.Instance.BuildShop(box, l.id); return; }
        var t = UIKit.LabelAt(box, body, 21, UIKit.Text, new Vector2(24, -70), new Vector2(476, 560));
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    private static string ShopTitle(string id) =>
        id == "house_1" ? "Le comptoir" : id == "house_3" ? "Le forgeron" : id == "tent_magic" ? "L'apothicaire" : "Le guichet de la Guilde";

    /// <summary>Recrée l'encart (après un achat).</summary>
    public void RefreshPanel()
    {
        // seulement si l'encart plein écran est ouvert : dans un intérieur « physique », la boutique est une fenêtre
        // à part, et recréer l'encart ici en faisait une seconde, impossible à fermer
        if (current == null || panel == null || Physical) return;
        if (panel != null) Destroy(panel.gameObject);
        ShowPanel(current);
    }

    private static void Content(string id, out string head, out string body)
    {
        var sb = new System.Text.StringBuilder();
        switch (id)
        {
            case "house_1":
                head = "Ce qu'on raconte";
                foreach (var r in Rumors.Generate(5)) sb.Append($"<color=#FFD9A0>{r.who}</color>\n<i>« {r.text} »</i>\n\n");
                break;
            case "house_4":
            {
                head = "Registre de la Guilde";
                var g = GuildSystem.Instance;
                if (g == null || !g.Assessed) sb.Append("Le donjon de l'île n'est pas encore classé.\n\n");
                else
                {
                    sb.Append($"Donjon de l'île : rang {GuildSystem.RankTag(g.officialRank)}\n<size=85%>{GuildSystem.DungeonRankDesc(g.officialRank)}</size>\n\n");
                    sb.Append($"Convoitise {GuildSystem.Gauge(g.greed)}    Hostilité {GuildSystem.Gauge(g.hostility)}\n");
                    sb.Append($"Aventuriers disparus : {g.guildDeaths}\n");
                    sb.Append($"Prime d'exploration : {80 + 60 * Mathf.Max(0, g.officialRank)} pièces d'or\n\n");
                }
                var n = NemesisSystem.Instance;
                if (n != null && n.records.Count > 0)
                {
                    sb.Append("<color=#FFD966>Vétérans du donjon</color>\n");
                    foreach (var r in n.records)
                        sb.Append(r.dead ? $"<color=#8A8499><s>{r.name} « {r.epithet} »</s> — tombé</color>\n"
                                         : $"{r.name} « {r.epithet} » — rang {GuildSystem.RankName(r.rank)}, {r.encounters} expédition(s)\n");
                }
                break;
            }
            case "house_3":
            {
                head = "Le forgeron";
                var c = SettlementManager.Instance;
                float hp = c != null ? c.AdventurerHpMult : 1f, dmg = c != null ? c.AdventurerDmgMult : 1f;
                sb.Append("<i>« Tout ce qui sort de ma forge revient cabossé. Quand ça revient. »</i>\n\n");
                sb.Append($"Équipement des aventuriers de l'île :\n  PV <b>+{Mathf.RoundToInt((hp - 1f) * 100f)} %</b>\n  Dégâts <b>+{Mathf.RoundToInt((dmg - 1f) * 100f)} %</b>\n\n");
                sb.Append("Plus la colonie grandit, mieux les intrus sont équipés. Un village qui a peur achète moins d'armures...");
                break;
            }
            case "prison":
                head = "La prison";
                sb.Append("<i>« Les cellules attendent tes créatures. Et ceux qui les aident. »</i>\n\n");
                sb.Append("Les gardes veillent aux portes de la cité : vos raids y sont plus risqués.");
                break;
            case "manor":
                head = "Le manoir des nobles";
                sb.Append("<i>« Un donjon rentable est un donjon qu'on nettoie. Avec des héros de rang S. »</i>\n\n");
                sb.Append("Les nobles financent les meilleurs aventuriers du royaume. Leur or attire les plus dangereux.");
                break;
            default:
            {
                head = IsLibrary(id) ? "Bestiaire de la bibliothèque" : "Bestiaire des mages";
                sb.Append("<i>« Nous étudions ce qui vit sous la montagne. Pour mieux le combattre. »</i>\n\n");
                var seen = new HashSet<string>();
                foreach (var m in MonsterUnit.All) if (m != null && !m.summoned && seen.Add(m.familyName)) sb.Append($"• {m.familyName}\n");
                if (seen.Count == 0) sb.Append("Aucune créature recensée.");
                break;
            }
        }
        body = sb.ToString();
    }

    private static bool IsLibrary(string id) => id == "library";

    private void Greet(string id)
    {
        var story = StoryManager.Instance;
        if (story == null) return;
        switch (id)
        {
            case "house_1":
                var r = Rumors.Generate(1);
                story.PlayOnce("tavern_first",
                    new StoryManager.Line(StoryManager.SelfName, "Mon regard se glisse jusque dans leur taverne. Ils boivent, ils rient... et ils parlent de moi."),
                    new StoryManager.Line(r[0].who, r[0].text));
                break;
            case "house_4":
                story.PlayOnce("guild_first", new StoryManager.Line("Maître de guilde", "Encore un contrat pour le donjon de l'île ? Signez ici. Et prévenez votre famille."));
                break;
        }
    }
}
