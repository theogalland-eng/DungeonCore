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

    [Header("Distance")]
    [Tooltip("Couleur du projectile (alpha 0 = attaque au corps à corps)")]
    public Color projectileColor = new Color(0, 0, 0, 0);

    [Header("Invocation (liche)")]
    public GameObject summonPrefab;
    public int summonCount;
    public float summonCooldown = 12f;
    public float summonLifetime = 40f;

    private GobelinAI owner;
    private readonly List<GobelinAI> minions = new List<GobelinAI>();
    private float nextSummon;

    private void Awake() => owner = GetComponent<GobelinAI>();

    public void OnHit(IDamageable target, float damage)
    {
        var tc = (target as Component);
        if (lifesteal > 0f && owner != null) owner.Heal(damage * lifesteal);
        if (tc == null) return;
        if (projectileColor.a > 0f) Projectile.Fire(transform.position + Vector3.up * 0.3f, tc.transform.position + Vector3.up * 0.2f, projectileColor);
        var ai = tc.GetComponent<AdventurerAI>();
        if (ai != null && slowSeconds > 0f) ai.Slow(slowMult, slowSeconds);
        if (burnDps > 0f && burnSeconds > 0f) DotEffect.Apply(tc.gameObject, burnDps, burnSeconds, new Color(1f, 0.55f, 0.2f));
    }

    private void Update()
    {
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
        if (unit != null) { unit.summoned = true; unit.nextStage = null; }
        if (ai != null)
        {
            ai.dungeonCore = owner.dungeonCore;
            ai.anchorPoint = owner.anchorPoint;
            ai.gobelinName = "Squelette invoqué";
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
    }
}
