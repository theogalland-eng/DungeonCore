using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Mesure des performances par le pont Claude : enregistre N images au profileur puis classe les fonctions
/// (scripts du jeu, physique, rendu) par temps propre moyen par image. Ignore la boucle de l'éditeur.
/// </summary>
public static class PerfProbe
{
    public static string Start()
    {
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        Profiler.enabled = true;
        return "profileur lancé";
    }

    public static string Worst() => ReportWorst(6);
    /// <summary>Coût de rendu de l'image courante (appels de dessin, lots, triangles) et caméra.</summary>
    public static string Gpu()
    {
        var cam = Camera.main;
        int lights = 0; foreach (var l in Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None)) if (l.isActiveAndEnabled) lights++;
        int srs = 0; foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) if (r.isVisible) srs++;
        return $"draw={UnityEditor.UnityStats.drawCalls} batches={UnityEditor.UnityStats.batches} setpass={UnityEditor.UnityStats.setPassCalls} tris={UnityEditor.UnityStats.triangles} " +
               $"cam={cam.orthographicSize:0.0} lights={lights} visibleSprites={srs} cine={(CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying)}";
    }

    public static string Report()
    {
        ProfilerDriver.enabled = false;
        int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
        if (first < 0 || last < first) return "aucune image";
        var self = new Dictionary<string, double>();
        var calls = new Dictionary<string, long>();
        var gc = new Dictionary<string, double>();
        double frameMs = 0, playerLoopMs = 0, gcTotal = 0;
        float maxFrame = 0f;
        int frames = 0;
        for (int f = first; f <= last; f++)
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                       HierarchyFrameDataView.columnSelfTime, false))
            {
                if (view == null || !view.valid) continue;
                frames++;
                frameMs += view.frameTimeMs;
                maxFrame = Mathf.Max(maxFrame, view.frameTimeMs);
                var stack = new Stack<(int id, bool inPlayer, string parent)>();
                stack.Push((view.GetRootItemID(), false, ""));
                var children = new List<int>();
                while (stack.Count > 0)
                {
                    var (id, inPlayer, parentName) = stack.Pop();
                    string name = view.GetItemName(id);
                    bool player = inPlayer || name == "PlayerLoop";
                    if (name == "PlayerLoop") playerLoopMs += view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
                    if (name == "EditorLoop") continue;
                    if (player)
                    {
                        float s = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
                        self.TryGetValue(name, out double v); self[name] = v + s;
                        calls.TryGetValue(name, out long c); calls[name] = c + (long)view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls);
                        // allocations : GC.Alloc est un enfant, on l'attribue à son parent
                        if (name == "GC.Alloc")
                        {
                            double b = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory);
                            gcTotal += b;
                            string parent = parentName;
                            gc.TryGetValue(parent, out double g); gc[parent] = g + b;
                        }
                    }
                    children.Clear();
                    view.GetItemChildren(id, children);
                    foreach (var ch in children) stack.Push((ch, player, name));
                }
            }
        }
        if (frames == 0) return "aucune image lisible";
        var sb = new StringBuilder();
        sb.Append($"{frames} images, image {frameMs / frames:0.00} ms (pire {maxFrame:0.0} ms), PlayerLoop {playerLoopMs / frames:0.00} ms, GC {gcTotal / frames / 1024:0.0} Ko/image\n");
        foreach (var kv in gc.OrderByDescending(k => k.Value).Take(18))
            sb.Append($"  GC {kv.Value / frames / 1024,6:0.00} Ko  {kv.Key}\n");
        foreach (var kv in self.OrderByDescending(k => k.Value).Take(45))
            sb.Append($"{kv.Value / frames,7:0.000} ms  x{calls[kv.Key] / (double)frames,7:0.0}  {kv.Key}\n");
        return sb.ToString();
    }

    /// <summary>Les k images les plus lentes (lancement, saccades) et ce qui les remplit (y compris le chargement).</summary>
    public static string ReportWorst(int k = 6)
    {
        ProfilerDriver.enabled = false;
        int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
        if (first < 0) return "aucune image";
        var frames = new List<(int f, float ms)>();
        for (int f = first; f <= last; f++)
            using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
                if (v != null && v.valid) frames.Add((f, v.frameTimeMs));
        var sb = new StringBuilder($"{frames.Count} images, total {frames.Sum(x => x.ms) / 1000f:0.0} s | ");
        foreach (var (f, ms) in frames.OrderByDescending(x => x.ms).Take(k))
        {
            sb.Append($" || image {f - first} : {ms:0} ms");
            using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
            {
                var tot = new Dictionary<string, float>();
                var stack = new Stack<int>(); stack.Push(v.GetRootItemID());
                var ch = new List<int>();
                while (stack.Count > 0)
                {
                    int id = stack.Pop();
                    string n = v.GetItemName(id);
                    float t = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
                    tot.TryGetValue(n, out float o); tot[n] = o + t;
                    ch.Clear(); v.GetItemChildren(id, ch); foreach (var c in ch) stack.Push(c);
                }
                foreach (var kv in tot.OrderByDescending(x => x.Value).Take(10)) sb.Append($" | {kv.Value:0.0} ms {kv.Key}");
            }
        }
        return sb.ToString();
    }

    /// <summary>Ce qui s'accumule au fil de la partie : objets par nom, ressources créées en mémoire, tas.</summary>
    public static string Leaks()
    {
        var sb = new StringBuilder();
        var gos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.Append($"GameObjects {gos.Length} | Texture2D {Resources.FindObjectsOfTypeAll<Texture2D>().Length} | Sprite {Resources.FindObjectsOfTypeAll<Sprite>().Length}");
        sb.Append($" | Material {Resources.FindObjectsOfTypeAll<Material>().Length} | Mesh {Resources.FindObjectsOfTypeAll<Mesh>().Length} | AudioClip {Resources.FindObjectsOfTypeAll<AudioClip>().Length}");
        sb.Append($" | ScriptableObject {Resources.FindObjectsOfTypeAll<ScriptableObject>().Length}");
        sb.Append($" | tas mono {Profiler.GetMonoUsedSizeLong() / 1048576f:0.0} Mo, alloué {Profiler.GetTotalAllocatedMemoryLong() / 1048576f:0} Mo");
        string Key(string n) { int i = n.IndexOfAny("0123456789(_ ".ToCharArray()); return i > 2 ? n.Substring(0, i) : n; }
        var groups = gos.GroupBy(g => Key(g.name)).OrderByDescending(g => g.Count()).Take(25);
        sb.Append(" || " + string.Join(", ", groups.Select(g => $"{g.Key} {g.Count()}")));
        return sb.ToString();
    }

    /// <summary>Ressources créées à l'exécution (pas des fichiers du projet), regroupées par nom.</summary>
    public static string RuntimeAssets()
    {
        string Group<T>(string label) where T : Object
        {
            var all = Resources.FindObjectsOfTypeAll<T>().Where(o => !UnityEditor.EditorUtility.IsPersistent(o)).ToList();
            var g = all.GroupBy(o => string.IsNullOrEmpty(o.name) ? "(sans nom)" : o.name).OrderByDescending(x => x.Count()).Take(8);
            return $"{label} {all.Count} : " + string.Join(", ", g.Select(x => $"{x.Key} {x.Count()}"));
        }
        string shaders = string.Join(", ", Resources.FindObjectsOfTypeAll<Material>().Where(m => !UnityEditor.EditorUtility.IsPersistent(m))
            .GroupBy(m => m.shader != null ? m.shader.name : "?").OrderByDescending(x => x.Count()).Take(6).Select(x => $"{x.Key} {x.Count()}"));
        return Group<Material>("MAT") + " | shaders : " + shaders + " || " + Group<Sprite>("SPR") + " || " + Group<Mesh>("MESH") + " || " + Group<Texture2D>("TEX");
    }

    /// <summary>Mémoire des textures chargées par la scène de jeu (les plus grosses d'abord).</summary>
    public static string TextureMemory()
    {
        var used = new HashSet<Texture>();
        foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (r.sprite != null) used.Add(r.sprite.texture);
        var all = Resources.FindObjectsOfTypeAll<Texture2D>();
        long total = 0; foreach (var t in all) total += Profiler.GetRuntimeMemorySizeLong(t);
        var top = all.OrderByDescending(t => Profiler.GetRuntimeMemorySizeLong(t)).Take(15)
            .Select(t => $"{t.name} {t.width}x{t.height} {t.format} {Profiler.GetRuntimeMemorySizeLong(t) / 1048576f:0.0}Mo");
        long sum = 0; int n = 0;
        foreach (var t in all) { if (UnityEditor.AssetDatabase.GetAssetPath(t).StartsWith("Assets/")) { sum += Profiler.GetRuntimeMemorySizeLong(t); n++; } }
        return $"textures en mémoire {all.Length} = {total / 1048576f:0} Mo (dont projet {n} = {sum / 1048576f:0} Mo) | " + string.Join(" ; ", top);
    }

    /// <summary>Compression Android (ASTC 4×4, 8 bits/pixel au lieu de 32) des textures de sprites ; « filter » limite aux chemins contenant ce texte.</summary>
    public static string AndroidAstc(string filter)
    {
        int n = 0;
        UnityEditor.AssetDatabase.StartAssetEditing();   // une seule passe d'import pour tout le lot
        try {
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(filter) && !path.Contains(filter)) continue;
            var imp = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (imp == null || imp.textureType != UnityEditor.TextureImporterType.Sprite) continue;
            var ps = imp.GetPlatformTextureSettings("Android");
            if (ps.overridden && ps.format == UnityEditor.TextureImporterFormat.ASTC_4x4) continue;
            ps.overridden = true;
            ps.format = UnityEditor.TextureImporterFormat.ASTC_4x4;
            ps.compressionQuality = 100;
            ps.maxTextureSize = imp.maxTextureSize;
            imp.SetPlatformTextureSettings(ps);
            imp.SaveAndReimport();
            n++;
        }
        } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
        return $"{n} textures passées en ASTC 4x4 (Android)";
    }
    public static string AstcAll() => AndroidAstc("");
    public static string AstcTest() => AndroidAstc("Characters8/goblin_2") + " | " + AndroidAstc("Characters8/orc_3") + " | " + AndroidAstc("Characters8/spider_2");

    public static string AnimatedTiles()
    {
        var sb = new StringBuilder();
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
        {
            var bnd = tm.cellBounds; var block = tm.GetTilesBlock(bnd);
            int n = 0, anim = 0;
            foreach (var t in block) { if (t == null) continue; n++; if (t is PhaseTile || t is AnimatedTileLite) anim++; }
            if (n > 0) sb.Append($"[{tm.name} tuiles={n} animées={anim} actif={tm.gameObject.activeInHierarchy} fps={tm.animationFrameRate}] ");
        }
        return sb.ToString();
    }

    public static string FreezeSea() { foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None)) if (tm.name == "T_Eau") tm.animationFrameRate = 0f; return "mer figée"; }

    /// <summary>Ce qui coûte au rendu et à la logique : lumières, ombres, rendus, IA, physique.</summary>
    public static string Census()
    {
        var sb = new StringBuilder();
        var lights = Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None);
        sb.Append($"Light2D {lights.Length} (actives {lights.Count(l => l.isActiveAndEnabled)}, ombres {lights.Count(l => l.isActiveAndEnabled && l.shadowsEnabled)}, global {lights.Count(l => l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global)})");
        sb.Append($" | ShadowCaster2D {Object.FindObjectsByType<UnityEngine.Rendering.Universal.ShadowCaster2D>(FindObjectsSortMode.None).Length}");
        sb.Append($" | SpriteRenderer {Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Count(r => r.isVisible)} visibles / {Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length}");
        sb.Append($" | ParticleSystem {Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length}");
        sb.Append($" | Animator {Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Length}");
        sb.Append($" | Rigidbody2D {Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None).Length}");
        sb.Append($" | Collider2D {Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).Length}");
        sb.Append($" | MonoBehaviour {Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Length}");
        sb.Append($" | monstres {GobelinAI.All.Count} intrus {AdventurerAI.All.Count}");
        sb.Append($" | TMP {Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Length}");
        sb.Append($" | AudioSource {Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length}");
        var byType = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).GroupBy(m => m.GetType().Name).OrderByDescending(g => g.Count()).Take(15);
        sb.Append("\nscripts : " + string.Join(", ", byType.Select(g => $"{g.Key} {g.Count()}")));
        return sb.ToString();
    }
}
