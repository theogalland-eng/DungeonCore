using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ═════════════════════════════════════════════
// ROCHER ROULANT : au bout d'un couloir ; dévale la ligne quand un intrus s'y engage
// ═════════════════════════════════════════════
public class BoulderTrap : TrapKit
{
    public float damage = 45f, speed = 7f, rearm = 30f;
    private Vector2Int dir;
    private int run;
    private int state;
    private Vector2 rollPos;
    private readonly HashSet<AdventurerCombat> hit = new HashSet<AdventurerCombat>();

    protected override void Start()
    {
        base.Start();
        // la plus longue ligne libre
        foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
        {
            int n = FreeRun(d, 9);
            if (n > run) { run = n; dir = d; }
        }
    }

    private void Update()
    {
        clock += Time.deltaTime;
        switch (state)
        {
            case 0:
                Show(0);
                if (sr != null) sr.enabled = true;
                for (int k = 1; k <= run; k++)
                    if (AnyIn(CellCenter(dir.x * k, dir.y * k), 0.45f)) { state = 1; clock = 0f; rollPos = Pos; hit.Clear(); Sfx.Play("door_break", Pos, 0.7f); break; }
                break;
            case 1:
                rollPos += (Vector2)dir * speed * Time.deltaTime;
                if (sr != null) sr.transform.position = rollPos;
                Show(1 + (int)(clock * 14f) % Mathf.Max(1, frames.Length - 1));
                CameraController.Instance?.Shake(0.03f, 0.05f);
                foreach (var v in Victims(rollPos, 0.55f))
                    if (hit.Add(v)) { Hurt(v, damage); Hold(v, 1f); }
                if (Vector2.Distance(rollPos, Pos) >= run + 0.2f)
                {
                    Fx.Play("smoke_puff", rollPos, 1.2f);
                    Sfx.Play("explosion", rollPos, 0.5f);
                    if (sr != null) { sr.enabled = false; sr.transform.localPosition = Vector3.zero; }
                    state = 2; clock = 0f;
                }
                break;
            case 2:
                if (clock >= rearm) { state = 0; Fx.Play("smoke_puff", Pos, 0.9f); }
                break;
        }
    }
}

// ═════════════════════════════════════════════
// PIÈGES DE THÈME
// ═════════════════════════════════════════════
