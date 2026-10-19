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
