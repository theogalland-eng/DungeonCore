using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fenêtre « Colonie & relations » (bouton Colonie du HUD), en trois onglets :
///   - RELATIONS : jauge de relation, pots-de-vin, trêve, intrus épargnés, raids nocturnes ;
///   - HABITANTS : chaque colon, son métier, sa maison et ce qu'il fait ;
///   - COMMERCE : le marché secret avec le Noyau, et l'annexion de la colonie conquise en avant-poste.
/// </summary>
public class ColonyWindow : MonoBehaviour
{
    public static ColonyWindow Instance { get; private set; }

    private Canvas canvas;
    private RectTransform window;
    private TextMeshProUGUI relationState, relationEffects, punitiveText, spareText, raidText;
    private RectTransform gaugeMarker;
    private Button bribeBtn, truceBtn, spareBtn, terrorBtn, bloodBtn, campBtn;
    private float refresh;
    private int page;
    private readonly RectTransform[] pages = new RectTransform[3];
    private readonly Image[] tabImgs = new Image[3];
    private TextMeshProUGUI peopleText, tradeText, outpostText;
    private readonly System.Collections.Generic.List<(ColonyTrade.Offer offer, Button btn)> offerBtns = new System.Collections.Generic.List<(ColonyTrade.Offer, Button)>();
    private Button annexBtn;

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
        string[] tabNames = { "Relations", "Habitants", "Commerce" };
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            var tb = UIKit.BtnAt(window, tabNames[i], () => SetPage(k), new Vector2(1, 1), new Vector2(-96 - (2 - i) * 172, -24), new Vector2(164, 52), 20);
            tabImgs[i] = tb.GetComponent<Image>();
            pages[i] = UIKit.Stretch(UIKit.Rect("Page" + i, window));
        }
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
        var rel = Section(pages[0], "Relation", "Relation", "people", -236, 272);
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
        punitiveText = UIKit.LabelAt(pages[0], "", 20, GameHUD.DangerColor, new Vector2(0, -514), new Vector2(800, 30), TextAlignmentOptions.Top, new Vector2(0.5f, 1));

        // ── Intrus
        var intr = Section(pages[0], "Intrus", "Intrus sur l'île", "skull", -548, 150);
        spareText = UIKit.LabelAt(intr, "", 19, UIKit.Sub, new Vector2(20, -54), new Vector2(420, 84));
        spareBtn = UIKit.BtnAt(intr, "Laisser partir", () => { RelationSystem.Instance?.SpareAll(); Refresh(); }, new Vector2(1, 0), new Vector2(-20, 18), new Vector2(320, 66), 21,
            new Color(0.2f, 0.38f, 0.27f, 1f));

        // ── Raids
        var raid = Section(pages[0], "Raids", "Raids nocturnes", "torch", -710, 246);
        raidText = UIKit.LabelAt(raid, "", 18, UIKit.Sub, new Vector2(20, -52), new Vector2(772, 100));
        campBtn = UIKit.BtnAt(raid, "Raid sur le camp", () => { var c = IntruderCamp.Raidable(); if (c != null && c.LaunchRaid()) Close(); },
            new Vector2(1, 1), new Vector2(-16, -10), new Vector2(250, 44), 17, new Color(0.45f, 0.2f, 0.12f, 1f));
        terrorBtn = UIKit.BtnAt(raid, "Raid de terreur", () => { if (RaidSystem.Instance != null && RaidSystem.Instance.Launch(false)) Close(); }, new Vector2(0, 0), new Vector2(20, 18), new Vector2(378, 66), 21,
            new Color(0.42f, 0.3f, 0.12f, 1f));
        bloodBtn = UIKit.BtnAt(raid, "Raid sanglant", () => { if (RaidSystem.Instance != null && RaidSystem.Instance.Launch(true)) Close(); }, new Vector2(1, 0), new Vector2(-20, 18), new Vector2(378, 66), 21,
            new Color(0.5f, 0.13f, 0.14f, 1f));

        // ── Habitants
        var people = Section(pages[1], "Habitants", "Habitants", "people", -236, 720);
        peopleText = UIKit.LabelAt(people, "", 19, UIKit.Text, new Vector2(20, -58), new Vector2(772, 650));
        peopleText.textWrappingMode = TextWrappingModes.NoWrap;
        peopleText.overflowMode = TextOverflowModes.Ellipsis;

        // ── Commerce secret
        var trade = Section(pages[2], "Commerce", "Commerce secret", "coin", -236, 446);
        tradeText = UIKit.LabelAt(trade, "", 18, UIKit.Sub, new Vector2(20, -52), new Vector2(772, 60));
        float oy = -116f;
        foreach (var o in ColonyTrade.Offers)
        {
            var offer = o;
            var row = UIKit.Place(UIKit.Rect("Offre_" + o.id, trade), new Vector2(0, 1), new Vector2(20, oy), new Vector2(772, 98));
            UIKit.Image(row, new Color(0.05f, 0.045f, 0.08f, 0.9f), true, false);
            var nm = UIKit.LabelAt(row, o.name, 21, UIKit.Gold, new Vector2(16, -10), new Vector2(430, 30));
            UIKit.StyleHead(nm);
            var ds = UIKit.LabelAt(row, o.desc, 16, UIKit.Sub, new Vector2(16, -42), new Vector2(470, 52));
            ds.textWrappingMode = TextWrappingModes.Normal;
            var b = UIKit.BtnAt(row, "Acheter", () => { ColonyTrade.Buy(offer); Refresh(); }, new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(256, 66), 19,
                new Color(0.32f, 0.24f, 0.12f, 1f));
            offerBtns.Add((offer, b));
            oy -= 106f;
        }

        // ── Avant-poste
        var outpost = Section(pages[2], "AvantPoste", "Avant-poste du Noyau", "skull", -698, 258);
        outpostText = UIKit.LabelAt(outpost, "", 18, UIKit.Sub, new Vector2(20, -52), new Vector2(772, 110));
        outpostText.textWrappingMode = TextWrappingModes.Normal;
        annexBtn = UIKit.BtnAt(outpost, "Annexer", () => { SettlementManager.Instance?.Annex(); Refresh(); }, new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(520, 66), 21,
            new Color(0.3f, 0.18f, 0.45f, 1f));
        SetPage(0);
    }

    public void SetPage(int p)
    {
        page = p;
        for (int i = 0; i < 3; i++)
        {
            pages[i].gameObject.SetActive(i == p);
            tabImgs[i].color = i == p ? new Color(0.42f, 0.3f, 0.62f, 1f) : UIKit.Button;
        }
        if (IsOpen) Refresh();
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

        if (page == 1) RefreshPeople(colony, exists);
        if (page == 2) RefreshTrade(colony, exists);

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
            var camp = IntruderCamp.Raidable();
            string campWhy = null;
            bool campOk = camp != null && camp.CanRaid(out campWhy);
            campBtn.gameObject.SetActive(camp != null);
            campBtn.interactable = campOk;
            UIKit.SetLabel(campBtn, campOk ? "Raid sur le camp" : $"<size=80%>Camp : {campWhy}</size>");
        }
    }

    private void RefreshPeople(SettlementManager colony, bool exists)
    {
        if (!exists) { peopleText.text = "<color=#A59FB6>Personne pour l'instant.</color>"; return; }
        if (colony.Outpost) { peopleText.text = "<color=#C9A8FF>L'avant-poste n'abrite plus que les ombres du Noyau.</color>"; return; }
        var sb = new System.Text.StringBuilder();
        var contact = ColonyTrade.Open(out _) ? ColonyTrade.Contact : null;
        int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        int shown = 0;
        foreach (var st in SettlerAI.All)
        {
            if (st == null || st.IsDying) continue;
            if (shown == 19) { sb.Append($"<color=#A59FB6>... et {SettlerAI.All.Count - shown} autre(s)</color>"); break; }
            string since = st.arrivedDay >= day ? "arrivé(e) aujourd'hui" : $"depuis {day - st.arrivedDay} j";
            string mark = st == contact ? " <color=#E8C872>· votre contact</color>" : "";
            sb.Append($"<b>{st.settlerName}</b> <color=#C8BFA8>{st.Trade}</color>  <size=85%><color=#8E89A3>{colony.HomeName(st.home)} · {since}</color></size>{mark}\n");
            sb.Append($"<size=85%>   <color=#B79CFF>{st.Activity}</color></size>\n");
            shown++;
        }
        if (shown == 0) sb.Append("<color=#A59FB6>La colonie est vide.</color>");
        peopleText.text = sb.ToString();
    }

    private void RefreshTrade(SettlementManager colony, bool exists)
    {
        bool open = ColonyTrade.Open(out string why);
        var c = ColonyTrade.Contact;
        tradeText.text = open
            ? $"Votre contact : <color=#E8C872>{c?.Title}</color>. Les marchandises sont déposées à l'entrée de la caverne ; chaque offre une fois par jour. <color=#9CF0A8>+relation, +prospérité</color>."
            : $"<color=#9A96B0>{why}.</color> Améliorez la relation (pots-de-vin, intrus épargnés) pour qu'un colon ose commercer avec vous.";
        foreach (var (offer, btn) in offerBtns)
        {
            bool ok = ColonyTrade.CanBuy(offer, out string w);
            btn.interactable = ok;
            string cost = offer.food > 0 ? $"{offer.mana} mana · {offer.food} vivres" : $"{offer.mana} mana";
            UIKit.SetLabel(btn, ok ? $"Acheter ({cost})" : $"<size=80%>{w}</size>");
        }

        if (colony == null || !exists) { outpostText.text = "Aucune colonie."; annexBtn.gameObject.SetActive(false); return; }
        if (colony.Outpost)
        {
            outpostText.text = $"La colonie est votre <color=#C9A8FF>avant-poste</color> : <color=#9CF0A8>+{colony.OutpostDailyMana} mana</color> chaque matin. Plus de milice, plus de renforts pour les expéditions.";
            annexBtn.gameObject.SetActive(false);
            return;
        }
        annexBtn.gameObject.SetActive(true);
        bool can = colony.CanAnnex(out string aw);
        outpostText.text = "Une colonie vidée de ses habitants (raids sanglants, terreur) peut être annexée : ses maisons deviennent un avant-poste du Noyau qui rapporte du mana chaque matin, et elle ne renforce plus les expéditions." +
                           (can ? "" : $"\n<color=#9A96B0>{aw}.</color>");
        annexBtn.interactable = can;
        UIKit.SetLabel(annexBtn, can ? $"Annexer la colonie ({SettlementManager.AnnexCost} mana)" : "<size=85%>Annexer la colonie</size>");
    }
}
