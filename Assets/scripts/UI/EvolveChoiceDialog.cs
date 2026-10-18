using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CHOIX DE LA VOIE D'ÉVOLUTION : au passage au stade 2, certaines familles proposent, en plus de la voie classique,
/// une ou deux voies de rôle (gobelin archer, squelette mage...). Une carte par voie : apparence, nom, rôle, stats.
/// </summary>
public class EvolveChoiceDialog : MonoBehaviour
{
    private static EvolveChoiceDialog instance;
    public static bool IsOpen => instance != null;

    public static void Open(MonsterUnit unit)
    {
        if (unit == null) return;
        if (!unit.CanEvolveNow(out string why)) { GameHUD.Toast(why, GameHUD.WarningColor); return; }
        if (instance != null) Destroy(instance.gameObject);
        var go = new GameObject("EvolveChoiceDialog");
        instance = go.AddComponent<EvolveChoiceDialog>();
        instance.Build(unit);
    }

    private void Build(MonsterUnit unit)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 170;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        gameObject.AddComponent<GraphicRaycaster>();
        var root = UIKit.Stretch(UIKit.Rect("Root", transform));
        var blocker = UIKit.Blocker(root, 0.55f);
        blocker.gameObject.AddComponent<Button>().onClick.AddListener(Close);

        var paths = new System.Collections.Generic.List<GameObject> { unit.nextStage };
        foreach (var b in unit.branches) if (b != null) paths.Add(b);
        const float cw = 380f, gap = 24f;
        float w = paths.Count * cw + (paths.Count - 1) * gap + 60f;
        var win = UIKit.PanelBox("Fenêtre", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, 700));
        win.pivot = new Vector2(0.5f, 0.5f);
        var ai = unit.GetComponent<GobelinAI>();
        var title = UIKit.LabelAt(win, $"Faire évoluer {(ai != null ? ai.gobelinName : unit.familyName)}", 36, UIKit.Gold, new Vector2(30, -22), new Vector2(w - 60, 48));
        UIKit.StyleTitle(title);
        UIKit.LabelAt(win, $"Choisissez sa voie : elle mène à une forme ultime différente. Coût : {unit.EffectiveEvolveCost} mana.",
            20, UIKit.Sub, new Vector2(30, -74), new Vector2(w - 60, 34));

        for (int i = 0; i < paths.Count; i++)
        {
            var target = paths[i];
            var tu = target.GetComponent<MonsterUnit>();
            var tai = target.GetComponent<GobelinAI>();
            var card = UIKit.PanelBox("Voie", win, new Vector2(0, 1), new Vector2(30 + i * (cw + gap), -120), new Vector2(cw, 540), UIKit.Card);
            // apparence de la nouvelle forme
            var pic = UIKit.Place(UIKit.Rect("Aperçu", card), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(200, 200));
            var img = pic.gameObject.AddComponent<Image>();
            var sr = target.GetComponent<SpriteRenderer>();
            img.sprite = tu != null && tu.portrait != null ? tu.portrait : sr != null ? sr.sprite : null;
            img.color = sr != null ? sr.color : Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            string tag = string.IsNullOrEmpty(tu?.role) ? "Voie classique" : "Voie de rôle";
            UIKit.LabelAt(card, tag, 18, string.IsNullOrEmpty(tu?.role) ? UIKit.Sub : UIKit.Mana, new Vector2(0, -200), new Vector2(cw, 26),
                TextAlignmentOptions.Top, new Vector2(0.5f, 1));
            var nm = UIKit.LabelAt(card, tu != null ? tu.familyName : target.name, 28, UIKit.Text, new Vector2(0, -228), new Vector2(cw - 20, 40),
                TextAlignmentOptions.Top, new Vector2(0.5f, 1));
            UIKit.StyleHead(nm);
            var desc = UIKit.LabelAt(card, tu != null ? tu.pathDesc : "", 19, UIKit.Sub, new Vector2(0, -274), new Vector2(cw - 36, 120),
                TextAlignmentOptions.Top, new Vector2(0.5f, 1));
            desc.textWrappingMode = TextWrappingModes.Normal;
            if (tai != null)
            {
                string kind = tai.attackRange >= 2.5f ? "distance" : "mêlée";
                UIKit.LabelAt(card, $"PV {tai.maxHealth:0}   ·   Dégâts {tai.attackDamage:0}   ·   {kind}", 19, UIKit.Gold,
                    new Vector2(0, -400), new Vector2(cw - 20, 30), TextAlignmentOptions.Top, new Vector2(0.5f, 1));
            }
            string final = FinalName(target);
            if (!string.IsNullOrEmpty(final))
                UIKit.LabelAt(card, $"Forme ultime : {final}", 18, UIKit.Sub, new Vector2(0, -432), new Vector2(cw - 20, 28),
                    TextAlignmentOptions.Top, new Vector2(0.5f, 1));
            var t = target;
            UIKit.BtnAt(card, "Choisir", () => { var u = unit; Close(); u.Evolve(t); }, new Vector2(0.5f, 0), new Vector2(0, 18),
                new Vector2(cw - 60, 62), 24, new Color(0.62f, 0.46f, 0.1f, 1f));
        }
        UIKit.BtnAt(win, "X", Close, new Vector2(1, 1), new Vector2(-14, -14), new Vector2(56, 56), 26);
    }

    private static string FinalName(GameObject p)
    {
        var u = p != null ? p.GetComponent<MonsterUnit>() : null;
        if (u == null || u.nextStage == null) return null;
        for (int guard = 0; u.nextStage != null && guard < 4; guard++) u = u.nextStage.GetComponent<MonsterUnit>();
        return u != null ? u.familyName : null;
    }

    private void Close()
    {
        PointerUtil.BlockWorldInput(0.2f);
        Destroy(gameObject);
    }
}
