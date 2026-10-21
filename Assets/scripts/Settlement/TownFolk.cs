using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// PASSANTS DE LA CITÉ : des habitants qui vont et viennent dans les rues (marché, taverne, chapelle, portes),
/// s'arrêtent bavarder, rentrent chez eux le soir et ressortent au matin. En mode Aventurier on peut leur parler :
/// la plupart n'ont rien d'important à dire, mais la ville vit.
/// </summary>
[RequireComponent(typeof(Seeker))]
public class TownFolk : MonoBehaviour
{
    public static readonly List<TownFolk> All = new List<TownFolk>();

    public string folkName = "Passant";
    public string trade = "passant";
    public bool female;
    public SettlementBuilding home;
    public string Title => $"{folkName}, {trade}";

    private Seeker seeker;
    private Animator animator;
    private SpriteRenderer sr;
    private Path path;
    private int wp;
    private Vector2 target, facing = Vector2.down, velocity;
    private float waitUntil, repathAt, stuckAt, talkUntil, nextChat;
    private Vector2 stuckPos;
    private bool moving, hidden, goingHome, hasSpeed, hasX, hasY;
    private float speed;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        FeetSort.Attach(gameObject);
        seeker = GetComponent<Seeker>();
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        hasSpeed = UnitAnim.HasParam(animator, "Speed");
        hasX = UnitAnim.HasParam(animator, "MoveX");
        hasY = UnitAnim.HasParam(animator, "MoveY");
        speed = Random.Range(0.9f, 1.25f);
        waitUntil = Time.time + Random.Range(0f, 4f);
        nextChat = Time.time + Random.Range(8f, 30f);
    }

    /// <summary>Le joueur lui parle : il s'arrête et le regarde.</summary>
    public void FaceAndStop(Vector2 who, float seconds = 4f)
    {
        talkUntil = Time.time + seconds;
        moving = false;
        path = null;
        var d = who - (Vector2)transform.position;
        if (d.sqrMagnitude > 0.01f) facing = d.normalized;
    }

    public void Say(string text) => SpeechBubble.Show(transform, text, new Color(0.45f, 0.4f, 0.32f), 2.6f);

    private void Update()
    {
        velocity = Vector2.zero;
        var gm = GameManager.Instance;
        bool night = gm != null && gm.IsNight;
        if (Time.time < talkUntil) { Animate(); return; }

        if (night && !goingHome && !hidden) { goingHome = true; Go(HomeDoor()); }
        if (!night && hidden) { Show(true); goingHome = false; waitUntil = Time.time + Random.Range(0f, 6f); }
        if (hidden) return;

        if (moving) Move();
        else if (goingHome) { Show(false); }
        else if (Time.time >= waitUntil) Go(PickDestination());
        else if (Time.time >= nextChat)
        {
            nextChat = Time.time + Random.Range(15f, 45f);
            if (Random.value < 0.5f) Say(TownTalk.Ambient());
        }
        Animate();
    }

    private void Show(bool v)
    {
        hidden = !v;
        if (sr != null) sr.enabled = v;
        foreach (var c in GetComponentsInChildren<Renderer>()) c.enabled = v;
    }

    private Vector2 HomeDoor()
    {
        if (home != null) return home.WorkSpot(0);
        return transform.position;
    }

    private Vector2 PickDestination()
    {
        var sm = SettlementManager.Instance;
        var site = sm != null ? sm.Site : null;
        float r = Random.value;
        // devant un bâtiment achevé (marché et taverne plus souvent), sinon un coin de rue
        if (sm != null && r < 0.65f)
        {
            var list = new List<SettlementBuilding>();
            foreach (var b in sm.Buildings)
                if (b != null && b.IsComplete && b.rect.width > 0 && (site == null || site.cityRect.Overlaps(b.rect) || Random.value < 0.15f)) list.Add(b);
            if (list.Count > 0)
            {
                var b = list[Random.Range(0, list.Count)];
                foreach (var x in list) if ((x.def.id == "market" || x.def.id == "house_1") && Random.value < 0.25f) b = x;
                return b.StampVisual != null ? (Vector2)b.transform.position + Vector2.down * 0.8f + Random.insideUnitCircle * 0.6f : b.WorkSpot(Random.Range(0, 3));
            }
        }
        if (site != null && site.cityStreets != null && site.cityStreets.Count > 0)
        {
            var c = site.cityStreets[Random.Range(0, site.cityStreets.Count)];
            return new Vector2(c.x + 0.5f, c.y + 0.5f);
        }
        return (Vector2)transform.position + Random.insideUnitCircle * 4f;
    }

    private void Go(Vector2 dest)
    {
        target = dest;
        moving = true;
        path = null;
        RequestPath();
        stuckAt = Time.time;
        stuckPos = transform.position;
    }

    private void RequestPath()
    {
        repathAt = Time.time + 4f;
        Vector2 t = target;
        var graph = AstarPath.active != null ? AstarPath.active.data.gridGraph : null;
        if (graph != null)
        {
            var nn = graph.GetNearest(t, NNConstraint.Default);
            if (nn.node != null) t = (Vector3)nn.node.position;
        }
        if (seeker != null) seeker.StartPath(transform.position, t, p => { if (!p.error) { path = p; wp = 0; } });
    }

    private void Move()
    {
        Vector2 pos = transform.position;
        if (Vector2.Distance(pos, target) < 0.5f || (path != null && wp >= path.vectorPath.Count && Vector2.Distance(pos, target) < 1.5f))
        {
            moving = false;
            waitUntil = Time.time + Random.Range(3f, 10f);
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
        if (Time.time - stuckAt > 4f)
        {
            if (Vector2.Distance(pos, stuckPos) < 0.3f) { moving = false; waitUntil = Time.time + 2f; }
            stuckAt = Time.time;
            stuckPos = pos;
        }
    }

    private void Animate()
    {
        if (animator == null) return;
        if (hasSpeed) animator.SetFloat("Speed", velocity.sqrMagnitude);
        if (hasX) animator.SetFloat("MoveX", facing.x);
        if (hasY) animator.SetFloat("MoveY", facing.y);
    }

    // ─────────────────────────────────────────────
    // PEUPLEMENT
    // ─────────────────────────────────────────────

    private static readonly (string m, string f)[] Trades =
    {
        ("passant", "passante"), ("marchand", "marchande"), ("garde", "garde"), ("apprenti", "apprentie"), ("lavandier", "lavandière"),
        ("vieux marin", "vieille poissonnière"), ("porteur", "porteuse"), ("ménestrel", "ménestrelle"), ("boulanger", "boulangère"), ("pèlerin", "pèlerine"),
    };

    /// <summary>Ajuste le nombre de passants à la taille de la cité.</summary>
    public static void Populate(SettlementManager sm, int wanted, Transform root)
    {
        All.RemoveAll(f => f == null);
        var cat = DungeonCatalog.Get();
        if (cat == null || cat.settlerPrefabs == null || cat.settlerPrefabs.Count == 0 || sm.Site == null) return;
        var homes = new List<SettlementBuilding>();
        foreach (var b in sm.Buildings) if (b != null && b.IsComplete && b.def.id.StartsWith("home_")) homes.Add(b);
        var streets = sm.Site.cityStreets;
        for (int i = All.Count; i < wanted; i++)
        {
            var prefab = cat.settlerPrefabs[Random.Range(0, cat.settlerPrefabs.Count)];
            if (prefab == null) continue;
            Vector3 at = streets != null && streets.Count > 0 ? (Vector3)streets[Random.Range(0, streets.Count)] + new Vector3(0.5f, 0.5f, 0f) : sm.Site.LandingWorld;
            var go = Object.Instantiate(prefab, at, Quaternion.identity, root);
            var settler = go.GetComponent<SettlerAI>();
            if (settler != null) Object.DestroyImmediate(settler);
            var f = go.AddComponent<TownFolk>();
            string n = SettlerNames.Pick();
            f.folkName = n;
            f.female = SettlerNames.TradeFor(n, "Pêcheur") == "pêcheuse";
            var tr = Trades[Random.Range(0, Trades.Length)];
            f.trade = f.female ? tr.f : tr.m;
            if (homes.Count > 0) f.home = homes[i % homes.Count];
            go.name = "Passant " + n;
        }
        while (All.Count > wanted)
        {
            var f = All[All.Count - 1];
            All.RemoveAt(All.Count - 1);
            if (f != null) Object.Destroy(f.gameObject);
        }
    }
}

/// <summary>Ce que racontent les passants (pour l'ambiance, parfois un conseil).</summary>
public static class TownTalk
{
    private static readonly string[] AmbientLines =
    {
        "Belle journée.", "Le pain est cher, cette semaine.", "Tu as vu la nouvelle tour ?", "Encore des aventuriers...",
        "Ma mère disait de ne jamais aller à la montagne.", "Hé, attention où tu marches !", "Il paraît qu'il pleuvra.",
        "Le marché est plein, aujourd'hui.", "Les gardes ont encore attrapé un gobelin.", "On manque de bras au port.",
    };

    private static readonly string[] TalkLines =
    {
        "Je vends des poissons depuis vingt ans. Ce donjon m'en a pris deux, de clients.",
        "Les aventuriers boivent à crédit et ne reviennent pas toujours. La tavernière tient les comptes.",
        "Mon fils veut entrer à la Guilde. Je lui ai dit d'apprendre à nager d'abord.",
        "La nuit, on entend la montagne gronder. Moi, je ferme les volets.",
        "Si tu descends, prends des rations. Le ventre vide, on ne frappe pas fort.",
        "La chapelle soigne les blessés, pour quelques pièces. Les morts, eux, restent dehors.",
        "Il y a une maison à bâtir, près du port. Le charpentier attend un client riche.",
        "Le forgeron dit que plus on descend, plus les lames s'usent vite.",
        "J'ai vu un héros dormir dans la boue, une fois. Le lendemain, il avait perdu la moitié de sa bourse.",
        "On dit que le Noyau change le donjon pendant qu'on dort.",
        "Le seigneur du manoir paie des héros de rang S. Nous, on paie les impôts.",
        "Au marché, l'étal aux armes a parfois de belles pièces. Regarde le matin.",
        "Les gardes ferment la porte nord à la nuit tombée. Enfin, ils devraient.",
        "Moi ? Je ne fais rien. Je regarde passer les gens. C'est déjà beaucoup.",
        "Ne bois pas les potions vertes. Personne ne sait ce qu'il y a dedans.",
    };

    public static string Ambient() => AmbientLines[Random.Range(0, AmbientLines.Length)];
    public static string Talk() => TalkLines[Random.Range(0, TalkLines.Length)];
}
