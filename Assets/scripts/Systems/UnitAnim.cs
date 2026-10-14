using UnityEngine;

/// <summary>
/// Aides d'animation communes aux unités : blessure et mort (joue l'animation avant de détruire).
/// Fonctionne avec les contrôleurs générés (paramètres "Hurt" et "Dead"), sans erreur sinon.
/// </summary>
public static class UnitAnim
{
    public static bool HasParam(Animator a, string name)
    {
        if (a == null || a.runtimeAnimatorController == null) return false;
        foreach (var p in a.parameters)
            if (p.name == name) return true;
        return false;
    }

    public static void Hurt(Animator a)
    {
        if (HasParam(a, "Hurt")) a.SetTrigger("Hurt");
    }

    /// <summary>
    /// Désactive l'unité (IA, collisions) et joue l'animation de mort, puis la détruit.
    /// </summary>
    public static void Die(GameObject go, MonoBehaviour ai, float delay = 1.1f)
    {
        if (ai != null) ai.enabled = false;
        foreach (var c in go.GetComponentsInChildren<Collider2D>()) c.enabled = false;
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }
        var hb = go.GetComponentInChildren<HealthBar>();
        if (hb != null) hb.gameObject.SetActive(false);

        var a = go.GetComponent<Animator>();
        if (HasParam(a, "Dead"))
        {
            if (HasParam(a, "IsAttacking")) a.SetBool("IsAttacking", false);
            if (HasParam(a, "Speed")) a.SetFloat("Speed", 0f);
            a.SetBool("Dead", true);
            Object.Destroy(go, delay);
        }
        else
        {
            Object.Destroy(go);
        }
    }
}
