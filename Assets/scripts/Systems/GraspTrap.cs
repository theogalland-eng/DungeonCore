using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Crypte : des mains d'os jaillissent et agrippent les chevilles.</summary>
public class GraspTrap : TrapKit
{
    public float hold = 3f, damage = 10f, rearm = 8f;
    private int state;

    private void Update()
    {
        clock += Time.deltaTime;
        switch (state)
        {
            case 0:
                Show(0);
                if (AnyIn(Pos, 0.5f)) { state = 1; clock = 0f; Sfx.Play("trap_snap", Pos, 0.4f); }
                break;
            case 1:
                Show(1 + Mathf.Min(2, (int)(clock * 14f)));
                if (clock >= 0.2f)
                {
                    foreach (var v in Victims(Pos, 0.55f)) { Hurt(v, damage); Hold(v, hold); v.GetComponent<AdventurerAI>()?.Speak("Lâche-moi !", new Color(0.85f, 0.95f, 0.9f)); }
                    state = 2; clock = 0f;
                }
                break;
            case 2:
                Show(clock < hold ? 3 + (int)(clock * 3f) % 2 : 5);
                if (clock >= hold + 0.3f) { state = 3; clock = 0f; }
                break;
            case 3:
                Show(0);
                if (clock >= rearm) state = 0;
                break;
        }
    }
}
