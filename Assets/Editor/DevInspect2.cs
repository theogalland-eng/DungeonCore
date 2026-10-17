#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>Outils de test (suite de DevInspect) — appelés par le pont Claude (exec Type.Method).</summary>
public static class DevInspect2
{
    const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

    public static string LayerInfo()
    {
        var l = Object.FindFirstObjectByType<DungeonLayers>();
        var b = Object.FindFirstObjectByType<DungeonBuilder>();
        return string.Join(" | ", l.layers.Select((x, i) => $"{i}:{x.name} {x.cellBounds} land={x.landingCell} stoneAtLand={b.stoneTilemap.HasTile(x.landingCell)}"));
    }

    public static string CatalogMonsters()
    {
        var cat = DungeonCatalog.Get();
        return string.Join(", ", cat.items.Where(i => i.kind == ItemKind.Monster).Select(i => $"{i.id}(L{i.unlockLevel},{i.cost})"));
    }

    static CrawlerMode C => CrawlerMode.Instance;
    static void Tp(Vector3 p) { C.Player.transform.position = p; C.Player.GetComponent<Rigidbody2D>().position = p; }

    public static string buildingId = "house_1";
    /// <summary>Place le joueur devant la porte d'un bâtiment (buildingId) et interagit.</summary>
    public static string EnterBuilding()
    {
        foreach (var b in SettlementManager.Instance.Buildings)
            if (b != null && b.def.id == buildingId)
            {
                Tp(b.transform.position + Vector3.down * 0.9f);
                C.Interact();
                return $"door={b.transform.position} " + DevInspect.CrawlerState();
            }
        return "no building";
    }
    public static string Tavern() { buildingId = "house_1"; return EnterBuilding(); }
    public static string Guild() { buildingId = "house_4"; return EnterBuilding(); }
    public static string Forge() { buildingId = "house_3"; return EnterBuilding(); }
    public static string Mage() { buildingId = "tent_magic"; return EnterBuilding(); }

    public static string Spots()
    {
        var i = InteriorSystem.Instance;
        return $"physical={i.Physical} door={i.DoorInside} room={i.RoomRect} player={C.Player.transform.position} | " + string.Join(" | ", i.Spots.Select(s => $"{s.key}@{s.World}"));
    }
    public static int spotIndex;
    public static string GoSpot()
    {
        var s = InteriorSystem.Instance.Spots[spotIndex];
        Tp(s.World);
        C.Interact();
        return $"at {s.key} shopOpen={typeof(CrawlerMode).GetField("shop", NP).GetValue(C) != null} dialog={StoryManager.Instance.IsShowing}";
    }
    public static string GuildBoard() { spotIndex = 1; return GoSpot(); }
    public static string HireFirst()
    {
        var list = (System.Collections.IList)typeof(CrawlerMode).GetField("recruits", NP).GetValue(C);
        if (list.Count == 0) typeof(CrawlerMode).GetMethod("RollRecruits", NP).Invoke(C, null);
        C.gold += 400;
        typeof(CrawlerMode).GetMethod("Hire", NP).Invoke(C, new[] { list[0] });
        StoryManager.Instance.GetType();
        return $"party={C.PartyCount} " + string.Join(",", CrawlerCompanion.All.Select(c => $"{c.displayName}/{c.role}"));
    }
    public static string CloseShop() { typeof(CrawlerMode).GetMethod("CloseShop", NP).Invoke(C, null); return "closed"; }
    public static string WalkOut()
    {
        var i = InteriorSystem.Instance;
        Tp(i.DoorInside + Vector3.down * 0.7f);
        return "walked out";
    }
    public static string Down()
    {
        int t = Mathf.Max(1, C.floor) + 1;
        if (C.floor == 0) t = 1;
        C.GoToFloor(t, true);
        return $"going to {t}";
    }
    public static string Companions() => string.Join(" | ", CrawlerCompanion.All.Select(c => $"{c.displayName} {c.transform.position} hp={c.Combat.currentHealth:0} d={Vector2.Distance(c.transform.position, C.Player.transform.position):0.0}"));
    public static string FloorMonsters()
    {
        var p = C.Player.transform.position;
        var near = GobelinAI.All.Where(g => g != null && Vector2.Distance(g.transform.position, p) < 40f)
            .GroupBy(g => g.gobelinName).Select(g => $"{g.Key}×{g.Count()}");
        return string.Join(", ", near);
    }
    public static string DoorProbe()
    {
        var cat = DungeonCatalog.Get();
        return $"stairsDown={cat.stairsDownSprite?.name} up={cat.stairsUpSprite?.name} door_wood={cat.Find("door_wood")?.icon?.name} gate={cat.Find("door_iron")?.icon?.name} skull={cat.Find("door_skull")?.icon?.name}";
    }
}
#endif
#if UNITY_EDITOR
public static class DevInspect3
{
    const System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
    public static string Floor5() { CrawlerMode.Instance.GoToFloor(5, true); return "5"; }
    public static string Floor8() { CrawlerMode.Instance.GoToFloor(8, true); return "8"; }
    public static string Floor10() { CrawlerMode.Instance.GoToFloor(10, true); return "10"; }
    public static string Floor2() { CrawlerMode.Instance.GoToFloor(2, false); return "2 from below"; }
    /// <summary>Mode Noyau : creuse un couloir sous le cristal, y pose les trois portes et un escalier, puis photographie.</summary>
    public static string DoorShowcase()
    {
        DevInspect.SkipDialogues();
        var b = DungeonBuilder.Instance;
        var t = DungeonTerritory.Instance;
        var core = t.CoreCell;
        var cells = new System.Collections.Generic.List<Vector3Int>();
        for (int x = -4; x <= 4; x++) for (int y = -5; y <= -1; y++) cells.Add(core + new Vector3Int(x, y, 0));
        b.DigBatch(cells);
        var cat = DungeonCatalog.Get();
        b.PlaceItemFree(cat.Find("door_wood"), core + new Vector3Int(-3, -3, 0));
        b.PlaceItemFree(cat.Find("door_iron"), core + new Vector3Int(0, -3, 0));
        b.PlaceItemFree(cat.Find("door_skull"), core + new Vector3Int(3, -3, 0));
        var layers = DungeonLayers.Instance;
        layers.ApplyUnlock(1, 0);
        layers.ConnectLayer(1, core + new Vector3Int(-2, -5, 0));
        var c = b.floorTilemap.GetCellCenterWorld(core + new Vector3Int(0, -3, 0));
        MountainRelief.Instance?.SetUserView(true);
        return DevInspect.Shot(c.x, c.y, 4.5f, 1600, 1000, "doors");
    }
    public static string DoorShot()
    {
        var core = DungeonTerritory.Instance.CoreCell;
        var c = DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(core + new Vector3Int(0, -3, 0));
        return DevInspect.Shot(c.x, c.y + 0.5f, 3.8f, 1600, 1000, "doors");
    }
    /// <summary>Encombrants : refus au milieu d'un couloir, accepté dans un recoin.</summary>
    public static string BlockTest()
    {
        DevInspect.SkipDialogues();
        var b = DungeonBuilder.Instance; var t = DungeonTerritory.Instance;
        var core = t.CoreCell;
        // salle 5x3 à l'est du cristal + couloir de 4 cases vers l'est + recoin au bout
        var cells = new System.Collections.Generic.List<Vector3Int>();
        for (int x = 2; x <= 10; x++) cells.Add(core + new Vector3Int(x, 0, 0));
        b.DigBatch(cells);
        var chest = DungeonCatalog.Get().Find("chest");
        ManaSystem.Instance.Add(9999);
        bool mid = b.CanPlaceItemAt(chest, core + new Vector3Int(5, 0, 0), out string r1);
        bool end = b.CanPlaceItemAt(chest, core + new Vector3Int(10, 0, 0), out string r2);
        var po = b.PlaceItemFree(chest, core + new Vector3Int(10, 0, 0));
        return $"middle={mid} ({r1}) end={end} ({r2}) placed={po != null}";
    }
    public static string BlockState()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in PlacedObject.All) if (p != null && PlacedObject.IsBlocking(p.itemId)) sb.Append($"{p.itemId}@{p.transform.position} blocking={p.Blocking} ");
        var core = DungeonTerritory.Instance.CoreCell + new Vector3Int(10, 0, 0);
        var c = DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(core);
        var node = AstarPath.active.GetNearest(c).node;
        return sb + $" nodeWalkable={node.Walkable} insideObstacle={DungeonTerritory.InsideObstacle(c)}";
    }
    /// <summary>20 invocations collées aux murs : aucune ne doit finir dans la roche.</summary>
    public static string SpawnTest()
    {
        DevInspect.SkipDialogues();
        var t = DungeonTerritory.Instance; var b = DungeonBuilder.Instance;
        var rat = DungeonCatalog.Get().Find("rat_1");
        int n = 0;
        foreach (var c in t.Cells)
        {
            bool nearWall = false;
            foreach (var d in DungeonTerritory.Dirs) if (b.stoneTilemap.HasTile(c + d)) nearWall = true;
            if (!nearWall) continue;
            ManaSystem.Instance.Add(100);
            MonsterSpawner.Instance.TrySummonAt(b.floorTilemap.GetCellCenterWorld(c), rat.prefab, 1, "Rat test");
            if (++n >= 20) break;
        }
        return $"summoned {n}";
    }
    public static string SpawnCheck()
    {
        int stuck = 0, total = 0;
        foreach (var g in GobelinAI.All) { if (g == null) continue; total++; if (DungeonTerritory.InsideObstacle(g.transform.position) || DungeonBuilder.Instance.stoneTilemap.HasTile(DungeonBuilder.Instance.floorTilemap.WorldToCell(g.transform.position))) stuck++; }
        return $"monsters={total} inWall={stuck} mana={ManaSystem.Instance.Current}/{ManaSystem.Instance.Max} lvl={PlayerLevel.Instance.currentLevel} xpNext={PlayerLevel.Instance.xpForNextLevel}";
    }
    /// <summary>Porte dans un couloir est-ouest.</summary>
    public static string DoorEW()
    {
        var b = DungeonBuilder.Instance; var core = DungeonTerritory.Instance.CoreCell;
        b.PlaceItemFree(DungeonCatalog.Get().Find("door_wood"), core + new Vector3Int(6, 0, 0));
        b.PlaceItemFree(DungeonCatalog.Get().Find("door_iron"), core + new Vector3Int(8, 0, 0));
        MountainRelief.Instance?.SetUserView(true);
        return "doors";
    }
    public static string DoorEWShot()
    {
        var c = DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(DungeonTerritory.Instance.CoreCell + new Vector3Int(6, 0, 0));
        return DevInspect.Shot(c.x, c.y, 3.5f, 1600, 1000, "doors_ew");
    }
    /// <summary>Labyrinthe de test : culs-de-sac à l'ouest et au nord, cristal au bout du couloir est.</summary>
    public static string MazeTest()
    {
        DevInspect.SkipDialogues();
        var b = DungeonBuilder.Instance; var t = DungeonTerritory.Instance;
        var c0 = t.CoreCell;
        var cells = new System.Collections.Generic.List<Vector3Int>();
        void Line(Vector3Int from, Vector3Int dir, int n) { for (int i = 1; i <= n; i++) cells.Add(from + dir * i); }
        Line(c0, Vector3Int.up, 10);                                       // couloir principal vers le nord
        Line(c0 + Vector3Int.up * 4, Vector3Int.left, 6);                  // cul-de-sac ouest
        Line(c0 + Vector3Int.up * 7, Vector3Int.right, 5);                 // branche est...
        Line(c0 + Vector3Int.up * 7 + Vector3Int.right * 5, Vector3Int.up, 3);   // ...en cul-de-sac
        Line(c0 + Vector3Int.up * 10, Vector3Int.left, 7);                 // vers la salle du cristal
        var room = c0 + Vector3Int.up * 12 + Vector3Int.left * 7;
        Line(c0 + Vector3Int.up * 10 + Vector3Int.left * 7, Vector3Int.up, 1);
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) cells.Add(room + new Vector3Int(x, y, 0));
        b.DigBatch(cells);
        ManaSystem.Instance.Add(9999);
        bool moved = b.TryMoveCore(room);
        GuildSystem.Instance.GetType();
        return $"dug={cells.Count} coreMoved={moved} core={t.CoreCell}";
    }
    public static string Explore()
    {
        var sb = new System.Text.StringBuilder();
        IntruderMap m = null;
        foreach (var x in AdventurerAI.All) if (x != null && x.Map != null) { m = x.Map; break; }
        if (m == null) m = new IntruderMap();
        sb.Append($"t={Time.time:0} map={m.Floor.Count}/{DungeonTerritory.Instance.CellCount} walls={m.Walls.Count} blocked={m.Blocked.Count} core={(m.CoreCell.HasValue ? m.CoreCell.Value.ToString() : "?")} | ");
        foreach (var a in AdventurerAI.All)
            if (a != null && !a.IsPrisoner)
                sb.Append($"{a.DisplayName} {a.State} {DungeonBuilder.Instance.floorTilemap.WorldToCell(a.transform.position)} in={a.IsInsideDungeon} seen={a.HasSeenCore} assess={a.IsAssessor} why={a.LeaveReason} | ");
        return sb.ToString();
    }
    public static string Probe()
    {
        var b = DungeonBuilder.Instance; var t = DungeonTerritory.Instance;
        var room = t.CoreCell;
        b.CanMoveCoreTo(room, out string why);
        var a = AdventurerAI.All.Find(x => x != null && x.IsInsideDungeon);
        int before = 0;
        var cell = a != null ? b.floorTilemap.WorldToCell(a.transform.position) : Vector3Int.zero;
        return $"why={why} roomInside={t.IsInsideCell(room)} roomStone={b.stoneTilemap.HasTile(room)} roomGround={t.IsDungeonGround(room)} terr={t.CellCount} | observe {before} cell={cell} ground={t.IsDungeonGround(cell)} floorTile={b.floorTilemap.GetTile(cell)?.name}";
    }
    public static string CoreRange()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>();
        var sb = new System.Text.StringBuilder($"core={core.transform.position} hp={core.currentHealth:0} | ");
        foreach (var a in AdventurerAI.All) if (a != null) sb.Append($"{a.DisplayName} d={Vector2.Distance(a.transform.position, core.transform.position):0.00} range={a.combat.attackRange:0.00} aware={a.IsAware} pos={a.transform.position} | ");
        return sb.ToString();
    }
    /// <summary>Mode Aventurier : un compagnon de chaque rôle à côté du joueur (photo).</summary>
    public static string RolesShot()
    {
        var c = CrawlerMode.Instance;
        c.gold += 2000;
        var t = typeof(CrawlerMode);
        var recruitT = t.GetNestedType("Recruit", NP);
        foreach (var role in new[] { AdventurerAI.Role.Archer, AdventurerAI.Role.Mage, AdventurerAI.Role.Healer })
        {
            var r = System.Activator.CreateInstance(recruitT);
            recruitT.GetField("name").SetValue(r, role.ToString());
            recruitT.GetField("role").SetValue(r, role);
            recruitT.GetField("cost").SetValue(r, 1);
            recruitT.GetField("female").SetValue(r, role == AdventurerAI.Role.Healer);
            t.GetMethod("Hire", NP).Invoke(c, new[] { r });
        }
        DevInspect.SkipDialogues();
        return "party=" + c.PartyCount;
    }
    public static string MateSprites()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var m in CrawlerCompanion.All)
        {
            var sr = m.GetComponent<SpriteRenderer>();
            var an = m.GetComponent<Animator>();
            sb.Append($"{m.displayName}: sprite={sr.sprite?.name} tex={sr.sprite?.texture.name} size={sr.bounds.size} scale={m.transform.localScale} ctrl={an?.runtimeAnimatorController?.name} | ");
        }
        var p = CrawlerMode.Instance.Player.GetComponent<SpriteRenderer>();
        sb.Append($"player sprite={p.sprite?.name} size={p.bounds.size}");
        return sb.ToString();
    }
    public static string ForceReimportRoles()
    {
        foreach (var n in new[] { "adv_archer", "adv_mage", "adv_healer" })
        {
            UnityEditor.AssetDatabase.ImportAsset($"Assets/Animations/Generated/{n}/{n}.controller", UnityEditor.ImportAssetOptions.ForceUpdate);
            UnityEditor.AssetDatabase.ImportAsset($"Assets/Prefabs/Characters/{n}.prefab", UnityEditor.ImportAssetOptions.ForceUpdate);
        }
        return PrefabCheck();
    }
    public static string PrefabCheck()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var n in new[] { "adv_archer", "adv_mage", "adv_healer" })
        {
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Characters/{n}.prefab");
            var an = go.GetComponent<Animator>();
            var cat = DungeonCatalog.Get();
            sb.Append($"{n}: ctrl={an?.runtimeAnimatorController?.name} sprite={go.GetComponent<SpriteRenderer>().sprite?.name} catArcher={cat.archerPrefab?.name} | ");
        }
        return sb.ToString();
    }
    /// <summary>Labyrinthe + portes (couloir nord et couloir est-ouest) + coffre + un monstre derrière une porte.</summary>
    public static string MazeDoors()
    {
        string r = MazeTest();
        var b = DungeonBuilder.Instance; var t = DungeonTerritory.Instance;
        var c0 = t.CoreCell;   // le cristal a bougé : on repart de l'ancien emplacement (bas du couloir principal)
        var start = b.floorTilemap.WorldToCell(new Vector3(-7.5f, -3.5f, 0f));
        var cat = DungeonCatalog.Get();
        b.PlaceItemFree(cat.Find("door_wood"), start + Vector3Int.up * 2);                         // couloir nord-sud
        b.PlaceItemFree(cat.Find("door_iron"), start + Vector3Int.up * 10 + Vector3Int.left * 3);  // couloir est-ouest
        b.PlaceItemFree(cat.Find("chest"), start + Vector3Int.up * 4 + Vector3Int.left * 6);       // au fond du cul-de-sac ouest
        b.PlaceItemFree(cat.Find("chest"), start + Vector3Int.up * 4 + Vector3Int.left * 1);       // à l'entrée de la branche ouest (en vue)
        var rat = cat.Find("rat_1");
        var g = MonsterSpawner.Instance.SpawnFree(rat.prefab, b.floorTilemap.GetCellCenterWorld(start + Vector3Int.up * 5), b.floorTilemap.GetCellCenterWorld(start + Vector3Int.up * 5), "Rat garde");
        g.SetBehavior(GobelinAI.GoblinBehavior.Patrol);
        return r + $" door1={start + Vector3Int.up * 2} rat={g.transform.position}";
    }
    public static string ChestInView()
    {
        var b = DungeonBuilder.Instance;
        var start = b.floorTilemap.WorldToCell(new Vector3(-7.5f, -3.5f, 0f));
        b.PlaceItemFree(DungeonCatalog.Get().Find("chest"), start + Vector3Int.up * 4 + Vector3Int.left * 1);
        LootSystem.Instance.Add(LootSystem.Instance.AllDefs.GetEnumerator().MoveNext() ? new System.Collections.Generic.List<LootDef>(LootSystem.Instance.AllDefs)[0].id : "x", 1, false);
        return "chest";
    }
    public static string Chests()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in TreasureChest.All) if (c != null) sb.Append($"chest@{c.transform.position} full={c.IsFull} blocking={c.GetComponent<PlacedObject>()?.Blocking} | ");
        return sb.ToString();
    }
    public static string ChestShot()
    {
        var b = DungeonBuilder.Instance;
        var start = b.floorTilemap.WorldToCell(new Vector3(-7.5f, -3.5f, 0f));
        var c = b.floorTilemap.GetCellCenterWorld(start + Vector3Int.up * 4);
        return DevInspect.Shot(c.x, c.y, 2.6f, 1600, 1000, "chest_loot");
    }
    public static string OrderRat()
    {
        var b = DungeonBuilder.Instance;
        var start = b.floorTilemap.WorldToCell(new Vector3(-7.5f, -3.5f, 0f));
        foreach (var g in GobelinAI.All) if (g != null && g.gobelinName == "Rat garde") { g.MoveToPosition(b.floorTilemap.GetCellCenterWorld(start)); return "ordered " + g.transform.position; }
        return "no rat";
    }
    public static string RatDebug()
    {
        foreach (var g in GobelinAI.All)
            if (g != null && g.gobelinName == "Rat garde")
            {
                var t = typeof(GobelinAI);
                var path = t.GetField("path", NP).GetValue(g) as Pathfinding.Path;
                var sk = g.GetComponent<Pathfinding.Seeker>();
                var node = AstarPath.active.GetNearest(g.transform.position).node;
                var b = DungeonBuilder.Instance;
                var start = b.floorTilemap.WorldToCell(new Vector3(-7.5f, -3.5f, 0f));
                var dn = AstarPath.active.GetNearest(b.floorTilemap.GetCellCenterWorld(start + Vector3Int.up * 2)).node;
                return $"beh={t.GetField("currentBehavior", NP).GetValue(g)} path={(path == null ? "null" : path.vectorPath.Count + " pts err=" + path.error)} wp={t.GetField("currentWaypoint", NP).GetValue(g)} done={sk.IsDone()} tags={sk.traversableTags} vel={g.GetComponent<Rigidbody2D>().linearVelocity} nodeTag={node.Tag} doorNodeTag={dn.Tag} doorWalk={dn.Walkable} mission={t.GetField("isOnMission", NP).GetValue(g)}";
            }
        return "no rat";
    }
    public static string DoorStatus()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var d in DungeonDoor.All) sb.Append($"{d.name}@{d.transform.position} open={d.IsOpen} sprite={d.doorRenderer.sprite.name} | ");
        foreach (var g in GobelinAI.All) if (g != null) sb.Append($"{g.gobelinName}@{g.transform.position} cross={g.MayCrossDoors} | ");
        sb.Append($"underAttack={DungeonBuilder.UnderAttack}");
        return sb.ToString();
    }
    public static string TryDig()
    {
        DungeonBuilder.Instance.SetMode(DungeonBuilder.BuildMode.Digging);
        return "mode=" + DungeonBuilder.Instance.GetCurrentMode();
    }
    public static string DoorShotEW()
    {
        var b = DungeonBuilder.Instance;
        var start = b.floorTilemap.WorldToCell(new Vector3(-7.5f, -3.5f, 0f));
        var c = b.floorTilemap.GetCellCenterWorld(start + Vector3Int.up * 9 + Vector3Int.left * 2);
        return DevInspect.Shot(c.x, c.y, 3.2f, 1600, 1000, "doors_ew");
    }
    public static string Fast5() { GameManager.Instance.SetSpeed(5f); return "x5"; }
    public static string IntroNow() { StoryManager.Instance.StartIntro(); return "intro"; }
    public static string NearObjects()
    {
        var p = CrawlerMode.Instance.Player.transform.position;
        var sb = new System.Text.StringBuilder();
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (sr.enabled && sr.gameObject.activeInHierarchy && Vector2.Distance(sr.transform.position, p) < 9f && sr.sprite != null)
                sb.Append($"{sr.gameObject.name}[{sr.sprite.name}] ");
        return sb.ToString();
    }
    /// <summary>Géographie utile à la cinématique d'intro.</summary>
    public static string IntroGeo()
    {
        var sb = new System.Text.StringBuilder();
        var core = Object.FindFirstObjectByType<DungeonCore>();
        var sr = core.GetComponent<SpriteRenderer>();
        sb.Append($"core pos={core.transform.position} scale={core.transform.localScale} sprite={sr.sprite.name} rect={sr.sprite.rect} pivot={sr.sprite.pivot} ppu={sr.sprite.pixelsPerUnit} bounds={sr.bounds.size} order={sr.sortingOrder} tex={sr.sprite.texture.name}\n");
        foreach (Transform ch in core.transform) sb.Append($"  child {ch.name} {ch.localPosition}\n");
        var sp = Object.FindFirstObjectByType<AdventurerSpawner>();
        sb.Append($"spawner={sp.SpawnPosition} crater={MountainRelief.Instance?.craterWorld}\n");
        var t = DungeonTerritory.Instance;
        sb.Append($"coreCell={t.CoreCell} entrance={t.EntranceCell} entranceW={t.CellCenter(t.EntranceCell)}\n");
        var sol = GameObject.Find("Sol").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var blk = GameObject.Find("NoPathfinding").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var c0 = sol.WorldToCell(sp.SpawnPosition);
        for (int y = 6; y >= -6; y -= 1)
        {
            for (int x = -12; x <= 12; x++)
            {
                var c = c0 + new Vector3Int(x, y, 0);
                var a = sol.GetTile(c); var b = blk.GetTile(c);
                char ch = b != null ? (b.name.Contains("reef") ? 'r' : b.name.Contains("abyss") ? 'a' : 'w') : a == null ? ' ' : a.name.Contains("gravel") ? 's' : a.name.Contains("plank") || a.name.Contains("82") ? 'p' : 'g';
                if (x == 0 && y == 0) ch = '@';
                sb.Append(ch);
            }
            sb.Append('\n');
        }
        foreach (var l in Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None))
            if (l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global) sb.Append($"global {l.name} {l.color} {l.intensity}\n");
        sb.Append($"blk sample={blk.GetTile(c0 + new Vector3Int(8, 0, 0))?.name} sol sample={sol.GetTile(c0 + new Vector3Int(-8, 0, 0))?.name}");
        return sb.ToString();
    }
    public static string PauseInfo()
    {
        var gm = GameManager.Instance;
        var f = typeof(GameManager).GetField("pauseRequests", NP);
        var u = typeof(GameManager).GetField("userPaused", NP);
        return $"requests={f.GetValue(gm)} user={u.GetValue(gm)} speed={gm.GameSpeed} ts={Time.timeScale} story={StoryManager.Instance?.IsShowing} intro={StoryManager.Instance?.HasFlag("intro")}";
    }
    public static string IntroBeach()
    {
        var c = CinematicDirector.Instance;
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        object g = typeof(CinematicDirector).GetMethod("ComputeGeo", NP).Invoke(c, new object[] { core });
        var t = g.GetType();
        Vector3 beach = (Vector3)t.GetField("beach").GetValue(g), sea = (Vector3)t.GetField("sea").GetValue(g), sails = (Vector3)t.GetField("sails").GetValue(g);
        var dock = Object.FindFirstObjectByType<AdventurerSpawner>().SpawnPosition;
        DevInspect.shotX = beach.x; DevInspect.shotY = beach.y; DevInspect.shotSize = 9;
        return $"beach={beach} sea={sea} sails={sails} dock={dock} d={Vector2.Distance(beach, dock):0.0} " + DevInspect.Custom();
    }
    // ── Héros 8 directions (2026-10-17) ──
    static CrawlerPlayer Hero => CrawlerMode.Instance != null ? CrawlerMode.Instance.Player : null;
    public static string HeroWalkSE() { VirtualJoystick.Value = new Vector2(0.55f, -0.55f); return HeroState(); }
    public static string HeroWalkNW() { VirtualJoystick.Value = new Vector2(-0.55f, 0.55f); return HeroState(); }
    public static string HeroRunNE() { VirtualJoystick.Value = new Vector2(0.69f, 0.69f); return HeroState(); }
    public static string HeroRunW() { VirtualJoystick.Value = new Vector2(-1f, 0f); return HeroState(); }
    public static string HeroStop() { VirtualJoystick.Value = Vector2.zero; return HeroState(); }
    public static string HeroRoll() { Hero.touchRoll = true; return "roll"; }
    public static string HeroAttack() { Hero.touchAttack = true; return "attack"; }
    public static string HeroState()
    {
        var h = Hero;
        if (h == null) return "pas de héros";
        var a = h.GetComponent<Animator>();
        var ci = a.GetCurrentAnimatorClipInfo(0);
        string clip = ci.Length > 0 ? ci[0].clip.name : "-";
        return $"clip={clip} sprite={h.GetComponent<SpriteRenderer>().sprite?.name} facing={h.Facing} run={h.Running} roll={h.Rolling} stamina={h.Stamina:0} exhausted={h.Exhausted} v={h.GetComponent<Rigidbody2D>().linearVelocity} ctrl={a.runtimeAnimatorController?.name}";
    }
    public static string HeroCloseShot()
    {
        var cam = Camera.main;
        cam.orthographicSize = 2.2f;
        var p = Hero.transform.position;
        cam.transform.position = new Vector3(p.x, p.y + 0.3f, cam.transform.position.z);
        return DevInspect.GameShot();
    }
    public static string SeaProbe()
    {
        var h = Hero;
        var sb = new System.Text.StringBuilder();
        var col = h.GetComponent<Collider2D>();
        sb.Append($"hero layer={LayerMask.LayerToName(h.gameObject.layer)} col={col?.GetType().Name} trigger={col?.isTrigger} rbType={h.GetComponent<Rigidbody2D>().bodyType} excl={col?.excludeLayers.value}\n");
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.TilemapCollider2D>(FindObjectsSortMode.None))
            sb.Append($"  tmcol {tm.name} layer={LayerMask.LayerToName(tm.gameObject.layer)} enabled={tm.enabled} trigger={tm.isTrigger} hit={Physics2D.GetIgnoreLayerCollision(h.gameObject.layer, tm.gameObject.layer)}\n");
        var p = (Vector2)h.transform.position;
        foreach (var off in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, -1) })
        {
            var hits = Physics2D.OverlapPointAll(p + off);
            sb.Append($"  at {p + off}: ");
            foreach (var c in hits) sb.Append($"{c.name}({LayerMask.LayerToName(c.gameObject.layer)}) ");
            sb.Append("\n");
        }
        return sb.ToString();
    }
    /// <summary>Déclenche une action du héros puis capture l'écran en plein mouvement (après 'delay' secondes).</summary>
    static string MidShot(System.Action act, float delay, string tag)
    {
        var cam = Camera.main;
        cam.orthographicSize = 2.4f;
        act();
        CrawlerMode.Instance.StartCoroutine(ShotLater(delay, tag));
        return "ok " + tag;
    }
    static System.Collections.IEnumerator ShotLater(float delay, string tag)
    {
        yield return new WaitForSeconds(delay);
        string path = System.IO.Path.GetFullPath($"Temp/mid_{tag}.png");
        ScreenCapture.CaptureScreenshot(path);
        midLog = HeroState();
    }
    static string midLog = "";
    public static string MidLog() => midLog;
    public static string HeroRollShot() => MidShot(() => Hero.touchRoll = true, 0.14f, "roll");
    public static string HeroWalkAtkShot() => MidShot(() => { VirtualJoystick.Value = new Vector2(0.5f, -0.5f); Hero.touchAttack = true; }, 0.2f, "walkatk");
    public static string HeroRunAtkShot() => MidShot(() => { VirtualJoystick.Value = new Vector2(-0.7f, -0.7f); Hero.touchAttack = true; }, 0.22f, "runatk");
    static string traceLog = "";
    public static string TraceLog() => traceLog;
    /// <summary>Marche en diagonale imparfaite et note le sprite affiché à chaque image.</summary>
    public static string HeroDiagTrace()
    {
        VirtualJoystick.Value = new Vector2(0.62f, -0.45f);
        CrawlerMode.Instance.StartCoroutine(Trace());
        return "trace";
    }
    static System.Collections.IEnumerator Trace()
    {
        var sb = new System.Text.StringBuilder();
        var sr = Hero.GetComponent<SpriteRenderer>();
        var a = Hero.GetComponent<Animator>();
        yield return new WaitForSeconds(0.3f);
        for (int i = 0; i < 40; i++)
        {
            var infos = a.GetCurrentAnimatorClipInfo(0);
            string w = "";
            foreach (var ci in infos) w += ci.clip.name.Replace("swordsman_1_", "") + ":" + ci.weight.ToString("0.00") + " ";
            sb.Append(sr.sprite != null ? sr.sprite.name.Replace("swordsman_1_", "") : "null").Append(" [").Append(w).Append("] en=").Append(sr.enabled).Append(" a=").Append(sr.color.a.ToString("0.0")).Append(" | ");
            yield return null;
        }
        VirtualJoystick.Value = Vector2.zero;
        traceLog = sb.ToString();
    }
    // ── Personnalisation, paliers, gestes (2026-10-17) ──
    public static string CustomizeOpen()
    {
        HeroLook.Hair = HeroLook.HairChoices[1]; HeroLook.Skin = HeroLook.SkinChoices[2]; HeroLook.Outfit = HeroLook.OutfitChoices[1];
        HeroLook.HeroName = "Aldric"; HeroLook.Save();
        typeof(MainMenu).GetMethod("ShowCustomize", NP).Invoke(MainMenu.Instance, new object[] { AdventurerAI.Role.Warrior });
        return DevInspect.GameShot();
    }
    public static string CustomizeGo()
    {
        typeof(MainMenu).GetMethod("StartCrawler", NP).Invoke(MainMenu.Instance, new object[] { AdventurerAI.Role.Warrior });
        return "go";
    }
    public static string ArmorUp()
    {
        var c = CrawlerMode.Instance;
        var f = typeof(CrawlerMode).GetField("armorLvl");
        f.SetValue(c, (int)f.GetValue(c) + 3);
        typeof(CrawlerMode).GetMethod("RefreshStats", NP).Invoke(c, new object[] { false });
        return $"armor={f.GetValue(c)} tier={Hero.Tier} ctrl={Hero.GetComponent<Animator>().runtimeAnimatorController.name}";
    }
    public static string DrinkShot() => MidShot(() => { var c = Hero.Combat; c.currentHealth = c.maxHealth * 0.4f; CrawlerMode.Instance.UsePotion(); }, 0.35f, "drink");
    public static string LookProbe()
    {
        var cat = DungeonCatalog.Get();
        var sp = cat.lookPreview != null && cat.lookPreview.Length > 0 ? cat.lookPreview[0] : null;
        var ta = Resources.Load<TextAsset>("HeroPalettes/swordsman_1");
        var m = HeroLook.Mapping.Build("swordsman_1", HeroLook.HairChoices[1], HeroLook.SkinChoices[2], HeroLook.OutfitChoices[1]);
        var f = TMPro.TMP_Settings.defaultFontAsset;
        return $"preview={sp?.name} tex={sp?.texture?.name} readable={sp?.texture?.isReadable} json={(ta != null ? ta.text.Length : -1)} map={(m != null ? m.map.Count : -1)} " +
               $"font={f?.name} atlas={f?.atlasTexture?.width}x{f?.atlasTexture?.height} mode={f?.atlasPopulationMode}";
    }
    public static string DecalsOff() { var g = GameObject.Find("Détails du sol"); if (g != null) g.SetActive(false); return DevInspect.GameShot(); }
    public static string DecalsOn() { foreach (var t in Resources.FindObjectsOfTypeAll<Transform>()) if (t.name == "Détails du sol") t.gameObject.SetActive(true); return DevInspect.GameShot(); }
    public static string RebuildCatalog() { CatalogBuilder.BuildCatalog(); var c = DungeonCatalog.Get(); return "preview=" + string.Join(",", System.Linq.Enumerable.Select(c.lookPreview, x => x != null ? x.name : "null")); }
    public static float pickX = 0.88f, pickY = 0.2f;
    /// <summary>Ce qui est dessiné au point de l'écran (pickX, pickY en fraction de l'écran).</summary>
    public static string Pick()
    {
        var cam = Camera.main;
        Vector3 w = cam.ViewportToWorldPoint(new Vector3(pickX, pickY, 10f));
        var sb = new System.Text.StringBuilder($"monde={w} ");
        foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (r.enabled && r.gameObject.activeInHierarchy && r.bounds.Contains(new Vector3(w.x, w.y, r.bounds.center.z)))
                sb.Append($"[SR {r.name} parent={r.transform.parent?.name} sprite={r.sprite?.name} col={r.color} order={r.sortingOrder}] ");
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
        {
            var c = tm.WorldToCell(w);
            var t = tm.GetTile(c);
            if (t != null) sb.Append($"[TM {tm.name} {t.name} col={tm.GetColor(c)}] ");
        }
        return sb.ToString();
    }
    /// <summary>Flash d'impact longuement maintenu sur le héros, pour le photographier.</summary>
    public static string FlashTest()
    {
        var sr = Hero.GetComponent<SpriteRenderer>();
        HitFlashFx.Flash(sr, 2f);
        return MidShot(() => { }, 0.1f, "flash");
    }
    public static string DockProbe()
    {
        var sb = new System.Text.StringBuilder();
        var hero = Hero.transform.position;
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
            sb.Append($"[{tm.name} pos={tm.transform.position} anchor={tm.tileAnchor} grid={tm.layoutGrid?.transform.position}] ");
        var decor = GameObject.Find("Decor").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var c = decor.WorldToCell(hero);
        sb.Append($" hero={hero} cell={c} ");
        for (int dx = -3; dx <= 2; dx++)
        {
            var k = c + new Vector3Int(dx, 0, 0);
            sb.Append($" x{k.x}:decor={decor.GetTile(k)?.name}");
        }
        return sb.ToString();
    }
    /// <summary>Cases de l'île sans aucune tuile au sol (on y voit le fond noir de la caméra).</summary>
    public static string Holes()
    {
        UnityEngine.Tilemaps.Tilemap sol = null, blk = null;
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
        { if (tm.name == "Sol") sol = tm; if (tm.name == "NoPathfinding") blk = tm; }
        var b = sol.cellBounds;
        int holes = 0;
        var sb = new System.Text.StringBuilder();
        var list = new System.Collections.Generic.List<Vector3Int>();
        for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var c = new Vector3Int(x, y, 0);
                if (sol.HasTile(c) || blk.HasTile(c)) continue;
                // trou entouré de terrain (pas la marge extérieure de la carte)
                int n = 0;
                foreach (var d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                    if (sol.HasTile(c + d * 3) || blk.HasTile(c + d * 3)) n++;
                if (n < 4) continue;
                holes++;
                if (list.Count < 40) list.Add(c);
            }
        foreach (var c in list) sb.Append($"{c.x},{c.y} ");
        return $"bounds={b} trous={holes} : {sb}";
    }
    public static string PickFromFile()
    {
        var t = System.IO.File.ReadAllText("Temp/pick.txt").Split(' ');
        pickX = float.Parse(t[0], System.Globalization.CultureInfo.InvariantCulture);
        pickY = float.Parse(t[1], System.Globalization.CultureInfo.InvariantCulture);
        return Pick();
    }
    public static string CullingFix()
    {
        int n = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var r in Object.FindObjectsByType<UnityEngine.Tilemaps.TilemapRenderer>(FindObjectsSortMode.None))
        {
            sb.Append($"{r.name}:{r.mode}/{r.detectChunkCullingBounds}/{r.chunkCullingBounds} ");
            r.detectChunkCullingBounds = UnityEngine.Tilemaps.TilemapRenderer.DetectChunkCullingBounds.Manual;
            r.chunkCullingBounds = new Vector3(4f, 4f, 0f);
            n++;
        }
        return $"{n} : " + sb;
    }
    public static string PickAll()
    {
        var w = new Vector3(43.3f, -7.25f, 0f);
        var sb = new System.Text.StringBuilder();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var b = r.bounds;
            if (r is UnityEngine.Tilemaps.TilemapRenderer) continue;
            if (w.x < b.min.x || w.x > b.max.x || w.y < b.min.y || w.y > b.max.y) continue;
            string extra = r is SpriteRenderer sr ? $" sprite={sr.sprite?.name} col={sr.color} mat={sr.sharedMaterial?.name}" : $" mat={r.sharedMaterial?.name}";
            sb.Append($"[{r.GetType().Name} {r.name} <- {r.transform.parent?.name} en={r.enabled}/{r.gameObject.activeInHierarchy} order={r.sortingOrder}{extra} size={b.size}] ");
        }
        return sb.ToString();
    }
    public static string NearTiles()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
        {
            var r = tm.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            var c0 = tm.WorldToCell(new Vector3(43.3f, -7.25f, 0f));
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    var c = c0 + new Vector3Int(dx, dy, 0);
                    var t = tm.GetTile(c);
                    if (t != null && tm.name != "Sol" && tm.name != "Decor") sb.Append($"[{tm.name} rend={r?.enabled} {c} {t.name} col={tm.GetColor(c)} spr={tm.GetSprite(c)?.name}] ");
                }
        }
        return sb.ToString();
    }

    public static string TrailInfo()
    {
        var w = TrailWear.Instance;
        var ads = Object.FindObjectsByType<AdventurerAI>(FindObjectsSortMode.None).Length;
        return w == null ? "pas de TrailWear" : $"cases usées {w.WornCells}, aventuriers {ads}, temps x{Time.timeScale}";
    }
    public static string Fast() { Time.timeScale = 8f; return "x8"; }
    public static string Spawn3() { var sp = Object.FindFirstObjectByType<AdventurerSpawner>(); for (int i = 0; i < 3; i++) sp.SpawnGroup(); return "3 groupes"; }
    public static string Normal() { Time.timeScale = 1f; return "x1"; }
    public static string TrailShot()
    {
        var site = Object.FindFirstObjectByType<SettlementSite>();
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        var mid = (site.LandingWorld + core) * 0.5f;
        return DevInspect.Shot(mid.x, mid.y, 26, 1600, 1000, "trail");
    }

    public static string TacticsArena()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        var cat = DungeonCatalog.Get();
        int n = 0;
        foreach (var fam in new[] { "goblin", "gnoll", "rat", "rat" })
        {
            var item = cat.items.Find(i => i.id == fam || (i.prefab != null && i.prefab.GetComponent<MonsterUnit>() != null && i.prefab.GetComponent<MonsterUnit>().familyId == fam));
            if (item == null || item.prefab == null) continue;
            Vector2 p = (Vector2)core + new Vector2(Random.Range(-1f, 1f), -1f);
            if (MonsterSpawner.Instance.SpawnFree(item.prefab, p, p, fam) != null) n++;
        }
        return $"{n} monstres";
    }
    public static string PuzzleTest()
    {
        DevInspect.SkipDialogues();
        while (PlayerLevel.Instance.currentLevel < 6) PlayerLevel.Instance.AddXP(PlayerLevel.Instance.xpForNextLevel);
        ManaSystem.Instance.Add(9000);
        var cat = DungeonCatalog.Get();
        var core = DungeonTerritory.Instance.CoreCell;
        var m = typeof(DungeonBuilder).GetMethod("TryPlaceItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var log = new System.Text.StringBuilder();
        foreach (var (id, dx, dy) in new[] { ("lever_door", 0, -3), ("lever", 0, -5), ("pressure_plate", 0, -4), ("nest", -1, 1), ("nest", 1, 1), ("nest", 0, 1) })
        {
            var r = m.Invoke(DungeonBuilder.Instance, new object[] { cat.Find(id), core + new Vector3Int(dx, dy, 0) });
            log.Append($"{id}={r} ");
        }
        return log.ToString();
    }
    public static string PuzzleState()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var d in DungeonDoor.All) if (d != null && d.sealSwitches > 0) sb.Append($"[porte scellée={d.Sealed} ouverte={d.IsOpen}] ");
        foreach (var s in PuzzleSwitch.All) if (s != null) sb.Append($"[{s.kind} actif={s.IsActive}] ");
        if (ThemedRooms.Instance != null) foreach (var r in ThemedRooms.Instance.rooms) sb.Append($"[salle {ThemedRooms.Names[(int)r.kind]}] ");
        foreach (var a in AdventurerAI.All) if (a != null && a.Life != null) sb.Append($"<{a.State} {a.Life.Activity}> ");
        sb.Append($" monstres={MonsterUnit.All.Count}");
        return sb.ToString();
    }
    public static string CardFirst()
    {
        foreach (var a in AdventurerAI.All)
            if (a != null && !a.IsDead && a.Life != null)
            {
                foreach (var t in a.Life.traits) a.Life.Reveal(t);
                IntruderCard.Show(a);
                var cam = Camera.main; cam.transform.position = new Vector3(a.transform.position.x, a.transform.position.y, -10f);
                return a.name;
            }
        return "aucun";
    }
    public static string ColonyOpen()
    {
        var sm = SettlementManager.Instance;
        if (sm != null && sm.stage < 2) sm.BuildInstantly(2);
        ColonyWindow.Instance?.Open();
        return $"colonie stade {sm?.stage}";
    }
    public static string GodCore() { var c = Object.FindFirstObjectByType<DungeonCore>(); c.maxHealth = 1e6f; c.currentHealth = 1e6f; return "cristal invulnérable"; }
    public static string Starve() { int n = 0; foreach (var a in AdventurerAI.All) if (a != null && a.Life != null) { a.Life.hunger = 88f; a.Life.rations = 0; n++; } return $"{n} affamés"; }
    public static string LifeReport()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var a in AdventurerAI.All)
        {
            if (a == null || a.Life == null) continue;
            var l = a.Life;
            sb.Append($"[{a.combat.adventurerName.Split(' ')[0]} {a.State} {l.Activity} faim={l.hunger:0} rat={l.rations} ivre={l.drunk:0.0} traits={string.Join("/", l.traits)} vus={l.revealed.Count}] ");
        }
        var sp = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var w in WildAnimal.All) if (w != null && !w.IsDead) { string k = w.species.ToString(); sp[k] = (sp.TryGetValue(k, out int n) ? n : 0) + 1; }
        sb.Append($" | faune {string.Join(",", System.Linq.Enumerable.Select(sp, kv => kv.Key + "=" + kv.Value))} carcasses={WildAnimal.Carcasses.Count} lacs={FreshWater.Lakes.Count}");
        sb.Append($" | chasses={AdventurerLife.Hunts} bagarres={AdventurerLife.Brawls} intrus={AdventurerAI.All.Count} | tactiques={GobelinAI.TacticsUsed} temps x{Time.timeScale} jour {GameManager.Instance?.CurrentDay} {GameManager.Instance?.DayProgress:0.00}");
        return sb.ToString();
    }
    public static string TacticsCount() =>$"tactiques lancées : {GobelinAI.TacticsUsed}";
}
#endif
