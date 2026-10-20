#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// ENDURANCE (pont Claude) : une partie Noyau accélérée avec un donjon peuplé et des expéditions régulières,
/// relevée toutes les 30 s dans Temp/soak.log (mémoire, objets, ressources, temps d'image) pour trouver
/// ce qui grossit au fil d'une longue partie.
/// </summary>
public static class SoakTest
{
    static readonly string LogPath = Path.Combine(Application.dataPath, "../Temp/soak.log");
    static bool running;
    static double startReal, lastSample, lastLaunch;
    static int frames;
    static float sumDt, worstDt;
    static Dictionary<string, int> firstGroups;
    static CrawlerDungeon.Floor floor;
    static DungeonCore core;
    static double lastRefill;
    static readonly System.Random rng = new System.Random(7);
    public static float speed = 5f;

    public static string Start()
    {
        if (!Application.isPlaying) return "pas en jeu";
        DevInspect.SkipDialogues();
        // un donjon peuplé : plan d'étage autour du cristal, monstres, pièges, coffres
        var f = CrawlerDungeon.Generate(1, 1234);
        CrawlerDungeon.Populate(f, new System.Random(5), _ => false);
        floor = f;
        core = Object.FindFirstObjectByType<DungeonCore>();
        ManaSystem.Instance?.Add(5000);
        GameManager.Instance.SetSpeed(speed);
        File.WriteAllText(LogPath, "");
        running = true;
        startReal = lastSample = EditorApplication.timeSinceStartup;
        lastLaunch = -999;
        frames = 0; sumDt = 0f; worstDt = 0f;
        firstGroups = Groups();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Sample();
        return $"endurance lancée : {f.rooms.Count} salles, {GobelinAI.All.Count} monstres, ×{speed}";
    }

    public static string Stop()
    {
        running = false;
        EditorApplication.update -= Tick;
        return "endurance arrêtée";
    }

    /// <summary>Durée d'une sauvegarde automatique : capture, conversion JSON, écriture.</summary>
    public static string SaveCost()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var d = SaveSystem.Instance.Capture();
        long cap = sw.ElapsedMilliseconds; sw.Restart();
        string json = JsonUtility.ToJson(d);
        long ser = sw.ElapsedMilliseconds; sw.Restart();
        File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/savecost.json"), json);
        long wr = sw.ElapsedMilliseconds;
        return $"capture {cap} ms, JSON {ser} ms ({json.Length / 1024} Ko), écriture {wr} ms";
    }

    /// <summary>Ombres de contact et détails du sol : nombre de tuiles posées, et vue rapprochée du cristal.</summary>
    public static string ShadeShot()
    {
        string n = "";
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
            if (tm.name.StartsWith("Ombres") || tm.name == "Détails du sol") { tm.CompressBounds(); int k = 0; foreach (var c in tm.cellBounds.allPositionsWithin) if (tm.HasTile(c)) k++; n += $"{tm.name}={k} "; }
        var core = Object.FindFirstObjectByType<DungeonCore>().transform.position;
        return n + DevInspect.Shot(core.x - 1.5f, core.y + 1f, 2.6f, 1600, 1000, "shade");
    }

    public static string Report() => File.Exists(LogPath) ? File.ReadAllText(LogPath) : "aucun relevé";

    /// <summary>Objets dont le nombre a le plus augmenté depuis le début.</summary>
    public static string Growth()
    {
        if (firstGroups == null) return "pas de référence";
        var now = Groups();
        var diff = now.Select(kv => (kv.Key, d: kv.Value - (firstGroups.TryGetValue(kv.Key, out int v) ? v : 0)))
            .Where(x => x.d != 0).OrderByDescending(x => x.d).Take(25);
        return string.Join(", ", diff.Select(x => $"{x.Key} {(x.d > 0 ? "+" : "")}{x.d}"));
    }

    static Dictionary<string, int> Groups()
    {
        string Key(string n) { int i = n.IndexOfAny("0123456789(_ ".ToCharArray()); return i > 2 ? n.Substring(0, i) : n; }
        return Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .GroupBy(g => Key(g.name)).ToDictionary(g => g.Key, g => g.Count());
    }

    static void Tick()
    {
        if (!running) return;
        if (!Application.isPlaying) { Stop(); return; }
        frames++;
        float dt = Time.unscaledDeltaTime;
        sumDt += dt;
        worstDt = Mathf.Max(worstDt, dt);
        double now = EditorApplication.timeSinceStartup;
        // dialogues et cinématiques passés, vitesse maintenue, une expédition dès que le donjon est calme
        if (StoryManager.Instance != null && StoryManager.Instance.IsShowing) DevInspect.SkipDialogues();
        if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying) CinematicDirector.Instance.Skip();
        if (Time.timeScale > 0f && Time.timeScale < speed && !(MainMenu.Instance != null && MainMenu.Instance.InPause)) GameManager.Instance.SetSpeed(speed);
        // le cristal ne tombe pas pendant l'essai ; le donjon se repeuple
        if (core != null) core.currentHealth = core.maxHealth;
        if (now - lastRefill > 45 && GobelinAI.All.Count < 10 && floor != null)
        {
            lastRefill = now;
            for (int i = 0; i < 4; i++)
            {
                var room = floor.rooms[1 + rng.Next(floor.rooms.Count - 1)];
                CrawlerDungeon.SpawnMonster(CrawlerDungeon.RandomFamily(2, rng), 1 + rng.Next(2), room.Center, floor);
            }
        }
        if (AdventurerAI.All.Count == 0 && now - lastLaunch > 20)
        {
            lastLaunch = now;
            ExpeditionManager.Instance?.LaunchExpedition();
        }
        if (now - lastSample >= 30) { lastSample = now; Sample(); }
    }

    static void Sample()
    {
        int Count<T>() where T : Object => Resources.FindObjectsOfTypeAll<T>().Length;
        int Runtime<T>() where T : Object => Resources.FindObjectsOfTypeAll<T>().Count(o => !EditorUtility.IsPersistent(o));
        var sb = new StringBuilder();
        float t = (float)(EditorApplication.timeSinceStartup - startReal);
        float avg = frames > 0 ? sumDt / frames * 1000f : 0f;
        sb.Append($"t={t:0}s jour={GameManager.Instance.CurrentDay} img={avg:0.0}ms pire={worstDt * 1000f:0}ms");
        sb.Append($" | mono={Profiler.GetMonoUsedSizeLong() / 1048576f:0.0}Mo alloué={Profiler.GetTotalAllocatedMemoryLong() / 1048576f:0}Mo réservé={Profiler.GetTotalReservedMemoryLong() / 1048576f:0}Mo");
        sb.Append($" | GO={Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
        sb.Append($" mesh={Count<Mesh>()}(exé {Runtime<Mesh>()}) mat={Count<Material>()}(exé {Runtime<Material>()}) tex={Count<Texture2D>()}(exé {Runtime<Texture2D>()}) spr={Runtime<Sprite>()}");
        sb.Append($" | monstres={GobelinAI.All.Count} intrus={AdventurerAI.All.Count} objets={PlacedObject.All.Count}");
        sb.Append($" ps={Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length} l2d={Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None).Length}");
        sb.Append($" sr={Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length} tmp={Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
        sb.Append($" audio={Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length} mb={Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Length}");
        sb.Append(System.Environment.NewLine);
        File.AppendAllText(LogPath, sb.ToString());
        frames = 0; sumDt = 0f; worstDt = 0f;
    }
}
#endif
