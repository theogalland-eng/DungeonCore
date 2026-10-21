using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ENCEINTE DE LA CITÉ : palissade de rondins au stade Village, remparts de pierre (tours aux angles et aux portes)
/// quand la cité se fortifie. Posée sur le tracé calculé par le générateur (SettlementSite.cityWall), avec une
/// collision par case : on n'entre et ne sort que par les trois portes.
/// </summary>
public static class CityWalls
{
    public enum Kind { None, Palisade, Stone }

    private static GameObject root;
    private static Kind built = Kind.None;

    public static void Apply(SettlementSite site, Kind kind)
    {
        if (site == null || site.cityWall == null || site.cityWall.Count == 0) return;
        if (root != null && kind == built) return;
        if (root == null && kind == Kind.None) { built = Kind.None; return; }
        if (root != null) Object.Destroy(root);
        root = null;
        built = kind;
        if (kind != Kind.None) Build(site, kind);
        var r = site.cityRect;
        AstarPath.active?.UpdateGraphs(new Bounds(new Vector3(r.center.x, r.center.y, 0f), new Vector3(r.width + 4, r.height + 4, 1f)), 0.2f);
    }

    private static void Build(SettlementSite site, Kind kind)
    {
        var art = SettlementArt.Get();
        if (art == null) return;
        root = new GameObject(kind == Kind.Stone ? "Remparts" : "Palissade");
        root.layer = StampVisual.ObstacleLayer;
        var r = site.cityRect;
        var gates = new HashSet<Vector3Int>(site.cityGates);
        bool NearGate(Vector3Int c)
        {
            foreach (var d in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                if (gates.Contains(c + d)) return true;
            return false;
        }
        // tours octogonales : aux angles, de part et d'autre des portes, et toutes les 12 cases le long des murs
        var towers = new HashSet<Vector3Int>();
        if (kind == Kind.Stone)
        {
            var wallSet = new HashSet<Vector3Int>(site.cityWall);
            foreach (var c in new[] { new Vector3Int(r.x, r.y, 0), new Vector3Int(r.xMax - 1, r.y, 0), new Vector3Int(r.x, r.yMax - 1, 0), new Vector3Int(r.xMax - 1, r.yMax - 1, 0) })
                towers.Add(c);
            foreach (var g in site.cityGates)
                foreach (var d in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                    if (wallSet.Contains(g + d) && !gates.Contains(g + d)) towers.Add(g + d + d);
            foreach (var c in site.cityWall)
            {
                bool hz = c.y == r.y || c.y == r.yMax - 1;
                int along = hz ? c.x - r.x : c.y - r.y;
                if (along % 12 != 6) continue;
                bool clear = true;
                foreach (var t in towers) if (Mathf.Abs(t.x - c.x) + Mathf.Abs(t.y - c.y) < 6) clear = false;
                if (clear) towers.Add(c);
            }
        }
        bool UnderTower(Vector3Int c)
        {
            foreach (var t in towers) if (t == c) return true;   // les murs voisins passent derrière la tour : pas de jour
            return false;
        }
        GameObject Piece(string sprite, Vector3 pos)
        {
            var sp = art.Sprite(sprite);
            if (sp == null) return null;
            var go = new GameObject(sprite);
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            return go;
        }
        foreach (var t in towers)
        {
            Piece("wall_tower_big", new Vector3(t.x + 0.5f, t.y - 0.1f, 0f));
            var col = root.AddComponent<BoxCollider2D>();
            col.offset = new Vector2(t.x + 0.5f, t.y + 0.4f);
            col.size = new Vector2(2.2f, 1.6f);
        }
        foreach (var c in site.cityWall)
        {
            bool south = c.y == r.y, north = c.y == r.yMax - 1;
            bool horizontal = south || north;
            if (kind == Kind.Stone && UnderTower(c)) continue;
            string sprite = kind == Kind.Stone ? (horizontal ? "wall_h" : "wall_v") : (horizontal ? "palisade_h" : "palisade_v");
            Piece(sprite, new Vector3(c.x + 0.5f, c.y + (horizontal ? 0.1f : 0f), 0f));
            var col = root.AddComponent<BoxCollider2D>();
            col.offset = new Vector2(c.x + 0.5f, c.y + 0.5f);
            col.size = new Vector2(1f, horizontal ? 0.9f : 1f);
        }
    }
}
