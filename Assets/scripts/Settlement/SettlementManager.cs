using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Colonie humaine de l'île. Elle naît quand le donjon est découvert, puis grandit avec la prospérité :
/// Campement → Port → Hameau → Village → Ville.
///   - Les colons coupent les arbres (bois), cassent les rochers (pierre), pêchent et cultivent (nourriture)
///     et construisent les bâtiments prévus sur les emplacements du SettlementSite.
///   - Le moral baisse quand le donjon tue, monte quand des survivants rentrent avec des histoires.
///     Une colonie terrifiée cesse de construire et se vide ; une colonie prospère envoie plus de visiteurs.
///   - La colonie renforce les expéditions (bateaux plus gros, groupes plus nombreux, équipement).
/// </summary>
public class SettlementManager : MonoBehaviour
{
    public static SettlementManager Instance { get; private set; }

    [Header("État")]
    public int stage;
    public float prosperity;
    [Range(0f, 100f)] public float morale = 60f;
    public int wood, stone, food = 20;

    [Header("Réglages")]
    public int woodPerTree = 4;
    public int stonePerRock = 3;
    public int foodPerCatch = 3;
    public float chopTime = 5f, mineTime = 6f, fishTime = 8f;
    public int foodPerSettlerPerDay = 2;
    public int campSettlers = 3;

    public SettlementSite Site { get; private set; }
    public IReadOnlyList<SettlementBuilding> Buildings => buildings;
    public int Population => SettlerAI.All.Count;
    private bool everPopulated;
    /// <summary>La colonie a déjà eu des habitants (une colonie vide après ça a été vidée par vos monstres).</summary>
    public bool EverPopulated { get { if (Population > 0) everPopulated = true; return everPopulated; } }
    public int Capacity { get { int c = campSettlers; foreach (var b in buildings) if (b.IsComplete) c += b.def.pop; return c; } }
    public string StageName => SettlementPlan.StageNames[Mathf.Clamp(stage, 0, SettlementPlan.StageNames.Length - 1)];
    public bool HasBuilding(string id) => buildings.Any(b => b.def.id == id && b.IsComplete);

    /// <summary>Bonus des expéditions apportés par la colonie.</summary>
    public float AdventurerHpMult => !Outpost && HasBuilding("stall_weapons") ? 1.15f : 1f;
    public float AdventurerDmgMult => !Outpost && HasBuilding("training") ? 1.15f : 1f;
    public int PartyBonus => Outpost ? 0 : (stage >= 3 ? 1 : 0) + (stage >= 5 ? 1 : 0);
    public float ExpeditionIntervalReduction => Outpost ? 0f : stage * 0.12f;

    /// <summary>La colonie conquise est devenue un avant-poste du Noyau.</summary>
    public bool Outpost { get; private set; }

    public event System.Action OnChanged;

    /// <summary>Feu de camp terminé (lieu de repos des intrus), sinon null.</summary>
    public Vector2? CampfirePosition
    {
        get
        {
            foreach (var b in buildings) if (b.IsComplete && b.def.id == "campfire") return b.WorkSpot(0);
            return null;
        }
    }

    /// <summary>Centre des chantiers d'un stade (cinématiques) ; le port se regarde depuis le ponton.</summary>
    public Vector3 StageFocus(int forStage)
    {
        if (Site == null) return Vector3.zero;
        if (forStage == 2) return Site.LandingWorld + new Vector3(0f, 1.5f, 0f);
        Vector3 sum = Vector3.zero;
        int n = 0;
        // Premier chantier du stade (le feu de camp pour le campement) : le cœur de l'action
        if (forStage > 0)
            foreach (var b in buildings)
                if (b != null && b.def.stage == forStage) return b.transform.position + new Vector3(0f, 1f, 0f);
        foreach (var plot in Site.plots)
        {
            var def = SettlementPlan.Buildings.FirstOrDefault(d => d.id == plot.buildingId);
            if (def == null || (forStage > 0 && def.stage != forStage)) continue;
            sum += new Vector3(plot.rect.x + plot.rect.width * 0.5f, plot.rect.y + plot.rect.height * 0.5f, 0f);
            n++;
        }
        return n > 0 ? sum / n : Site.LandingWorld;
    }

    private readonly List<SettlementBuilding> buildings = new List<SettlementBuilding>();
    private readonly List<Transform> trees = new List<Transform>();
    private readonly HashSet<Transform> reservedTrees = new HashSet<Transform>();
    private readonly HashSet<Vector3Int> reservedRocks = new HashSet<Vector3Int>();
    private readonly List<Vector2> felled = new List<Vector2>();
    private readonly List<Vector3Int> mined = new List<Vector3Int>();
    private Tilemap sol, blocked, decor, trunks, routes;
    private int streetsState = -1;                       // -1 inconnu, 0 forêt, 1 terre, 2 briques
    private readonly HashSet<int> roadsDone = new HashSet<int>();
    private bool cityCleared, wallsCleared;
    private List<Transform> forestAll;
    private Transform root;
    private int expeditionsSeen;
    private float arrivalTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnNewDay -= OnNewDay; gm.OnNightChanged -= OnNightChanged; }
        if (ExpeditionManager.Instance != null) ExpeditionManager.Instance.OnExpeditionArrived -= OnExpeditionArrived;
    }

    private void Start()
    {
        Site = FindFirstObjectByType<SettlementSite>();
        if (Site == null) { enabled = false; return; }
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name == "Sol") sol = tm;
            else if (tm.name == "NoPathfinding") blocked = tm;
            else if (tm.name == "Decor") decor = tm;
            else if (tm.name == "trees_trunk") trunks = tm;
            else if (tm.name == "Routes") routes = tm;
        }
        WidenPier();
        root = new GameObject("Colonie").transform;
        var forest = GameObject.Find("Forêt");
        if (forest != null)
            foreach (Transform t in forest.transform)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("tree") || n.Contains("palm")) trees.Add(t);
            }
        var gm = GameManager.Instance;
        if (gm != null) { gm.OnNewDay += OnNewDay; gm.OnNightChanged += OnNightChanged; }
        if (ExpeditionManager.Instance != null) ExpeditionManager.Instance.OnExpeditionArrived += OnExpeditionArrived;
    }

    // ─────────────────────────────────────────────
    // CROISSANCE
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (Site == null) return;
        if (stage == 0)
        {
            var rep = ReputationSystem.Instance;
            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            // La Guilde ne s'installe que lorsque le donjon s'est taillé une réputation :
            // des survivants sont revenus avec des récits (et du butin), le bouche-à-oreille a fait le reste.
            // (jours comptés depuis le classement du donjon par la Guilde)
            int survivors = GameManager.Instance != null ? GameManager.Instance.statFled : 0;
            var guild = GuildSystem.Instance;
            bool known = rep != null && rep.dungeonDiscovered && (guild == null || guild.Assessed);
            int since = guild != null && guild.Assessed ? day - guild.assessedDay : day;
            bool famous = known && since >= SettlementPlan.FoundMinDay && survivors >= SettlementPlan.FoundSurvivors && rep.renown >= SettlementPlan.FoundRenown;
            if (famous || (known && since >= SettlementPlan.FoundLatestDay)) FoundCamp();
            return;
        }
        if (Outpost) return;
        // Passage au stade suivant : prospérité suffisante et chantiers du stade terminés
        if (stage < SettlementPlan.StageNames.Length - 1 && prosperity >= SettlementPlan.StageProsperity[stage + 1] &&
            buildings.All(b => b.IsComplete) && morale >= 25f)
            AdvanceStage();

        // Nouveaux colons (par bateau) tant qu'il y a de la place et de quoi manger
        arrivalTimer -= Time.deltaTime;
        if (arrivalTimer <= 0f)
        {
            arrivalTimer = 95f;
            if (Population < Capacity && food >= 12 && morale >= 35f)
            {
                food -= 4;
                var s = SpawnSettler();
                if (s != null)
                {
                    GameHUD.Toast($"Colonie : {s.Title} débarque", GameHUD.InfoColor);
                    Chronicle.Add($"{s.Title} débarque à la colonie.");
                }
            }
        }
    }

    /// <summary>La maison du héros existe-t-elle (chantier compris) ?</summary>
    public bool HasHeroHome => buildings.Any(b => b != null && b.def.id == "hero_home");
    public SettlementBuilding HeroHome => buildings.FirstOrDefault(b => b != null && b.def.id == "hero_home");

    /// <summary>
    /// Mode Aventurier : la maison commandée au charpentier. Le chantier est payé d'avance ; les colons le bâtissent
    /// sous les yeux du joueur (cadre de rondins, silhouette, barre d'avancement).
    /// </summary>
    public SettlementBuilding PlaceHeroHome(bool complete, float progress = 0f)
    {
        if (Site == null) return null;
        var found = HeroHome;
        if (found != null) return found;
        var def = SettlementPlan.Get("hero_home");
        var plot = Site.plots.Find(p => p.buildingId == "hero_home");
        if (def == null || plot == null) return null;
        for (int k = 0; k < 80; k++) { var t = TreeInRect(plot.rect); if (t == null) break; FellTree(t); t.gameObject.SetActive(false); }
        var b = SettlementBuilding.Create(def, plot.rect, root, complete);
        b.paid = true;
        if (!complete && progress > 0f) { b.progress = Mathf.Clamp01(progress); b.Work(0f); }
        buildings.Add(b);
        OnChanged?.Invoke();
        return b;
    }

    /// <summary>Mode Aventurier : la colonie est d'emblée une ville (tous les chantiers finis, parcelles dégagées).</summary>
    public void BuildInstantly(int toStage)
    {
        if (Site == null) return;
        if (stage == 0) FoundCamp();
        while (stage < toStage)
        {
            foreach (var b in buildings) if (!b.IsComplete) b.Complete();
            morale = 80f;
            AdvanceStage();
        }
        foreach (var b in buildings)
        {
            // emprise et parvis nets : ni arbre ni souche
            var r = b.rect.width > 0 ? new RectInt(b.rect.x - 1, b.rect.y - 3, b.rect.width + 2, b.rect.height + 4) : b.rect;
            if (r.width > 0) ClearTrees(p => r.Contains(Vector2Int.FloorToInt(p)), new Bounds(new Vector3(r.center.x, r.center.y, 0f), new Vector3(r.width + 2, r.height + 2, 1f)));
            if (!b.IsComplete) b.Complete();
        }
        RefreshCityLook();
        wood += 200; stone += 100; food += 120;
        OnChanged?.Invoke();
    }

    private void FoundCamp()
    {
        stage = 1;
        prosperity = Mathf.Max(prosperity, 10f);
        for (int i = 0; i < campSettlers; i++) SpawnSettler(i == 0 ? "settler_fisher" : null);
        CreateSites(1);
        RefreshCityLook();   // le chemin du camp
        GameHUD.Toast("Des colons installent un campement sur l'île !", GameHUD.WarningColor);
        StoryManager.Instance?.OnSettlementStage(1, StageName);
        OnChanged?.Invoke();
    }

    private void AdvanceStage()
    {
        stage++;
        CreateSites(stage);
        RefreshCityLook();
        GameHUD.Toast($"La colonie humaine devient : {StageName}", GameHUD.WarningColor);
        Chronicle.Add($"La colonie humaine devient : {StageName}.");
        StoryManager.Instance?.OnSettlementStage(stage, StageName);
        OnChanged?.Invoke();
    }

    private void CreateSites(int forStage)
    {
        foreach (var def in SettlementPlan.Buildings.Where(d => d.stage == forStage))
        {
            if (def.id == "pier")
            {
                buildings.Add(SettlementBuilding.Create(def, new RectInt(Site.landing.x - 1, Site.landing.y, 0, 0), root, false));
                continue;
            }
            var plot = Site.plots.Find(p => p.buildingId == def.id);
            if (plot == null) continue;
            var b = SettlementBuilding.Create(def, plot.rect, root, false);
            buildings.Add(b);
            if (def.id == "pier") b.transform.position = Site.LandingWorld;
        }
    }

    /// <summary>
    /// Allure de la cité selon le stade : rues de terre, puis pavées de briques quand la cité se fortifie.
    /// </summary>
    // ─────────────────────────────────────────────
    // FORÊT ET ROUTES : la forêt recule à mesure que la colonie grandit
    // ─────────────────────────────────────────────

    private void EnsureForestList()
    {
        if (forestAll != null) return;
        forestAll = new List<Transform>();
        var forest = GameObject.Find("Forêt");
        if (forest != null) foreach (Transform t in forest.transform) forestAll.Add(t);
    }

    /// <summary>Arbres effacés (route, rue, emprise) : sans souche, la place est nette. Une seule mise à jour du graphe.</summary>
    private void ClearTrees(System.Func<Vector2, bool> inside, Bounds area)
    {
        EnsureForestList();
        bool any = false;
        foreach (var t in forestAll)
        {
            if (t == null || !t.gameObject.activeSelf || !inside(t.position)) continue;
            trees.Remove(t);
            reservedTrees.Remove(t);
            if (trunks != null) trunks.SetTile(trunks.WorldToCell(t.position), null);
            t.gameObject.SetActive(false);
            any = true;
        }
        if (any) AstarPath.active?.UpdateGraphs(area, 0.2f);
    }

    private static Bounds BoundsOf(IList<Vector3Int> cells)
    {
        var b = new Bounds(new Vector3(cells[0].x + 0.5f, cells[0].y + 0.5f, 0f), Vector3.one);
        foreach (var c in cells) b.Encapsulate(new Vector3(c.x + 0.5f, c.y + 0.5f, 0f));
        b.Expand(new Vector3(3f, 3f, 0f));
        return b;
    }

    /// <summary>Une route ou une rue apparaît : terre battue, arbres et cailloux dégagés de part et d'autre.</summary>
    private void OpenRoad(IList<Vector3Int> cells, TileBase tile)
    {
        if (cells == null || cells.Count == 0) return;
        var set = new HashSet<Vector3Int>(cells);
        if (routes != null && tile != null)
        {
            var tiles = new TileBase[cells.Count];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            routes.SetTiles(new List<Vector3Int>(cells).ToArray(), tiles);
        }
        if (decor != null) foreach (var c in cells) if (decor.HasTile(c)) decor.SetTile(c, null);
        ClearTrees(p =>
        {
            var c = Vector3Int.FloorToInt(p);
            return set.Contains(c) || set.Contains(c + Vector3Int.left) || set.Contains(c + Vector3Int.right) || set.Contains(c + Vector3Int.up);
        }, BoundsOf(cells));
    }

    /// <summary>Travaux des stades atteints : routes entre les quartiers, rues de la cité, forêt de la cité éclaircie, enceinte.</summary>
    private void ApplyStageWorks()
    {
        if (Site == null || Outpost) return;
        if (Site.roads != null)
            for (int i = 0; i < Site.roads.Count; i++)
            {
                var rs = Site.roads[i];
                if (rs.stage > stage || roadsDone.Contains(i)) continue;
                roadsDone.Add(i);
                OpenRoad(rs.cells, Site.streetDirt);
            }
        // le hameau gagne l'emplacement de la cité : on y trace les rues, la forêt s'éclaircit (quelques arbres restent dans les jardins)
        if (stage >= 3 && !cityCleared && Site.cityRect.width > 0)
        {
            cityCleared = true;
            var r = Site.cityRect;
            var keepOut = new HashSet<Vector3Int>(Site.cityStreets);
            foreach (var pl in Site.plots)
                if (r.Overlaps(pl.rect))
                    for (int x = pl.rect.x - 2; x < pl.rect.xMax + 2; x++)
                        for (int y = pl.rect.y - 3; y < pl.rect.yMax + 1; y++) keepOut.Add(new Vector3Int(x, y, 0));
            ClearTrees(p =>
            {
                var c = Vector3Int.FloorToInt(p);
                if (!r.Contains(new Vector2Int(c.x, c.y))) return false;
                if (keepOut.Contains(c) || keepOut.Contains(c + Vector3Int.left) || keepOut.Contains(c + Vector3Int.right)) return true;
                uint h = (uint)(c.x * 73856093 ^ c.y * 19349663);
                return (h % 100) >= 30;   // trois arbres sur dix restent : vergers et jardins
            }, new Bounds(new Vector3(r.center.x, r.center.y, 0f), new Vector3(r.width + 4, r.height + 4, 1f)));
        }
        if (stage >= SettlementPlan.PalisadeStage && !wallsCleared && Site.cityWall != null && Site.cityWall.Count > 0)
        {
            wallsCleared = true;
            var wall = new HashSet<Vector3Int>(Site.cityWall);
            foreach (var g in Site.cityGates) wall.Add(g);
            ClearTrees(p =>
            {
                var c = Vector3Int.FloorToInt(p);
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 2; dy++) if (wall.Contains(c + new Vector3Int(dx, dy, 0))) return true;
                return false;
            }, BoundsOf(Site.cityWall));
        }
    }

    public void RefreshCityLook()
    {
        if (Site == null) return;
        ApplyStageWorks();
        // passants : la ville vit (dès le hameau, plus nombreux à chaque stade ; la ville complète en mode Aventurier)
        int folk = Outpost || stage < 3 ? 0 : CrawlerMode.Active ? 16 : 4 + 3 * (stage - 3);
        if (root != null) TownFolk.Populate(this, folk, root);
        // enceinte : palissade au stade Village, remparts de pierre une fois fortifiée
        CityWalls.Apply(Site, Outpost ? CityWalls.Kind.None
            : stage >= SettlementPlan.FortifiedStage ? CityWalls.Kind.Stone
            : stage >= SettlementPlan.PalisadeStage ? CityWalls.Kind.Palisade : CityWalls.Kind.None);
        if (routes == null || Site.cityStreets == null || Site.cityStreets.Count == 0) return;
        // rues de la cité : rien avant le hameau, terre battue ensuite, briques une fois fortifiée
        int want = Outpost || stage < 3 ? 0 : stage >= SettlementPlan.FortifiedStage ? 2 : 1;
        if (want == streetsState) return;
        streetsState = want;
        if (want == 0) return;
        OpenRoad(Site.cityStreets, want == 2 ? Site.streetBrick : Site.streetDirt);
    }

    public void OnBuildingComplete(SettlementBuilding b)
    {
        prosperity += 3f;
        GameHUD.Toast($"Colonie : {b.def.name} terminé", GameHUD.InfoColor);
        if (b.def.id == "pier") BuildPier();
        OnChanged?.Invoke();
    }

    private void OnExpeditionArrived(AdventurerTier tier, int count)
    {
        expeditionsSeen++;
        if (stage > 0) prosperity += 5f;
    }

    /// <summary>Un intrus quitte l'île (vivant) ou meurt dans le donjon.</summary>
    public void OnIntruderGone(bool killed)
    {
        if (stage == 0) return;
        if (killed) { morale = Mathf.Max(0f, morale - 5f); prosperity = Mathf.Max(0f, prosperity - 2f); }
        else { morale = Mathf.Min(100f, morale + 2f); prosperity += 4f; }
        OnChanged?.Invoke();
    }

    private void OnNewDay(int day)
    {
        if (stage == 0) return;
        if (Outpost) { OutpostIncome(); return; }
        int pop = Population;
        foreach (var b in buildings) if (b.IsComplete) { food += Mathf.RoundToInt(b.def.foodPerDay * Seasons.FoodMult); prosperity += b.def.prosperityPerDay; }
        food -= pop * foodPerSettlerPerDay;
        if (food < 0) { food = 0; morale -= 6f; GameHUD.Toast("La colonie a faim", GameHUD.InfoColor); }
        prosperity += pop * 0.45f + stage * 0.8f + (morale - 50f) / 12f;
        morale = Mathf.MoveTowards(morale, 60f, 4f);

        // Terreur : les colons plient bagage
        if (morale < 20f && pop > 1)
        {
            var leaver = SettlerAI.All[Random.Range(0, SettlerAI.All.Count)];
            leaver.CancelJob();
            leaver.Assign(new SettlerJob { label = "Départ", target = DockEnd(), duration = 0.3f, onDone = () => Destroy(leaver.gameObject) });
            GameHUD.Toast($"Par peur du donjon, {leaver.Title} quitte l'île", GameHUD.GoldColor);
            Chronicle.Add($"Par peur du donjon, {leaver.Title} quitte l'île.");
        }

        // Des curieux de la colonie viennent visiter le donjon (et y laissent du mana... ou leur vie)
        var rep = ReputationSystem.Instance;
        if (rep != null && rep.dungeonDiscovered && Random.value < 0.25f + 0.08f * stage && morale > 30f)
            ExpeditionManager.Instance?.LaunchLocalVisitor(Site.LandingWorld + Vector3.up * 2f);
        OnChanged?.Invoke();
    }

    private void OnNightChanged(bool night)
    {
        if (night)
        {
            foreach (var s in SettlerAI.All.ToArray())
            {
                s.CancelJob();
                var home = HomeFor(s);
                s.Assign(new SettlerJob { label = "Repos", target = home, duration = 0.4f, hideWhenDone = true });
            }
        }
        else
        {
            foreach (var s in SettlerAI.All) if (s.IsHidden) s.SetVisible(true);
        }
    }

    /// <summary>Maison attitrée (la moins pleine des habitations achevées), sinon le campement.</summary>
    private Vector2 HomeFor(SettlerAI s)
    {
        if (s.home == null || !s.home.IsComplete)
        {
            s.home = null;
            SettlementBuilding best = null;
            int bestFree = 0;
            foreach (var b in buildings)
            {
                if (!b.IsComplete || b.def.pop <= 0) continue;
                int free = b.def.pop - Residents(b);
                if (free > bestFree) { bestFree = free; best = b; }
            }
            s.home = best;
        }
        if (s.home == null) return (Vector2)Site.LandingWorld + Vector2.up + Random.insideUnitCircle * 0.5f;
        int i = 0;
        foreach (var o in SettlerAI.All) { if (o == s) break; if (o.home == s.home) i++; }
        return s.home.WorkSpot(i);
    }

    public int Residents(SettlementBuilding b)
    {
        int n = 0;
        foreach (var o in SettlerAI.All) if (o != null && o.home == b) n++;
        return n;
    }

    /// <summary>Nom d'une maison pour la fenêtre Colonie (« Maison 2 »).</summary>
    public string HomeName(SettlementBuilding b)
    {
        if (b == null) return "sous la tente";
        int i = 0;
        foreach (var o in buildings) if (o.def.id == b.def.id && o.IsComplete) { i++; if (o == b) break; }
        return $"{b.def.name} {i}";
    }

    // ─────────────────────────────────────────────
    // COLONS
    // ─────────────────────────────────────────────

    private Vector2 DockEnd() => Site.dockCells.Count > 0 ? (Vector2)Site.CellCenter(Site.dockCells[Site.dockCells.Count - 1]) : (Vector2)Site.LandingWorld;

    private SettlerAI SpawnSettler(string id = null, SettlerSave from = null)
    {
        var cat = DungeonCatalog.Get();
        GameObject prefab = null;
        if (from != null && !string.IsNullOrEmpty(from.prefab)) prefab = cat.settlerPrefabs.Find(p => p != null && p.name == from.prefab);
        if (prefab == null && id != null) prefab = cat.settlerPrefabs.Find(p => p != null && p.name.Contains("Pêcheur"));
        if (prefab == null && cat.settlerPrefabs.Count > 0) prefab = cat.settlerPrefabs[Random.Range(0, cat.settlerPrefabs.Count)];
        if (prefab == null) return null;
        var go = Instantiate(prefab, (Vector3)DockEnd() + (Vector3)(Random.insideUnitCircle * 0.3f), Quaternion.identity, root);
        var s = go.GetComponent<SettlerAI>();
        if (s == null) return null;
        s.prefabId = prefab.name;
        everPopulated = true;
        if (from != null)
        {
            s.settlerName = from.name;
            s.profession = from.profession;
            s.arrivedDay = from.arrivedDay;
        }
        else
        {
            s.settlerName = SettlerNames.Pick();
            s.profession = prefab.name.Contains("Pêcheur") || id != null ? "Pêcheur" : SettlerNames.Professions[Random.Range(0, SettlerNames.Professions.Length)];
            s.arrivedDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        }
        go.name = "Colon " + s.settlerName;
        return s;
    }

    private static readonly string[] EveningTalk =
    {
        "Vous avez entendu, cette nuit ?", "La montagne a grondé...", "Encore un qui n'est pas revenu.", "Belle prise aujourd'hui !",
        "Il paraît qu'un cristal y pense.", "À la santé des vivants !", "Mon dos...", "Demain, on finit le toit.", "Chut... écoutez.",
    };

    private bool IsEvening
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null && !gm.IsNight && gm.DayProgress >= 1f - gm.nightPortion - 0.07f;
        }
    }

    /// <summary>Un colon libre demande du travail.</summary>
    public void RequestJob(SettlerAI s)
    {
        if (Site == null || stage == 0) return;
        if (GameManager.Instance != null && GameManager.Instance.IsNight) return;
        bool terrified = morale < 20f;
        if (s.home == null || !s.home.IsComplete) HomeFor(s);   // emménage dès qu'une maison a de la place

        // 0. Le soir, on veille autour du feu
        var fire = CampfirePosition;
        if (IsEvening && fire.HasValue)
        {
            float ang = (Mathf.Abs(s.GetInstanceID()) % 9) / 9f * Mathf.PI * 2f;
            var seat = fire.Value + new Vector2(Mathf.Cos(ang) * 1.4f, Mathf.Sin(ang) * 0.9f - 0.2f);
            var who = s;
            s.Assign(new SettlerJob
            {
                label = "Veillée", target = seat, duration = Random.Range(6f, 10f),
                onDone = () => { if (who != null && Random.value < 0.35f) who.Say(EveningTalk[Random.Range(0, EveningTalk.Length)]); },
            });
            return;
        }

        // 1. Bâtir (le chantier payé en priorité, sinon payer un nouveau)
        if (!terrified)
            foreach (var b in buildings)
            {
                if (b.IsComplete || b.buildersPresent >= 2) continue;
                if (!b.paid && wood >= b.def.wood && stone >= b.def.stone) { wood -= b.def.wood; stone -= b.def.stone; b.paid = true; OnChanged?.Invoke(); }
                if (!b.paid) continue;
                // arbres sur l'emprise : d'abord les abattre
                var blocking = TreeInRect(b.rect) ?? TreeInRect(Apron(b.rect));
                if (blocking != null) { AssignChop(s, blocking, true); return; }
                var site = b;
                int slot = site.buildersPresent;
                site.buildersPresent++;
                s.Assign(new SettlerJob
                {
                    label = "Bâtisseur", target = site.def.id == "pier" ? (Vector2)Site.LandingWorld : (Vector2)site.WorkSpot(slot),
                    duration = site.def.BuildSeconds * (1f - site.progress) + 0.5f,
                    stillValid = () => site != null && !site.IsComplete,
                    onWork = dt => site.Work(dt),
                    onDone = () => { if (site != null) site.buildersPresent--; },
                    onCancel = () => { if (site != null) site.buildersPresent--; },
                });
                return;
            }

        int woodNeed = 0, stoneNeed = 0;
        foreach (var b in buildings) if (!b.paid && !b.IsComplete) { woodNeed += b.def.wood; stoneNeed += b.def.stone; }
        float r = Random.value;

        // 2. Son métier, le plus souvent
        if (r < 0.55f)
            switch (s.profession)
            {
                case "Bûcheron": if (AssignChop(s, NearestTree(s.transform.position), false)) return; break;
                case "Carrier": if (AssignMine(s)) return; break;
                case "Pêcheur": AssignFish(s); return;
            }
        r = Random.value;

        // 3. Ressources manquantes
        if (woodNeed > wood && (stoneNeed <= stone || r < 0.65f)) { if (AssignChop(s, NearestTree(s.transform.position), false)) return; }
        if (stoneNeed > stone) { if (AssignMine(s)) return; }
        if (food < Population * 5 || r < 0.35f) { AssignFish(s); return; }
        if (r < 0.7f) { if (AssignChop(s, NearestTree(s.transform.position), false)) return; }

        // 3. Flâner autour du camp
        Vector2 wander = (Vector2)Site.LandingWorld + Vector2.up * 3f + Random.insideUnitCircle * 5f;
        s.Assign(new SettlerJob { label = "Repos", target = wander, duration = Random.Range(2f, 5f) });
    }

    private Vector2 Stockpile()
    {
        var sp = buildings.FirstOrDefault(b => b.IsComplete && (b.def.id == "stockpile" || b.def.id == "crates"))
              ?? buildings.FirstOrDefault(b => b.IsComplete && b.def.id == "campfire");
        return sp != null ? (Vector2)sp.WorkSpot(1) : (Vector2)Site.LandingWorld + Vector2.up;
    }

    private void Deliver(SettlerAI s, string what, int amount)
    {
        s.Assign(new SettlerJob
        {
            label = "Porteur", target = Stockpile(), duration = 0.3f,
            onDone = () =>
            {
                if (what == "wood") wood += amount; else if (what == "stone") stone += amount; else food += amount;
                FloatingText.Spawn(s.transform.position + Vector3.up * 0.7f, $"+{amount} {(what == "wood" ? "bois" : what == "stone" ? "pierre" : "vivres")}",
                    new Color(0.9f, 0.85f, 0.6f), 0.8f);
                OnChanged?.Invoke();
            },
        });
    }

    private bool AssignChop(SettlerAI s, Transform tree, bool clearing)
    {
        if (tree == null) return false;
        reservedTrees.Add(tree);
        s.Assign(new SettlerJob
        {
            label = "Bûcheron", target = (Vector2)tree.position + new Vector2(0f, -0.7f), duration = chopTime,
            stillValid = () => tree != null,
            onWork = dt => { if (tree != null) tree.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 20f) * 1.5f); },
            onDone = () => { FellTree(tree); Deliver(s, "wood", woodPerTree); },
            onCancel = () => reservedTrees.Remove(tree),
        });
        return true;
    }

    private Transform NearestTree(Vector2 from)
    {
        Transform best = null;
        float bestD = 26f * 26f;
        Vector2 center = Site.LandingWorld;
        foreach (var t in trees)
        {
            if (t == null || reservedTrees.Contains(t)) continue;
            float d = ((Vector2)t.position - center).sqrMagnitude;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    /// <summary>Parvis devant la porte (côté sud) : on n'y laisse pas d'arbre.</summary>
    public static RectInt Apron(RectInt r) => r.width == 0 ? r : new RectInt(r.x - 1, r.y - 3, r.width + 2, 3);

    private Transform TreeInRect(RectInt r)
    {
        if (r.width == 0) return null;
        foreach (var t in trees)
        {
            if (t == null || reservedTrees.Contains(t)) continue;
            var p = t.position;
            if (p.x >= r.x - 0.5f && p.x <= r.xMax + 0.5f && p.y >= r.y - 0.5f && p.y <= r.yMax + 0.5f) return t;
        }
        return null;
    }

    private void FellTree(Transform tree)
    {
        if (tree == null) return;
        felled.Add(tree.position);
        trees.Remove(tree);
        reservedTrees.Remove(tree);
        tree.localRotation = Quaternion.identity;
        var art = SettlementArt.Get();
        var sr = tree.GetComponent<SpriteRenderer>();
        if (sr != null && art != null && art.stumps != null && art.stumps.Length > 0) sr.sprite = art.stumps[Random.Range(0, art.stumps.Length)];
        tree.name = "Souche";
        if (trunks != null)
        {
            var c = trunks.WorldToCell(tree.position);
            trunks.SetTile(c, null);
            AstarPath.active?.UpdateGraphs(new Bounds(tree.position, new Vector3(3, 3, 1)), 0.1f);
        }
    }

    private bool AssignMine(SettlerAI s)
    {
        // Rochers du décor proches (plage et hautes terres)
        Vector3Int best = default;
        bool found = false;
        float bestD = float.MaxValue;
        if (decor != null)
        {
            var lc = Site.landing;
            for (int dx = -24; dx <= 24; dx++)
                for (int dy = -8; dy <= 30; dy++)
                {
                    var c = new Vector3Int(lc.x + dx, lc.y + dy, 0);
                    if (reservedRocks.Contains(c)) continue;
                    var t = decor.GetTile(c);
                    if (t == null || !IsBoulder(t.name)) continue;
                    float d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = c; found = true; }
                }
        }
        if (!found)
        {
            // Carrière : la roche au pied de la montagne (inépuisable, plus lente)
            s.Assign(new SettlerJob { label = "Carrier", target = QuarrySpot(), duration = mineTime * 1.6f, onDone = () => Deliver(s, "stone", 2) });
            return true;
        }
        var cell = best;
        reservedRocks.Add(cell);
        s.Assign(new SettlerJob
        {
            label = "Carrier", target = decor.GetCellCenterWorld(cell) + Vector3.down * 0.6f, duration = mineTime,
            stillValid = () => decor.GetTile(cell) != null,
            onDone = () => { decor.SetTile(cell, null); mined.Add(cell); reservedRocks.Remove(cell); Deliver(s, "stone", stonePerRock); },
            onCancel = () => reservedRocks.Remove(cell),
        });
        return true;
    }

    private static bool IsBoulder(string n) => n.EndsWith("_16") || n.EndsWith("_17") || n.EndsWith("_38") || n.EndsWith("_39");

    private Vector2 quarry = Vector2.positiveInfinity;
    private Vector2 QuarrySpot()
    {
        if (!float.IsInfinity(quarry.x)) return quarry;
        var murs = DungeonBuilder.Instance != null ? DungeonBuilder.Instance.stoneTilemap : null;
        quarry = Site.LandingWorld;
        if (murs == null) return quarry;
        float bestD = float.MaxValue;
        var lc = Site.landing;
        for (int dx = -30; dx <= 30; dx++)
            for (int dy = 0; dy <= 40; dy++)
            {
                var c = new Vector3Int(lc.x + dx, lc.y + dy, 0);
                if (!murs.HasTile(c + Vector3Int.up) || murs.HasTile(c) || sol == null || !sol.HasTile(c)) continue;
                if (DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsAdjacentToTerritory(c)) continue;
                float d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; quarry = sol.GetCellCenterWorld(c); }
            }
        return quarry;
    }

    private void AssignFish(SettlerAI s)
    {
        Vector2 spot = DockEnd() + new Vector2(Random.Range(-0.3f, 0.3f), 0.4f);
        if (HasBuilding("pier") && Site.pierExtension.Count > 0)
            spot = Site.CellCenter(Site.pierExtension[Random.Range(0, Site.pierExtension.Count)]);
        s.Assign(new SettlerJob { label = "Pêcheur", target = spot, duration = fishTime, onDone = () => Deliver(s, "food", foodPerCatch) });
    }

    // ─────────────────────────────────────────────
    // RAIDS NOCTURNES
    // ─────────────────────────────────────────────

    /// <summary>Lieux visés par un raid : habitations achevées (sinon le feu de camp).</summary>
    public List<Vector2> RaidTargets()
    {
        var list = buildings.Where(b => b.IsComplete && (b.def.pop > 0 || b.def.id == "campfire" || b.def.id.StartsWith("tent")))
                            .Select(b => (Vector2)b.WorkSpot(0)).ToList();
        if (list.Count == 0 && Site != null) list.Add((Vector2)Site.LandingWorld + Vector2.up * 2f);
        return list;
    }

    /// <summary>Refuge des colons paniqués : le bout du ponton.</summary>
    public Vector2 Refuge => DockEnd();

    /// <summary>Le raid terrorise les colons proches (ils fuient) ; renvoie les colons effrayés.</summary>
    public List<SettlerAI> ScareAround(Vector2 at, float radius)
    {
        var n = new List<SettlerAI>();
        foreach (var s in SettlerAI.All.ToArray())
        {
            if (s == null || s.IsDying) continue;
            // la nuit ils dorment chez eux : on compte ceux dont la maison est proche
            Vector2 p = s.IsHidden ? HomeFor(s) : (Vector2)s.transform.position;
            if (Vector2.Distance(p, at) > radius) continue;
            if (s.IsHidden) s.transform.position = p;
            s.Panic(at, DockEnd() + Random.insideUnitCircle * 1.5f);
            n.Add(s);
        }
        return n;
    }

    /// <summary>Un monstre tue le colon le plus proche (raid sanglant). Renvoie sa position ou null.</summary>
    public Vector2? KillNearest(Vector2 at, float radius)
    {
        SettlerAI best = null;
        float bd = radius;
        foreach (var s in SettlerAI.All)
        {
            if (s == null || s.IsDying) continue;
            Vector2 p = s.IsHidden ? HomeFor(s) : (Vector2)s.transform.position;
            float d = Vector2.Distance(p, at);
            if (d < bd) { bd = d; best = s; }
        }
        if (best == null) return null;
        if (best.IsHidden) best.transform.position = HomeFor(best);
        Vector2 pos = best.transform.position;
        Chronicle.Add($"<color=#FF9A8A>{best.Title} périt lors d'un raid nocturne.</color>");
        best.Kill();
        return pos;
    }

    /// <summary>Conséquences d'un raid sur la colonie.</summary>
    public void OnRaid(bool lethal, int scared, int killed)
    {
        if (stage == 0) return;
        morale = Mathf.Max(0f, morale - (lethal ? 14f + killed * 6f : 6f + scared * 2f));
        prosperity = Mathf.Max(0f, prosperity - (lethal ? 10f + killed * 4f : 4f + scared));
        food = Mathf.Max(0, food - (lethal ? 6 : 10));   // réserves piétinées, pillées
        OnChanged?.Invoke();
    }

    // ─────────────────────────────────────────────
    // AVANT-POSTE DU NOYAU (colonie conquise)
    // ─────────────────────────────────────────────

    public const int AnnexCost = 200;
    public int OutpostDailyMana => 10 + 6 * buildings.Count(b => b.IsComplete);

    public bool CanAnnex(out string why)
    {
        why = null;
        if (Outpost) { why = "Déjà un avant-poste"; return false; }
        if (stage < 1 || !EverPopulated) { why = "Aucune colonie à prendre"; return false; }
        if (Population > 0) { why = $"Encore {Population} habitant(s) sur place"; return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(AnnexCost)) { why = $"{AnnexCost} mana requis"; return false; }
        return true;
    }

    public bool Annex()
    {
        if (!CanAnnex(out string why)) { GameHUD.Toast(why, GameHUD.WarningColor); return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(AnnexCost)) return false;
        MakeOutpost(false);
        return true;
    }

    private void MakeOutpost(bool restoring)
    {
        Outpost = true;
        foreach (var b in buildings)
        {
            if (b == null) continue;
            foreach (var sr in b.GetComponentsInChildren<SpriteRenderer>()) if (sr.sprite != ShardSprite) sr.color = new Color(0.6f, 0.5f, 0.82f, sr.color.a);
            if (!b.IsComplete || b.transform.Find("Lueur du Noyau") != null) continue;
            var glow = new GameObject("Lueur du Noyau");
            glow.transform.SetParent(b.transform, false);
            glow.transform.position = b.WorkSpot(0) + Vector3.up * 0.4f;
            var l = glow.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(0.55f, 0.25f, 1f);
            l.intensity = 1.3f;
            l.pointLightOuterRadius = 3f;
            // un éclat du cristal flotte devant la porte
            var shard = glow.AddComponent<SpriteRenderer>();
            shard.sprite = ShardSprite;
            shard.sortingOrder = 6;
            glow.AddComponent<Bob>();
        }
        OnChanged?.Invoke();
        if (restoring) return;
        GameHUD.Toast($"La colonie devient un avant-poste du Noyau (+{OutpostDailyMana} mana chaque matin)", GameHUD.GoldColor);
        Chronicle.Add("<color=#C9A8FF>Le Noyau annexe la colonie déserte : elle devient son avant-poste.</color>");
        Sfx.Play("shine", null, 0.9f);
        StoryManager.Instance?.Play(
            new StoryManager.Line(StoryManager.SelfName, "Leurs maisons sont vides. Leurs feux, éteints. J'y fais brûler les miens."),
            new StoryManager.Line(StoryManager.SystemName, "AVANT-POSTE : mana chaque matin selon les bâtiments, plus aucune milice ni renfort des expéditions, et les intrus qui débarquent sont ébranlés."));
    }

    private static Sprite shardSprite;

    /// <summary>Éclat de cristal violet (pixel art, 7 × 12).</summary>
    private static Sprite ShardSprite
    {
        get
        {
            if (shardSprite != null) return shardSprite;
            string[] rows =
            {
                "...o...", "..olo..", "..olo..", ".ollmo.", ".olmmo.", "olmmmdo", "olmmmdo", ".ommdo.", ".ommdo.", "..odo..", "..odo..", "...o...",
            };
            var tex = new Texture2D(7, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 7; x++)
                {
                    char c = rows[11 - y][x];
                    tex.SetPixel(x, y, c == 'o' ? new Color(0.18f, 0.08f, 0.3f) : c == 'l' ? new Color(0.92f, 0.8f, 1f) : c == 'm' ? new Color(0.62f, 0.38f, 0.98f) :
                                       c == 'd' ? new Color(0.38f, 0.2f, 0.7f) : Color.clear);
                }
            tex.Apply();
            shardSprite = Sprite.Create(tex, new Rect(0, 0, 7, 12), new Vector2(0.5f, 0f), 16f);
            return shardSprite;
        }
    }

    /// <summary>Lent flottement de haut en bas.</summary>
    private class Bob : MonoBehaviour
    {
        private Vector3 basePos;
        private float phase;
        private void Start() { basePos = transform.localPosition; phase = Random.value * 6f; }
        private void Update() => transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 1.6f + phase) * 0.08f);
    }

    private void OutpostIncome()
    {
        int m = OutpostDailyMana;
        ManaSystem.Instance?.Add(m);
        GameHUD.Toast($"Avant-poste : +{m} mana", new Color(0.78f, 0.65f, 1f));
        OnChanged?.Invoke();
    }

    /// <summary>Force de la milice (stade ≥ 3) : dégâts subis par chaque monstre qui attaque.</summary>
    public float MilitiaDamage => !Outpost && stage >= 3 ? 6f * (stage - 2) * (HasBuilding("training") ? 1.5f : 1f) : 0f;

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    public SettlementSave Capture()
    {
        var s = new SettlementSave
        {
            stage = stage, prosperity = prosperity, morale = morale, wood = wood, stone = stone, food = food,
            expeditionsSeen = expeditionsSeen, population = Population, outpost = Outpost,
        };
        foreach (var st in SettlerAI.All)
            if (st != null && !st.IsDying)
                s.settlers.Add(new SettlerSave { name = st.settlerName, profession = st.profession, prefab = st.prefabId, arrivedDay = st.arrivedDay });
        foreach (var b in buildings) s.buildings.Add(new BuildingSave { id = b.def.id, progress = b.progress, paid = b.paid });
        foreach (var f in felled) { s.felledTrees.Add(f.x); s.felledTrees.Add(f.y); }
        foreach (var c in mined) { s.minedRocks.Add(c.x); s.minedRocks.Add(c.y); }
        return s;
    }

    public void Restore(SettlementSave s)
    {
        if (s == null || Site == null) return;
        // arbres abattus et rochers extraits
        for (int i = 0; i + 1 < s.felledTrees.Count; i += 2)
        {
            var p = new Vector2(s.felledTrees[i], s.felledTrees[i + 1]);
            var t = trees.FirstOrDefault(x => x != null && Vector2.Distance(x.position, p) < 0.05f);
            if (t != null) FellTree(t);
        }
        if (decor != null)
            for (int i = 0; i + 1 < s.minedRocks.Count; i += 2)
            {
                var c = new Vector3Int(s.minedRocks[i], s.minedRocks[i + 1], 0);
                decor.SetTile(c, null);
                mined.Add(c);
            }

        stage = s.stage;
        prosperity = s.prosperity;
        morale = s.morale;
        wood = s.wood;
        stone = s.stone;
        food = s.food;
        expeditionsSeen = s.expeditionsSeen;
        if (stage <= 0) { OnChanged?.Invoke(); return; }

        for (int st = 1; st <= stage; st++) CreateSites(st);
        foreach (var b in buildings)
        {
            var bs = s.buildings.Find(x => x.id == b.def.id);
            if (bs == null) continue;
            b.paid = bs.paid;
            if (bs.progress >= 1f)
            {
                b.Complete();
                if (b.def.id == "pier") BuildPier();
            }
            else
            {
                b.progress = bs.progress;
                b.Work(0f);   // silhouette du chantier selon l'avancement
            }
        }
        RefreshCityLook();
        if (s.settlers != null && s.settlers.Count > 0) foreach (var st in s.settlers) SpawnSettler(null, st);
        else for (int i = 0; i < s.population; i++) SpawnSettler(i == 0 ? "settler_fisher" : null);
        arrivalTimer = 95f;
        if (s.outpost) MakeOutpost(true);
        OnChanged?.Invoke();
    }

    // ─────────────────────────────────────────────
    // PORT
    // ─────────────────────────────────────────────

    /// <summary>
    /// Les planches du ponton ne couvrent que 11 px sur 16 de chaque case (une bande d'eau de chaque côté) :
    /// on les élargit jusqu'aux bords de la zone praticable, pour ne plus avoir l'air de marcher sur l'eau.
    /// </summary>
    private void WidenPier()
    {
        var art = SettlementArt.Get();
        if (art == null || decor == null || art.pierLeft == null || art.pierRight == null) return;
        const float scale = 16f / 11f, shift = 0.5f * scale - 0.5f;   // le bord extérieur des planches revient au bord de la case
        var b = decor.cellBounds;
        foreach (var c in b.allPositionsWithin)
        {
            var t = decor.GetTile(c);
            bool left = t == art.pierLeft && decor.GetTile(c + Vector3Int.right) == art.pierRight;
            bool right = t == art.pierRight && decor.GetTile(c + Vector3Int.left) == art.pierLeft;
            if (!left && !right) continue;
            decor.SetTileFlags(c, TileFlags.None);
            decor.SetTransformMatrix(c, Matrix4x4.TRS(new Vector3(left ? -shift : shift, 0f, 0f), Quaternion.identity, new Vector3(scale, 1f, 1f)));
        }
    }

    private void BuildPier()
    {
        var art = SettlementArt.Get();
        if (art == null || sol == null) return;
        foreach (var c in Site.pierExtension)
        {
            if (blocked != null) blocked.SetTile(c, null);
            sol.SetTile(c, art.seaFloor);
            sol.SetTileFlags(c, TileFlags.None);
            sol.SetColor(c, Color.clear);   // l'eau est dessinée par la couche T_Eau, dessous
            if (decor != null) decor.SetTile(c, (c.x - Site.landing.x) % 2 == 0 ? art.pierLeft : art.pierRight);
        }
        WidenPier();
        if (blocked != null)
        {
            var tc = blocked.GetComponent<TilemapCollider2D>();
            if (tc != null) tc.ProcessTilemapChanges();
        }
        if (Site.pierExtension.Count > 0)
        {
            var b = new Bounds(Site.CellCenter(Site.pierExtension[0]), Vector3.one);
            foreach (var c in Site.pierExtension) b.Encapsulate(Site.CellCenter(c));
            b.Expand(3f);
            AstarPath.active?.UpdateGraphs(b, 0.2f);
        }
        // Barques amarrées
        var boats = art.Frames("boat_row");
        foreach (var m in Site.moorings)
        {
            var go = new GameObject("Barque");
            go.transform.SetParent(root, false);
            go.transform.position = m;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            var anim = go.AddComponent<SpriteAnim>();
            anim.frames = art.Frames(Random.value < 0.5f ? "boat_row" : "boat_row2") ?? boats;
            anim.fps = 5f;
            anim.randomStart = true;
        }
    }
}
