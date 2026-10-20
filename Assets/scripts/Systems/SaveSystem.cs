using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Sauvegarde et chargement de la partie.
///   - Capture : état du Noyau, des étages (cases creusées/remblayées, escaliers), objets, monstres,
///     colonie, réputation, histoire, butin, relations.
///   - Chargement : la scène (île générée) est rechargée à neuf puis l'état est réappliqué.
///   - Sauvegarde automatique à chaque aube et quand l'application passe en arrière-plan (mobile).
/// </summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    /// <summary>Partie à appliquer au prochain démarrage de la scène.</summary>
    public static SaveData PendingLoad;
    public static string PendingSlot;

    public bool IsLoading { get; private set; }
    public float PlaySeconds => playSeconds;
    public event Action OnLoaded;

    private float playSeconds;
    private float profileClock;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
        GameSettings.Apply();
        if (PendingLoad != null)
        {
            var data = PendingLoad;
            PendingLoad = null;
            if (data.mode == "crawler" && data.crawler != null) StartCoroutine(ApplyCrawlerRoutine(data));
            else StartCoroutine(ApplyRoutine(data));
        }
    }

    private void Update()
    {
        if (!InGame) return;
        playSeconds += Time.unscaledDeltaTime;
        profileClock += Time.unscaledDeltaTime;
    }

    /// <summary>La partie est lancée (pas sur l'écran d'accueil, pas de chargement ni de game over).</summary>
    public bool InGame => !CrawlerMode.Active &&
        !IsLoading && (MainMenu.Instance == null || !MainMenu.Instance.InHome) &&
        GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.Playing;

    private void OnNewDay(int day)
    {
        if (day > 1 && GameSettings.AutoSave && InGame && !(CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying))
            Save("auto", true);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && GameSettings.AutoSave && InGame) Save("auto", true);
        else if (paused && GameSettings.AutoSave && InCrawler) Save("adv_auto", true);
    }

    private void OnApplicationQuit()
    {
        if (GameSettings.AutoSave && InGame) Save("auto", true);
        else if (GameSettings.AutoSave && InCrawler) Save("adv_auto", true);
    }

    /// <summary>Une aventure est en cours (héros présent, pas de chargement).</summary>
    public bool InCrawler => CrawlerMode.Active && CrawlerMode.Instance != null && CrawlerMode.Instance.Player != null && !IsLoading &&
                             (MainMenu.Instance == null || !MainMenu.Instance.InHome);

    /// <summary>Sauvegarde automatique de l'aventure (retour en ville).</summary>
    public void AutoSaveCrawler()
    {
        if (GameSettings.AutoSave && InCrawler) Save("adv_auto", true);
    }

    // ─────────────────────────────────────────────
    // API
    // ─────────────────────────────────────────────

    public bool Save(string slot, bool silent = false)
    {
        var data = CrawlerMode.Active && CrawlerMode.Instance != null ? CaptureCrawler() : Capture();
        bool ok = ProfileStore.WriteSlot(slot, data);
        // statistiques du profil
        var p = ProfileStore.Active;
        if (p != null)
        {
            p.playSeconds += profileClock;
            profileClock = 0f;
            p.bestDay = Mathf.Max(p.bestDay, data.day);
            ProfileStore.Save();
        }
        if (!silent) GameHUD.Toast(ok ? $"Partie sauvegardée ({ProfileStore.SlotLabel(slot)})" : "Échec de la sauvegarde", ok ? GameHUD.GoldColor : GameHUD.DangerColor);
        else if (ok) GameHUD.Toast("Sauvegarde automatique", GameHUD.InfoColor);
        return ok;
    }

    /// <summary>Charge un emplacement : la scène est rechargée puis la partie restaurée.</summary>
    public static bool Load(string slot)
    {
        var data = ProfileStore.ReadSlot(slot);
        if (data == null) { GameHUD.Toast("Sauvegarde introuvable ou illisible", GameHUD.DangerColor); return false; }
        PendingLoad = data;
        PendingSlot = slot;
        MainMenu.StartMode = MainMenu.Boot.Load;
        Time.timeScale = 1f;
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.name : scene.path);
        return true;
    }

    // ─────────────────────────────────────────────
    // CAPTURE
    // ─────────────────────────────────────────────

    /// <summary>Partie du mode Aventurier : l'heure, et l'état du héros (le donjon se recrée depuis sa graine).</summary>
    public SaveData CaptureCrawler()
    {
        var d = new SaveData
        {
            savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            savedTicks = DateTime.Now.Ticks,
            playSeconds = playSeconds,
            mode = "crawler",
            difficulty = (int)GameDifficulty.Current,
            crawler = CrawlerMode.Instance.CaptureSave(),
        };
        var gm = GameManager.Instance;
        if (gm != null) { d.day = gm.CurrentDay; d.dayTimer = gm.DayTimer; }
        return d;
    }

    private IEnumerator ApplyCrawlerRoutine(SaveData d)
    {
        IsLoading = true;
        yield return null;
        yield return null;
        try
        {
            playSeconds = d.playSeconds;
            GameDifficulty.Current = d.difficulty < 0 ? Difficulty.Facile : (Difficulty)Mathf.Clamp(d.difficulty, 0, 3);
            if (MainMenu.Instance != null) MainMenu.Instance.StartCrawlerFromSave(d.crawler);
            else
            {
                var cm = FindFirstObjectByType<CrawlerMode>() ?? new GameObject("CrawlerMode").AddComponent<CrawlerMode>();
                cm.Begin((AdventurerAI.Role)d.crawler.role, d.crawler);
            }
            GameManager.Instance?.RestoreTime(Mathf.Max(1, d.day), d.dayTimer);
        }
        catch (Exception e) { Debug.LogException(e); GameHUD.Toast("Chargement incomplet : " + e.Message, GameHUD.DangerColor); }
        IsLoading = false;
        OnLoaded?.Invoke();
        GameHUD.Toast("Aventure reprise", GameHUD.GoldColor);
    }

    public SaveData Capture()
    {
        var d = new SaveData
        {
            savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            savedTicks = DateTime.Now.Ticks,
            playSeconds = playSeconds,
        };
        var gm = GameManager.Instance;
        if (gm != null)
        {
            d.day = gm.CurrentDay;
            d.dayTimer = gm.DayTimer;
            d.statKills = gm.statKills;
            d.statFled = gm.statFled;
            d.statExpeditions = gm.statExpeditions;
            d.statHeroKills = gm.statHeroKills;
            d.statEvolutions = gm.statEvolutions;
        }
        if (ManaSystem.Instance != null) d.mana = ManaSystem.Instance.Raw;
        var lvl = PlayerLevel.Instance;
        if (lvl != null) { d.level = lvl.currentLevel; d.xp = lvl.currentXP; d.xpForNext = lvl.xpForNextLevel; }

        var core = FindFirstObjectByType<DungeonCore>();
        var territory = DungeonTerritory.Instance;
        if (core != null && territory != null)
        {
            var c = territory.WorldToCell(core.transform.position);
            d.coreX = c.x; d.coreY = c.y;
            d.coreHealth = core.currentHealth;
        }

        var rep = ReputationSystem.Instance;
        if (rep != null) { d.threat = rep.threat; d.renown = rep.renown; d.discovered = rep.dungeonDiscovered; d.lostExpeditions = rep.lostExpeditions; }
        var exp = ExpeditionManager.Instance;
        if (exp != null) d.nextExpeditionDay = exp.NextExpeditionDay;
        d.difficulty = (int)GameDifficulty.Current;
        GuildSystem.Instance?.Capture(d);
        IslandEvents.Instance?.Capture(d);
        NemesisSystem.Instance?.Capture(d);
        PrisonSystem.Instance?.Capture(d);
        if (ObjectiveSystem.Instance != null) d.objectiveIndex = ObjectiveSystem.Instance.Index;

        var layers = DungeonLayers.Instance;
        if (layers != null)
            foreach (var l in layers.layers)
                d.layers.Add(new LayerSave { unlocked = l.unlocked, connected = l.connected, themeIndex = l.themeIndex, stairsX = l.stairsCell.x, stairsY = l.stairsCell.y });

        if (territory != null)
            foreach (var c in territory.Cells) { d.territory.Add(c.x); d.territory.Add(c.y); }

        var builder = DungeonBuilder.Instance;
        if (builder != null && builder.placedWallTilemap != null)
        {
            var tm = builder.placedWallTilemap;
            foreach (var p in tm.cellBounds.allPositionsWithin)
                if (tm.HasTile(p)) { d.placedWalls.Add(p.x); d.placedWalls.Add(p.y); }
        }

        foreach (var po in PlacedObject.All)
        {
            if (po == null || string.IsNullOrEmpty(po.itemId) || territory == null) continue;
            var c = territory.WorldToCell(po.transform.position);
            d.objects.Add(new ObjectSave { id = po.itemId, x = c.x, y = c.y, data = po.data });
        }

        foreach (var g in GobelinAI.All)
        {
            if (g == null || !g.IsAlive()) continue;
            var u = g.GetComponent<MonsterUnit>();
            if (u == null || u.summoned || string.IsNullOrEmpty(u.familyId)) continue;
            Vector2 pos = g.transform.position;
            // un monstre en raid est sauvegardé chez lui
            Vector2 anchor = g.anchorPoint != null ? (Vector2)g.anchorPoint.position : pos;
            if (g.IsRaiding || (territory != null && !territory.IsInside(pos))) pos = anchor;
            d.monsters.Add(new MonsterSave
            {
                family = u.familyId, stage = u.stage, name = g.gobelinName,
                x = pos.x, y = pos.y, ax = anchor.x, ay = anchor.y,
                hp = g.currentHealth, xp = u.xp, behavior = (int)g.currentBehavior == 2 ? 0 : (int)g.currentBehavior,
                guardian = g.GetComponent<MonsterMind>() != null && g.GetComponent<MonsterMind>().IsGuardian,
                guardianName = g.GetComponent<MonsterMind>() != null ? g.GetComponent<MonsterMind>().GivenName : null,
                traits = g.Life != null ? g.Life.TraitMask : -1,
                hunger = g.Life != null ? g.Life.hunger : 20f,
                energy = g.Life != null ? g.Life.energy : 90f,
                mood = g.Life != null ? g.Life.mood : 60f,
                variant = g.GetComponent<MonsterVariant>() != null ? g.GetComponent<MonsterVariant>().theme : null,
                role = u.role,
            });
        }

        if (SettlementManager.Instance != null && SettlementManager.Instance.Site != null) d.colony = SettlementManager.Instance.Capture();
        TrailWear.Instance?.Capture(d.trails);
        Talents.Capture(d.talents);
        Chronicle.Capture(d.chronicle);
        if (StoryManager.Instance != null) d.storyFlags.AddRange(StoryManager.Instance.Flags);
        LootSystem.Instance?.Capture(d);
        RelationSystem.Instance?.Capture(d);
        if (RaidSystem.Instance != null) d.raidsDone = RaidSystem.Instance.RaidsDone;

        var cam = Camera.main;
        if (cam != null) { d.camX = cam.transform.position.x; d.camY = cam.transform.position.y; d.camSize = cam.orthographicSize; }
        if (layers != null) d.view = layers.CurrentView;
        return d;
    }

    // ─────────────────────────────────────────────
    // CHARGEMENT
    // ─────────────────────────────────────────────

    private IEnumerator ApplyRoutine(SaveData d)
    {
        IsLoading = true;
        GameManager.Instance?.PushPause();
        // tous les systèmes ont fini leur Start (A* scanné, colonie, catalogue...)
        yield return null;
        yield return null;
        try { Apply(d); }
        catch (Exception e) { Debug.LogException(e); GameHUD.Toast("Chargement incomplet : " + e.Message, GameHUD.DangerColor); }
        IsLoading = false;
        GameManager.Instance?.PopPause();
        OnLoaded?.Invoke();
        GameHUD.Toast($"Partie chargée : jour {d.day}", GameHUD.GoldColor);
    }

    private static void UpdateGraphsAround(List<Vector3Int> cells, DungeonBuilder builder, DungeonLayers layers)
    {
        if (AstarPath.active == null || builder == null || cells.Count == 0) return;
        var boxes = new Dictionary<int, Bounds>();
        foreach (var c in cells)
        {
            int k = layers != null ? layers.IndexAtCell(c) : 0;
            var p = builder.floorTilemap.GetCellCenterWorld(c);
            if (boxes.TryGetValue(k, out var b)) { b.Encapsulate(p); boxes[k] = b; }
            else boxes[k] = new Bounds(p, Vector3.one);
        }
        foreach (var b in boxes.Values)
        {
            var e = b;
            e.Expand(new Vector3(6f, 6f, 2f));
            AstarPath.active.UpdateGraphs(e);
        }
        AstarPath.active.FlushGraphUpdates();
    }

    private static Vector3Int Cell(List<int> flat, int i) => new Vector3Int(flat[i], flat[i + 1], 0);

    public void Apply(SaveData d)
    {
        playSeconds = d.playSeconds;
        GameDifficulty.Current = d.difficulty < 0 ? Difficulty.Facile : (Difficulty)Mathf.Clamp(d.difficulty, 0, 3);
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.RestoreTime(d.day, d.dayTimer);
            gm.statKills = d.statKills;
            gm.statFled = d.statFled;
            gm.statExpeditions = d.statExpeditions;
            gm.statHeroKills = d.statHeroKills;
            gm.statEvolutions = d.statEvolutions;
        }
        var lvl = PlayerLevel.Instance;
        if (lvl != null) { lvl.currentLevel = Mathf.Max(1, d.level); lvl.currentXP = d.xp; lvl.xpForNextLevel = PlayerLevel.XpFor(lvl.currentLevel); }

        var builder = DungeonBuilder.Instance;
        var layers = DungeonLayers.Instance;
        var territory = DungeonTerritory.Instance;

        // Étages débloqués (roche du thème + salle d'arrivée)
        if (layers != null)
            for (int i = 1; i < layers.layers.Count && i < d.layers.Count; i++)
                if (d.layers[i].unlocked && !layers.layers[i].unlocked) layers.ApplyUnlock(i, d.layers[i].themeIndex);

        // Cœur (avant le recalcul du territoire, qui part de lui)
        var core = FindFirstObjectByType<DungeonCore>();
        if (core != null && builder != null)
        {
            core.transform.position = builder.floorTilemap.GetCellCenterWorld(new Vector3Int(d.coreX, d.coreY, 0));
            if (d.coreHealth > 0f) core.currentHealth = Mathf.Min(core.maxHealth, d.coreHealth);
        }

        // Cases creusées / remblayées (notées : seul le graphe autour d'elles est recalculé, pas toute l'île)
        var changed = new List<Vector3Int>();
        if (layers != null)
            for (int i = 1; i < layers.layers.Count && i < d.layers.Count; i++)
                if (d.layers[i].unlocked) changed.Add(layers.layers[i].landingCell);
        if (builder != null && territory != null)
        {
            var saved = new HashSet<Vector3Int>();
            for (int i = 0; i + 1 < d.territory.Count; i += 2) saved.Add(Cell(d.territory, i));
            var initial = new List<Vector3Int>(territory.Cells);
            var stone = builder.stoneTilemap;
            var floor = builder.floorTilemap;
            foreach (var c in saved)
            {
                if (!stone.HasTile(c) && territory.IsDungeonGround(c)) continue;
                stone.SetTile(c, null);
                changed.Add(c);
                AutoWallTile.RefreshAround(stone, c);
                floor.SetTile(c, layers != null ? layers.FloorTileFor(c) : builder.floorTile);
                layers?.TintCell(floor, c, false);
                builder.placedWallTilemap.SetTile(c, null);
            }
            foreach (var c in initial)
            {
                if (saved.Contains(c)) continue;
                var wall = layers != null ? layers.WallTileFor(c) : builder.stoneTile;
                stone.SetTile(c, wall != null ? wall : builder.stoneTile);
                changed.Add(c);
                AutoWallTile.RefreshAround(stone, c);
                layers?.TintCell(stone, c, true);
            }
            builder.RefreshStoneCollidersPublic();
        }

        // Escaliers
        if (layers != null)
            for (int i = 1; i < layers.layers.Count && i < d.layers.Count; i++)
                if (d.layers[i].connected && !layers.layers[i].connected)
                {
                    var st = new Vector3Int(d.layers[i].stairsX, d.layers[i].stairsY, 0);
                    layers.ConnectLayer(i, st);
                    changed.Add(st);
                }

        // Graphe de déplacement : mis à jour zone par zone autour des cases modifiées (un scan complet de l'île
        // prenait plusieurs secondes sur téléphone)
        UpdateGraphsAround(changed, builder, layers);
        territory?.Recompute();

        // Murs décoratifs
        if (builder != null)
            for (int i = 0; i + 1 < d.placedWalls.Count; i += 2) builder.RestorePlacedWall(Cell(d.placedWalls, i));

        // Butin (avant les trophées)
        LootSystem.Instance?.Restore(d);

        // Pièges, décors, trophées
        var cat = DungeonCatalog.Get();
        if (builder != null)
            foreach (var o in d.objects)
            {
                var c = new Vector3Int(o.x, o.y, 0);
                if (o.id == LootSystem.TrophyId) { LootSystem.Instance?.CreateTrophy(o.data, c); continue; }
                var item = cat.Find(TrapRules.Aliases.TryGetValue(o.id, out var newId) ? newId : o.id);   // pièges remplacés
                if (item != null) builder.PlaceItemFree(item, c);
            }

        // Monstres
        var prefabs = MonsterPrefabs(cat);
        foreach (var m in d.monsters)
        {
            if (!prefabs.TryGetValue(PrefabKey(m.family, m.role, m.stage), out var prefab)) continue;
            var ai = MonsterSpawner.Instance != null ? MonsterSpawner.Instance.SpawnFree(prefab, new Vector2(m.x, m.y), new Vector2(m.ax, m.ay), m.name) : null;
            if (ai == null) continue;
            MonsterVariant.Inherit(ai.gameObject, string.IsNullOrEmpty(m.variant) ? null : m.variant);
            if (m.guardian) MonsterMind.For(ai).MakeGuardian(string.IsNullOrEmpty(m.guardianName) ? "Gardien" : m.guardianName, false);
            if (m.hp > 0f) ai.currentHealth = Mathf.Min(ai.maxHealth, m.hp);
            var u = ai.GetComponent<MonsterUnit>();
            if (u != null) u.xp = m.xp;
            if (m.behavior == (int)GobelinAI.GoblinBehavior.Stay) ai.SetBehavior(GobelinAI.GoblinBehavior.Stay);
            if (m.traits >= 0)
            {
                var life = ai.GetComponent<MonsterLife>() ?? ai.gameObject.AddComponent<MonsterLife>();
                life.SetTraitMask(m.traits);
                life.hunger = m.hunger; life.energy = m.energy; life.mood = m.mood;
            }
        }

        // Mana (après le niveau : le max en dépend)
        ManaSystem.Instance?.SetRaw(d.mana);

        // Réputation, expéditions, histoire
        var rep = ReputationSystem.Instance;
        if (rep != null) { rep.threat = d.threat; rep.renown = d.renown; rep.dungeonDiscovered = d.discovered; rep.lostExpeditions = d.lostExpeditions; }
        int nextExp = d.nextExpeditionDay;
        if (gm != null && nextExp <= gm.CurrentDay && gm.DayProgress > (ExpeditionManager.Instance != null ? ExpeditionManager.Instance.arrivalTimeOfDay : 0.25f))
            nextExp = gm.CurrentDay + 1;   // l'expédition en cours au moment de la sauvegarde est repartie
        ExpeditionManager.Instance?.RestoreSchedule(nextExp);
        StoryManager.Instance?.RestoreFlags(d.storyFlags);
        GuildSystem.Instance?.Restore(d);
        IslandEvents.Instance?.Restore(d);
        NemesisSystem.Instance?.Restore(d);
        PrisonSystem.Instance?.Restore(d);
        ObjectiveSystem.Instance?.Restore(d.objectiveIndex);

        // Colonie, relations, raids
        SettlementManager.Instance?.Restore(d.colony);
        TrailWear.Instance?.Restore(d.trails);
        Talents.Restore(d.talents);
        Chronicle.Restore(d.chronicle);
        RelationSystem.Instance?.Restore(d);
        if (RaidSystem.Instance != null) RaidSystem.Instance.RaidsDone = d.raidsDone;

        // Vue
        if (layers != null)
        {
            layers.NotifyChanged();
            if (d.view > 0 && d.view < layers.layers.Count && layers.layers[d.view].unlocked) layers.SetView(d.view);
            else layers.SetView(0);
        }
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = Mathf.Clamp(d.camSize, 3f, 20f);
            CameraController.Instance?.CenterOn(new Vector2(d.camX, d.camY));
        }
    }

    /// <summary>Prefabs de monstres par « famille_stade » (en suivant les chaînes d'évolution du catalogue).</summary>
    private static string PrefabKey(string family, string role, int stage) =>
        string.IsNullOrEmpty(role) ? family + "_" + stage : family + "_" + role + "_" + stage;

    /// <summary>Prefabs par « famille[_rôle]_stade », voies de rôle comprises.</summary>
    private static Dictionary<string, GameObject> MonsterPrefabs(DungeonCatalog cat)
    {
        var map = new Dictionary<string, GameObject>();
        var todo = new Stack<GameObject>();
        foreach (var item in cat.items)
            if (item.kind == ItemKind.Monster && item.prefab != null) todo.Push(item.prefab);
        for (int guard = 0; todo.Count > 0 && guard < 2000; guard++)
        {
            var p = todo.Pop();
            var u = p != null ? p.GetComponent<MonsterUnit>() : null;
            if (u == null || string.IsNullOrEmpty(u.familyId)) continue;
            string key = PrefabKey(u.familyId, u.role, u.stage);
            if (map.ContainsKey(key)) continue;
            map[key] = p;
            if (u.nextStage != null) todo.Push(u.nextStage);
            if (u.branches != null) foreach (var b in u.branches) if (b != null) todo.Push(b);
        }
        return map;
    }
}
