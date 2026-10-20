using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pose un tampon de la cité (SettlementArt.Stamp) : le sol à plat sous les personnages, chaque morceau trié à ses
/// pieds (le héros passe devant ou derrière), les morceaux animés (drapeaux, fumée, fontaine, habitants) animés
/// ensemble, et des collisions au pied des bâtiments, clôtures, arbres et gros objets.
/// </summary>
public class StampVisual : MonoBehaviour
{
    public const int GroundOrder = -3;
    public const int ObstacleLayer = 6;

    private readonly List<(SpriteRenderer sr, Sprite[] frames, float fps, float phase)> animated = new List<(SpriteRenderer, Sprite[], float, float)>();
    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private readonly List<Collider2D> colliders = new List<Collider2D>();
    public SettlementArt.Stamp Stamp { get; private set; }

    /// <summary>Crée le tampon, coin bas-gauche en <paramref name="bottomLeft"/>.</summary>
    public static StampVisual Create(SettlementArt.Stamp st, Transform parent, Vector3 bottomLeft, bool colliders)
    {
        var go = new GameObject("Tampon " + st.id);
        go.transform.SetParent(parent, false);
        go.transform.position = bottomLeft;
        var v = go.AddComponent<StampVisual>();
        v.Stamp = st;
        v.Build(colliders);
        return v;
    }

    private void Build(bool withColliders)
    {
        if (Stamp.ground != null)
        {
            var g = new GameObject("Sol");
            g.transform.SetParent(transform, false);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Stamp.ground;
            sr.sortingOrder = GroundOrder;
            renderers.Add(sr);
        }
        int i = 0;
        foreach (var p in Stamp.pieces)
        {
            if (p.frames == null || p.frames.Length == 0 || p.frames[0] == null) continue;
            var go = new GameObject($"{p.kind} {i++}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(p.pos.x, p.pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = p.frames[0];
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            renderers.Add(sr);
            if (p.frames.Length > 1) animated.Add((sr, p.frames, p.fps > 0f ? p.fps : 7f, Random.value * 10f));
            if (withColliders && p.solid) AddCollider(go, p);
        }
        if (animated.Count == 0) enabled = false;
    }

    private void AddCollider(GameObject go, SettlementArt.StampPiece p)
    {
        var col = go.AddComponent<BoxCollider2D>();
        go.layer = ObstacleLayer;
        float w = p.size.x, h = p.size.y;
        Vector2 size;
        switch (p.kind)
        {
            case "building": size = new Vector2(w * 0.9f, Mathf.Max(0.5f, h * 0.42f)); break;
            case "fence": size = new Vector2(w, 0.22f); break;
            case "tree": size = new Vector2(Mathf.Min(w * 0.4f, 0.6f), 0.3f); break;
            default: size = new Vector2(w * 0.8f, Mathf.Clamp(h * 0.35f, 0.2f, 0.7f)); break;
        }
        col.size = size;
        col.offset = new Vector2(0f, size.y * 0.5f + (p.kind == "fence" ? 0.02f : 0.04f));
        colliders.Add(col);
    }

    /// <summary>Chantier : silhouette translucide ; achevé : opaque.</summary>
    public void SetAlpha(float a)
    {
        foreach (var sr in renderers) if (sr != null) sr.color = new Color(1f, 1f, 1f, a);
    }

    public void SetColliders(bool on)
    {
        foreach (var c in colliders) if (c != null) c.enabled = on;
    }

    /// <summary>Le tampon contient-il ce point (monde) dans un de ses morceaux ou son sol ?</summary>
    public bool Contains(Vector2 world)
    {
        var b = new Bounds(transform.position + (Vector3)(Stamp.size * 0.5f), Stamp.size);
        return b.Contains(new Vector3(world.x, world.y, b.center.z));
    }

    private void Update()
    {
        float t = Time.time;
        for (int i = 0; i < animated.Count; i++)
        {
            var (sr, frames, fps, phase) = animated[i];
            if (sr == null) continue;
            var f = frames[(int)((t + phase) * fps) % frames.Length];
            if (sr.sprite != f) sr.sprite = f;
        }
    }
}
