using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fenêtre de l'arbre de talents du Noyau (bouton Talents du HUD) : un arbre qui pousse depuis le cristal,
/// trois branches maîtresses qui se divisent en sous-branches. Les rameaux acquis s'illuminent d'or, ceux que l'on
/// peut apprendre pulsent ; toucher un talent l'affiche en bas, avec le bouton pour l'apprendre.
/// </summary>
public class TalentTreeWindow : MonoBehaviour
{
    public static TalentTreeWindow Instance { get; private set; }

    private RectTransform window, blocker, tree;
    private TextMeshProUGUI pointsText, trackerText, detailName, detailSub, detailDesc;
    private Button learnBtn;
    private Talents.Def selected;

    private class NodeUi { public Talents.Def def; public RectTransform rt; public Image ring, core, icon; public TextMeshProUGUI label; }
    private class LinkUi { public Talents.Def child; public Image line, shadow; }
    private readonly List<NodeUi> nodes = new List<NodeUi>();
    private readonly List<LinkUi> links = new List<LinkUi>();

    private static readonly Vector2 Scale = new Vector2(0.92f, 0.9f);
    private static readonly Vector2 RootPos = new Vector2(0f, 40f);
    private static readonly Color Gold = new Color(0.95f, 0.76f, 0.36f);
    private static readonly Color Avail = new Color(0.62f, 0.52f, 0.95f);
    private static readonly Color Locked = new Color(0.24f, 0.21f, 0.3f);
    private static readonly Color[] BranchTint = { new Color(0.85f, 0.62f, 0.38f), new Color(0.5f, 0.82f, 0.5f), new Color(0.72f, 0.5f, 0.95f) };

    public bool IsOpen => window != null && window.gameObject.activeSelf;

    private void Awake()
    {
        Instance = this;
        var go = new GameObject("TalentTreeWindow_Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();
        Build(UIKit.Stretch(UIKit.Rect("Root", go.transform)));
        window.gameObject.SetActive(false);
        blocker.gameObject.SetActive(false);
        Talents.OnChanged += Refresh;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Talents.OnChanged -= Refresh;
    }

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    public void Open()
    {
        window.gameObject.SetActive(true);
        blocker.gameObject.SetActive(true);
        if (selected == null) selected = FirstAvailable();
        Refresh();
    }

    public void Close()
    {
        window.gameObject.SetActive(false);
        blocker.gameObject.SetActive(false);
        PointerUtil.BlockWorldInput(0.2f);
    }

    private static Talents.Def FirstAvailable()
    {
        foreach (var d in Talents.All) if (Talents.CanBuy(d, out _)) return d;
        return Talents.All[0];
    }

    private static Vector2 P(Vector2 p) => new Vector2(p.x * Scale.x, p.y * Scale.y);

    private void Build(RectTransform root)
    {
        blocker = UIKit.Blocker(root, 0.6f);
        blocker.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        window = UIKit.PanelBox("Talents", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1880, 1040));
        window.pivot = new Vector2(0.5f, 0.5f);
        var title = UIKit.LabelAt(window, "Talents du Noyau", 40, UIKit.Gold, new Vector2(30, -20), new Vector2(800, 52));
        UIKit.StyleTitle(title);
        pointsText = UIKit.LabelAt(window, "", 22, UIKit.Sub, new Vector2(32, -74), new Vector2(900, 30));
        trackerText = UIKit.LabelAt(window, "", 17, UIKit.Text, new Vector2(-100, -14), new Vector2(520, 110), TextAlignmentOptions.TopLeft, new Vector2(1, 1));
        var close = UIKit.BtnAt(window, "", Close, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(60, 60));
        UIKit.Icon(close.transform, "close", Vector2.zero, 26, new Vector2(0.5f, 0.5f));

        // ── l'arbre : origine au pied, au centre
        tree = UIKit.Rect("Arbre", window);
        tree.anchorMin = tree.anchorMax = new Vector2(0.5f, 0f);
        tree.pivot = new Vector2(0.5f, 0f);
        tree.anchoredPosition = new Vector2(0f, 168f);
        tree.sizeDelta = new Vector2(1800f, 760f);

        // noms des branches maîtresses, à leur pied
        string[] heads = Talents.BranchNames;
        Vector2[] headPos = { new Vector2(-560, 120), new Vector2(-150, 280), new Vector2(560, 120) };
        for (int b = 0; b < 3; b++)
        {
            var h = UIKit.LabelAt(tree, heads[b].ToUpper(), 20, BranchTint[b], headPos[b], new Vector2(260, 28), TextAlignmentOptions.Center, new Vector2(0.5f, 0f));
            h.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UIKit.StyleHead(h);
        }

        // rameaux (sous les nœuds)
        foreach (var d in Talents.All)
        {
            Vector2 from = d.parent != null ? Talents.Get(d.parent).pos : RootPos;
            float thick = Mathf.Max(8f, 24f - d.tier * 3.5f);
            var shadow = Line(P(from), P(d.pos), thick + 8f, new Color(0.04f, 0.03f, 0.06f, 0.9f));
            var line = Line(P(from), P(d.pos), thick, Locked);
            links.Add(new LinkUi { child = d, line = line, shadow = shadow });
        }

        // le cristal, au pied de l'arbre
        var coreRt = UIKit.Rect("Cristal", tree);
        coreRt.anchorMin = coreRt.anchorMax = new Vector2(0.5f, 0f);
        coreRt.pivot = new Vector2(0.5f, 0.5f);
        coreRt.anchoredPosition = P(RootPos);
        coreRt.sizeDelta = new Vector2(120, 120);
        var glow = coreRt.gameObject.AddComponent<Image>();
        glow.sprite = SpriteUtil.Circle;
        glow.color = new Color(0.45f, 0.35f, 0.95f, 0.45f);
        glow.raycastTarget = false;
        UIKit.Icon(coreRt, "mana", Vector2.zero, 64, new Vector2(0.5f, 0.5f));

        // sous-branches : leur nom près de leur premier talent
        var subDone = new HashSet<string>();
        foreach (var d in Talents.All)
        {
            if (d.sub == null || !subDone.Add(d.sub)) continue;
            Vector2 p = P(d.pos);
            float side = Mathf.Sign(p.x);
            // à côté de son premier talent, vers l'extérieur de l'arbre
            var s = UIKit.LabelAt(tree, d.sub, 17, new Color(0.75f, 0.7f, 0.85f), p + (p.y < 300f ? new Vector2(0f, 70f) : new Vector2(side * 118f, 0f)), new Vector2(140, 24), TextAlignmentOptions.Center, new Vector2(0.5f, 0f));
            s.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s.fontStyle = FontStyles.Italic | FontStyles.SmallCaps;
        }

        // talents
        foreach (var d in Talents.All)
        {
            var def = d;
            var rt = UIKit.Rect("Talent_" + d.id, tree);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = P(d.pos);
            rt.sizeDelta = new Vector2(92, 92);
            var ring = rt.gameObject.AddComponent<Image>();
            ring.sprite = SpriteUtil.Circle;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = ring;
            btn.onClick.AddListener(() => { selected = def; Refresh(); Sfx.Play("ui_click", null, 0.5f); });
            var coreR = UIKit.Rect("Fond", rt);
            coreR.anchorMin = coreR.anchorMax = coreR.pivot = new Vector2(0.5f, 0.5f);
            coreR.sizeDelta = new Vector2(74, 74);
            var core = coreR.gameObject.AddComponent<Image>();
            core.sprite = SpriteUtil.Circle;
            core.color = new Color(0.08f, 0.06f, 0.12f, 1f);
            core.raycastTarget = false;
            var icon = UIKit.Icon(rt, Talents.BranchIcons[(int)d.branch], Vector2.zero, 40, new Vector2(0.5f, 0.5f));
            var label = UIKit.LabelAt(rt, d.name, 16, UIKit.Text, new Vector2(0, -50), new Vector2(190, 44), TextAlignmentOptions.Top, new Vector2(0.5f, 0.5f));
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            nodes.Add(new NodeUi { def = d, rt = rt, ring = ring, core = core, icon = icon, label = label });
        }

        // ── fiche du talent choisi
        var card = UIKit.PanelBox("Fiche", window, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(1820, 136), UIKit.Card);
        card.pivot = new Vector2(0.5f, 0f);
        card.Find("Liseré").GetComponent<Image>().color = UIKit.TrimSoft;
        detailName = UIKit.LabelAt(card, "", 28, UIKit.Gold, new Vector2(30, -16), new Vector2(900, 36));
        UIKit.StyleHead(detailName);
        detailSub = UIKit.LabelAt(card, "", 17, UIKit.Sub, new Vector2(30, -54), new Vector2(900, 24));
        detailDesc = UIKit.LabelAt(card, "", 21, UIKit.Text, new Vector2(30, -82), new Vector2(1300, 44));
        detailDesc.textWrappingMode = TextWrappingModes.Normal;
        learnBtn = UIKit.BtnAt(card, "Apprendre", () => { if (selected != null && Talents.Buy(selected)) selected = FirstAvailable(); Refresh(); },
            new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(380, 80), 24, new Color(0.6f, 0.45f, 0.12f, 1f));
    }

    /// <summary>Segment épais entre deux points (rameau).</summary>
    private Image Line(Vector2 a, Vector2 b, float thick, Color c)
    {
        var rt = UIKit.Rect("Rameau", tree);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = a;
        Vector2 d = b - a;
        rt.sizeDelta = new Vector2(d.magnitude, thick);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = SpriteUtil.RoundedUI;
        img.type = Image.Type.Sliced;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    private void Update()
    {
        if (!IsOpen) return;
        // les talents à portée pulsent doucement ; le talent choisi est un peu plus grand
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (var n in nodes)
        {
            bool can = Talents.CanBuy(n.def, out _);
            float s = (n.def == selected ? 1.15f : 1f) * (can ? 1f + 0.05f * pulse : 1f);
            n.rt.localScale = Vector3.one * s;
            if (can) n.ring.color = Color.Lerp(Avail, Color.white, 0.25f * pulse);
        }
    }

    private void Refresh()
    {
        if (pointsText == null) return;
        if (trackerText != null) trackerText.text = Domination.Tracker();
        int p = Talents.Points;
        pointsText.text = p > 0 ? $"<color=#E8C77F>{p} point{(p > 1 ? "s" : "")} à dépenser</color>  ·  un point par niveau du Noyau"
                                : "Aucun point libre  ·  un point par niveau du Noyau";
        foreach (var n in nodes)
        {
            bool has = Talents.Has(n.def.id);
            bool can = Talents.CanBuy(n.def, out _);
            Color tint = BranchTint[(int)n.def.branch];
            n.ring.color = has ? Gold : can ? Avail : Locked;
            n.core.color = has ? Color.Lerp(new Color(0.2f, 0.15f, 0.08f), tint, 0.25f) : new Color(0.08f, 0.06f, 0.12f, 1f);
            n.icon.color = has ? Color.Lerp(Color.white, tint, 0.3f) : can ? new Color(0.85f, 0.82f, 0.95f) : new Color(0.45f, 0.42f, 0.5f);
            n.label.color = has ? new Color(1f, 0.9f, 0.7f) : can ? UIKit.Text : new Color(0.55f, 0.52f, 0.62f);
        }
        foreach (var l in links)
        {
            bool has = Talents.Has(l.child.id);
            bool can = Talents.CanBuy(l.child, out _);
            l.line.color = has ? Gold : can ? new Color(Avail.r, Avail.g, Avail.b, 0.85f) : Locked;
        }
        if (selected != null)
        {
            bool has = Talents.Has(selected.id);
            bool can = Talents.CanBuy(selected, out string why);
            detailName.text = selected.name;
            string branch = Talents.BranchNames[(int)selected.branch];
            detailSub.text = selected.sub != null ? $"{branch} · {selected.sub}" : branch;
            detailDesc.text = selected.desc;
            learnBtn.interactable = can;
            UIKit.SetLabel(learnBtn, has ? "Acquis" : can ? "Apprendre (1 point)" : $"<size=80%>{why}</size>");
        }
    }
}
