using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ARBRE DE COMPÉTENCES DE LA CLASSE (mode Aventurier, touche K ou bouton « Talents ») : trois voies côte à côte,
/// trois talents chacune, du plus simple (1 point) au plus puissant (3 points).
/// </summary>
public class SkillTreeWindow : MonoBehaviour
{
    private static SkillTreeWindow instance;
    public static bool IsOpen => instance != null && instance.window != null;

    private RectTransform root, window;

    public static void Toggle()
    {
        if (!CrawlerMode.Active) return;
        if (instance == null) instance = new GameObject("SkillTreeWindow").AddComponent<SkillTreeWindow>();
        if (IsOpen) instance.Close(); else instance.Rebuild();
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        gameObject.AddComponent<GraphicRaycaster>();
        root = UIKit.Stretch(UIKit.Rect("Root", transform));
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void Update()
    {
        if (!IsOpen) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || !CrawlerMode.Active) Close();
    }

    private void Close()
    {
        foreach (Transform c in root) Destroy(c.gameObject);
        window = null;
        PointerUtil.BlockWorldInput(0.2f);
    }

    private void Rebuild()
    {
        foreach (Transform c in root) Destroy(c.gameObject);
        var cm = CrawlerMode.Instance;
        if (cm == null || cm.Player == null) return;
        var tree = CrawlerSkills.For(cm.Player.role);

        var blocker = UIKit.Blocker(root, 0.55f);
        blocker.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        window = UIKit.PanelBox("SkillTree", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 860));
        window.pivot = new Vector2(0.5f, 0.5f);
        var title = UIKit.LabelAt(window, $"Talents · {CrawlerMode.ClassNames[(int)cm.Player.role]}", 40, UIKit.Gold, new Vector2(30, -20), new Vector2(900, 52));
        UIKit.StyleTitle(title);
        int pts = CrawlerSkills.Points;
        UIKit.LabelAt(window, $"<color=#FFD966>{pts} point(s)</color> à dépenser   ·   <size=85%><color=#A59FB6>un point par nouvel étage atteint, un par rang validé à la Guilde</color></size>",
            21, UIKit.Text, new Vector2(32, -74), new Vector2(1200, 30));
        var close = UIKit.BtnAt(window, "", Close, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(60, 60));
        UIKit.Icon(close.transform, "close", Vector2.zero, 26, new Vector2(0.5f, 0.5f));

        for (int b = 0; b < 3; b++)
        {
            var col = UIKit.PanelBox("Voie" + b, window, new Vector2(0, 1), new Vector2(24 + b * 488, -118), new Vector2(476, 718), UIKit.Card);
            var head = UIKit.LabelAt(col, tree.branches[b], 26, UIKit.Gold, new Vector2(0, -16), new Vector2(440, 36), TextAlignmentOptions.Top, new Vector2(0.5f, 1));
            UIKit.StyleHead(head);
            int t = 0;
            foreach (var n in tree.nodes)
            {
                if (n.branch != b) continue;
                var node = n;
                bool has = CrawlerSkills.Has(n.id);
                bool can = CrawlerSkills.CanLearn(n, out string why);
                float y = -70 - t * 212;
                // trait reliant les talents de la voie
                if (t > 0)
                {
                    var link = UIKit.Place(UIKit.Rect("Lien", col), new Vector2(0.5f, 1), new Vector2(0, y + 22), new Vector2(6, 22));
                    UIKit.Image(link, has ? UIKit.Gold : new Color(0.35f, 0.32f, 0.45f, 1f), false, false);
                }
                var card = UIKit.Place(UIKit.Rect("Talent_" + n.id, col), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(436, 190));
                UIKit.Image(card, has ? new Color(0.2f, 0.16f, 0.08f, 0.95f) : new Color(0.05f, 0.045f, 0.08f, 0.92f), true, false);
                var nm = UIKit.LabelAt(card, (has ? "<color=#E8C77F>" : can ? "<color=#EFE9DC>" : "<color=#8E89A3>") + n.name + "</color>", 24, UIKit.Text, new Vector2(18, -12), new Vector2(300, 32));
                UIKit.StyleHead(nm);
                UIKit.LabelAt(card, $"<color=#A59FB6>{n.tier} pt</color>", 18, UIKit.Sub, new Vector2(-16, -16), new Vector2(80, 26), TextAlignmentOptions.TopRight, new Vector2(1, 1));
                var ds = UIKit.LabelAt(card, n.desc, 19, UIKit.Text, new Vector2(18, -52), new Vector2(400, 64));
                ds.textWrappingMode = TextWrappingModes.Normal;
                if (has) UIKit.LabelAt(card, "<color=#E8C77F>Acquis</color>", 20, UIKit.Gold, new Vector2(0, 16), new Vector2(400, 40), TextAlignmentOptions.Center, new Vector2(0.5f, 0));
                else
                {
                    var btn = UIKit.BtnAt(card, can ? "Apprendre" : $"<size=85%>{why}</size>", () => { CrawlerSkills.Learn(node); Rebuild(); },
                        new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(400, 56), 20, can ? UIKit.Accent : UIKit.Disabled);
                    btn.interactable = can;
                }
                t++;
            }
        }
    }
}
