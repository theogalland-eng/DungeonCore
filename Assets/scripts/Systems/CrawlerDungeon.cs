using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mode Aventurier : le donjon du Noyau adverse, sur DIX ÉTAGES procéduraux.
///   - Étage 1 : creusé dans la montagne de l'île (on y entre à pied depuis la ville).
///   - Étages 2 à 9 : générés dans les zones souterraines (étages du mode Noyau) ; deux zones alternent,
///     un étage est (re)matérialisé quand on y descend. Son plan dépend d'une graine : il reste le même,
///     mais le donjon, vivant, le repeuple à chaque retour.
///   - Étage 10 : l'antre du cristal (zone réservée), gardé par un seigneur démon.
/// Plus on descend, plus les monstres sont forts, évolués et nombreux (rats et gobelins d'abord... démons à la fin).
/// </summary>
public static class CrawlerDungeon
{
    public const int FloorCount = 10;

    public class Room
    {
        public RectInt rect;
        public int depth;
        public Vector3 Center => new Vector3(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f, 0f);
        public Vector3Int CenterCell => new Vector3Int(rect.x + rect.width / 2, rect.y + rect.height / 2, 0);
    }

    public class Floor
    {
        public int number, region, seed;
        public List<Room> rooms = new List<Room>();
        public Room Start => rooms[0];
        public Room Last { get { var l = rooms[0]; foreach (var r in rooms) if (r.depth >= l.depth) l = r; return l; } }
        public readonly List<GameObject> spawned = new List<GameObject>();
    }

    public static readonly string[] FloorNames =
    {
        "", "La Grotte", "Les Galeries moisies", "Les Cavernes inondées", "La Crypte", "Les Catacombes",
        "Les Profondeurs gelées", "Les Veines de cristal", "La Forteresse engloutie", "Le Fleuve de lave", "L'Antre du cristal",
    };

    // thème des zones souterraines (index du catalogue : 0 volcan, 1 lave, 2 aquatique, 3 glace, 4 fongique, 5 cristal, 6 crypte, 7 forteresse, 8 catacombes)
    private static readonly int[] FloorTheme = { 0, 0, 4, 2, 6, 8, 3, 5, 7, 1, 1 };

    // Bestiaire par étage : du plus faible au plus fort
    private static readonly string[][] Families =
    {
        null,
        new[] { "rat", "slime", "goblin" },
        new[] { "goblin", "rat", "slime", "mushroom" },
        new[] { "goblin", "orc", "mushroom" },
        new[] { "orc", "skeleton", "zombie" },
        new[] { "skeleton", "zombie", "gnoll" },
        new[] { "gnoll", "imp", "ghost", "lizard" },
        new[] { "lizard", "ghost", "golem", "imp" },
        new[] { "golem", "lich", "vampire" },
        new[] { "vampire", "lich", "demon" },
        new[] { "demon", "vampire" },
    };
    private static readonly string[][] Traps =
    {
        null, new string[0], new[] { "spikes", "web" }, new[] { "spikes", "bear", "web" }, new[] { "saw", "bear", "flame" },
        new[] { "cage", "flame", "spikes" }, new[] { "pendulum", "rune", "saw" }, new[] { "rune", "ghost_trap", "cage" },
        new[] { "ghost_trap", "pendulum", "rune" }, new[] { "flame", "rune", "pendulum" }, new[] { "ghost_trap", "flame" },
    };
    private static readonly string[][] Deco =
    {
        null, new[] { "torch", "barrel", "bones" }, new[] { "mushrooms_long", "mushrooms_small", "mushroom_slime", "bones" }, new[] { "crystal_blue", "stalagmites", "barrel" },
        new[] { "bones", "skull_pile", "candelabra", "coffin" }, new[] { "skull_pile", "sarcophagus", "candelabra", "hanging" }, new[] { "crystal_blue", "stalagmites_gray", "bones" },
        new[] { "crystal_red", "crystal_yellow", "crystal_green", "stalagmites" }, new[] { "banner", "statue_knight", "torch", "rack" }, new[] { "brazier", "skull_pile", "magma_vent" },
        new[] { "brazier", "demon_altar", "demon_hand", "gargoyle" },
    };

    /// <summary>Zone du donjon (DungeonLayers) qui accueille l'étage.</summary>
    public static int RegionOf(int floor) => floor <= 1 ? 0 : floor >= FloorCount ? 3 : 1 + (floor % 2);

    public static int RoomCount(int floor) => floor == 1 ? 6 : floor == FloorCount ? 5 : Mathf.Min(9, 5 + floor / 2);

    // ─────────────────────────────────────────────
    // GÉNÉRATION DU PLAN
    // ─────────────────────────────────────────────

    /// <summary>Remplit la zone de roche (efface l'étage qui l'occupait) aux couleurs du thème.</summary>
    public static void WipeRegion(int region, int floor)
    {
        var layers = DungeonLayers.Instance;
        var builder = DungeonBuilder.Instance;
        if (layers == null || region <= 0 || region >= layers.layers.Count) return;
        var l = layers.layers[region];
        var b = l.cellBounds;
        var wb = new Bounds(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.width + 2, b.height + 2, 10f));
        foreach (var g in new List<GobelinAI>(GobelinAI.All))
            if (g != null && wb.Contains(new Vector3(g.transform.position.x, g.transform.position.y, 0f))) Object.Destroy(g.gameObject);
        foreach (var po in Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None))
            if (po != null && wb.Contains(new Vector3(po.transform.position.x, po.transform.position.y, 0f))) Object.Destroy(po.gameObject);
        foreach (var s in new List<CrawlerStairs>(CrawlerStairs.All))
            if (s != null && wb.Contains(new Vector3(s.transform.position.x, s.transform.position.y, 0f))) Object.Destroy(s.gameObject);

        // roche partout, puis tuiles du thème (ApplyUnlock creuse aussi la salle d'arrivée)
        var wall = layers.WallTileFor(l.landingCell) ?? builder.stoneTile;
        var cells = new List<Vector3Int>();
        foreach (var p in b.allPositionsWithin) cells.Add(new Vector3Int(p.x, p.y, 0));
        var tiles = new UnityEngine.Tilemaps.TileBase[cells.Count];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = wall;
        builder.stoneTilemap.SetTiles(cells.ToArray(), tiles);
        layers.ApplyUnlock(region, FloorTheme[Mathf.Clamp(floor, 0, FloorTheme.Length - 1)]);
        builder.RefreshStoneCollidersPublic();
    }

    /// <summary>Creuse le plan de l'étage (déterministe pour une graine donnée).</summary>
    public static Floor Generate(int floor, int seed)
    {
        var f = new Floor { number = floor, region = RegionOf(floor), seed = seed };
        var rng = new System.Random(seed * 7919 + floor * 104729);
        var builder = DungeonBuilder.Instance;
        var layers = DungeonLayers.Instance;
        var territory = DungeonTerritory.Instance;
        RectInt bounds = layers != null && layers.layers.Count > f.region ? layers.layers[f.region].cellBounds : new RectInt(-50, -30, 100, 60);
        bounds = new RectInt(bounds.x + 3, bounds.y + 3, bounds.width - 6, bounds.height - 6);
        if (f.region > 0) WipeRegion(f.region, floor);

        var cells = new HashSet<Vector3Int>();
        Vector3Int anchor = f.region == 0 ? territory.CoreCell : layers.layers[f.region].landingCell;
        var first = new RectInt(anchor.x - 2, anchor.y - 1, 5, 4);
        f.rooms.Add(new Room { rect = first, depth = 0 });
        AddRect(cells, first);

        int want = RoomCount(floor);
        var dirs = new[] { Vector2Int.up, Vector2Int.up, Vector2Int.left, Vector2Int.right, Vector2Int.left, Vector2Int.right, Vector2Int.down };
        for (int i = 1; i < want; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 200 && !placed; attempt++)
            {
                // on part surtout de la dernière salle (l'étage s'enfonce), parfois d'une autre (embranchements)
                var parent = attempt < 50 || rng.Next(3) > 0 ? f.rooms[f.rooms.Count - 1] : f.rooms[rng.Next(f.rooms.Count)];
                var prev = parent.rect;
                var d = dirs[rng.Next(dirs.Length)];
                int len = rng.Next(2, 6), w = rng.Next(5, 9), h = rng.Next(4, 7);
                if (floor == FloorCount && i == want - 1) { w = 9; h = 7; }   // la salle du cristal est vaste
                Vector2Int start = d == Vector2Int.up ? new Vector2Int(prev.x + prev.width / 2, prev.yMax)
                                 : d == Vector2Int.down ? new Vector2Int(prev.x + prev.width / 2, prev.y - 1)
                                 : d == Vector2Int.left ? new Vector2Int(prev.x - 1, prev.y + prev.height / 2)
                                 : new Vector2Int(prev.xMax, prev.y + prev.height / 2);
                var corridor = new List<Vector3Int>();
                var p = start;
                for (int k = 0; k < len; k++) { corridor.Add(new Vector3Int(p.x, p.y, 0)); p += d; }
                RectInt room = d == Vector2Int.up ? new RectInt(p.x - w / 2, p.y, w, h)
                             : d == Vector2Int.down ? new RectInt(p.x - w / 2, p.y - h + 1, w, h)
                             : d == Vector2Int.left ? new RectInt(p.x - w + 1, p.y - h / 2, w, h)
                             : new RectInt(p.x, p.y - h / 2, w, h);
                if (!Inside(bounds, room)) continue;
                // tout est creusé dans la roche (murs compris), jamais sur l'herbe au-dehors
                bool solid = true;
                foreach (var c in corridor) if (!builder.stoneTilemap.HasTile(c) && !cells.Contains(c)) { solid = false; break; }
                for (int x = room.xMin - 1; x <= room.xMax && solid; x++)
                    for (int y = room.yMin - 1; y <= room.yMax && solid; y++)
                    {
                        var c = new Vector3Int(x, y, 0);
                        if (!builder.stoneTilemap.HasTile(c) && !cells.Contains(c)) solid = false;
                    }
                if (!solid) continue;
                var grown = new RectInt(room.x - 2, room.y - 2, room.width + 4, room.height + 4);
                bool overlap = false;
                foreach (var r in f.rooms) if (r.rect.Overlaps(grown)) { overlap = true; break; }
                if (overlap) continue;
                foreach (var c in corridor) cells.Add(c);
                AddRect(cells, room);
                f.rooms.Add(new Room { rect = room, depth = parent.depth + 1 });
                placed = true;
            }
            if (!placed) break;
        }
        builder.DigBatch(cells);
        return f;
    }

    private static bool Inside(RectInt b, RectInt r) => r.xMin >= b.xMin && r.yMin >= b.yMin && r.xMax <= b.xMax && r.yMax <= b.yMax;

    private static void AddRect(HashSet<Vector3Int> cells, RectInt r)
    {
        for (int x = r.xMin; x < r.xMax; x++)
            for (int y = r.yMin; y < r.yMax; y++)
                cells.Add(new Vector3Int(x, y, 0));
    }

    // ─────────────────────────────────────────────
    // POPULATION
    // ─────────────────────────────────────────────

    /// <summary>
    /// Les monstres sont taillés pour affronter des groupes d'intrus : face à un héros (et sa petite équipe),
    /// on les adoucit (dégâts ×0,55, PV ×0,8) et ils voient un peu moins loin (on peut les aborder un par un).
    /// </summary>
    public static void Tame(GobelinAI g, float extra = 1f)
    {
        if (g == null) return;
        g.attackDamage *= 0.55f * extra;
        g.maxHealth *= 0.8f * extra;
        g.currentHealth = g.maxHealth;
        g.detectionRange = Mathf.Min(g.detectionRange, 4.5f);
    }

    /// <summary>Prefab d'un monstre : famille + stade (1-3).</summary>
    public static GameObject MonsterPrefab(string family, int stage)
    {
        var cat = DungeonCatalog.Get();
        foreach (var it in cat.items)
        {
            var u = it.prefab != null ? it.prefab.GetComponent<MonsterUnit>() : null;
            if (u == null || u.familyId != family || u.stage != 1) continue;
            var p = it.prefab;
            for (int s = 1; s < stage && u.nextStage != null; s++) { p = u.nextStage; u = p.GetComponent<MonsterUnit>(); }
            return p;
        }
        return null;
    }

    public static string RandomFamily(int floor, System.Random rng)
    {
        var fams = Families[Mathf.Clamp(floor, 1, FloorCount)];
        return fams[rng.Next(fams.Length)];
    }

    public static int StageFor(int floor, System.Random rng)
    {
        double r = rng.NextDouble();
        if (floor <= 3) return 1;
        if (floor <= 5) return r < 0.25 ? 2 : 1;
        if (floor <= 7) return r < 0.6 ? 2 : 1;
        if (floor == 8) return 2;
        if (floor == 9) return r < 0.3 ? 3 : 2;
        return 3;
    }

    private static readonly string[] BossNames = { "", "", "Skrag la Fouine", "Mère Moisissure", "Ossegrince", "", "Hurle-Lune", "Vrask", "", "Comtesse Ysolde", "" };

    /// <summary>Familles de monstres d'un étage (contrats de chasse).</summary>
    public static string[] FamiliesOn(int floor) => Families[Mathf.Clamp(floor, 1, FloorCount)];

    public static GobelinAI SpawnMonster(string family, int stage, Vector2 pos, Floor f)
    {
        var prefab = MonsterPrefab(family, stage);
        if (prefab == null) return null;
        var g = MonsterSpawner.Instance?.SpawnFree(prefab, pos, pos, prefab.GetComponent<GobelinAI>()?.gobelinName ?? prefab.name);
        Tame(g);
        if (g != null) f?.spawned.Add(g.gameObject);
        return g;
    }

    /// <summary>Monstres, pièges, coffres et décor de l'étage (appelé à chaque arrivée : le donjon se repeuple).</summary>
    public static void Populate(Floor f, System.Random rng, System.Func<string, bool> chestLooted)
    {
        var builder = DungeonBuilder.Instance;
        var cat = DungeonCatalog.Get();
        int fl = f.number;
        foreach (var room in f.rooms)
        {
            if (room == f.Start) continue;
            var used = new HashSet<Vector3Int>();
            Vector3Int RandomCell()
            {
                for (int k = 0; k < 30; k++)
                {
                    var c = new Vector3Int(rng.Next(room.rect.xMin + 1, room.rect.xMax - 1), rng.Next(room.rect.yMin + 1, room.rect.yMax - 1), 0);
                    if (used.Add(c)) return c;
                }
                return new Vector3Int(room.rect.x + 1, room.rect.y + 1, 0);
            }
            Vector2 Pos(Vector3Int c) => builder.floorTilemap.GetCellCenterWorld(c);

            bool last = room == f.Last;
            // monstres : de plus en plus nombreux
            int count = Mathf.Min(6, 1 + fl / 2 + rng.Next(0, 3));
            if (fl == 1) count = Mathf.Min(count, 2 + rng.Next(0, 2));
            if (fl == FloorCount) count = last ? 2 : 2 + rng.Next(0, 2);   // l'antre : peu de monstres, mais des formes ultimes
            for (int i = 0; i < count; i++) SpawnMonster(RandomFamily(fl, rng), StageFor(fl, rng), Pos(RandomCell()), f);

            // pièges
            var traps = Traps[Mathf.Clamp(fl, 1, FloorCount)];
            int nTraps = traps.Length == 0 ? 0 : 1 + rng.Next(0, 1 + fl / 4);
            for (int i = 0; i < nTraps; i++)
            {
                var item = cat.Find(traps[rng.Next(traps.Length)]);
                if (item != null) builder.PlaceItemFree(item, RandomCell());
            }
            // coffre (dans un coin), sauf s'il a déjà été pillé
            var chestCell = new Vector3Int(room.rect.xMax - 1, room.rect.yMax - 1, 0);
            if ((rng.Next(2) == 0 || last) && !chestLooted($"{fl}:{chestCell.x}:{chestCell.y}"))
            {
                var chest = cat.Find(fl >= 4 ? "chest_royal" : "chest") ?? cat.Find("chest");
                if (chest != null) builder.PlaceItemFree(chest, chestCell);
            }
            used.Add(chestCell);
            // décor du thème
            var deco = Deco[Mathf.Clamp(fl, 1, FloorCount)];
            foreach (var id in deco)
            {
                var item = cat.Find(id);
                if (item != null && rng.Next(3) > 0) builder.PlaceItemFree(item, RandomCell());
            }
        }
        // gardiens : un boss dans la dernière salle de chaque étage (seigneur orc au 5, archiliche au 8 ;
        // au 10, le seigneur de l'antre est posé devant le cristal par CrawlerMode)
        if (fl >= 2 && fl < FloorCount)
        {
            var room = f.Last;
            string fam = fl == 5 ? "orc" : fl == 8 ? "lich" : Families[fl][Families[fl].Length - 1];
            var g = SpawnMonster(fam, Mathf.Min(3, 1 + (fl + 1) / 3), room.Center + Vector3.down * 0.5f, f);
            if (g != null)
            {
                MonsterMind.For(g).MakeGuardian(fl == 5 ? "Grukk" : fl == 8 ? "Sa'Veth" : BossNames[fl], true);
                g.SetBehavior(GobelinAI.GoblinBehavior.Stay);
                CrawlerBoss.Attach(g, fl);
            }
        }
    }
}
