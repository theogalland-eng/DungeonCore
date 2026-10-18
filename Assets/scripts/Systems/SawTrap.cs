using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ═════════════════════════════════════════════
// SCIE AU SOL : court dans sa fente, dans l'axe du couloir ; ronge ce qui marche dessus
// ═════════════════════════════════════════════
public class SawTrap : TrapKit
{
    public float dps = 15f, fps = 12f;
    private float t, tick;

    protected override void Start()
    {
        base.Start();
        if (!HorizontalCorridor() && sr != null) sr.transform.localRotation = Quaternion.Euler(0, 0, 90f);
    }

    private void Update()
    {
        t += Time.deltaTime * fps;
        Show((int)t);
        if (frames.Length > 0 && (int)t >= frames.Length) t = 0f;
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.25f;
        foreach (var v in Victims(Pos, 0.5f)) { Hurt(v, dps * 0.25f); Slow(v, 0.7f, 0.4f); }
    }
}
