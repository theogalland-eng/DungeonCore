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
        foreach (var c in site.cityWall)
        {
            bool south = c.y == r.y, north = c.y == r.yMax - 1;
            bool west = c.x == r.x, east = c.x == r.xMax - 1;
            bool horizontal = south || north;
            bool corner = horizontal && (west || east);
            string sprite;
            if (kind == Kind.Stone)
                sprite = corner || NearGate(c) ? "wall_tower" : horizontal ? "wall_h" : "wall_v";
            else
                sprite = horizontal ? "palisade_h" : "palisade_v";
            var s = art.Sprite(sprite);
            if (s == null) continue;
            var go = new GameObject(sprite);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(c.x + 0.5f, c.y + (horizontal ? 0.1f : 0f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var col = root.AddComponent<BoxCollider2D>();
            col.offset = new Vector2(c.x + 0.5f, c.y + 0.5f);
            col.size = new Vector2(1f, horizontal ? 0.9f : 1f);
        }
    }
}
