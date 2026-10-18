using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Caverne gelée : l'ombre grandit, le glaçon tombe et éclate.</summary>
public class StalactiteTrap : TrapKit
{
    public float damage = 40f, rearm = 10f;
    private int state;

    private void Update()
    {
        clock += Time.deltaTime;
        switch (state)
        {
            case 0:
                Show(0);
                if (AnyIn(Pos, 0.6f)) { state = 1; clock = 0f; }
                break;
            case 1:
                Show(clock < 0.4f ? 1 : 2);
                if (clock >= 0.7f)
                {
                    Show(3);
                    Sfx.Play("trap_snap", Pos, 0.6f);
                    foreach (var v in Victims(Pos, 0.7f)) { Hurt(v, damage); Slow(v, 0.5f, 2f); }
                    state = 2; clock = 0f;
                }
                break;
            case 2:
                Show(clock < 0.6f ? 3 : 4);
                if (clock >= rearm) state = 0;
                break;
        }
    }
}
