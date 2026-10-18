using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ═════════════════════════════════════════════
// TRAPPE : l'intrus tombe à l'étage du dessous (ou au fond d'une fosse)
// ═════════════════════════════════════════════
public class TrapdoorTrap : TrapKit
{
    public float rearm = 15f;
    private int state;

    private void Update()
    {
        clock += Time.deltaTime;
        switch (state)
        {
            case 0:
                Show(0);
                if (AnyIn(Pos, 0.4f)) { state = 1; clock = 0f; Sfx.Play("door_break", Pos, 0.5f); }
                break;
            case 1:
                Show(1 + Mathf.Min(2, (int)(clock * 12f)));
                if (clock >= 0.2f)
                {
                    foreach (var v in Victims(Pos, 0.5f)) Drop(v);
                    state = 2; clock = 0f;
                }
                break;
            case 2:
                Show(clock < 1.2f ? 3 : 5);
                if (clock >= 1.5f) { state = 3; clock = 0f; Show(0); }
                break;
            case 3:
                if (clock >= rearm) state = 0;
                break;
        }
    }

    private void Drop(AdventurerCombat v)
    {
        var a = v.GetComponent<AdventurerAI>();
        var layers = DungeonLayers.Instance;
        int li = layers != null ? layers.IndexAt(Pos) : 0;
        // un étage relié en dessous : il y tombe
        if (a != null && layers != null && li + 1 < layers.layers.Count && layers.layers[li + 1].unlocked && layers.layers[li + 1].connected)
        {
            Vector2 land = DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(layers.layers[li + 1].landingCell);
            var rb = v.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = land;
            v.transform.position = land;
            Hurt(v, 15f);
            a.Hold(2f);
            a.Speak("Aaaah !", new Color(1f, 0.8f, 0.6f));
            Fx.Play("smoke_puff", land, 1f);
        }
        else
        {
            Hurt(v, 30f);
            Hold(v, 3f);
            a?.Speak("Une fosse !", new Color(1f, 0.8f, 0.6f));
        }
    }
}
