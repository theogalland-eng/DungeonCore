using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
    public int Capacity { get { int c = campSettlers; foreach (var b in buildings) if (b.IsComplete) c += b.def.pop; return c; } }
    public string StageName => SettlementPlan.StageNames[Mathf.Clamp(stage, 0, SettlementPlan.StageNames.Length - 1)];
    public bool HasBuilding(string id) => buildings.Any(b => b.def.id == id && b.IsComplete);

    /// <summary>Bonus des expéditions apportés par la colonie.</summary>
    public float AdventurerHpMult => HasBuilding("stall_weapons") ? 1.15f : 1f;
    public float AdventurerDmgMult => HasBuilding("training") ? 1.15f : 1f;
    public int PartyBonus => (stage >= 3 ? 1 : 0) + (stage >= 5 ? 1 : 0);
    public float ExpeditionIntervalReduction => stage * 0.12f;

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
    private Tilemap sol, blocked, decor, trunks;
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
                SpawnSettler();
            }
        }
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
            for (int k = 0; k < 12; k++) { var t = TreeInRect(b.rect); if (t == null) break; FellTree(t); }
            // parvis dégagé : aucun arbre (ni souche) devant la porte
            for (int k = 0; k < 16; k++) { var t = TreeInRect(Apron(b.rect)); if (t == null) break; FellTree(t); t.gameObject.SetActive(false); }
            if (!b.IsComplete) b.Complete();
        }
        wood += 200; stone += 100; food += 120;
        OnChanged?.Invoke();
    }

    private void FoundCamp()
    {
        stage = 1;
        prosperity = Mathf.Max(prosperity, 10f);
        for (int i = 0; i < campSettlers; i++) SpawnSettler(i == 0 ? "settler_fisher" : null);
        CreateSites(1);
        GameHUD.Toast("Des colons installent un campement sur l'île !", GameHUD.WarningColor);
        StoryManager.Instance?.OnSettlementStage(1, StageName);
        OnChanged?.Invoke();
    }

    private void AdvanceStage()
    {
        stage++;
        CreateSites(stage);
        GameHUD.Toast($"La colonie humaine devient : {StageName}", GameHUD.WarningColor);
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
        int pop = Population;
        foreach (var b in buildings) if (b.IsComplete) { food += b.def.foodPerDay; prosperity += b.def.prosperityPerDay; }
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
            GameHUD.Toast("Terrifié par le donjon, un colon quitte l'île", GameHUD.GoldColor);
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

    private Vector2 HomeFor(SettlerAI s)
    {
        var homes = buildings.Where(b => b.IsComplete && b.def.pop > 0).ToList();
        if (homes.Count == 0) return Site.LandingWorld + Vector3.up;
        return homes[Mathf.Abs(s.GetInstanceID()) % homes.Count].WorkSpot(0);
    }

    // ─────────────────────────────────────────────
    // COLONS
    // ─────────────────────────────────────────────

    private Vector2 DockEnd() => Site.dockCells.Count > 0 ? (Vector2)Site.CellCenter(Site.dockCells[Site.dockCells.Count - 1]) : (Vector2)Site.LandingWorld;

    private void SpawnSettler(string id = null)
    {
        var cat = DungeonCatalog.Get();
        GameObject prefab = null;
        if (id != null) prefab = cat.settlerPrefabs.Find(p => p != null && p.name.Contains("Pêcheur"));
        if (prefab == null && cat.settlerPrefabs.Count > 0) prefab = cat.settlerPrefabs[Random.Range(0, cat.settlerPrefabs.Count)];
        if (prefab == null) return;
        var go = Instantiate(prefab, (Vector3)DockEnd() + (Vector3)(Random.insideUnitCircle * 0.3f), Quaternion.identity, root);
        go.name = "Colon";
    }

    /// <summary>Un colon libre demande du travail.</summary>
    public void RequestJob(SettlerAI s)
    {
        if (Site == null || stage == 0) return;
        if (GameManager.Instance != null && GameManager.Instance.IsNight) return;
        bool terrified = morale < 20f;

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

        // 2. Ressources manquantes
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

    /// <summary>Force de la milice (stade ≥ 3) : dégâts subis par chaque monstre qui attaque.</summary>
    public float MilitiaDamage => stage >= 3 ? 6f * (stage - 2) * (HasBuilding("training") ? 1.5f : 1f) : 0f;

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    public SettlementSave Capture()
    {
        var s = new SettlementSave
        {
            stage = stage, prosperity = prosperity, morale = morale, wood = wood, stone = stone, food = food,
            expeditionsSeen = expeditionsSeen, population = Population,
        };
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
        for (int i = 0; i < s.population; i++) SpawnSettler(i == 0 ? "settler_fisher" : null);
        arrivalTimer = 95f;
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
