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
        if (critChance > 0f && target.IsAlive() && Random.value < critChance)
        {
            target.TakeDamage(damage);   // le trait trouve le défaut de l'armure : dégâts doublés
            FloatingText.Spawn(tc.transform.position + Vector3.up * 0.9f, "Critique !", new Color(1f, 0.85f, 0.35f), 0.9f);
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
        if (owner != null && owner.IsAlive()) SpinWebs();
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
