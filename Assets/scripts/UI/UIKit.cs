using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Fabrique d'éléments d'interface construits en code (menus, fenêtres) aux couleurs du jeu.
/// Toutes les positions partent du coin haut-gauche du parent sauf mention contraire.
/// </summary>
public static class UIKit
{
    private static TMP_FontAsset titleFont, headFont, bodyFont;

    /// <summary>Alegreya Sans (texte courant) : devient la police par défaut de tous les textes créés en jeu.</summary>
    public static TMP_FontAsset BodyFont => bodyFont != null ? bodyFont : (bodyFont = Resources.Load<TMP_FontAsset>("Fonts/Body SDF"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void UseBodyFont()
    {
        var body = BodyFont;
        if (body == null) return;
        var old = TMP_Settings.defaultFontAsset;
        if (old == body) return;
        // l'ancienne police reste en secours pour les symboles absents d'Alegreya
        if (old != null)
        {
            if (body.fallbackFontAssetTable == null) body.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!body.fallbackFontAssetTable.Contains(old)) body.fallbackFontAssetTable.Add(old);
        }
        TMP_Settings.defaultFontAsset = body;
    }
    private static Material titleGlow;
    private static Sprite hGradient;

    /// <summary>Cinzel Decorative (titres) — null si les polices n'ont pas été créées (menu DungeonCore/12).</summary>
    public static TMP_FontAsset TitleFont => titleFont != null ? titleFont : (titleFont = Resources.Load<TMP_FontAsset>("Fonts/Title SDF"));
    /// <summary>Cinzel (en-têtes, boutons du menu principal).</summary>
    public static TMP_FontAsset HeadFont => headFont != null ? headFont : (headFont = Resources.Load<TMP_FontAsset>("Fonts/Head SDF"));

    /// <summary>Titre en Cinzel Decorative avec un halo doré (matériau « Title Glow »).</summary>
    public static void StyleTitle(TextMeshProUGUI t)
    {
        if (TitleFont == null) return;
        t.font = TitleFont;
        if (titleGlow == null) titleGlow = Resources.Load<Material>("Fonts/Title Glow");
        if (titleGlow != null) t.fontSharedMaterial = titleGlow;
    }

    public static void StyleHead(TextMeshProUGUI t)
    {
        if (HeadFont != null) t.font = HeadFont;
    }

    /// <summary>Dégradé horizontal (opaque à gauche → transparent à droite).</summary>
    public static Sprite HGradient
    {
        get
        {
            if (hGradient != null) return hGradient;
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 256; x++)
            {
                float k = x / 255f;
                float a = k < 0.55f ? 1f : Mathf.SmoothStep(1f, 0f, (k - 0.55f) / 0.45f);
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            hGradient = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
            return hGradient;
        }
    }

    // Palette « Interface » de la maquette Design (les fonds pixel sont à ~86 % : teintes légèrement relevées)
    static Color H(string hex, float a = 1f, float lift = 1.16f)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return new Color(Mathf.Min(1f, c.r * lift), Mathf.Min(1f, c.g * lift), Mathf.Min(1f, c.b * lift), a);
    }
    public static readonly Color Panel = H("#1B1726", 0.96f);
    public static readonly Color Card = H("#241F33", 0.97f);
    public static readonly Color Button = H("#2A2340", 0.98f);
    public static readonly Color Accent = H("#6F55C9");
    public static readonly Color Disabled = H("#1B1726", 0.9f);
    public static readonly Color Danger = H("#9A2F35");
    public static readonly Color Gold = H("#E8C77F", 1f, 1f);
    public static readonly Color Trim = H("#C9A45C", 0.9f, 1f);
    public static readonly Color TrimSoft = new Color(0.62f, 0.55f, 0.75f, 0.32f);
    public static readonly Color Text = H("#EFE9DC", 1f, 1f);
    public static readonly Color Sub = H("#A59FB6", 1f, 1f);
    public static readonly Color Mana = H("#8E7CFF", 1f, 1f);
    public static readonly Color Heart = H("#D9534A", 1f, 1f);

    /// <summary>Liseré pixel (or par défaut) par-dessus un fond : le cadre « soigné » de la maquette.</summary>
    public static Image Frame(RectTransform rt, Color? c = null)
    {
        var f = Stretch(Rect("Liseré", rt));
        var img = f.gameObject.AddComponent<Image>();
        img.sprite = SpriteUtil.UITrim;
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.color = c ?? Trim;
        img.raycastTarget = false;
        f.SetAsFirstSibling();
        f.SetSiblingIndex(0);
        return img;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Sprite> icons = new System.Collections.Generic.Dictionary<string, Sprite>();

    /// <summary>Icône pixel (Resources/UI/Icons/icon_nom), null si absente.</summary>
    public static Sprite IconSprite(string name)
    {
        if (icons.TryGetValue(name, out var s)) return s;
        s = Resources.Load<Sprite>("UI/Icons/icon_" + name);
        icons[name] = s;
        return s;
    }

    /// <summary>Icône posée dans un parent (ancre haut-gauche par défaut), taille en unités du canevas.</summary>
    public static Image Icon(Transform parent, string name, Vector2 pos, float size, Vector2? anchor = null)
    {
        var rt = Place(Rect("Icône_" + name, parent), anchor ?? new Vector2(0, 1), pos, new Vector2(size, size));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = IconSprite(name);
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (img.sprite == null) img.enabled = false;
        return img;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static RectTransform Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
        return rt;
    }

    /// <summary>Place un élément (ancre + pivot identiques) à une position relative.</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Image(RectTransform rt, Color c, bool rounded = true, bool raycast = true)
    {
        var img = rt.gameObject.AddComponent<Image>();
        if (rounded) { img.sprite = SpriteUtil.RoundedUI; img.type = UnityEngine.UI.Image.Type.Sliced; }
        img.color = c;
        img.raycastTarget = raycast;
        return img;
    }

    public static RectTransform PanelBox(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color? c = null)
    {
        var rt = Place(Rect(name, parent), anchor, pos, size);
        Image(rt, c ?? Panel);
        Frame(rt);
        return rt;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var t = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>Texte positionné (ancre haut-gauche par défaut).</summary>
    public static TextMeshProUGUI LabelAt(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
        TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Vector2? anchor = null)
    {
        var t = Label(parent, text, size, color, align);
        Place(t.rectTransform, anchor ?? new Vector2(0, 1), pos, box);
        return t;
    }

    public static Button Btn(Transform parent, string label, UnityAction onClick, float fontSize = 24, Color? color = null)
    {
        var rt = Rect("Btn_" + label, parent);
        var img = Image(rt, color ?? Button);
        var b = rt.gameObject.AddComponent<Button>();
        var cols = b.colors;
        cols.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cols.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cols.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.8f);
        b.colors = cols;
        b.targetGraphic = img;
        b.onClick.AddListener(() => Sfx.Play("ui_click", null, 0.6f, 0.04f));
        if (onClick != null) b.onClick.AddListener(onClick);
        Frame(rt, color.HasValue && color.Value == Accent ? Trim : TrimSoft);
        if (!string.IsNullOrEmpty(label))
        {
            var t = Label(rt, label, fontSize, Text);
            Stretch(t.rectTransform, 8, 8, 0, 0);
            if (fontSize >= 22) StyleHead(t);
        }
        return b;
    }

    public static Button BtnAt(Transform parent, string label, UnityAction onClick, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize = 24, Color? color = null)
    {
        var b = Btn(parent, label, onClick, fontSize, color);
        Place((RectTransform)b.transform, anchor, pos, size);
        return b;
    }

    public static void SetLabel(Button b, string text)
    {
        var t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.text = text;
    }

    /// <summary>Zone défilante verticale : renvoie le contenu (ancré en haut, hauteur à fixer).</summary>
    public static RectTransform ScrollArea(RectTransform parent, out ScrollRect scroll)
    {
        var vp = Stretch(Rect("Viewport", parent));
        Image(vp, new Color(0, 0, 0, 0), false);
        vp.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", vp);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = new Vector2(0, 100);
        scroll = vp.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = vp;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.08f;
        scroll.scrollSensitivity = 45f;
        scroll.decelerationRate = 0.12f;
        return content;
    }

    /// <summary>Champ de saisie TextMeshPro minimal.</summary>
    public static TMP_InputField Input(Transform parent, string placeholder, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize = 28)
    {
        var rt = Place(Rect("Input", parent), anchor, pos, size);
        var bg = Image(rt, new Color(0.03f, 0.03f, 0.06f, 0.95f));
        var area = Stretch(Rect("TextArea", rt), 16, 16, 6, 6);
        area.gameObject.AddComponent<RectMask2D>();
        var ph = Label(area, placeholder, fontSize, new Color(0.5f, 0.48f, 0.58f), TextAlignmentOptions.MidlineLeft);
        ph.fontStyle = FontStyles.Italic;
        ph.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(ph.rectTransform);
        var txt = Label(area, "", fontSize, Text, TextAlignmentOptions.MidlineLeft);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(txt.rectTransform);
        var input = rt.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = bg;
        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = ph;
        input.characterLimit = 20;
        input.pointSize = fontSize;
        return input;
    }

    /// <summary>Voile plein écran qui bloque les clics derrière une fenêtre.</summary>
    public static RectTransform Blocker(Transform parent, float alpha = 0.6f)
    {
        var rt = Stretch(Rect("Blocker", parent));
        Image(rt, new Color(0.01f, 0.01f, 0.03f, alpha), false);
        return rt;
    }

    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i).gameObject;
            c.transform.SetParent(null, false);
            Object.Destroy(c);
        }
    }
}
