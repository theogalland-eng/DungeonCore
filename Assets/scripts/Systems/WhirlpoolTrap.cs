using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Sanctuaire englouti : tourbillon qui aspire et noie.</summary>
public class WhirlpoolTrap : TrapKit
{
    private float t, tick;

    private void Update()
    {
        t += Time.deltaTime * 8f;
        Show((int)t % Mathf.Max(1, frames.Length));
        foreach (var v in Victims(Pos, 1.6f))
        {
            Vector2 p = v.transform.position;
            Vector2 to = Vector2.MoveTowards(p, Pos, 0.7f * Time.deltaTime);
            var rb = v.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = to; else v.transform.position = to;
            Slow(v, 0.5f, 0.3f);
        }
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.5f;
        foreach (var v in Victims(Pos, 0.6f)) Hurt(v, 2f);
    }
}
