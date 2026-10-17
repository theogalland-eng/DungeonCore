using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fenêtre « Colonie & relations » (bouton Colonie du HUD) :
/// état de la colonie, jauge de relation, pots-de-vin, trêve, intrus épargnés, raids nocturnes.
/// </summary>
public class ColonyWindow : MonoBehaviour
{
    public static ColonyWindow Instance { get; private set; }

    private Canvas canvas;
    private RectTransform window;
    private TextMeshProUGUI relationState, relationEffects, punitiveText, spareText, raidText;
    private RectTransform gaugeMarker;
    private Button bribeBtn, truceBtn, spareBtn, terrorBtn, bloodBtn;
    private float refresh;

    public bool IsOpen => window != null && window.gameObject.activeSelf;

    private void Awake()
    {
        Instance = this;
        var go = new GameObject("ColonyWindow_Canvas");
        canvas = go.AddComponent<Canvas>();
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
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        window.gameObject.SetActive(true);
        blocker.gameObject.SetActive(true);
        window.localScale = Vector3.one * 0.96f;
        Refresh();
    }

    public void Close()
    {
        window.gameObject.SetActive(false);
        blocker.gameObject.SetActive(false);
        PointerUtil.BlockWorldInput(0.2f);
    }

    private TextMeshProUGUI stageText, emptyText;
    private RectTransform blocker;
    private readonly System.Collections.Generic.Dictionary<string, TextMeshProUGUI> stats = new System.Collections.Generic.Dictionary<string, TextMeshProUGUI>();
    private RectTransform statRow;

    /// <summary>Carte de section : fond plus clair, liseré discret, titre en Cinzel avec icône.</summary>
    private static RectTransform Section(RectTransform parent, string name, string title, string icon, float y, float h)
    {
        var card = UIKit.PanelBox(name, parent, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(812, h), UIKit.Card);
        card.pivot = new Vector2(0.5f, 1);
        card.Find("Liseré").GetComponent<Image>().color = UIKit.TrimSoft;
        UIKit.Icon(card, icon, new Vector2(18, -14), 30);
        var t = UIKit.LabelAt(card, title, 24, UIKit.Gold, new Vector2(58, -14), new Vector2(460, 34));
        UIKit.StyleHead(t);
        return card;
    }

    /// <summary>Tuile de statistique : icône, valeur en grand, libellé dessous.</summary>
    private void StatTile(string key, string icon, string label, int index)
    {
        var tile = UIKit.Place(UIKit.Rect("Stat_" + key, statRow), new Vector2(0, 1), new Vector2(index * 136f, 0), new Vector2(128, 96));
        UIKit.Image(tile, new Color(0.05f, 0.045f, 0.08f, 0.9f), true, false);
        UIKit.Icon(tile, icon, new Vector2(0, -8), 34, new Vector2(0.5f, 1));
        var v = UIKit.LabelAt(tile, "-", 24, UIKit.Text, new Vector2(0, -42), new Vector2(124, 30), TextAlignmentOptions.Top, new Vector2(0.5f, 1));
        UIKit.StyleHead(v);   // chiffres alignés (Cinzel)
        UIKit.LabelAt(tile, label, 15, UIKit.Sub, new Vector2(0, -70), new Vector2(124, 22), TextAlignmentOptions.Top, new Vector2(0.5f, 1));
        stats[key] = v;
    }

    private void Build(RectTransform root)
    {
        blocker = UIKit.Blocker(root, 0.55f);
        blocker.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        window = UIKit.PanelBox("ColonyWindow", root, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(860, 984));
        window.pivot = new Vector2(0.5f, 0.5f);

        // ── En-tête
        var title = UIKit.LabelAt(window, "Colonie", 40, UIKit.Gold, new Vector2(30, -20), new Vector2(560, 52));
        UIKit.StyleTitle(title);
        stageText = UIKit.LabelAt(window, "", 20, UIKit.Sub, new Vector2(32, -72), new Vector2(700, 28));
        var close = UIKit.BtnAt(window, "", Close, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(60, 60));
        UIKit.Icon(close.transform, "close", Vector2.zero, 26, new Vector2(0.5f, 0.5f));
        var sep = UIKit.Place(UIKit.Rect("Séparateur", window), new Vector2(0.5f, 1), new Vector2(0, -108), new Vector2(812, 2));
        UIKit.Image(sep, new Color(UIKit.Trim.r, UIKit.Trim.g, UIKit.Trim.b, 0.35f), false, false);

        // ── Chiffres de la colonie
        statRow = UIKit.Place(UIKit.Rect("Stats", window), new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(808, 96));
        StatTile("pop", "people", "habitants", 0);
        StatTile("mood", "mood", "moral", 1);
        StatTile("prosp", "coin", "prospérité", 2);
        StatTile("wood", "log", "bois", 3);
        StatTile("stone", "rock", "pierre", 4);
        StatTile("food", "fish", "vivres", 5);
        emptyText = UIKit.LabelAt(window, "Aucune colonie humaine sur l'île pour l'instant. Elle apparaîtra quand le monde découvrira votre existence.",
            20, UIKit.Sub, new Vector2(0, -132), new Vector2(760, 80), TextAlignmentOptions.Center, new Vector2(0.5f, 1));

        // ── Relation
        var rel = Section(window, "Relation", "Relation", "people", -236, 272);
        relationState = UIKit.LabelAt(rel, "", 24, UIKit.Gold, new Vector2(-20, -16), new Vector2(320, 32), TextAlignmentOptions.TopRight, new Vector2(1, 1));
        var gauge = UIKit.Place(UIKit.Rect("Gauge", rel), new Vector2(0, 1), new Vector2(20, -62), new Vector2(772, 22));
        UIKit.Image(gauge, new Color(0.03f, 0.025f, 0.05f, 0.95f), true, false);
        // dégradé rouge → vert, en segments de pixels
        for (int i = 0; i < 24; i++)
        {
            var seg = UIKit.Rect("Seg", gauge);
            seg.anchorMin = new Vector2(i / 24f, 0); seg.anchorMax = new Vector2((i + 1) / 24f, 1);
            seg.offsetMin = new Vector2(i == 0 ? 5 : 1, 5); seg.offsetMax = new Vector2(i == 23 ? -5 : -1, -5);
            UIKit.Image(seg, Color.Lerp(new Color(0.85f, 0.33f, 0.29f, 0.85f), new Color(0.47f, 0.66f, 0.27f, 0.85f), i / 23f), false, false);
        }
        gaugeMarker = UIKit.Rect("Marker", gauge);
        gaugeMarker.anchorMin = gaugeMarker.anchorMax = new Vector2(0.5f, 0.5f);
        gaugeMarker.sizeDelta = new Vector2(10, 36);
        UIKit.Image(gaugeMarker, UIKit.Text, true, false);
        var trucePos = UIKit.Rect("TruceMark", gauge);
        trucePos.anchorMin = trucePos.anchorMax = new Vector2(0.8f, 0.5f);
        trucePos.sizeDelta = new Vector2(3, 30);
        UIKit.Image(trucePos, new Color(0.6f, 1f, 0.7f, 0.8f), false, false);
        UIKit.LabelAt(rel, "haine", 14, UIKit.Sub, new Vector2(20, -88), new Vector2(120, 20));
        UIKit.LabelAt(rel, "trêve", 14, UIKit.Sub, new Vector2(20 + 772 * 0.8f - 20, -88), new Vector2(120, 20));
        UIKit.LabelAt(rel, "respect", 14, UIKit.Sub, new Vector2(-20, -88), new Vector2(120, 20), TextAlignmentOptions.TopRight, new Vector2(1, 1));

        relationEffects = UIKit.LabelAt(rel, "", 19, UIKit.Sub, new Vector2(20, -114), new Vector2(772, 96));
        bribeBtn = UIKit.BtnAt(rel, "Pot-de-vin", () => { RelationSystem.Instance?.Bribe(); Refresh(); }, new Vector2(0, 0), new Vector2(20, 18), new Vector2(378, 66), 21);
        truceBtn = UIKit.BtnAt(rel, "Proposer une trêve", () =>
        {
            var r = RelationSystem.Instance;
            if (r == null) return;
            if (r.truce) r.BreakTruce(); else if (r.ProposeTruce()) Close();
            Refresh();
        }, new Vector2(1, 0), new Vector2(-20, 18), new Vector2(378, 66), 21);
        punitiveText = UIKit.LabelAt(window, "", 20, GameHUD.DangerColor, new Vector2(0, -514), new Vector2(800, 30), TextAlignmentOptions.Top, new Vector2(0.5f, 1));

        // ── Intrus
        var intr = Section(window, "Intrus", "Intrus sur l'île", "skull", -548, 150);
        spareText = UIKit.LabelAt(intr, "", 19, UIKit.Sub, new Vector2(20, -54), new Vector2(420, 84));
        spareBtn = UIKit.BtnAt(intr, "Laisser partir", () => { RelationSystem.Instance?.SpareAll(); Refresh(); }, new Vector2(1, 0), new Vector2(-20, 18), new Vector2(320, 66), 21,
            new Color(0.2f, 0.38f, 0.27f, 1f));

        // ── Raids
        var raid = Section(window, "Raids", "Raids nocturnes", "torch", -710, 246);
        raidText = UIKit.LabelAt(raid, "", 18, UIKit.Sub, new Vector2(20, -52), new Vector2(772, 100));
        terrorBtn = UIKit.BtnAt(raid, "Raid de terreur", () => { if (RaidSystem.Instance != null && RaidSystem.Instance.Launch(false)) Close(); }, new Vector2(0, 0), new Vector2(20, 18), new Vector2(378, 66), 21,
            new Color(0.42f, 0.3f, 0.12f, 1f));
        bloodBtn = UIKit.BtnAt(raid, "Raid sanglant", () => { if (RaidSystem.Instance != null && RaidSystem.Instance.Launch(true)) Close(); }, new Vector2(1, 0), new Vector2(-20, 18), new Vector2(378, 66), 21,
            new Color(0.5f, 0.13f, 0.14f, 1f));
    }

    private void Update()
    {
        if (!IsOpen) return;
        // petite apparition
        if (window.localScale.x < 1f) window.localScale = Vector3.one * Mathf.MoveTowards(window.localScale.x, 1f, Time.unscaledDeltaTime * 0.5f);
        refresh -= Time.unscaledDeltaTime;
        if (refresh <= 0f) Refresh();
    }

    public void Refresh()
    {
        refresh = 0.4f;
        var colony = SettlementManager.Instance;
        var rel = RelationSystem.Instance;
        var raids = RaidSystem.Instance;
        bool exists = colony != null && colony.enabled && colony.stage > 0;

        statRow.gameObject.SetActive(exists);
        emptyText.gameObject.SetActive(!exists);
        if (exists)
        {
            string mood = colony.morale < 25 ? "<color=#FF8A7A>terrifiée</color>" : colony.morale < 45 ? "<color=#FFC266>inquiète</color>" : "<color=#9CF0A8>sereine</color>";
            stageText.text = $"{colony.StageName} · population {mood}";
            stats["pop"].text = $"{colony.Population}<size=70%><color=#A59FB6>/{colony.Capacity}</color></size>";
            stats["mood"].text = $"{colony.morale:0}";
            stats["prosp"].text = $"{colony.prosperity:0}";
            stats["wood"].text = colony.wood.ToString();
            stats["stone"].text = colony.stone.ToString();
            stats["food"].text = colony.food < colony.Population * 3 ? $"<color=#FF8A7A>{colony.food}</color>" : colony.food.ToString();
        }
        else stageText.text = "Pas encore de colonie";

        if (rel != null)
        {
            relationState.text = $"<color=#{ColorUtility.ToHtmlStringRGB(rel.StateColor)}>{rel.StateName}</color>  <size=80%>({rel.relation:+0;-0;0})</size>";
            gaugeMarker.anchorMin = gaugeMarker.anchorMax = new Vector2((rel.relation + 100f) / 200f, 0.5f);
            string fx;
            if (rel.truce) fx = $"Trêve : offrande de <color=#9CF0A8>{rel.DailyTribute} mana</color> chaque matin, expéditions plus rares (+1 jour) et plus petites (-1). <color=#FF9A8A>Tuer un habitant = trahison.</color>";
            else if (rel.relation <= -50f) fx = "Haine : expéditions d'un palier plus fort et plus nombreuses.";
            else if (rel.relation <= -15f) fx = "Hostilité : un intrus de plus dans chaque expédition.";
            else if (rel.relation >= rel.truceThreshold) fx = "Respect : une trêve peut être proposée.";
            else if (rel.relation >= 25f) fx = "Tolérance : la colonie envoie ses expéditions moins souvent (+½ jour).";
            else fx = "Méfiance : aucune conséquence particulière.";
            relationEffects.text = fx + "\n<size=90%><color=#8E89A3>Tuer : − · laisser repartir / épargner : + · pots-de-vin : + · raids : −− (trahison si trêve).</color></size>";

            bool okBribe = rel.CanBribe(out string whyBribe);
            UIKit.SetLabel(bribeBtn, okBribe ? $"Pot-de-vin ({rel.BribeCost} mana)" : $"<size=80%>Pot-de-vin : {whyBribe}</size>");
            bribeBtn.interactable = okBribe;
            if (rel.truce)
            {
                UIKit.SetLabel(truceBtn, "Rompre la trêve");
                truceBtn.interactable = true;
                truceBtn.GetComponent<Image>().color = new Color(0.45f, 0.14f, 0.16f, 1f);
            }
            else
            {
                bool ok = rel.CanProposeTruce(out string why);
                UIKit.SetLabel(truceBtn, ok ? $"Proposer une trêve ({rel.truceCost} mana)" : $"<size=80%>Trêve : {why}</size>");
                truceBtn.interactable = ok;
                truceBtn.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.28f, 1f);
            }
            punitiveText.text = rel.PunitivePending ? $"Expédition punitive attendue le jour {rel.PunitiveDay} (palier renforcé, plus nombreuse)" : "";
        }

        int present = 0;
        foreach (var a in AdventurerAI.All) if (a != null && !a.Spared) present++;
        spareText.text = present > 0
            ? $"{present} intrus sur l'île. Les laisser partir : vos monstres s'écartent, la colonie vous en sait gré (+relation)."
            : "Aucun intrus en ce moment. Épargner des intrus améliore la relation avec la colonie.";
        spareBtn.interactable = present > 0;

        if (raids != null)
        {
            bool can = raids.CanRaid(out string why);
            var team = raids.PickRaiders();
            int sel = MonsterSelector.Instance != null ? MonsterSelector.Instance.SelectedGoblins.Count : 0;
            string who = team.Count == 0 ? "aucun monstre disponible" : sel > 0 ? $"votre sélection ({team.Count} monstre(s))" : $"vos {team.Count} monstre(s) les plus forts (sélectionnez-en pour choisir)";
            string militia = colony != null && colony.MilitiaDamage > 0 ? $" <color=#FFC266>Une milice défend la colonie ({colony.MilitiaDamage:0} dégâts par assaut).</color>" : "";
            raidText.text = raids.Active ? "<color=#FFC266>Raid en cours...</color>"
                : $"Participants : {who}.{militia}\n<color=#E0C38A>Terreur</color> : moral effondré, vivres pillés, un peu de mana, personne ne meurt. " +
                  $"<color=#FF8A7A>Sanglant</color> : colons tués (beaucoup de mana et d'XP) mais <b>expédition punitive</b> de haut niveau." +
                  (can ? "" : $"\n<color=#9A96B0>{why}</color>");
            terrorBtn.interactable = can;
            bloodBtn.interactable = can;
        }
    }
}
