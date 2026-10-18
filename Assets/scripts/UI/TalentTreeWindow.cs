using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fenêtre de l'arbre de talents du Noyau (bouton Talents du HUD) : trois colonnes de cinq talents.</summary>
public class TalentTreeWindow : MonoBehaviour
{
    public static TalentTreeWindow Instance { get; private set; }

    private RectTransform window, blocker;
    private TextMeshProUGUI pointsText, trackerText;
    private readonly List<(Talents.Def def, Button btn, Image bg, Image trim, TextMeshProUGUI state)> cards =
        new List<(Talents.Def, Button, Image, Image, TextMeshProUGUI)>();

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
        Refresh();
    }

    public void Close()
    {
        window.gameObject.SetActive(false);
        blocker.gameObject.SetActive(false);
        PointerUtil.BlockWorldInput(0.2f);
    }

    private void Build(RectTransform root)
    {
        blocker = UIKit.Blocker(root, 0.55f);
        blocker.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        window = UIKit.PanelBox("Talents", root, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1440, 900));
        window.pivot = new Vector2(0.5f, 0.5f);
        var title = UIKit.LabelAt(window, "Talents du Noyau", 40, UIKit.Gold, new Vector2(30, -20), new Vector2(800, 52));
        UIKit.StyleTitle(title);
        pointsText = UIKit.LabelAt(window, "", 22, UIKit.Sub, new Vector2(32, -74), new Vector2(900, 30));
        trackerText = UIKit.LabelAt(window, "", 17, UIKit.Text, new Vector2(-100, -14), new Vector2(520, 100), TextAlignmentOptions.TopLeft, new Vector2(1, 1));
        var close = UIKit.BtnAt(window, "", Close, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(60, 60));
        UIKit.Icon(close.transform, "close", Vector2.zero, 26, new Vector2(0.5f, 0.5f));

        for (int b = 0; b < 3; b++)
        {
            var branch = (Talents.Branch)b;
            float x = 30 + b * 465;
            var col = UIKit.PanelBox("Branche", window, new Vector2(0, 1), new Vector2(x, -118), new Vector2(445, 760), UIKit.Card);
            col.Find("Liseré").GetComponent<Image>().color = UIKit.TrimSoft;
            UIKit.Icon(col, Talents.BranchIcons[b], new Vector2(18, -16), 32);
            var bt = UIKit.LabelAt(col, Talents.BranchNames[b], 26, UIKit.Gold, new Vector2(60, -16), new Vector2(360, 36));
            UIKit.StyleHead(bt);
            float y = -70;
            foreach (var d in Talents.All)
            {
                if (d.branch != branch) continue;
                var def = d;
                var btn = UIKit.BtnAt(col, "", () => { Talents.Buy(def); }, new Vector2(0, 1), new Vector2(16, y), new Vector2(413, 128), 20);
                var rt = (RectTransform)btn.transform;
                var name = UIKit.LabelAt(rt, $"{d.tier}. {d.name}", 22, UIKit.Text, new Vector2(16, -10), new Vector2(380, 30));
                name.fontStyle = FontStyles.Bold;
                var desc = UIKit.LabelAt(rt, d.desc, 17, UIKit.Sub, new Vector2(16, -42), new Vector2(380, 56));
                var state = UIKit.LabelAt(rt, "", 15, UIKit.Gold, new Vector2(-14, 10), new Vector2(240, 22), TextAlignmentOptions.BottomRight, new Vector2(1, 0));
                cards.Add((def, btn, btn.GetComponent<Image>(), rt.Find("Liseré").GetComponent<Image>(), state));
                y -= 138;
            }
        }
    }

    private void Refresh()
    {
        if (pointsText == null) return;
        if (trackerText != null) trackerText.text = Domination.Tracker();
        int p = Talents.Points;
        pointsText.text = p > 0 ? $"<color=#E8C77F>{p} point{(p > 1 ? "s" : "")} à dépenser</color>  ·  un point par niveau du Noyau"
                                : "Aucun point libre  ·  un point par niveau du Noyau";
        foreach (var (def, btn, bg, trim, state) in cards)
        {
            bool has = Talents.Has(def.id);
            bool can = Talents.CanBuy(def, out string why);
            btn.interactable = can;
            bg.color = has ? new Color(0.28f, 0.22f, 0.12f, 1f) : can ? UIKit.Button : new Color(0.1f, 0.09f, 0.14f, 0.95f);
            trim.color = has ? UIKit.Trim : can ? new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.9f) : UIKit.TrimSoft;
            state.text = has ? "Acquis" : can ? "<color=#B9AEFF>Disponible</color>" : $"<color=#6F6A80>{why}</color>";
        }
    }
}

