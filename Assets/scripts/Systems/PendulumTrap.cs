using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ═════════════════════════════════════════════
// LAME PENDULAIRE : balaie la case sans fin ; frappe quand elle passe au centre
// ═════════════════════════════════════════════
public class PendulumTrap : TrapKit
{
    public float damage = 28f, fps = 9f;
    private float t;

    private void Update()
    {
        t += Time.deltaTime * fps;
        int f = (int)t % Mathf.Max(1, frames.Length);
        Show(f);
        // la lame passe au milieu de la case sur les images 2-3 et 7-8
        if (f == 2 || f == 3 || f == 7 || f == 8)
            foreach (var v in Victims(Pos, 0.55f)) Hurt(v, damage, 1.1f);
    }
}
