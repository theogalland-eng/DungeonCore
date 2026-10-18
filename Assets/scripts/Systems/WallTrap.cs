using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ═════════════════════════════════════════════
// PIÈGES MURAUX : lances (traversent le couloir), fléchettes et arbalète (tirent dans l'axe)
// ═════════════════════════════════════════════
public class WallTrap : TrapKit
{
    public enum Mode { Spears, Darts, Crossbow }
    public Mode mode;
    public float damage = 26f, interval = 3f;
    private Vector2Int dir;
    private int state;

    protected override void Start()
    {
        base.Start();
        dir = AwayFromWall(cell);
        if (dir == Vector2Int.zero) dir = Vector2Int.right;
        Face(dir);
    }

    private bool TargetInLine(int range)
    {
        int run = FreeRun(dir, range);
        for (int k = 0; k <= run; k++) if (AnyIn(CellCenter(dir.x * k, dir.y * k), 0.45f)) return true;
        return false;
    }

    private void Update()
    {
        clock += Time.deltaTime;
        switch (mode)
        {
            case Mode.Spears:
                // frappe à intervalle régulier : rentrées, sortie, coup, retour
                float ph = clock % interval;
                int f = ph < interval - 0.6f ? 0 : ph < interval - 0.45f ? 1 : ph < interval - 0.3f ? 2 : ph < interval - 0.15f ? 3 : 5;
                Show(f);
                if (f == 3)
                    for (int k = 0; k <= Mathf.Min(1, FreeRun(dir, 2)); k++)
                        foreach (var v in Victims(CellCenter(dir.x * k, dir.y * k), 0.45f)) Hurt(v, damage, interval * 0.8f);
                break;
            case Mode.Darts:
                Show(state == 1 && clock < 0.15f ? 1 : 0);
                if (clock >= interval && TargetInLine(6))
                {
                    clock = 0f; state = 1;
                    Sfx.Play("arrow_shoot", Pos, 0.6f);
                    for (int i = 0; i < 3; i++) Shoot(i * 0.12f, 12f, new Color(0.85f, 0.8f, 0.6f));
                }
                break;
            case Mode.Crossbow:
                Show(clock < 0.15f ? 1 : clock < interval * 0.5f ? 2 : 0);
                if (clock >= interval && TargetInLine(6))
                {
                    clock = 0f;
                    Sfx.Play("arrow_shoot", Pos, 0.8f);
                    Shoot(0f, damage, new Color(0.9f, 0.85f, 0.7f));
                }
                break;
        }
    }

    private void Shoot(float delay, float dmg, Color c)
    {
        int run = FreeRun(dir, 6);
        Vector2 to = CellCenter(dir.x * run, dir.y * run);
        Vector2 from = Pos + (Vector2)dir * 0.3f;
        StartCoroutine(Fly(delay, from, to, dmg, c));
    }

    private System.Collections.IEnumerator Fly(float delay, Vector2 from, Vector2 to, float dmg, Color c)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Projectile.Fire(from, to, c);
        // la première victime sur la trajectoire est touchée
        float len = Vector2.Distance(from, to);
        for (float d = 0f; d <= len; d += 0.4f)
        {
            var hit = Victims(from + (to - from).normalized * d, 0.35f);
            if (hit.Count > 0) { Hurt(hit[0], dmg); yield break; }
        }
    }
}
