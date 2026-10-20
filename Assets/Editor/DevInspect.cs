using UnityEditor;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Outils d'inspection (éditeur uniquement), appelés via ClaudeBridge.</summary>
public static class DevInspect
{
    public static string Tiles()
    {
        var sb = new StringBuilder();
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            tm.CompressBounds();
            var counts = new Dictionary<string, int>();
            foreach (var p in tm.cellBounds.allPositionsWithin)
            {
                var t = tm.GetTile(p);
                if (t == null) continue;
                counts.TryGetValue(t.name, out int c);
                counts[t.name] = c + 1;
            }
            sb.Append($"\n{tm.name} layer={tm.gameObject.layer} bounds={tm.cellBounds} tiles:");
            foreach (var kv in counts) sb.Append($" {kv.Key}={kv.Value}");
        }
        // Sol autour du coeur
        var sol = GameObject.Find("Sol")?.GetComponent<Tilemap>();
        var murs = GameObject.Find("murs")?.GetComponent<Tilemap>();
        var core = GameObject.Find("dungeon_orb");
        if (sol != null && core != null)
        {
            var c = sol.WorldToCell(core.transform.position);
            sb.Append($"\nCore cell {c}\n");
            for (int y = c.y + 6; y >= c.y - 6; y--)
            {
                for (int x = c.x - 4; x <= c.x + 26; x++)
                {
                    var p = new Vector3Int(x, y, 0);
                    char ch = '.';
                    if (murs != null && murs.HasTile(p)) ch = '#';
                    else if (sol.HasTile(p)) ch = sol.GetTile(p).name.Length > 0 ? sol.GetTile(p).name[sol.GetTile(p).name.Length - 1] : '?';
                    if (p == c) ch = 'O';
                    sb.Append(ch);
                }
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    public static string SkipDialogues()
    {
        CinematicDirector.Instance?.Skip();
        int n = 0;
        while (StoryManager.Instance != null && StoryManager.Instance.IsShowing && n < 50) { StoryManager.Instance.Advance(); StoryManager.Instance.Advance(); n++; }
        return $"skipped {n}";
    }

    public static string State()
    {
        var sb = new StringBuilder();
        var gm = GameManager.Instance;
        if (gm == null) return "no GameManager (not playing?)";
        sb.Append($"Day {gm.CurrentDay} {gm.DayProgress:0.00} {gm.GetTimeOfDayLabel()} timeScale={Time.timeScale} state={gm.State} dialog={StoryManager.Instance?.IsShowing}\n");
        var m = ManaSystem.Instance;
        sb.Append($"Mana {m.Current}/{m.Max} regen {m.CurrentRegen:0.0} intrudersInside {m.IntrudersInside}\n");
        var r = ReputationSystem.Instance;
        sb.Append($"Threat {r.threat:0.0} renown {r.renown:0.0} discovered {r.dungeonDiscovered} tier {r.CurrentTier}\n");
        var e = ExpeditionManager.Instance;
        sb.Append($"Expedition active {e.ExpeditionActive} count {e.ActiveCount} next day {e.NextExpeditionDay}\n");
        sb.Append($"Territory cells {DungeonTerritory.Instance.CellCount} entrance {DungeonTerritory.Instance.EntranceCell} core {DungeonTerritory.Instance.CoreCell}\n");
        var core = Object.FindFirstObjectByType<DungeonCore>();
        sb.Append($"Core HP {core.currentHealth:0}/{core.maxHealth}\n");
        foreach (var a in AdventurerAI.All)
            sb.Append($"  ADV {a.DisplayName} {a.State} aware={a.IsAware} inside={a.IsInsideDungeon} hp={a.combat.currentHealth:0}/{a.combat.maxHealth:0} pos={(Vector2)a.transform.position}\n");
        foreach (var g in GobelinAI.All)
            sb.Append($"  GOB {g.gobelinName} {g.currentBehavior} mission={g.IsOnMission} hp={g.currentHealth:0}/{g.maxHealth:0} pos={(Vector2)g.transform.position} possessed={g.IsPossessed}\n");
        sb.Append($"Wildlife {WildAnimal.All.Count}  ");
        sb.Append($"Traps {SpinningBladeTrap.All.Count}  Mode {DungeonBuilder.Instance?.GetCurrentMode()} digQueue {DungeonBuilder.Instance?.QueuedDigCount}");
        return sb.ToString();
    }

    public static string LaunchExpedition()
    {
        ExpeditionManager.Instance.LaunchExpedition();
        return "launched";
    }

    public static string AdventurerExpedition()
    {
        var r = ReputationSystem.Instance;
        r.Discover();
        r.threat = Mathf.Max(r.threat, 25f);
        ExpeditionManager.Instance.LaunchExpedition();
        SkipDialogues();
        return "adventurers launched";
    }

    public static string Verbose()
    {
        DLog.verbose = !DLog.verbose;
        return $"verbose={DLog.verbose}";
    }

    public static string Lights()
    {
        var sb = new StringBuilder();
        foreach (var l in Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None))
            sb.Append($"{l.name} type={l.lightType} intensity={l.intensity} color={l.color} layers={l.targetSortingLayers?.Length}\n");
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.sharedMaterial != null && (r is TilemapRenderer || r is SpriteRenderer))
                sb.Append($"  {r.name}: {r.sharedMaterial.name} / {r.sharedMaterial.shader.name}\n");
        return sb.ToString();
    }

    public static string Night() { GameManager.Instance.DebugSetDayProgress(0.8f); return "night"; }
    public static string Day() { GameManager.Instance.DebugSetDayProgress(0.3f); return "day"; }

    public static string Fast() { GameManager.Instance.SetSpeed(3f); return "x3"; }
    public static string KillCore() { Object.FindFirstObjectByType<DungeonCore>().TakeDamage(99999); return "core destroyed"; }
    public static string AutoSkip() { autoSkip = !autoSkip; if (autoSkip) UnityEditor.EditorApplication.update += AutoSkipTick; else UnityEditor.EditorApplication.update -= AutoSkipTick; return $"autoskip={autoSkip}"; }
    private static bool autoSkip;
    private static void AutoSkipTick() { if (!Application.isPlaying) return; if (StoryManager.Instance != null && StoryManager.Instance.IsShowing) SkipDialogues(); if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying) CinematicDirector.Instance.Skip(); }

    public static float shotX = 0, shotY = 0, shotSize = 80;
    public static string Overview() => Shot(0, 0, 80, 2400, 1600, "overview");
    public static string Grotto()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        return Shot(core.x, core.y - 1.5f, 5, 1600, 1000, "grotto");
    }
    public static string Dock()
    {
        var p = Object.FindFirstObjectByType<AdventurerSpawner>().transform.position;
        return Shot(p.x, p.y + 6, 16, 1600, 1000, "dock");
    }
    public static string Custom() => Shot(shotX, shotY, shotSize, 1600, 1000, "custom");
    public static string Forest() => Shot(-20, 10, 14, 1600, 1000, "forest");
    public static string Forest2() => Shot(20, -30, 14, 1600, 1000, "forest2");
    public static string Mountain() => Shot(10, 8, 12, 1600, 1000, "mountain");

    public static string Shot(float x, float y, float size, int w, int h, string name)
    {
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = size;
        cam.transform.position = new Vector3(x, y, -10);
        cam.backgroundColor = new Color(0.02f, 0.025f, 0.05f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        string path = System.IO.Path.GetFullPath($"Temp/{name}.png");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
        return path;
    }

    public static string LevelUp() { PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel); ManaSystem.Instance.Add(2000); return $"level {PlayerLevel.Instance.currentLevel}"; }

    public static string LayerTest()
    {
        var l = DungeonLayers.Instance;
        var t = DungeonTerritory.Instance;
        bool u = l.Unlock(1, 1);
        var cell = t.CoreCell + new Vector3Int(1, 1, 0);
        bool s = l.PlaceStairsDown(cell);
        bool m = l.MoveCoreTo(1);
        return $"unlock={u} stairs={s} move={m} territory={t.CellCount} core={t.CoreCell}";
    }

    static void DigAt(Vector3Int c)
    {
        var b = DungeonBuilder.Instance;
        var f = typeof(DungeonBuilder).GetField("currentMiningCell", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f.SetValue(b, c);
        typeof(DungeonBuilder).GetMethod("CompleteMining", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(b, null);
    }

    /// <summary>Creuse une salle au nord de la grotte (test des murs auto-connectés).</summary>
    public static string DigTest()
    {
        var core = DungeonTerritory.Instance.CoreCell;
        for (int y = 2; y <= 5; y++)
            for (int x = -3; x <= 3; x++)
                if (!(y == 2 && x != 0)) DigAt(core + new Vector3Int(x, y, 0));
        DigAt(core + new Vector3Int(4, 4, 0));
        var tm = DungeonBuilder.Instance.stoneTilemap;
        var p = core + new Vector3Int(0, 6, 0);
        return $"territory={DungeonTerritory.Instance.CellCount} wallAbove={tm.GetTile(p)?.name} sprite={tm.GetSprite(p)?.name}";
    }

    /// <summary>Débloque l'étage 1 en Crypte, le relie, creuse, puis y déplace le cœur.</summary>
    public static string CryptTest()
    {
        var l = DungeonLayers.Instance;
        var t = DungeonTerritory.Instance;
        while (PlayerLevel.Instance.currentLevel < 2) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(5000);
        int crypt = DungeonCatalog.Get().themes.FindIndex(th => th.id == "crypt");
        bool u = l.Unlock(1, crypt);
        bool st = l.PlaceStairsDown(t.CoreCell + new Vector3Int(0, -2, 0));
        var land = l.layers[1].landingCell;
        for (int x = 2; x <= 6; x++) DigAt(land + new Vector3Int(x, 0, 0));
        for (int y = 1; y <= 3; y++) for (int x = 4; x <= 7; x++) DigAt(land + new Vector3Int(x, y, 0));
        ManaSystem.Instance.Add(5000);
        int before = ManaSystem.Instance.Current, cost = DungeonBuilder.Instance.MoveCoreCost;
        bool m = DungeonBuilder.Instance.TryMoveCore(land + new Vector3Int(6, 2, 0));
        return $"crypt={crypt} unlock={u} stairs={st} move={m} cost={cost} mana {before}->{ManaSystem.Instance.Current} max={ManaSystem.Instance.Max} territory={t.CellCount}";
    }

    public static string ThemePicker()
    {
        while (PlayerLevel.Instance.currentLevel < 2) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(5000);
        var sp = Object.FindFirstObjectByType<SidePanel>();
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(SidePanel).GetMethod("SetTab", bf).Invoke(sp, new object[] { CatalogCategory.Layer });
        typeof(SidePanel).GetField("themePickerLayer", bf).SetValue(sp, 1);
        typeof(SidePanel).GetMethod("Rebuild", bf).Invoke(sp, null);
        return "picker";
    }

    public static string Icons()
    {
        var sb = new StringBuilder();
        foreach (var it in DungeonCatalog.Get().items)
        {
            if (it.kind != ItemKind.Monster) continue;
            var sr = it.prefab != null ? it.prefab.GetComponent<SpriteRenderer>() : null;
            sb.Append($"{it.id}: icon={it.icon?.name}/{(it.icon != null ? AssetDatabase.GetAssetPath(it.icon) : "")} prefabSprite={sr?.sprite?.name} | ");
        }
        return sb.ToString();
    }

    public static string TrapTest()
    {
        var b = DungeonBuilder.Instance;
        var t = DungeonTerritory.Instance;
        var cat = DungeonCatalog.Get();
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        while (PlayerLevel.Instance.currentLevel < 3) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(5000);
        var core = t.CoreCell;
        var sb = new StringBuilder();
        void P(string id, Vector3Int c) { var it = cat.Find(id); b.CanPlaceItemAt(it, c, out string r); m.Invoke(b, new object[] { it, c }); sb.Append($"{id}@{c}:{r ?? "ok"} "); }
        P("door_wood", core + new Vector3Int(0, -3, 0));
        P("spikes", core + new Vector3Int(0, -4, 0));
        P("bear", core + new Vector3Int(0, -5, 0));
        P("poison", core + new Vector3Int(-1, 1, 0));
        P("door_iron", core + new Vector3Int(0, -2, 0));
        return sb.ToString();
    }

    public static string DockShot()
    {
        var p = Object.FindFirstObjectByType<AdventurerSpawner>().transform.position;
        return Shot(p.x + 3, p.y, 7, 1600, 1000, "dockshot");
    }

    public static string TrapShot()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        return Shot(core.x, core.y - 2.5f, 4, 1600, 1000, "trapshot");
    }

    public static string Boats()
    {
        var c = DungeonCatalog.Get();
        var boats = Object.FindObjectsByType<ExpeditionBoat>(FindObjectsSortMode.None);
        return $"sprites=[{string.Join(",", c.boatSprites.ConvertAll(b => b != null ? b.name : "null"))}] boats=[{string.Join(",", System.Array.ConvertAll(boats, b => b.GetComponent<SpriteRenderer>().sprite.name + "@" + b.transform.position))}]";
    }

    public static string MonsterTab()
    {
        while (PlayerLevel.Instance.currentLevel < 5) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        var sp = Object.FindFirstObjectByType<SidePanel>();
        typeof(SidePanel).GetMethod("SetTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sp, new object[] { CatalogCategory.Monster });
        return "monsters";
    }

    static readonly System.Reflection.BindingFlags BF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    /// <summary>Fonde la colonie tout de suite et lui donne des ressources.</summary>
    public static string ColonyFound()
    {
        var sm = SettlementManager.Instance;
        if (sm == null || !sm.enabled) return "pas de colonie (site absent ?)";
        if (sm.stage == 0) typeof(SettlementManager).GetMethod("FoundCamp", BF).Invoke(sm, null);
        sm.wood += 60; sm.stone += 20; sm.food += 40;
        return $"stage={sm.stage} pop={sm.Population} buildings={sm.Buildings.Count} plots={sm.Site.plots.Count}";
    }

    public static string ColonyAdvance()
    {
        var sm = SettlementManager.Instance;
        foreach (var b in sm.Buildings) if (!b.IsComplete) b.Complete();
        sm.prosperity = SettlementPlan.StageProsperity[Mathf.Min(sm.stage + 1, 5)] + 1;
        sm.wood += 120; sm.stone += 60; sm.food += 60;
        return $"stage={sm.stage} prosperity={sm.prosperity}";
    }

    public static string ColonyState()
    {
        var sm = SettlementManager.Instance;
        if (sm == null) return "none";
        var jobs = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var st in SettlerAI.All) { jobs.TryGetValue(st.Job, out int n); jobs[st.Job] = n + 1; }
        string j = string.Join(",", System.Linq.Enumerable.Select(jobs, kv => kv.Key + ":" + kv.Value));
        string b = string.Join(",", System.Linq.Enumerable.Select(sm.Buildings, x => $"{x.def.id}:{(x.IsComplete ? "ok" : (x.paid ? "chantier" + Mathf.RoundToInt(x.progress * 100) : "attente"))}"));
        return $"stage={sm.stage} prosp={sm.prosperity:0} morale={sm.morale:0} pop={sm.Population}/{sm.Capacity} wood={sm.wood} stone={sm.stone} food={sm.food} jobs[{j}] buildings[{b}]";
    }

    public static string ColonyShot()
    {
        var site = Object.FindFirstObjectByType<SettlementSite>();
        var p = site.LandingWorld;
        return Shot(p.x, p.y + 7, 11, 1600, 1000, "colony");
    }

    public static string EvolveTest()
    {
        ManaSystem.Instance.Add(5000);
        while (PlayerLevel.Instance.currentLevel < 6) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(5000);
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        var cat = DungeonCatalog.Get();
        var orc = MonsterSpawner.Instance.TrySummonAt((Vector2)core + Vector2.down, cat.Find("orc_1").prefab, 50, "Orc");
        var u = orc.GetComponent<MonsterUnit>();
        u.AddXp(100);
        string before = $"{orc.gobelinName} hp={orc.maxHealth} dmg={orc.attackDamage}";
        var nu = u.Evolve();
        var g2 = nu.GetComponent<GobelinAI>();
        return $"{before} -> {g2.gobelinName} hp={g2.maxHealth} dmg={g2.attackDamage} stage={nu.stage} upkeep={nu.upkeepPerDay} daily={UpkeepSystem.Instance.DailyCost}";
    }

    public static string LichTest()
    {
        ManaSystem.Instance.Add(5000);
        while (PlayerLevel.Instance.currentLevel < 6) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        var lich = MonsterSpawner.Instance.TrySummonAt((Vector2)core + new Vector2(1, -1), CharacterImporter.Prefab("lich_3"), 0, "Archiliche");
        var sp = Object.FindFirstObjectByType<AdventurerSpawner>();
        var tier = ExpeditionManager.Instance.tiers[2];
        var adv = sp.SpawnForExpedition(tier, 2, 0, 1, core + new Vector3(0, -3.5f, 0));
        return $"lich={lich?.gobelinName} traits={lich?.GetComponent<MonsterTraits>()?.summonCount} adv={adv?.DisplayName}";
    }

    public static string Minions()
    {
        int s = 0, n = 0;
        foreach (var m in MonsterUnit.All) { if (m.summoned) s++; else n++; }
        return $"monsters={n} summoned={s} advs={AdventurerAI.All.Count}";
    }

    public static string NewDay()
    {
        var gm = GameManager.Instance;
        var f = typeof(GameManager).GetField("OnNewDay", BF);
        var d = f.GetValue(gm) as System.Action<int>;
        d?.Invoke(gm.CurrentDay);
        return $"upkeep due={UpkeepSystem.Instance.LastDue} paid={UpkeepSystem.Instance.LastPaid} mana={ManaSystem.Instance.Current}";
    }

    public static string CleanupOldCharacters()
    {
        int n = 0;
        foreach (var id in new[] { "goblin", "hobgoblin", "orc", "slime", "slime_toxic", "slime_magma", "ghost", "adventurer", "knight", "hero" })
        {
            if (AssetDatabase.DeleteAsset($"Assets/Prefabs/Characters/{id}.prefab")) n++;
            if (AssetDatabase.DeleteAsset($"Assets/Animations/Generated/{id}")) n++;
            AssetDatabase.DeleteAsset($"Assets/Art/Generated/Icons/{id}.png");
        }
        return $"supprimés : {n}";
    }

    public static string SeaProbe()
    {
        var sb = new StringBuilder();
        UnityEngine.Tilemaps.Tilemap sea = null;
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None)) if (tm.name == "NoPathfinding") sea = tm;
        var tc = sea.GetComponent<TilemapCollider2D>();
        sb.Append($"seaShapes={tc.shapeCount} ");
        // noeuds praticables posés sur la mer
        int bad = 0, total = 0;
        var gg = AstarPath.active.data.gridGraph;
        gg.GetNodes(n => { if (!n.Walkable) return; total++; var c = sea.WorldToCell((Vector3)n.position); if (sea.HasTile(c)) bad++; });
        sb.Append($"walkable={total} walkableOnSea={bad} ");
        int wa = 0, wOn = 0;
        foreach (var a in WildAnimal.All) { wa++; if (sea.HasTile(sea.WorldToCell(a.transform.position))) { wOn++; if (wOn < 4) sb.Append($"[animal {a.name} {a.transform.position}] "); } }
        int ad = 0;
        foreach (var a in AdventurerAI.All) if (a != null && sea.HasTile(sea.WorldToCell(a.transform.position))) ad++;
        sb.Append($"animals={wa} onSea={wOn} advOnSea={ad}");
        return sb.ToString();
    }

    public static int animalIdx = 0;
    /// <summary>Capture de l'animal le plus proche de la mer (vérifie le rendu des pieds au bord de l'eau).</summary>
    public static string AnimalShot()
    {
        UnityEngine.Tilemaps.Tilemap sea = null;
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None)) if (tm.name == "NoPathfinding") sea = tm;
        var list = new List<(float d, WildAnimal a)>();
        foreach (var a in WildAnimal.All)
        {
            var c = sea.WorldToCell(a.transform.position);
            float best = 99;
            for (int dx = -4; dx <= 4; dx++) for (int dy = -4; dy <= 4; dy++)
                if (sea.HasTile(c + new Vector3Int(dx, dy, 0))) best = Mathf.Min(best, Vector2.Distance(a.transform.position, sea.GetCellCenterWorld(c + new Vector3Int(dx, dy, 0))));
            list.Add((best, a));
        }
        list.Sort((x, y) => x.d.CompareTo(y.d));
        var pick = list[Mathf.Clamp(animalIdx, 0, list.Count - 1)];
        var p = pick.a.transform.position;
        var sr = pick.a.GetComponent<SpriteRenderer>();
        return $"{pick.a.name} d={pick.d:0.0} pos={p} sprite={sr.sprite?.name} pivot={sr.sprite?.pivot} bounds={sr.bounds} order={sr.sortingOrder} layer={sr.sortingLayerName} " + Shot(p.x, p.y, 4, 1200, 800, "animal");
    }

    /// <summary>Zones A* : taille de chaque île / îlot et animaux hors de l'île principale.</summary>
    public static string Areas()
    {
        var gg = AstarPath.active.data.gridGraph;
        var counts = new Dictionary<uint, int>();
        gg.GetNodes(n => { if (!n.Walkable) return; counts.TryGetValue(n.Area, out int c); counts[n.Area] = c + 1; });
        var sp = Object.FindFirstObjectByType<AdventurerSpawner>().transform.position;
        uint main = AstarPath.active.GetNearest(sp, Pathfinding.NNConstraint.Default).node.Area;
        int off = 0;
        var sb = new StringBuilder();
        foreach (var a in WildAnimal.All)
        {
            var n = AstarPath.active.GetNearest(a.transform.position, Pathfinding.NNConstraint.None).node;
            if (n.Area != main) { off++; if (off < 5) sb.Append($"[{a.name} {a.transform.position} area={n.Area} size={(counts.ContainsKey(n.Area) ? counts[n.Area] : 0)}] "); }
        }
        int small = 0; foreach (var kv in counts) if (kv.Key != main && kv.Value < 60) small++;
        return $"areas={counts.Count} main={main} mainSize={counts[main]} smallIslets={small} animalsOffMain={off} " + sb;
    }

    public static string panelTab = "Trap";
    public static float panelScroll = 0f;   // 1 = haut, 0 = bas
    /// <summary>Ouvre le panneau sur un onglet et le fait défiler (test de la liste déroulante).</summary>
    public static string PanelScroll()
    {
        var sp = Object.FindFirstObjectByType<SidePanel>(FindObjectsInactive.Include);
        sp.SetExpanded(true);
        var tab = (CatalogCategory)System.Enum.Parse(typeof(CatalogCategory), panelTab);
        typeof(SidePanel).GetMethod("SetTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sp, new object[] { tab });
        Canvas.ForceUpdateCanvases();
        var sr = sp.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        sr.verticalNormalizedPosition = panelScroll;
        return $"content={sr.content.rect.height:0} viewport={sr.viewport.rect.height:0} items={sr.content.childCount} pos={sr.verticalNormalizedPosition:0.00}";
    }

    // ───────────── Accueil, sauvegarde, butin, raids, relations ─────────────
    static readonly System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

    public static string MenuState()
    {
        var m = MainMenu.Instance;
        var p = ProfileStore.Active;
        return $"home={m?.InHome} pause={m?.InPause} frozen={MainMenu.Frozen} hold={MainMenu.HoldIntro} profile={p?.name} profiles={ProfileStore.List.profiles.Count} latest={ProfileStore.Latest()?.slot} path={Application.persistentDataPath}";
    }

    public static string MenuCreateProfile()
    {
        if (!ProfileStore.HasProfile) ProfileStore.Create("Testeur");
        MainMenu.Instance.CloseModal();
        return MenuState();
    }

    public static string MenuNewGame()
    {
        typeof(MainMenu).GetMethod("StartNewGame", NP).Invoke(MainMenu.Instance, null);
        return MenuState();
    }

    public static string MenuPause() { MainMenu.Instance.OpenPause(); return MenuState(); }
    public static string MenuClose() { MainMenu.Instance.ClosePause(); return MenuState(); }
    public static string MenuSlots() { typeof(MainMenu).GetMethod("ShowSlots", NP).Invoke(MainMenu.Instance, new object[] { true }); return "slots"; }
    public static string MenuOptions() { typeof(MainMenu).GetMethod("ShowOptions", NP).Invoke(MainMenu.Instance, null); return "options"; }
    public static string MenuModes() { typeof(MainMenu).GetMethod("ShowModes", NP).Invoke(MainMenu.Instance, null); return "modes"; }
    public static string MenuProfiles() { typeof(MainMenu).GetMethod("ShowProfiles", NP).Invoke(MainMenu.Instance, null); return "profiles"; }

    /// <summary>Partie de test : creusage, étage, monstres, pièges, décor, butin.</summary>
    public static string SaveSetup()
    {
        SkipDialogues();
        while (PlayerLevel.Instance.currentLevel < 4) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(5000);
        var core = DungeonTerritory.Instance.CoreCell;
        var stone = DungeonBuilder.Instance.stoneTilemap;
        int dug = 0;
        for (int dx = 1; dx <= 5; dx++) { var c = core + new Vector3Int(dx, 0, 0); if (stone.HasTile(c)) { DigAt(c); dug++; } }
        var cat = DungeonCatalog.Get();
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", BF);
        m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find("orc_1"), core + new Vector3Int(2, 0, 0) });
        m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find("lich_1"), core + new Vector3Int(3, 0, 0) });
        m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find("spikes"), core + new Vector3Int(4, 0, 0) });
        m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find("brazier"), core + new Vector3Int(5, 0, 0) });
        LootSystem.Instance.Add("gold_coins", 2);
        LootSystem.Instance.Add("hero_crown", 1);
        LootSystem.Instance.PlaceTrophy("hero_crown", core + new Vector3Int(1, 0, 0));
        foreach (var u in MonsterUnit.All) u.xp = 33;
        RelationSystem.Instance.relation = 42f;
        GameManager.Instance.DebugSetDayProgress(0.4f);
        return $"dug={dug} " + SaveState();
    }

    public static string SaveState()
    {
        var gm = GameManager.Instance;
        int monsters = 0; string names = "";
        foreach (var g in GobelinAI.All) if (g != null) { monsters++; names += g.gobelinName + "(" + g.GetComponent<MonsterUnit>()?.xp + ") "; }
        return $"day={gm.CurrentDay} t={gm.DayTimer:0.0} lvl={PlayerLevel.Instance.currentLevel} mana={ManaSystem.Instance.Current} territory={DungeonTerritory.Instance.CellCount} objects={PlacedObject.All.Count} prestige={PlacedObject.TotalPrestige()} monsters={monsters} [{names}] loot={LootSystem.Instance.OwnedCount} rel={RelationSystem.Instance.relation:0} core={DungeonTerritory.Instance.CoreCell} colony={SettlementManager.Instance.stage}/{SettlementManager.Instance.Population}";
    }

    public static string SaveSlot1() => SaveSystem.Instance.Save("slot1") ? "saved " + ProfileStore.SlotPath("slot1") : "fail";
    public static string LoadSlot1() => SaveSystem.Load("slot1") ? "loading" : "fail";

    public static string LootTest()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        var def = LootSystem.Instance.Def("merc_sword");
        LootPickup.Spawn(def, core + new Vector3(1.5f, -1f, 0));
        return "spawned";
    }

    public static string TreasureTab()
    {
        var sp = Object.FindFirstObjectByType<SidePanel>(FindObjectsInactive.Include);
        sp.SetExpanded(true);
        typeof(SidePanel).GetMethod("SetTab", NP).Invoke(sp, new object[] { CatalogCategory.Treasure });
        return "treasure owned=" + LootSystem.Instance.OwnedCount;
    }

    public static string ColonyWin() { ColonyWindow.Instance.Open(); return "open"; }

    public static string RaidTest()
    {
        var sm = SettlementManager.Instance;
        if (sm.stage == 0) typeof(SettlementManager).GetMethod("FoundCamp", NP).Invoke(sm, null);
        if (!GameManager.Instance.IsNight) GameManager.Instance.DebugSetDayProgress(0.7f);
        string why;
        bool ok = RaidSystem.Instance.CanRaid(out why);
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        string dist = "";
        foreach (var t in sm.RaidTargets()) dist += $"{Vector2.Distance(core, t):0} ";
        return $"targets=[{dist}] night={GameManager.Instance.IsNight} can={ok} {why} raiders={RaidSystem.Instance.PickRaiders().Count} morale={sm.morale:0} pop={sm.Population}";
    }
    public static string RaidTerror() { RaidSystem.Instance.CanRaid(out string why); return $"why={why} night={GameManager.Instance.IsNight} p={GameManager.Instance.DayProgress:0.00} " + RaidSystem.Instance.Launch(false) + " " + RaidStatus(); }
    public static string RaidBlood() { GameManager.Instance.DebugSetDayProgress(0.66f); RaidSystem.Instance.CanRaid(out string why); return $"why={why} " + RaidSystem.Instance.Launch(true) + " " + RaidStatus(); }
    public static string RaidStatus()
    {
        var sm = SettlementManager.Instance;
        string st = "";
        foreach (var g in GobelinAI.All) if (g != null) st += $"{g.gobelinName}:{(g.IsRaiding ? (g.RaidArrived ? "there" : "going") : "home")}@{(Vector2)g.transform.position} ";
        return $"active={RaidSystem.Instance.Active} done={RaidSystem.Instance.RaidsDone} morale={sm.morale:0} pop={sm.Population} food={sm.food} rel={RelationSystem.Instance.relation:0} punitive={RelationSystem.Instance.PunitiveDay} next={ExpeditionManager.Instance.NextExpeditionDay} mana={ManaSystem.Instance.Current} | {st}";
    }

    public static string RelationTest()
    {
        var r = RelationSystem.Instance;
        ManaSystem.Instance.Add(3000);
        bool b1 = r.Bribe(); float a = r.relation;
        r.relation = 65f;
        bool t = r.ProposeTruce();
        SkipDialogues();
        return $"bribe={b1} rel={a:0} truce={t} state={r.StateName} tribute={r.DailyTribute} cost={r.BribeCost}";
    }

    public static string ColonyShotNight()
    {
        var sm = SettlementManager.Instance;
        var p = sm.Site.LandingWorld;
        return Shot(p.x, p.y + 4f, 9, 1600, 1000, "colony_raid");
    }

    /// <summary>Colonie au stade 1 avec camp terminé, 3 orcs, nuit, pas d'expédition.</summary>
    public static string RaidScenario()
    {
        SkipDialogues();
        ExpeditionManager.Instance.RestoreSchedule(99);
        var sm = SettlementManager.Instance;
        if (sm.stage == 0) typeof(SettlementManager).GetMethod("FoundCamp", NP).Invoke(sm, null);
        foreach (var b in sm.Buildings) if (!b.IsComplete) b.Complete();
        SimSetup();
        GameManager.Instance.SetSpeed(2f);
        GameManager.Instance.DebugSetDayProgress(0.66f);
        SkipDialogues();
        return RaidTest();
    }

    public static string SettlerDebug()
    {
        var sm = SettlementManager.Instance;
        string t = "targets: ";
        foreach (var x in sm.RaidTargets()) t += $"{x} ";
        t += "| settlers: ";
        foreach (var s in SettlerAI.All) t += $"{(Vector2)s.transform.position}{(s.IsHidden ? "H" : "")}:{s.Job} ";
        t += "| buildings: ";
        foreach (var b in sm.Buildings) t += $"{b.def.id}(pop{b.def.pop},{(b.IsComplete ? "ok" : "site")}) ";
        return t;
    }

    public static string SpareTest()
    {
        ExpeditionManager.Instance.CallNextExpedition();
        SkipDialogues();
        int n = 0; foreach (var a in AdventurerAI.All) if (a != null) n++;
        return $"intruders={n}";
    }
    public static string SpareNow() => $"spared={RelationSystem.Instance.SpareAll()} rel={RelationSystem.Instance.relation:0}";
    public static string SpareStatus()
    {
        string st = "";
        foreach (var a in AdventurerAI.All) if (a != null) st += $"{a.DisplayName}:{a.State}{(a.Spared ? "*" : "")} ";
        return $"rel={RelationSystem.Instance.relation:0} fled={GameManager.Instance.statFled} kills={GameManager.Instance.statKills} | {st}";
    }

    public static string MenuContinue() { typeof(MainMenu).GetMethod("ContinueGame", NP).Invoke(MainMenu.Instance, null); return "continue"; }

    public static string LayerSaveTest()
    {
        MainMenu.Instance.ClosePause();
        while (PlayerLevel.Instance.currentLevel < 6) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(9000);
        var l = DungeonLayers.Instance;
        var t = DungeonTerritory.Instance;
        bool u = l.Unlock(1, 2);
        Vector3Int cell = t.CoreCell;
        foreach (var c in t.Cells) if (c != t.CoreCell && DungeonBuilder.Instance.GetPlacedAt(c) == null && l.IndexAtCell(c) == 0) { cell = c; break; }
        bool s = l.PlaceStairsDown(cell);
        SkipDialogues();
        return $"unlock={u} stairs={s}@{cell} " + LayerState();
    }
    public static string LayerState()
    {
        var l = DungeonLayers.Instance;
        string r = "";
        for (int i = 0; i < l.layers.Count; i++) r += $"[{i}:{(l.layers[i].unlocked ? "U" : "-")}{(l.layers[i].connected ? "C" : "-")} th{l.layers[i].themeIndex} st{l.layers[i].stairsCell}] ";
        return r + $" stairsObjs={DungeonStairs.All.Count} territory={DungeonTerritory.Instance.CellCount}";
    }
    public static string SaveSlot2() => SaveSystem.Instance.Save("slot2") ? "saved" : "fail";
    public static string LoadSlot2() => SaveSystem.Load("slot2") ? "loading" : "fail";

    public static string Sorting()
    {
        var sb = new StringBuilder();
        foreach (var r in Object.FindObjectsByType<UnityEngine.Tilemaps.TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.Append($"{r.name}: layer={r.sortingLayerName} order={r.sortingOrder} mode={r.mode} sort={r.sortOrder} | ");
        return sb.ToString();
    }

    /// <summary>Couloir d'une case bordé de murs, avec tous les pièges animés (vérifie qu'ils ne débordent plus sur les murs).</summary>
    public static string TrapWallTest()
    {
        SkipDialogues();
        while (PlayerLevel.Instance.currentLevel < 5) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(9000);
        var core = DungeonTerritory.Instance.CoreCell;
        var stone = DungeonBuilder.Instance.stoneTilemap;
        for (int dx = 1; dx <= 8; dx++) { var c = core + new Vector3Int(dx, 0, 0); if (stone.HasTile(c)) DigAt(c); }
        var cat = DungeonCatalog.Get();
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", BF);
        string[] ids = { "spikes", "blades", "guillotine", "column", "bomb", "ghost_trap", "web" };
        for (int i = 0; i < ids.Length; i++) m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find(ids[i]), core + new Vector3Int(2 + i, 0, 0) });
        var p = DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(core + new Vector3Int(5, 0, 0));
        return $"traps={PlacedObject.Count(ItemKind.Trap)} " + Shot(p.x, p.y, 3.2f, 1600, 1000, "trapwall");
    }

    public static string PartyTest()
    {
        SkipDialogues();
        var cat = DungeonCatalog.Get();
        var rep = ReputationSystem.Instance;
        rep.dungeonDiscovered = true;
        rep.threat = 25f;
        rep.renown = 60f;   // groupe plus nombreux
        SimSetup();
        GameManager.Instance.SetSpeed(1f);
        ExpeditionManager.Instance.LaunchExpedition();
        SkipDialogues();
        return $"prefabs archer={cat.archerPrefab != null} mage={cat.magePrefab != null} healer={cat.healerPrefab != null} | " + PartyStatus();
    }
    public static string PartyStatus()
    {
        string st = "";
        foreach (var a in AdventurerAI.All)
            if (a != null) st += $"{a.DisplayName}[{a.role}] {a.State} hp={a.combat.currentHealth:0}/{a.combat.maxHealth:0} rng={a.combat.attackRange} | ";
        int mon = 0; foreach (var g in GobelinAI.All) if (g != null) mon++;
        return $"monsters={mon} " + st;
    }
    public static string PartyShot()
    {
        Vector3 p = Vector3.zero; int n = 0;
        foreach (var a in AdventurerAI.All) if (a != null) { p += a.transform.position; n++; }
        if (n == 0) return "no party";
        p /= n;
        return Shot(p.x, p.y, 4f, 1600, 1000, "party");
    }

    public static string ReliefShot()
    {
        var r = MountainRelief.Instance;
        var c = r != null ? r.craterWorld : Vector2.zero;
        return $"crater={c} view={(r != null ? r.DungeonView.ToString() : "-")} " + Shot(c.x, c.y - 6f, 22f, 2000, 1250, "relief");
    }
    public static string ReliefClose()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        return Shot(core.x, core.y + 2f, 9f, 1600, 1000, "relief_close");
    }
    public static string IslandView() { MountainRelief.Instance.SetUserView(false); return "island"; }
    public static string DungeonViewOn() { MountainRelief.Instance.SetUserView(true); return "dungeon"; }

    public static string FxTest()
    {
        var c = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        string r = "";
        foreach (var n in new[] { "fireball", "explosion", "heal", "revival", "smoke_circle", "smoke_skull", "arrow", "magma_geyser", "smoke_column" })
            r += $"{n}={(Fx.Frames(n) != null ? Fx.Frames(n).Length : 0)} ";
        Fx.Play("revival", c + new Vector3(-1.5f, 0.3f, 0), 2.2f, 4f, 16, null, true);
        Fx.Play("heal", c + new Vector3(1.5f, -0.1f, 0), 1.1f, 4f, 16, null, true);
        Fx.Play("explosion", c + new Vector3(0f, -2f, 0), 1.3f, 3f, 16, null, true);
        Projectile.FireFx("fireball", c + new Vector3(-2, -3, 0), c + new Vector3(2, -3, 0), 0.55f, false, null, 1f, 0.2f, 10);
        return r;
    }
    public static string FxShot()
    {
        var c = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        return Shot(c.x, c.y - 1f, 3.5f, 1600, 1000, "fx");
    }

    public static string SimColony()
    {
        SkipDialogues();
        var core = Object.FindFirstObjectByType<DungeonCore>();
        core.maxHealth = 1e6f; core.currentHealth = 1e6f;
        SimSetup();
        GameManager.Instance.SetSpeed(10f);
        return Sim();
    }

    public static string SeaColliderTest()
    {
        var sb = new StringBuilder();
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
        {
            var tc = tm.GetComponent<TilemapCollider2D>();
            if (tc == null) continue;
            var comp = tm.GetComponent<CompositeCollider2D>();
            int before = tc.shapeCount;
            tc.ProcessTilemapChanges();
            if (comp != null) comp.GenerateGeometry();
            var rb = tm.GetComponent<Rigidbody2D>();
            sb.Append($"(before={before} en={tc.enabled} active={tc.isActiveAndEnabled} rb={(rb != null ? rb.bodyType.ToString() + "/sim=" + rb.simulated : "none")} tiles={tm.GetUsedTilesCount()} extr={tc.extrusionFactor} dela={tc.useDelaunayMesh} maxChange={tc.maximumTileChangeCount}) ");
            sb.Append($"{tm.name}: tcShapes={tc.shapeCount} op={tc.compositeOperation} comp={(comp != null ? comp.geometryType + "/" + comp.pathCount : "none")} ");
            if (tm.name == "trees_trunk")
            {
                tc.compositeOperation = Collider2D.CompositeOperation.None;
                tc.ProcessTilemapChanges();
                Physics2D.SyncTransforms();
                sb.Append($"[sans composite shapes={tc.shapeCount}] ");
                tc.compositeOperation = Collider2D.CompositeOperation.Merge;
                tc.ProcessTilemapChanges();
            }
            if (tm.name == "NoPathfinding" && comp != null)
            {
                comp.geometryType = CompositeCollider2D.GeometryType.Outlines;
                comp.GenerateGeometry();
                sb.Append($"[outlines paths={comp.pathCount}] ");
                comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
                comp.GenerateGeometry();
                sb.Append($"[polygons paths={comp.pathCount}] ");
            }
            sb.Append("| ");
        }
        return sb.ToString();
    }

    /// <summary>Défense de base pour simuler la partie en accéléré (orcs + pics près de l'entrée).</summary>
    public static string SimSetup()
    {
        var cat = DungeonCatalog.Get();
        var core = DungeonTerritory.Instance.CoreCell;
        ManaSystem.Instance.Add(400);
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", BF);
        foreach (var off in new[] { new Vector3Int(0, -2, 0), new Vector3Int(0, -3, 0), new Vector3Int(-1, 1, 0) })
            m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find("orc_1"), core + off });
        m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find("spikes"), core + new Vector3Int(0, -4, 0) });
        GameManager.Instance.SetSpeed(10f);
        return $"monsters={GobelinAI.All.Count} traps={PlacedObject.Count(ItemKind.Trap)} mana={ManaSystem.Instance.Current}";
    }

    public static string Sim()
    {
        var gm = GameManager.Instance;
        var sm = SettlementManager.Instance;
        var rep = ReputationSystem.Instance;
        return $"day={gm.CurrentDay} {gm.State} core={Object.FindFirstObjectByType<DungeonCore>().currentHealth:0} mana={ManaSystem.Instance.Current}/{ManaSystem.Instance.Max} lvl={PlayerLevel.Instance.currentLevel} upkeep={UpkeepSystem.Instance.DailyCost} kills={gm.statKills} fled={gm.statFled} exp={gm.statExpeditions} tier={rep.CurrentTier} disc={rep.dungeonDiscovered} | colony stage={sm.stage} prosp={sm.prosperity:0} morale={sm.morale:0} pop={sm.Population}";
    }

    public static string CoreState()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>();
        var l = DungeonLayers.Instance;
        return $"core={core.transform.position} layer={l.IndexAt(core.transform.position)} coreCell={DungeonTerritory.Instance.CoreCell} relocating={core.IsRelocating} scale={core.transform.localScale} view={l.CurrentView} territory={DungeonTerritory.Instance.CellCount}";
    }

    public static string CoreShot()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        return Shot(core.x - 2, core.y, 6, 1600, 1000, "coreshot");
    }

    public static string ClickTorch()
    {
        var sp0 = Object.FindFirstObjectByType<SidePanel>();
        typeof(SidePanel).GetMethod("SetTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sp0, new object[] { CatalogCategory.Decor });
        var go = GameObject.Find("Item_torch");
        if (go == null) return "no Item_torch";
        var b = go.GetComponent<UnityEngine.UI.Button>();
        var es = UnityEngine.EventSystems.EventSystem.current;
        var rt = (RectTransform)go.transform;
        Vector3 sp = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        var data = new UnityEngine.EventSystems.PointerEventData(es) { position = sp };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        es.RaycastAll(data, hits);
        string top = hits.Count > 0 ? string.Join(",", hits.ConvertAll(h => h.gameObject.name)) : "none";
        b.onClick.Invoke();
        return $"rebuilds={SidePanel.RebuildCount} screen={sp} hits={top} mode={DungeonBuilder.Instance?.GetCurrentMode()} item={DungeonBuilder.Instance?.SelectedItem?.id} ES={es?.name} modules={es?.currentInputModule}";
    }

    public static string Fps() => $"dt={Time.smoothDeltaTime * 1000f:0}ms fps={1f / Time.smoothDeltaTime:0} unscaled={Time.unscaledDeltaTime * 1000f:0}ms res={Screen.width}x{Screen.height}";

    public static string RaycastItems()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        var sb = new StringBuilder();
        foreach (var name in new[] { "Item_dig", "Item_select", "Tab1", "Tab3", "Item_torch" })
        {
            var go = GameObject.Find(name);
            if (go == null) { sb.Append($"{name}: absent | "); continue; }
            var rt = (RectTransform)go.transform;
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = sp }, hits);
            var g = go.GetComponent<UnityEngine.UI.Graphic>();
            sb.Append($"{name} at {sp} depth={g.depth} rect={rt.rect} active={go.activeInHierarchy} hits=[{string.Join(",", hits.ConvertAll(h => h.gameObject.name + "(" + h.depth + ")"))}] | ");
        }
        return sb.ToString();
    }

    public static string DebugInput() { GameHUD.debugInput = !GameHUD.debugInput; return $"debugInput={GameHUD.debugInput}"; }

    public static string PlaceTest()
    {
        var b = DungeonBuilder.Instance;
        var t = DungeonTerritory.Instance;
        var cat = DungeonCatalog.Get();
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ManaSystem.Instance.Add(1000);
        var core = t.CoreCell;
        var sb = new StringBuilder();
        void P(string id, Vector3Int c) { var it = cat.Find(id); b.CanPlaceItemAt(it, c, out string r); m.Invoke(b, new object[] { it, c }); sb.Append($"{id}@{c}:{r ?? "ok"} "); }
        P("torch", core + new Vector3Int(-1, 1, 0));
        P("chest", core + new Vector3Int(1, 1, 0));
        P("blade", core + new Vector3Int(0, -2, 0));
        P("goblin", core + new Vector3Int(-1, -1, 0));
        P("slime", core + new Vector3Int(1, -1, 0));
        PlayerLevel.Instance.AddXP(200);
        P("flame", core + new Vector3Int(0, -3, 0));
        sb.Append($" traps={PlayerLevel.Instance.GetCurrentTrapCount()} monsters={GobelinAI.All.Count} placed={PlacedObject.All.Count} chests={TreasureChest.All.Count}");
        return sb.ToString();
    }

    public static string Speed10() { GameManager.Instance.SetSpeed(10f); return "x10"; }

    public static string Astar()
    {
        var sb = new StringBuilder();
        var ap = Object.FindFirstObjectByType<AstarPath>();
        if (ap == null) return "no AstarPath";
        if (ap.data.graphs == null || ap.data.graphs.Length == 0) ap.data.DeserializeGraphs();
        foreach (var g in ap.data.graphs)
        {
            if (g is Pathfinding.GridGraph gg)
                sb.Append($"GridGraph center={gg.center} w={gg.width} d={gg.depth} node={gg.nodeSize} rot={gg.rotation} 2D={gg.is2D} collision.use2D={gg.collision.use2D} mask={gg.collision.mask.value} diam={gg.collision.diameter} type={gg.collision.type} heightCheck={gg.collision.heightCheck} neighbours={gg.neighbours}");
            else sb.Append(g?.GetType().Name);
        }
        return sb.ToString();
    }

    // ── Guilde, difficulté, cinématiques (2026-09-26) ──
    private static int gameShotN;
    public static string GameShot()
    {
        string path = System.IO.Path.GetFullPath($"Temp/gshot_{++gameShotN}.png");
        ScreenCapture.CaptureScreenshot(path);
        return "shot " + path;
    }
    public static string LeaveHome()
    {
        typeof(MainMenu).GetMethod("LeaveHome", NP).Invoke(MainMenu.Instance, null);
        return MenuState();
    }
    public static string CineState()
    {
        var c = CinematicDirector.Instance;
        if (c == null) return "no cine";
        var fade = (UnityEngine.UI.Image)typeof(CinematicDirector).GetField("fade", NP).GetValue(c);
        var cap = (TMPro.TextMeshProUGUI)typeof(CinematicDirector).GetField("caption", NP).GetValue(c);
        var title = (TMPro.TextMeshProUGUI)typeof(CinematicDirector).GetField("titleText", NP).GetValue(c);
        var stamp = (TMPro.TextMeshProUGUI)typeof(CinematicDirector).GetField("stampText", NP).GetValue(c);
        return $"playing={c.IsPlaying} fade={fade.color.a:0.00} caption='{cap.text}' title='{title.text}'({title.alpha:0.0}) stamp='{stamp.text}'({stamp.alpha:0.0}) dialog={StoryManager.Instance?.IsShowing} ts={Time.timeScale} rt={Time.realtimeSinceStartup:0.0} udt={Time.unscaledDeltaTime:0.000}";
    }
    public static string FirstBoatTest()
    {
        LeaveHome();
        SkipDialogues();
        ExpeditionManager.Instance.LaunchExpedition();
        return CineState();
    }
    public static string GuildState()
    {
        var g = GuildSystem.Instance;
        var e = ExpeditionManager.Instance;
        string m = "";
        foreach (var a in AdventurerAI.All) if (a != null) m += $"{a.DisplayName}[{a.role}] hp={a.combat.maxHealth:0} dmg={a.combat.attackDamage:0.0} seen={a.MonstersSeen} | ";
        return $"day={GameManager.Instance.CurrentDay} diff={GameDifficulty.Name} stage={g.stage} official={g.officialRank} reported={g.reportedRank} assessDay={g.assessDay} score={g.DangerScore():0.0} measured={g.MeasuredRank} greed={g.greed:0.00} host={g.hostility:0.00} report='{g.reportSummary}' next={e.NextExpeditionDay} active={e.ExpeditionActive} assess={e.IsAssessment} rank={e.CurrentRank} tier={ReputationSystem.Instance.CurrentTier} | {m}";
    }
    /// <summary>Simule le retour des pêcheurs terrifiés : signalement à la Guilde.</summary>
    public static string GuildReport()
    {
        var g = GuildSystem.Instance;
        typeof(GuildSystem).GetField("repFled", NP).SetValue(g, 2);
        typeof(GuildSystem).GetField("repMonsters", NP).SetValue(g, 3);
        typeof(GuildSystem).GetField("repMaxStage", NP).SetValue(g, 2);
        typeof(GuildSystem).GetField("repHurt", NP).SetValue(g, 1);
        ReputationSystem.Instance.Discover();
        return GuildState();
    }
    public static string GuildAssess()
    {
        SkipDialogues();
        var g = GuildSystem.Instance;
        g.assessDay = GameManager.Instance.CurrentDay;
        ExpeditionManager.Instance.ScheduleAt(GameManager.Instance.CurrentDay);
        ExpeditionManager.Instance.LaunchExpedition();
        return GuildState() + " || " + CineState();
    }
    /// <summary>Fin de l'évaluation : kill = nombre de membres tués, les autres rentrent.</summary>
    public static string GuildEnd(int kill)
    {
        SkipDialogues();
        int k = 0;
        foreach (var a in new List<AdventurerAI>(AdventurerAI.All))
        {
            if (a == null) continue;
            if (k < kill) { a.combat.TakeDamage(99999f); k++; }
            else typeof(AdventurerAI).GetMethod("Depart", NP).Invoke(a, null);
        }
        return GuildState() + " || " + CineState();
    }
    public static string GuildEnd0() => GuildEnd(0);
    public static string GuildEnd1() => GuildEnd(1);
    public static string GuildEndAll() => GuildEnd(99);
    public static string GuildNext()
    {
        SkipDialogues();
        ExpeditionManager.Instance.CallNextExpedition();
        SkipDialogues();
        return GuildState();
    }
    public static string VerdictReplay() { CinematicDirector.Instance.PlayGuildVerdict(4, "Test : l'équipe est revenue au complet.", null, null); return "verdict"; }
    /// <summary>Cinématique du verdict au hall : 0 = tous rentrés, 1 = pertes, 2 = personne (test).</summary>
    public static int verdictCase;
    public static string VerdictHall()
    {
        var v = new GuildSystem.VerdictInfo { monsters = 14, maxStage = 2, traps = 6, floors = 2, level = 5, deaths = verdictCase == 0 ? 0 : 3, reported = 1,
                                              monsterScore = 22f, trapScore = 4.8f, levelScore = 2.4f, floorScore = 1.5f, deathScore = verdictCase == 0 ? 0f : 1.8f };
        v.team.Add(("Brennor", AdventurerAI.Role.Warrior)); v.team.Add(("Ysa", AdventurerAI.Role.Healer)); v.team.Add(("Kael", AdventurerAI.Role.Archer)); v.team.Add(("Mirth", AdventurerAI.Role.Mage));
        if (verdictCase == 0) foreach (var m in v.team) v.back.Add(m.name);
        if (verdictCase == 1) { v.back.Add("Brennor"); v.back.Add("Kael"); }
        v.killed = verdictCase == 0 ? 0 : verdictCase == 1 ? 2 : 4;
        v.fled = verdictCase == 0 ? 4 : verdictCase == 1 ? 2 : 0;
        v.rank = GuildSystem.EstimateRank(32.5f) + (verdictCase == 0 ? 0 : 1);
        CinematicDirector.Instance.PlayGuildVerdict(v.rank, "test", v, null);
        return $"verdict case {verdictCase} rank {v.rank}";
    }
    public static string VerdictHall1() { verdictCase = 1; return VerdictHall(); }
    public static string VerdictHall2() { verdictCase = 2; return VerdictHall(); }
    public static string GuildSaveTest()
    {
        var d = SaveSystem.Instance.Capture();
        return $"diff={d.difficulty} gstage={d.guildStage} grank={d.guildRank} rep={d.guildReportedRank} assessDay={d.guildAssessDay} host={d.guildHostility:0.00} report='{d.guildReport}' next={d.nextExpeditionDay}";
    }
    public static string ReimportAudio()
    {
        int n = 0;
        foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" }))
        { AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate); n++; }
        return $"reimported {n}";
    }
    public static string AudioState()
    {
        var m = MusicManager.Instance;
        if (m == null) return "no music manager";
        string r = m.Describe() + " || ";
        foreach (var a in m.GetComponentsInChildren<AudioSource>()) r += $"{a.name}: {(a.clip != null ? a.clip.name : "-")} play={a.isPlaying} vol={a.volume:0.00} pitch={a.pitch:0.00} | ";
        return r + $" listener={Object.FindFirstObjectByType<AudioListener>() != null} vol={AudioListener.volume}";
    }
    /// <summary>Couloir sinueux d'une case (virages serrés) avec le cœur au bout.</summary>
    public static string WindingSetup()
    {
        SkipDialogues();
        while (PlayerLevel.Instance.currentLevel < 4) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(9000);
        var core = DungeonTerritory.Instance.CoreCell;
        var cells = new List<Vector3Int>();
        for (int x = 1; x <= 4; x++) cells.Add(core + new Vector3Int(x, 0, 0));
        for (int y = 1; y <= 3; y++) cells.Add(core + new Vector3Int(4, y, 0));
        for (int x = 3; x >= -2; x--) cells.Add(core + new Vector3Int(x, 3, 0));
        for (int y = 4; y <= 6; y++) cells.Add(core + new Vector3Int(-2, y, 0));
        for (int x = -1; x <= 1; x++) cells.Add(core + new Vector3Int(x, 6, 0));
        var stone = DungeonBuilder.Instance.stoneTilemap;
        foreach (var c in cells) if (stone.HasTile(c)) DigAt(c);
        var end = cells[cells.Count - 1];
        bool moved = DungeonBuilder.Instance.TryMoveCore(end);
        SkipDialogues();
        return $"dug={cells.Count} moved={moved} end={end}";
    }
    public static string CornersOld() { foreach (var g in AstarPath.active.data.graphs) if (g is Pathfinding.GridGraph gg) gg.cutCorners = true; AstarPath.active.Scan(); return "cutCorners=true"; }
    public static string CornersNew() { foreach (var g in AstarPath.active.data.graphs) if (g is Pathfinding.GridGraph gg) gg.cutCorners = false; AstarPath.active.Scan(); return "cutCorners=false"; }
    public static string WindingRun()
    {
        SkipDialogues();
        AdventurerAI.StuckEvents = 0;
        foreach (var a in new List<AdventurerAI>(AdventurerAI.All)) if (a != null) Object.Destroy(a.gameObject);
        var g = GuildSystem.Instance;
        g.stage = GuildSystem.Stage.Assessed; g.officialRank = 2;
        ReputationSystem.Instance.dungeonDiscovered = true;
        windStart = Time.time;
        ExpeditionManager.Instance.CallNextExpedition();
        SkipDialogues();
        GameManager.Instance.SetSpeed(3f);
        return WindingStatus();
    }
    private static float windStart;
    public static string WindingStatus()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        string r = $"t={Time.time - windStart:0}s stuck={AdventurerAI.StuckEvents} coreHP={Object.FindFirstObjectByType<DungeonCore>().currentHealth:0} | ";
        foreach (var a in AdventurerAI.All) if (a != null) r += $"{a.name.Substring(0, Mathf.Min(6, a.name.Length))} {a.State} d={Vector2.Distance(a.transform.position, core):0.0} los={AdventurerAI.ClearLine(a.transform.position, core, 1 << 6)} | ";
        return r;
    }
    public static string PathDebug()
    {
        string r = "";
        var tm = DungeonBuilder.Instance.stoneTilemap;
        foreach (var a in AdventurerAI.All)
        {
            if (a == null) continue;
            var path = (Pathfinding.Path)typeof(AdventurerAI).GetField("path", NP).GetValue(a);
            int wp = (int)typeof(AdventurerAI).GetField("currWp", NP).GetValue(a);
            var rb = a.GetComponent<Rigidbody2D>();
            r += $"{a.name.Substring(0, 6)} pos={a.transform.position} vel={rb.linearVelocity} wp={wp}/{(path != null ? path.vectorPath.Count : -1)} ";
            if (path != null) for (int i = 0; i < Mathf.Min(path.vectorPath.Count, 8); i++) r += $"[{i}]{(Vector2)path.vectorPath[i]} ";
            var c = tm.WorldToCell(a.transform.position);
            r += " cells:";
            for (int dy = 1; dy >= -1; dy--) { r += " "; for (int dx = -1; dx <= 1; dx++) r += tm.HasTile(c + new Vector3Int(dx, dy, 0)) ? "#" : "."; }
            var hits = Physics2D.OverlapCircleAll(a.transform.position, 0.35f);
            r += " hits:"; foreach (var h in hits) if (h.gameObject != a.gameObject) r += h.name + "(L" + h.gameObject.layer + ") ";
            r += " | ";
        }
        return r;
    }
    public static string GraphDebug()
    {
        var gg = AstarPath.active.data.gridGraph;
        string r = $"cutCorners={gg.cutCorners} ";
        foreach (var a in AdventurerAI.All)
        {
            if (a == null) continue;
            var p = a.transform.position;
            var baseNode = (Pathfinding.GridNode)AstarPath.active.GetNearest(p, Pathfinding.NNConstraint.None).node;
            r += $"node@{(Vector3)baseNode.position} walk={baseNode.Walkable} conns=";
            for (int d = 0; d < 8; d++) r += baseNode.HasConnectionInDirection(d) ? "1" : "0";
            r += " grid:";
            for (int dy = 1; dy >= -1; dy--)
            {
                r += " ";
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = AstarPath.active.GetNearest((Vector3)baseNode.position + new Vector3(dx, dy, 0), Pathfinding.NNConstraint.None).node;
                    r += n != null && n.Walkable ? "." : "#";
                }
            }
            break;
        }
        return r;
    }
    public static string StormNow() { IslandEvents.Instance.Begin(IslandEvent.Storm, GameManager.Instance.CurrentDay + 1); return "storm"; }
    public static string FogNow() { IslandEvents.Instance.Begin(IslandEvent.Fog, GameManager.Instance.CurrentDay + 1); return "fog"; }
    public static string EruptionNow() { IslandEvents.Instance.Begin(IslandEvent.Eruption, GameManager.Instance.CurrentDay + 1); return "eruption"; }
    public static string EventEnd() { typeof(IslandEvents).GetMethod("End", NP).Invoke(IslandEvents.Instance, null); return "end " + ExpeditionManager.Instance.ActiveCount; }
    /// <summary>Crée une Némésis (survivant marqué) et lance une expédition classée où elle revient à coup sûr.</summary>
    public static string NemesisTest()
    {
        SkipDialogues();
        var n = NemesisSystem.Instance;
        n.joinChance = 1f;
        var cat = DungeonCatalog.Get();
        var core = DungeonTerritory.Instance.CoreCell;
        var rec = new NemesisRecord { name = "Garrick", epithet = "Cicatrice", prefab = cat.archerPrefab.name, role = (int)AdventurerAI.Role.Archer, rank = 2, lastSeenDay = -5 };
        rec.traps.Add(core.x); rec.traps.Add(core.y - 3);
        n.records.Add(rec);
        var g = GuildSystem.Instance; g.stage = GuildSystem.Stage.Assessed; g.officialRank = 3;
        ReputationSystem.Instance.dungeonDiscovered = true;
        ExpeditionManager.Instance.CallNextExpedition();
        SkipDialogues();
        string r = "";
        foreach (var a in AdventurerAI.All) if (a != null) r += $"{a.DisplayName} nem={(a.Nemesis != null)} hp={a.combat.maxHealth:0} | ";
        return r;
    }
    public static string ObjState() => $"obj={ObjectiveSystem.Instance.Index} ach={string.Join(",", ProfileStore.Active.achievements)} ev={IslandEvents.Instance.Current} weather={GameManager.Instance.weatherIntensity:0.00}";
    public static string PrisonSetup()
    {
        SkipDialogues();
        while (PlayerLevel.Instance.currentLevel < 6) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(9000);
        var cat = DungeonCatalog.Get();
        var core = DungeonTerritory.Instance.CoreCell;
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", BF);
        m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find("cell"), core + new Vector3Int(0, -2, 0) });
        PrisonSystem.Instance.captureEnabled = true;
        // un monstre de forme finale intelligent
        var lich = cat.items.Find(i => i.prefab != null && i.prefab.name.StartsWith("lich"));
        GameObject l3 = null;
        foreach (var it in cat.items) if (it.prefab != null && it.prefab.GetComponent<MonsterUnit>() != null && it.prefab.GetComponent<MonsterUnit>().familyId == "lich") { var u = it.prefab.GetComponent<MonsterUnit>(); var p = it.prefab; while (u.nextStage != null) { p = u.nextStage; u = p.GetComponent<MonsterUnit>(); } l3 = p; }
        var pos = DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(core + new Vector3Int(0, 1, 0));
        var g = l3 != null ? MonsterSpawner.Instance.SpawnFree(l3, pos, pos, "Liche") : null;
        bool promoted = g != null && MonsterMind.Promote(g);
        SkipDialogues();
        return $"cells={PrisonCell.All.Count} lich={(l3 != null ? l3.name : "none")} promoted={promoted} name={(g != null ? g.gobelinName : "-")} hp={(g != null ? g.maxHealth : 0):0}";
    }
    public static string PrisonCapture()
    {
        SkipDialogues();
        ExpeditionManager.Instance.CallNextExpedition();
        SkipDialogues();
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        foreach (var a in AdventurerAI.All) { if (a == null) continue; var rb = a.GetComponent<Rigidbody2D>(); rb.position = (Vector2)core + new Vector2(0f, -1f); a.transform.position = rb.position; break; }
        return "teleported";
    }
    public static string PrisonKill()
    {
        foreach (var a in new List<AdventurerAI>(AdventurerAI.All)) if (a != null && a.IsInsideDungeon) { a.combat.TakeDamage(99999f); break; }
        SkipDialogues();
        var mind = Object.FindFirstObjectByType<MonsterMind>();
        if (mind != null) mind.Talk();
        string r = $"prisoners={PrisonSystem.Instance.prisoners.Count} ";
        foreach (var p in PrisonSystem.Instance.prisoners) r += p.DisplayName + " ";
        return r;
    }
    public static string RoomTest()
    {
        SkipDialogues();
        while (PlayerLevel.Instance.currentLevel < 6) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(9000);
        var cat = DungeonCatalog.Get();
        var core = DungeonTerritory.Instance.CoreCell;
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", BF);
        foreach (var (id, dx, dy) in new[] { ("bones", -1, 1), ("coffin", 1, 1), ("skull_pile", -1, -1), ("candelabra", 1, -1) })
            m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find(id), core + new Vector3Int(dx, dy, 0) });
        return $"placed={PlacedObject.All.Count}";
    }
    public static string RoomState()
    {
        string r = $"rooms={ThemedRooms.Instance.rooms.Count} ";
        foreach (var x in ThemedRooms.Instance.rooms) r += $"{ThemedRooms.Names[(int)x.kind]}@{x.center} r={x.radius:0.0} items={x.items.Count} | ";
        return r + $" prestige={PlacedObject.TotalPrestige():0.0}";
    }
    /// <summary>Colonie complète instantanée (tous les stades, tous les bâtiments finis), sans cinématiques.</summary>
    public static string ColonyFull()
    {
        GameSettings.Cinematics = false;
        var sm = SettlementManager.Instance;
        if (sm.stage == 0) typeof(SettlementManager).GetMethod("FoundCamp", BF).Invoke(sm, null);
        for (int i = 0; i < 5 && sm.stage < 5; i++)
        {
            foreach (var b in sm.Buildings) if (!b.IsComplete) b.Complete();
            sm.morale = 80f;
            typeof(SettlementManager).GetMethod("AdvanceStage", BF).Invoke(sm, null);
        }
        foreach (var b in sm.Buildings) if (!b.IsComplete) b.Complete();
        SkipDialogues();
        GameSettings.Cinematics = true;
        return $"stage={sm.stage} buildings={sm.Buildings.Count}";
    }
    public static string TownShot()
    {
        var sm = SettlementManager.Instance;
        var c = sm.StageFocus(0);
        MountainRelief.Instance?.SetUserView(false);
        return Shot(c.x, c.y + 2f, 11f, 2000, 1250, "town");
    }
    public static string GroundTiles()
    {
        var sb = new StringBuilder();
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            var counts = new Dictionary<string, int>();
            foreach (var p in tm.cellBounds.allPositionsWithin)
            {
                var t = tm.GetTile(p);
                if (t == null) continue;
                counts[t.name] = counts.TryGetValue(t.name, out int n) ? n + 1 : 1;
            }
            var r = tm.GetComponent<TilemapRenderer>();
            sb.Append($"[{tm.name} order={(r != null ? r.sortingOrder : -99)}] ");
            foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(counts, k => k.Value), 12)) sb.Append($"{kv.Key}:{kv.Value} ");
            sb.Append(" || ");
        }
        return sb.ToString();
    }
    public static string EnterTavern() { InteriorSystem.Instance.Enter("house_1"); return "tavern"; }
    public static string EnterGuild() { InteriorSystem.Instance.Enter("house_4"); return "guild"; }
    public static string EnterForge() { InteriorSystem.Instance.Enter("house_3"); return "forge"; }
    public static string EnterMage() { InteriorSystem.Instance.Enter("tent_magic"); return "mage"; }
    public static string ExitInterior() { InteriorSystem.Instance.Exit(); return "exit"; }
    public static string CaveTest()
    {
        SkipDialogues();
        while (PlayerLevel.Instance.currentLevel < 7) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(99999);
        DigTest();
        var cat = DungeonCatalog.Get();
        var core = DungeonTerritory.Instance.CoreCell;
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", BF);
        foreach (var (id, dx, dy) in new[] { ("crystal_blue", -3, 5), ("crystal_red", -2, 5), ("crystal_yellow", -3, 4), ("magma_vent", 3, 5), ("demon_altar", 0, 5),
                                              ("mushrooms_long", 2, 3), ("mushroom_slime", 3, 3), ("mushrooms_small", 3, 4), ("stalagmites", -3, 3), ("demon_hand", 1, 4), ("golem_1", -1, 3), ("golem_3", 1, 3) })
        {
            var it = cat.Find(id) ?? cat.items.Find(i => i.prefab != null && i.prefab.name == id);
            if (it != null) m.Invoke(DungeonBuilder.Instance, new object[] { it, core + new Vector3Int(dx, dy, 0) });
        }
        SkipDialogues();
        var c = DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(core + new Vector3Int(0, 4, 0));
        return Shot(c.x, c.y, 4.5f, 1600, 1000, "cave");
    }
    public static string CrawlerStart()
    {
        typeof(MainMenu).GetMethod("StartCrawler", NP).Invoke(MainMenu.Instance, new object[] { AdventurerAI.Role.Warrior });
        return CrawlerState();
    }
    public static string CrawlerState()
    {
        var c = CrawlerMode.Instance;
        if (c == null || c.Player == null) return "no crawler";
        var pp = c.Player.transform.position;
        var pc = DungeonBuilder.Instance.floorTilemap.WorldToCell(pp);
        float nearest = 999f; foreach (var g in GobelinAI.All) if (g != null) nearest = Mathf.Min(nearest, Vector2.Distance(g.transform.position, pp));
        return $"floor={c.floor} deepest={c.deepest} player={pp} cell={pc} stone={DungeonBuilder.Instance.stoneTilemap.HasTile(pc)} inTerr={DungeonTerritory.Instance.IsInside(pp)} " +
               $"hp={c.Player.Combat.currentHealth:0}/{c.Player.Combat.maxHealth:0} gold={c.gold} kills={c.kills} party={c.PartyCount} monsters={GobelinAI.All.Count} nearest={nearest:0.0} " +
               $"stairs={CrawlerStairs.All.Count} chests={TreasureChest.All.Count} core={(c.Core != null ? c.Core.transform.position.ToString() : "-")} inside={InteriorSystem.Instance.Inside} physical={InteriorSystem.Instance.Physical} frozen={c.Frozen}";
    }
    /// <summary>Téléporte le joueur près de l'escalier de descente de l'étage courant (test).</summary>
    public static string CrawlerRoom2() => CrawlerToStairs(true);
    public static string CrawlerRoomLast() => CrawlerToStairs(true);
    public static string CrawlerToUp() => CrawlerToStairs(false);
    private static string CrawlerToStairs(bool down)
    {
        var c = CrawlerMode.Instance;
        foreach (var st in CrawlerStairs.All)
            if (st != null && st.down == down && st.floor == Mathf.Max(1, c.floor))
            {
                var p = st.transform.position + Vector3.down * 0.9f;
                c.Player.transform.position = p; c.Player.GetComponent<Rigidbody2D>().position = p;
            }
        return CrawlerState();
    }
    public static string CrawlerHp()
    {
        var c = CrawlerMode.Instance; var p = c.Player.transform.position;
        string r = $"hp={c.Player.Combat.currentHealth:0.0} dead={c.Player.IsDead} pos={p} | ";
        foreach (var g in GobelinAI.All) if (g != null && Vector2.Distance(g.transform.position, p) < 4f) r += $"{g.gobelinName} d={Vector2.Distance(g.transform.position, p):0.0} dmg={g.attackDamage:0} | ";
        foreach (var t in PlacedObject.All) if (t != null && t.kind == ItemKind.Trap && Vector2.Distance(t.transform.position, p) < 3f) r += $"TRAP {t.itemId} d={Vector2.Distance(t.transform.position, p):0.0} | ";
        return r;
    }
    private static bool autoFight;
    public static string CrawlerAutoFight() { autoFight = !autoFight; if (autoFight) UnityEditor.EditorApplication.update += AutoFightTick; else UnityEditor.EditorApplication.update -= AutoFightTick; return $"autofight={autoFight}"; }
    private static void AutoFightTick() { if (Application.isPlaying && CrawlerMode.Instance != null && CrawlerMode.Instance.Player != null) CrawlerMode.Instance.Player.touchAttack = true; }
    public static string CrawlerRich() { CrawlerMode.Instance.gold += 500; return "gold=" + CrawlerMode.Instance.gold; }
    public static string CrawlerWin() { CrawlerMode.Instance.Core.TakeDamage(999999f); return "boom"; }
    public static string CrawlerAttack() { CrawlerMode.Instance.Player.touchAttack = true; return "attack"; }
    public static string CrawlerSkill() { CrawlerMode.Instance.Player.touchSkill = true; return "skill"; }
    public static string ShowDifficulty() { typeof(MainMenu).GetMethod("ShowDifficulty", NP).Invoke(MainMenu.Instance, null); return "difficulty"; }
    public static string SetHard() { GameDifficulty.Current = Difficulty.Difficile; return GameDifficulty.Name; }
    public static string CoreMoveTest()
    {
        SkipDialogues();
        var t = DungeonTerritory.Instance;
        var b = DungeonBuilder.Instance;
        ManaSystem.Instance.Add(9999);
        float before = ManaSystem.Instance.Current;
        int cost1 = b.MoveCoreCost;
        Vector3Int target = t.CoreCell;
        foreach (var c in t.Cells) if (c != t.CoreCell && b.CanMoveCoreTo(c, out _)) { target = c; break; }
        bool ok = b.TryMoveCore(target);
        float after = ManaSystem.Instance.Current;
        SkipDialogues();
        return $"cost1={cost1} ok={ok} spent={before - after:0} nowCost={b.MoveCoreCost} free={b.FirstCoreMoveFree}";
    }
    public static string ColonyStageCine()
    {
        SkipDialogues();
        var sm = SettlementManager.Instance;
        if (sm.stage == 0) typeof(SettlementManager).GetMethod("FoundCamp", BF).Invoke(sm, null);
        else typeof(SettlementManager).GetMethod("AdvanceStage", BF).Invoke(sm, null);
        return $"stage={sm.stage} focus={sm.StageFocus(sm.stage)} landing={sm.Site.LandingWorld} " + CineState();
    }
}
#endif
