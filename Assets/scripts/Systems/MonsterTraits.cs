using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pouvoirs spéciaux d'un monstre, déclenchés à chaque coup porté (GobelinAI.Attack) :
/// vol de vie (vampires), ralentissement (champignons, fantômes), brûlure (démons, diablotins),
/// projectile visuel pour les attaques à distance, invocation de squelettes (liches).
/// </summary>
public class MonsterTraits : MonoBehaviour
{
    [Header("À chaque coup")]
    [Range(0f, 1f)] public float lifesteal;
    [Range(0f, 1f)] public float slowMult = 1f;
    public float slowSeconds;
    public float burnDps;
    public float burnSeconds;

    [Tooltip("Venin : dégâts par seconde (vert)")]
    public float poisonDps;
    public float poisonSeconds;
    [Tooltip("Coup critique (tireur d'élite) : chance de doubler les dégâts")]
    [Range(0f, 1f)] public float critChance;

    [Header("Distance")]
    [Tooltip("Couleur du projectile (alpha 0 = attaque au corps à corps)")]
    public Color projectileColor = new Color(0, 0, 0, 0);
    [Tooltip("Projectile animé de la bibliothèque d'effets (« arrow »...) ; vide = boule lumineuse")]
    public string projectileFx;

    [Header("Toiles (araignées)")]
    [Tooltip("Toiles tissées en même temps (0 = aucune)")]
    public int webCount;
    public Sprite webSprite;
    private readonly List<SpiderWeb> webs = new List<SpiderWeb>();
    private float nextWeb;

    [Header("Aura (chaman)")]
    [Tooltip("PV max rendus toutes les 4 s aux alliés de la même famille, en %")]
    public float healAuraPct;
    [Tooltip("Dégâts des alliés de la même famille autour de lui (1 = aucun bonus)")]
    public float buffAuraMult = 1f;
    private float nextAura;

    [Header("Flèches perçantes")]
    [Tooltip("La flèche traverse : dégâts infligés à l'intrus suivant (fraction)")]
    public float pierceMult;

    [Header("Mimique")]
    [Tooltip("Attire les intrus qui passent (« Un coffre ! »)")]
    public bool lureAsChest;
    [Tooltip("Engloutit : immobilise l'intrus mordu (secondes, une fois sur trois)")]
    public float swallowSeconds;
    private float nextLure;

    [Header("Rage (berserker)")]
    [Tooltip("Plus il est blessé, plus il frappe vite et fort (0 = aucun effet, 1 = jusqu'à +45 % vitesse d'attaque et +40 % de dégâts)")]
    public float berserk;
    private float baseCooldown = -1f, baseChase;
    private bool raging;

    [Header("Totem de guerre (orc chaman)")]
    public Sprite[] totemFrames;
    public float totemMult = 1f, totemHealPct, totemSeconds = 15f;
    private WarTotem totem;
    private float nextTotem;

    [Header("Chasseur de tireurs (chauves-souris)")]
    [Tooltip("Fond sur les archers, mages et soigneurs à portée plutôt que sur les guerriers")]
    public bool preferRanged;
    private float nextRetarget;

    /// <summary>Compteurs de pouvoirs déclenchés (tests et chroniques).</summary>
    public static int Executes, Howls, Pierces, Corrodes, Splashes;

    [Header("Achever les blessés (gnoll chasseur)")]
    [Tooltip("Dégâts en plus contre un intrus sous 40 % de ses PV (fraction)")]
    public float executeMult;

    [Header("Meute et hurlement (loups)")]
    [Tooltip("Dégâts en plus par loup de la meute à moins de 4 cases (3 au plus)")]
    public float packBonus;
    [Tooltip("Hurlement au combat : ralentit les intrus alentour (0 = aucun)")]
    public float howlSlow;
    [Tooltip("Le hurlement galvanise aussi la meute")]
    public float howlBuff = 1f;
    private float nextPack, nextHowl;

    [Header("Acide (goule)")]
    [Tooltip("Ronge l'armure : l'intrus touché subit plus de dégâts (multiplicateur) pendant corrodeSeconds")]
    public float corrodeMult = 1f;
    public float corrodeSeconds = 5f;

    [Header("Éclaboussure (nuée, souffle)")]
    [Tooltip("Dégâts infligés aux autres intrus autour de la cible (fraction)")]
    public float splashMult;
    public float splashRadius = 1.1f;
    [Tooltip("Souffle : seulement les intrus devant le monstre, dans un cône")]
    public bool splashCone;

    [Header("Nécromancie")]
    [Tooltip("Intrus tués relevés en squelettes en même temps (0 = aucun)")]
    public int raiseDead;
    public GameObject raisePrefab;
    private readonly List<GobelinAI> raised = new List<GobelinAI>();

    [Header("Invocation (liche)")]
    public GameObject summonPrefab;
    public int summonCount;
    public float summonCooldown = 12f;
    public float summonLifetime = 40f;
    public string summonName = "Squelette invoqué";

    private GobelinAI owner;
    private readonly List<GobelinAI> minions = new List<GobelinAI>();
    private float nextSummon;

    private void Awake() => owner = GetComponent<GobelinAI>();

    private void OnEnable() { if (raiseDead > 0) AdventurerAI.AnyDied += OnIntruderDied; }
    private void OnDisable() => AdventurerAI.AnyDied -= OnIntruderDied;

    public void OnHit(IDamageable target, float damage)
    {
        var tc = (target as Component);
        if (lifesteal > 0f && owner != null) owner.Heal(damage * lifesteal);
        if (tc == null) return;
        if (!string.IsNullOrEmpty(projectileFx))
        {
            Sfx.Play("arrow_shoot", transform.position, 0.7f);
            Projectile.FireFx(projectileFx, transform.position + Vector3.up * 0.3f, tc.transform.position + Vector3.up * 0.2f, 0.5f, true, null, 16f, 0.12f);
        }
        else if (projectileColor.a > 0f) Projectile.Fire(transform.position + Vector3.up * 0.3f, tc.transform.position + Vector3.up * 0.2f, projectileColor);
        var ai = tc.GetComponent<AdventurerAI>();
        if (ai != null && slowSeconds > 0f) ai.Slow(slowMult, slowSeconds);
        if (burnDps > 0f && burnSeconds > 0f) DotEffect.Apply(tc.gameObject, burnDps, burnSeconds, new Color(1f, 0.55f, 0.2f));
        if (poisonDps > 0f && poisonSeconds > 0f) DotEffect.Apply(tc.gameObject, poisonDps, poisonSeconds, new Color(0.55f, 1f, 0.35f));
        if (pierceMult > 0f) Pierce(tc.transform.position, damage * pierceMult);
        if (corrodeMult > 1f)
        {
            var ac = tc.GetComponent<AdventurerCombat>();
            if (ac != null) { ac.Corrode(corrodeMult, corrodeSeconds); Corrodes++; }
        }
        if (splashMult > 0f) Splash(tc, damage * splashMult);
        if (executeMult > 0f && target.IsAlive())
        {
            var ac = tc.GetComponent<AdventurerCombat>();
            if (ac != null && ac.currentHealth < ac.maxHealth * 0.4f)
            {
                target.TakeDamage(damage * executeMult);   // il sent le sang : coup de grâce
                Executes++;
                FloatingText.Spawn(tc.transform.position + Vector3.up * 0.9f, "Achevé !", new Color(1f, 0.5f, 0.35f), 0.9f);
            }
        }
        if (swallowSeconds > 0f && ai != null && target.IsAlive() && Random.value < 0.33f)
        {
            ai.Hold(swallowSeconds);
            ai.Speak("Il m'avale !", new Color(1f, 0.6f, 0.5f), true);
        }
        if (critChance > 0f && target.IsAlive() && Random.value < critChance)
        {
            target.TakeDamage(damage);   // le trait trouve le défaut de l'armure : dégâts doublés
            FloatingText.Spawn(tc.transform.position + Vector3.up * 0.9f, "Critique !", new Color(1f, 0.85f, 0.35f), 0.9f);
        }
    }

    /// <summary>La flèche traverse sa cible et touche l'intrus suivant sur sa trajectoire.</summary>
    private void Pierce(Vector2 hit, float dmg)
    {
        Vector2 dir = (hit - (Vector2)transform.position).normalized;
        for (float k = 0.6f; k <= 2.6f; k += 0.5f)
            foreach (var h in Physics2D.OverlapCircleAll(hit + dir * k, 0.4f, 1 << 7))
            {
                var d = h.GetComponent<IDamageable>();
                if (d == null || !d.IsAlive() || Vector2.Distance(h.transform.position, hit) < 0.3f) continue;
                d.TakeDamage(dmg);
                Pierces++;
                FloatingText.Spawn(h.transform.position + Vector3.up * 0.8f, "Transpercé !", new Color(0.9f, 0.9f, 0.8f), 0.8f);
                return;
            }
    }

    /// <summary>Chaman : soigne et galvanise les monstres de sa famille autour de lui.</summary>
    private void Aura()
    {
        if ((healAuraPct <= 0f && buffAuraMult <= 1f) || Time.time < nextAura) return;
        nextAura = Time.time + 4f;
        var me = GetComponent<MonsterUnit>();
        string fam = me != null ? me.familyId : null;
        bool any = false;
        foreach (var g in GobelinAI.All)
        {
            if (g == null || !g.IsAlive() || Vector2.Distance(g.transform.position, transform.position) > 3.5f) continue;
            var u = g.GetComponent<MonsterUnit>();
            if (u == null || u.familyId != fam) continue;
            if (buffAuraMult > 1f && g != owner) g.Buff(buffAuraMult, 4.5f);
            if (healAuraPct > 0f && g.currentHealth < g.maxHealth)
            {
                float h = g.maxHealth * healAuraPct;
                g.Heal(h);
                FloatingText.Spawn(g.transform.position + Vector3.up * 0.8f, $"+{h:0}", new Color(0.5f, 1f, 0.55f), 0.7f);
                any = true;
            }
        }
        if (any) Fx.Play("smoke_skull", transform.position + Vector3.up * 0.4f, 0.8f, 18f, 16, new Color(1f, 0.6f, 0.3f, 0.6f));
    }

    /// <summary>Berserker : la douleur le rend plus rapide et plus fort.</summary>
    private void Rage()
    {
        if (berserk <= 0f) return;
        if (baseCooldown < 0f) { baseCooldown = owner.attackCooldown; baseChase = owner.chaseSpeed; }
        float missing = 1f - Mathf.Clamp01(owner.currentHealth / Mathf.Max(1f, owner.maxHealth));
        owner.attackCooldown = baseCooldown * (1f - 0.45f * berserk * missing);
        owner.chaseSpeed = baseChase * (1f + 0.4f * berserk * missing);
        owner.Buff(1f + 0.4f * berserk * missing, 0.3f);
        bool now = missing > 0.5f;
        if (now && !raging) FloatingText.Spawn(transform.position + Vector3.up * 0.9f, "Rage !", new Color(1f, 0.3f, 0.25f), 1f);
        raging = now;
    }

    /// <summary>Orc chaman : plante un totem de guerre dès qu'il se bat (un à la fois).</summary>
    private void PlantTotem()
    {
        if (totemMult <= 1f || !owner.IsOnMission || Time.time < nextTotem || totem != null) return;
        nextTotem = Time.time + totemSeconds + 6f;
        var u = GetComponent<MonsterUnit>();
        Vector2 p = transform.position;
        totem = WarTotem.Plant(p + Random.insideUnitCircle * 0.4f, totemFrames, u != null ? u.familyId : null, totemMult, totemHealPct, totemSeconds, transform.parent);
        SpeechBubble.Show(transform, "Par le sang !", new Color(1f, 0.45f, 0.4f), 1.6f, 0.85f);
    }

    /// <summary>Chauve-souris : fond sur les tireurs (archers, mages, soigneurs) qui passent à portée.</summary>
    private void HuntRanged()
    {
        if (!preferRanged || !owner.IsOnMission || Time.time < nextRetarget) return;
        nextRetarget = Time.time + 1f;
        var cur = owner.MissionTarget != null ? owner.MissionTarget.GetComponent<AdventurerAI>() : null;
        if (cur != null && cur.role != AdventurerAI.Role.Warrior) return;
        AdventurerAI best = null;
        float bd = 6f;
        foreach (var a in AdventurerAI.All)
        {
            if (a == null || a.IsDead || a.role == AdventurerAI.Role.Warrior) continue;
            float d = Vector2.Distance(a.transform.position, transform.position);
            if (d < bd) { bd = d; best = a; }
        }
        if (best != null) owner.Retarget(best.transform);
    }

    /// <summary>Nuée (morsures tout autour de la cible) ou souffle (cône devant le monstre).</summary>
    private void Splash(Component main, float dmg)
    {
        Vector2 me = transform.position, at = main.transform.position;
        Vector2 dir = (at - me).normalized;
        Vector2 c = splashCone ? me : at;
        float r = splashCone ? (owner.attackRange + 1.2f) : splashRadius;
        foreach (var h in Physics2D.OverlapCircleAll(c, r, 1 << 7))
        {
            if (h.transform == main.transform) continue;
            var d = h.GetComponent<IDamageable>();
            if (d == null || !d.IsAlive()) continue;
            if (splashCone && Vector2.Angle(dir, (Vector2)h.transform.position - me) > 40f) continue;
            d.TakeDamage(dmg);
            Splashes++;
            if (burnDps > 0f && burnSeconds > 0f) DotEffect.Apply(h.gameObject, burnDps, burnSeconds, new Color(1f, 0.55f, 0.2f));
        }
        if (splashCone) Fx.Play("small_fire", (Vector3)(me + dir * 1.1f) + Vector3.up * 0.2f, 0.9f, 16f, 16);
    }

    /// <summary>Loup : plus fort entouré de sa meute.</summary>
    private void Pack()
    {
        if (packBonus <= 0f || Time.time < nextPack) return;
        nextPack = Time.time + 1f;
        var me = GetComponent<MonsterUnit>();
        int n = 0;
        foreach (var g in GobelinAI.All)
        {
            if (g == null || g == owner || !g.IsAlive() || Vector2.Distance(g.transform.position, transform.position) > 4f) continue;
            var u = g.GetComponent<MonsterUnit>();
            if (u != null && me != null && u.familyId == me.familyId && ++n >= 3) break;
        }
        if (n > 0) owner.Buff(1f + packBonus * n, 1.2f);
    }

    /// <summary>Loup de givre : au combat, un hurlement glacé ralentit les intrus (et l'alpha galvanise la meute).</summary>
    private void Howl()
    {
        if (howlSlow <= 0f || !owner.IsOnMission || Time.time < nextHowl) return;
        nextHowl = Time.time + 14f;
        Howls++;
        foreach (var a in AdventurerAI.All)
            if (a != null && !a.IsDead && Vector2.Distance(a.transform.position, transform.position) < 4.5f) a.Slow(1f - howlSlow, 3f);
        if (howlBuff > 1f)
        {
            var me = GetComponent<MonsterUnit>();
            foreach (var g in GobelinAI.All)
            {
                var u = g != null ? g.GetComponent<MonsterUnit>() : null;
                if (u != null && me != null && u.familyId == me.familyId && Vector2.Distance(g.transform.position, transform.position) < 5f) g.Buff(howlBuff, 5f);
            }
        }
        Voices.Monster(owner, "aggro", 1f);
        Fx.Play("ice", transform.position + Vector3.up * 0.2f, 1.6f, 14f, 8, new Color(0.7f, 0.9f, 1f, 0.8f));
        FloatingText.Spawn(transform.position + Vector3.up * 1.0f, "Aouuuh !", new Color(0.7f, 0.9f, 1f), 1f);
    }

    /// <summary>Mimique : un intrus qui passe voit un coffre... et s'en approche.</summary>
    private void LureAsChest()
    {
        if (!lureAsChest || Time.time < nextLure || owner.IsOnMission) return;
        nextLure = Time.time + 6f;
        foreach (var a in AdventurerAI.All)
            if (a != null && !a.IsDead && a.IsInsideDungeon && !a.IsFighting && Vector2.Distance(a.transform.position, transform.position) < 5f)
            {
                a.LifeGoTo(transform.position);
                a.Speak("Un coffre !", new Color(1f, 0.85f, 0.4f));
                break;
            }
    }

    /// <summary>Nécromancien : un intrus tombé tout près se relève en squelette à son service.</summary>
    private void OnIntruderDied(AdventurerAI dead)
    {
        if (dead == null || owner == null || !owner.IsAlive() || raisePrefab == null) return;
        Vector2 p = dead.transform.position;
        if (Vector2.Distance(p, transform.position) > 6f) return;
        raised.RemoveAll(m => m == null || !m.IsAlive());
        if (raised.Count >= raiseDead) return;
        var go = Instantiate(raisePrefab, p, Quaternion.identity, transform.parent);
        var unit = go.GetComponent<MonsterUnit>();
        if (unit != null) { unit.summoned = true; unit.nextStage = null; unit.branches = null; }
        var ai = go.GetComponent<GobelinAI>();
        if (ai != null)
        {
            ai.dungeonCore = owner.dungeonCore;
            ai.anchorPoint = owner.anchorPoint;
            ai.gobelinName = "Mort relevé";
            raised.Add(ai);
        }
        EvolutionFx.Play(p, new Color(0.45f, 1f, 0.6f), 0.7f);
        Fx.Play("smoke_skull", (Vector3)p + Vector3.up * 0.3f, 1.3f, 18f, 16, new Color(0.6f, 1f, 0.7f, 0.9f));
        SpeechBubble.Show(transform, "Relève-toi... et sers-moi.", new Color(0.65f, 1f, 0.75f), 2f, 0.85f);
        Destroy(go, 60f);
    }

    /// <summary>Araignée tisseuse : tend des toiles là où elle passe (davantage quand elle chasse).</summary>
    private void SpinWebs()
    {
        if (webCount <= 0 || webSprite == null || Time.time < nextWeb) return;
        nextWeb = Time.time + (owner.IsOnMission ? 6f : 22f) * Random.Range(0.8f, 1.2f);
        webs.RemoveAll(w => w == null);
        Vector2 p = transform.position;
        var t = DungeonTerritory.Instance;
        if (webs.Count >= webCount || SpiderWeb.Near(p, 1.3f) || (t != null && !t.IsInside(p))) return;
        webs.Add(SpiderWeb.Spin(p, webSprite, transform.parent));
    }

    private void Update()
    {
        if (owner != null && owner.IsAlive()) { SpinWebs(); Aura(); LureAsChest(); Rage(); PlantTotem(); HuntRanged(); Pack(); Howl(); }
        if (summonCount <= 0 || summonPrefab == null || owner == null || !owner.IsAlive()) return;
        minions.RemoveAll(m => m == null || !m.IsAlive());
        // La liche invoque quand un intrus est proche
        if (Time.time < nextSummon || minions.Count >= summonCount || !owner.IsOnMission) return;
        nextSummon = Time.time + summonCooldown;
        for (int i = minions.Count; i < summonCount; i++) Summon(i);
    }

    private void Summon(int i)
    {
        Vector2 pos = (Vector2)transform.position + (Vector2)(Quaternion.Euler(0, 0, 120f * i + 30f) * Vector2.right) * 0.6f;
        if (Physics2D.OverlapCircle(pos, 0.2f, 1 << 6) != null) pos = DungeonTerritory.FreeSpotNear(transform.position, true);
        var go = Instantiate(summonPrefab, pos, Quaternion.identity, transform.parent);
        var ai = go.GetComponent<GobelinAI>();
        var unit = go.GetComponent<MonsterUnit>();
        if (unit != null) { unit.summoned = true; unit.nextStage = null; unit.branches = null; }
        if (ai != null)
        {
            ai.dungeonCore = owner.dungeonCore;
            ai.anchorPoint = owner.anchorPoint;
            ai.gobelinName = summonName;
            minions.Add(ai);
        }
        EvolutionFx.Play(pos, new Color(0.55f, 1f, 0.6f), 0.6f);
        Fx.Play("smoke_skull", (Vector3)pos + Vector3.up * 0.3f, 1.3f, 18f, 16, new Color(0.7f, 1f, 0.75f, 0.9f));
        Destroy(go, summonLifetime);
    }

    private void OnDestroy()
    {
        // Les invocations se dissipent avec leur maître
        foreach (var m in minions) if (m != null) Destroy(m.gameObject, 0.2f);
        foreach (var m in raised) if (m != null) Destroy(m.gameObject, 0.2f);
        foreach (var w in webs) if (w != null) w.life = Mathf.Min(w.life, 3f);
    }
}
