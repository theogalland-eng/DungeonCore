#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// ATELIER DE LA CITÉ (éditeur) : génère à côté de l'île la cité finale (bâtiments, rues, enceintes, décor) et, plus
/// loin, chaque intérieur, comme objets de la scène que l'on retouche librement (déplacer, supprimer, ajouter des
/// objets, peindre les rues). Le jeu copie ces modèles à leur place sur l'île (CityWorkshop).
/// </summary>
public static class CityWorkshopBuilder
{
    const string RootName = "Atelier de la cité (modifiable)";
    static readonly Vector3 CityOffset = new Vector3(300f, 0f, 0f);
    static readonly Vector3 InteriorsOrigin = new Vector3(480f, -40f, 0f);

    [MenuItem("DungeonCore/Atelier de la cité/Générer l'atelier (remplace les retouches !)")]
    public static void GenerateMenu()
    {
        if (CityWorkshop.Instance != null &&
            !EditorUtility.DisplayDialog("Atelier de la cité", "L'atelier existe déjà : le régénérer efface toutes les retouches faites à la main. Continuer ?", "Régénérer", "Annuler"))
            return;
        Debug.Log(Generate());
    }

    [MenuItem("DungeonCore/Atelier de la cité/Régénérer l'élément sélectionné")]
    public static void RegenerateMenu() => Debug.Log(RegenerateSelected());

    [MenuItem("DungeonCore/Atelier de la cité/Afficher le sol de l'île sous l'atelier")]
    public static void GroundMenu()
    {
        var ws = CityWorkshop.Instance;
        var site = Object.FindFirstObjectByType<SettlementSite>();
        if (ws == null || site == null) { Debug.Log("pas d'atelier"); return; }
        Debug.Log($"sol de référence : {CopyGround(ws, site)} tuiles (non enregistrées)");
    }

    [MenuItem("DungeonCore/Atelier de la cité/Voir la cité")]
    public static void FrameCity() => Frame(CityWorkshop.Instance != null ? CityWorkshop.Instance.buildings : null);

    [MenuItem("DungeonCore/Atelier de la cité/Voir les intérieurs")]
    public static void FrameInteriors() => Frame(CityWorkshop.Instance != null ? CityWorkshop.Instance.interiors : null);

    static void Frame(Transform t)
    {
        if (t == null) { Debug.Log("pas d'atelier : DungeonCore › Atelier de la cité › Générer l'atelier"); return; }
        Selection.activeGameObject = t.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    static Transform Node(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        return t;
    }

    public static string Generate()
    {
        if (Application.isPlaying) return "à faire hors du jeu";
        var site = Object.FindFirstObjectByType<SettlementSite>();
        var art = SettlementArt.Get();
        if (site == null || art == null) return "pas de SettlementSite ou de SettlementArt";
        var old = Object.FindFirstObjectByType<CityWorkshop>(FindObjectsInactive.Include);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject(RootName);
        var ws = root.AddComponent<CityWorkshop>();
        ws.cityOffset = CityOffset;
        ws.buildings = Node(root.transform, "Bâtiments (chacun copié à sa place sur l'île)", Vector3.zero);
        ws.wallsStone = Node(root.transform, "Enceinte — remparts (cité fortifiée)", CityOffset);
        ws.wallsPalisade = Node(root.transform, "Enceinte — palissade (village)", CityOffset);
        ws.decor = Node(root.transform, "Décor de la cité (arbres, objets ajoutés)", CityOffset);
        ws.interiors = Node(root.transform, "Intérieurs", InteriorsOrigin);
        ws.reference = Node(root.transform, "Sol de l'île (référence, non utilisé en jeu)", CityOffset);
        ws.wallsPalisade.gameObject.SetActive(false);   // visible à la demande (même tracé que les remparts)

        int nb = 0;
        foreach (var plot in site.plots) if (BuildTemplate(ws, site, art, plot) != null) nb++;
        CityWalls.BuildGroup(site, CityWalls.Kind.Stone, ws.wallsStone);
        CityWalls.BuildGroup(site, CityWalls.Kind.Palisade, ws.wallsPalisade);
        // rues : une grille posée sur le décalage, ses cases sont celles de l'île
        var grid = Node(root.transform, "Rues de la cité (peindre ici, tuile « terrain_brick »)", CityOffset).gameObject.AddComponent<Grid>();
        var streets = new GameObject("Rues").AddComponent<Tilemap>();
        streets.transform.SetParent(grid.transform, false);
        streets.gameObject.AddComponent<TilemapRenderer>().sortingOrder = -4;
        foreach (var c in site.cityStreets) streets.SetTile(c, site.streetBrick);
        ws.streets = streets;
        int trees = CopyDecor(ws, site);
        int tiles = CopyGround(ws, site);
        int rooms = BuildInteriors(ws);

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject = ws.buildings.gameObject;
        return $"atelier : {nb} bâtiments, {site.cityStreets.Count} cases de rues, {trees} arbres de décor, {tiles} tuiles de sol, {rooms} intérieurs — cité à x+{CityOffset.x}, intérieurs à {InteriorsOrigin}";
    }

    /// <summary>Modèle d'un bâtiment : la scène du pack (ou le sprite) à la place qu'il aura sur l'île, décalée.</summary>
    static GameObject BuildTemplate(CityWorkshop ws, SettlementSite site, SettlementArt art, SettlementPlot plot)
    {
        var def = SettlementPlan.Get(plot.buildingId);
        if (def == null || plot.rect.width <= 0) return null;
        var st = art.GetStamp(def.stamp);
        GameObject go;
        Vector3 bl, door;
        if (st != null)
        {
            bl = new Vector3(plot.rect.x + (plot.rect.width - st.size.x) * 0.5f, plot.rect.y, 0f);
            door = (Vector3)st.door + new Vector3(0f, 0.3f, 0f);
            go = StampVisual.Create(st, ws.buildings, bl + CityOffset, true).gameObject;
        }
        else
        {
            var frames = art.Frames(def.sprite);
            if (frames == null || frames.Length == 0 || frames[0] == null) return null;
            bl = new Vector3(plot.rect.x, plot.rect.y, 0f);
            door = new Vector3(plot.rect.width * 0.5f, 0.15f, 0f);
            go = new GameObject();
            go.transform.SetParent(ws.buildings, false);
            go.transform.position = bl + CityOffset;
            go.AddComponent<StampVisual>();
            var sp = new GameObject("Sprite");
            sp.transform.SetParent(go.transform, false);
            sp.transform.localPosition = door;
            var sr = sp.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (frames.Length > 1) { var part = sp.AddComponent<StampPart>(); part.frames = frames; part.fps = 8f; }
            if (def.id != "field_1" && def.id != "field_2")
            {
                var col = sp.AddComponent<BoxCollider2D>();
                sp.layer = StampVisual.ObstacleLayer;
                col.size = new Vector2(Mathf.Max(0.8f, plot.rect.width - 0.6f), Mathf.Max(0.6f, plot.rect.height - 0.8f));
                col.offset = new Vector2(0f, col.size.y * 0.5f - 0.15f);
            }
        }
        go.name = $"plot:{def.id} — {def.name}";
        var ct = go.AddComponent<CityTemplate>();
        ct.id = def.id;
        ct.islandOrigin = bl;
        ct.rect = plot.rect;
        ct.doorLocal = door;
        return go;
    }

    /// <summary>Arbres gardés dans l'enceinte (jardins, vergers) : copiés dans le décor, à retoucher.</summary>
    static int CopyDecor(CityWorkshop ws, SettlementSite site)
    {
        var forest = GameObject.Find("Forêt");
        if (forest == null) return 0;
        var r = site.cityRect;
        var keepOut = new HashSet<Vector3Int>(site.cityStreets);
        foreach (var c in site.cityWall) keepOut.Add(c);
        foreach (var pl in site.plots)
            if (r.Overlaps(pl.rect))
                for (int x = pl.rect.x; x < pl.rect.xMax; x++)
                    for (int y = pl.rect.y - 2; y < pl.rect.yMax - 1; y++) keepOut.Add(new Vector3Int(x, y, 0));
        int n = 0;
        foreach (Transform t in forest.transform)
        {
            var c = Vector3Int.FloorToInt(t.position);
            if (!r.Contains(new Vector2Int(c.x, c.y))) continue;
            if (keepOut.Contains(c)) continue;
            uint h = (uint)(c.x * 73856093 ^ c.y * 19349663);
            if (h % 100 >= 55) continue;
            var copy = Object.Instantiate(t.gameObject, ws.decor);
            copy.name = t.name;
            copy.transform.position = t.position + CityOffset;
            copy.layer = StampVisual.ObstacleLayer;
            if (copy.GetComponent<Collider2D>() == null)
            {
                var col = copy.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.5f, 0.3f);
                col.offset = new Vector2(0f, 0.15f);
            }
            n++;
        }
        return n;
    }

    /// <summary>Sol de l'île sous la cité (référence visuelle pour retoucher ; le jeu ne s'en sert pas).</summary>
    static int CopyGround(CityWorkshop ws, SettlementSite site)
    {
        var area = site.cityRect;
        foreach (var pl in site.plots)
        {
            int x0 = Mathf.Min(area.xMin, pl.rect.xMin), y0 = Mathf.Min(area.yMin, pl.rect.yMin);
            int x1 = Mathf.Max(area.xMax, pl.rect.xMax), y1 = Mathf.Max(area.yMax, pl.rect.yMax);
            area = new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
        area = new RectInt(area.x - 6, area.y - 6, area.width + 12, area.height + 12);
        // jamais enregistré dans la scène (des dizaines de Mo) : recréé à la demande par le menu de l'atelier
        for (int k = ws.reference.childCount - 1; k >= 0; k--) Object.DestroyImmediate(ws.reference.GetChild(k).gameObject);
        var holder = new GameObject("Sol (non enregistré — menu « Afficher le sol de l'île sous l'atelier »)") { hideFlags = HideFlags.DontSave };
        holder.transform.SetParent(ws.reference, false);
        var grid = holder.AddComponent<Grid>();
        int n = 0;
        foreach (var src in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (!src.name.StartsWith("T_") || src.name == "T_Neige" || src.transform.IsChildOf(ws.transform)) continue;   // pas la neige d'hiver
            var srcR = src.GetComponent<TilemapRenderer>();
            var dst = new GameObject(src.name) { hideFlags = HideFlags.DontSave }.AddComponent<Tilemap>();
            dst.transform.SetParent(grid.transform, false);
            var r = dst.gameObject.AddComponent<TilemapRenderer>();
            if (srcR != null) { r.sortingOrder = srcR.sortingOrder; r.sharedMaterial = srcR.sharedMaterial; }
            foreach (var p in area.allPositionsWithin)
            {
                var c = new Vector3Int(p.x, p.y, 0);
                var t = src.GetTile(c);
                if (t == null) continue;
                dst.SetTile(c, t);
                dst.SetTileFlags(c, TileFlags.None);
                dst.SetColor(c, src.GetColor(c));
                n++;
            }
        }
        return n;
    }

    /// <summary>Chaque intérieur, côte à côte, avec ses repères (porte, points d'interaction) à déplacer.</summary>
    static int BuildInteriors(CityWorkshop ws)
    {
        int i = 0;
        foreach (var (id, title, stamp) in InteriorSystem.StampLayouts())
        {
            var pos = InteriorsOrigin + new Vector3((i % 5) * 20f, -(i / 5) * 18f, 0f);
            var room = Node(ws.interiors, $"interior:{id} — {title}", pos);
            if (!InteriorSystem.BuildRoomContent(id, room, true)) { Object.DestroyImmediate(room.gameObject); continue; }
            var label = new GameObject("Titre");
            label.transform.SetParent(room, false);
            label.transform.localPosition = new Vector3(0f, 7.5f, 0f);
            var tmp = label.AddComponent<TMPro.TextMeshPro>();
            tmp.text = title;
            tmp.fontSize = 6f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(18f, 2f);
            tmp.sortingOrder = 60;
            i++;
        }
        return i;
    }

    public static string RegenerateSelected()
    {
        var ws = CityWorkshop.Instance;
        var sel = Selection.activeGameObject;
        if (ws == null || sel == null) return "sélectionner un bâtiment ou un intérieur de l'atelier";
        var site = Object.FindFirstObjectByType<SettlementSite>();
        var ct = sel.GetComponentInParent<CityTemplate>();
        if (ct != null)
        {
            var plot = site.plots.Find(p => p.buildingId == ct.id);
            if (plot == null) return "emplacement introuvable";
            var pos = ct.transform.position;
            Object.DestroyImmediate(ct.gameObject);
            var go = BuildTemplate(ws, site, SettlementArt.Get(), plot);
            if (go != null) { go.transform.position = pos; Selection.activeGameObject = go; }
            EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
            return "bâtiment régénéré : " + plot.buildingId;
        }
        var t = sel.transform;
        while (t != null && !t.name.StartsWith("interior:")) t = t.parent;
        if (t != null)
        {
            string id = t.name.Substring(9).Split(' ')[0];
            for (int k = t.childCount - 1; k >= 0; k--) if (t.GetChild(k).name != "Titre") Object.DestroyImmediate(t.GetChild(k).gameObject);
            InteriorSystem.BuildRoomContent(id, t, true);
            EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
            return "intérieur régénéré : " + id;
        }
        return "sélectionner un bâtiment ou un intérieur de l'atelier";
    }
}
#endif
