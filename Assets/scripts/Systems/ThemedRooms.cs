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

    /// <summary>Salle en formation : un ou deux objets d'un thème, regroupés (il en faut trois).</summary>
    public class Partial { public Kind kind; public Vector2 center; public int count, layer; }
    public readonly List<Partial> partials = new List<Partial>();

    /// <summary>Changement de salles (formée, perdue, en formation) : la fenêtre Salles se rafraîchit.</summary>
    public int Version { get; private set; }

    public static bool TryKindOf(string itemId, out Kind k)
    {
        k = Kind.Crypt;
        return itemId != null && ItemKinds.TryGetValue(itemId, out k);
    }

    public static Color ColorOf(Kind k) => Colors[(int)k];

    /// <summary>Noms des objets du catalogue qui comptent pour un type de salle.</summary>
    public static List<string> ItemNamesOf(Kind k)
    {
        var names = new List<string>();
        var cat = DungeonCatalog.Get();
        foreach (var kv in ItemKinds)
        {
            if (kv.Value != k) continue;
            var it = cat != null ? cat.Find(kv.Key) : null;
            if (it != null && !names.Contains(it.displayName)) names.Add(it.displayName);
        }
        return names;
    }

    /// <summary>Force un nouveau relevé des salles (objet posé ou effacé).</summary>
    public void RescanSoon() => nextScan = 0f;
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
            for (int k = 0; k < Talents.HatchCount; k++)
            {
                if (PlayerLevel.Instance != null && MonsterUnit.All.Count >= PlayerLevel.Instance.GetMaxMonsters()) break;
                Vector2 at = r.center + Random.insideUnitCircle * 0.6f * k;
                var g = MonsterSpawner.Instance.SpawnFree(prefab, at, at, prefab.GetComponent<GobelinAI>()?.gobelinName ?? prefab.name);
                if (g != null)
                {
                    Fx.Play("shine", at, 1.2f);
                    GameHUD.Toast($"Couvoir : une nouvelle créature est née ({g.gobelinName})", GameHUD.GoldColor);
                }
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

    /// <summary>Dans une salle aménagée (quelle qu'elle soit) : un monstre s'y sent chez lui.</summary>
    public static bool InAnyRoom(Vector2 p)
    {
        var t = Instance;
        if (t == null) return false;
        foreach (var r in t.rooms) if (Vector2.Distance(p, r.center) <= r.radius) return true;
        return false;
    }

    /// <summary>Ce point est-il dans une salle formée de ce type ?</summary>
    public static bool InRoom(Vector2 p, Kind k)
    {
        var t = Instance;
        if (t == null) return false;
        foreach (var r in t.rooms)
            if (r.kind == k && Vector2.Distance(p, r.center) <= r.radius + 0.75f) return true;
        return false;
    }

    /// <summary>Salle d'un type donné la plus proche (centre), dans un rayon.</summary>
    public static bool NearestRoom(Vector2 p, Kind k, float maxDist, out Vector2 center)
    {
        center = Vector2.zero;
        var t = Instance;
        if (t == null) return false;
        float bd = maxDist;
        bool found = false;
        foreach (var r in t.rooms)
        {
            if (r.kind != k) continue;
            float d = Vector2.Distance(p, r.center);
            if (d < bd) { bd = d; center = r.center; found = true; }
        }
        return found;
    }

    /// <summary>Le coffre est dans une salle du trésor : les cupides le repèrent de plus loin.</summary>
    public static bool InTreasury(Vector2 p)
    {
        var t = Instance;
        return t != null && t.RoomAt(p, Kind.Treasury) != null;
    }

    private void Update()
    {
        UpdatePreview();
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
        var newPartials = new List<Partial>();
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
                if (cluster.Count < 3)
                {
                    Vector2 pc = Vector2.zero;
                    foreach (var p in cluster) pc += (Vector2)p.transform.position;
                    pc /= cluster.Count;
                    newPartials.Add(new Partial { kind = kv.Key, center = pc, count = cluster.Count, layer = LayerAt(pc) });
                    continue;
                }
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
        string before = Sig(rooms, partials);
        rooms.Clear();
        rooms.AddRange(found);
        partials.Clear();
        partials.AddRange(newPartials);
        if (Sig(rooms, partials) != before) Version++;
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

    private static string Sig(List<Room> rs, List<Partial> ps)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in rs) sb.Append($"{r.kind}{Mathf.RoundToInt(r.center.x)},{Mathf.RoundToInt(r.center.y)}:{r.items.Count};");
        foreach (var p in ps) sb.Append($"p{p.kind}{Mathf.RoundToInt(p.center.x)},{Mathf.RoundToInt(p.center.y)}:{p.count};");
        return sb.ToString();
    }

    // ── Aperçu pendant la pose d'un objet de thème : cercle de regroupement et compte « 2/3 »
    private GameObject preview;
    private TextMeshPro previewLabel;
    private SpriteRenderer previewRing;

    private void UpdatePreview()
    {
        var b = DungeonBuilder.Instance;
        var item = b != null && b.GetCurrentMode() == DungeonBuilder.BuildMode.PlacingItem ? b.SelectedItem : null;
        var ptr = UnityEngine.InputSystem.Pointer.current;
        if (item == null || !TryKindOf(item.id, out var k) || ptr == null || CrawlerMode.Active)
        {
            if (preview != null && preview.activeSelf) preview.SetActive(false);
            return;
        }
        if (preview == null)
        {
            preview = new GameObject("Aperçu de salle");
            previewRing = preview.AddComponent<SpriteRenderer>();
            previewRing.sprite = SpriteUtil.Glow;
            previewRing.sortingOrder = 3;
            preview.transform.localScale = Vector3.one * 7f;
            var lg = new GameObject("Compte");
            lg.transform.SetParent(preview.transform, false);
            previewLabel = lg.AddComponent<TextMeshPro>();
            previewLabel.fontSize = 3.2f / 7f * 7f;
            previewLabel.alignment = TextAlignmentOptions.Center;
            previewLabel.rectTransform.sizeDelta = new Vector2(10f, 1.4f);
            previewLabel.sortingOrder = 220;
            previewLabel.outlineWidth = 0.25f;
            previewLabel.outlineColor = new Color32(10, 6, 16, 255);
            lg.transform.localScale = Vector3.one / 7f;
            lg.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        }
        if (!preview.activeSelf) preview.SetActive(true);
        Vector2 w = PointerUtil.ScreenToWorld(ptr.position.ReadValue());
        preview.transform.position = w;
        int layer = LayerAt(w);
        int near = 0;
        foreach (var p in PlacedObject.All)
            if (p != null && TryKindOf(p.itemId, out var pk) && pk == k && LayerAt(p.transform.position) == layer &&
                Vector2.Distance(p.transform.position, w) <= 3.5f) near++;
        var c = Colors[(int)k];
        previewRing.color = new Color(c.r, c.g, c.b, 0.18f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f));
        int total = near + 1;
        previewLabel.text = total >= 3
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{Names[(int)k]}</color>  {total}/3 — salle !"
            : $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{Names[(int)k]}</color>  {total}/3";
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
