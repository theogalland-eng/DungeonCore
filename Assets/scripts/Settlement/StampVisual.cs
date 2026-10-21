using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animation d'un morceau de tampon, enregistrée dans la scène (les morceaux peuvent être déplacés, copiés, ajoutés
/// à la main dans l'atelier de la cité) : images, cadence, et rôle pour les animations sur événement.
/// </summary>
public class StampPart : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 7f;
    /// <summary>« door » (quelqu'un passe), « shutter » (volets : ouverts le jour), « gate » (herse) ; vide = en boucle.</summary>
    public string role;
    public int openFrame;
    public bool cycle;
}

/// <summary>
/// Pose un tampon de la cité (SettlementArt.Stamp) : le sol à plat sous les personnages, chaque morceau trié à ses
/// pieds (le héros passe devant ou derrière), les morceaux animés (drapeaux, fumée, fontaine, habitants) animés
/// ensemble, portes et volets joués sur événement, et des collisions au pied des bâtiments, clôtures, arbres et gros
/// objets. Tout est porté par des objets de la scène : un tampon copié ou retouché dans l'atelier reste vivant.
/// </summary>
public class StampVisual : MonoBehaviour
{
    public const int GroundOrder = -3;
    public const int ObstacleLayer = 6;

    private readonly List<(SpriteRenderer sr, Sprite[] frames, float fps, float phase)> animated = new List<(SpriteRenderer, Sprite[], float, float)>();

    /// <summary>Porte, volet ou herse : joue son animation sur événement (passage, lever et coucher du soleil), pas en boucle.</summary>
    private class Trigger
    {
        public SpriteRenderer sr;
        public Sprite[] frames;
        public string role;
        public int open;
        public bool cycle;
        public float fps;
        public float frame;         // image courante (fractionnaire)
        public int target;          // image visée
        public float lastSeen = -99f;
        public bool closingForward; // animation en cycle : on referme en allant jusqu'au bout
    }
    private readonly List<Trigger> triggers = new List<Trigger>();
    private float nextScan;
    private bool collected;
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
            if (p.frames.Length > 1)
            {
                var part = go.AddComponent<StampPart>();
                part.frames = p.frames;
                part.fps = p.fps > 0f ? p.fps : 7f;
                part.role = p.role;
                part.openFrame = Mathf.Clamp(p.openFrame, 0, p.frames.Length - 1);
                part.cycle = p.cycle;
                // porte ou volet : trié comme le bâtiment qui le porte (juste devant sa façade)
                if (!string.IsNullOrEmpty(p.role))
                    go.transform.localPosition = new Vector3(p.pos.x, p.pos.y, Mathf.Min(p.sortY, p.pos.y) - p.pos.y - 0.02f);
            }
            if (withColliders && p.solid) AddCollider(go, p.kind, p.size);
        }
        collected = false;
    }

    /// <summary>Collision au pied d'un morceau (bâtiment, clôture, arbre, objet).</summary>
    public static BoxCollider2D AddCollider(GameObject go, string kind, Vector2 sizeUnits)
    {
        var col = go.AddComponent<BoxCollider2D>();
        go.layer = ObstacleLayer;
        float w = sizeUnits.x, h = sizeUnits.y;
        Vector2 size;
        switch (kind)
        {
            case "building": size = new Vector2(w * 0.9f, Mathf.Max(0.5f, h * 0.42f)); break;
            case "fence": size = new Vector2(w, 0.22f); break;
            case "tree": size = new Vector2(Mathf.Min(w * 0.4f, 0.6f), 0.3f); break;
            default: size = new Vector2(w * 0.8f, Mathf.Clamp(h * 0.35f, 0.2f, 0.7f)); break;
        }
        col.size = size;
        col.offset = new Vector2(0f, size.y * 0.5f + (kind == "fence" ? 0.02f : 0.04f));
        return col;
    }

    /// <summary>Recense les morceaux animés (après chargement de la scène, copie ou retouche dans l'atelier).</summary>
    private void Collect()
    {
        collected = true;
        animated.Clear();
        triggers.Clear();
        bool night = IsNight();
        foreach (var part in GetComponentsInChildren<StampPart>(true))
        {
            var sr = part.GetComponent<SpriteRenderer>();
            if (sr == null || part.frames == null || part.frames.Length < 2) continue;
            if (!string.IsNullOrEmpty(part.role))
            {
                var t = new Trigger
                {
                    sr = sr, frames = part.frames, role = part.role, cycle = part.cycle, fps = Mathf.Max(6f, part.fps),
                    open = Mathf.Clamp(part.openFrame, 0, part.frames.Length - 1),
                };
                t.frame = t.target = 0;
                if (t.role == "shutter" && !night) { t.frame = t.target = t.open; }
                sr.sprite = part.frames[(int)t.frame];
                triggers.Add(t);
            }
            else animated.Add((sr, part.frames, part.fps > 0f ? part.fps : 7f, Random.value * 10f));
        }
    }

    /// <summary>Chantier : silhouette translucide ; achevé : opaque.</summary>
    public void SetAlpha(float a)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) if (sr != null) sr.color = new Color(1f, 1f, 1f, a);
    }

    public void SetColliders(bool on)
    {
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) if (c != null) c.enabled = on;
    }

    /// <summary>Le tampon contient-il ce point (monde) ?</summary>
    public bool Contains(Vector2 world)
    {
        var b = Bounds();
        return b.Contains(new Vector3(world.x, world.y, b.center.z));
    }

    /// <summary>Emprise visible (tous les dessins).</summary>
    public Bounds Bounds()
    {
        bool any = false;
        var b = new Bounds(transform.position, Vector3.zero);
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.sprite == null) continue;
            if (!any) { b = sr.bounds; any = true; } else b.Encapsulate(sr.bounds);
        }
        return b;
    }

    private static bool IsNight() => GameManager.Instance != null && GameManager.Instance.IsNight;

    /// <summary>Quelqu'un (héros, compagnon, colon, aventurier, passant) se tient près de ce point ?</summary>
    public static bool SomeoneNear(Vector2 p, float r)
    {
        float r2 = r * r;
        var cm = CrawlerMode.Instance;
        if (cm != null && cm.Player != null && ((Vector2)cm.Player.transform.position - p).sqrMagnitude < r2) return true;
        foreach (var c in CrawlerCompanion.All) if (c != null && ((Vector2)c.transform.position - p).sqrMagnitude < r2) return true;
        foreach (var s in SettlerAI.All) if (s != null && ((Vector2)s.transform.position - p).sqrMagnitude < r2) return true;
        foreach (var a in AdventurerAI.All) if (a != null && ((Vector2)a.transform.position - p).sqrMagnitude < r2) return true;
        foreach (var f in TownFolk.All) if (f != null && ((Vector2)f.transform.position - p).sqrMagnitude < r2) return true;
        return false;
    }

    private void UpdateTriggers()
    {
        if (triggers.Count == 0) return;
        bool scan = Time.time >= nextScan;
        if (scan) nextScan = Time.time + 0.2f;
        bool night = IsNight();
        foreach (var t in triggers)
        {
            if (t.sr == null) continue;
            if (t.role == "shutter")
            {
                // volets : ouverts le jour, fermés la nuit
                int want = night ? 0 : t.open;
                if (want != t.target) { t.target = want; t.closingForward = t.cycle && want == 0; }
            }
            else if (scan)
            {
                float radius = t.role == "gate" ? 2.2f : 1.5f;
                if (SomeoneNear(t.sr.transform.position, radius)) t.lastSeen = Time.time;
                bool keepOpen = Time.time - t.lastSeen < (t.role == "gate" ? 2.5f : 1.2f);
                int want = keepOpen ? t.open : 0;
                if (want != t.target) { t.target = want; t.closingForward = t.cycle && want == 0; }
            }
            // avance vers l'image visée
            float step = t.fps * Time.deltaTime;
            int n = t.frames.Length;
            if (t.closingForward && t.target == 0 && t.frame >= t.open)
            {
                t.frame += step;
                if (t.frame >= n) { t.frame = 0f; t.closingForward = false; }
            }
            else if (t.frame < t.target) t.frame = Mathf.Min(t.target, t.frame + step);
            else if (t.frame > t.target) t.frame = Mathf.Max(t.target, t.frame - step);
            var f = t.frames[Mathf.Clamp((int)t.frame, 0, n - 1)];
            if (t.sr.sprite != f) t.sr.sprite = f;
        }
    }

    private void Update()
    {
        if (!collected) Collect();
        UpdateTriggers();
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
