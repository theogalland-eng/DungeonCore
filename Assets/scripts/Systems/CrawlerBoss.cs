using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS D'ÉTAGE (mode Aventurier) : le gardien de la dernière salle de chaque étage combat en trois phases.
///   1. (100 → 66 % PV) il se bat normalement ;
///   2. (sous 66 %) il rugit, s'enrage (plus vite, plus fort) et appelle deux créatures de l'étage ;
///   3. (sous 33 %) il frappe le sol toutes les quelques secondes : un cercle d'alerte, puis une onde de choc
///      qui blesse tout le groupe à portée — il faut s'en écarter.
/// Sa jauge de vie s'affiche en haut de l'écran quand le héros s'approche.
/// </summary>
public class CrawlerBoss : MonoBehaviour
{
    public static readonly List<CrawlerBoss> All = new List<CrawlerBoss>();

    public GobelinAI Ai { get; private set; }
    public string Title { get; private set; }
    public int Phase { get; private set; } = 1;
    public float Health01 => Ai != null && Ai.maxHealth > 0f ? Mathf.Clamp01(Ai.currentHealth / Ai.maxHealth) : 0f;

    private int floor;
    private float nextQuake;
    private const float QuakeRadius = 2.4f;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public static CrawlerBoss Attach(GobelinAI g, int floor)
    {
        if (g == null) return null;
        var b = g.gameObject.GetComponent<CrawlerBoss>() ?? g.gameObject.AddComponent<CrawlerBoss>();
        b.Ai = g;
        b.floor = floor;
        var mm = g.GetComponent<MonsterMind>();
        string role = floor >= CrawlerDungeon.FloorCount ? "Seigneur de l'antre" : $"Gardien de l'étage {floor}";
        b.Title = mm != null && mm.IsGuardian ? $"{mm.GivenName} — {role}" : g.gobelinName;
        return b;
    }

    /// <summary>Le boss le plus proche du héros (jauge du HUD), s'il est à portée.</summary>
    public static CrawlerBoss Near(Vector2 p, float range = 13f)
    {
        CrawlerBoss best = null;
        float bd = range;
        foreach (var b in All)
        {
            if (b == null || b.Ai == null || !b.Ai.IsAlive()) continue;
            float d = Vector2.Distance(b.transform.position, p);
            if (d < bd) { bd = d; best = b; }
        }
        return best;
    }

    private void Update()
    {
        if (Ai == null || !Ai.IsAlive()) { enabled = false; return; }
        float r = Health01;
        if (Phase == 1 && r < 0.66f) EnterPhase2();
        else if (Phase == 2 && r < 0.33f) EnterPhase3();
        if (Phase == 3 && Time.time >= nextQuake && HeroNear(6f)) StartCoroutine(Quake());
    }

    private bool HeroNear(float d)
    {
        var cm = CrawlerMode.Instance;
        return cm != null && cm.Player != null && Vector2.Distance(cm.Player.transform.position, transform.position) < d;
    }

    private void Shout(string text, Color c)
    {
        FloatingText.Spawn(transform.position + Vector3.up * 1.4f, text, c, 1.8f);
        Sfx.Play("growl", transform.position, 1f);
        CameraController.Instance?.Shake(0.12f, 0.35f);
    }

    private void EnterPhase2()
    {
        Phase = 2;
        Ai.chaseSpeed *= 1.3f;
        Ai.attackDamage *= 1.25f;
        Ai.attackCooldown *= 0.8f;
        Shout("À moi, mes enfants !", new Color(1f, 0.6f, 0.35f));
        GameHUD.Toast($"{Title} s'enrage et appelle des renforts", GameHUD.DangerColor);
        var fams = CrawlerDungeon.FamiliesOn(floor);
        var f = CrawlerMode.Instance != null ? CrawlerMode.Instance.FloorData(floor) : null;
        for (int i = 0; i < 2; i++)
        {
            Vector2 at = (Vector2)transform.position + Random.insideUnitCircle.normalized * 1.6f;
            var add = CrawlerDungeon.SpawnMonster(fams[Random.Range(0, fams.Length)], 1 + floor / 4, at, f);
            if (add != null) Fx.Play("smoke_puff", at, 1f);
        }
    }

    private void EnterPhase3()
    {
        Phase = 3;
        transform.localScale *= 1.1f;
        Ai.attackDamage *= 1.15f;
        nextQuake = Time.time + 1.5f;
        Shout("ASSEZ !", new Color(1f, 0.35f, 0.3f));
        GameHUD.Toast($"{Title} frappe le sol : écartez-vous des cercles rouges !", GameHUD.DangerColor);
    }

    private System.Collections.IEnumerator Quake()
    {
        nextQuake = Time.time + 5.5f;
        Vector3 at = transform.position;
        // alerte : un cercle rouge s'étend, puis l'onde frappe
        Fx.Play("smoke_ring", at, QuakeRadius, 6f, 16, new Color(1f, 0.25f, 0.2f, 0.9f));
        FloatingText.Spawn(at + Vector3.up * 1.2f, "!", new Color(1f, 0.3f, 0.25f), 1f);
        yield return new WaitForSeconds(1.1f);
        if (Ai == null || !Ai.IsAlive()) yield break;
        Fx.Play("explosion", at, QuakeRadius * 1.1f, 14f, 16, null, false, null, new Color(0.9f, 0.6f, 0.35f));
        Sfx.Play("explosion", at, 0.9f);
        CameraController.Instance?.Shake(0.18f, 0.4f);
        float dmg = Ai.attackDamage * 1.6f;
        var cm = CrawlerMode.Instance;
        if (cm != null && cm.Player != null && Vector2.Distance(cm.Player.transform.position, at) <= QuakeRadius) cm.Player.Combat.TakeDamage(dmg);
        foreach (var m in CrawlerCompanion.All)
            if (m != null && !m.IsDead && Vector2.Distance(m.transform.position, at) <= QuakeRadius) m.Combat.TakeDamage(dmg * 0.8f);
    }
}
