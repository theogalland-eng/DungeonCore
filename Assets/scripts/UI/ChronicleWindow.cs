using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fenêtre des Chroniques de l'île (menu pause) : les moments marquants, du plus récent au plus ancien.</summary>
public class ChronicleWindow : MonoBehaviour
{
    public static ChronicleWindow Instance { get; private set; }
    private Canvas canvas;
    private RectTransform root, content;
    private TextMeshProUGUI text;

    public static void Show()
    {
        if (Instance == null) new GameObject("ChronicleWindow").AddComponent<ChronicleWindow>();
        Instance.Open();
    }

    private void Awake()
    {
        Instance = this;
        var go = new GameObject("Chronicle_Canvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;   // au-dessus du menu pause
        var scaler = go.AddComponent<CanvasScaler>();
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();
        root = UIKit.Stretch(UIKit.Rect("Root", go.transform));
        UIKit.Blocker(root, 0.6f).gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var box = UIKit.PanelBox("Chroniques", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 900));
        box.pivot = new Vector2(0.5f, 0.5f);
        var title = UIKit.LabelAt(box, "Chroniques de l'île", 40, UIKit.Gold, new Vector2(30, -20), new Vector2(900, 52));
        UIKit.StyleTitle(title);
        var close = UIKit.BtnAt(box, "", Close, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(60, 60));
        UIKit.Icon(close.transform, "close", Vector2.zero, 26, new Vector2(0.5f, 0.5f));
        var area = UIKit.Rect("Zone", box);
        UIKit.Stretch(area, 30, 30, 96, 30);
        content = UIKit.ScrollArea(area, out _);
        text = UIKit.Label(content, "", 21, UIKit.Text, TextAlignmentOptions.TopLeft);
        text.rectTransform.anchorMin = new Vector2(0, 1); text.rectTransform.anchorMax = new Vector2(1, 1);
        text.rectTransform.pivot = new Vector2(0.5f, 1);
        text.rectTransform.offsetMin = new Vector2(8, 0); text.rectTransform.offsetMax = new Vector2(-8, 0);
        text.lineSpacing = 8f;
        root.gameObject.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Open()
    {
        root.gameObject.SetActive(true);
        var sb = new System.Text.StringBuilder();
        var list = Chronicle.Entries;
        if (list.Count == 0) sb.Append("<color=#A59FB6>Rien encore n'est digne d'être raconté. Patience : l'île a la mémoire longue.</color>");
        int lastDay = -1;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            if (e.day != lastDay)
            {
                if (lastDay >= 0) sb.Append('\n');
                sb.Append($"<color=#E8C77F><b>Jour {e.day}</b></color>\n");
                lastDay = e.day;
            }
            sb.Append("<color=#C9A45C>·</color> ").Append(e.text).Append('\n');
        }
        text.text = sb.ToString();
        text.ForceMeshUpdate();
        content.sizeDelta = new Vector2(0, text.preferredHeight + 20f);
        text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, text.preferredHeight + 10f);
    }

    public void Close() => root.gameObject.SetActive(false);
}
