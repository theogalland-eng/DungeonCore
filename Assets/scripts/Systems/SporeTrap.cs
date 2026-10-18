using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Jardin fongique : les champignons crachent des spores qui empoisonnent et aveuglent.</summary>
public class SporeTrap : TrapKit
{
    public float rearm = 9f;
    private int state;

    private void Update()
    {
        clock += Time.deltaTime;
        switch (state)
        {
            case 0:
                Show((int)(Time.time * 1.5f) % 2);
                if (AnyIn(Pos, 0.8f)) { state = 1; clock = 0f; Sfx.Play("trap_web", Pos, 0.5f); }
                break;
            case 1:
                Show(2 + Mathf.Min(2, (int)(clock * 8f)));
                if (clock >= 0.15f && clock - Time.deltaTime < 0.15f)
                {
                    Fx.Play("smoke_circle", Pos, 1.8f, 12f, 16, new Color(0.65f, 1f, 0.4f, 0.75f));
                    foreach (var v in Victims(Pos, 1.5f))
                    {
                        DotEffect.Apply(v.gameObject, 5f, 5f, new Color(0.55f, 1f, 0.35f));
                        var a = v.GetComponent<AdventurerAI>();
                        if (a != null) a.BlindUntil = Mathf.Max(a.BlindUntil, Time.time + 3f);
                    }
                }
                if (clock >= 0.5f) { state = 2; clock = 0f; }
                break;
            case 2:
                Show(0);
                if (clock >= rearm) state = 0;
                break;
        }
    }
}
