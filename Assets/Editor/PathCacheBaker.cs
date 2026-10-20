#if UNITY_EDITOR
using System.IO;
using Pathfinding;
using Pathfinding.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// GRAPHE DE DÉPLACEMENT PRÉCALCULÉ : au lieu de scanner toute l'île au lancement (≈ 0,5 s sur ordinateur,
/// plusieurs secondes sur téléphone), le graphe est scanné une fois ici et enregistré dans la scène (cache de
/// démarrage d'A*). À refaire chaque fois que la carte de la scène change (île régénérée, étages élargis...).
/// </summary>
public static class PathCacheBaker
{
    const string CachePath = "Assets/Art/Generated/PathCache.bytes";

    [MenuItem("DungeonCore/Précalculer le graphe de déplacement")]
    public static void BakeMenu() => Debug.Log(Bake());

    public static string Bake()
    {
        if (Application.isPlaying) return "à faire hors du jeu";
        var ap = Object.FindFirstObjectByType<AstarPath>();
        if (ap == null) return "pas d'AstarPath";
        if (AstarPath.active == null) AstarPath.active = ap;
        if (ap.data.graphs == null || ap.data.graphs.Length == 0) ap.data.DeserializeGraphs();
        var gg = ap.data.gridGraph;
        if (gg == null) return "pas de grille";
        gg.cutCorners = false;                  // comme AdventurerAI.FixGraphCorners au lancement
        // collisions des tilemaps à jour (juste après une génération, l'éditeur ne les a pas encore reconstruites)
        foreach (var tc in Object.FindObjectsByType<UnityEngine.Tilemaps.TilemapCollider2D>(FindObjectsSortMode.None)) tc.ProcessTilemapChanges();
        foreach (var cc in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None)) cc.GenerateGeometry();
        Physics2D.SyncTransforms();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ap.Scan();
        long ms = sw.ElapsedMilliseconds;
        int walkable = Walkable(gg);
        var bytes = ap.data.SerializeGraphs(new SerializeSettings { nodes = true });
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CachePath));
        File.WriteAllBytes(CachePath, bytes);
        AssetDatabase.ImportAsset(CachePath);
        ap.data.file_cachedStartup = AssetDatabase.LoadAssetAtPath<TextAsset>(CachePath);
        ap.data.cacheStartup = true;
        ap.data.SetData(ap.data.SerializeGraphs());
        EditorUtility.SetDirty(ap);
        EditorSceneManager.MarkSceneDirty(ap.gameObject.scene);
        EditorSceneManager.SaveScene(ap.gameObject.scene);
        return $"graphe précalculé : {gg.width}x{gg.depth}, {walkable} cases praticables, scan {ms} ms, cache {bytes.Length / 1024} Ko";
    }

    /// <summary>Allège les tuiles animées de la scène une fois pour toutes (copies fixes enregistrées en fichiers).</summary>
    [MenuItem("DungeonCore/Alléger les tuiles animées (scène)")]
    public static void TileDietMenu() => Debug.Log(BakeTileDiet());

    public static string BakeTileDiet()
    {
        if (Application.isPlaying) return "à faire hors du jeu";
        const string dir = "Assets/Art/Generated/FrozenTiles";
        Directory.CreateDirectory(dir);
        int made = 0;
        TileDiet.MakeCopy = (name, sprite, col) =>
        {
            string file = $"{dir}/{name}_{(sprite != null ? sprite.name : "vide")}_{col}.asset".Replace(" ", "_");
            var t = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(file);
            if (t != null) return t;
            t = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            t.name = name;                       // les requêtes testent le nom (« sea »)
            t.sprite = sprite;
            t.colliderType = col;
            t.flags = UnityEngine.Tilemaps.TileFlags.None;
            AssetDatabase.CreateAsset(t, file);
            made++;
            return t;
        };
        string r;
        try { r = TileDiet.Run(true); }
        finally { TileDiet.MakeCopy = null; }
        AssetDatabase.SaveAssets();
        UnityEngine.SceneManagement.Scene scene = default;
        foreach (var tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None)) { EditorUtility.SetDirty(tm); scene = tm.gameObject.scene; }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{r} — {made} tuiles fixes créées";
    }

    /// <summary>En jeu : scan complet à l'instant (référence pour vérifier le précalcul).</summary>
    public static string LiveScan()
    {
        AstarPath.active.Scan();
        return "scan en jeu : " + Check();
    }

    /// <summary>En jeu : cases praticables du graphe chargé (pour comparer au précalcul).</summary>
    public static string Check()
    {
        var gg = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        return gg == null ? "pas de grille" : $"{Walkable(gg)} cases praticables, cache={AstarPath.active.data.cacheStartup && AstarPath.active.data.file_cachedStartup != null}";
    }

    static int Walkable(GridGraph gg)
    {
        int n = 0;
        gg.GetNodes(node => { if (node.Walkable) n++; });
        return n;
    }
}
#endif
