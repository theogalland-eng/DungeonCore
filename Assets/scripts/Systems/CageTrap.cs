using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ═════════════════════════════════════════════
// CAGE TOMBANTE : tombe sur l'intrus et le retient 5 s pour vos monstres
// ═════════════════════════════════════════════
public class CageTrap : TrapKit
{
    public float holdSeconds = 5f, damage = 8f, rearm = 12f;
    private int state;   // 0 attente, 1 chute, 2 enfermé, 3 remontée, 4 réarmement

    private void Update()
    {
        clock += Time.deltaTime;
        switch (state)
        {
            case 0:
                Show(0);
                if (AnyIn(Pos, 0.45f)) { state = 1; clock = 0f; Sfx.Play("trap_snap", Pos, 0.6f); }
                break;
            case 1:
                Show(1 + Mathf.Min(2, (int)(clock * 14f)));
                if (clock >= 0.22f)
                {
                    foreach (var v in Victims(Pos, 0.55f)) { Hurt(v, damage); Hold(v, holdSeconds); v.GetComponent<AdventurerAI>()?.Speak("Une cage !", new Color(0.85f, 0.85f, 0.75f)); }
                    CameraController.Instance?.Shake(0.06f, 0.15f);
                    Sfx.Play("door_break", Pos, 0.5f);
                    state = 2; clock = 0f;
                }
                break;
            case 2:
                Show(4);
                if (clock >= holdSeconds) { state = 3; clock = 0f; }
                break;
            case 3:
                Show(clock < 0.15f ? 5 : 2);
                if (clock >= 0.35f) { state = 4; clock = 0f; Show(0); }
                break;
            case 4:
                if (clock >= rearm) state = 0;
                break;
        }
    }
}
