using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Apparence personnalisée du héros du mode Aventurier : couleur des cheveux, de la peau et de la tenue.
/// Les palettes de chaque apparence (Resources/HeroPalettes/&lt;contrôleur&gt;.json, Tools~/make_palettes.py) disent
/// quelles couleurs appartiennent à chaque catégorie ; chaque sprite affiché par l'animation est remplacé, après
/// l'Animator, par une copie recolorée (créée une fois, puis gardée en cache).
/// </summary>
[DefaultExecutionOrder(1000)]
public class HeroLook : MonoBehaviour
{
    // ── Choix du joueur (écran de personnalisation), gardés d'une partie à l'autre ──
    public static Color? Hair, Skin, Outfit;
    public static string HeroName = "";

    public static readonly Color[] HairChoices =
    {
        new Color32(42, 34, 48, 255), new Color32(180, 83, 42, 255), new Color32(226, 194, 106, 255),
        new Color32(232, 230, 238, 255), new Color32(47, 63, 122, 255), new Color32(63, 107, 63, 255), new Color32(192, 96, 138, 255),
    };
    public static readonly Color[] SkinChoices =
    {
        new Color32(242, 201, 160, 255), new Color32(198, 138, 94, 255), new Color32(138, 86, 54, 255), new Color32(90, 54, 38, 255),
    };
    public static readonly Color[] OutfitChoices =
    {
        new Color32(154, 44, 44, 255), new Color32(44, 74, 154, 255), new Color32(47, 106, 58, 255), new Color32(90, 47, 138, 255),
        new Color32(42, 42, 48, 255), new Color32(216, 212, 204, 255), new Color32(184, 138, 44, 255),
    };

    const string Key = "heroLook_";

    public static void Load()
    {
        Hair = Read("hair"); Skin = Read("skin"); Outfit = Read("outfit");
        HeroName = PlayerPrefs.GetString(Key + "name", "");
    }

    public static void Save()
    {
        Write("hair", Hair); Write("skin", Skin); Write("outfit", Outfit);
        PlayerPrefs.SetString(Key + "name", HeroName ?? "");
        PlayerPrefs.Save();
    }

    static Color? Read(string k)
    {
        string s = PlayerPrefs.GetString(Key + k, "");
        return !string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c) ? c : (Color?)null;
    }

    static void Write(string k, Color? c) => PlayerPrefs.SetString(Key + k, c.HasValue ? "#" + ColorUtility.ToHtmlStringRGB(c.Value) : "");

    public static bool Customized => Hair.HasValue || Skin.HasValue || Outfit.HasValue;

    // ─────────────────────────────────────────────
    // Recoloration
    // ─────────────────────────────────────────────

    [System.Serializable]
    private class PaletteJson { public string[] hair, skin, outfit; }

    /// <summary>Correspondance couleur d'origine → couleur recolorée, pour une apparence et des choix donnés.</summary>
    public class Mapping
    {
        public readonly Dictionary<Color32, Color32> map = new Dictionary<Color32, Color32>(new C32Eq());

        public static Mapping Build(string look, Color? hair, Color? skin, Color? outfit)
        {
            var ta = Resources.Load<TextAsset>("HeroPalettes/" + look);
            if (ta == null) return null;
            var p = JsonUtility.FromJson<PaletteJson>(ta.text);
            var m = new Mapping();
            m.Add(p.hair, hair);
            m.Add(p.skin, skin);
            m.Add(p.outfit, outfit);
            return m.map.Count > 0 ? m : null;
        }

        void Add(string[] hex, Color? target)
        {
            if (hex == null || hex.Length == 0 || !target.HasValue) return;
            var cols = new List<Color>();
            foreach (var h in hex) if (ColorUtility.TryParseHtmlString(h, out var c)) cols.Add(c);
            if (cols.Count == 0) return;
            // valeur et saturation moyennes de la catégorie : chaque couleur garde son écart (ombres, lumières)
            float avgS = 0f, avgV = 0f;
            foreach (var c in cols) { Color.RGBToHSV(c, out _, out float s, out float v); avgS += s; avgV += v; }
            avgS /= cols.Count; avgV /= cols.Count;
            Color.RGBToHSV(target.Value, out float th, out float ts, out float tv);
            foreach (var c in cols)
            {
                Color.RGBToHSV(c, out _, out float s, out float v);
                float ns = Mathf.Clamp01(s * ts / Mathf.Max(0.05f, avgS));
                float nv = Mathf.Clamp01(v * tv / Mathf.Max(0.05f, avgV));
                map[(Color32)c] = (Color32)Color.HSVToRGB(th, ns, nv);
            }
        }

        public Texture2D Recolor(Texture2D src)
        {
            if (!src.isReadable) return null;
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 0) continue;
                var key = new Color32(c.r, c.g, c.b, 255);
                if (map.TryGetValue(key, out var n)) px[i] = new Color32(n.r, n.g, n.b, c.a);
            }
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = src.name + "_perso" };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }

    class C32Eq : IEqualityComparer<Color32>
    {
        public bool Equals(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
        public int GetHashCode(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;
    }

    /// <summary>Copie recolorée d'un sprite (pour l'aperçu de l'écran de personnalisation).</summary>
    public static Sprite RecolorSprite(Sprite s, Mapping m)
    {
        if (s == null || m == null) return s;
        var t = m.Recolor(s.texture);
        if (t == null) return s;
        return Sprite.Create(t, s.rect, new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height), s.pixelsPerUnit);
    }

    // ─────────────────────────────────────────────
    // Composant du héros
    // ─────────────────────────────────────────────

    private SpriteRenderer sr;
    private Animator anim;
    private string look;
    private Mapping mapping;
    private readonly Dictionary<Texture2D, Texture2D> textures = new Dictionary<Texture2D, Texture2D>();
    private readonly Dictionary<Sprite, Sprite> sprites = new Dictionary<Sprite, Sprite>();
    private readonly HashSet<Sprite> made = new HashSet<Sprite>();

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    /// <summary>À rappeler quand l'apparence (palier d'armure) ou les choix changent.</summary>
    public void Refresh()
    {
        foreach (var t in textures.Values) if (t != null) Destroy(t);
        textures.Clear();
        sprites.Clear();
        made.Clear();
        look = anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : null;
        mapping = look != null && Customized ? Mapping.Build(look, Hair, Skin, Outfit) : null;
    }

    private void LateUpdate()
    {
        if (sr == null || anim == null) return;
        string cur = anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : null;
        if (cur != look) Refresh();
        if (mapping == null) return;
        var s = sr.sprite;
        if (s == null || made.Contains(s)) return;
        if (!sprites.TryGetValue(s, out var r))
        {
            if (!textures.TryGetValue(s.texture, out var t))
            {
                t = mapping.Recolor(s.texture);
                textures[s.texture] = t;
            }
            r = t != null ? Sprite.Create(t, s.rect, new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height), s.pixelsPerUnit) : s;
            sprites[s] = r;
            made.Add(r);
        }
        sr.sprite = r;
    }

    private void OnDestroy()
    {
        foreach (var t in textures.Values) if (t != null) Destroy(t);
    }
}
