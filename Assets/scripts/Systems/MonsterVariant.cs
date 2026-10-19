using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// VARIANTE D'ÉTAGE et BONUS « CHEZ SOI » d'un monstre (mode Donjon).
///   - Les familles COMMUNES (rat, slime, gobelin, squelette) nées sur un étage à thème en prennent la variante :
///     couleurs repeintes (SpriteRecolor), lueur et particules, un nom et un pouvoir propres
///     (slime de feu qui laisse une traînée brûlante, gobelin des glaces qui lance des javelots...).
///     La variante se garde en évoluant et se sauvegarde.
///   - Les familles NATIVES ont +15 % de PV et de dégâts quand elles se trouvent sur un étage de leur thème.
/// </summary>
public class MonsterVariant : MonoBehaviour
{
    public enum Fx { None, Ember, Drop, Snow, Spore, Sparkle, Wisp }

    public class Look
    {
        public SpriteRecolor.Ramp ramp;
        public Color glow;
        public Fx fx;
    }

    public class Def
    {
        public string name1, suffix, desc;
        public float burnDps, burnSecs, poisonDps, poisonSecs, slowMult = 1f, slowSecs, knock, blindSecs, manaPerHit, lifesteal;
        public string throwKind;      // "fire", "net", "ice"
        public float deathDmg, deathRadius, deathSlow, cloudDps;
        public bool trail;
        public float regen;           // fraction des PV max par seconde
        public float armor;           // dégâts subis en moins (0..1)
        public float reflect, dodge;
        public float hpMult = 1f, dmgMult = 1f, speedMult = 1f;
        public bool lure;
    }

    private static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
    {
        { "lava", new Look { ramp = new SpriteRecolor.Ramp { dark = new Color(0.45f, 0.08f, 0.04f), mid = new Color(0.95f, 0.38f, 0.1f), light = new Color(1f, 0.85f, 0.4f) }, glow = new Color(1f, 0.45f, 0.15f), fx = Fx.Ember } },
        { "aqua", new Look { ramp = new SpriteRecolor.Ramp { dark = new Color(0.05f, 0.18f, 0.35f), mid = new Color(0.15f, 0.55f, 0.75f), light = new Color(0.65f, 0.95f, 1f) }, glow = new Color(0.3f, 0.75f, 1f), fx = Fx.Drop } },
        { "ice", new Look { ramp = new SpriteRecolor.Ramp { dark = new Color(0.12f, 0.25f, 0.55f), mid = new Color(0.4f, 0.7f, 1f), light = new Color(0.92f, 0.98f, 1f), mix = 0.8f }, glow = new Color(0.65f, 0.85f, 1f), fx = Fx.Snow } },
        { "fungal", new Look { ramp = new SpriteRecolor.Ramp { dark = new Color(0.15f, 0.25f, 0.06f), mid = new Color(0.45f, 0.72f, 0.2f), light = new Color(0.85f, 1f, 0.55f) }, glow = new Color(0.5f, 1f, 0.35f), fx = Fx.Spore } },
        { "crystal", new Look { ramp = new SpriteRecolor.Ramp { dark = new Color(0.25f, 0.12f, 0.45f), mid = new Color(0.6f, 0.4f, 0.95f), light = new Color(0.95f, 0.85f, 1f) }, glow = new Color(0.75f, 0.5f, 1f), fx = Fx.Sparkle } },
        { "crypt", new Look { ramp = new SpriteRecolor.Ramp { dark = new Color(0.08f, 0.2f, 0.2f), mid = new Color(0.35f, 0.65f, 0.6f), light = new Color(0.8f, 1f, 0.95f), mix = 0.75f }, glow = new Color(0.4f, 1f, 0.85f), fx = Fx.Wisp } },
        { "catacomb", new Look { ramp = new SpriteRecolor.Ramp { dark = new Color(0.18f, 0.12f, 0.1f), mid = new Color(0.55f, 0.48f, 0.38f), light = new Color(0.95f, 0.9f, 0.78f), mix = 0.6f }, glow = new Color(0.9f, 0.85f, 0.6f), fx = Fx.Wisp } },
        { "fortress", new Look { ramp = new SpriteRecolor.Ramp { dark = new Color(0.15f, 0.16f, 0.2f), mid = new Color(0.5f, 0.52f, 0.58f), light = new Color(0.9f, 0.92f, 0.95f), mix = 0.55f }, glow = new Color(0, 0, 0, 0), fx = Fx.None } },
    };

    private static Def D(string n, string s, string d) => new Def { name1 = n, suffix = s, desc = d };

    private static readonly Dictionary<string, Def> Defs = new Dictionary<string, Def>
    {
        // ── Fournaise
        { "lava/rat", With(D("Rat des braises", "des braises", "explose en étincelles en mourant"), d => { d.deathDmg = 18f; d.deathRadius = 1.5f; }) },
        { "lava/slime", With(D("Slime de feu", "de feu", "laisse une traînée brûlante, ses coups brûlent"), d => { d.trail = true; d.burnDps = 4f; d.burnSecs = 3f; }) },
        { "lava/goblin", With(D("Gobelin pyromane", "pyromane", "lance des fioles de feu"), d => { d.throwKind = "fire"; }) },
        { "lava/skeleton", With(D("Squelette ardent", "ardent", "ses coups brûlent"), d => { d.burnDps = 5f; d.burnSecs = 4f; }) },
        // ── Sanctuaire englouti
        { "aqua/rat", With(D("Rat d'égout", "d'égout", "rapide dans l'eau"), d => { d.speedMult = 1.3f; }) },
        { "aqua/slime", With(D("Slime des marées", "des marées", "se régénère dans l'eau"), d => { d.regen = 0.025f; }) },
        { "aqua/goblin", With(D("Gobelin pêcheur", "pêcheur", "lance un filet qui ralentit"), d => { d.throwKind = "net"; }) },
        { "aqua/skeleton", With(D("Squelette noyé", "noyé", "ses coups repoussent"), d => { d.knock = 0.7f; }) },
        // ── Caverne gelée
        { "ice/rat", With(D("Rat des neiges", "des neiges", "sa morsure engourdit"), d => { d.slowMult = 0.8f; d.slowSecs = 1.2f; }) },
        { "ice/slime", With(D("Slime de givre", "de givre", "ses coups ralentissent"), d => { d.slowMult = 0.5f; d.slowSecs = 2f; }) },
        { "ice/goblin", With(D("Gobelin des glaces", "des glaces", "lance des javelots de glace"), d => { d.throwKind = "ice"; }) },
        { "ice/skeleton", With(D("Squelette gelé", "gelé", "éclate en éclats de glace en mourant"), d => { d.deathDmg = 15f; d.deathRadius = 1.6f; d.deathSlow = 0.5f; }) },
        // ── Jardin fongique
        { "fungal/rat", With(D("Rat pestiféré", "pestiféré", "sa morsure empoisonne"), d => { d.poisonDps = 3f; d.poisonSecs = 5f; }) },
        { "fungal/slime", With(D("Slime toxique", "toxique", "libère un nuage toxique en mourant"), d => { d.cloudDps = 4f; d.deathRadius = 1.6f; }) },
        { "fungal/goblin", With(D("Gobelin sporé", "sporé", "ses spores aveuglent"), d => { d.blindSecs = 3f; }) },
        { "fungal/skeleton", With(D("Squelette moussu", "moussu", "se régénère"), d => { d.regen = 0.015f; }) },
        // ── Temple de cristal
        { "crystal/rat", With(D("Rat de quartz", "de quartz", "cuirassé de cristal"), d => { d.armor = 0.25f; }) },
        { "crystal/slime", With(D("Slime de cristal", "de cristal", "renvoie 20 % des dégâts"), d => { d.reflect = 0.2f; }) },
        { "crystal/goblin", With(D("Gobelin prismatique", "prismatique", "son éclat attire les intrus"), d => { d.lure = true; d.hpMult = 1.2f; }) },
        { "crystal/skeleton", With(D("Squelette de cristal", "de cristal", "chaque coup draine du mana"), d => { d.manaPerHit = 3f; }) },
        // ── Crypte hantée et Catacombes
        { "crypt/rat", With(D("Rat spectral", "spectral", "évite un coup sur quatre"), d => { d.dodge = 0.25f; }) },
        { "crypt/slime", With(D("Slime d'ectoplasme", "d'ectoplasme", "son contact glace"), d => { d.slowMult = 0.6f; d.slowSecs = 1.5f; }) },
        { "crypt/goblin", With(D("Gobelin goule", "goule", "se nourrit de ses coups"), d => { d.lifesteal = 0.2f; }) },
        { "crypt/skeleton", With(D("Squelette des cryptes", "des cryptes", "frappe plus fort"), d => { d.dmgMult = 1.25f; }) },
        { "catacomb/rat", With(D("Rat des ossuaires", "des ossuaires", "évite un coup sur quatre"), d => { d.dodge = 0.25f; }) },
        { "catacomb/slime", With(D("Slime d'ossements", "d'ossements", "son contact glace"), d => { d.slowMult = 0.6f; d.slowSecs = 1.5f; }) },
        { "catacomb/goblin", With(D("Gobelin goule", "goule", "se nourrit de ses coups"), d => { d.lifesteal = 0.2f; }) },
        { "catacomb/skeleton", With(D("Squelette des catacombes", "des catacombes", "frappe plus fort"), d => { d.dmgMult = 1.25f; }) },
        // ── Forteresse
        { "fortress/rat", With(D("Rat de garnison", "de garnison", "cuirassé"), d => { d.armor = 0.2f; }) },
        { "fortress/slime", With(D("Slime d'acier", "d'acier", "très résistant, mais lent"), d => { d.armor = 0.3f; d.speedMult = 0.85f; }) },
        { "fortress/goblin", With(D("Gobelin en armure", "en armure", "+40 % de PV, plus lent"), d => { d.hpMult = 1.4f; d.speedMult = 0.85f; }) },
        { "fortress/skeleton", With(D("Squelette légionnaire", "légionnaire", "son bouclier pare un quart des dégâts"), d => { d.armor = 0.25f; }) },
    };

    private static Def With(Def d, System.Action<Def> f) { f(d); return d; }

    /// <summary>Description courte de la variante (fiche du monstre), ou null.</summary>
    public static string Describe(string theme, string family) =>
        theme != null && Defs.TryGetValue(theme + "/" + family, out var d) ? $"{d.name1} : {d.desc}" : null;

    /// <summary>Nom de la variante au stade 1 (onglet Monstres), ou null.</summary>
    public static string VariantName(string theme, string family) =>
        theme != null && Defs.TryGetValue(theme + "/" + family, out var d) ? d.name1 : null;

    // ── État
    public string theme;          // thème de la variante (null : aucune)
    public bool decided;          // déjà fixée (naissance, évolution, chargement)
    private GobelinAI ai;
    private MonsterUnit unit;
    private SpriteRenderer sr;
    private Def def;
    private Look look;
    private bool homeApplied;
    private float nextCheck, nextThrow, nextTrail, nextFx, nextLure, lastIncoming;
    private readonly List<Transform> motes = new List<Transform>();

    public Def Variant => def;

    /// <summary>Transmet la variante à la forme évoluée (ou au monstre rechargé).</summary>
    public static void Inherit(GameObject go, string theme)
    {
        if (go == null) return;
        var v = go.GetComponent<MonsterVariant>() ?? go.AddComponent<MonsterVariant>();
        v.theme = theme;
        v.decided = true;
    }

    private void Start()
    {
        ai = GetComponent<GobelinAI>();
        unit = GetComponent<MonsterUnit>();
        sr = GetComponent<SpriteRenderer>();
        if (ai == null || unit == null || CrawlerMode.Active) { enabled = false; return; }
        if (!decided)
        {
            decided = true;
            string t = MonsterHabitat.ThemeAt(transform.position);
            if (MonsterHabitat.IsCommon(unit.familyId) && t != "volcanic" && !unit.summoned) theme = t;
        }
        if (theme != null && Defs.TryGetValue(theme + "/" + unit.familyId, out def))
        {
            Looks.TryGetValue(theme, out look);
            ApplyStats();
            ai.gobelinName = unit.stage <= 1 ? def.name1 : $"{unit.familyName} {def.suffix}";
            if (look != null && look.glow.a > 0f)
            {
                var g = new GameObject("Lueur de variante");
                g.transform.SetParent(transform, false);
                var l = g.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
                l.lightType = Light2D.LightType.Point;
                l.color = look.glow;
                l.intensity = 0.55f;
                l.pointLightOuterRadius = 1.3f;
            }
        }
        else theme = null;
    }

    private void ApplyStats()
    {
        if (def == null) return;
        ai.maxHealth *= def.hpMult;
        ai.currentHealth *= def.hpMult;
        ai.attackDamage *= def.dmgMult;
        ai.chaseSpeed *= def.speedMult;
        ai.patrolSpeed *= def.speedMult;
    }

    private void LateUpdate()
    {
        // repeint l'image courante de l'animation
        if (look != null && sr != null && sr.sprite != null && !SpriteRecolor.IsRecolored(sr.sprite))
            sr.sprite = SpriteRecolor.Get(sr.sprite, theme, look.ramp);
    }

    private void Update()
    {
        if (ai == null || !ai.IsAlive()) return;
        // chez soi (familles natives)
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 1f;
            bool home = MonsterHabitat.IsHome(unit.familyId, MonsterHabitat.ThemeAt(transform.position));
            if (home != homeApplied)
            {
                float m = home ? 1.15f : 1f / 1.15f;
                ai.maxHealth *= m;
                ai.currentHealth *= m;
                ai.attackDamage *= m;
                homeApplied = home;
            }
        }
        if (def == null) return;
        if (def.regen > 0f && ai.currentHealth < ai.maxHealth) ai.Heal(ai.maxHealth * def.regen * Time.deltaTime);
        if (def.trail && Time.time >= nextTrail && ai.IsOnMission) { nextTrail = Time.time + 0.6f; FireTrail.Drop(transform.position); }
        if (def.throwKind != null && Time.time >= nextThrow) TryThrow();
        if (def.lure && Time.time >= nextLure) Lure();
        if (look != null && look.fx != Fx.None && Time.time >= nextFx) { nextFx = Time.time + 0.45f; Mote(); }
    }

    // ── Pouvoirs (appelés par GobelinAI)
    public void OnHit(IDamageable target, float dmg)
    {
        if (def == null) return;
        var tc = target as Component;
        if (def.lifesteal > 0f) ai.Heal(dmg * def.lifesteal);
        if (def.manaPerHit > 0f && ManaSystem.Instance != null) ManaSystem.Instance.Add(Mathf.RoundToInt(def.manaPerHit));
        if (tc == null) return;
        var a = tc.GetComponent<AdventurerAI>();
        if (def.burnDps > 0f) DotEffect.Apply(tc.gameObject, def.burnDps, def.burnSecs, new Color(1f, 0.55f, 0.2f));
        if (def.poisonDps > 0f) DotEffect.Apply(tc.gameObject, def.poisonDps, def.poisonSecs, new Color(0.55f, 1f, 0.35f));
        if (a == null) return;
        if (def.slowSecs > 0f) a.Slow(def.slowMult, def.slowSecs);
        if (def.blindSecs > 0f) a.BlindUntil = Mathf.Max(a.BlindUntil, Time.time + def.blindSecs);
        if (def.knock > 0f)
        {
            Vector2 dir = ((Vector2)(tc.transform.position - transform.position)).normalized;
            Vector2 to = (Vector2)tc.transform.position + dir * def.knock;
            if (Physics2D.OverlapCircle(to, 0.2f, 1 << 6) == null)
            {
                var rb = tc.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = to; else tc.transform.position = to;
            }
        }
    }

    /// <summary>Dégâts reçus, après armure et esquive.</summary>
    public float Incoming(float amount)
    {
        if (def == null) return amount;
        if (def.dodge > 0f && Random.value < def.dodge)
        {
            FloatingText.Spawn(transform.position + Vector3.up * 0.6f, "Esquive !", new Color(0.7f, 0.9f, 1f), 0.8f);
            lastIncoming = 0f;
            return 0f;
        }
        amount *= 1f - def.armor;
        lastIncoming = amount;
        return amount;
    }

    public void OnAttackedBy(Transform attacker)
    {
        if (def == null || def.reflect <= 0f || attacker == null || lastIncoming <= 0f) return;
        var c = attacker.GetComponent<AdventurerCombat>();
        if (c != null && c.currentHealth > 0f) { c.TakeDamage(lastIncoming * def.reflect); lastIncoming = 0f; }
    }

    public void OnDeath()
    {
        if (def == null) return;
        Vector2 at = transform.position;
        if (def.deathDmg > 0f)
        {
            var color = theme == "ice" ? new Color(0.7f, 0.9f, 1f) : new Color(1f, 0.6f, 0.25f);
            global::Fx.Play("explosion", at, def.deathRadius * 1.2f, 18f, 16, null, false, null, color);
            foreach (var a in AdventurerAI.All.ToArray())
                if (a != null && !a.IsDead && a.combat != null && Vector2.Distance(a.transform.position, at) <= def.deathRadius)
                {
                    a.combat.TakeDamage(def.deathDmg);
                    if (def.deathSlow > 0f) a.Slow(def.deathSlow, 2.5f);
                }
        }
        if (def.cloudDps > 0f)
        {
            global::Fx.Play("smoke_circle", at, def.deathRadius * 1.3f, 12f, 16, new Color(0.5f, 1f, 0.35f, 0.75f));
            foreach (var a in AdventurerAI.All.ToArray())
                if (a != null && !a.IsDead && Vector2.Distance(a.transform.position, at) <= def.deathRadius)
                    DotEffect.Apply(a.gameObject, def.cloudDps, 4f, new Color(0.55f, 1f, 0.35f));
        }
    }

    private void TryThrow()
    {
        var target = ai.CombatTarget;
        if (target == null) return;
        float d = Vector2.Distance(target.position, transform.position);
        if (d > 4.5f || d < 1.3f) return;
        nextThrow = Time.time + 4f;
        var t = target;
        float dmg = ai.attackDamage * 0.8f;
        Color c = def.throwKind == "fire" ? new Color(1f, 0.55f, 0.2f) : def.throwKind == "ice" ? new Color(0.7f, 0.9f, 1f) : new Color(0.85f, 0.8f, 0.6f);
        Sfx.Play("swing", transform.position, 0.6f);
        Projectile.Fire(transform.position + Vector3.up * 0.3f, t.position + Vector3.up * 0.2f, c);
        StartCoroutine(Land(t, dmg));
    }

    private System.Collections.IEnumerator Land(Transform t, float dmg)
    {
        yield return new WaitForSeconds(0.35f);
        if (t == null) yield break;
        var c = t.GetComponent<AdventurerCombat>();
        var a = t.GetComponent<AdventurerAI>();
        if (c == null || c.currentHealth <= 0f) yield break;
        switch (def.throwKind)
        {
            case "fire":
                c.TakeDamage(dmg);
                DotEffect.Apply(t.gameObject, 4f, 3f, new Color(1f, 0.55f, 0.2f));
                global::Fx.Play("explosion", t.position, 0.9f, 18f, 16, null, false, null, new Color(1f, 0.55f, 0.2f));
                break;
            case "ice":
                c.TakeDamage(dmg);
                if (a != null) a.Slow(0.55f, 2f);
                break;
            default:   // filet
                if (a != null) { a.Slow(0.4f, 2.5f); a.Speak("Un filet !", new Color(0.85f, 0.85f, 0.7f)); }
                break;
        }
    }

    private void Lure()
    {
        nextLure = Time.time + 5f;
        foreach (var a in AdventurerAI.All)
            if (a != null && !a.IsDead && a.IsInsideDungeon && !a.IsFighting && Vector2.Distance(a.transform.position, transform.position) < 4.5f)
            {
                a.OnAttacked(transform);
                a.Speak("Qu'est-ce qui brille, là ?", new Color(0.85f, 0.75f, 1f));
                break;
            }
    }

    // ── Particules : petites taches qui montent (braises), tombent (neige), flottent (spores, étincelles)
    private static Sprite dot;

    private void Mote()
    {
        if (dot == null)
        {
            var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            dot = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
        }
        var go = new GameObject("Particule");
        go.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * 0.3f) + Vector3.up * 0.3f;
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = dot;
        s.sortingOrder = (sr != null ? sr.sortingOrder : 10) + 1;
        Color c = look.glow.a > 0f ? look.glow : Color.white;
        s.color = Color.Lerp(c, Color.white, 0.3f);
        Vector2 vel = look.fx == Fx.Snow ? new Vector2(Random.Range(-0.1f, 0.1f), -0.25f)
                    : look.fx == Fx.Drop ? new Vector2(0f, -0.4f)
                    : new Vector2(Random.Range(-0.15f, 0.15f), 0.35f);
        go.AddComponent<MoteFade>().Init(vel, 1.1f);
    }

    private class MoteFade : MonoBehaviour
    {
        private Vector2 vel; private float life, max; private SpriteRenderer s;
        public void Init(Vector2 v, float l) { vel = v; life = max = l; s = GetComponent<SpriteRenderer>(); }
        private void Update()
        {
            life -= Time.deltaTime;
            transform.position += (Vector3)(vel * Time.deltaTime);
            if (s != null) { var c = s.color; c.a = Mathf.Clamp01(life / max); s.color = c; }
            if (life <= 0f) Destroy(gameObject);
        }
    }
}

/// <summary>Flaque de feu laissée par un slime de feu : brûle les intrus qui marchent dedans.</summary>
public class FireTrail : MonoBehaviour
{
    private float life = 3f;
    private float nextTick;
    private SpriteRenderer sr;
    private static Sprite sprite;

    public static void Drop(Vector2 at)
    {
        var go = new GameObject("Traînée de feu");
        go.transform.position = at + Vector2.down * 0.15f;
        var t = go.AddComponent<FireTrail>();
        t.sr = go.AddComponent<SpriteRenderer>();
        t.sr.sprite = Sprite();
        t.sr.sortingOrder = 2;
        var l = go.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
        l.lightType = Light2D.LightType.Point;
        l.color = new Color(1f, 0.5f, 0.15f);
        l.intensity = 0.5f;
        l.pointLightOuterRadius = 0.8f;
    }

    private static Sprite Sprite()
    {
        if (sprite != null) return sprite;
        const int n = 10;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y * 1.6f), new Vector2(4.5f, 4.5f * 1.6f)) / 4.5f;
                Color c = d < 0.45f ? new Color(1f, 0.85f, 0.35f) : d < 0.8f ? new Color(1f, 0.45f, 0.1f) : d < 1f ? new Color(0.6f, 0.15f, 0.05f, 0.8f) : Color.clear;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 16f);
        return sprite;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        if (sr != null) { var c = sr.color; c.a = Mathf.Clamp01(life / 1f); sr.color = c; }
        if (life <= 0f) { Destroy(gameObject); return; }
        if (Time.time < nextTick) return;
        nextTick = Time.time + 0.5f;
        foreach (var a in AdventurerAI.All)
            if (a != null && !a.IsDead && Vector2.Distance(a.transform.position, transform.position) < 0.55f)
                DotEffect.Apply(a.gameObject, 4f, 2f, new Color(1f, 0.55f, 0.2f));
    }
}
