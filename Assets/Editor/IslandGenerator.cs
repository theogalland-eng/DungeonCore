#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Génère l'île (menu DungeonCore/3. Générer l'île) :
///   récifs noirs et courants → grève de galets et falaises → forêt primordiale et ravins
///   → hautes terres brûlées et évents → échine volcanique (roche creusable du donjon).
/// Le Noyau démarre dans une petite grotte naturelle, sans monstres ni pièges.
/// Crée aussi les zones des étages souterrains et redimensionne le graphe A*.
/// </summary>
public static class IslandGenerator
{
    public static int Seed = 1337;
    const int W = 160, H = 120, Margin = 16;
    const int LayerW = 84, LayerH = 54, LayerGap = 12;

    enum K : byte { Deep, Shallow, Reef, Beach, Cliff, Low, High, Mountain, Ravine, Path, Dock, Cave }

    static int minX, minY, sizeX, sizeY;
    static K[,] map;
    static float[,] forest;
    static System.Random rng;
    static Vector2 o1, o2, o3, o4, o5;

    static K Get(int x, int y) => (x < minX || y < minY || x >= minX + sizeX || y >= minY + sizeY) ? K.Deep : map[x - minX, y - minY];
    static void Set(int x, int y, K k) { if (x >= minX && y >= minY && x < minX + sizeX && y < minY + sizeY) map[x - minX, y - minY] = k; }
    static bool IsSea(K k) => k == K.Deep || k == K.Shallow || k == K.Reef;
    static bool Walkable(K k) => k == K.Beach || k == K.Low || k == K.High || k == K.Path || k == K.Dock || k == K.Cave;

    static float Fbm(float x, float y, int oct)
    {
        float a = 0.5f, f = 1f, s = 0f, n = 0f;
        for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f, y * f); n += a; a *= 0.5f; f *= 2f; }
        return s / n;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    [MenuItem("DungeonCore/3. Générer l'île (remplace la carte !)")]
    public static string Generate()
    {
        if (Application.isPlaying) return "Quittez le mode Play d'abord";
        CatalogBuilder.BuildCatalog();

        rng = new System.Random(Seed);
        Vector2 R() => new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
        o1 = R(); o2 = R(); o3 = R(); o4 = R(); o5 = R();

        minX = -(W / 2 + Margin); minY = -(H / 2 + Margin);
        sizeX = W + 2 * Margin; sizeY = H + 2 * Margin;
        map = new K[sizeX, sizeY];
        forest = new float[sizeX, sizeY];

        BuildHeightmap();
        CarveRavines();
        AddCliffs();
        var (grotto, doorstep, dir) = PlaceGrotto();
        var landing = TracePath(doorstep);
        var spawn = BuildDock(landing);
        landingCell = landing;
        PlanSettlement(landing);

        Paint(grotto, doorstep, spawn, out string paintLog);
        return $"Île générée (graine {Seed}) — {paintLog}";
    }

    // ─────────────────────────────────────────────
    // RELIEF
    // ─────────────────────────────────────────────

    static void BuildHeightmap()
    {
        float hw = W / 2f, hh = H / 2f;
        Vector2[] spine =
        {
            new Vector2(-0.64f * hw, -0.34f * hh), new Vector2(-0.22f * hw, 0.02f * hh),
            new Vector2(0.12f * hw, 0.08f * hh), new Vector2(0.4f * hw, 0.3f * hh), new Vector2(0.62f * hw, 0.5f * hh),
        };
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                int x = ix + minX, y = iy + minY;
                float nx = x / hw, ny = y / hh;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float n1 = Fbm(x * 0.045f + o1.x, y * 0.045f + o1.y, 4);
                float h = 1.02f - d + (n1 - 0.5f) * 0.6f;

                float ds = float.MaxValue;
                var p = new Vector2(x, y);
                for (int i = 0; i < spine.Length - 1; i++) ds = Mathf.Min(ds, SegDist(p, spine[i], spine[i + 1]));
                float n2 = Fbm(x * 0.08f + o2.x, y * 0.08f + o2.y, 3);
                float ridge = Mathf.Clamp01(1f - ds / (0.3f * hw)) * (0.55f + 0.8f * n2);

                forest[ix, iy] = Fbm(x * 0.06f + o3.x, y * 0.06f + o3.y, 3);

                K k;
                if (h < -0.02f) k = K.Deep;
                else if (h < 0.07f) k = Fbm(x * 0.2f + o4.x, y * 0.2f + o4.y, 2) > 0.6f ? K.Reef : K.Shallow;
                else if (h < 0.15f) k = K.Beach;
                else
                {
                    float e = ridge + (h - 0.15f) * 0.3f;
                    k = e > 0.7f ? K.Mountain : e > 0.4f ? K.High : K.Low;
                }
                map[ix, iy] = k;
            }
    }

    /// <summary>Ravins : failles sinueuses creusées par des "vers" aléatoires dans la forêt.</summary>
    static void CarveRavines()
    {
        int made = 0;
        for (int tries = 0; tries < 400 && made < 11; tries++)
        {
            int x = rng.Next(minX, minX + sizeX), y = rng.Next(minY, minY + sizeY);
            if (Get(x, y) != K.Low || forest[x - minX, y - minY] < 0.45f) continue;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            int len = rng.Next(14, 32);
            Vector2 p = new Vector2(x, y);
            for (int i = 0; i < len; i++)
            {
                ang += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                p += new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
                if (Get(cx, cy) != K.Low) break;
                Set(cx, cy, K.Ravine);
                if (i > 3 && i < len - 3 && rng.NextDouble() < 0.55)
                {
                    int sx = cx + (Mathf.Abs(Mathf.Cos(ang)) > 0.7f ? 0 : 1), sy = cy + (Mathf.Abs(Mathf.Cos(ang)) > 0.7f ? 1 : 0);
                    if (Get(sx, sy) == K.Low) Set(sx, sy, K.Ravine);
                }
            }
            made++;
        }
    }

    static void AddCliffs()
    {
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY - 1; iy++)
            {
                if (map[ix, iy] != K.Beach) continue;
                K above = map[ix, iy + 1];
                if ((above == K.Low || above == K.High) && Fbm((ix + minX) * 0.1f + o1.y, (iy + minY) * 0.1f, 2) > 0.42f)
                    map[ix, iy] = K.Cliff;
            }
    }

    // ─────────────────────────────────────────────
    // GROTTE DE DÉPART
    // ─────────────────────────────────────────────

    static (Vector3Int grotto, Vector3Int doorstep, Vector3Int dir) PlaceGrotto()
    {
        Vector2 target = new Vector2(-6, -H * 0.12f);
        float best = float.MaxValue;
        Vector3Int bestG = Vector3Int.zero, bestN = Vector3Int.zero, bestD = Vector3Int.right;
        var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                if (map[ix, iy] != K.Mountain) continue;
                int x = ix + minX, y = iy + minY;
                foreach (var d in dirs)
                {
                    var n = new Vector3Int(x, y, 0) - d;
                    K nk = Get(n.x, n.y);
                    if (nk != K.Low && nk != K.High) continue;
                    var g = new Vector3Int(x, y, 0) + d * 5;
                    bool ok = true;
                    for (int ax = -3; ax <= 3 && ok; ax++)
                        for (int ay = -3; ay <= 3 && ok; ay++)
                            if (Get(g.x + ax, g.y + ay) != K.Mountain) ok = false;
                    for (int k = 0; k <= 4 && ok; k++)
                    {
                        var t = new Vector3Int(x, y, 0) + d * k;
                        if (Get(t.x, t.y) != K.Mountain) ok = false;
                    }
                    if (!ok) continue;
                    float score = Vector2.Distance(new Vector2(x, y), target) + (d == Vector3Int.up ? 0f : 6f);
                    if (score < best) { best = score; bestG = g; bestN = n; bestD = d; }
                }
            }

        for (int ax = -1; ax <= 1; ax++)
            for (int ay = -1; ay <= 1; ay++)
                Set(bestG.x + ax, bestG.y + ay, K.Cave);
        for (int k = 0; k <= 4; k++)
        {
            var t = bestN + bestD * (k + 1);
            Set(t.x, t.y, K.Cave);
        }
        return (bestG, bestN, bestD);
    }

    // ─────────────────────────────────────────────
    // SENTIER & PONTON
    // ─────────────────────────────────────────────

    static Vector3Int TracePath(Vector3Int start)
    {
        var dist = new Dictionary<Vector3Int, float>();
        var steps = new Dictionary<Vector3Int, int>();
        var parent = new Dictionary<Vector3Int, Vector3Int>();
        var open = new SortedSet<(float, int, int, int)>();
        dist[start] = 0f; steps[start] = 0;
        open.Add((0f, start.x, start.y, 0));
        var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        int serial = 1;
        while (open.Count > 0)
        {
            var top = open.Min; open.Remove(top);
            var c = new Vector3Int(top.Item2, top.Item3, 0);
            if (top.Item1 > dist[c]) continue;
            foreach (var d in dirs)
            {
                var n = c + d;
                K nk = Get(n.x, n.y);
                if (nk != K.Low && nk != K.High && nk != K.Beach) continue;
                // coût bruité → sentier sinueux ; la forêt dense est évitée
                float cost = 1f + 4f * Fbm(n.x * 0.12f + o2.y, n.y * 0.12f + o1.x, 2) + (nk == K.Beach ? 2f : 0f);
                float nd = dist[c] + cost;
                if (dist.TryGetValue(n, out float old) && old <= nd) continue;
                dist[n] = nd; steps[n] = steps[c] + 1; parent[n] = c;
                open.Add((nd, n.x, n.y, serial++));
            }
        }

        Vector3Int best = start;
        int bestScore = int.MaxValue;
        foreach (var kv in steps)
        {
            var c = kv.Key;
            if (Get(c.x, c.y) != K.Beach) continue;
            if (!dirs.Any(d => IsSea(Get(c.x + d.x, c.y + d.y)))) continue;
            int score = Mathf.Abs(kv.Value - 60) + (c.y > 0 ? 30 : 0);
            if (score < bestScore) { bestScore = score; best = c; }
        }

        var cur = best;
        while (parent.ContainsKey(cur))
        {
            if (Get(cur.x, cur.y) != K.Beach) Set(cur.x, cur.y, K.Path);
            var side = cur + ((cur.x + cur.y) % 2 == 0 ? Vector3Int.right : Vector3Int.up);
            K sk = Get(side.x, side.y);
            if (sk == K.Low || sk == K.High) Set(side.x, side.y, K.Path);
            cur = parent[cur];
        }
        Set(start.x, start.y, K.Path);
        return best;
    }

    static Vector3Int landingCell, dockDir;
    static readonly List<Vector3Int> dockCells = new List<Vector3Int>();
    static readonly List<Vector3Int> pierExt = new List<Vector3Int>();
    static readonly List<Vector3> moorings = new List<Vector3>();
    static readonly List<SettlementPlot> plots = new List<SettlementPlot>();

    /// <summary>Ponton de 2 cases de large vers le large ; le port (stade 2) le prolonge.</summary>
    static Vector3Int BuildDock(Vector3Int landing)
    {
        dockCells.Clear(); pierExt.Clear(); moorings.Clear();
        var dirs = new[] { Vector3Int.down, Vector3Int.left, Vector3Int.right, Vector3Int.up };
        Vector3Int dir = Vector3Int.down;
        foreach (var d in dirs)
            if (IsSea(Get(landing.x + d.x, landing.y + d.y))) { dir = d; break; }
        dockDir = dir;
        var side = dir.x == 0 ? Vector3Int.right : Vector3Int.up;   // seconde rangée de planches
        var c = landing;
        var last = c;
        for (int i = 0; i < 7; i++)
        {
            c += dir;
            if (!IsSea(Get(c.x, c.y)) && i > 0) break;
            foreach (var cc in new[] { c, c + side })
            {
                if (IsSea(Get(cc.x, cc.y)) || Get(cc.x, cc.y) == K.Beach) { Set(cc.x, cc.y, K.Dock); dockCells.Add(cc); }
            }
            last = c;
        }
        // Extension du port : 6 cases de plus et une plateforme en T
        var e = last;
        for (int i = 0; i < 6; i++)
        {
            e += dir;
            foreach (var cc in new[] { e, e + side })
                if (IsSea(Get(cc.x, cc.y))) pierExt.Add(cc);
        }
        var perp = side;
        for (int k = -3; k <= 4; k++)
        {
            var cc = e + perp * k;
            if (IsSea(Get(cc.x, cc.y)) && !pierExt.Contains(cc)) pierExt.Add(cc);
        }
        // Barques amarrées le long de l'extension (côté gauche)
        var mid = last + dir * 3;
        for (int k = 0; k < 3; k++)
        {
            var mc = mid + dir * (k * 2) - side * 1;
            if (IsSea(Get(mc.x, mc.y))) moorings.Add(new Vector3(mc.x + 0.5f - side.x * 0.3f, mc.y + 0.5f - side.y * 0.3f, 0f));
        }
        return last;
    }

    // ─────────────────────────────────────────────
    // COLONIE : EMPLACEMENTS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Place les bâtiments de la colonie (dans l'ordre des stades) sur la terre ferme proche du ponton,
    /// hors du sentier et loin de l'entrée du donjon. Les arbres sur les emplacements seront abattus.
    /// </summary>
    static void PlanSettlement(Vector3Int landing)
    {
        plots.Clear();
        var reserved = new HashSet<Vector3Int>();
        var caves = new List<Vector3Int>();
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
                if (map[ix, iy] == K.Cave) caves.Add(new Vector3Int(ix + minX, iy + minY, 0));

        bool Buildable(int x, int y)
        {
            K k = Get(x, y);
            if (k != K.Low && k != K.Beach && k != K.High) return false;
            if (reserved.Contains(new Vector3Int(x, y, 0))) return false;
            if (Mathf.Abs(x - landing.x) <= 2 && Mathf.Abs(y - landing.y) <= 2) return false;
            return true;
        }
        bool FarFromPath(int x, int y)
        {
            for (int ax = -1; ax <= 1; ax++)
                for (int ay = -1; ay <= 1; ay++)
                {
                    K k = Get(x + ax, y + ay);
                    if (k == K.Path || k == K.Dock || k == K.Cave || k == K.Ravine || k == K.Mountain || IsSea(k)) return false;
                }
            return true;
        }

        foreach (var def in SettlementPlan.Buildings)
        {
            if (def.w <= 0) continue;
            float best = float.MaxValue;
            RectInt bestRect = default;
            for (int dx = -30; dx <= 30; dx++)
                for (int dy = -6; dy <= 34; dy++)
                {
                    int x0 = landing.x + dx, y0 = landing.y + dy;
                    bool ok = true;
                    for (int x = x0 - 1; x <= x0 + def.w && ok; x++)
                        for (int y = y0 - 1; y <= y0 + def.h && ok; y++)
                            if (!Buildable(x, y) || !FarFromPath(x, y)) ok = false;
                    if (!ok) continue;
                    var center = new Vector2(x0 + def.w * 0.5f, y0 + def.h * 0.5f);
                    float nearCave = float.MaxValue;
                    foreach (var cv in caves) nearCave = Mathf.Min(nearCave, Vector2.Distance(center, new Vector2(cv.x, cv.y)));
                    if (nearCave < 12f) continue;
                    // près du ponton, les premiers stades d'abord ; un peu de hasard pour un village organique
                    float score = Vector2.Distance(center, new Vector2(landing.x, landing.y)) + (float)rng.NextDouble() * 3f;
                    if (score < best) { best = score; bestRect = new RectInt(x0, y0, def.w, def.h); }
                }
            if (best == float.MaxValue) continue;
            plots.Add(new SettlementPlot { buildingId = def.id, rect = bestRect });
            for (int x = bestRect.x - 1; x <= bestRect.xMax; x++)
                for (int y = bestRect.y - 1; y <= bestRect.yMax; y++)
                    reserved.Add(new Vector3Int(x, y, 0));
        }
    }

    // ─────────────────────────────────────────────
    // PEINTURE DES TILEMAPS & SCÈNE
    // ─────────────────────────────────────────────

    static Tilemap FindTilemap(string name)
    {
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (tm.name == name) return tm;
        return null;
    }

    static Tilemap CreateTilemap(string name, int order)
    {
        var grid = Object.FindFirstObjectByType<Grid>();
        var go = new GameObject(name);
        go.transform.SetParent(grid.transform, false);
        var tm = go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        r.sortingOrder = order;
        return tm;
    }

    /// <summary>
    /// Collision case par case (TilemapCollider2D seul). Le CompositeCollider2D ne générait aucune forme
    /// (0 chemin) : l'eau et la roche n'avaient en réalité aucune collision, ni pour la physique ni pour A*.
    /// </summary>
    static void EnsureComposite(Tilemap tm)
    {
        var tc = tm.GetComponent<TilemapCollider2D>();
        if (tc == null) tc = tm.gameObject.AddComponent<TilemapCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.None;
        var comp = tm.GetComponent<CompositeCollider2D>();
        if (comp != null) Object.DestroyImmediate(comp);
        var rb = tm.GetComponent<Rigidbody2D>();
        if (rb == null) rb = tm.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
    }

    static void Put(Tilemap tm, Vector3Int c, TileBase t, Color col)
    {
        tm.SetTile(c, t);
        tm.SetTileFlags(c, TileFlags.None);
        tm.SetColor(c, col);
    }

    static Color Jit(Color c, float amount)
    {
        float j = 1f + ((float)rng.NextDouble() - 0.5f) * amount;
        return new Color(c.r * j, c.g * j, c.b * j, c.a);
    }

    static float[,] coastDist;

    /// <summary>Distance à la terre (cases, plafonnée) pour un dégradé de l'eau turquoise vers le bleu profond.</summary>
    static void ComputeCoastDistance()
    {
        coastDist = new float[sizeX, sizeY];
        var q = new Queue<Vector2Int>();
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                bool water = IsSea(map[ix, iy]) || map[ix, iy] == K.Dock;
                coastDist[ix, iy] = water ? 99f : 0f;
                if (!water) q.Enqueue(new Vector2Int(ix, iy));
            }
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (n.x < 0 || n.y < 0 || n.x >= sizeX || n.y >= sizeY) continue;
                if (coastDist[n.x, n.y] <= coastDist[c.x, c.y] + 1f) continue;
                coastDist[n.x, n.y] = coastDist[c.x, c.y] + 1f;
                q.Enqueue(n);
            }
        }
    }

    static Color SeaColor(int ix, int iy)
    {
        float d = coastDist[ix, iy];
        // bruit doux pour casser les bandes régulières
        float n = (Mathf.PerlinNoise((ix + minX) * 0.15f + o5.x, (iy + minY) * 0.15f + o5.y) - 0.5f) * 2.5f;
        float t = Mathf.Clamp01((d + n - 1f) / 9f);
        var shallow = new Color(0.52f, 0.78f, 0.86f);
        var mid = new Color(0.38f, 0.56f, 0.78f);
        var deep = new Color(0.24f, 0.32f, 0.55f);
        var col = t < 0.4f ? Color.Lerp(shallow, mid, t / 0.4f) : Color.Lerp(mid, deep, (t - 0.4f) / 0.6f);
        return Jit(col, 0.03f);
    }

    static void Paint(Vector3Int grotto, Vector3Int doorstep, Vector3Int spawnCell, out string log)
    {
        ComputeCoastDistance();
        var sol = FindTilemap("Sol");
        var murs = FindTilemap("murs");
        var trees = FindTilemap("trees");
        var trunks = FindTilemap("trees_trunk");
        var objects = FindTilemap("objects");
        var placed = FindTilemap("PlacedWall");
        var blocked = FindTilemap("NoPathfinding");
        var decor = FindTilemap("Decor") ?? CreateTilemap("Decor", 1);

        foreach (var tm in new[] { sol, murs, trees, trunks, objects, placed, blocked, decor }) tm.ClearAllTiles();
        EnsureComposite(blocked);
        EnsureComposite(murs);
        EnsureComposite(trunks);

        var island = CatalogBuilder.Sprites(CatalogBuilder.ArtPath);
        var snow = CatalogBuilder.Sprites("Assets/imports/snow-expansion.png");
        Tile I(string n, Tile.ColliderType ct = Tile.ColliderType.None) => CatalogBuilder.TileFor(island["island_" + n], ct);
        Tile S(int i, Tile.ColliderType ct = Tile.ColliderType.None) => CatalogBuilder.TileFor(snow[$"snow-expansion_{i}"], ct);

        var builder = Object.FindFirstObjectByType<DungeonBuilder>();
        TileBase caveFloor = DungeonCatalog.Get() != null && DungeonCatalog.Get().themes.Count > 0 && DungeonCatalog.Get().themes[0].floorTile != null
            ? DungeonCatalog.Get().themes[0].floorTile : builder.floorTile;

        TileBase sea = (TileBase)ArtImporter.SeaBlocked() ?? S(176, Tile.ColliderType.Grid);
        var sart = SettlementArt.Get();
        Tile reef = I("reef", Tile.ColliderType.Grid);
        Tile abyss = I("abyss", Tile.ColliderType.Grid);
        Tile cliff = S(155, Tile.ColliderType.Grid);
        Tile stoneA = S(195, Tile.ColliderType.Grid), stoneB = S(194, Tile.ColliderType.Grid);
        Tile[] grass = { S(0), S(0), S(0), S(1), S(2), S(3), S(22), S(23) };
        Tile[] gravel = { I("gravel_a"), I("gravel_b") };
        Tile[] volcanic = { I("volcanic_a"), I("volcanic_b"), I("volcanic_a"), I("scree") };
        Tile dirt = S(184), plank = S(82), vent = I("vent");
        Tile trunk = S(174, Tile.ColliderType.Grid);   // collision invisible des troncs
        var cat = DungeonCatalog.Get();
        TileBase wallTile = cat.themes.Count > 0 && cat.themes[0].wallTile != null ? cat.themes[0].wallTile : stoneA;
        var wallCells = new List<Vector3Int>();
        Tile[] mushrooms = { S(20), S(21), S(42), S(43) };
        Tile[] boulders = { S(16), S(17), S(38), S(39) };
        Tile tuft = S(137);

        var volcanicTint = new Color(0.36f, 0.33f, 0.33f);
        int trees0 = 0, vents0 = 0;
        var ventCells = new List<Vector3Int>();
        var occupied = new HashSet<Vector3Int>();

        // Distance à la mer (pour la teinte de la forêt / densité)
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                int x = ix + minX, y = iy + minY;
                var c = new Vector3Int(x, y, 0);
                K k = map[ix, iy];
                switch (k)
                {
                    case K.Deep:
                    case K.Shallow:
                        Put(blocked, c, sea, SeaColor(ix, iy));
                        break;
                    case K.Reef:
                        Put(blocked, c, reef, Color.white);
                        break;
                    case K.Beach:
                        Put(sol, c, gravel[rng.Next(2)], Jit(Color.white, 0.12f));
                        if (rng.NextDouble() < 0.025) Put(decor, c, boulders[rng.Next(4)], new Color(0.35f, 0.33f, 0.36f));
                        break;
                    case K.Cliff:
                        Put(sol, c, gravel[0], Color.white);
                        Put(blocked, c, cliff, new Color(0.55f, 0.52f, 0.58f));
                        break;
                    case K.Low:
                    {
                        float f = forest[ix, iy];
                        float dark = Mathf.Lerp(1f, 0.52f, Mathf.InverseLerp(0.3f, 0.7f, f));
                        Put(sol, c, grass[rng.Next(grass.Length)], Jit(new Color(dark, dark * 1.02f, dark * 0.95f), 0.06f));
                        break;
                    }
                    case K.High:
                        Put(sol, c, volcanic[rng.Next(volcanic.Length)], Jit(Color.white, 0.1f));
                        if (rng.NextDouble() < 0.05) Put(decor, c, boulders[rng.Next(4)], new Color(0.3f, 0.28f, 0.3f));
                        break;
                    case K.Mountain:
                        Put(sol, c, volcanic[0], Color.white);
                        wallCells.Add(c);
                        break;
                    case K.Ravine:
                        Put(blocked, c, abyss, Color.white);
                        break;
                    case K.Path:
                        Put(sol, c, gravel[rng.Next(2)], Jit(new Color(0.78f, 0.64f, 0.5f), 0.08f));
                        break;
                    case K.Dock:
                        if (sart != null && sart.pierLeft != null && sart.seaFloor != null)
                        {
                            // eau (sans collision) + planches du ponton par-dessus
                            Put(sol, c, sart.seaFloor, Jit(new Color(0.5f, 0.64f, 0.82f), 0.05f));
                            bool first = dockDir.x == 0 ? !dockCells.Contains(c + Vector3Int.left) : !dockCells.Contains(c + Vector3Int.down);
                            Put(decor, c, dockDir.x == 0 ? (first ? sart.pierLeft : sart.pierRight) : sart.pierLeft, Color.white);
                        }
                        else Put(sol, c, plank, Color.white);
                        break;
                    case K.Cave:
                        sol.SetTile(c, caveFloor);
                        sol.SetTileFlags(c, TileFlags.None);
                        sol.SetColor(c, Color.white);
                        break;
                }
            }

        // Roche de la montagne : murs auto-connectés (dessus, arêtes, faces)
        SetWalls(murs, wallCells, wallTile);

        // Forêt primordiale, palmiers de la grève, arbres calcinés des hautes terres
        var env = GameObject.Find("Environment") ?? new GameObject("Environment");
        for (int i = env.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(env.transform.GetChild(i).gameObject);
        trees0 = PlaceVegetation(env.transform, trunks, trunk, occupied);
        trunks.GetComponent<TilemapRenderer>().enabled = false;
        SetupDepthSorting();

        // Sous-bois : champignons et herbes
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                if (map[ix, iy] != K.Low) continue;
                var c = new Vector3Int(ix + minX, iy + minY, 0);
                if (occupied.Contains(c)) continue;
                double r = rng.NextDouble();
                if (r < 0.018) Put(decor, c, mushrooms[rng.Next(4)], Color.white);
                else if (r < 0.06) Put(decor, c, tuft, new Color(0.8f, 0.85f, 0.75f));
            }

        // Évents géothermiques dans les hautes terres
        for (int tries = 0; tries < 4000 && ventCells.Count < 14; tries++)
        {
            int x = rng.Next(minX, minX + sizeX), y = rng.Next(minY, minY + sizeY);
            if (Get(x, y) != K.High) continue;
            var c = new Vector3Int(x, y, 0);
            if (ventCells.Any(v => Vector3Int.Distance(v, c) < 10)) continue;
            if (occupied.Contains(c) || occupied.Contains(c + Vector3Int.up) || occupied.Contains(c + Vector3Int.down)) continue;
            bool nearPath = false;
            for (int ax = -5; ax <= 5 && !nearPath; ax++)
                for (int ay = -5; ay <= 5 && !nearPath; ay++)
                    if (Get(x + ax, y + ay) == K.Path || Get(x + ax, y + ay) == K.Cave) nearPath = true;
            if (nearPath) continue;
            ventCells.Add(c);
            Put(sol, c, vent, Color.white);
            decor.SetTile(c, null);
            var go = new GameObject("Évent");
            go.transform.SetParent(env.transform, false);
            go.transform.position = sol.GetCellCenterWorld(c);
            go.AddComponent<SteamVent>();
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(1f, 0.5f, 0.2f);
            l.intensity = 0.7f;
            l.pointLightOuterRadius = 2.2f;
            l.pointLightInnerRadius = 0.2f;
            vents0++;
        }

        // ── Étages souterrains ──
        var layersGo = GameObject.Find("DungeonLayers") ?? new GameObject("DungeonLayers");
        var layers = layersGo.GetComponent<DungeonLayers>() ?? layersGo.AddComponent<DungeonLayers>();
        layers.layers.Clear();
        layers.layers.Add(new DungeonLayerDef { name = "Surface", cellBounds = new RectInt(minX, minY, sizeX, sizeY), unlocked = true, connected = true, themeIndex = 0 });
        string[] names = { "Profondeurs", "Abîmes", "Cœur du volcan" };
        int[] lv = { 4, 9, 14 };
        int[] cost = { 250, 500, 800 };
        int top = minY - LayerGap;
        var undergroundCells = new List<Vector3Int>();
        for (int i = 0; i < 3; i++)
        {
            var rect = new RectInt(-LayerW / 2, top - LayerH, LayerW, LayerH);
            layers.layers.Add(new DungeonLayerDef
            {
                name = names[i], cellBounds = rect, unlockLevel = lv[i], unlockCost = cost[i],
                landingCell = new Vector3Int(rect.x + rect.width / 2, rect.y + rect.height / 2, 0),
            });
            foreach (var p in rect.allPositionsWithin) undergroundCells.Add(new Vector3Int(p.x, p.y, 0));
            top -= LayerH + LayerGap;
        }
        SetWalls(murs, undergroundCells, wallTile);
        EditorUtility.SetDirty(layers);

        // ── Objets de la scène ──
        RemoveTestObjects();
        var core = Object.FindFirstObjectByType<DungeonCore>();
        core.transform.position = sol.GetCellCenterWorld(grotto);
        // Équilibrage : un cœur qui survit à un premier groupe de villageois sans défense
        core.maxHealth = 800f;
        core.regenPerSecond = 3f;
        EditorUtility.SetDirty(core);
        if (core.GetComponentInChildren<Light2D>() == null)
        {
            var lgo = new GameObject("Lueur");
            lgo.transform.SetParent(core.transform, false);
            var l = lgo.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(0.45f, 0.75f, 1f);
            l.intensity = 1.1f;
            l.pointLightOuterRadius = 4.5f;
            l.pointLightInnerRadius = 0.4f;
        }

        var spawner = Object.FindFirstObjectByType<AdventurerSpawner>();
        spawner.transform.position = sol.GetCellCenterWorld(spawnCell);
        spawner.spawnPoint = null;
        spawner.groupSpawnRadius = 1f;
        EditorUtility.SetDirty(spawner);

        // Équilibrage : plafonds du Noyau (l'entretien journalier limite désormais l'armée)
        var pl = Object.FindFirstObjectByType<PlayerLevel>();
        if (pl != null)
        {
            pl.baseMonsterLimit = 5; pl.monstersPerLevel = 2;
            pl.baseTrapLimit = 4; pl.trapsPerLevel = 2;
            EditorUtility.SetDirty(pl);
        }

        var siteGo = GameObject.Find("SettlementSite") ?? new GameObject("SettlementSite");
        var site = siteGo.GetComponent<SettlementSite>() ?? siteGo.AddComponent<SettlementSite>();
        site.landing = landingCell;
        site.dockCells = new List<Vector3Int>(dockCells);
        site.pierExtension = new List<Vector3Int>(pierExt);
        site.moorings = new List<Vector3>(moorings);
        site.shipMooring = Vector3.zero;
        site.plots = new List<SettlementPlot>(plots);
        EditorUtility.SetDirty(site);

        var gate = Object.FindFirstObjectByType<DungeonBoundary>();
        if (gate != null) gate.transform.position = sol.GetCellCenterWorld(doorstep);

        var cam = Camera.main;
        cam.transform.position = new Vector3(core.transform.position.x, core.transform.position.y, -10f);
        cam.orthographicSize = 8f;
        cam.backgroundColor = new Color(0.02f, 0.025f, 0.05f);
        EditorUtility.SetDirty(cam);

        ConfigurePathfinding();
        murs.CompressBounds();
        sol.CompressBounds();

        EditorSceneManager.MarkSceneDirty(core.gameObject.scene);
        EditorSceneManager.SaveScene(core.gameObject.scene);
        log = $"{trees0} arbres, {vents0} évents, grotte {grotto}, ponton {spawnCell}, colonie : {plots.Count} emplacements";
    }

    static void SetWalls(Tilemap tm, List<Vector3Int> cells, TileBase wall)
    {
        var tiles = new TileBase[cells.Count];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = wall;
        tm.SetTiles(cells.ToArray(), tiles);
        foreach (var c in cells)
        {
            tm.SetTileFlags(c, TileFlags.None);
            tm.SetColor(c, Color.white);
        }
    }

    // ─────────────────────────────────────────────
    // VÉGÉTATION (sprites triés en profondeur)
    // ─────────────────────────────────────────────

    class TreeKind
    {
        public string[] names;
        public float spacing;          // distance mini entre troncs (cases)
        public int halfWidth, height;  // emprise du feuillage (cases) : x ± halfWidth, y de -1 à height
        public TreeKind(float spacing, int halfWidth, int height, params string[] names)
        { this.names = names; this.spacing = spacing; this.halfWidth = halfWidth; this.height = height; }
    }

    static readonly TreeKind BigTree = new TreeKind(2.3f, 2, 5, "Tree1", "Tree1", "Tree1", "Moss_tree1", "Moss_tree1", "Moss_tree1", "Tree1", "Moss_tree1", "Tree1", "Moss_tree1", "Tree1", "Autumn_tree1");
    static readonly TreeKind MidTree = new TreeKind(1.9f, 1, 4, "Tree2", "Moss_tree2", "Tree2", "Moss_tree2", "Tree2", "Moss_tree2", "Tree2", "Moss_tree2", "Tree2", "Moss_tree2", "Fruit_tree2", "Autumn_tree2");
    static readonly TreeKind SmallTree = new TreeKind(1.6f, 1, 3, "Tree3", "Moss_tree3", "Tree3", "Moss_tree3", "Tree3", "Moss_tree3", "Tree3", "Moss_tree3", "Fruit_tree3", "Flower_tree2-1");
    static readonly TreeKind Palm = new TreeKind(3.2f, 1, 4, "Palm_tree1_2", "Palm_tree2_2", "Palm_tree1_3", "Palm_tree2_3");
    static readonly TreeKind Burned = new TreeKind(3f, 1, 3, "Burned_tree2", "Burned_tree3", "Broken_tree3", "Broken_tree4", "Broken_tree2");

    /// <summary>
    /// Arbres en sprites transparents (pivot au pied du tronc), triés par Y avec les personnages :
    /// un arbre plus au sud passe devant ceux du nord, sans carré d'herbe qui dépasse.
    /// Les troncs reçoivent une collision invisible (tilemap trees_trunk, calque obstacles).
    /// </summary>
    static int PlaceVegetation(Transform env, Tilemap trunks, TileBase trunkTile, HashSet<Vector3Int> occupied)
    {
        var forestRoot = new GameObject("Forêt").transform;
        forestRoot.SetParent(env, false);
        var trunksList = new List<(Vector2 pos, float spacing)>();
        var cells = new List<Vector3Int>();
        for (int ix = 2; ix < sizeX - 2; ix++)
            for (int iy = 2; iy < sizeY - 2; iy++)
                cells.Add(new Vector3Int(ix + minX, iy + minY, 0));
        // ordre aléatoire (stable) → répartition naturelle
        for (int i = cells.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (cells[i], cells[j]) = (cells[j], cells[i]); }

        int count = 0;
        foreach (var c in cells)
        {
            K k = Get(c.x, c.y);
            float f = forest[c.x - minX, c.y - minY];
            TreeKind kind = null;
            if (k == K.Low)
            {
                if (f < 0.26f || rng.NextDouble() > 0.4 + f * 1.2f) continue;
                kind = f > 0.52f ? (rng.NextDouble() < 0.7 ? BigTree : MidTree)
                     : f > 0.4f ? (rng.NextDouble() < 0.6 ? MidTree : SmallTree)
                     : (rng.NextDouble() < 0.3 ? MidTree : SmallTree);
            }
            else if (k == K.Beach)
            {
                if (rng.NextDouble() > 0.06) continue;
                bool nearLand = false;
                foreach (var d in new[] { Vector3Int.up, Vector3Int.left, Vector3Int.right })
                    if (Get(c.x + d.x * 2, c.y + d.y * 2) == K.Low) nearLand = true;
                if (!nearLand) continue;
                kind = Palm;
            }
            else if (k == K.High)
            {
                if (rng.NextDouble() > 0.03) continue;
                kind = Burned;
            }
            else continue;

            if (!TreeFits(c, kind, k)) continue;
            var pos = new Vector2(c.x + 0.5f, c.y + 0.5f);
            bool tooClose = false;
            foreach (var t in trunksList)
            {
                float need = (t.spacing + kind.spacing) * 0.5f;
                if ((t.pos - pos).sqrMagnitude < need * need) { tooClose = true; break; }
            }
            if (tooClose) continue;

            string name = kind.names[rng.Next(kind.names.Length)];
            var sprite = WallBuilder.Tree(name);
            if (sprite == null) continue;
            var go = new GameObject(name);
            go.transform.SetParent(forestRoot, false);
            // léger décalage dans la case pour casser la grille
            go.transform.position = new Vector3(c.x + 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.4f, c.y + 0.3f + (float)rng.NextDouble() * 0.2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            sr.flipX = rng.NextDouble() < 0.5;
            // pénombre de la forêt profonde
            float shade = k == K.Low ? Mathf.Lerp(0.95f, 0.58f, Mathf.InverseLerp(0.3f, 0.7f, f)) : 0.92f;
            sr.color = Jit(new Color(shade, shade, shade * 0.97f), 0.06f);

            trunks.SetTile(c, trunkTile);
            occupied.Add(c);
            trunksList.Add((pos, kind.spacing));
            count++;
        }

        // Sous-bois : fougères et buissons (sans collision)
        string[] bushes = { "Fern1_2", "Fern2_2", "Fern1_3", "Fern2_3", "Bush_simple1_2", "Bush_simple2_2", "Bush_simple1_3", "Bush_simple2_3" };
        string[] flowers = { "Bush_red_flowers2", "Bush_pink_flowers2", "Bush_blue_flowers2", "Bush_orange_flowers3" };
        int bushCount = 0;
        foreach (var c in cells)
        {
            if (Get(c.x, c.y) != K.Low || occupied.Contains(c)) continue;
            float f = forest[c.x - minX, c.y - minY];
            if (rng.NextDouble() > (f > 0.35f ? 0.07 : 0.025)) continue;
            if (NearKind(c, 1, K.Path, K.Cave, K.Dock)) continue;
            string name = rng.NextDouble() < 0.12 ? flowers[rng.Next(flowers.Length)] : bushes[rng.Next(bushes.Length)];
            var sprite = WallBuilder.Bush(name);
            if (sprite == null) continue;
            var go = new GameObject(name);
            go.transform.SetParent(forestRoot, false);
            go.transform.position = new Vector3(c.x + 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.6f, c.y + 0.2f + (float)rng.NextDouble() * 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            sr.flipX = rng.NextDouble() < 0.5;
            float shade = Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(0.35f, 0.75f, f));
            sr.color = new Color(shade, shade, shade);
            occupied.Add(c);
            bushCount++;
        }
        return count;
    }

    static bool NearKind(Vector3Int c, int r, params K[] kinds)
    {
        for (int ax = -r; ax <= r; ax++)
            for (int ay = -r; ay <= r; ay++)
                if (kinds.Contains(Get(c.x + ax, c.y + ay))) return true;
        return false;
    }

    /// <summary>Le tronc est sur un sol libre et le feuillage ne cache ni le sentier, ni la grotte, ni le ponton.</summary>
    static bool TreeFits(Vector3Int c, TreeKind kind, K ground)
    {
        // tronc et voisins immédiats : même type de sol (pas au bord d'un ravin ou d'une falaise)
        for (int ax = -1; ax <= 1; ax++)
            for (int ay = -1; ay <= 1; ay++)
            {
                K n = Get(c.x + ax, c.y + ay);
                if (n != ground && !(ground == K.Beach && n == K.Low)) return false;
            }
        // feuillage : rien d'important dessous (on doit voir les intrus sur le sentier)
        for (int ax = -kind.halfWidth - 1; ax <= kind.halfWidth + 1; ax++)
            for (int ay = -2; ay <= kind.height; ay++)
            {
                K n = Get(c.x + ax, c.y + ay);
                if (n == K.Path || n == K.Cave || n == K.Dock) return false;
            }
        return true;
    }

    /// <summary>Tri des sprites par Y (arbres, personnages, décors) dans le Renderer 2D.</summary>
    static void SetupDepthSorting()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Renderer2DData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(data);
            var mode = so.FindProperty("m_TransparencySortMode");
            var axis = so.FindProperty("m_TransparencySortAxis");
            if (mode == null || axis == null) continue;
            mode.intValue = (int)TransparencySortMode.CustomAxis;
            axis.vector3Value = new Vector3(0f, 1f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }

    static void RemoveTestObjects()
    {
        string[] names = { "gobelin", "gobelin (1)", "gobelin (2)", "GobelinAnchor", "GobelinAnchor (1)", "TrapSpinningBlade", "Slime1", "Aventurier_lv1" };
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            if (names.Contains(root.name)) Object.DestroyImmediate(root);
    }

    static void ConfigurePathfinding()
    {
        var ap = Object.FindFirstObjectByType<AstarPath>();
        if (ap == null) return;
        if (ap.data.graphs == null || ap.data.graphs.Length == 0) ap.data.DeserializeGraphs();
        var gg = ap.data.gridGraph;
        if (gg == null) return;
        int top = minY + sizeY + 2;
        int bottom = minY - 3 * (LayerH + LayerGap) - 2;
        int depth = top - bottom; if (depth % 2 == 1) depth++;
        int width = sizeX + 4; if (width % 2 == 1) width++;
        gg.center = new Vector3(0f, (top + bottom) / 2, 0f);
        gg.SetDimensions(width, depth, 1f);
        ap.data.cacheStartup = false;
        ap.scanOnStartup = true;
        ap.data.SetData(ap.data.SerializeGraphs());
        EditorUtility.SetDirty(ap);
    }
}
#endif
