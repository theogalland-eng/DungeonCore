using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Le cristal grandit et change d'aspect avec le niveau du Noyau : taille, éclat, couleur (bleu → violet → or),
/// éclats qui gravitent autour de lui.
/// </summary>
[RequireComponent(typeof(DungeonCore))]
public class CoreForm : MonoBehaviour
{
    private DungeonCore core;
    private Light2D glow;
    private readonly List<Transform> motes = new List<Transform>();
    private int shownTier = -1;

    private static readonly Color[] Tints = { Color.white, new Color(0.85f, 0.75f, 1f), new Color(1f, 0.82f, 1f), new Color(1f, 0.9f, 0.6f) };
    private static readonly Color[] Glows = { new Color(0.45f, 0.75f, 1f), new Color(0.6f, 0.55f, 1f), new Color(0.85f, 0.5f, 1f), new Color(1f, 0.8f, 0.4f) };

    private void Start()
    {
        core = GetComponent<DungeonCore>();
        glow = GetComponentInChildren<Light2D>();
    }

    public static int TierFor(int level) => level >= 15 ? 3 : level >= 10 ? 2 : level >= 5 ? 1 : 0;

    private void Update()
    {
        if (core == null) return;
        int lvl = CoreSpells.Level;
        core.growth = 1f + Mathf.Min(0.6f, (lvl - 1) * 0.04f);
        int tier = TierFor(lvl);
        if (tier != shownTier)
        {
            if (shownTier >= 0) { Fx.Play("shine", transform.position, 2f); CoreVoice.Say("grow", tier == 3 ? "Je suis devenu soleil." : "Je grandis. Je le sens jusque dans la roche."); }
            shownTier = tier;
            core.SetTint(Tints[tier]);
            if (glow != null) { glow.color = Glows[tier]; glow.pointLightOuterRadius = 4.5f + tier * 1.2f; glow.intensity = 1.1f + tier * 0.15f; }
            while (motes.Count < tier * 2) motes.Add(MakeMote());
        }
        for (int i = 0; i < motes.Count; i++)
        {
            float a = Time.time * (0.8f + 0.1f * i) + i * Mathf.PI * 2f / Mathf.Max(1, motes.Count);
            motes[i].position = transform.position + new Vector3(Mathf.Cos(a) * 0.75f, 0.35f + Mathf.Sin(a) * 0.3f, 0f);
            motes[i].GetComponent<SpriteRenderer>().color = Glows[shownTier] * new Color(1f, 1f, 1f, 0.85f);
        }
    }

    private Transform MakeMote()
    {
        var go = new GameObject("Éclat");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteUtil.Diamond;
        sr.sortingOrder = 6;
        go.transform.localScale = Vector3.one * 0.18f;
        return go.transform;
    }
}
