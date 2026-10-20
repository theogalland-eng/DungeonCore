using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MODE AVENTURIER (dungeon crawler) : le joueur incarne un aventurier arrivé sur l'île.
///   - La colonie est une ville complète : on ENTRE À PIED dans la taverne, la forge, le hall de la Guilde et la
///     tour des mages, et on parle aux habitants (repos, rumeurs, équipement, potions, rang, recrues).
///   - Au hall de la Guilde, on forme un GROUPE : jusqu'à trois compagnons (guerrier, archer, mage, soigneuse).
///   - Le donjon compte DIX ÉTAGES procéduraux (CrawlerDungeon) : rats et gobelins d'abord, puis orcs, morts-vivants,
///     gnolls, golems, liches, vampires... et un seigneur démon devant le cristal, au dixième.
///     Escaliers entre les étages ; la Guilde ouvre des passages directs vers les étages 4 et 7 une fois atteints.
///   - Le donjon est vivant : les étages se repeuplent quand on y revient.
///   - Mort : on se réveille à la taverne, délesté de la moitié de son or. Briser le cristal = victoire.
///   - ÉQUIPEMENT (CrawlerGear) : arme, armure, bijou, de commun à légendaire ; sac de dix objets (touche I).
///   - CONTRATS de la Guilde (CrawlerContracts) : chasse, exploration, pillage, prime sur un gardien.
/// </summary>
public class CrawlerMode : MonoBehaviour
{
    public static CrawlerMode Instance { get; private set; }
    public static bool Active => Instance != null && Instance.started;

    public static readonly string[] ClassNames = { "Guerrier", "Archer", "Mage", "Soigneuse" };
    public static readonly string[] ClassDescs =
    {
        "Solide au corps à corps. Compétence : Tourbillon (frappe tout autour).",
        "Tire de loin. Compétence : Volée (trois flèches sur trois cibles).",
        "Boules de feu explosives. Compétence : Nova (explosion autour de soi).",
        "Frappe peu, tient longtemps. Compétence : Prière (grands soins).",
    };
    private static readonly int[] RankKills = { 0, 5, 12, 22, 35, 50, 70, 95, 130 };
    private static readonly int[] Checkpoints = { 4, 7 };
    public const int MaxParty = 3;

    public CrawlerPlayer Player { get; private set; }
    public DungeonCore Core { get; private set; }
    public int gold = 40, potions = 2, recallStones, kills, weaponLvl, armorLvl, rank;
    /// <summary>Satiété du héros (100 = repu). Elle baisse doucement ; affamé, il récupère mal et frappe moins fort.
    /// Jamais mortelle. Rations : taverne, coffres, gibier chassé puis dépecé.</summary>
    public float satiety = 100f;
    public int rations = 2;
    [Tooltip("Secondes pour passer de repu à affamé")] public float satietyDuration = 600f;
    /// <summary>Étage où se trouve le joueur (0 = dehors), et le plus profond atteint.</summary>
    public int floor, deepest, reportedDeepest;

    /// <summary>Tout est figé (dialogue, boutique, menu, transition).</summary>
    public bool Frozen =>
        victory || GearWindow.IsOpen || SkillTreeWindow.IsOpen || shop != null || transitioning || (StoryManager.Instance != null && StoryManager.Instance.IsShowing) ||
        (MainMenu.Instance != null && MainMenu.Instance.InPause) || (InteriorSystem.Instance != null && InteriorSystem.Instance.Moving);
    public bool InputBlocked => Frozen;

    private bool started, victory, transitioning;
    private int seed;
    private readonly Dictionary<int, CrawlerDungeon.Floor> floors = new Dictionary<int, CrawlerDungeon.Floor>();
    private readonly Dictionary<int, int> regionFloor = new Dictionary<int, int>();
    private readonly HashSet<string> lootedChests = new HashSet<string>();
    private readonly List<Recruit> recruits = new List<Recruit>();
    private GameObject checkpointCircle;
    private int killedSinceTown;
    private System.Random rng;
    private Canvas canvas;
    private RectTransform hud, shop;
    private TextMeshProUGUI title, stats, prompt, partyText, banner;
    private RectTransform hpFill, skillFill, staminaFill, satietyFill;
    private Image satietyImg;
    private Image staminaImg;
    private Image fade;
    private float startTime;

    // ── Équipement
    public const int BagSize = 10;
    public readonly List<CrawlerGear.Item> bag = new List<CrawlerGear.Item>();
    public readonly CrawlerGear.Item[] equipped = new CrawlerGear.Item[3];
    private int trip, contractsTrip = -1;

    public float GearSum(CrawlerGear.Stat st)
    {
        float v = 0f;
        foreach (var it in equipped) if (it != null) v += it.Get(st);
        return v;
    }

    public void Equip(CrawlerGear.Item it)
    {
        if (it == null) return;
        int i = (int)it.slot;
        var old = equipped[i];
        bag.Remove(it);
        equipped[i] = it;
        if (old != null) bag.Add(old);
        RefreshStats(false);
        Sfx.Play("hit_sword", null, 0.5f);
    }

    public bool CanSell => floor == 0;

    public void Sell(CrawlerGear.Item it)
    {
        if (it == null || !CanSell) return;
        if (!bag.Remove(it)) return;
        gold += it.Value;
        Sfx.Play("coin");
    }

    /// <summary>Un objet trouvé : dans le sac (ou vendu sur-le-champ s'il est plein).</summary>
    public void GiveItem(CrawlerGear.Item it, Vector3 at)
    {
        string col = ColorUtility.ToHtmlStringRGB(CrawlerGear.RarityColor(it.rarity));
        FloatingText.Spawn(at + Vector3.up * 1.4f, it.name, CrawlerGear.RarityColor(it.rarity), 1.8f);
        if (bag.Count >= BagSize)
        {
            gold += it.Value / 2;
            GameHUD.Toast($"Sac plein : <color=#{col}>{it.name}</color> laissé pour {it.Value / 2} or", GameHUD.WarningColor);
            return;
        }
        bag.Add(it);
        GameHUD.Toast($"Trouvé : <color=#{col}>{it.name}</color> ({CrawlerGear.RarityNames[(int)it.rarity].ToLower()}) — sac : touche I", GameHUD.InfoColor);
        if (it.rarity >= CrawlerGear.Rarity.Epic) { Sfx.Play("shine"); Haptics.Pulse(); }
    }

    private Button talentsBtn;
    private RectTransform bossPanel, bossFill;
    private TextMeshProUGUI bossName;

    /// <summary>Données d'un étage déjà créé (renforts des boss).</summary>
    public CrawlerDungeon.Floor FloorData(int n) => floors.TryGetValue(n, out var f) ? f : null;

    /// <summary>Un talent de classe vient d'être appris.</summary>
    public void OnSkillsChanged() => RefreshStats(false);

    private class Recruit { public string name; public AdventurerAI.Role role; public int rank, cost; public bool female; }

    private static readonly string[] MaleClass = { "Guerrier", "Archer", "Mage", "Soigneur" };
    private static readonly string[] FemaleClass = { "Guerrière", "Archère", "Mage", "Soigneuse" };
    public static string ClassLabel(AdventurerAI.Role r, bool female) => (female ? FemaleClass : MaleClass)[(int)r];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
        DungeonTerritory.ExtraSeeds.Clear();
    }

    // ─────────────────────────────────────────────
    // DÉMARRAGE
    // ─────────────────────────────────────────────

    public void Begin(AdventurerAI.Role role, CrawlerSave restore = null)
    {
        if (started) return;
        started = true;
        startTime = Time.time;
        seed = restore != null ? restore.seed : System.Environment.TickCount & 0x7fffffff;
        rng = new System.Random(seed);
        GameHUD.Instance?.SetPanelsVisible(false);
        foreach (var sel in FindObjectsByType<MonsterSelector>(FindObjectsSortMode.None)) sel.enabled = false;
        if (RaidSystem.Instance != null) RaidSystem.Instance.enabled = false;
        if (ExpeditionManager.Instance != null) ExpeditionManager.Instance.enabled = false;
        var story = StoryManager.Instance;
        if (story != null)
            foreach (var f in new[] { "intro", "colony_1", "colony_2", "colony_3", "colony_4", "colony_5", "colony_cine_1", "colony_cine_2", "colony_cine_4", "colony_cine_5",
                                      "first_dig", "first_summon", "first_decor", "first_room", "first_guardian", "tavern_first", "guild_first" }) story.SetFlag(f);

        CrawlerContracts.Reset();
        CrawlerSkills.Reset();
        bag.Clear();
        for (int i = 0; i < equipped.Length; i++) equipped[i] = null;
        BuildTown();
        var overlord = gameObject.AddComponent<CrawlerOverlord>();   // l'intelligence qui gère et agrandit le donjon
        if (restore != null)
        {
            overlord.Restore(restore);
            // l'apparence du héros sauvegardé redevient celle du jeu
            HeroLook.HeroName = restore.heroName ?? "";
            HeroLook.Hair = ParseColor(restore.hair); HeroLook.Skin = ParseColor(restore.skin); HeroLook.Outfit = ParseColor(restore.outfit);
            HeroLook.Save();
        }
        BuildDungeon();
        SpawnPlayer(role);
        BuildHud();
        if (restore != null) ApplySave(restore);
        else StartCoroutine(Intro());
    }

    private void BuildTown()
    {
        var sm = SettlementManager.Instance;
        if (sm == null || sm.Site == null) return;
        bool cine = GameSettings.Cinematics;
        GameSettings.Cinematics = false;
        sm.BuildInstantly(5);
        GameSettings.Cinematics = cine;
    }

    private void BuildDungeon()
    {
        Core = FindFirstObjectByType<DungeonCore>();
        ManaSystem.Instance?.Add(999999);
        // Étage 1 : dans la montagne, autour de l'ancien emplacement du cristal
        var f1 = CrawlerDungeon.Generate(1, seed);
        floors[1] = f1;
        regionFloor[0] = 1;
        FinishFloor(f1);
        // Étage 10 : l'antre du cristal (zone réservée), creusé dès maintenant pour y loger le Noyau
        var f10 = CrawlerDungeon.Generate(CrawlerDungeon.FloorCount, seed);
        floors[CrawlerDungeon.FloorCount] = f10;
        regionFloor[CrawlerDungeon.RegionOf(CrawlerDungeon.FloorCount)] = CrawlerDungeon.FloorCount;
        if (Core != null)
        {
            var last = f10.Last;
            Core.transform.position = new Vector3(last.rect.x + last.rect.width * 0.5f, last.rect.yMax - 1.2f, 0f);
            Core.maxHealth = 1800f;
            Core.currentHealth = Core.maxHealth;
            if (AstarPath.active != null) AstarPath.active.UpdateGraphs(new Bounds(Core.transform.position, new Vector3(4, 4, 1)));
        }
        CrawlerStairs.Create(StairsUpPos(f10), f10.number, false);
        UpdateSeeds();
        // cercle de passage de la Guilde (étages 4 et 7), dans la première salle
        var circle = Resources.Load<Sprite>("Interiors/mag_circle");
        foreach (var s in Resources.LoadAll<Sprite>("Interiors")) if (s.name == "mag_circle") circle = s;
        if (circle != null)
        {
            checkpointCircle = new GameObject("Cercle de passage");
            checkpointCircle.transform.position = f1.Start.Center + new Vector3(-1.2f, -0.3f, 0f);
            var sr = checkpointCircle.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.sortingOrder = 1;
            sr.color = new Color(0.7f, 0.8f, 1f, 0.9f);
            checkpointCircle.transform.localScale = Vector3.one * 0.6f;
            checkpointCircle.SetActive(false);
        }
    }

    private Vector3 StairsUpPos(CrawlerDungeon.Floor f) => DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(new Vector3Int(f.Start.CenterCell.x, f.Start.rect.yMax - 1, 0));
    private Vector3 StairsDownPos(CrawlerDungeon.Floor f) => DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(new Vector3Int(f.StairsRoom.CenterCell.x, f.StairsRoom.rect.yMax - 1, 0));

    /// <summary>Étages dont le gardien a été vaincu : leur descente reste ouverte, même quand l'étage est recréé.</summary>
    private readonly HashSet<int> bossesDefeated = new HashSet<int>();

    /// <summary>Peuple l'étage et pose ses escaliers.</summary>
    private void FinishFloor(CrawlerDungeon.Floor f)
    {
        CrawlerDungeon.Populate(f, new System.Random(rng.Next()), key => lootedChests.Contains(key), !bossesDefeated.Contains(f.number));
        if (f.number < CrawlerDungeon.FloorCount)
        {
            var down = CrawlerStairs.Create(StairsDownPos(f), f.number, true);
            if (f.guardian != null) down.Seal(f.guardian);
        }
        if (f.number > 1) CrawlerStairs.Create(StairsUpPos(f), f.number, false);
        if (f.number == CrawlerDungeon.FloorCount && Core != null)
        {
            // le seigneur de l'antre, devant le cristal
            var g = CrawlerDungeon.SpawnMonster(rng.Next(2) == 0 ? "demon" : "vampire", 3, (Vector2)Core.transform.position + Vector2.down * 1.8f, f);
            if (g != null)
            {
                MonsterMind.For(g).MakeGuardian(new[] { "Azhra", "Morvenna", "Kharn", "Vorth" }[rng.Next(4)], true);
                g.SetBehavior(GobelinAI.GoblinBehavior.Stay);
                CrawlerBoss.Attach(g, f.number);
            }
        }
    }

    private void UpdateSeeds()
    {
        DungeonTerritory.ExtraSeeds.Clear();
        foreach (var kv in regionFloor)
            if (floors.TryGetValue(kv.Value, out var f)) DungeonTerritory.ExtraSeeds.Add(f.Start.CenterCell);
        DungeonTerritory.Instance?.Recompute();
    }

    private void SpawnPlayer(AdventurerAI.Role role)
    {
        var spawner = FindFirstObjectByType<AdventurerSpawner>();
        Vector3 at = spawner != null ? spawner.SpawnPosition : Vector3.zero;
        var go = SpawnAdventurer(role, at, "Joueur");
        Player = go.AddComponent<CrawlerPlayer>();
        Player.Setup(role);
        HeroLook.Load();
        Player.SetTier(1 + armorLvl);
        Player.ApplyLook();
        Player.Combat.adventurerName = string.IsNullOrWhiteSpace(HeroLook.HeroName) ? $"{ClassNames[(int)role]} (vous)" : HeroLook.HeroName;
        HealthBar.Attach(go.transform, () => Player.Combat.currentHealth / Player.Combat.maxHealth, new Color(0.35f, 0.9f, 0.4f));
        NemesisSystem.AddTag(go.transform, string.IsNullOrWhiteSpace(HeroLook.HeroName) ? "Vous" : HeroLook.HeroName, new Color(0.6f, 0.9f, 1f));
        var cam = Camera.main;
        if (cam != null) { cam.orthographicSize = 6f; cam.transform.position = new Vector3(at.x, at.y, cam.transform.position.z); }
        if (CameraController.Instance != null) CameraController.Instance.cinematic = true;
        MountainRelief.Instance?.SetUserView(false);
    }

    /// <summary>Un aventurier contrôlé par le mode (joueur ou compagnon) : sans l'IA d'intrus.</summary>
    private static GameObject SpawnAdventurer(AdventurerAI.Role role, Vector3 at, string name)
    {
        var cat = DungeonCatalog.Get();
        GameObject prefab = role == AdventurerAI.Role.Archer ? cat.archerPrefab : role == AdventurerAI.Role.Mage ? cat.magePrefab :
                            role == AdventurerAI.Role.Healer ? cat.healerPrefab : (cat.adventurerPrefabs.Count > 2 ? cat.adventurerPrefabs[2] : null);
        if (prefab == null) prefab = cat.adventurerPrefabs.Count > 0 ? cat.adventurerPrefabs[0] : null;
        var go = Instantiate(prefab, at, Quaternion.identity);
        go.name = name;
        var ai = go.GetComponent<AdventurerAI>();
        if (ai != null) DestroyImmediate(ai);
        return go;
    }

    private IEnumerator Intro()
    {
        yield return null;
        StoryManager.Instance?.Play(
            new StoryManager.Line(StoryManager.NarratorName, "Le bateau de la Guilde vous dépose au ponton de l'île. Au loin, le volcan fume."),
            new StoryManager.Line("Maître de guilde", "Encore un qui veut sa part du donjon ? Il a DIX étages, et chaque marche vers le bas est pire que la précédente."),
            new StoryManager.Line("Maître de guilde", "Passe à la forge, à la tour des mages, et viens au hall : on y forme des groupes. Seul, tu ne passeras pas le quatrième étage."),
            new StoryManager.Line("Maître de guilde", "Le cristal est tout au fond. Brise-le, et ton nom sera chanté dans toutes les tavernes."),
            new StoryManager.Line(StoryManager.SystemName, "Déplacement : ZQSD / flèches ou joystick. Attaque : Espace, clic ou bouton. Roulade : Ctrl, clic droit ou bouton Esquive. Compétence : R. Potion : F. Interagir (portes, habitants, coffres, escaliers) : E. Pierre de rappel : T."));
    }

    // ─────────────────────────────────────────────
    // BOUCLE
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (!started || Player == null) return;
        var kb = Keyboard.current;
        if (shop != null && kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) { CloseShop(); return; }
        if (!Frozen && kb != null && kb.tKey.wasPressedThisFrame) UseRecall();
        if (kb != null && kb.iKey.wasPressedThisFrame && shop == null) GearWindow.Toggle();
        if (kb != null && kb.kKey.wasPressedThisFrame && shop == null) SkillTreeWindow.Toggle();
        TickHunger();
        // entrer dans un bâtiment en marchant vers sa porte
        if (!Frozen && Player.MoveInput.y > 0.5f && InteriorSystem.Instance != null && !InteriorSystem.Instance.Inside)
        {
            var b = DoorNear(0.75f);
            if (b != null) EnterBuilding(b);
        }
    }

    private void LateUpdate()
    {
        if (!started || Player == null) return;
        var cam = Camera.main;
        var interior = InteriorSystem.Instance;
        bool indoors = interior != null && interior.Physical;
        Vector2 pp = Player.transform.position;
        int fl = indoors ? 0 : FloorAt(pp);
        if (cam != null && !transitioning)
        {
            if (CameraController.Instance != null) CameraController.Instance.cinematic = true;
            Vector3 target = new Vector3(pp.x, pp.y, cam.transform.position.z);
            float size = indoors ? 4.3f : fl > 0 ? 4f : 6.5f;      // sous la montagne : vue rapprochée
            var room = fl > 0 && CrawlerFog.Instance != null ? CrawlerFog.Instance.CurrentRoom : null;
            // une salle à la fois : la caméra se cale sur la salle (murs compris) tant qu'on y est. Le passage
            // salle ↔ couloir se fait en fondu (plus de saut au seuil des portes)
            if (room.HasValue) camRoom = room.Value;
            camRoomBlend = Mathf.MoveTowards(camRoomBlend, room.HasValue ? 1f : 0f, Time.unscaledDeltaTime * 2.2f);
            if (fl <= 0) camRoomBlend = 0f;
            if (camRoomBlend > 0f)
            {
                var r = camRoom;
                float hw = size * cam.aspect, hh = size;
                float cx = r.x + r.width * 0.5f, cy = r.y + r.height * 0.5f;
                var locked = target;
                locked.x = r.width + 2 > hw * 2f ? Mathf.Clamp(target.x, r.xMin - 1 + hw, r.xMax + 1 - hw) : cx;
                locked.y = r.height + 2 > hh * 2f ? Mathf.Clamp(target.y, r.yMin - 1 + hh, r.yMax + 1 - hh) : cy;
                target = Vector3.Lerp(target, locked, Mathf.SmoothStep(0f, 1f, camRoomBlend));
            }
            if (indoors)
            {
                // la caméra reste dans la pièce
                var r = interior.RoomRect;
                float hw = size * cam.aspect, hh = size;
                target.x = r.width > hw * 2f ? Mathf.Clamp(target.x, r.xMin + hw - 0.5f, r.xMax - hw + 0.5f) : r.center.x;
                target.y = r.height > hh * 2f ? Mathf.Clamp(target.y, r.yMin + hh - 1f, r.yMax - hh) : r.center.y;
            }
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, Time.unscaledDeltaTime * 2f);
        }
        // Dedans : on voit l'intérieur de la montagne ; dehors : le volcan
        var relief = MountainRelief.Instance;
        bool dungeonView = fl > 0;
        if (relief != null && relief.UserDungeonView != dungeonView && !indoors) relief.SetUserView(dungeonView);

        if (!indoors && fl != floor)
        {
            if (fl == 1 && floor == 0) { OnEnterDungeon(); trip++; }
            if (fl == 0 && floor == 1) GameHUD.Toast("Vous ressortez à l'air libre", GameHUD.InfoColor);
            if (fl == 0 && floor >= 1)
            {
                CrawlerOverlord.Instance?.OnTripEnd();   // le Noyau a le champ libre
                SaveSystem.Instance?.AutoSaveCrawler();  // retour en ville : sauvegarde automatique
            }
            floor = fl;
            CrawlerContracts.OnFloor(floor);
            if (floor > deepest)
            {
                deepest = floor;
                GameHUD.Toast("Nouvel étage : +1 point de talent (touche K)", GameHUD.GoldColor);
                if (System.Array.IndexOf(Checkpoints, floor) >= 0)
                    GameHUD.Toast($"Étage {floor} atteint : la Guilde peut désormais vous y faire descendre directement", GameHUD.GoldColor);
            }
        }
        if (checkpointCircle != null) checkpointCircle.SetActive(Checkpoint() > 1);
        UpdateFog(indoors ? 0 : fl, pp);
        UpdateHud();
    }

    private int fogFloor = -1;
    private RectInt camRoom;
    private float camRoomBlend;

    /// <summary>Brouillard de guerre : sous la montagne, seule la salle du héros est visible.</summary>
    private void UpdateFog(int fl, Vector2 pp)
    {
        var fog = CrawlerFog.Ensure();
        if (fog == null) return;
        if (fl <= 0 || !floors.TryGetValue(fl, out var f))
        {
            if (fogFloor != -1) { fog.Hide(); fogFloor = -1; }
            return;
        }
        // nouvel étage, ou étage recréé (même numéro, nouvelles salles)
        if (fogFloor != fl || fogData != f)
        {
            fogFloor = fl;
            fogData = f;
            fog.SetFloor(f, FogArea(f));
        }
        fog.Tick(pp);
    }

    private CrawlerDungeon.Floor fogData;

    /// <summary>Étages actuellement creusés (un par zone souterraine) : ceux sur lesquels l'Esprit du Noyau peut agir.</summary>
    public IEnumerable<CrawlerDungeon.Floor> LiveFloors()
    {
        foreach (var kv in regionFloor)
            if (floors.TryGetValue(kv.Value, out var f)) yield return f;
    }

    public bool ChestLooted(string key) => lootedChests.Contains(key);

    /// <summary>Zone couverte : les salles de l'étage et une large marge, dans les limites de sa couche.</summary>
    private static RectInt FogArea(CrawlerDungeon.Floor f)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (var r in f.rooms) { x0 = Mathf.Min(x0, r.rect.xMin); y0 = Mathf.Min(y0, r.rect.yMin); x1 = Mathf.Max(x1, r.rect.xMax); y1 = Mathf.Max(y1, r.rect.yMax); }
        const int M = 14;
        var a = new RectInt(x0 - M, y0 - M, x1 - x0 + 2 * M, y1 - y0 + 2 * M);
        var layers = DungeonLayers.Instance;
        if (layers != null && layers.layers.Count > f.region)
        {
            var lb = layers.layers[f.region].cellBounds;
            int ax = Mathf.Max(a.xMin, lb.xMin), ay = Mathf.Max(a.yMin, lb.yMin);
            int bx = Mathf.Min(a.xMax, lb.xMax), by = Mathf.Min(a.yMax, lb.yMax);
            if (bx > ax && by > ay) a = new RectInt(ax, ay, bx - ax, by - ay);
        }
        return a;
    }

    /// <summary>Étage d'une position (0 : dehors, en ville ou à l'intérieur d'un bâtiment).</summary>
    public int FloorAt(Vector2 p)
    {
        var t = DungeonTerritory.Instance;
        if (t == null || !t.IsInside(p)) return 0;
        var layers = DungeonLayers.Instance;
        int region = layers != null ? layers.IndexAt(p) : 0;
        return regionFloor.TryGetValue(region, out int f) ? f : 0;
    }

    private int Checkpoint()
    {
        int c = 1;
        foreach (var cp in Checkpoints) if (deepest >= cp) c = cp;
        return c;
    }

    /// <summary>Le donjon est vivant : en votre absence, il fait repousser une partie de ses créatures.</summary>
    private void OnEnterDungeon()
    {
        // l'Esprit du Noyau a agi pendant l'expédition précédente (repeuplement, salles, évolutions) : il le fait sentir
        killedSinceTown = 0;
        CrawlerOverlord.Instance?.OnEnterDungeon();
    }

    // ─────────────────────────────────────────────
    // ÉTAGES
    // ─────────────────────────────────────────────

    /// <summary>Descend (ou remonte) à un étage ; l'étage est (re)créé s'il n'existe plus.</summary>
    public void GoToFloor(int target, bool fromAbove)
    {
        if (transitioning || target < 1 || target > CrawlerDungeon.FloorCount) return;
        StartCoroutine(FloorRoutine(target, fromAbove));
    }

    private IEnumerator FloorRoutine(int target, bool fromAbove)
    {
        transitioning = true;
        yield return Fade(1f, 0.35f);
        var f = Materialize(target);
        Vector3 at = fromAbove
            ? (target == 1 ? f.Start.Center : StairsUpPos(f) + Vector3.down * 1.3f)
            : StairsDownPos(f) + Vector3.down * 1.3f;
        TeleportParty(at);
        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(at.x, at.y, cam.transform.position.z);
        MountainRelief.Instance?.SetUserView(true);
        Sfx.Play(fromAbove ? "dig" : "place", at, 0.6f);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Fade(0f, 0.45f);
        transitioning = false;
        ShowBanner($"Étage {target}", CrawlerDungeon.FloorNames[target]);
        if (target == CrawlerDungeon.FloorCount)
            StoryManager.Instance?.PlayOnce("crawler_lair",
                new StoryManager.Line(StoryManager.NarratorName, "L'air vibre. Tout au fond, une lueur bat comme un cœur : le cristal du Noyau."),
                new StoryManager.Line("???", "Tant de marches pour venir mourir ici... Approche, petit héros."));
        else if (target == 5 || target == 8)
            StoryManager.Instance?.PlayOnce("crawler_guardian_" + target,
                new StoryManager.Line(StoryManager.NarratorName, target == 5 ? "Des tambours de guerre résonnent au loin. Quelque chose de gros garde l'escalier." : "Un froid de tombe. Des murmures en langue morte montent des profondeurs."));
    }

    private CrawlerDungeon.Floor Materialize(int target)
    {
        int region = CrawlerDungeon.RegionOf(target);
        if (floors.TryGetValue(target, out var f) && regionFloor.TryGetValue(region, out int present) && present == target)
        {
            // étage final : peuplé au premier passage seulement
            if (target == CrawlerDungeon.FloorCount && f.spawned.Count == 0) FinishFloor(f);
            return f;
        }
        f = CrawlerDungeon.Generate(target, seed);
        floors[target] = f;
        regionFloor[region] = target;
        FinishFloor(f);
        UpdateSeeds();
        return f;
    }

    private void TeleportParty(Vector3 at)
    {
        Player.transform.position = at;
        var rb = Player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = at; rb.linearVelocity = Vector2.zero; }
        int i = 0;
        foreach (var c in CrawlerCompanion.All)
            if (c != null && !c.IsDead) c.WarpTo((Vector2)at + new Vector2((i++ % 2 == 0 ? -0.8f : 0.8f), -0.5f));
    }

    private void UseRecall()
    {
        if (floor <= 0) { GameHUD.Toast("Vous êtes déjà à l'air libre", GameHUD.InfoColor); return; }
        if (recallStones <= 0) { GameHUD.Toast("Pas de pierre de rappel (tour des mages)", GameHUD.WarningColor); return; }
        recallStones--;
        StartCoroutine(RecallRoutine());
    }

    private IEnumerator RecallRoutine()
    {
        transitioning = true;
        Fx.Play("revival", Player.transform.position, 2f, 14f, 16, null, false, null, new Color(0.6f, 0.8f, 1f));
        Sfx.Play("summon", Player.transform.position);
        yield return new WaitForSecondsRealtime(0.6f);
        yield return Fade(1f, 0.3f);
        Vector3 at = TavernDoor();
        TeleportParty(at);
        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(at.x, at.y, cam.transform.position.z);
        yield return Fade(0f, 0.4f);
        transitioning = false;
        GameHUD.Toast("La pierre de rappel vous ramène en ville", GameHUD.InfoColor);
    }

    // ─────────────────────────────────────────────
    // INTERACTIONS
    // ─────────────────────────────────────────────

    public void UsePotion()
    {
        if (potions <= 0) { GameHUD.Toast("Plus de potions (tour des mages)", GameHUD.WarningColor); return; }
        var c = Player.Combat;
        if (c.currentHealth >= c.maxHealth) return;
        if (Player.InGesture) return;
        potions--;
        float heal = c.maxHealth * 0.5f * (1f + CrawlerSkills.Get("potion"));
        Player.Gesture(false, 0.7f, () =>
        {
            c.currentHealth = Mathf.Min(c.maxHealth, c.currentHealth + heal);
            Sfx.Play("heal", Player.transform.position);
            Fx.Play("heal", Player.transform.position, 1.2f, 14f, 16, null, false, Player.transform, new Color(0.4f, 1f, 0.5f));
            FloatingText.Spawn(Player.transform.position + Vector3.up * 0.8f, $"+{Mathf.RoundToInt(heal)}", new Color(0.45f, 1f, 0.5f), 1.2f);
        }, PotionSprite());
    }

    /// <summary>Manger une ration (touche G ou bouton) : +45 de satiété.</summary>
    public void Eat()
    {
        if (Player == null || Player.InGesture) return;
        if (rations <= 0) { GameHUD.Toast("Plus de rations (taverne, coffres, chasse)", GameHUD.WarningColor); return; }
        if (satiety >= 95f) { GameHUD.Toast("Vous n'avez pas faim", GameHUD.InfoColor); return; }
        rations--;
        Player.Gesture(true, 1.1f, () =>
        {
            satiety = Mathf.Min(100f, satiety + 45f);
            FloatingText.Spawn(Player.transform.position + Vector3.up * 0.8f, "Miam", new Color(0.95f, 0.75f, 0.45f), 1f);
        });
    }

    /// <summary>Dépecer une carcasse : des rations de viande (2 pour un cerf ou un sanglier).</summary>
    private void Butcher(WildAnimal w)
    {
        int n = 0;
        while (w != null && w.TakeMeat()) n++;
        if (n == 0) return;
        rations += n;
        FloatingText.Spawn(Player.transform.position + Vector3.up * 0.8f, $"+{n} ration{(n > 1 ? "s" : "")} de viande", new Color(0.95f, 0.75f, 0.45f), 1.1f);
    }

    /// <summary>La faim monte doucement ; ses effets (jamais mortels) s'appliquent au héros.</summary>
    private void TickHunger()
    {
        if (Player == null || Frozen) return;
        bool indoors = InteriorSystem.Instance != null && InteriorSystem.Instance.Physical;
        if (!indoors) satiety = Mathf.Max(0f, satiety - 100f / satietyDuration * Time.deltaTime * (Player.Running ? 1.6f : 1f) * Seasons.HungerMult);
        Player.StaminaRegenMult = satiety <= 0f ? 0.4f : satiety < 25f ? 0.65f : 1f;
        var c = Player.Combat;
        c.damageMult = (satiety <= 0f ? 0.8f : 1f) * (c.currentHealth < c.maxHealth * 0.5f ? 1f + CrawlerSkills.Get("rage") : 1f);
        // affamé : les PV ne dépassent plus 80 %
        if (satiety <= 0f && c.currentHealth > c.maxHealth * 0.8f) c.currentHealth = c.maxHealth * 0.8f;
        if (satiety < 25f && satiety + 100f / satietyDuration * Time.deltaTime >= 25f)
            GameHUD.Toast("Votre ventre gargouille (G : manger)", GameHUD.WarningColor);
    }

    private Sprite potionSprite;
    private Sprite PotionSprite()
    {
        if (potionSprite == null) potionSprite = Resources.Load<Sprite>("Loot/loot_mana_potion");
        return potionSprite;
    }

    private SettlementBuilding DoorNear(float radius)
    {
        var colony = SettlementManager.Instance;
        if (colony == null || Player == null) return null;
        Vector2 p = Player.transform.position;
        foreach (var b in colony.Buildings)
        {
            if (b == null || !b.IsComplete || !InteriorSystem.HasInterior(b.def.id)) continue;
            if (Vector2.Distance(DoorOf(b), p) <= radius) return b;
        }
        return null;
    }

    /// <summary>Devant la porte (milieu du bas de la façade).</summary>
    private static Vector3 DoorOf(SettlementBuilding b) => b.transform.position + Vector3.down * 0.45f;

    private void EnterBuilding(SettlementBuilding b)
    {
        var party = new List<Transform> { Player.transform };
        foreach (var c in CrawlerCompanion.All) if (c != null && !c.IsDead) party.Add(c.transform);
        InteriorSystem.Instance?.EnterPhysical(b.def.id, DoorOf(b) + Vector3.down * 0.5f, party);
    }

    private string InteractionLabel(out System.Action act)
    {
        act = null;
        if (Player == null) return null;
        Vector2 p = Player.transform.position;
        var interior = InteriorSystem.Instance;
        if (interior != null && interior.Physical)
        {
            InteriorSystem.Spot best = null;
            float bd = 1.3f;
            foreach (var s in interior.Spots)
            {
                float d = Vector2.Distance(s.World, p);
                if (d < bd) { bd = d; best = s; }
            }
            if (best != null) { var key = best.key; act = () => OpenShop(key); return best.label; }
            if (Vector2.Distance(interior.DoorInside, p) < 1.1f) { act = () => interior.ExitPhysical(); return "Sortir"; }
            return null;
        }
        foreach (var w in WildAnimal.Carcasses)
        {
            if (w == null || w.Meat <= 0 || Vector2.Distance(w.transform.position, p) > 1.2f) continue;
            var carcass = w;
            act = () => Player.Gesture(true, 1.2f, () => Butcher(carcass));
            return $"Dépecer : {WildAnimal.Name(w.species).ToLowerInvariant()}";
        }
        foreach (var ch in TreasureChest.All)
        {
            if (ch == null || !ch.IsFull || Vector2.Distance(ch.transform.position, p) > 1.3f) continue;
            var chest = ch;
            act = () => Player.Gesture(true, 0.8f, () => OpenChest(chest));
            return "Ouvrir le coffre";
        }
        foreach (var st in CrawlerStairs.All)
        {
            if (st == null || Vector2.Distance(st.transform.position, p) > 1.2f) continue;
            int t = st.Target;
            bool down = st.down;
            if (st.Sealed)
            {
                string who = st.GuardianName;
                act = () => { GameHUD.Toast($"L'escalier est scellé : vaincre {who} pour descendre", GameHUD.WarningColor); Sfx.Play("ui_error", null, 0.5f); };
                return $"Scellé — vaincre {who}";
            }
            act = () => Player.Gesture(false, 0.35f, () => GoToFloor(t, down));
            return down ? $"Descendre : étage {t} — {CrawlerDungeon.FloorNames[t]}" : $"Remonter à l'étage {t}";
        }
        if (checkpointCircle != null && checkpointCircle.activeSelf && Vector2.Distance(checkpointCircle.transform.position, p) < 1.2f)
        {
            int cp = Checkpoint();
            act = () => GoToFloor(cp, true);
            return $"Cercle de passage : descendre à l'étage {cp}";
        }
        var b = DoorNear(1.4f);
        if (b != null) { act = () => Player.Gesture(false, 0.35f, () => EnterBuilding(b)); return $"Entrer : {b.def.name}"; }
        return null;
    }

    public void Interact()
    {
        if (shop != null) { CloseShop(); return; }
        if (Player != null && Player.InGesture) return;
        InteractionLabel(out var act);
        act?.Invoke();
    }

    private void OpenChest(TreasureChest chest)
    {
        int depth = Mathf.Max(1, floor);
        int g = 15 + 22 * depth + rng.Next(0, 10 + 5 * depth);
        gold += g;
        chest.Loot(null);
        chest.refillTime = 1e9f;   // un coffre pillé le reste
        var cell = DungeonBuilder.Instance.floorTilemap.WorldToCell(chest.transform.position);
        lootedChests.Add($"{floor}:{cell.x}:{cell.y}");
        Sfx.Play("coin", chest.transform.position);
        FloatingText.Spawn(chest.transform.position + Vector3.up * 0.8f, $"+{g} or", GameHUD.GoldColor, 1.4f);
        if (rng.Next(3) == 0) { potions++; FloatingText.Spawn(chest.transform.position + Vector3.up * 1.3f, "+1 potion", new Color(0.5f, 1f, 0.6f), 1.2f); }
        else if (rng.Next(2) == 0) { rations++; FloatingText.Spawn(chest.transform.position + Vector3.up * 1.3f, "+1 ration", new Color(0.95f, 0.75f, 0.45f), 1.2f); }
        CrawlerContracts.OnChest(floor);
        if (rng.Next(100) < 35) GiveItem(CrawlerGear.Roll(depth, Player.role, rng), chest.transform.position);
        else if (depth >= 3 && rng.Next(4) == 0) { recallStones++; FloatingText.Spawn(chest.transform.position + Vector3.up * 1.3f, "+1 pierre de rappel", new Color(0.6f, 0.8f, 1f), 1.2f); }
    }

    public void OnMonsterKilled(GobelinAI g)
    {
        var u = g.GetComponent<MonsterUnit>();
        int stage = u != null ? u.stage : 1;
        bool guardian = g.GetComponent<MonsterMind>()?.IsGuardian == true;
        if (guardian && floor > 0) bossesDefeated.Add(floor);
        int reward = 3 + 4 * stage + Mathf.Max(0, floor) * 2 + rng.Next(0, 5) + (guardian ? 150 : 0);
        CrawlerContracts.OnKill(u != null ? u.familyId : null, floor, guardian);
        if (guardian) GiveItem(CrawlerGear.Roll(Mathf.Max(1, floor), Player.role, rng, CrawlerGear.Rarity.Rare), g.transform.position);
        else if (rng.Next(1000) < 60 + 6 * floor) GiveItem(CrawlerGear.Roll(Mathf.Max(1, floor), Player.role, rng), g.transform.position);
        gold += reward;
        kills++;
        killedSinceTown++;
        CrawlerOverlord.Instance?.OnKill(g);
        FloatingText.Spawn(g.transform.position + Vector3.up * 0.9f, $"+{reward} or", GameHUD.GoldColor, 1.1f);
        if (rank < RankKills.Length - 1 && kills == RankKills[rank + 1])
            GameHUD.Toast($"Vous méritez le rang {GuildSystem.RankName(rank + 1)} : faites-le valider au hall de la Guilde", GameHUD.GoldColor);
    }

    public void OnPlayerDied()
    {
        if (transitioning) return;
        CrawlerOverlord.Instance?.OnPlayerDeath();
        StartCoroutine(Respawn());
    }

    private IEnumerator Respawn()
    {
        transitioning = true;
        GameHUD.Toast("Vous vous effondrez...", GameHUD.DangerColor);
        yield return new WaitForSecondsRealtime(2f);
        yield return Fade(1f, 0.4f);
        int lost = gold / 2;
        gold -= lost;
        Vector3 at = TavernDoor();
        Player.Revive(at);
        TeleportParty(at);
        foreach (var c in CrawlerCompanion.All) if (c != null && !c.IsDead) c.Combat.currentHealth = c.Combat.maxHealth;
        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(at.x, at.y, cam.transform.position.z);
        yield return Fade(0f, 0.5f);
        transitioning = false;
        StoryManager.Instance?.Play(new StoryManager.Line("La tavernière", lost > 0
            ? $"On t'a ramassé à l'entrée de la grotte. Les soins, ça se paie : {lost} pièces. Repose-toi, et n'y retourne pas les mains vides."
            : "On t'a ramassé à l'entrée de la grotte. Repose-toi... et apprends à fuir."));
    }

    public void OnCompanionDied(CrawlerCompanion c)
    {
        GameHUD.Toast($"{c.displayName} est tombé(e) au combat", GameHUD.DangerColor);
        int i = 0;
        foreach (var o in CrawlerCompanion.All) if (o != null && !o.IsDead) o.slot = i++;
    }

    private Vector3 TavernDoor()
    {
        var colony = SettlementManager.Instance;
        if (colony != null)
            foreach (var b in colony.Buildings)
                if (b != null && b.def.id == "house_1") return DoorOf(b) + Vector3.down * 0.6f;
        var spawner = FindFirstObjectByType<AdventurerSpawner>();
        return spawner != null ? spawner.SpawnPosition : Vector3.zero;
    }

    // ─────────────────────────────────────────────
    // HABITANTS ET BOUTIQUES
    // ─────────────────────────────────────────────

    private int WeaponCost => 60 + 55 * weaponLvl;
    private int ArmorCost => 50 + 50 * armorLvl;
    private float HpMult => (1f + 0.25f * armorLvl) * (1f + 0.08f * rank) * (1f + GearSum(CrawlerGear.Stat.Health)) * (1f + CrawlerSkills.Get("hp"));
    private float DmgMult => (1f + 0.22f * weaponLvl) * (1f + 0.08f * rank) * (1f + GearSum(CrawlerGear.Stat.Damage)) * (1f + CrawlerSkills.Get("dmg"));
    public int PartyCount { get { int n = 0; foreach (var c in CrawlerCompanion.All) if (c != null && !c.IsDead) n++; return n; } }

    private void RefreshStats(bool heal)
    {
        Player.ApplyStats(HpMult, DmgMult, heal);
        Player.SpeedMult = 1f + GearSum(CrawlerGear.Stat.Speed) + CrawlerSkills.Get("speed");
        Player.GearStaminaMult = 1f + GearSum(CrawlerGear.Stat.Stamina) + CrawlerSkills.Get("stamina");
        Player.Combat.critChance = GearSum(CrawlerGear.Stat.Crit) + CrawlerSkills.Get("crit");
        Player.Combat.dodgeChance = Mathf.Min(0.5f, GearSum(CrawlerGear.Stat.Dodge) + CrawlerSkills.Get("dodge"));
        Player.Combat.damageTakenMult = Mathf.Max(0.4f, 1f - CrawlerSkills.Get("taken"));
        Player.Combat.lifeSteal = CrawlerSkills.Get("lifesteal");
        if (Player.role == AdventurerAI.Role.Mage) Player.Combat.splashRadius = 1.3f * (1f + CrawlerSkills.Get("splash"));
        Player.SetTier(1 + armorLvl);   // chaque renfort d'armure se voit
    }

    private static string ShopTitle(string key)
    {
        switch (key)
        {
            case "tavern_host": return "La tavernière";
            case "tavern_bard": return "Le barde";
            case "tavern_drinkers": return "Les buveurs";
            case "guild_master": return "Le maître de guilde";
            case "guild_board": return "Tableau des recrues";
            case "guild_talkers": return "Les aventuriers";
            case "forge_smith": case "forge_rack": return "La forgeronne";
            case "mage_seller": return "L'apothicaire";
            default: return "L'orbe de scrutation";
        }
    }

    /// <summary>Compatibilité : encart des intérieurs « panneau » (identifiant de bâtiment).</summary>
    public void BuildShop(RectTransform box, string id) =>
        FillShop(box, id == "house_1" ? "tavern_host" : id == "house_3" ? "forge_smith" : id == "tent_magic" ? "mage_seller" : "guild_master");

    private void OpenShop(string key)
    {
        // les simples bavards : quelques répliques
        if (key == "tavern_bard" || key == "tavern_drinkers" || key == "guild_talkers")
        {
            var r = Rumors.Generate(2);
            var lines = new List<StoryManager.Line>();
            if (key == "tavern_bard") lines.Add(new StoryManager.Line("Le barde", "♪ Dix marches de pierre, dix portes de nuit... le cristal attend celui qui ne fuit ! ♪"));
            foreach (var x in r) lines.Add(new StoryManager.Line(x.who, x.text));
            if (key == "guild_talkers" && deepest < 4) lines.Add(new StoryManager.Line("Aventurière", "Seul au-delà du troisième étage ? Recrute au tableau, ou prépare ton épitaphe."));
            StoryManager.Instance?.Play(lines.ToArray());
            return;
        }
        if (key == "mage_orb")
        {
            int next = Mathf.Clamp(deepest + 1, 1, CrawlerDungeon.FloorCount);
            StoryManager.Instance?.Play(
                new StoryManager.Line("L'orbe de scrutation", $"Des images montent dans le cristal... L'étage {next}, « {CrawlerDungeon.FloorNames[next]} »."),
                new StoryManager.Line("L'apothicaire", next <= 3 ? "Des bestioles, surtout. Rats, slimes, gobelins. Garde tes potions pour plus bas."
                    : next <= 5 ? "Des orcs, des morts qui marchent. Et au cinquième, un seigneur de guerre garde l'escalier."
                    : next <= 7 ? "Gnolls, spectres, golems... Là-bas, une soigneuse vaut plus qu'une épée."
                    : next <= 9 ? "Des liches. Des vampires. Une archiliche au huitième. Ne descends pas sans groupe."
                    : "Le cristal. Et ce qui le garde. Que les dieux t'accompagnent."));
            return;
        }
        if (shop != null) CloseShop();
        shop = UIKit.Rect("Boutique", canvas.transform);
        UIKit.Stretch(shop);
        var blocker = UIKit.Image(shop, new Color(0f, 0f, 0f, 0.35f), false);
        var box = UIKit.PanelBox("Encart", shop, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(620, 700));
        UIKit.LabelAt(box, ShopTitle(key), 32, UIKit.Gold, new Vector2(24, -18), new Vector2(560, 44));
        FillShop(box, key);
        var close = UIKit.BtnAt(box, "Au revoir", CloseShop, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(300, 64), 26, UIKit.Button);
        shopKey = key;
    }

    private string shopKey;

    private void CloseShop()
    {
        if (shop != null) Destroy(shop.gameObject);
        shop = null;
        shopKey = null;
    }

    private void RefreshShop()
    {
        if (shopKey == null) return;
        string k = shopKey;
        CloseShop();
        OpenShop(k);
    }

    private void FillShop(RectTransform box, string key)
    {
        float y = 76f;
        void Line(string text, float h = 60f)
        {
            var t = UIKit.LabelAt(box, text, 20, UIKit.Text, new Vector2(24, -y), new Vector2(570, h));
            t.textWrappingMode = TextWrappingModes.Normal;
            // hauteur réelle du texte (retours à la ligne compris)
            h = Mathf.Max(28f, t.GetPreferredValues(text, 570f, 0f).y + 4f);
            t.rectTransform.sizeDelta = new Vector2(570f, h);
            y += h + 4f;
        }
        void Button(string label, System.Action a, bool ok)
        {
            UIKit.BtnAt(box, label, () => { a(); RefreshShop(); InteriorSystem.Instance?.RefreshPanel(); }, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(560, 56), 21, ok ? UIKit.Accent : UIKit.Disabled);
            y += 64f;
        }
        Line($"<color=#FFD966>Votre bourse : {gold} or</color>   ·   Potions : {potions}   ·   Pierres de rappel : {recallStones}", 34f);
        switch (key)
        {
            case "tavern_host":
                Line("<i>« Un lit, une soupe, et on n'en parle plus. Tes compagnons aussi, s'ils ont de quoi payer — c'est toi qui paies. »</i>", 70f);
                int cost = 10 + 5 * PartyCount;
                Button($"Se reposer ({cost} or) : santé pleine pour tout le groupe", () =>
                {
                    if (!Pay(cost)) return;
                    Player.Combat.currentHealth = Player.Combat.maxHealth;
                    foreach (var c in CrawlerCompanion.All) if (c != null && !c.IsDead) c.Combat.currentHealth = c.Combat.maxHealth;
                    GameHUD.Toast("Le groupe est reposé", GameHUD.InfoColor);
                }, gold >= cost);
                Button("Une soupe chaude (6 or) : satiété pleine", () => { if (Pay(6)) { satiety = 100f; Sfx.Play("coin"); GameHUD.Toast("Rassasié", GameHUD.InfoColor); } }, gold >= 6);
                Button("Provisions : 3 rations (14 or)", () => { if (Pay(14)) { rations += 3; Sfx.Play("coin"); } }, gold >= 14);
                foreach (var r in Rumors.Generate(1)) Line($"<color=#FFD9A0>{r.who}</color> : <i>« {r.text} »</i>", 80f);
                break;
            case "forge_smith":
            case "forge_rack":
                Line("<i>« Tout ce qui sort de ma forge revient cabossé. Quand ça revient. »</i>", 50f);
                Line($"Arme niveau {weaponLvl} (dégâts ×{DmgMult:0.00}) · Armure niveau {armorLvl} (PV ×{HpMult:0.00})", 40f);
                Button($"Aiguiser l'arme ({WeaponCost} or)", () => { if (Pay(WeaponCost)) { weaponLvl++; RefreshStats(false); Sfx.Play("hit_sword"); } }, gold >= WeaponCost);
                Button($"Renforcer l'armure ({ArmorCost} or)", () => { if (Pay(ArmorCost)) { armorLvl++; RefreshStats(false); Sfx.Play("hit_sword"); } }, gold >= ArmorCost);
                if (PartyCount > 0)
                    Button($"Équiper le groupe ({40 * PartyCount} or) : compagnons +15 % PV et dégâts", () =>
                    {
                        if (!Pay(40 * PartyCount)) return;
                        foreach (var c in CrawlerCompanion.All)
                            if (c != null && !c.IsDead) { c.Combat.maxHealth *= 1.15f; c.Combat.currentHealth = c.Combat.maxHealth; c.Combat.attackDamage *= 1.15f; }
                        Sfx.Play("hit_sword");
                    }, gold >= 40 * PartyCount);
                break;
            case "mage_seller":
                Line("<i>« Rouge pour les blessures. Bleu pour rentrer. Ne buvez pas les vertes. »</i>", 50f);
                Button("Potion de soins (25 or)", () => { if (Pay(25)) { potions++; Sfx.Play("coin"); } }, gold >= 25);
                Button("Trois potions (65 or)", () => { if (Pay(65)) { potions += 3; Sfx.Play("coin"); } }, gold >= 65);
                Button("Pierre de rappel (45 or) : retour en ville (touche T)", () => { if (Pay(45)) { recallStones++; Sfx.Play("coin"); } }, gold >= 45);
                break;
            case "guild_master":
            {
                int earned = 0;
                for (int i = 0; i < RankKills.Length; i++) if (kills >= RankKills[i]) earned = i;
                Line($"Votre rang : {GuildSystem.RankTag(rank)} · Monstres vaincus : {kills} · Étage le plus profond : {deepest}/{CrawlerDungeon.FloorCount}", 40f);
                if (CrawlerOverlord.Instance != null) Line($"<color=#D9A0FF>{CrawlerOverlord.Instance.Report()}</color>", 34f);
                if (earned > rank)
                    Button($"Faire valider le rang {GuildSystem.RankName(earned)} (+8 % par rang)", () =>
                    {
                        rank = earned; RefreshStats(true); Sfx.Play("levelup");
                        GameHUD.Toast($"Vous êtes désormais aventurier de rang {GuildSystem.RankName(rank)}", GameHUD.GoldColor);
                    }, true);
                else if (rank < RankKills.Length - 1) Line($"Prochain rang ({GuildSystem.RankName(rank + 1)}) : {RankKills[rank + 1]} monstres vaincus.", 34f);
                // prime d'exploration : chaque nouvel étage rapporté paie
                if (deepest > reportedDeepest)
                {
                    int bounty = 0;
                    for (int f = reportedDeepest + 1; f <= deepest; f++) bounty += 25 * f;
                    Button($"Rapporter votre exploration (prime : {bounty} or)", () => { reportedDeepest = deepest; gold += bounty; Sfx.Play("coin"); }, true);
                }
                // contrats
                if (contractsTrip != trip) { contractsTrip = trip; CrawlerContracts.Roll(deepest, rng); }
                Line("<color=#E8C77F>Contrats de la Guilde</color> <size=85%><color=#B9B3CC>(deux à la fois, renouvelés à chaque retour du donjon)</color></size>", 34f);
                foreach (var c in new List<CrawlerContracts.Contract>(CrawlerContracts.Active))
                {
                    var cc = c;
                    if (cc.Done)
                        Button($"Toucher la prime : {cc.Title} ({cc.gold} or + objet)", () =>
                        {
                            CrawlerContracts.Active.Remove(cc);
                            gold += cc.gold;
                            GiveItem(CrawlerGear.Roll(Mathf.Max(1, Mathf.Max(deepest, cc.minFloor)), Player.role, rng, cc.reward), Player.transform.position);
                            Sfx.Play("levelup");
                        }, true);
                    else Line($"• {cc.Title} — <color=#FFD966>{cc.Progress}</color>   <size=85%><color=#B9B3CC>prime : {cc.Reward}</color></size>", 34f);
                }
                foreach (var c in new List<CrawlerContracts.Contract>(CrawlerContracts.Offered))
                {
                    var cc = c;
                    Button($"Accepter : {cc.Title}  ({cc.gold} or + objet {CrawlerGear.RarityNames[(int)cc.reward].ToLower()})", () => CrawlerContracts.Accept(cc),
                        CrawlerContracts.Active.Count < CrawlerContracts.MaxActive);
                }
                int cp = Checkpoint();
                Line(cp > 1 ? $"Passage ouvert : le cercle de la première salle du donjon mène à l'étage {cp}." :
                              "La Guilde ouvre un passage direct vers les étages 4 et 7 dès qu'un aventurier les a atteints.", 60f);
                break;
            }
            case "guild_board":
            {
                if (recruits.Count == 0) RollRecruits();
                Line($"Votre groupe : {PartyCount}/{MaxParty} compagnons. Un compagnon tombé ne revient pas.", 34f);
                foreach (var c in CrawlerCompanion.All)
                    if (c != null && !c.IsDead) Line($"• {c.displayName} — {c.classLabel} de rang {GuildSystem.RankTag(c.rank)} ({Mathf.CeilToInt(c.Combat.currentHealth)}/{Mathf.RoundToInt(c.Combat.maxHealth)} PV)", 30f);
                Line("<color=#B9B3CC>Recrues disponibles :</color>", 28f);
                foreach (var r in new List<Recruit>(recruits))
                {
                    var rr = r;
                    Button($"{rr.name} — {ClassLabel(rr.role, rr.female)} de rang {GuildSystem.RankName(rr.rank)} ({rr.cost} or)", () => Hire(rr), gold >= rr.cost && PartyCount < MaxParty);
                }
                if (recruits.Count == 0) Line("<i>Plus personne ne veut descendre... Revenez plus tard.</i>", 34f);
                break;
            }
        }
    }

    private bool Pay(int cost)
    {
        if (gold < cost) { GameHUD.Toast("Pas assez d'or", GameHUD.WarningColor); return false; }
        gold -= cost;
        return true;
    }

    // ─────────────────────────────────────────────
    // GROUPE
    // ─────────────────────────────────────────────

    private static readonly string[] MaleNames = { "Aldric", "Cassian", "Elwin", "Garrick", "Joran", "Lothar", "Nils", "Perrin", "Sigur", "Ulric", "Wendel", "Bastien", "Oswin" };
    private static readonly string[] FemaleNames = { "Brunhild", "Dagna", "Fenna", "Hilde", "Isolde", "Kaela", "Maelis", "Orla", "Roswitha", "Talia", "Vesna", "Yseult", "Lise" };

    private void RollRecruits()
    {
        recruits.Clear();
        var roles = new List<AdventurerAI.Role> { AdventurerAI.Role.Warrior, AdventurerAI.Role.Archer, AdventurerAI.Role.Mage, AdventurerAI.Role.Healer };
        for (int i = 0; i < 4; i++)
        {
            var role = roles[rng.Next(roles.Count)];
            roles.Remove(role);
            int rk = Mathf.Clamp(rank + rng.Next(-1, 2), 0, GuildSystem.MaxRank);
            bool female = rng.Next(2) == 0;
            var pool = female ? FemaleNames : MaleNames;
            recruits.Add(new Recruit
            {
                name = pool[rng.Next(pool.Length)], role = role, rank = rk, female = female,
                cost = 45 + 40 * rk + (role == AdventurerAI.Role.Healer ? 20 : 0),
            });
        }
    }

    private void Hire(Recruit r)
    {
        if (PartyCount >= MaxParty || !Pay(r.cost)) return;
        recruits.Remove(r);
        Vector3 at = Player.transform.position + new Vector3(0.8f, -0.4f, 0f);
        SpawnCompanion(r.role, r.name, r.rank, r.female, at);
        Sfx.Play("levelup", at, 0.6f);
        GameHUD.Toast($"{r.name} rejoint votre groupe", GameHUD.GoldColor);
        StoryManager.Instance?.Play(new StoryManager.Line(r.name, new[]
        {
            "Tu paies, je cogne. On s'entend.", "Dix étages ? J'en ai vu d'autres. Enfin... pas vraiment.",
            "Reste derrière moi et tout ira bien.", "Si je tombe, garde ma part pour ma mère.",
        }[rng.Next(4)]));
    }

    private CrawlerCompanion SpawnCompanion(AdventurerAI.Role role, string name, int rank, bool female, Vector3 at)
    {
        var go = SpawnAdventurer(role, at, name);
        var c = go.AddComponent<CrawlerCompanion>();
        float m = 1f + 0.12f * rank;
        float hp = role == AdventurerAI.Role.Warrior ? 190 : role == AdventurerAI.Role.Healer ? 140 : role == AdventurerAI.Role.Archer ? 120 : 100;
        float dmg = role == AdventurerAI.Role.Warrior ? 15 : role == AdventurerAI.Role.Healer ? 8 : role == AdventurerAI.Role.Archer ? 12 : 18;
        c.Setup(role, name, rank, hp * m, dmg * m);
        c.classLabel = ClassLabel(role, female);
        c.female = female;
        c.slot = PartyCount - 1;
        HealthBar.Attach(go.transform, () => c.Combat.currentHealth / c.Combat.maxHealth, new Color(0.4f, 0.7f, 1f));
        NemesisSystem.AddTag(go.transform, name, new Color(0.7f, 0.85f, 1f));
        return c;
    }

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    private static Color? ParseColor(string s) => !string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c) ? c : (Color?)null;
    private static string ColorText(Color? c) => c.HasValue ? "#" + ColorUtility.ToHtmlStringRGB(c.Value) : "";

    private static GearSave SaveGear(CrawlerGear.Item it)
    {
        var g = new GearSave { name = it.name, slot = (int)it.slot, rarity = (int)it.rarity, level = it.level };
        foreach (var kv in it.stats) { g.statKeys.Add((int)kv.Key); g.statValues.Add(kv.Value); }
        return g;
    }

    private static CrawlerGear.Item LoadGear(GearSave g)
    {
        var it = new CrawlerGear.Item { name = g.name, slot = (CrawlerGear.Slot)g.slot, rarity = (CrawlerGear.Rarity)g.rarity, level = g.level };
        for (int i = 0; i < g.statKeys.Count && i < g.statValues.Count; i++) it.stats[(CrawlerGear.Stat)g.statKeys[i]] = g.statValues[i];
        return it;
    }

    private static ContractSave SaveContract(CrawlerContracts.Contract c) =>
        new ContractSave { kind = (int)c.kind, family = c.family, goal = c.goal, progress = c.progress, minFloor = c.minFloor, gold = c.gold, reward = (int)c.reward };

    private static CrawlerContracts.Contract LoadContract(ContractSave c) =>
        new CrawlerContracts.Contract { kind = (CrawlerContracts.Kind)c.kind, family = c.family, goal = c.goal, progress = c.progress, minFloor = c.minFloor, gold = c.gold, reward = (CrawlerGear.Rarity)c.reward };

    /// <summary>Tout ce qu'il faut pour reprendre l'aventure : le donjon se recrée depuis la graine.</summary>
    public CrawlerSave CaptureSave()
    {
        var s = new CrawlerSave
        {
            role = (int)(Player != null ? Player.role : AdventurerAI.Role.Warrior), seed = seed,
            heroName = HeroLook.HeroName, hair = ColorText(HeroLook.Hair), skin = ColorText(HeroLook.Skin), outfit = ColorText(HeroLook.Outfit),
            gold = gold, potions = potions, rations = rations, recallStones = recallStones, kills = kills,
            weaponLvl = weaponLvl, armorLvl = armorLvl, rank = rank, deepest = deepest, reportedDeepest = reportedDeepest, trip = trip, satiety = satiety,
        };
        foreach (var it in bag) if (it != null) s.bag.Add(SaveGear(it));
        foreach (var it in equipped) if (it != null) s.equipped.Add(SaveGear(it));
        s.skills.AddRange(CrawlerSkills.Owned);
        foreach (var c in CrawlerContracts.Offered) s.offered.Add(SaveContract(c));
        foreach (var c in CrawlerContracts.Active) s.active.Add(SaveContract(c));
        foreach (var c in CrawlerCompanion.All)
            if (c != null && !c.IsDead)
                s.companions.Add(new CompanionSave { name = c.displayName, role = (int)c.role, rank = c.rank, female = c.female,
                                                     health = c.Combat.maxHealth > 0f ? c.Combat.currentHealth / c.Combat.maxHealth : 1f });
        s.lootedChests.AddRange(lootedChests);
        s.bosses.AddRange(bossesDefeated);
        var o = CrawlerOverlord.Instance;
        if (o != null)
        {
            s.overlordEssence = o.essence; s.overlordSpent = o.Spent;
            s.overlordRooms = o.RoomsDug; s.overlordEvolved = o.Evolved; s.overlordReinforced = o.Reinforced; s.overlordTraps = o.TrapsSet;
        }
        return s;
    }

    /// <summary>Reprise : le héros se réveille à la taverne avec son équipement, ses talents et son groupe.</summary>
    private void ApplySave(CrawlerSave s)
    {
        gold = s.gold; potions = s.potions; rations = s.rations; recallStones = s.recallStones; kills = s.kills;
        weaponLvl = s.weaponLvl; armorLvl = s.armorLvl; rank = s.rank; deepest = s.deepest; reportedDeepest = s.reportedDeepest;
        trip = s.trip; satiety = s.satiety;
        bag.Clear();
        foreach (var g in s.bag) bag.Add(LoadGear(g));
        foreach (var g in s.equipped) { var it = LoadGear(g); equipped[(int)it.slot] = it; }
        CrawlerSkills.Restore(s.skills);
        CrawlerContracts.Reset();
        foreach (var c in s.offered) CrawlerContracts.Offered.Add(LoadContract(c));
        foreach (var c in s.active) CrawlerContracts.Active.Add(LoadContract(c));
        lootedChests.Clear();
        foreach (var k in s.lootedChests) lootedChests.Add(k);
        foreach (var b in s.bosses) bossesDefeated.Add(b);
        Player.SetTier(1 + armorLvl);
        Player.ApplyLook();
        RefreshStats(true);
        Vector3 at = TavernDoor();
        TeleportParty(at);
        int i = 0;
        foreach (var c in s.companions)
        {
            var comp = SpawnCompanion((AdventurerAI.Role)c.role, c.name, c.rank, c.female, at + new Vector3(i++ % 2 == 0 ? -0.8f : 0.8f, -0.5f, 0f));
            comp.Combat.currentHealth = Mathf.Max(1f, comp.Combat.maxHealth * Mathf.Clamp01(c.health));
        }
        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(at.x, at.y, cam.transform.position.z);
    }

    // ─────────────────────────────────────────────
    // VICTOIRE
    // ─────────────────────────────────────────────

    public void Victory()
    {
        if (victory) return;
        victory = true;
        Sfx.Play("core_break");
        CameraController.Instance?.Shake(0.3f, 1.2f);
        StartCoroutine(VictoryScreen());
    }

    private IEnumerator VictoryScreen()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        var box = UIKit.Stretch(UIKit.Rect("Victoire", canvas.transform));
        UIKit.Image(box, new Color(0.02f, 0.01f, 0.04f, 0.88f), false);
        var t = UIKit.LabelAt(box, "Le donjon de l'île est tombé", 64, UIKit.Gold, new Vector2(0, -260), new Vector2(1600, 90), TextAlignmentOptions.Center, new Vector2(0.5f, 1));
        t.rectTransform.pivot = new Vector2(0.5f, 1);
        float minutes = (Time.time - startTime) / 60f;
        var s = UIKit.LabelAt(box, $"Le cristal vole en éclats. Dans toutes les tavernes du continent, on chantera votre nom.\n\n" +
            $"Classe : <b>{ClassNames[(int)Player.role]}</b>   ·   Rang : <b>{GuildSystem.RankName(rank)}</b>   ·   Monstres vaincus : <b>{kills}</b>\n" +
            $"Compagnons survivants : <b>{PartyCount}</b>   ·   Or : <b>{gold}</b>   ·   Temps : <b>{minutes:0} min</b>", 28, UIKit.Text, new Vector2(0, -380), new Vector2(1400, 220), TextAlignmentOptions.Center, new Vector2(0.5f, 1));
        s.rectTransform.pivot = new Vector2(0.5f, 1);
        s.textWrappingMode = TextWrappingModes.Normal;
        var b = UIKit.BtnAt(box, "Retour à l'accueil", () => MainMenu.Instance?.GoHome(), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(420, 80), 30, UIKit.Accent);
        ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0);
    }

    // ─────────────────────────────────────────────
    // INTERFACE
    // ─────────────────────────────────────────────

    private void BuildHud()
    {
        var go = new GameObject("Crawler_Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();
        hud = UIKit.Stretch(UIKit.Rect("HUD", go.transform));
        SafeArea.Apply(hud);

        var panel = UIKit.PanelBox("Statut", hud, new Vector2(0, 1), new Vector2(16, -16), new Vector2(600, 166));
        title = UIKit.LabelAt(panel, "", 26, UIKit.Gold, new Vector2(18, -10), new Vector2(570, 34));
        var bg = UIKit.PanelBox("PV", panel, new Vector2(0, 1), new Vector2(18, -52), new Vector2(560, 20), new Color(0.15f, 0.08f, 0.1f, 1f));
        hpFill = UIKit.Rect("Fill", bg);
        UIKit.Image(hpFill, new Color(0.35f, 0.85f, 0.4f), false);
        hpFill.anchorMin = Vector2.zero; hpFill.anchorMax = new Vector2(1, 1); hpFill.pivot = new Vector2(0, 0.5f); hpFill.offsetMin = hpFill.offsetMax = Vector2.zero;
        // endurance : fine jauge sous les PV
        var sbg = UIKit.PanelBox("Endurance", panel, new Vector2(0, 1), new Vector2(18, -76), new Vector2(560, 9), new Color(0.1f, 0.1f, 0.12f, 1f));
        staminaFill = UIKit.Rect("Fill", sbg);
        staminaImg = UIKit.Image(staminaFill, new Color(0.95f, 0.8f, 0.35f), false);
        staminaFill.anchorMin = Vector2.zero; staminaFill.anchorMax = new Vector2(1, 1); staminaFill.pivot = new Vector2(0, 0.5f); staminaFill.offsetMin = staminaFill.offsetMax = Vector2.zero;
        // faim : jauge brune sous l'endurance
        var fbg = UIKit.PanelBox("Faim", panel, new Vector2(0, 1), new Vector2(18, -88), new Vector2(560, 9), new Color(0.1f, 0.09f, 0.08f, 1f));
        satietyFill = UIKit.Rect("Fill", fbg);
        satietyImg = UIKit.Image(satietyFill, new Color(0.78f, 0.55f, 0.3f), false);
        satietyFill.anchorMin = Vector2.zero; satietyFill.anchorMax = new Vector2(1, 1); satietyFill.pivot = new Vector2(0, 0.5f); satietyFill.offsetMin = satietyFill.offsetMax = Vector2.zero;
        stats = UIKit.LabelAt(panel, "", 20, UIKit.Text, new Vector2(18, -100), new Vector2(570, 60));
        stats.textWrappingMode = TextWrappingModes.Normal;
        partyText = UIKit.LabelAt(hud, "", 20, UIKit.Text, new Vector2(22, -192), new Vector2(560, 120));
        partyText.textWrappingMode = TextWrappingModes.Normal;

        var menu = UIKit.BtnAt(hud, "Menu", () => MainMenu.Instance?.OpenPause(), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(140, 60), 24);

        prompt = UIKit.LabelAt(hud, "", 28, new Color(1f, 0.95f, 0.8f), new Vector2(0, -24), new Vector2(1000, 44), TextAlignmentOptions.Center, new Vector2(0.5f, 1));
        UIKit.Outline(prompt, new Color32(10, 6, 16, 255), 0.2f);   // matériau partagé (pas un par texte)
        banner = UIKit.LabelAt(go.transform, "", 56, UIKit.Gold, new Vector2(0, 170), new Vector2(1400, 160), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
        banner.alpha = 0f;

        // joystick virtuel
        var joy = UIKit.Rect("Joystick", hud);
        joy.anchorMin = joy.anchorMax = joy.pivot = Vector2.zero;
        joy.anchoredPosition = new Vector2(60, 60);
        joy.sizeDelta = new Vector2(280, 280);
        var jbg = joy.gameObject.AddComponent<Image>();
        jbg.sprite = SpriteUtil.Circle;
        jbg.color = new Color(1f, 1f, 1f, 0.12f);
        var handle = UIKit.Rect("Handle", joy);
        handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(120, 120);
        var hi = handle.gameObject.AddComponent<Image>();
        hi.sprite = SpriteUtil.Circle;
        hi.color = new Color(1f, 1f, 1f, 0.4f);
        hi.raycastTarget = false;
        joy.gameObject.AddComponent<VirtualJoystick>().handle = handle;

        // boutons d'action
        Button Act(string label, Vector2 pos, Vector2 size, Color c, System.Action a)
        {
            var b = UIKit.BtnAt(hud, label, () => a(), new Vector2(1, 0), pos, size, 24, c);
            return b;
        }
        Act("Attaque", new Vector2(-40, 60), new Vector2(200, 200), new Color(0.55f, 0.2f, 0.22f, 0.9f), () => { if (Player) Player.touchAttack = true; });
        Act("Esquive", new Vector2(-260, 60), new Vector2(170, 110), new Color(0.18f, 0.42f, 0.5f, 0.9f), () => { if (Player) Player.touchRoll = true; });
        var sk = Act("Compétence", new Vector2(-260, 185), new Vector2(170, 110), new Color(0.3f, 0.25f, 0.55f, 0.9f), () => { if (Player) Player.touchSkill = true; });
        skillFill = UIKit.Rect("Recharge", (RectTransform)sk.transform);
        UIKit.Image(skillFill, new Color(1f, 1f, 1f, 0.18f), false).raycastTarget = false;
        skillFill.anchorMin = Vector2.zero; skillFill.anchorMax = new Vector2(1, 0); skillFill.pivot = new Vector2(0.5f, 0); skillFill.offsetMin = skillFill.offsetMax = Vector2.zero;
        Act("Potion", new Vector2(-450, 60), new Vector2(160, 80), new Color(0.2f, 0.45f, 0.3f, 0.9f), () => { if (Player) Player.touchPotion = true; });
        Act("Interagir", new Vector2(-40, 280), new Vector2(200, 80), new Color(0.45f, 0.38f, 0.15f, 0.9f), () => { if (Player) Player.touchInteract = true; });
        Act("Rappel", new Vector2(-450, 150), new Vector2(160, 80), new Color(0.2f, 0.3f, 0.5f, 0.9f), UseRecall);
        Act("Manger", new Vector2(-450, 240), new Vector2(160, 80), new Color(0.45f, 0.3f, 0.16f, 0.9f), () => { if (Player) Player.touchEat = true; });
        UIKit.BtnAt(hud, "Sac", GearWindow.Toggle, new Vector2(1, 1), new Vector2(-170, -16), new Vector2(140, 60), 24);
        talentsBtn = UIKit.BtnAt(hud, "Talents", SkillTreeWindow.Toggle, new Vector2(1, 1), new Vector2(-324, -16), new Vector2(160, 60), 24);

        // jauge du boss d'étage
        bossPanel = UIKit.PanelBox("Boss", hud, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(760, 74));
        bossPanel.pivot = new Vector2(0.5f, 1);
        bossName = UIKit.LabelAt(bossPanel, "", 22, new Color(1f, 0.75f, 0.6f), new Vector2(0, -6), new Vector2(720, 30), TextAlignmentOptions.Top, new Vector2(0.5f, 1));
        UIKit.StyleHead(bossName);
        var bbg = UIKit.PanelBox("Jauge", bossPanel, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(720, 22), new Color(0.15f, 0.05f, 0.06f, 1f));
        bbg.pivot = new Vector2(0.5f, 1);
        bossFill = UIKit.Rect("Fill", bbg);
        UIKit.Image(bossFill, new Color(0.85f, 0.22f, 0.2f), false);
        bossFill.anchorMin = Vector2.zero; bossFill.anchorMax = Vector2.one; bossFill.offsetMin = new Vector2(3, 3); bossFill.offsetMax = new Vector2(-3, -3);
        // repères des phases (66 % et 33 %)
        foreach (float mark in new[] { 0.66f, 0.33f })
        {
            var m = UIKit.Rect("Phase", bbg);
            m.anchorMin = new Vector2(mark, 0); m.anchorMax = new Vector2(mark, 1); m.sizeDelta = new Vector2(3, 0);
            UIKit.Image(m, new Color(1f, 0.9f, 0.7f, 0.7f), false, false);
        }
        bossPanel.gameObject.SetActive(false);

        fade = UIKit.Image(UIKit.Stretch(UIKit.Rect("Fondu", go.transform)), new Color(0f, 0f, 0f, 0f), false, false);
        fade.raycastTarget = false;
    }

    private IEnumerator Fade(float a, float t)
    {
        if (fade == null) yield break;
        float from = fade.color.a;
        for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / t)
        {
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, a, k));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, a);
    }

    private void ShowBanner(string head, string sub) => StartCoroutine(BannerRoutine(head, sub));

    private IEnumerator BannerRoutine(string head, string sub)
    {
        banner.text = $"<smallcaps>{head}</smallcaps>\n<size=60%><i>{sub}</i></size>";
        for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / 0.6f) { banner.alpha = k; yield return null; }
        banner.alpha = 1f;
        yield return new WaitForSecondsRealtime(2f);
        for (float k = 1f; k > 0f; k -= Time.unscaledDeltaTime / 0.8f) { banner.alpha = k; yield return null; }
        banner.alpha = 0f;
    }

    private void UpdateHud()
    {
        if (hud == null || Player == null) return;
        bool show = !victory && shop == null && (MainMenu.Instance == null || !MainMenu.Instance.InPause);
        hud.gameObject.SetActive(show);
        if (!show) return;
        var c = Player.Combat;
        title.text = $"{ClassNames[(int)Player.role]}  ·  rang {GuildSystem.RankTag(rank)}";
        hpFill.anchorMax = new Vector2(Mathf.Clamp01(c.currentHealth / c.maxHealth), 1f);
        staminaFill.anchorMax = new Vector2(Player.Stamina01, 1f);
        satietyFill.anchorMax = new Vector2(Mathf.Clamp01(satiety / 100f), 1f);
        satietyImg.color = satiety <= 0f ? Color.Lerp(new Color(0.85f, 0.35f, 0.25f), new Color(0.45f, 0.18f, 0.12f), Mathf.PingPong(Time.unscaledTime * 2f, 1f))
                                         : satiety < 25f ? new Color(0.9f, 0.45f, 0.25f) : new Color(0.78f, 0.55f, 0.3f);
        // à bout de souffle : la jauge rougit et clignote
        staminaImg.color = Player.Exhausted ? Color.Lerp(new Color(0.9f, 0.3f, 0.25f), new Color(0.5f, 0.15f, 0.12f), Mathf.PingPong(Time.unscaledTime * 3f, 1f))
                                            : new Color(0.95f, 0.8f, 0.35f);
        var interior = InteriorSystem.Instance;
        string where = interior != null && interior.Physical ? interior.CurrentTitle
                     : floor > 0 ? $"Étage {floor}/{CrawlerDungeon.FloorCount} — {CrawlerDungeon.FloorNames[floor]}" : "La ville du donjon";
        string hungerTag = satiety <= 0f ? "   ·   <color=#FF8A7A>affamé</color>" : satiety < 25f ? "   ·   <color=#FFC266>faim</color>" : "";
        stats.text = $"PV {Mathf.CeilToInt(c.currentHealth)}/{Mathf.RoundToInt(c.maxHealth)}   ·   <color=#FFD966>{gold} or</color>   ·   Potions {potions}   ·   Rations {rations}   ·   Rappel {recallStones}{hungerTag}\n" +
                     $"<size=85%><color=#B9B3CC>{where}   ·   record : étage {deepest}   ·   vaincus {kills}</color></size>";
        var sb = new System.Text.StringBuilder();
        foreach (var m in CrawlerCompanion.All)
        {
            if (m == null || m.IsDead) continue;
            float r = Mathf.Clamp01(m.Combat.currentHealth / m.Combat.maxHealth);
            string col = r > 0.6f ? "#7BE08A" : r > 0.3f ? "#FFD966" : "#FF6B6B";
            sb.Append($"<color=#A9C8FF>{m.displayName}</color> <size=80%>({m.classLabel})</size>  <color={col}>{Mathf.CeilToInt(m.Combat.currentHealth)} PV</color>\n");
        }
        foreach (var ct in CrawlerContracts.Active)
            sb.Append($"<size=85%><color=#E8C77F>Contrat</color> {ct.Title} <color={(ct.Done ? "#9CF0A8" : "#FFD966")}>{(ct.Done ? "rempli : prime au hall" : ct.Progress)}</color></size>\n");
        partyText.text = sb.ToString();
        skillFill.anchorMax = new Vector2(1f, Player.SkillReady);
        var boss = CrawlerBoss.Near(Player.transform.position);
        bossPanel.gameObject.SetActive(boss != null);
        if (boss != null)
        {
            bossName.text = $"{boss.Title}  <size=75%><color=#C8BFA8>phase {boss.Phase}/3</color></size>";
            bossFill.anchorMax = new Vector2(boss.Health01, 1f);
        }
        int pts = CrawlerSkills.Points;
        UIKit.SetLabel(talentsBtn, pts > 0 ? $"Talents <color=#FFD966>+{pts}</color>" : "Talents");
        string label = Frozen ? null : InteractionLabel(out _);
        prompt.text = label != null ? $"[E] {label}" : "";
    }
}
