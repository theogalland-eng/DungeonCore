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
    private TextMeshProUGUI colonyText, relationState, relationEffects, punitiveText, spareText, raidText;
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
        Refresh();
    }

    public void Close()
    {
        window.gameObject.SetActive(false);
        PointerUtil.BlockWorldInput(0.2f);
    }

    private void Build(RectTransform root)
    {
        window = UIKit.PanelBox("ColonyWindow", root, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(820, 900));
        window.pivot = new Vector2(0.5f, 0.5f);
        UIKit.LabelAt(window, "Colonie & relations", 38, UIKit.Gold, new Vector2(28, -20), new Vector2(600, 50));
        UIKit.BtnAt(window, "Fermer", Close, new Vector2(1, 1), new Vector2(-18, -18), new Vector2(130, 50), 22);

        colonyText = UIKit.LabelAt(window, "", 22, UIKit.Text, new Vector2(28, -80), new Vector2(764, 64));

        // ── Relation
        var rel = UIKit.PanelBox("Relation", window, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(772, 300), UIKit.Card);
        rel.pivot = new Vector2(0.5f, 1);
        UIKit.LabelAt(rel, "Relation avec la colonie", 26, UIKit.Text, new Vector2(20, -14), new Vector2(420, 34));
        relationState = UIKit.LabelAt(rel, "", 26, UIKit.Gold, new Vector2(-20, -14), new Vector2(300, 34), TextAlignmentOptions.TopRight, new Vector2(1, 1));
        var gauge = UIKit.Place(UIKit.Rect("Gauge", rel), new Vector2(0, 1), new Vector2(20, -58), new Vector2(732, 20));
        UIKit.Image(gauge, new Color(0, 0, 0, 0.5f), false, false);
        // dégradé rouge → vert
        for (int i = 0; i < 20; i++)
        {
            var seg = UIKit.Rect("Seg", gauge);
            seg.anchorMin = new Vector2(i / 20f, 0); seg.anchorMax = new Vector2((i + 1) / 20f, 1);
            seg.offsetMin = new Vector2(1, 3); seg.offsetMax = new Vector2(-1, -3);
            UIKit.Image(seg, Color.Lerp(new Color(0.85f, 0.25f, 0.2f, 0.55f), new Color(0.3f, 0.85f, 0.45f, 0.55f), i / 19f), false, false);
        }
        gaugeMarker = UIKit.Rect("Marker", gauge);
        gaugeMarker.anchorMin = gaugeMarker.anchorMax = new Vector2(0.5f, 0.5f);
        gaugeMarker.sizeDelta = new Vector2(8, 34);
        UIKit.Image(gaugeMarker, Color.white, false, false);
        var trucePos = UIKit.Rect("TruceMark", gauge);
        trucePos.anchorMin = trucePos.anchorMax = new Vector2(0.8f, 0.5f);
        trucePos.sizeDelta = new Vector2(3, 28);
        UIKit.Image(trucePos, new Color(0.6f, 1f, 0.7f, 0.8f), false, false);

        relationEffects = UIKit.LabelAt(rel, "", 19, UIKit.Sub, new Vector2(20, -90), new Vector2(732, 110));
        bribeBtn = UIKit.BtnAt(rel, "Pot-de-vin", () => { RelationSystem.Instance?.Bribe(); Refresh(); }, new Vector2(0, 0), new Vector2(20, 18), new Vector2(360, 66), 22);
        truceBtn = UIKit.BtnAt(rel, "Proposer une trêve", () =>
        {
            var r = RelationSystem.Instance;
            if (r == null) return;
            if (r.truce) r.BreakTruce(); else if (r.ProposeTruce()) Close();
            Refresh();
        }, new Vector2(1, 0), new Vector2(-20, 18), new Vector2(360, 66), 22);
        punitiveText = UIKit.LabelAt(window, "", 22, GameHUD.DangerColor, new Vector2(28, -458), new Vector2(764, 34));

        // ── Intrus
        var intr = UIKit.PanelBox("Intrus", window, new Vector2(0.5f, 1), new Vector2(0, -496), new Vector2(772, 118), UIKit.Card);
        intr.pivot = new Vector2(0.5f, 1);
        spareText = UIKit.LabelAt(intr, "", 20, UIKit.Sub, new Vector2(20, -14), new Vector2(380, 90));
        spareBtn = UIKit.BtnAt(intr, "Laisser partir les intrus", () => { RelationSystem.Instance?.SpareAll(); Refresh(); }, new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(340, 66), 21,
            new Color(0.2f, 0.4f, 0.28f, 1f));

        // ── Raids
        var raid = UIKit.PanelBox("Raids", window, new Vector2(0.5f, 1), new Vector2(0, -626), new Vector2(772, 250), UIKit.Card);
        raid.pivot = new Vector2(0.5f, 1);
        UIKit.LabelAt(raid, "Raids nocturnes", 26, UIKit.Text, new Vector2(20, -14), new Vector2(420, 34));
        raidText = UIKit.LabelAt(raid, "", 19, UIKit.Sub, new Vector2(20, -52), new Vector2(732, 104));
        terrorBtn = UIKit.BtnAt(raid, "Raid de terreur", () => { if (RaidSystem.Instance != null && RaidSystem.Instance.Launch(false)) Close(); }, new Vector2(0, 0), new Vector2(20, 18), new Vector2(360, 66), 22,
            new Color(0.42f, 0.3f, 0.12f, 1f));
        bloodBtn = UIKit.BtnAt(raid, "Raid sanglant", () => { if (RaidSystem.Instance != null && RaidSystem.Instance.Launch(true)) Close(); }, new Vector2(1, 0), new Vector2(-20, 18), new Vector2(360, 66), 22,
            new Color(0.5f, 0.13f, 0.14f, 1f));
    }

    private void Update()
    {
        if (!IsOpen) return;
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

        if (exists)
        {
            string mood = colony.morale < 25 ? "<color=#FF8A7A>terrifiée</color>" : colony.morale < 45 ? "<color=#FFC266>inquiète</color>" : "<color=#9CF0A8>sereine</color>";
            colonyText.text = $"<b>{colony.StageName}</b> · {colony.Population}/{colony.Capacity} habitants · moral {colony.morale:0} ({mood})\n" +
                              $"<size=88%><color=#B9B3CC>Prospérité {colony.prosperity:0} · bois {colony.wood} · pierre {colony.stone} · vivres {colony.food}</color></size>";
        }
        else colonyText.text = "<color=#9A96B0>Aucune colonie humaine sur l'île pour l'instant. Elle apparaîtra quand le monde découvrira votre existence.</color>";

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
