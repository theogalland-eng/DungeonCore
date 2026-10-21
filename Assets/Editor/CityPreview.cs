#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// APERÇU DE LA CITÉ DANS L'ÉDITEUR : les bâtiments de la colonie et les intérieurs ne sont pas posés dans la scène
/// (ils naissent en jeu, stade par stade, depuis SettlementArt et les emplacements du générateur). Ce menu les
/// affiche dans la vue Scène pour les regarder ; l'aperçu n'est jamais enregistré dans la scène.
/// </summary>
public static class CityPreview
{
    const string RootName = "[Aperçu] Cité finale (non enregistré)";

    [MenuItem("DungeonCore/Aperçu de la cité finale (vue Scène)")]
    public static void ShowMenu() => Debug.Log(Show());

    [MenuItem("DungeonCore/Effacer l'aperçu de la cité")]
    public static void ClearMenu() => Debug.Log(Clear());

    public static string Show()
    {
        if (Application.isPlaying) return "à faire hors du jeu";
        Clear();
        var site = Object.FindFirstObjectByType<SettlementSite>();
        var art = SettlementArt.Get();
        if (site == null || art == null) return "pas de SettlementSite ou de SettlementArt";
        var root = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
        int n = 0;
        foreach (var plot in site.plots)
        {
            var def = SettlementPlan.Get(plot.buildingId);
            if (def == null || plot.rect.width <= 0) continue;
            var st = art.GetStamp(def.stamp);
            if (st != null)
            {
                var bl = new Vector3(plot.rect.x + (plot.rect.width - st.size.x) * 0.5f, plot.rect.y, 0f);
                var v = StampVisual.Create(st, root.transform, bl, false);
                v.gameObject.hideFlags = HideFlags.DontSave;
                v.name = "plot:" + plot.buildingId + " — " + def.name;
            }
            else
            {
                var frames = art.Frames(def.sprite);
                if (frames == null || frames.Length == 0) continue;
                var go = new GameObject("plot:" + plot.buildingId + " — " + def.name) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(plot.rect.x + plot.rect.width * 0.5f, plot.rect.y + 0.15f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = 3;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
            }
            n++;
        }
        // rues de la cité et enceinte (dessinées en jeu au fil des stades)
        var streets = new GameObject("Rues et enceinte") { hideFlags = HideFlags.DontSave };
        streets.transform.SetParent(root.transform, false);
        foreach (var c in site.cityStreets) Dot(streets.transform, c, new Color(0.78f, 0.6f, 0.42f, 0.55f));
        foreach (var c in site.cityWall) Dot(streets.transform, c, new Color(0.85f, 0.78f, 0.62f, 0.8f));
        // intérieurs : côte à côte au-dessus de l'île
        int k = 0;
        foreach (var st in art.stamps)
        {
            if (!st.id.StartsWith("int_")) continue;
            var v = StampVisual.Create(st, root.transform, new Vector3(-90f + k * 16f, 90f, 0f), false);
            v.gameObject.hideFlags = HideFlags.DontSave;
            v.name = "Intérieur " + st.id;
            k++;
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        return $"aperçu : {n} bâtiments aux emplacements du plan, {k} intérieurs au nord de l'île (x -90, y 90) — non enregistré dans la scène";
    }

    static void Dot(Transform parent, Vector3Int c, Color col)
    {
        var go = new GameObject("case");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteUtil.Square;
        sr.color = col;
        sr.sortingOrder = -2;
    }

    /// <summary>
    /// Les bâtiments déplacés dans l'aperçu gardent leur nouvelle place : emplacements de la scène mis à jour, et
    /// déplacements notés dans Assets/Art/Generated/PlotOverrides.json (le générateur d'île les réapplique).
    /// </summary>
    [MenuItem("DungeonCore/Enregistrer les déplacements de l'aperçu")]
    public static void SaveMenu() => Debug.Log(SaveMoves());

    public const string OverridesPath = "Assets/Art/Generated/PlotOverrides.json";

    [System.Serializable] public class Move { public string id; public int x, y; }
    [System.Serializable] public class Moves { public System.Collections.Generic.List<Move> list = new System.Collections.Generic.List<Move>(); }

    public static Moves LoadMoves()
    {
        if (!System.IO.File.Exists(OverridesPath)) return new Moves();
        return JsonUtility.FromJson<Moves>(System.IO.File.ReadAllText(OverridesPath)) ?? new Moves();
    }

    public static string SaveMoves()
    {
        var site = Object.FindFirstObjectByType<SettlementSite>();
        var art = SettlementArt.Get();
        if (site == null) return "pas de SettlementSite";
        GameObject root = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>()) if (go != null && go.name == RootName && go.scene.IsValid()) root = go;
        if (root == null) return "pas d'aperçu ouvert";
        var moves = LoadMoves();
        int n = 0;
        foreach (Transform t in root.transform)
        {
            if (!t.name.StartsWith("plot:")) continue;
            string id = t.name.Substring(5).Split(' ')[0];
            var plot = site.plots.Find(p => p.buildingId == id);
            var def = SettlementPlan.Get(id);
            if (plot == null || def == null) continue;
            var st = art != null ? art.GetStamp(def.stamp) : null;
            // origine de l'emprise d'après la position de l'objet (coin bas-gauche du tampon, ou pied du sprite)
            Vector3 p0 = t.position;
            int x = st != null ? Mathf.RoundToInt(p0.x - (plot.rect.width - st.size.x) * 0.5f) : Mathf.RoundToInt(p0.x - plot.rect.width * 0.5f);
            int y = st != null ? Mathf.RoundToInt(p0.y) : Mathf.RoundToInt(p0.y - 0.15f);
            if (x == plot.rect.x && y == plot.rect.y) continue;
            plot.rect = new RectInt(x, y, plot.rect.width, plot.rect.height);
            moves.list.RemoveAll(m => m.id == id);
            moves.list.Add(new Move { id = id, x = x, y = y });
            n++;
        }
        System.IO.File.WriteAllText(OverridesPath, JsonUtility.ToJson(moves, true));
        AssetDatabase.ImportAsset(OverridesPath);
        EditorUtility.SetDirty(site);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(site.gameObject.scene);
        return n > 0 ? $"{n} bâtiment(s) déplacé(s) : emplacements enregistrés (scène à sauvegarder ; re-précalculer le graphe : PathCacheBaker.Bake)" : "aucun déplacement";
    }

    public static string Clear()
    {
        int n = 0;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go != null && go.name == RootName && go.scene.IsValid()) { Object.DestroyImmediate(go); n++; }
        return n > 0 ? "aperçu effacé" : "pas d'aperçu";
    }
}
#endif
