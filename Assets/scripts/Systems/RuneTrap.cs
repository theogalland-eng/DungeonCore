using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ═════════════════════════════════════════════
// RUNE EXPLOSIVE : rougeoie 0,8 s quand on marche dessus, puis souffle la zone
// ═════════════════════════════════════════════
public class RuneTrap : TrapKit
{
    public float damage = 70f, radius = 1.6f, rearm = 20f;
    private int state;
    private Light2D glow;

    protected override void Start()
    {
        base.Start();
        var g = new GameObject("Lueur");
        g.transform.SetParent(transform, false);
        Light(g, new Color(0.6f, 0.35f, 1f), 1.4f, 0.5f);
        glow = g.GetComponent<Light2D>();
    }

    private void Update()
    {
        clock += Time.deltaTime;
        switch (state)
        {
            case 0:
                Show((int)(Time.time * 2f) % 2);
                if (glow != null) { glow.color = new Color(0.6f, 0.35f, 1f); glow.intensity = 0.4f + 0.15f * Mathf.Sin(Time.time * 4f); }
                if (AnyIn(Pos, 0.5f)) { state = 1; clock = 0f; Sfx.Play("trap_snap", Pos, 0.5f); }
                break;
            case 1:   // avertissement : rouge clignotant
                Show(2 + (int)(clock * 10f) % 2);
                if (glow != null) { glow.color = new Color(1f, 0.3f, 0.2f); glow.intensity = 1.2f; }
                if (clock >= 0.8f)
                {
                    Show(4);
                    Fx.Play("explosion", Pos, radius * 1.3f, 18f, 16, null, false, null, new Color(1f, 0.45f, 0.3f));
                    Sfx.Play("explosion", Pos, 0.9f);
                    CameraController.Instance?.Shake(0.15f, 0.3f);
                    foreach (var v in Victims(Pos, radius)) Hurt(v, damage);
                    state = 2; clock = 0f;
                }
                break;
            case 2:
                Show(5);
                if (glow != null) glow.intensity = 0f;
                if (clock >= rearm) { state = 0; clock = 0f; }
                break;
        }
    }
}
