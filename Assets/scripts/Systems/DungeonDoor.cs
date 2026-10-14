using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// Porte du donjon.
///   - FERMÉE : elle bloque la vue et les tirs (couche « Door »), et les monstres ne la franchissent pas d'eux-mêmes
///     (étiquette de pathfinding + barrière physique) — sauf sur ordre du Noyau (déplacement commandé, possession, raid).
///   - Le premier intrus qui l'atteint la force (immobilisé quelques secondes), puis elle RESTE OUVERTE pour toute
///     l'expédition : intrus et monstres passent librement. Elle se referme quand l'île est vide.
///   - Couloir est-ouest : on affiche la porte vue de profil (battant qui pivote, herse qui remonte).
/// </summary>
public class DungeonDoor : MonoBehaviour
{
    public static readonly List<DungeonDoor> All = new List<DungeonDoor>();
    public const int DoorLayer = 10;
    public const int DoorTag = 1;
    public const int LosMask = 1 << DoorLayer;

    [Tooltip("Temps pour forcer la porte (s)")]
    public float holdSeconds = 2.5f;
    [Tooltip("Dégâts infligés à l'intrus qui force la porte (gueule d'ossements)")]
    public float biteDamage;
    public float radius = 0.6f;
    public LayerMask intruderLayer = 1 << 7;
    public SpriteRenderer doorRenderer;
    [Tooltip("Animation d'ouverture (image 0 = fermée)")]
    public Sprite[] frames;
    [Tooltip("Animation vue de profil (couloir est-ouest)")]
    public Sprite[] sideFrames;
    public string forceText = "Force la porte…";

    public bool IsOpen { get; private set; }

    private BoxCollider2D blocker;
    private float forcedAt = -1f, openAmount, emptySince = -1f, playerOpenUntil;
    private Sprite[] anim;

    private void Awake()
    {
        if (doorRenderer == null) doorRenderer = GetComponentInChildren<SpriteRenderer>();
        anim = frames;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        OrientToCorridor();
        // barrière : vue, tirs et monstres (les aventuriers la traversent physiquement, mais doivent la forcer)
        var go = new GameObject("Battant");
        go.transform.SetParent(transform, false);
        go.layer = DoorLayer;
        blocker = go.AddComponent<BoxCollider2D>();
        blocker.size = new Vector2(0.96f, 0.96f);
        Physics2D.IgnoreLayerCollision(DoorLayer, 7, true);    // aventuriers
        Physics2D.IgnoreLayerCollision(DoorLayer, 6, true);
        // les monstres sous ordre déjà présents peuvent passer
        foreach (var g in GobelinAI.All) if (g != null && g.MayCrossDoors) SetPassable(g, true, this);
        SetClosed(true);
    }

    private void OnDestroy()
    {
        if (!IsOpen) ApplyTag(false);
    }

    /// <summary>
    /// Couloir est-ouest (roche au nord et au sud) : porte vue de profil si on en a le dessin, sinon un quart de tour.
    /// </summary>
    public void OrientToCorridor()
    {
        var b = DungeonBuilder.Instance;
        if (b == null || doorRenderer == null) return;
        var c = b.floorTilemap.WorldToCell(transform.position);
        bool Wall(Vector3Int d) => b.stoneTilemap.HasTile(c + d);
        bool eastWest = Wall(Vector3Int.up) && Wall(Vector3Int.down) && !(Wall(Vector3Int.left) && Wall(Vector3Int.right));
        if (!eastWest || frames == null) return;
        if (sideFrames != null && sideFrames.Length > 0)
        {
            anim = sideFrames;
            doorRenderer.sprite = sideFrames[0];
            return;
        }
        var t = doorRenderer.transform;
        float len = doorRenderer.sprite != null ? doorRenderer.sprite.bounds.size.y : 1.5f;
        float scale = Mathf.Min(1f, 1.25f / Mathf.Max(0.01f, len));
        t.localRotation = Quaternion.Euler(0f, 0f, 90f);
        t.localScale = Vector3.one * scale;
        t.localPosition = new Vector3(len * scale * 0.5f, 0f, 0f);
    }

    private void FixedUpdate()
    {
        if (IsOpen) return;
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, radius, intruderLayer))
        {
            var ai = h.GetComponent<AdventurerAI>();
            if (ai == null)
            {
                // le héros du mode Aventurier pousse la porte sans être retenu
                if (h.GetComponent<CrawlerPlayer>() != null) { playerOpenUntil = Time.time + 2.5f; Open(false); }
                continue;
            }
            if (forcedAt < 0f)
            {
                // le premier arrivé force la porte ; elle cède au bout de quelques secondes
                forcedAt = Time.time + holdSeconds;
                Sfx.Play(biteDamage > 0f ? "growl" : "door_break", transform.position, 0.8f);
                if (biteDamage > 0f) ai.GetComponent<AdventurerCombat>()?.TakeDamage(biteDamage);
                FloatingText.Spawn(transform.position + Vector3.up * 0.7f, forceText, new Color(0.9f, 0.8f, 0.6f), 0.8f);
            }
            ai.Hold(Mathf.Max(0.1f, forcedAt - Time.time));
        }
        if (forcedAt > 0f && Time.time >= forcedAt) Open(true);
    }

    /// <summary>Ouverture : la porte reste ouverte (expédition) ou se refermera derrière le héros (mode Aventurier).</summary>
    public void Open(bool forced)
    {
        if (IsOpen) return;
        IsOpen = true;
        forcedAt = -1f;
        emptySince = -1f;
        if (forced) Sfx.Play("door_break", transform.position, 1f);
        SetClosed(false);
    }

    /// <summary>Compatibilité : ouvre un instant.</summary>
    public void OpenFor(float seconds) { playerOpenUntil = Time.time + seconds; Open(false); }

    private void Close()
    {
        IsOpen = false;
        forcedAt = -1f;
        SetClosed(true);
    }

    private void SetClosed(bool closed)
    {
        if (blocker != null) blocker.enabled = closed;
        ApplyTag(closed);
    }

    /// <summary>Étiquette de la case pour le pathfinding : les monstres sans ordre l'évitent tant qu'elle est fermée.</summary>
    private void ApplyTag(bool closed)
    {
        if (AstarPath.active == null) return;
        var guo = new GraphUpdateObject(new Bounds(transform.position, new Vector3(0.9f, 0.9f, 1f)))
        {
            modifyTag = true, setTag = closed ? DoorTag : 0, updatePhysics = false,
        };
        AstarPath.active.UpdateGraphs(guo);
    }

    private float nextTag;

    private void Update()
    {
        // une mise à jour du graphe (creusage...) peut effacer l'étiquette : on la réapplique de temps en temps
        if (!IsOpen && Time.time >= nextTag) { nextTag = Time.time + 2f; ApplyTag(true); }
        // ouverte : elle le reste tant que des intrus sont sur l'île (ou que le héros est tout près)
        if (IsOpen)
        {
            bool crawler = CrawlerMode.Active;
            bool keep = crawler ? Time.time < playerOpenUntil : IntrudersOnIsland();
            if (keep) emptySince = -1f;
            else if (emptySince < 0f) emptySince = Time.time;
            else if (Time.time - emptySince > (crawler ? 0.5f : 4f)) Close();
        }
        if (doorRenderer == null) return;
        if (anim != null && anim.Length > 1)
        {
            // pendant qu'on la force : entrouverte ; puis grande ouverte
            float target = IsOpen ? 1f : forcedAt > 0f ? 0.34f : 0f;
            openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime * 2.5f);
            doorRenderer.sprite = anim[Mathf.Clamp(Mathf.RoundToInt(openAmount * (anim.Length - 1)), 0, anim.Length - 1)];
        }
        else
        {
            var c = doorRenderer.color;
            c.a = Mathf.MoveTowards(c.a, IsOpen ? 0.45f : 1f, Time.deltaTime * 2f);
            doorRenderer.color = c;
        }
    }

    private static bool IntrudersOnIsland()
    {
        foreach (var a in AdventurerAI.All) if (a != null && !a.IsPrisoner) return true;
        return false;
    }

    /// <summary>Un monstre peut-il traverser les portes fermées (ordre du Noyau) ? Ignore la barrière physique.</summary>
    public static void SetPassable(GobelinAI g, bool pass, DungeonDoor only = null)
    {
        if (g == null) return;
        var cols = g.GetComponents<Collider2D>();
        foreach (var d in All)
        {
            if (d == null || d.blocker == null || (only != null && d != only)) continue;
            foreach (var c in cols) Physics2D.IgnoreCollision(c, d.blocker, pass);
        }
    }
}
