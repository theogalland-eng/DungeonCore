using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Bibliothèque d'effets animés (Resources/FX, bandes générées par Tools~/crop_fx.py) :
/// boule de feu, explosion, soin, évolution, fumées, magma, éclairs...
/// </summary>
public static class Fx
{
    private static Dictionary<string, Sprite[]> frames;

    public static Sprite[] Frames(string name)
    {
        if (frames == null)
        {
            frames = new Dictionary<string, Sprite[]>();
            var groups = new Dictionary<string, List<Sprite>>();
            foreach (var s in Resources.LoadAll<Sprite>("FX"))
            {
                // « explosion_3 » → explosion ; « arrow » (image seule) → arrow
                int us = s.name.LastIndexOf('_');
                string key = us > 0 && int.TryParse(s.name.Substring(us + 1), out _) ? s.name.Substring(0, us) : s.name;
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<Sprite>();
                l.Add(s);
            }
            foreach (var kv in groups)
            {
                kv.Value.Sort((a, b) => Index(a.name).CompareTo(Index(b.name)));
                frames[kv.Key] = kv.Value.ToArray();
            }
        }
        return frames.TryGetValue(name, out var f) ? f : null;
    }

    private static int Index(string n)
    {
        int us = n.LastIndexOf('_');
        return us > 0 && int.TryParse(n.Substring(us + 1), out int i) ? i : 0;
    }

    /// <summary>
    /// Joue un effet à une position. height = hauteur à l'écran (unités monde).
    /// Renvoie null si l'effet n'existe pas (le jeu continue sans).
    /// </summary>
    public static FxAnim Play(string name, Vector3 pos, float height, float fps = 14f, int order = 15, Color? tint = null,
                              bool loop = false, Transform follow = null, Color? light = null, int from = 0, int to = -1)
    {
        var f = Frames(name);
        if (f == null || f.Length == 0) return null;
        if (to < 0 || to >= f.Length) to = f.Length - 1;
        if (from > 0 || to < f.Length - 1)
        {
            var sub = new Sprite[to - from + 1];
            System.Array.Copy(f, from, sub, 0, sub.Length);
            f = sub;
        }
        var go = new GameObject("FX_" + name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = f[0];
        sr.sortingOrder = order;
        sr.color = tint ?? Color.white;
        float h = f[0].bounds.size.y;
        if (h > 0f) go.transform.localScale = Vector3.one * (height / h);
        if (light.HasValue)
        {
            var l = go.AddComponent<Light2D>(); Light2DCleanup.Attach(l);
            l.lightType = Light2D.LightType.Point;
            l.color = light.Value;
            l.intensity = 0.9f;
            l.pointLightOuterRadius = Mathf.Max(1.2f, height * 1.4f);
        }
        var a = go.AddComponent<FxAnim>();
        a.frames = f;
        a.fps = fps;
        a.loop = loop;
        a.follow = follow;
        if (follow != null) a.followOffset = pos - follow.position;
        return a;
    }
}
