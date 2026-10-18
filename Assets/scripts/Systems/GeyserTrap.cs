using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Fournaise : évent qui bouillonne puis crache la lave.</summary>
public class GeyserTrap : TrapKit
{
    public float interval = 4f, damage = 35f;

    private void Update()
    {
        clock += Time.deltaTime;
        float ph = clock % interval;
        int f = ph < interval - 1.3f ? 0 : ph < interval - 0.5f ? 1 : ph < interval - 0.35f ? 2 : ph < interval - 0.15f ? 3 : 4;
        Show(f);
        if (f == 3)
            foreach (var v in Victims(Pos, 0.8f))
            {
                Hurt(v, damage, interval * 0.8f);
                DotEffect.Apply(v.gameObject, 4f, 3f, new Color(1f, 0.55f, 0.2f));
            }
    }
}
