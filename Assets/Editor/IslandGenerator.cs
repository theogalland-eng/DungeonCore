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
    const int LayerW = 180, LayerH = 54, LayerGap = 12;   // étages souterrains larges (la grille A* fait 196 cases)
    /// <summary>Cases ajoutées au sud de la carte : l'île s'y prolonge pour loger la cité (plan validé du 20/10).</summary>
    const int ExtraSouth = 70;

    static void InitBounds()
    {
        minX = -(W / 2 + Margin); minY = -(H / 2 + Margin + ExtraSouth);
        sizeX = W + 2 * Margin; sizeY = H + 2 * Margin + ExtraSouth;
    }

    /// <summary>
    /// Terres du sud (cité, port, manoir) : un grand rectangle aux coins arrondis fondu dans l'ellipse de l'île.
    /// Distance exprimée comme celle de l'île (une unité ≈ 70 cases) : la côte garde la même pente, la même
    /// largeur de grève et le même bruit que le reste ; rien ne trahit un ajout.
    /// </summary>
    static float SouthDistance(float x, float y)
    {
        Vector2 c = new Vector2(29.5f, -71f), b = new Vector2(61.5f, 61f);
        const float r = 28f;
        float qx = Mathf.Abs(x - c.x) - b.x + r, qy = Mathf.Abs(y - c.y) - b.y + r;
        float sdf = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        return 1f + sdf / 70f;
    }

    /// <summary>Minimum adouci : la jonction entre l'île et les terres du sud n'a pas d'angle rentrant.</summary>
    static float SmoothMin(float a, float b, float k)
    {
        float t = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        return Mathf.Lerp(b, a, t) - k * t * (1f - t);
    }

    /// <summary>Emprises du plan qui doivent rester sur la terre ferme (cité, manoir, ferme, terrain du héros), avec leur marge.</summary>
    static readonly RectInt[] Protected =
    {
        new RectInt(-13, -109, 78, 46),   // cité (-10, -106, 72×40)
        new RectInt(62, -96, 23, 16),     // manoir
        new RectInt(43, -11, 28, 22),     // ferme
        new RectInt(-19, -59, 19, 15),    // terrain du héros
    };

    /// <summary>Terre ferme garantie sous les emprises du plan, sans dessiner de bord rond.</summary>
    static float CityFloor(int x, int y)
    {
        float best = -1f;
        foreach (var r in Protected)
        {
            int dx = Mathf.Max(r.x - x, 0, x - (r.xMax - 1)), dy = Mathf.Max(r.y - y, 0, y - (r.yMax - 1));
            best = Mathf.Max(best, 0.32f - Mathf.Sqrt(dx * dx + dy * dy) * 0.06f);
        }
        return best;
    }

    enum K : byte { Deep, Shallow, Reef, Beach, Cliff, Low, High, Mountain, Ravine, Path, Dock, Cave, Lake, Lava }

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

        InitBounds();
        map = new K[sizeX, sizeY];
        forest = new float[sizeX, sizeY];

        BuildHeightmap();
        FixInland();
        CarveLakes();
        CarveLake(new Vector2(80f, -76f), 3.6f);   // le lac du manoir
        AddCliffs();
        var (grotto, doorstep, dir) = PlaceGrotto();
        var landing = TracePath(doorstep);
        AddLavaFlows(grotto);
        var spawn = BuildDock(landing);
        landingCell = landing;
        LayoutSettlement(doorstep, landing);

        Paint(grotto, doorstep, spawn, out string paintLog);
        return $"Île générée (graine {Seed}) — {paintLog}";
    }

    /// <summary>Génère la carte en mémoire seulement (rien n'est peint) et décrit les repères : grotte, débarcadère, côtes.</summary>
    public static string DryRun()
    {
        rng = new System.Random(Seed);
        Vector2 R() => new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
        o1 = R(); o2 = R(); o3 = R(); o4 = R(); o5 = R();
        InitBounds();
        map = new K[sizeX, sizeY];
        forest = new float[sizeX, sizeY];
        BuildHeightmap();
        FixInland();
        CarveLakes();
        CarveLake(new Vector2(80f, -76f), 3.6f);
        AddCliffs();
        var (grotto, doorstep, dir) = PlaceGrotto();
        var landing = TracePath(doorstep);
        LayoutSettlement(doorstep, landing);
        var sb = new System.Text.StringBuilder($"grotte={grotto} seuil={doorstep} dir={dir} débarcadère={landing} routes={roadCells.Count} pavé={pavedCells.Count} | ");
        foreach (var pl in plots) sb.Append($"{pl.buildingId}@{pl.rect.x},{pl.rect.y} ");
        sb.Append("| ");
        foreach (var m in planMarks) sb.Append($"{m.name}@{m.cell.x},{m.cell.y} ");
        sb.Append("| ");
        // côte : premières et dernières cases de terre par rangée (tous les 10)
        for (int y = minY + sizeY - 1; y >= minY; y -= 10)
        {
            int a = int.MaxValue, b = int.MinValue;
            for (int x = minX; x < minX + sizeX; x++) if (!IsSea(Get(x, y))) { a = Mathf.Min(a, x); b = Mathf.Max(b, x); }
            sb.Append(a <= b ? $"y{y}:{a}..{b} " : $"y{y}:- ");
        }
        sb.Append("| colonnes : ");
        for (int x = minX; x < minX + sizeX; x += 10)
        {
            int a = int.MaxValue, b = int.MinValue;
            for (int y = minY; y < minY + sizeY; y++) if (!IsSea(Get(x, y))) { a = Mathf.Min(a, y); b = Mathf.Max(b, y); }
            sb.Append(a <= b ? $"x{x}:{a}..{b} " : $"x{x}:- ");
        }
        sb.Append("| lacs : ");
        for (int ix = 0; ix < sizeX; ix += 3) for (int iy = 0; iy < sizeY; iy += 3) if (map[ix, iy] == K.Lake) sb.Append($"({ix + minX},{iy + minY})");
        return sb.ToString();
    }

    /// <summary>Aperçu de la carte en mémoire (Temp/map.png, 4 px par case) : relief, routes, dallage, emplacements, repères.</summary>
    public static string MapPng()
    {
        string r = DryRun();
        const int S = 4;
        var tex = new Texture2D(sizeX * S, sizeY * S, TextureFormat.RGB24, false);
        var px = new Color32[sizeX * S * sizeY * S];
        Color32 Col(K k) => k switch
        {
            K.Deep => new Color32(14, 40, 70, 255), K.Shallow => new Color32(30, 110, 130, 255), K.Reef => new Color32(40, 130, 140, 255),
            K.Beach => new Color32(70, 64, 66, 255), K.Cliff => new Color32(50, 44, 46, 255), K.Low => new Color32(70, 140, 60, 255),
            K.High => new Color32(55, 110, 50, 255), K.Mountain => new Color32(95, 85, 80, 255), K.Lake => new Color32(50, 150, 150, 255),
            K.Lava => new Color32(220, 90, 30, 255), K.Cave => new Color32(20, 20, 20, 255), K.Dock => new Color32(150, 110, 70, 255),
            _ => new Color32(255, 0, 255, 255),
        };
        var plotCells = new HashSet<Vector3Int>();
        foreach (var pl in plots) foreach (var pp in pl.rect.allPositionsWithin) plotCells.Add(new Vector3Int(pp.x, pp.y, 0));
        var markCells = new HashSet<Vector3Int>(planMarks.Select(m => m.cell));
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                var c = new Vector3Int(ix + minX, iy + minY, 0);
                Color32 col = Col(Ground(c.x, c.y));
                if (Get(c.x, c.y) == K.Path && !pavedCells.Contains(c) && !roadCells.Contains(c)) col = Color32.Lerp(col, new Color32(140, 180, 90, 255), 0.35f);
                if (roadCells.Contains(c)) col = new Color32(150, 115, 75, 255);
                if (pavedCells.Contains(c)) col = new Color32(150, 150, 160, 255);
                if (plotCells.Contains(c)) col = new Color32(200, 70, 60, 255);
                if (markCells.Contains(c)) col = new Color32(255, 220, 90, 255);
                if (c.x == 0 || c.y == 0) col = Color32.Lerp(col, new Color32(255, 255, 255, 255), 0.3f);
                for (int a = 0; a < S; a++) for (int b = 0; b < S; b++) px[(iy * S + b) * sizeX * S + ix * S + a] = col;
            }
        tex.SetPixels32(px);
        System.IO.File.WriteAllBytes("Temp/map.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return "Temp/map.png — " + r.Substring(0, Mathf.Min(r.Length, 300));
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
                float d0 = Mathf.Sqrt(nx * nx + ny * ny);
                float n1 = Fbm(x * 0.045f + o1.x, y * 0.045f + o1.y, 4);
                float hOld = 1.02f - d0 + (n1 - 0.5f) * 0.6f;   // relief d'origine : il donne l'altitude
                float d = SmoothMin(d0, SouthDistance(x, y), 0.12f);   // + les terres du sud (plan de la cité)
                float h = Mathf.Max(1.02f - d + (n1 - 0.5f) * 0.6f, CityFloor(x, y));

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
                    float e = ridge + (Mathf.Min(h, hOld) - 0.15f) * 0.3f;
                    k = e > 0.7f ? K.Mountain : e > 0.4f ? K.High : K.Low;
                }
                map[ix, iy] = k;
            }
    }

    /// <summary>
    /// Pas de mer ni de grève au milieu des terres : les poches de mer coupées du large deviennent un lac
    /// (ou de la terre si elles sont minuscules), et la grève qui ne touche pas le large redevient de la basse terre.
    /// </summary>
    static void FixInland()
    {
        var outer = new bool[sizeX, sizeY];
        var q = new Queue<Vector2Int>();
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
                if ((ix == 0 || iy == 0 || ix == sizeX - 1 || iy == sizeY - 1) && IsSea(map[ix, iy])) { outer[ix, iy] = true; q.Enqueue(new Vector2Int(ix, iy)); }
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (n.x < 0 || n.y < 0 || n.x >= sizeX || n.y >= sizeY || outer[n.x, n.y] || !IsSea(map[n.x, n.y])) continue;
                outer[n.x, n.y] = true; q.Enqueue(n);
            }
        }
        // poches de mer intérieures
        var seen = new bool[sizeX, sizeY];
        var comp = new List<Vector2Int>();
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                if (outer[ix, iy] || seen[ix, iy] || !IsSea(map[ix, iy])) continue;
                comp.Clear(); seen[ix, iy] = true; q.Enqueue(new Vector2Int(ix, iy));
                while (q.Count > 0)
                {
                    var c = q.Dequeue(); comp.Add(c);
                    foreach (var d in dirs)
                    {
                        var n = c + d;
                        if (n.x < 0 || n.y < 0 || n.x >= sizeX || n.y >= sizeY || seen[n.x, n.y] || !IsSea(map[n.x, n.y])) continue;
                        seen[n.x, n.y] = true; q.Enqueue(n);
                    }
                }
                K fill = comp.Count >= 14 ? K.Lake : K.Low;
                foreach (var c in comp) map[c.x, c.y] = fill;
            }
        // grève : seulement celle reliée au large
        var shore = new bool[sizeX, sizeY];
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                if (map[ix, iy] != K.Beach) continue;
                foreach (var d in dirs)
                {
                    int nx = ix + d.x, ny = iy + d.y;
                    if (nx >= 0 && ny >= 0 && nx < sizeX && ny < sizeY && outer[nx, ny]) { shore[ix, iy] = true; q.Enqueue(new Vector2Int(ix, iy)); break; }
                }
            }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (n.x < 0 || n.y < 0 || n.x >= sizeX || n.y >= sizeY || shore[n.x, n.y] || map[n.x, n.y] != K.Beach) continue;
                shore[n.x, n.y] = true; q.Enqueue(n);
            }
        }
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
                if (map[ix, iy] == K.Beach && !shore[ix, iy]) map[ix, iy] = K.Low;
    }

    /// <summary>
    /// Petits lacs d'eau douce dans la forêt (à la place des anciens ravins noirs) : deux lacs et quelques mares,
    /// aux rives irrégulières, loin de la grève et de la montagne.
    /// </summary>
    static void CarveLakes()
    {
        var centers = new List<Vector2>();
        for (int tries = 0; tries < 900 && centers.Count < 5; tries++)
        {
            int x = rng.Next(minX, minX + sizeX), y = rng.Next(minY, minY + sizeY);
            if (Get(x, y) != K.Low) continue;
            // les terres du sud et le camp suivent le plan de la cité : pas de lac au hasard
            if (y < -46 || Vector2.Distance(new Vector2(x, y), new Vector2(24f, -30f)) < 13f || (x > 38 && x < 74 && y > -16 && y < 16)) continue;
            float r = centers.Count < 2 ? 3.4f + (float)rng.NextDouble() * 1.6f : 1.7f + (float)rng.NextDouble() * 1.1f;
            var c = new Vector2(x, y);
            if (centers.Any(o => Vector2.Distance(o, c) < 20f)) continue;
            int reach = Mathf.CeilToInt(r) + 5;
            bool ok = true;
            for (int ax = -reach; ax <= reach && ok; ax++)
                for (int ay = -reach; ay <= reach && ok; ay++)
                    if (ax * ax + ay * ay <= reach * reach && Get(x + ax, y + ay) != K.Low) ok = false;
            if (!ok) continue;
            centers.Add(c);
            for (int ax = -reach; ax <= reach; ax++)
                for (int ay = -reach; ay <= reach; ay++)
                {
                    float wob = 0.72f + 0.56f * Fbm((x + ax) * 0.3f + o5.y, (y + ay) * 0.3f + o4.x, 2);
                    // ovale un peu plus large que haut, comme vu de 3/4
                    float d = Mathf.Sqrt(ax * ax / 1.25f + ay * ay) / (r * wob);
                    if (d < 1f) Set(x + ax, y + ay, K.Lake);
                }
        }
    }

    /// <summary>
    /// Coulées de lave : elles naissent au pied de la montagne-volcan et descendent dans les hautes terres,
    /// loin de la grotte du Noyau et du passage des aventuriers.
    /// </summary>
    static void AddLavaFlows(Vector3Int grotto)
    {
        var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        int made = 0;
        var sources = new List<Vector2>();
        for (int tries = 0; tries < 3000 && made < 4; tries++)
        {
            int x = rng.Next(minX, minX + sizeX), y = rng.Next(minY, minY + sizeY);
            if (Get(x, y) != K.High) continue;
            var start = new Vector3Int(x, y, 0);
            if (Vector3Int.Distance(start, grotto) < 16f) continue;
            if (sources.Any(o => Vector2.Distance(o, new Vector2(x, y)) < 14f)) continue;
            Vector2 away = Vector2.zero;
            foreach (var d in dirs) if (Get(x + d.x, y + d.y) == K.Mountain) away -= new Vector2(d.x, d.y);
            if (away == Vector2.zero || NearKind(start, 3, K.Path, K.Cave, K.Dock)) continue;
            away.Normalize();
            var p = new Vector2(x, y);
            var cells = new List<Vector3Int>();
            int len = rng.Next(8, 16);
            for (int i = 0; i < len; i++)
            {
                var c = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 0);
                if (Get(c.x, c.y) != K.High || NearKind(c, 2, K.Path, K.Cave, K.Dock, K.Low)) break;
                cells.Add(c);
                if (i > 2 && rng.NextDouble() < 0.4)
                {
                    var side = c + (Mathf.Abs(away.x) > Mathf.Abs(away.y) ? Vector3Int.up : Vector3Int.right);
                    if (Get(side.x, side.y) == K.High && !NearKind(side, 2, K.Path, K.Cave, K.Dock, K.Low)) cells.Add(side);
                }
                float a = Mathf.Atan2(away.y, away.x) + ((float)rng.NextDouble() - 0.5f) * 0.9f;
                p += new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            if (cells.Count < 5) continue;
            foreach (var c in cells) Set(c.x, c.y, K.Lava);
            sources.Add(new Vector2(x, y));
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
            // le port du plan : sur la côte sud du lobe sud-ouest
            int score = Mathf.RoundToInt(Vector2.Distance(new Vector2(c.x, c.y), PortTarget) * 10f);
            if (score < bestScore) { bestScore = score; best = c; }
        }

        // Le passage le plus naturel vers la grotte : il n'est PAS dessiné (pas de chemin au départ),
        // il garde juste les arbres à l'écart. Le sentier naît ensuite du passage des aventuriers (TrailWear).
        pathUnder.Clear();
        void Mark(Vector3Int c)
        {
            K k = Get(c.x, c.y);
            if (k == K.Path) return;
            pathUnder[c] = k;
            Set(c.x, c.y, K.Path);
        }
        var cur = best;
        while (parent.ContainsKey(cur))
        {
            if (Get(cur.x, cur.y) != K.Beach) Mark(cur);
            var side = cur + ((cur.x + cur.y) % 2 == 0 ? Vector3Int.right : Vector3Int.up);
            K sk = Get(side.x, side.y);
            if (sk == K.Low || sk == K.High) Mark(side);
            cur = parent[cur];
        }
        Mark(start);
        return best;
    }

    static Vector3Int landingCell, dockDir;
    static readonly Vector2 PortTarget = new Vector2(-28f, -76f);
    static readonly Dictionary<Vector3Int, K> pathUnder = new Dictionary<Vector3Int, K>();
    /// <summary>Sol réel d'une case (sous le passage invisible vers la grotte).</summary>
    static K Ground(int x, int y) { K k = Get(x, y); return k == K.Path && pathUnder.TryGetValue(new Vector3Int(x, y, 0), out var u) ? u : k; }
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

    static readonly List<PlanMark> planMarks = new List<PlanMark>();
    static readonly HashSet<Vector3Int> roadCells = new HashSet<Vector3Int>();
    static readonly HashSet<Vector3Int> pavedCells = new HashSet<Vector3Int>();
    static RectInt cityRect;

    /// <summary>Un lac rond (cases de basse terre seulement).</summary>
    static void CarveLake(Vector2 c, float r)
    {
        int reach = Mathf.CeilToInt(r) + 1;
        for (int ax = -reach; ax <= reach; ax++)
            for (int ay = -reach; ay <= reach; ay++)
            {
                int x = Mathf.RoundToInt(c.x) + ax, y = Mathf.RoundToInt(c.y) + ay;
                float wob = 0.8f + 0.4f * Fbm(x * 0.3f + o5.y, y * 0.3f + o4.x, 2);
                if (Mathf.Sqrt(ax * ax / 1.25f + ay * ay) / (r * wob) < 1f && Get(x, y) == K.Low) Set(x, y, K.Lake);
            }
    }

    static bool OpenGround(K g) => g == K.Low || g == K.High || g == K.Beach;

    /// <summary>Case rendue praticable et sans arbre (sentier invisible, route ou dallage selon la liste qui la retient).</summary>
    static void MarkPath(Vector3Int c)
    {
        K k = Get(c.x, c.y);
        if (k == K.Path || !OpenGround(k)) return;
        pathUnder[c] = k;
        Set(c.x, c.y, K.Path);
    }

    /// <summary>
    /// Route de terre : plus court chemin praticable (8 directions, sans couper les coins), puis tracé simplifié,
    /// lissé (courbes de Chaikin) et peint au pinceau rond : des routes qui tournent en douceur, sans escaliers.
    /// </summary>
    static void Road(Vector3Int a, Vector3Int b, float width = 2.5f)
    {
        var dist = new Dictionary<Vector3Int, float> { [a] = 0f };
        var parent = new Dictionary<Vector3Int, Vector3Int>();
        var open = new SortedSet<(float, int, int, int)>();
        int serial = 0;
        open.Add((Vector3Int.Distance(a, b), a.x, a.y, serial++));
        int guard = 0;
        bool Open(Vector3Int n) => n == b || (OpenGround(Ground(n.x, n.y)) && !reservedCells.Contains(n)
                                               && (!cityRect.Contains(new Vector2Int(n.x, n.y)) || roadCells.Contains(n)));
        while (open.Count > 0 && guard++ < 120000)
        {
            var top = open.Min; open.Remove(top);
            var c = new Vector3Int(top.Item2, top.Item3, 0);
            if (c == b) break;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var n = c + new Vector3Int(dx, dy, 0);
                    if (!Open(n)) continue;
                    bool diag = dx != 0 && dy != 0;
                    if (diag && (!Open(c + new Vector3Int(dx, 0, 0)) || !Open(c + new Vector3Int(0, dy, 0)))) continue;
                    K g = Ground(n.x, n.y);
                    float cost = (diag ? 1.414f : 1f) * (1f + 0.6f * Fbm(n.x * 0.12f + o3.y, n.y * 0.12f + o5.x, 2) + (g == K.Beach ? 1.5f : 0f))
                                 - (roadCells.Contains(n) ? 0.6f : 0f);
                    float nd = dist[c] + cost;
                    if (dist.TryGetValue(n, out float old) && old <= nd) continue;
                    dist[n] = nd; parent[n] = c;
                    open.Add((nd + Vector3Int.Distance(n, b), n.x, n.y, serial++));
                }
        }
        if (!parent.ContainsKey(b)) { Debug.LogWarning($"[Île] route introuvable {a} -> {b}"); return; }
        var cells = new List<Vector2>();
        for (var c = b; ; c = parent[c])
        {
            cells.Add(new Vector2(c.x + 0.5f, c.y + 0.5f));
            if (c == a || !parent.ContainsKey(c)) break;
        }
        cells.Reverse();
        var pts = Simplify(cells, 1.1f);
        for (int k = 0; k < 3; k++) pts = Chaikin(pts);
        float r = width * 0.5f;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            float len = Vector2.Distance(pts[i], pts[i + 1]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
            for (int k = 0; k <= steps; k++)
            {
                var p = Vector2.Lerp(pts[i], pts[i + 1], k / (float)steps);
                int ri = Mathf.CeilToInt(r);
                for (int x = Mathf.FloorToInt(p.x) - ri; x <= Mathf.FloorToInt(p.x) + ri; x++)
                    for (int y = Mathf.FloorToInt(p.y) - ri; y <= Mathf.FloorToInt(p.y) + ri; y++)
                    {
                        if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), p) > r) continue;
                        var cc = new Vector3Int(x, y, 0);
                        if (reservedCells.Contains(cc) || (cityRect.Contains(new Vector2Int(x, y)) && !roadCells.Contains(cc))) continue;
                        if (OpenGround(Ground(x, y))) { roadCells.Add(cc); MarkPath(cc); }
                    }
            }
        }
    }

    /// <summary>Douglas-Peucker : garde les points qui s'écartent de plus de <paramref name="eps"/> de la corde.</summary>
    static List<Vector2> Simplify(List<Vector2> pts, float eps)
    {
        if (pts.Count < 3) return new List<Vector2>(pts);
        int idx = 0; float dmax = 0f;
        for (int i = 1; i < pts.Count - 1; i++)
        {
            float d = SegDist(pts[i], pts[0], pts[pts.Count - 1]);
            if (d > dmax) { dmax = d; idx = i; }
        }
        if (dmax <= eps) return new List<Vector2> { pts[0], pts[pts.Count - 1] };
        var left = Simplify(pts.GetRange(0, idx + 1), eps);
        var right = Simplify(pts.GetRange(idx, pts.Count - idx), eps);
        left.RemoveAt(left.Count - 1);
        left.AddRange(right);
        return left;
    }

    static List<Vector2> Chaikin(List<Vector2> pts)
    {
        if (pts.Count < 3) return pts;
        var o = new List<Vector2> { pts[0] };
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            o.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.25f));
            o.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.75f));
        }
        o.Add(pts[pts.Count - 1]);
        return o;
    }

    static bool BuildableAt(int x, int y, HashSet<Vector3Int> reserved)
    {
        var c = new Vector3Int(x, y, 0);
        return OpenGround(Ground(x, y)) && !reserved.Contains(c) && !roadCells.Contains(c);
    }

    /// <summary>Emprise libre (avec une case de marge) la plus proche du centre voulu.</summary>
    static RectInt? FindRect(int cx, int cy, int w, int h, HashSet<Vector3Int> reserved, int radius = 10, int margin = 1)
    {
        for (int r = 0; r <= radius; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x0 = cx - w / 2 + dx, y0 = cy - h / 2 + dy;
                    bool ok = true;
                    for (int x = x0 - margin; x < x0 + w + margin && ok; x++)
                        for (int y = y0 - margin; y < y0 + h + margin && ok; y++)
                            if (!BuildableAt(x, y, reserved)) ok = false;
                    if (ok) return new RectInt(x0, y0, w, h);
                }
        return null;
    }

    static readonly HashSet<Vector3Int> reservedCells = new HashSet<Vector3Int>();
    static readonly List<Vector3Int> cityStreets = new List<Vector3Int>();
    static readonly List<Vector3Int> cityWall = new List<Vector3Int>();
    static readonly List<Vector3Int> cityGates = new List<Vector3Int>();

    /// <summary>
    /// PLAN DE LA CITÉ FINALE (validé le 20/10, agrandi le 21/10) : emplacements fixes, ancrés sur l'entrée du donjon.
    /// Camp à ≈ 40 cases de l'entrée ; cité fortifiée de 72 × 48 au sud (quatre quartiers, rues de terre pavées de
    /// briques une fois fortifiée) ; faubourg à la porte ouest, sur la route du port ; port et ponton au sud-ouest ;
    /// terrain du héros près du port ; manoir et son lac à l'est de la cité ; grande ferme au nord-est.
    /// Les bâtiments des packs sont des « tampons » (scène complète, cour comprise) posés sans marge.
    /// </summary>
    static void LayoutSettlement(Vector3Int doorstep, Vector3Int landing)
    {
        plots.Clear(); planMarks.Clear(); roadCells.Clear(); pavedCells.Clear();
        cityStreets.Clear(); cityWall.Clear(); cityGates.Clear(); reservedCells.Clear();
        var reserved = reservedCells;
        cityRect = new RectInt(0, 0, 0, 0);
        int ox = doorstep.x + 8, oy = doorstep.y + 10;   // le plan est dessiné pour une entrée en (-8, -10)
        Vector3Int P(int x, int y) => new Vector3Int(x + ox, y + oy, 0);
        void Reserve(RectInt r, int margin)
        {
            for (int x = r.x - margin; x < r.xMax + margin; x++)
                for (int y = r.y - margin; y < r.yMax + margin; y++) reserved.Add(new Vector3Int(x, y, 0));
        }

        var camp = P(24, -30);
        var port = P(-24, -66);
        var hero = P(-10, -52);
        cityRect = new RectInt(-10 + ox, -106 + oy, 72, 40);
        int cx0 = cityRect.x, cy0 = cityRect.y;
        Vector3Int C(float x, float y) => new Vector3Int(cx0 + Mathf.RoundToInt(x), cy0 + Mathf.RoundToInt(y), 0);
        // colonnes : rempart 0 | ronde 1-2 | A 3-18 | rue 19-21 | B 22-33 | avenue 34-37 | C 38-49 | rue 50-52 | D 53-68 | ronde 69-70 | rempart 71
        // rangées : rempart 0 | ronde 1-2 | sud 3-11 | rue 12-14 | milieu 15-23 | rue 24-26 | nord 27-36 | ronde 37-38 | rempart 39
        // les bâtiments sont posés au bas de leur îlot : leur porte donne sur la rue
        bool Street(int rx, int ry) =>
            rx == 1 || rx == 2 || rx == 69 || rx == 70 || ry == 1 || ry == 2 || ry == 37 || ry == 38
            || (rx >= 19 && rx <= 21) || (rx >= 34 && rx <= 37) || (rx >= 50 && rx <= 52)
            || (ry >= 12 && ry <= 14) || (ry >= 24 && ry <= 26);
        bool Gate(int rx, int ry) =>
            (ry == 39 && rx >= 34 && rx <= 37) || (rx == 0 && ry >= 12 && ry <= 14) || (rx == 71 && ry >= 24 && ry <= 26);
        var gateN = C(35, 40);
        var gateW = C(-1, 13);
        var gateE = C(72, 25);
        var manor = C(83, 18);
        var farm = P(57, 0);

        // la cité : tout l'enclos dégagé (pas d'arbre) ; rues de terre ; tracé de l'enceinte et des portes
        foreach (var pp in cityRect.allPositionsWithin)
        {
            var c = new Vector3Int(pp.x, pp.y, 0);
            if (!OpenGround(Ground(c.x, c.y))) continue;
            MarkPath(c);
            int rx = c.x - cx0, ry = c.y - cy0;
            bool edge = rx == 0 || ry == 0 || rx == 71 || ry == 39;
            if (Gate(rx, ry)) { cityGates.Add(c); roadCells.Add(c); cityStreets.Add(c); }
            else if (edge) { cityWall.Add(c); reserved.Add(c); }
            else if (Street(rx, ry)) { roadCells.Add(c); cityStreets.Add(c); }
        }

        void Plot(string id, Vector3Int at, int radius = 10)
        {
            var def = SettlementPlan.Get(id);
            if (def == null || def.w <= 0) return;
            bool stamp = !string.IsNullOrEmpty(def.stamp);
            var r = FindRect(at.x, at.y, def.w, def.h, reserved, radius, stamp ? 0 : 1);
            if (r == null) { Debug.LogWarning($"[Île] pas de place pour {id} près de {at}"); return; }
            plots.Add(new SettlementPlot { buildingId = id, rect = r.Value });
            Reserve(r.Value, 0);
            foreach (var pp in r.Value.allPositionsWithin) MarkPath(new Vector3Int(pp.x, pp.y, 0));   // pas d'arbre sur l'emprise
        }
        void Mark(string name, Vector3Int at, int w, int h, bool adventureOnly = false)
        {
            var r = FindRect(at.x, at.y, w, h, reserved, 8) ?? new RectInt(at.x - w / 2, at.y - h / 2, w, h);
            Reserve(r, 1);
            foreach (var pp in r.allPositionsWithin) MarkPath(new Vector3Int(pp.x, pp.y, 0));
            planMarks.Add(new PlanMark { name = name, cell = new Vector3Int(r.x + w / 2, r.y, 0), adventureOnly = adventureOnly });
        }

        // emplacement au coin bas-gauche visé (relatif à la cité)
        void At(string id, int rx, int ry, int radius = 3)
        {
            var def = SettlementPlan.Get(id);
            if (def == null) return;
            Plot(id, C(rx + def.w / 2, ry + def.h / 2), radius);
        }

        // 1. camp de la Guilde
        Plot("campfire", camp);
        Plot("tent_a", camp + new Vector3Int(-4, 3, 0));
        Plot("tent_b", camp + new Vector3Int(4, 3, 0));
        Plot("stockpile", camp + new Vector3Int(4, -2, 0));
        // 2. port de pêche
        Plot("fisher_hut", port + new Vector3Int(2, 3, 0));
        Plot("fish_rack", port + new Vector3Int(-6, 0, 0));
        Plot("tent_c", port + new Vector3Int(6, 5, 0));
        Plot("crates", port + new Vector3Int(-1, -5, 0));
        // la cité — rangée nord : tour des mages, bibliothèque, taverne, Guilde et armurerie
        At("tent_magic", 4, 27);
        At("library", 22, 27);
        At("house_1", 38, 27);
        At("house_4", 53, 27);
        At("stall_weapons", 64, 28);
        // milieu : maisons, marché, entraînement, forge et étal
        int home = 1;
        foreach (var (hx, hy) in new[] { (4, 16), (10, 16), (16, 16), (4, 20), (10, 20), (16, 20) })
            At("home_" + home++, hx - 1, hy, 1);
        At("market", 22, 15);
        At("training", 38, 15);
        At("house_3", 53, 15);
        At("stall_fruits", 64, 16);
        // sud : vieille chapelle, maisons, verrerie, prison
        At("chapel", 5, 3);
        foreach (var (hx, hy) in new[] { (23, 4), (28, 4), (23, 8), (28, 8) })
            At("home_" + home++, hx - 1, hy, 1);
        At("glass", 38, 3);
        At("prison", 54, 3);
        // faubourgs : porte ouest (route du port) et porte nord (route du camp)
        Plot("house_2", C(-7, 20), 6);
        var faubourg = new[] { C(-7, 6), C(-7, 26), C(-14, 14), C(26, 45), C(45, 45), C(20, 50), C(51, 50), C(-13, 30) };
        for (int i = 0; home <= SettlementPlan.HomeCount && i < faubourg.Length; i++)
            Plot("home_" + home++, faubourg[i], 7);
        // devant la porte nord : boulangerie et pavillon
        Plot("stall_bread", C(31, 43), 5);
        Plot("tent_round", C(40, 43), 5);
        // champs du hameau puis grande ferme
        Plot("farm", farm, 8);
        Plot("field_1", farm + new Vector3Int(-16, 6, 0), 6);
        Plot("field_2", farm + new Vector3Int(-16, -2, 0), 6);
        // manoir à l'est de la cité
        Plot("manor", manor, 6);
        // terrain du héros (mode Aventurier : à acheter)
        Plot("hero_home", hero, 6);
        var hp = plots.Find(x => x.buildingId == "hero_home");
        if (hp != null)
            planMarks.Add(new PlanMark { name = "Terrain du héros (à acheter)", cell = new Vector3Int(hp.rect.x + hp.rect.width / 2, hp.rect.y, 0), adventureOnly = true });

        // routes entre les quartiers, tracées après les emplacements : elles les contournent et finissent devant les portes
        Vector3Int Door(string id, Vector3Int fallback)
        {
            var pl = plots.Find(x => x.buildingId == id);
            return pl == null ? fallback : new Vector3Int(pl.rect.x + pl.rect.width / 2, pl.rect.y - 1, 0);
        }
        var campDoor = Door("campfire", camp);
        var portDoor = Door("fisher_hut", port);
        Road(doorstep, campDoor, 2.2f);
        Road(campDoor, gateN, 2.6f);
        Road(gateW, portDoor, 2.6f);
        Road(portDoor, landing, 2.2f);
        Road(portDoor, Door("hero_home", hero), 2f);
        Road(gateE, Door("manor", manor), 2.2f);
        Road(gateE, Door("farm", farm), 2.2f);
        Road(campDoor, Door("farm", farm), 2f);
        var outside = new List<string> { "house_2", "stall_bread", "tent_round", "crates", "field_1", "field_2" };
        for (int i = 11; i <= SettlementPlan.HomeCount; i++) outside.Add("home_" + i);
        foreach (var id in outside)
        {
            var pl = plots.Find(x => x.buildingId == id);
            if (pl == null) continue;
            var d = Door(id, Vector3Int.zero);
            // petit chemin jusqu'à la route la plus proche
            Vector3Int best = d; float bd = float.MaxValue;
            foreach (var rc in roadCells) { float dd = Vector3Int.Distance(rc, d); if (dd < bd) { bd = dd; best = rc; } }
            if (bd < 14f && bd > 1.5f) Road(d, best, 1.6f);
        }
    }

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
                    if (k == K.Path || k == K.Dock || k == K.Cave || k == K.Lake || k == K.Lava || k == K.Mountain || IsSea(k)) return false;
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

    // Palette « Mer et récif » de la maquette Design ; la tuile d'eau est claire (~84 %), on compense la teinte
    static Color Hex(string h, float boost = 1f / 0.84f)
    {
        ColorUtility.TryParseHtmlString(h, out var c);
        return new Color(Mathf.Min(1f, c.r * boost), Mathf.Min(1f, c.g * boost), Mathf.Min(1f, c.b * boost), 1f);
    }
    static readonly Color Lagoon = Hex("#39a8a6"), Turquoise = Hex("#1f7a8c"), SeaBlue = Hex("#13436b"), Abyss = Hex("#0b2440");
    static readonly Color LakeCol = Hex("#2f8f8c"), LakeDeep = Hex("#1f6f7a");

    static Color SeaColor(int ix, int iy)
    {
        float d = coastDist[ix, iy];
        // bruit doux pour casser les bandes régulières
        float n = (Mathf.PerlinNoise((ix + minX) * 0.15f + o5.x, (iy + minY) * 0.15f + o5.y) - 0.5f) * 2.5f;
        float t = Mathf.Clamp01((d + n - 0.5f) / 12f);
        if (t < 0.18f) return Color.Lerp(Lagoon, Turquoise, t / 0.18f);
        if (t < 0.5f) return Color.Lerp(Turquoise, SeaBlue, (t - 0.18f) / 0.32f);
        return Color.Lerp(SeaBlue, Abyss, (t - 0.5f) / 0.5f);
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

        var snow = CatalogBuilder.Sprites("Assets/imports/snow-expansion.png");
        Tile S(int i, Tile.ColliderType ct = Tile.ColliderType.None) => CatalogBuilder.TileFor(snow[$"snow-expansion_{i}"], ct);

        var builder = Object.FindFirstObjectByType<DungeonBuilder>();
        TileBase caveFloor = DungeonCatalog.Get() != null && DungeonCatalog.Get().themes.Count > 0 && DungeonCatalog.Get().themes[0].floorTile != null
            ? DungeonCatalog.Get().themes[0].floorTile : builder.floorTile;

        // Collision seule : le tilemap NoPathfinding n'est plus affiché (l'eau et les rives sont dessinées en couches)
        TileBase sea = (TileBase)ArtImporter.SeaBlocked() ?? S(176, Tile.ColliderType.Grid);
        var sart = SettlementArt.Get();
        Tile wallCollider = S(155, Tile.ColliderType.Grid);
        Tile stoneA = S(195, Tile.ColliderType.Grid);
        Tile plank = S(82);
        Tile trunk = S(174, Tile.ColliderType.Grid);   // collision invisible des troncs
        var cat = DungeonCatalog.Get();
        TileBase wallTile = cat.themes.Count > 0 && cat.themes[0].wallTile != null ? cat.themes[0].wallTile : stoneA;
        var wallCells = new List<Vector3Int>();

        // ── Couches du terrain (Tools~/make_terrain.py), de bas en haut ──
        TerrainArt.ClearCache();
        var water = TerrainArt.Water();
        BlobTile tSand = TerrainArt.Blob("sand"), tRock = TerrainArt.Blob("rock"), tGrass = TerrainArt.Blob("grass"),
                 tJungle = TerrainArt.Blob("jungle"), tLava = TerrainArt.Blob("lava"), tSnow = TerrainArt.Blob("snow");
        BlobTile tTrampled = TerrainArt.Blob("trampled"), tDirt = TerrainArt.Blob("dirt"), tPaved = TerrainArt.Blob("paved");
        tTrampled.sameAs = new TileBase[] { tDirt }; tDirt.sameAs = new TileBase[] { tTrampled };
        EditorUtility.SetDirty(tTrampled); EditorUtility.SetDirty(tDirt);
        var cliffTiles = TerrainArt.Cliff();
        Tile markGravel = TerrainArt.Marker("ground_gravel"), markSoil = TerrainArt.Marker("ground_soil");
        Tile D(string n) => TerrainArt.Decor(n);

        Tilemap Layer(string name, int order)
        {
            var tm = FindTilemap(name) ?? CreateTilemap(name, order);
            tm.GetComponent<TilemapRenderer>().sortingOrder = order;
            tm.ClearAllTiles();
            return tm;
        }
        var lWater = Layer("T_Eau", -12);
        var lSand = Layer("T_Sable", -11);
        var lRock = Layer("T_Roche", -10);
        var lGrass = Layer("T_Herbe", -9);
        var lJungle = Layer("T_Jungle", -8);
        var lLava = Layer("T_Lave", -7);
        var lSnow = Layer("T_Neige", -7);   // n'apparaît qu'en hiver (Seasons)
        var lCliff = Layer("T_Falaise", -6);
        Layer("Sentier", -5);
        var lRoutes = Layer("Routes", -4);
        blocked.GetComponent<TilemapRenderer>().enabled = false;

        bool IsLand(K k) => !IsSea(k) && k != K.Lake && k != K.Dock;
        bool RockOwn(K k) => k == K.High || k == K.Mountain || k == K.Lava || k == K.Cave;
        bool GrassOwn(K k) => k == K.Low || k == K.Cliff;
        bool JungleAt(int x, int y) => Ground(x, y) == K.Low && forest[x - minX, y - minY] > 0.56f;
        bool NearAny(int x, int y, System.Func<int, int, bool> f)
        {
            for (int ax = -1; ax <= 1; ax++)
                for (int ay = -1; ay <= 1; ay++)
                    if ((ax != 0 || ay != 0) && f(x + ax, y + ay)) return true;
            return false;
        }

        var seaPixels = new Color[sizeX * sizeY];
        var env0 = GameObject.Find("Environment") ?? new GameObject("Environment");
        for (int i = env0.transform.childCount - 1; i >= 0; i--)
            if (env0.transform.GetChild(i).name != "Relief") Object.DestroyImmediate(env0.transform.GetChild(i).gameObject);
        var lakeCells = new List<Vector3Int>();
        var lavaCells = new List<Vector3Int>();
        int trees0 = 0, vents0 = 0;
        var ventCells = new List<Vector3Int>();
        var occupied = new HashSet<Vector3Int>();

        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                int x = ix + minX, y = iy + minY;
                var c = new Vector3Int(x, y, 0);
                K g = Ground(x, y);

                // eau partout dessous (les rives des couches supérieures sont découpées dedans)
                Color wcol;
                if (g == K.Lake) wcol = NearAny(x, y, (a, b) => Get(a, b) != K.Lake) ? LakeCol : LakeDeep;
                else if (IsLand(g) && NearAny(x, y, (a, b) => Get(a, b) == K.Lake)) wcol = LakeCol;
                else wcol = SeaColor(ix, iy);
                // couleur : texture lissée (1 pixel par case, filtrage bilinéaire) ; la tuile n'apporte que les vaguelettes
                var wc = wcol / (1f / 0.84f); wc.a = 1f;
                seaPixels[iy * sizeX + ix] = wc;
                Put(lWater, c, water, Color.white);

                if (IsLand(g)) lSand.SetTile(c, tSand);
                // la roche déborde d'une case sous l'herbe voisine : la lisière herbe/roche laisse voir la roche
                bool rock = RockOwn(g) || (GrassOwn(g) && NearAny(x, y, (a, b) => RockOwn(Ground(a, b))));
                if (rock) lRock.SetTile(c, tRock);
                if (GrassOwn(g)) lGrass.SetTile(c, tGrass);
                if (JungleAt(x, y)) lJungle.SetTile(c, tJungle);
                if (g == K.Lava) { lLava.SetTile(c, tLava); lavaCells.Add(c); }
                // neige d'hiver par plaques (pas sur la grève ni près de la lave)
                if ((g == K.Low || g == K.High || g == K.Cliff) && !NearAny(x, y, (a, b) => Ground(a, b) == K.Lava) &&
                    Fbm(x * 0.09f + o2.x, y * 0.09f + o5.y, 3) > 0.47f)
                    lSnow.SetTile(c, tSnow);

                switch (g)
                {
                    case K.Deep:
                    case K.Shallow:
                        Put(blocked, c, sea, Color.white);
                        break;
                    case K.Reef:
                        Put(blocked, c, sea, Color.white);
                        rng.NextDouble();   // (récifs sans rochers dessinés : ils se lisaient comme des taches sombres)
                        break;
                    case K.Lake:
                        Put(blocked, c, stoneA, Color.white);
                        lakeCells.Add(c);
                        break;
                    case K.Lava:
                        Put(blocked, c, stoneA, Color.white);
                        Put(sol, c, markSoil, Color.white);
                        break;
                    case K.Beach:
                    {
                        Put(sol, c, markGravel, Color.white);
                        double r = rng.NextDouble();
                        bool shore = NearAny(x, y, (a, b) => IsSea(Get(a, b)));
                        if (r < 0.03) Put(decor, c, D("pebbles_" + rng.Next(2)), Color.white);
                        else if (r < 0.045 && shore) Put(decor, c, D("shells_0"), Color.white);
                        else if (r < 0.05 && shore) Put(decor, c, D("driftwood_0"), Color.white);
                        break;
                    }
                    case K.Cliff:
                    {
                        Put(sol, c, markGravel, Color.white);
                        Put(blocked, c, wallCollider, Color.white);
                        bool l = Get(x - 1, y) == K.Cliff, rr = Get(x + 1, y) == K.Cliff;
                        int v = l && rr ? rng.Next(2) : rr ? 2 : l ? 3 : 4;
                        Put(lCliff, c, cliffTiles[v], Color.white);
                        break;
                    }
                    case K.Low:
                        Put(sol, c, markSoil, Color.white);
                        break;
                    case K.High:
                        Put(sol, c, markSoil, Color.white);
                        if (rng.NextDouble() < 0.03) Put(decor, c, D("pebbles_" + rng.Next(2)), new Color(0.8f, 0.78f, 0.82f));
                        break;
                    case K.Mountain:
                        wallCells.Add(c);
                        break;
                    case K.Dock:
                        if (sart != null && sart.pierLeft != null && sart.seaFloor != null)
                        {
                            // fond marin (invisible : l'eau est dessinée dessous) + planches du ponton par-dessus
                            Put(sol, c, sart.seaFloor, Color.clear);
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

        BuildSeaColour(seaPixels, env0);

        // routes de terre entre les quartiers, dallage de la cité
        foreach (var c in pavedCells) lRoutes.SetTile(c, tPaved);
        foreach (var c in roadCells) if (!pavedCells.Contains(c)) lRoutes.SetTile(c, tDirt);

        // Lacs : roseaux sur les rives, nénuphars sur l'eau
        foreach (var c in lakeCells)
        {
            if (rng.NextDouble() < 0.14) Put(decor, c, D("lily_" + rng.Next(2)), Color.white);
            foreach (var d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
            {
                var n = c + d;
                if (Get(n.x, n.y) != K.Low || decor.HasTile(n) || rng.NextDouble() > 0.4) continue;
                Put(decor, n, D("reeds_" + rng.Next(3)), Color.white);
                occupied.Add(n);
            }
        }

        // Entrée de la grotte : presque invisible, une faille sombre à demi cachée par les fougères et les éboulis
        {
            var into = Vector3Int.up;
            foreach (var d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                if (Get(doorstep.x + d.x, doorstep.y + d.y) == K.Cave) into = d;
            var side = into.x == 0 ? Vector3Int.right : Vector3Int.up;
            for (int k = 1; k <= 2; k++)
            {
                var cc = doorstep + into * k;
                if (Get(cc.x, cc.y) != K.Cave) continue;
                sol.SetTileFlags(cc, TileFlags.None);
                sol.SetColor(cc, k == 1 ? new Color(0.32f, 0.3f, 0.36f) : new Color(0.55f, 0.52f, 0.58f));
            }
            foreach (var cc in new[] { doorstep + side, doorstep - side, doorstep + side * 2 - into, doorstep - side * 2 })
            {
                if (!Walkable(Get(cc.x, cc.y))) continue;
                Put(decor, cc, D(rng.NextDouble() < 0.6 ? "fern_" + rng.Next(2) : "pebbles_" + rng.Next(2)), Color.white);
                occupied.Add(cc);
            }
        }

        // Coulées de lave : lueur orangée qui palpite
        var env = env0;
        var lavaRoot = new GameObject("Lave").transform;
        lavaRoot.SetParent(env.transform, false);
        for (int i = 0; i < lavaCells.Count; i += 3)
        {
            var go = new GameObject("Lueur de lave");
            go.transform.SetParent(lavaRoot, false);
            go.transform.position = sol.GetCellCenterWorld(lavaCells[i]);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(1f, 0.45f, 0.15f);
            l.intensity = 0.65f;
            l.pointLightOuterRadius = 2.8f;
            l.pointLightInnerRadius = 0.3f;
            go.AddComponent<LavaGlow>();
        }

        // Roche de la montagne : murs auto-connectés (dessus, arêtes, faces)
        SetWalls(murs, wallCells, wallTile);

        // Forêt primordiale, palmiers de la grève, arbres calcinés des hautes terres
        trees0 = PlaceVegetation(env.transform, trunks, trunk, occupied);
        trunks.GetComponent<TilemapRenderer>().enabled = false;
        SetupDepthSorting();

        // Sous-bois : touffes, fleurs des clairières en massifs, fougères de la jungle
        for (int ix = 0; ix < sizeX; ix++)
            for (int iy = 0; iy < sizeY; iy++)
            {
                if (map[ix, iy] != K.Low) continue;
                var c = new Vector3Int(ix + minX, iy + minY, 0);
                if (occupied.Contains(c) || decor.HasTile(c)) continue;
                float f = forest[ix, iy];
                double r = rng.NextDouble();
                if (f < 0.34f)
                {
                    bool patch = Fbm(c.x * 0.25f + o3.y, c.y * 0.25f + o2.x, 2) > 0.58f;
                    int hue = Mathf.FloorToInt(Fbm(c.x * 0.08f, c.y * 0.08f + o1.y, 1) * 7f) % 4;
                    if (patch && r < 0.35) Put(decor, c, D("flowers_" + hue), Color.white);
                    else if (r < 0.08) Put(decor, c, D("tuft_" + rng.Next(3)), Color.white);
                }
                else if (f < 0.56f)
                {
                    if (r < 0.06) Put(decor, c, D("tuft_" + rng.Next(3)), Color.white);
                    else if (r < 0.08) Put(decor, c, D("fern_" + rng.Next(2)), Color.white);
                }
                else if (r < 0.1) Put(decor, c, D("fern_" + rng.Next(2)), new Color(0.85f, 0.9f, 0.85f));
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
                    if (Get(x + ax, y + ay) == K.Path || Get(x + ax, y + ay) == K.Cave || Get(x + ax, y + ay) == K.Lava) nearPath = true;
            if (nearPath) continue;
            ventCells.Add(c);
            Put(decor, c, D("vent"), Color.white);
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
        site.planMarks = new List<PlanMark>(planMarks);
        site.cityRect = cityRect;
        site.cityStreets = new List<Vector3Int>(cityStreets);
        site.cityWall = new List<Vector3Int>(cityWall);
        site.cityGates = new List<Vector3Int>(cityGates);
        site.streetDirt = tDirt;
        site.streetBrick = TerrainArt.Blob("brick");
        if (siteGo.GetComponent<PlanMarkers>() == null) siteGo.AddComponent<PlanMarkers>();
        EditorUtility.SetDirty(site);
        var wear = siteGo.GetComponent<TrailWear>() ?? siteGo.AddComponent<TrailWear>();
        wear.trampled = tTrampled; wear.dirt = tDirt; wear.paved = tPaved;
        EditorUtility.SetDirty(wear);

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

    /// <summary>
    /// Couleur de la mer et des lacs : une texture d'un pixel par case, filtrée en bilinéaire, posée sous tout le reste.
    /// Le dégradé du lagon au grand large est ainsi parfaitement continu (plus de marches case par case).
    /// </summary>
    static void BuildSeaColour(Color[] px, GameObject env)
    {
        const string path = TerrainArt.Dir + "/sea_colour.png";
        var tex = new Texture2D(sizeX, sizeY, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 1f;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.npotScale = TextureImporterNPOTScale.None;
        var st = new TextureImporterSettings();
        imp.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.BottomLeft;
        st.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(st);
        imp.SaveAndReimport();
        var go = new GameObject("Couleur de la mer");
        go.transform.SetParent(env.transform, false);
        go.transform.position = new Vector3(minX, minY, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        sr.sortingOrder = -13;
    }

    /// <summary>
    /// Élargit les étages souterrains de la scène ouverte à LayerW cases, sans régénérer l'île : les cases ajoutées
    /// sont remplies de roche ; la hauteur et la position des étages ne changent pas (sauvegardes compatibles).
    /// </summary>
    public static string WidenUnderground()
    {
        var layers = Object.FindFirstObjectByType<DungeonLayers>();
        var murs = FindTilemap("murs");
        if (layers == null || murs == null) return "no layers/murs";
        int added = 0;
        for (int i = 1; i < layers.layers.Count; i++)
        {
            var old = layers.layers[i].cellBounds;
            if (old.width >= LayerW) continue;
            var rect = new RectInt(-LayerW / 2, old.y, LayerW, old.height);
            var wall = murs.GetTile(new Vector3Int(old.x, old.y, 0));
            var cells = new List<Vector3Int>();
            foreach (var p in rect.allPositionsWithin)
                if (!old.Contains(p)) cells.Add(new Vector3Int(p.x, p.y, 0));
            SetWalls(murs, cells, wall);
            layers.layers[i].cellBounds = rect;
            added += cells.Count;
        }
        EditorUtility.SetDirty(layers);
        EditorUtility.SetDirty(murs);
        EditorSceneManager.MarkSceneDirty(layers.gameObject.scene);
        EditorSceneManager.SaveScene(layers.gameObject.scene);
        return $"added {added} rock cells; " + string.Join(" ", layers.layers.Select(l => l.cellBounds.ToString()));
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
                // végétation par masses : clairières nues, lisières clairsemées, jungle dense
                double dens = f < 0.34f ? 0.0 : f < 0.44f ? 0.1 : f < 0.56f ? 0.45 : 0.9;
                if (rng.NextDouble() > dens) continue;
                kind = f > 0.56f ? (rng.NextDouble() < 0.7 ? BigTree : MidTree)
                     : f > 0.44f ? (rng.NextDouble() < 0.6 ? MidTree : SmallTree)
                     : SmallTree;
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
            // buissons surtout aux lisières (entre clairière et jungle), fleuris côté clairière
            if (rng.NextDouble() > (f > 0.34f && f < 0.5f ? 0.11 : f >= 0.5f ? 0.05 : 0.012)) continue;
            if (NearKind(c, 1, K.Path, K.Cave, K.Dock)) continue;
            string name = rng.NextDouble() < (f < 0.42f ? 0.35 : 0.08) ? flowers[rng.Next(flowers.Length)] : bushes[rng.Next(bushes.Length)];
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
            axis.vector3Value = new Vector3(0f, 1f, 1f);   // z : décalage des pieds des personnages (FeetSort)
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
        if (AstarPath.active == null) AstarPath.active = ap;   // juste après un rechargement du code, A* n'est pas encore initialisé
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
