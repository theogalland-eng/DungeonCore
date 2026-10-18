using System.Collections.Generic;
using UnityEngine;

/// <summary>Sprites d'objets dessinés pour le projet et chargés en jeu (Resources/Props, 16 px par unité, nets).</summary>
public static class PropArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string name, float pivotY = 0.1f)
    {
        string key = name + "@" + pivotY;
        if (cache.TryGetValue(key, out var s) && s != null) return s;
        var tex = Resources.Load<Texture2D>("Props/" + name);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Point;
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, pivotY), 16f);
        cache[key] = s;
        return s;
    }
}
