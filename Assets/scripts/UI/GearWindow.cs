using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SAC ET ÉQUIPEMENT (mode Aventurier, touche I ou bouton « Sac ») : les trois emplacements portés à gauche,
/// le sac à droite. Chaque objet du sac se compare à celui qu'il remplacerait (+ en vert, − en rouge) ;
/// on l'équipe d'un geste, et on le vend en ville.
/// </summary>
public class GearWindow : MonoBehaviour
{
    private static GearWindow instance;
    public static bool IsOpen => instance != null && instance.window != null && instance.window.gameObject.activeSelf;

    private Canvas canvas;
    private RectTransform root, window;

    public static void Toggle()
    {
        if (!CrawlerMode.Active) return;
        if (instance == null) instance = new GameObject("GearWindow").AddComponent<GearWindow>();
        if (IsOpen) instance.Close(); else instance.Open();
    }

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
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
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        if (!CrawlerMode.Active) Close();
    }

    private void Open()
    {
        Rebuild();
        Sfx.Play("ui_click", null, 0.6f);
    }

    private void Close()
    {
        if (window != null) window.gameObject.SetActive(false);
        foreach (Transform c in root) Destroy(c.gameObject);
        window = null;
        PointerUtil.BlockWorldInput(0.2f);
    }

    private void Rebuild()
    {
        foreach (Transform c in root) Destroy(c.gameObject);
        var cm = CrawlerMode.Instance;
        if (cm == null) return;

        var blocker = UIKit.Blocker(root, 0.55f);
        blocker.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        window = UIKit.PanelBox("GearWindow", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 900));
        window.pivot = new Vector2(0.5f, 0.5f);
        var title = UIKit.LabelAt(window, "Équipement", 40, UIKit.Gold, new Vector2(30, -20), new Vector2(600, 52));
        UIKit.StyleTitle(title);
        string sellHint = cm.CanSell ? "Vendre : ici, en ville." : "On ne vend qu'en ville.";
        UIKit.LabelAt(window, $"<color=#FFD966>{cm.gold} or</color>   ·   sac {cm.bag.Count}/{CrawlerMode.BagSize}   ·   <size=85%><color=#A59FB6>{sellHint}</color></size>",
            21, UIKit.Text, new Vector2(32, -74), new Vector2(900, 30));
        var close = UIKit.BtnAt(window, "", Close, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(60, 60));
        UIKit.Icon(close.transform, "close", Vector2.zero, 26, new Vector2(0.5f, 0.5f));

        // ── Porté
        var worn = UIKit.PanelBox("Porté", window, new Vector2(0, 1), new Vector2(24, -118), new Vector2(560, 758), UIKit.Card);
        var wt = UIKit.LabelAt(worn, "Porté", 24, UIKit.Gold, new Vector2(20, -14), new Vector2(400, 34));
        UIKit.StyleHead(wt);
        for (int i = 0; i < 3; i++)
        {
            var it = cm.equipped[i];
            var card = UIKit.Place(UIKit.Rect("Slot" + i, worn), new Vector2(0, 1), new Vector2(16, -60 - i * 150), new Vector2(528, 140));
            UIKit.Image(card, new Color(0.05f, 0.045f, 0.08f, 0.9f), true, false);
            if (it != null)
            {
                var edge = UIKit.Place(UIKit.Rect("Rareté", card), new Vector2(0, 1), new Vector2(0, 0), new Vector2(6, 140));
                UIKit.Image(edge, CrawlerGear.RarityColor(it.rarity), false, false);
            }
            UIKit.LabelAt(card, CrawlerGear.SlotNames[i], 16, UIKit.Sub, new Vector2(18, -10), new Vector2(300, 22));
            var nm = UIKit.LabelAt(card, it != null ? it.ColoredName : "<color=#6F6A80>— rien —</color>", 22, UIKit.Text, new Vector2(18, -34), new Vector2(500, 32));
            nm.enableAutoSizing = true; nm.fontSizeMin = 15; nm.fontSizeMax = 22;
            if (it != null)
            {
                UIKit.LabelAt(card, $"<size=85%><color=#A59FB6>{CrawlerGear.RarityNames[(int)it.rarity]} · étage {it.level}</color></size>", 17, UIKit.Sub, new Vector2(18, -66), new Vector2(500, 24));
                var st = UIKit.LabelAt(card, it.StatLine(), 17, UIKit.Text, new Vector2(18, -92), new Vector2(500, 44));
                st.textWrappingMode = TextWrappingModes.Normal;
            }
        }
        var tot = UIKit.LabelAt(worn, Totals(cm), 18, UIKit.Sub, new Vector2(20, -520), new Vector2(520, 220));
        tot.textWrappingMode = TextWrappingModes.Normal;

        // ── Sac
        var bagBox = UIKit.PanelBox("Sac", window, new Vector2(0, 1), new Vector2(600, -118), new Vector2(876, 758), UIKit.Card);
        var bt = UIKit.LabelAt(bagBox, "Sac", 24, UIKit.Gold, new Vector2(20, -14), new Vector2(400, 34));
        UIKit.StyleHead(bt);
        if (cm.bag.Count == 0)
            UIKit.LabelAt(bagBox, "<i>Vide. Coffres, gardiens et primes des contrats regorgent d'objets.</i>", 19, UIKit.Sub, new Vector2(20, -64), new Vector2(820, 40));
        float y = -58f;
        foreach (var it in new System.Collections.Generic.List<CrawlerGear.Item>(cm.bag))
        {
            var item = it;
            var row = UIKit.Place(UIKit.Rect("Objet", bagBox), new Vector2(0, 1), new Vector2(16, y), new Vector2(844, 66));
            UIKit.Image(row, new Color(0.05f, 0.045f, 0.08f, 0.9f), true, false);
            var edge = UIKit.Place(UIKit.Rect("Rareté", row), new Vector2(0, 1), new Vector2(0, 0), new Vector2(5, 66));
            UIKit.Image(edge, CrawlerGear.RarityColor(it.rarity), false, false);
            var nm = UIKit.LabelAt(row, $"{it.ColoredName}  <size=75%><color=#8E89A3>{CrawlerGear.SlotNames[(int)it.slot].ToLower()}</color></size>", 19, UIKit.Text, new Vector2(16, -6), new Vector2(560, 28));
            nm.enableAutoSizing = true; nm.fontSizeMin = 13; nm.fontSizeMax = 19;
            var st = UIKit.LabelAt(row, it.StatLine(cm.equipped[(int)it.slot]), 15, UIKit.Sub, new Vector2(16, -34), new Vector2(570, 28));
            st.enableAutoSizing = true; st.fontSizeMin = 11; st.fontSizeMax = 15;
            UIKit.BtnAt(row, "Équiper", () => { cm.Equip(item); Rebuild(); }, new Vector2(1, 0.5f), new Vector2(-132, 0), new Vector2(118, 50), 18, UIKit.Accent);
            var sell = UIKit.BtnAt(row, $"{it.Value} or", () => { cm.Sell(item); Rebuild(); }, new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(118, 50), 18,
                new Color(0.42f, 0.32f, 0.12f, 1f));
            sell.interactable = cm.CanSell;
            y -= 70f;
        }
    }

    private static string Totals(CrawlerMode cm)
    {
        var sb = new System.Text.StringBuilder("<color=#E8C77F>Bonus de l'équipement</color>\n");
        bool any = false;
        foreach (CrawlerGear.Stat s in System.Enum.GetValues(typeof(CrawlerGear.Stat)))
        {
            float v = cm.GearSum(s);
            if (v <= 0f) continue;
            any = true;
            sb.Append($"+{Mathf.RoundToInt(v * 100)} % {CrawlerGear.StatNames[(int)s]}   ");
        }
        if (!any) sb.Append("<color=#6F6A80>aucun</color>");
        return sb.ToString();
    }
}
