using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>Tâche confiée à un colon par le SettlementManager.</summary>
public class SettlerJob
{
    public string label;              // "Bûcheron", "Bâtisseur"...
    public Vector2 target;            // où aller
    public float duration;            // temps de travail sur place (s)
    public Func<bool> stillValid;     // la tâche a-t-elle encore un sens ?
    public Action<float> onWork;      // chaque frame de travail (dt)
    public Action onDone;             // à la fin du travail
    public Action onCancel;
    public bool hideWhenDone;         // dormir : disparaître dans la tente
}

/// <summary>
/// Colon de l'île : va sur place, travaille (couper, bâtir, pêcher, miner), rapporte, dort la nuit.
/// Ne met jamais les pieds dans le donjon.
/// </summary>
[RequireComponent(typeof(Seeker))]
public class SettlerAI : MonoBehaviour
{
    public static readonly List<SettlerAI> All = new List<SettlerAI>();

    public float speed = 1.3f;
    public string Job => job != null ? job.label : "Repos";

    private enum Phase { Idle, Moving, Working, Hidden }
    private Phase phase = Phase.Idle;
    private SettlerJob job;
    private Seeker seeker;
    private Animator animator;
    private SpriteRenderer sr;
    private Path path;
    private int wp;
    private float workLeft;
    private Vector2 facing = Vector2.down, velocity;
    private float repathAt, stuckAt;
    private Vector2 stuckPos;
    private bool hasSpeed, hasX, hasY;
    private float chipTimer;

    private void OnEnable() { if (!IsDying) All.Add(this); }
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        seeker = GetComponent<Seeker>();
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        hasSpeed = UnitAnim.HasParam(animator, "Speed");
        hasX = UnitAnim.HasParam(animator, "MoveX");
        hasY = UnitAnim.HasParam(animator, "MoveY");
        speed *= UnityEngine.Random.Range(0.9f, 1.1f);
    }

    public void Assign(SettlerJob j)
    {
        job = j;
        SetVisible(true);
        phase = Phase.Moving;
        path = null;
        RequestPath();
        stuckAt = Time.time;
        stuckPos = transform.position;
    }

    public void CancelJob()
    {
        job?.onCancel?.Invoke();
        job = null;
        phase = Phase.Idle;
        path = null;
    }

    public void SetVisible(bool v)
    {
        if (sr != null) sr.enabled = v;
        if (!v) phase = Phase.Hidden;
        else if (phase == Phase.Hidden) phase = Phase.Idle;
    }

    public bool IsHidden => phase == Phase.Hidden;
    public bool IsIdle => phase == Phase.Idle;
    public bool IsDying { get; private set; }

    private static readonly string[] Cries = { "Au secours !", "Des monstres !", "Fuyez !", "À l'aide !" };

    /// <summary>Raid nocturne : le colon sort en hurlant et court se réfugier loin de la menace.</summary>
    public void Panic(Vector2 threat, Vector2 refuge)
    {
        if (IsDying) return;
        CancelJob();
        SetVisible(true);
        FloatingText.Spawn(transform.position + Vector3.up * 0.8f, Cries[UnityEngine.Random.Range(0, Cries.Length)], new Color(1f, 0.85f, 0.6f), 0.9f);
        speed = Mathf.Max(speed, 2.4f);
        Assign(new SettlerJob { label = "Fuite", target = refuge, duration = 0.2f, hideWhenDone = GameManager.Instance != null && GameManager.Instance.IsNight });
    }

    /// <summary>Tué pendant un raid sanglant.</summary>
    public void Kill()
    {
        if (IsDying) return;
        IsDying = true;
        CancelJob();
        SetVisible(true);
        enabled = false;
        All.Remove(this);
        StartCoroutine(DieRoutine());
    }

    private System.Collections.IEnumerator DieRoutine()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        var a = GetComponent<Animator>();
        if (UnitAnim.HasParam(a, "Dead")) a.SetBool("Dead", true);
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            if (sr != null) sr.color = new Color(1f, 0.55f, 0.55f, 1f - t / 1.4f);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void RequestPath()
    {
        if (job == null) return;
        repathAt = Time.time + 4f;
        Vector2 target = job.target;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph != null)
        {
            var nn = graph.GetNearest(target, NNConstraint.Default);
            if (nn.node != null) target = (Vector3)nn.node.position;
        }
        seeker.StartPath(transform.position, target, p => { if (!p.error) { path = p; wp = 0; } });
    }

    private void Update()
    {
        velocity = Vector2.zero;
        switch (phase)
        {
            case Phase.Idle:
                if (Time.frameCount % 15 == (GetInstanceID() & 15)) SettlementManager.Instance?.RequestJob(this);
                break;
            case Phase.Moving: UpdateMoving(); break;
            case Phase.Working: UpdateWorking(); break;
        }
        Animate();
    }

    private void UpdateMoving()
    {
        if (job == null) { phase = Phase.Idle; return; }
        if (job.stillValid != null && !job.stillValid()) { CancelJob(); return; }
        Vector2 pos = transform.position;
        if (Vector2.Distance(pos, job.target) < 0.7f || (path != null && wp >= path.vectorPath.Count && Vector2.Distance(pos, job.target) < 1.6f))
        {
            phase = Phase.Working;
            workLeft = job.duration;
            if (job.target.x != pos.x) facing = new Vector2(Mathf.Sign(job.target.x - pos.x), 0f);
            return;
        }
        if (path == null || wp >= path.vectorPath.Count)
        {
            if (Time.time > repathAt) RequestPath();
            return;
        }
        Vector2 t = path.vectorPath[wp];
        if (Vector2.Distance(pos, t) < 0.15f) { wp++; return; }
        velocity = (t - pos).normalized * speed;
        transform.position = pos + velocity * Time.deltaTime;
        facing = velocity.normalized;

        // Bloqué ? on recalcule, puis on abandonne
        if (Time.time - stuckAt > 3f)
        {
            if (Vector2.Distance(pos, stuckPos) < 0.3f) { CancelJob(); return; }
            stuckAt = Time.time;
            stuckPos = pos;
        }
    }

    private void UpdateWorking()
    {
        if (job == null) { phase = Phase.Idle; return; }
        if (job.stillValid != null && !job.stillValid()) { CancelJob(); return; }
        float dt = Time.deltaTime;
        workLeft -= dt;
        job.onWork?.Invoke(dt);
        // petit geste de travail
        chipTimer -= dt;
        if (chipTimer <= 0f && job.duration > 0f && job.label != "Repos")
        {
            chipTimer = 1.1f;
            FloatingText.Spawn(transform.position + new Vector3(facing.x * 0.3f, 0.6f, 0f), WorkWord(), new Color(0.95f, 0.9f, 0.75f), 0.5f);
        }
        if (workLeft <= 0f)
        {
            var j = job;
            job = null;
            phase = Phase.Idle;
            j.onDone?.Invoke();
            if (j.hideWhenDone) SetVisible(false);
        }
    }

    private string WorkWord()
    {
        switch (job.label)
        {
            case "Bûcheron": return "toc";
            case "Carrier": return "tac";
            case "Bâtisseur": return "pan";
            case "Pêcheur": return "~";
            case "Fermier": return "·";
            default: return "";
        }
    }

    private void Animate()
    {
        if (animator == null) return;
        if (hasSpeed) animator.SetFloat("Speed", velocity.sqrMagnitude);
        if (hasX) animator.SetFloat("MoveX", facing.x);
        if (hasY) animator.SetFloat("MoveY", facing.y);
    }
}
