using UnityEngine;

/// <summary>
/// Plaque de pression : l'intrus qui marche dessus déclenche le piège de la salle — des fléchettes jaillissent des murs,
/// les portes voisines claquent et restent bloquées quelques secondes (le groupe est coupé en deux), et vos monstres
/// proches sont alertés. Elle se réarme au bout de 10 s.
/// </summary>
public class PressurePlate : MonoBehaviour
{
    public Sprite upSprite, downSprite;
    public float damage = 9f;
    public float doorRadius = 4.5f;
    public float lockSeconds = 8f;
    public float rearm = 10f;

    private float readyAt;
    private SpriteRenderer sr;

    private void Start() => sr = GetComponentInChildren<SpriteRenderer>();

    private void FixedUpdate()
    {
        bool down = Time.time < readyAt;
        if (sr != null && downSprite != null) sr.sprite = down ? downSprite : upSprite;
        if (down) return;
        Vector2 p = transform.position;
        AdventurerCombat victim = null;
        foreach (var a in AdventurerAI.All)
            if (a != null && !a.IsDead && Vector2.Distance(a.transform.position, p) < 0.4f) { victim = a.combat; break; }
        if (victim == null && CrawlerMode.Instance != null && CrawlerMode.Instance.Player != null &&
            Vector2.Distance(CrawlerMode.Instance.Player.transform.position, p) < 0.4f) victim = CrawlerMode.Instance.Player.Combat;
        if (victim == null) return;

        readyAt = Time.time + rearm;
        Sfx.Play("trap_snap", p, 0.9f);
        FloatingText.Spawn(p + Vector2.up * 0.7f, "Clic !", new Color(1f, 0.8f, 0.5f), 1f);
        // fléchettes des murs
        Projectile.FireFx("arrow", (Vector3)p + new Vector3(-2.5f, 0.4f, 0f), victim.transform.position + Vector3.up * 0.2f, 0.4f, true,
            () => { if (victim != null && victim.IsAlive()) victim.TakeDamage(damage * Talents.PlateDamageMult); }, 18f, 0.1f);
        // les portes voisines claquent
        foreach (var d in DungeonDoor.All)
            if (d != null && Vector2.Distance(d.transform.position, p) <= doorRadius) d.Slam(lockSeconds);
        // vos monstres accourent
        foreach (var g in GobelinAI.All)
            if (g != null && g.IsAlive() && Vector2.Distance(g.transform.position, p) <= 8f) g.ReceiveAlert(p);
    }
}
