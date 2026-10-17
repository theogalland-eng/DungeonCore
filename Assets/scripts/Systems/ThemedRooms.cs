using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// SALLES THÉMATIQUES : au moins trois décors d'un même thème rapprochés forment une salle qui donne un bonus
/// aux monstres qui s'y trouvent (rayon de quelques cases) :
///   - Crypte (ossements, cercueils, sarcophages, crânes...) : morts-vivants +25 % de dégâts et de PV régénérés.
///   - Armurerie (râteliers, heaumes, bannières, statues de chevaliers) : tous les monstres +15 % de dégâts.
///   - Salle du trésor (coffres, or, trophées) : prestige des trésors ×1,5.
///   - Sanctuaire (braseros, torches spectrales, gargouilles, statues démoniaques, fontaine, cristaux, autel) : régénération ×3.
///   - Jardin fongique (champignons) : slimes et champignons +30 % de dégâts et de régénération.
/// </summary>
public class ThemedRooms : MonoBehaviour
{
    public static ThemedRooms Instance { get; private set; }

    public enum Kind { Crypt, Armory, Treasury, Sanctum, Fungal, Hatchery, Pantry, Forge }

    public class Room
    {
        public Kind kind;
        public Vector2 center;
        public float radius;
        public int layer;
        public readonly List<PlacedObject> items = new List<PlacedObject>();
        public GameObject visual;
    }

    private static readonly Dictionary<string, Kind> ItemKinds = new Dictionary<string, Kind>
    {
        { "bones", Kind.Crypt }, { "skull_pile", Kind.Crypt }, { "coffin", Kind.Crypt }, { "hanging", Kind.Crypt },
        { "sarcophagus", Kind.Crypt }, { "sarcophagus_open", Kind.Crypt }, { "candelabra", Kind.Crypt }, { "skull", Kind.Crypt },
        { "rack", Kind.Armory }, { "helm", Kind.Armory }, { "banner", Kind.Armory }, { "statue_knight", Kind.Armory },
        { "chest", Kind.Treasury }, { "gold", Kind.Treasury }, { "gold_pile", Kind.Treasury }, { "chest_royal", Kind.Treasury }, { "trophy", Kind.Treasury },
        { "brazier", Kind.Sanctum }, { "wall_torch", Kind.Sanctum }, { "statue_fire", Kind.Sanctum }, { "gargoyle", Kind.Sanctum }, { "fountain", Kind.Sanctum },
        { "crystal_blue", Kind.Sanctum }, { "crystal_red", Kind.Sanctum }, { "crystal_yellow", Kind.Sanctum }, { "crystal_green", Kind.Sanctum }, { "crystal_black", Kind.Sanctum },
        { "magma_vent", Kind.Sanctum }, { "demon_hand", Kind.Sanctum }, { "demon_altar", Kind.Sanctum },
        { "mushrooms_long", Kind.Fungal }, { "mushroom_slime", Kind.Fungal }, { "mushrooms_small", Kind.Fungal },
        // salles à fonction
        { "nest", Kind.Hatchery },
        { "food_cache", Kind.Pantry }, { "supplies", Kind.Pantry }, { "barrel", Kind.Pantry },
        { "anvil", Kind.Forge }, { "forge", Kind.Forge },
    };
    public static readonly string[] Names = { "Crypte", "Armurerie", "Salle du trésor", "Sanctuaire", "Jardin fongique", "Couvoir", "Garde-manger", "Forge des monstres" };
    public static readonly string[] Effects = { "morts-vivants +25 %", "monstres +15 % de dégâts", "prestige des trésors ×1,5, attire les cupides de loin", "régénération ×3", "slimes et champignons +30 %",
        "un monstre naît chaque matin", "entretien des monstres -10 % (jusqu'à -30 %)", "monstres -15 % de dégâts subis, évolutions -25 %" };
    private static readonly Color[] Colors =
    {
        new Color(0.55f, 0.8f, 0.75f), new Color(0.9f, 0.55f, 0.4f), new Color(1f, 0.85f, 0.35f), new Color(0.75f, 0.5f, 1f), new Color(0.45f, 1f, 0.8f),
        new Color(0.95f, 0.85f, 0.65f), new Color(0.95f, 0.65f, 0.4f), new Color(1f, 0.5f, 0.25f),
    };

    public readonly List<Room> rooms = new List<Room>();
    private readonly HashSet<string> announced = new HashSet<string>();
    private float nextScan;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    /// <summary>Couvoir : chaque matin, un monstre de la famille la plus présente dans la salle éclôt (gratuit).</summary>
    private void OnNewDay(int day)
    {
        if (CrawlerMode.Active) return;
        var cat = DungeonCatalog.Get();
        if (cat == null || MonsterSpawner.Instance == null) return;
        foreach (var r in rooms)
        {
            if (r.kind != Kind.Hatchery) continue;
            if (PlayerLevel.Instance != null && MonsterUnit.All.Count >= PlayerLevel.Instance.GetMaxMonsters())
            {
                GameHUD.Toast("Couvoir : plus de place pour une nouvelle créature", GameHUD.WarningColor);
                return;
            }
            // famille dominante autour du nid (rat par défaut)
            var count = new Dictionary<string, int>();
            foreach (var m in MonsterUnit.All)
                if (m != null && !m.summoned && Vector2.Distance(m.transform.position, r.center) <= r.radius + 3f)
                    count[m.familyId] = (count.TryGetValue(m.familyId, out int n) ? n : 0) + 1;
            string fam = "rat"; int best = 0;
            foreach (var kv in count) if (kv.Value > best && !string.IsNullOrEmpty(kv.Key)) { best = kv.Value; fam = kv.Key; }
            GameObject prefab = null;
            foreach (var it in cat.items)
            {
                if (it.kind != ItemKind.Monster || it.prefab == null) continue;
                var u = it.prefab.GetComponent<MonsterUnit>();
                if (u != null && u.familyId == fam && u.stage <= 1) { prefab = it.prefab; break; }
            }
            if (prefab == null) continue;
            var g = MonsterSpawner.Instance.SpawnFree(prefab, r.center, r.center, prefab.GetComponent<GobelinAI>()?.gobelinName ?? prefab.name);
            if (g != null)
            {
                Fx.Play("shine", r.center, 1.2f);
                GameHUD.Toast($"Couvoir : une nouvelle créature est née ({g.gobelinName})", GameHUD.GoldColor);
            }
        }
    }

    /// <summary>Garde-manger : chaque salle réduit l'entretien des monstres de 10 % (jusqu'à -30 %).</summary>
    public static float MonsterUpkeepMult()
    {
        var t = Instance;
        if (t == null) return 1f;
        int n = 0;
        foreach (var r in t.rooms) if (r.kind == Kind.Pantry) n++;
        return 1f - Mathf.Min(3, n) * 0.1f;
    }

    /// <summary>Forge : un monstre dans la salle encaisse mieux.</summary>
    public static float DamageTakenMult(Vector2 p)
    {
        var t = Instance;
        return t != null && t.RoomAt(p, Kind.Forge) != null ? 0.85f : 1f;
    }

    /// <summary>Forge : évoluer y coûte 25 % de mana en moins.</summary>
    public static float EvolveCostMult(Vector2 p)
    {
        var t = Instance;
        return t != null && t.RoomAt(p, Kind.Forge) != null ? 0.75f : 1f;
    }

    /// <summary>Le coffre est dans une salle du trésor : les cupides le repèrent de plus loin.</summary>
    public static bool InTreasury(Vector2 p)
    {
        var t = Instance;
        return t != null && t.RoomAt(p, Kind.Treasury) != null;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 2f;
            Scan();
        }
        // visibles seulement en vue donjon, sur l'étage regardé
        var relief = MountainRelief.Instance;
        bool dungeonView = relief == null || relief.DungeonView;
        int view = DungeonLayers.Instance != null ? DungeonLayers.Instance.CurrentView : 0;
        foreach (var r in rooms)
            if (r.visual != null) r.visual.SetActive(dungeonView && r.layer == view);
    }

    private static int LayerAt(Vector2 p) => DungeonLayers.Instance != null ? DungeonLayers.Instance.IndexAt(p) : 0;

    private void Scan()
    {
        var byKind = new Dictionary<Kind, List<PlacedObject>>();
        foreach (var p in PlacedObject.All)
        {
            if (p == null || p.itemId == null || !ItemKinds.TryGetValue(p.itemId, out var k)) continue;
            if (!byKind.TryGetValue(k, out var l)) byKind[k] = l = new List<PlacedObject>();
            l.Add(p);
        }
        var found = new List<Room>();
        foreach (var kv in byKind)
        {
            var left = new List<PlacedObject>(kv.Value);
            while (left.Count > 0)
            {
                // regroupement par proximité (même étage, 3,5 cases)
                var cluster = new List<PlacedObject> { left[0] };
                left.RemoveAt(0);
                for (int i = 0; i < cluster.Count; i++)
                    for (int j = left.Count - 1; j >= 0; j--)
                        if (Vector2.Distance(cluster[i].transform.position, left[j].transform.position) <= 3.5f &&
                            LayerAt(cluster[i].transform.position) == LayerAt(left[j].transform.position))
                        { cluster.Add(left[j]); left.RemoveAt(j); }
                if (cluster.Count < 3) continue;
                Vector2 c = Vector2.zero;
                foreach (var p in cluster) c += (Vector2)p.transform.position;
                c /= cluster.Count;
                float spread = 0f;
                foreach (var p in cluster) spread = Mathf.Max(spread, Vector2.Distance(c, p.transform.position));
                var room = new Room { kind = kv.Key, center = c, radius = Mathf.Max(3f, spread + 1.5f), layer = LayerAt(c) };
                room.items.AddRange(cluster);
                found.Add(room);
            }
        }
        // on garde les visuels des salles qui existent toujours
        foreach (var old in rooms)
        {
            var same = found.Find(f => f.kind == old.kind && Vector2.Distance(f.center, old.center) < 1.5f);
            if (same != null && same.visual == null) same.visual = old.visual;
            else if (old.visual != null) Destroy(old.visual);
        }
        rooms.Clear();
        rooms.AddRange(found);
        foreach (var r in rooms)
        {
            if (r.visual == null) r.visual = MakeVisual(r);
            else r.visual.transform.position = r.center;
            string key = r.kind + "@" + Mathf.RoundToInt(r.center.x) + "," + Mathf.RoundToInt(r.center.y);
            if (announced.Add(key))
            {
                if (!CrawlerMode.Active) GameHUD.Toast($"Salle formée : {Names[(int)r.kind]} ({Effects[(int)r.kind]})", GameHUD.GoldColor);
                StoryManager.Instance?.PlayOnce("first_room",
                    new StoryManager.Line(StoryManager.SelfName, "Ces objets, réunis, donnent à ce lieu une âme. Mes créatures s'y sentent chez elles."),
                    new StoryManager.Line(StoryManager.SystemName, "Trois objets d'un même thème rapprochés forment une salle : crypte, armurerie, salle du trésor, sanctuaire, jardin fongique — ou salles à fonction : couvoir (nids), garde-manger (vivres), forge (enclumes, forges)."));
            }
        }
    }

    private GameObject MakeVisual(Room r)
    {
        var go = new GameObject("Salle_" + Names[(int)r.kind]);
        go.transform.position = r.center;
        var glow = new GameObject("Halo").AddComponent<SpriteRenderer>();
        glow.transform.SetParent(go.transform, false);
        glow.sprite = SpriteUtil.Glow;
        var c = Colors[(int)r.kind];
        glow.color = new Color(c.r, c.g, c.b, 0.13f);
        glow.sortingOrder = 1;
        glow.transform.localScale = Vector3.one * r.radius * 2f;
        var label = new GameObject("Nom").AddComponent<TextMeshPro>();
        label.transform.SetParent(go.transform, false);
        label.transform.localPosition = new Vector3(0f, -r.radius * 0.55f, 0f);
        label.text = Names[(int)r.kind];
        label.fontSize = 3f;
        label.fontStyle = FontStyles.SmallCaps;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(c.r, c.g, c.b, 0.55f);
        label.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
        label.sortingOrder = 2;
        return go;
    }

    private Room RoomAt(Vector2 p, Kind k)
    {
        foreach (var r in rooms) if (r.kind == k && Vector2.Distance(p, r.center) <= r.radius) return r;
        return null;
    }

    private static bool IsFungal(string f) => f == "mushroom" || f == "slime";

    private static bool IsUndead(string f) => f == "skeleton" || f == "zombie" || f == "lich" || f == "ghost" || f == "vampire";

    /// <summary>Multiplicateur de dégâts d'un monstre selon la salle où il se tient.</summary>
    public static float DamageMult(Vector2 p, string family)
    {
        var t = Instance;
        if (t == null) return 1f;
        float m = 1f;
        if (t.RoomAt(p, Kind.Armory) != null) m *= 1.15f;
        if (IsUndead(family) && t.RoomAt(p, Kind.Crypt) != null) m *= 1.25f;
        if (IsFungal(family) && t.RoomAt(p, Kind.Fungal) != null) m *= 1.3f;
        return m;
    }

    /// <summary>Multiplicateur de régénération.</summary>
    public static float RegenMult(Vector2 p, string family)
    {
        var t = Instance;
        if (t == null) return 1f;
        float m = 1f;
        if (t.RoomAt(p, Kind.Sanctum) != null) m *= 3f;
        if (IsUndead(family) && t.RoomAt(p, Kind.Crypt) != null) m *= 1.25f;
        if (IsFungal(family) && t.RoomAt(p, Kind.Fungal) != null) m *= 1.3f;
        return m;
    }

    /// <summary>Prestige d'un objet (les trésors d'une salle du trésor comptent 1,5 fois).</summary>
    public static float PrestigeMult(PlacedObject p)
    {
        var t = Instance;
        if (t == null || p == null) return 1f;
        foreach (var r in t.rooms) if (r.kind == Kind.Treasury && r.items.Contains(p)) return 1.5f;
        return 1f;
    }
}
