using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// Faune de l'île (cerfs, lièvres, renards, tétras, sangliers) : flâne dans la nature
/// et fuit les humains et les monstres. Purement vivante / décorative.
/// </summary>
[RequireComponent(typeof(Seeker))]
public class WildAnimal : MonoBehaviour
{
    public static readonly List<WildAnimal> All = new List<WildAnimal>();

    public float walkSpeed = 0.8f;
    public float runSpeed = 2.4f;
    public float wanderRadius = 6f;
    public float fearRadius = 3.5f;

    private Seeker seeker;
    private Animator animator;
    private SpriteRenderer sr;
    private Path path;
    private int wp;
    private Vector2 home;
    private Vector2 facing = Vector2.down;
    private Vector2 velocity;
    private float nextDecision;
    private bool fleeing;
    private bool hasSpeed, hasX, hasY;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        seeker = GetComponent<Seeker>();
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        home = transform.position;
        hasSpeed = UnitAnim.HasParam(animator, "Speed");
        hasX = UnitAnim.HasParam(animator, "MoveX");
        hasY = UnitAnim.HasParam(animator, "MoveY");
        nextDecision = Time.time + Random.Range(0f, 4f);
    }

    private void Update()
    {
        Vector2 pos = transform.position;

        // Menace proche ?
        Vector2 threat = Vector2.zero;
        bool scared = false;
        foreach (var a in AdventurerAI.All)
            if (a != null && Vector2.Distance(a.transform.position, pos) < fearRadius) { threat = a.transform.position; scared = true; break; }
        if (!scared)
            foreach (var g in GobelinAI.All)
                if (g != null && Vector2.Distance(g.transform.position, pos) < fearRadius) { threat = g.transform.position; scared = true; break; }

        if (scared && !fleeing)
        {
            fleeing = true;
            Vector2 away = (pos - threat).normalized;
            GoTo(pos + away * 6f + Random.insideUnitCircle * 2f);
        }
        else if (Time.time > nextDecision)
        {
            fleeing = false;
            nextDecision = Time.time + Random.Range(3f, 8f);
            if (Random.value < 0.6f) GoTo(home + Random.insideUnitCircle * wanderRadius);
            else path = null;   // se repose
        }

        Move(fleeing ? runSpeed : walkSpeed);
        Animate();
    }

    private void GoTo(Vector2 target)
    {
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph != null)
        {
            var nn = graph.GetNearest(target, NNConstraint.Default);
            if (nn.node == null) return;
            var here = graph.GetNearest(transform.position, NNConstraint.Default).node;
            // Même île (pas de traversée vers un îlot), jamais au bord de l'eau ni sur le ponton
            if (here != null && nn.node.Area != here.Area) return;
            target = (Vector3)nn.node.position;
            if (!IsDrySpot(target)) return;
        }
        // Les animaux n'entrent pas dans le donjon
        if (DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(target)) return;
        seeker.StartPath(transform.position, target, p => { if (!p.error) { path = p; wp = 0; } });
    }

    private static UnityEngine.Tilemaps.Tilemap seaMap, solMap;
    private static UnityEngine.Tilemaps.TileBase seaFloor;

    /// <summary>Case sèche : aucune eau dans un rayon d'une case et pas de ponton (fond marin sous les planches).</summary>
    public static bool IsDrySpot(Vector2 p)
    {
        if (seaMap == null || solMap == null)
        {
            foreach (var tm in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
            {
                if (tm.name == "NoPathfinding") seaMap = tm;
                else if (tm.name == "Sol") solMap = tm;
            }
            var art = SettlementArt.Get();
            seaFloor = art != null ? art.seaFloor : null;
            if (seaMap == null || solMap == null) return true;
        }
        var c = seaMap.WorldToCell(p);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var n = c + new Vector3Int(dx, dy, 0);
                if (seaMap.HasTile(n)) return false;
                if (seaFloor != null && solMap.GetTile(n) == seaFloor) return false;
            }
        return true;
    }

    private void Move(float speed)
    {
        velocity = Vector2.zero;
        if (path == null || wp >= path.vectorPath.Count) return;
        Vector2 pos = transform.position;
        Vector2 target = path.vectorPath[wp];
        if (Vector2.Distance(pos, target) < 0.2f) { wp++; return; }
        velocity = (target - pos).normalized * speed;
        transform.position = pos + velocity * Time.deltaTime;
        facing = velocity.normalized;
    }

    private void Animate()
    {
        if (animator == null) return;
        if (hasSpeed) animator.SetFloat("Speed", velocity.sqrMagnitude);
        if (hasX) animator.SetFloat("MoveX", facing.x);
        if (hasY) animator.SetFloat("MoveY", facing.y);
        if (!hasX && sr != null && Mathf.Abs(facing.x) > 0.1f) sr.flipX = facing.x < 0;
    }
}
