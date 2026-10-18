using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RECOLORATION DES SPRITES (variantes de monstres) : chaque image est repeinte avec une rampe de couleurs du
/// thème (du sombre au clair, selon la luminosité), mêlée à la couleur d'origine pour garder les détails ;
/// le contour sombre reste intact. Les textures sont relues par le GPU (pas besoin de « Read/Write »),
/// recolorées une fois par thème, puis mises en cache.
/// </summary>
public static class SpriteRecolor
{
    public class Ramp
    {
        public Color dark, mid, light;
        public float mix = 0.7f;   // part de la rampe (le reste : couleur d'origine)
    }

    private static readonly Dictionary<(Texture2D, string), Texture2D> textures = new Dictionary<(Texture2D, string), Texture2D>();
    private static readonly Dictionary<(Sprite, string), Sprite> sprites = new Dictionary<(Sprite, string), Sprite>();
    private static readonly HashSet<Sprite> recolored = new HashSet<Sprite>();

    public static bool IsRecolored(Sprite s) => s != null && recolored.Contains(s);

    public static Sprite Get(Sprite src, string key, Ramp ramp)
    {
        if (src == null || ramp == null || recolored.Contains(src)) return src;
        if (sprites.TryGetValue((src, key), out var hit)) return hit;
        var tex = Texture(src.texture, key, ramp);
        if (tex == null) return src;
        var r = src.rect;
        var pivot = new Vector2(src.pivot.x / r.width, src.pivot.y / r.height);
        var s = Sprite.Create(tex, r, pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect, src.border);
        s.name = src.name + "_" + key;
        sprites[(src, key)] = s;
        recolored.Add(s);
        return s;
    }

    private static Texture2D Texture(Texture2D src, string key, Ramp ramp)
    {
        if (src == null) return null;
        if (textures.TryGetValue((src, key), out var hit)) return hit;
        // relecture GPU : la texture d'origine n'a pas besoin d'être lisible
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = copy.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            var p = px[i];
            if (p.a < 8) continue;
            Color c = p;
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            if (lum < 0.12f) continue;   // contour et ombres profondes : intacts
            Color g = lum < 0.5f ? Color.Lerp(ramp.dark, ramp.mid, lum / 0.5f) : Color.Lerp(ramp.mid, ramp.light, (lum - 0.5f) / 0.5f);
            Color o = Color.Lerp(c, g, ramp.mix);
            o.a = c.a;
            px[i] = o;
        }
        copy.SetPixels32(px);
        copy.Apply();
        textures[(src, key)] = copy;
        return copy;
    }
}
