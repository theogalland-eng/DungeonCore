using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fiche d'un intrus (toucher un aventurier) : nom, rang, rôle, santé, ce qu'il fait, faim et provisions,
/// et ses traits de caractère — seulement ceux que le Noyau a déjà observés.
/// </summary>
public class IntruderCard : MonoBehaviour
{
    public static IntruderCard Instance { get; private set; }

    private RectTransform card;
    private TextMeshProUGUI title, sub, activity, body;
    private RectTransform hpFill;
    private AdventurerAI target;
    private float refresh;

    public static void Show(AdventurerAI a)
    {
        if (a == null) return;
        if (Instance == null) new GameObject("IntruderCard").AddComponent<IntruderCard>();
        Instance.target = a;
        Instance.card.gameObject.SetActive(true);
        Instance.Refresh();
    }

    public static void Hide()
    {
        if (Instance != null) { Instance.target = null; Instance.card.gameObject.SetActive(false); }
    }

    private void Awake()
    {
        Instance = this;
        var go = new GameObject("IntruderCard_Canvas");
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();
        var root = UIKit.Stretch(UIKit.Rect("Root", go.transform));

        card = UIKit.PanelBox("Fiche", root, new Vector2(0, 0.5f), new Vector2(16, 30), new Vector2(440, 280));
        UIKit.Icon(card, "skull", new Vector2(18, -18), 32);
        title = UIKit.LabelAt(card, "", 26, UIKit.Gold, new Vector2(60, -14), new Vector2(320, 34));
        UIKit.StyleHead(title);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true;
        title.fontSizeMin = 15f;
        title.fontSizeMax = 26f;
        sub = UIKit.LabelAt(card, "", 18, UIKit.Sub, new Vector2(60, -48), new Vector2(360, 26));
        var close = UIKit.BtnAt(card, "", Hide, new Vector2(1, 1), new Vector2(-12, -12), new Vector2(46, 46));
        UIKit.Icon(close.transform, "close", Vector2.zero, 20, new Vector2(0.5f, 0.5f));

        var bar = UIKit.Place(UIKit.Rect("Santé", card), new Vector2(0, 1), new Vector2(20, -84), new Vector2(400, 18));
        UIKit.Image(bar, new Color(0.03f, 0.025f, 0.05f, 0.95f), true, false);
        hpFill = UIKit.Rect("Fill", bar);
        hpFill.anchorMin = Vector2.zero; hpFill.anchorMax = Vector2.one;
        hpFill.offsetMin = new Vector2(4, 4); hpFill.offsetMax = new Vector2(-4, -4);
        UIKit.Image(hpFill, UIKit.Heart, false, false);

        activity = UIKit.LabelAt(card, "", 19, UIKit.Text, new Vector2(20, -112), new Vector2(400, 28));
        body = UIKit.LabelAt(card, "", 18, UIKit.Text, new Vector2(20, -146), new Vector2(400, 124));
        body.lineSpacing = 6f;
        card.gameObject.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (target == null) { if (card.gameObject.activeSelf) Hide(); return; }
        refresh -= Time.unscaledDeltaTime;
        if (refresh <= 0f) Refresh();
    }

    private void Refresh()
    {
        refresh = 0.3f;
        var a = target;
        if (a == null) return;
        var c = a.combat;
        string nm = c != null ? c.adventurerName.Split('·')[0].Trim() : "";
        title.text = string.IsNullOrEmpty(nm) ? "Aventurier" : nm;
        string rank = a.Rank >= 0 ? $"rang {GuildSystem.RankName(a.Rank)} · " : "";
        sub.text = $"{rank}{AdventurerAI.RoleName(a.role)}";
        float pct = a.HealthPct;
        hpFill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);

        string act;
        if (a.IsDead) act = "<color=#FF8A7A>mort</color>";
        else if (a.Life != null && a.Life.Activity != null) act = a.Life.Activity;
        else act = a.State switch
        {
            AdventurerAI.AdventurerState.Wandering => "flâne sur la grève",
            AdventurerAI.AdventurerState.Advancing => a.IsInsideDungeon ? "explore le donjon" : "marche vers la grotte",
            AdventurerAI.AdventurerState.Fighting => "<color=#FF8A7A>combat</color>",
            AdventurerAI.AdventurerState.Retreating => "<color=#CFE0FF>s'enfuit</color>",
            AdventurerAI.AdventurerState.Healing => "panse ses plaies",
            AdventurerAI.AdventurerState.WaitingGroup => "attend des compagnons",
            AdventurerAI.AdventurerState.Leaving => "quitte l'île",
            AdventurerAI.AdventurerState.Looting => "<color=#F2D27A>pille un coffre</color>",
            _ => "",
        };
        activity.text = $"<color=#A59FB6>En ce moment :</color> {act}";
        body.text = a.Life != null ? a.Life.Describe() : "<color=#A59FB6>Un habitant de la colonie.</color>";
        if (a.IsDead) target = null;
    }
}
