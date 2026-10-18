using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Interface principale, construite par code (aucun réglage de scène nécessaire).
/// Pensée pour le mobile : gros boutons, zone sûre (encoches), joystick virtuel.
/// Les couleurs / tailles sont regroupées ici pour faciliter un futur habillage graphique.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    public static readonly Color ManaColor = UIKit.Mana;
    public static readonly Color WarningColor = new Color(1f, 0.78f, 0.35f);
    public static readonly Color DangerColor = new Color(1f, 0.42f, 0.38f);
    public static readonly Color InfoColor = new Color(0.86f, 0.9f, 1f);
    public static readonly Color GoldColor = UIKit.Gold;

    private static readonly Color PanelColor = new Color(UIKit.Panel.r, UIKit.Panel.g, UIKit.Panel.b, 0.92f);
    private static readonly Color ButtonColor = UIKit.Button;
    private static readonly Color ButtonActiveColor = UIKit.Accent;
    private static readonly Color SegmentColor = new Color(0f, 0f, 0f, 0f);
    private static readonly Color TextColor = UIKit.Text;
    private static readonly Color SubTextColor = UIKit.Sub;

    [Tooltip("Masquer l'ancien Canvas (textes Mana/Mode positionnés pour un écran QHD)")]
    public bool hideLegacyCanvas = true;

    private static readonly List<KeyValuePair<string, Color>> pendingToasts = new List<KeyValuePair<string, Color>>();

    private Canvas canvas;
    private RectTransform root;
    private Rect lastSafeArea;

    // Barre du haut
    private RectTransform hpFill, manaFill, xpFill, dayFill;
    private TextMeshProUGUI hpText, manaText, xpText, dayText, countsText, threatText, expeditionText;
    private readonly List<KeyValuePair<float, Image>> speedButtons = new List<KeyValuePair<float, Image>>();
    private Image pauseButtonImage;
    private TextMeshProUGUI skillLabel;
    private RectTransform skillCd;
    private Button talentButton;
    private TextMeshProUGUI talentBadge;
    private GameObject callBoatButton;
    private TextMeshProUGUI pausedLabel;

    // Outils
    private SidePanel sidePanel;
    private RectTransform hintPanel;
    private TextMeshProUGUI hintText;

    // Sélection
    private RectTransform selectionPanel;
    private GameObject evolveButton, guardianButton, talkButton;
    private TextMeshProUGUI evolveLabel, colonyText;
    private TextMeshProUGUI selectionText;
    private GameObject possessButton, patrolButton, holdButton;

    // Possession
    private RectTransform possessionPanel;
    private TextMeshProUGUI possessionText;
    private VirtualJoystick joystick;

    // Messages
    private RectTransform feed;
    private readonly List<Toaster> toasts = new List<Toaster>();

    // Dialogue
    private RectTransform dialogueRoot;
    private TextMeshProUGUI speakerText, dialogueText, continueText;
    private float typeProgress;
    private bool typing;

    // Fin de partie
    private RectTransform gameOverPanel;
    private TextMeshProUGUI gameOverStats;

    private float refreshTimer;

    private class Toaster
    {
        public RectTransform rt;
        public CanvasGroup group;
        public float life;
    }

    // ─────────────────────────────────────────────
    // API STATIQUE
    // ─────────────────────────────────────────────

    /// <summary>Masque tout le HUD (cinématiques).</summary>
    /// <summary>Panneau supplémentaire dans le HUD (objectifs...).</summary>
    public RectTransform Root => root;
    public SidePanel Side => sidePanel;
    public RectTransform AddPanel(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) => Panel(name, root, anchor, pivot, pos, size);
    public static TextMeshProUGUI AddLabel(RectTransform parent, string text, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align) =>
        Label(parent, text, size, color, pos, box, align);

    public bool HudVisible => canvas != null && canvas.enabled && !panelsHidden;

    private bool panelsHidden;
    private readonly List<GameObject> hiddenByUs = new List<GameObject>();

    /// <summary>
    /// Masque les panneaux du mode Noyau mais garde les dialogues et les messages
    /// (intérieurs, mode Aventurier) : un dialogue ne doit jamais être invisible.
    /// </summary>
    public void SetPanelsVisible(bool v)
    {
        panelsHidden = !v;
        if (v)
        {
            foreach (var go in hiddenByUs) if (go != null) go.SetActive(true);
            hiddenByUs.Clear();
        }
        else ApplyPanelsHidden();
    }

    private void ApplyPanelsHidden()
    {
        foreach (Transform c in root)
        {
            if (c == dialogueRoot || c == feed || c == gameOverPanel) continue;
            if (c.gameObject.activeSelf) { c.gameObject.SetActive(false); hiddenByUs.Add(c.gameObject); }
        }
    }

    public void SetHudVisible(bool v)
    {
        if (canvas != null) canvas.enabled = v;
    }

    // ── Intro : le HUD, ce sont les sens du Noyau ; chaque élément apparaît quand il le découvre ──
    private bool introHud;
    private readonly List<GameObject> introHidden = new List<GameObject>();
    private readonly Dictionary<string, List<CanvasGroup>> introParts = new Dictionary<string, List<CanvasGroup>>();
    private readonly Dictionary<string, float> introAlpha = new Dictionary<string, float>();
    private CanvasGroup introCorePanel;

    /// <summary>Intro : seuls le panneau du Cœur (lignes Cœur / Mana / Niveau) et le Jour existent, invisibles au départ.</summary>
    public void IntroHud(bool on)
    {
        if (on == introHud || root == null) return;
        introHud = on;
        if (on)
        {
            introHidden.Clear();
            introParts.Clear();
            introAlpha.Clear();
            foreach (Transform ch in root)
                if (ch.gameObject.activeSelf && ch.name != "CorePanel" && ch.name != "DayPanel") { ch.gameObject.SetActive(false); introHidden.Add(ch.gameObject); }
            var core = root.Find("CorePanel");
            introCorePanel = core.gameObject.GetComponent<CanvasGroup>();
            if (introCorePanel == null) introCorePanel = core.gameObject.AddComponent<CanvasGroup>();
            foreach (Transform ch in core)
            {
                float y = ((RectTransform)ch).anchoredPosition.y;
                string part = y > -40f ? "coeur" : y > -85f ? "mana" : "xp";
                AddIntroPart(part, ch.gameObject);
            }
            AddIntroPart("jour", root.Find("DayPanel").gameObject);
            foreach (var k in introParts.Keys) introAlpha[k] = 0f;
            ApplyIntroAlpha();
            if (canvas != null) canvas.enabled = true;
        }
        else
        {
            foreach (var go in introHidden) if (go != null) go.SetActive(true);
            introHidden.Clear();
            foreach (var list in introParts.Values) foreach (var g in list) if (g != null) g.alpha = 1f;
            if (introCorePanel != null) introCorePanel.alpha = 1f;
            introParts.Clear();
        }
    }

    private void AddIntroPart(string part, GameObject go)
    {
        if (!introParts.TryGetValue(part, out var list)) introParts[part] = list = new List<CanvasGroup>();
        var g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        list.Add(g);
    }

    // le rafraîchissement du HUD réaffiche certains boutons : pendant l'intro, on les recache aussitôt
    private void LateUpdate()
    {
        if (!introHud || root == null) return;
        foreach (Transform ch in root)
            if (ch.gameObject.activeSelf && ch.name != "CorePanel" && ch.name != "DayPanel")
            {
                ch.gameObject.SetActive(false);
                if (!introHidden.Contains(ch.gameObject)) introHidden.Add(ch.gameObject);
            }
    }

    /// <summary>Opacité d'un élément du HUD d'intro : "coeur", "mana", "xp" ou "jour".</summary>
    public void IntroReveal(string part, float alpha)
    {
        if (!introHud) return;
        introAlpha[part] = alpha;
        ApplyIntroAlpha();
    }

    private void ApplyIntroAlpha()
    {
        foreach (var kv in introParts)
            foreach (var g in kv.Value) if (g != null) g.alpha = introAlpha.TryGetValue(kv.Key, out var a) ? a : 0f;
        if (introCorePanel != null)
        {
            float a = 0f;
            foreach (var k in new[] { "coeur", "mana", "xp" }) if (introAlpha.TryGetValue(k, out var v)) a = Mathf.Max(a, v);
            introCorePanel.alpha = a;
            // les lignes déjà visibles restent pleines quand le panneau apparaît
            foreach (var k in new[] { "coeur", "mana", "xp" })
                if (introParts.TryGetValue(k, out var list) && introAlpha.TryGetValue(k, out var v))
                    foreach (var g in list) g.alpha = a > 0f ? v / a : 0f;
        }
    }

    public static void Toast(string message, Color color)
    {
        // HUD absent ou masqué (cinématique) : TMP ne s'initialise pas sur un objet inactif, on attend
        if (Instance == null || Instance.feed == null || !Instance.feed.gameObject.activeInHierarchy)
        {
            if (pendingToasts.Count < 12) pendingToasts.Add(new KeyValuePair<string, Color>(message, color));
            return;
        }
        Instance.AddToast(message, color);
    }

    // ─────────────────────────────────────────────
    // CONSTRUCTION
    // ─────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        EnsureEventSystem();
        BuildCanvas();
        BuildTopBar();
        BuildToolbar();
        BuildSpellBar();
        BuildSelectionPanel();
        BuildPossessionPanel();
        BuildFeed();
        BuildDialogue();
        BuildGameOver();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private void Start()
    {
        if (hideLegacyCanvas)
        {
            var legacy = FindFirstObjectByType<DungeonBuilderUI>();
            if (legacy != null && legacy.manaText != null && legacy.manaText.canvas != null)
                legacy.manaText.canvas.rootCanvas.gameObject.SetActive(false);
        }

        if (GameManager.Instance != null) GameManager.Instance.OnGameOver += ShowGameOver;
        if (PossessionController.Instance != null) PossessionController.Instance.OnPossessionChanged += OnPossessionChanged;


        OnPossessionChanged(false);
        Refresh();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildCanvas()
    {
        var go = new GameObject("GameHUD_Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();

        root = NewRect("SafeArea", go.transform);
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Rect sa = Screen.safeArea;
        if (sa == lastSafeArea || Screen.width == 0) return;
        lastSafeArea = sa;
        root.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        root.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        root.offsetMin = root.offsetMax = Vector2.zero;
    }

    private void BuildTopBar()
    {
        // ── Gauche : cœur, mana, niveau ──
        // carte du Noyau : icône du cristal, barres cœur / mana, niveau
        var left = Panel("CorePanel", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(560, 150));
        var portrait = Place(NewRect("Portrait", left), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -14), new Vector2(84, 84));
        var pImg = portrait.gameObject.AddComponent<Image>();
        pImg.sprite = SpriteUtil.RoundedUI; pImg.type = Image.Type.Sliced; pImg.color = new Color(0.07f, 0.06f, 0.1f, 1f); pImg.raycastTarget = false;
        UIKit.Frame(portrait, new Color(UIKit.Trim.r, UIKit.Trim.g, UIKit.Trim.b, 0.6f));
        UIKit.Icon(portrait, "mana", Vector2.zero, 60, new Vector2(0.5f, 0.5f));

        UIKit.Icon(left, "heart", new Vector2(112, -16), 30);
        Bar(left, new Vector2(150, -18), new Vector2(268, 26), UIKit.Heart, out hpFill);
        hpText = Label(left, "", 22, TextColor, new Vector2(428, -14), new Vector2(124, 34), TextAlignmentOptions.Left);

        UIKit.Icon(left, "mana", new Vector2(112, -58), 30);
        Bar(left, new Vector2(150, -60), new Vector2(268, 26), ManaColor, out manaFill);
        manaText = Label(left, "", 22, TextColor, new Vector2(428, -56), new Vector2(128, 34), TextAlignmentOptions.Left);

        UIKit.Icon(left, "star", new Vector2(112, -100), 26);
        xpText = Label(left, "", 20, SubTextColor, new Vector2(146, -98), new Vector2(400, 30), TextAlignmentOptions.Left);
        Bar(left, new Vector2(112, -130), new Vector2(430, 8), GoldColor, out xpFill);

        // Menu (pause, sauvegarde) et fenêtre de la colonie
        WithIcon(MakeButton(root, "Menu", "Menu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(588, -16), new Vector2(150, 66),
            () => MainMenu.Instance?.OpenPause(), 22), "menu");
        colonyButton = MakeButton(root, "Colony", "Colonie", new Vector2(0, 1), new Vector2(0, 1), new Vector2(588, -92), new Vector2(150, 58),
            () => ColonyWindow.Instance?.Toggle(), 20);
        colonyButton.GetComponent<Image>().color = new Color(0.17f, 0.25f, 0.2f, 0.97f);
        WithIcon(colonyButton, "house");
        colonyBadge = Label((RectTransform)colonyButton.transform, "", 15, GoldColor, new Vector2(0, -38), new Vector2(150, 22), TextAlignmentOptions.Center, true);
        // Vue île (volcan en relief) / vue donjon (intérieur de la montagne)
        viewButton = MakeButton(root, "View", "Vue île", new Vector2(0, 1), new Vector2(0, 1), new Vector2(588, -158), new Vector2(150, 58),
            () => MountainRelief.Instance?.Toggle(), 19);
        viewButton.GetComponent<Image>().color = new Color(0.27f, 0.19f, 0.15f, 0.97f);
        WithIcon(viewButton, "volcano");
        // talents du Noyau, avec le nombre de points libres
        talentButton = MakeButton(root, "Talents", "Talents", new Vector2(0, 1), new Vector2(0, 1), new Vector2(588, -224), new Vector2(150, 58),
            () => TalentTreeWindow.Instance?.Toggle(), 19);
        talentButton.GetComponent<Image>().color = new Color(0.25f, 0.2f, 0.36f, 0.97f);
        WithIcon(talentButton, "star");
        talentBadge = Label((RectTransform)talentButton.transform, "", 15, GoldColor, new Vector2(0, -38), new Vector2(150, 22), TextAlignmentOptions.Center, true);

        // ── Centre : jour ──
        var center = Panel("DayPanel", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(460, 112));
        dayText = Label(center, "", 30, GoldColor, new Vector2(0, -8), new Vector2(440, 40), TextAlignmentOptions.Center, true);
        UIKit.StyleHead(dayText);
        dayText.textWrappingMode = TextWrappingModes.NoWrap;
        dayText.enableAutoSizing = true;
        dayText.fontSizeMin = 16f;
        dayText.fontSizeMax = 30f;
        Bar(center, new Vector2(30, -54), new Vector2(400, 8), GoldColor, out dayFill);
        countsText = Label(center, "", 21, SubTextColor, new Vector2(0, -70), new Vector2(460, 32), TextAlignmentOptions.Center, true);

        // ── Droite : menace + vitesse ──
        var right = Panel("ThreatPanel", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(560, 180));
        UIKit.Icon(right, "skull", new Vector2(16, -12), 28);
        threatText = Label(right, "", 23, DangerColor, new Vector2(52, -10), new Vector2(366, 32), TextAlignmentOptions.Left);
        threatText.textWrappingMode = TextWrappingModes.NoWrap;
        threatText.enableAutoSizing = true;
        threatText.fontSizeMin = 13f;
        threatText.fontSizeMax = 23f;
        expeditionText = Label(right, "", 20, SubTextColor, new Vector2(18, -44), new Vector2(440, 30), TextAlignmentOptions.Left);

        colonyText = Label(right, "", 19, SubTextColor, new Vector2(18, -142), new Vector2(530, 30), TextAlignmentOptions.Left);
        // contrôle segmenté : pause | x1 | x2 | x3 | x5 dans un seul cadre
        var seg = Place(NewRect("SpeedGroup", right), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -76), new Vector2(536, 68));
        var segImg = seg.gameObject.AddComponent<Image>();
        segImg.sprite = SpriteUtil.RoundedUI; segImg.type = Image.Type.Sliced;
        segImg.color = new Color(0.05f, 0.045f, 0.08f, 1f); segImg.raycastTarget = false;
        UIKit.Frame(seg, UIKit.TrimSoft);
        float x = 8;
        var pb = MakeButton(seg, "Pause", "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -6), new Vector2(98, 56),
            () => GameManager.Instance?.ToggleUserPause(), 26);
        UIKit.Icon(pb.transform, "pause", Vector2.zero, 30, new Vector2(0.5f, 0.5f));
        Segment(pb);
        pauseButtonImage = pb.GetComponent<Image>();
        x += 105;
        foreach (float s in new[] { 1f, 2f, 3f, 5f })
        {
            float speed = s;
            var b = MakeButton(seg, $"Speed{s}", $"x{s}", new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -6), new Vector2(98, 56),
                () => GameManager.Instance?.SetSpeed(speed), 24);
            Segment(b);
            speedButtons.Add(new KeyValuePair<float, Image>(speed, b.GetComponent<Image>()));
            x += 105;
        }

        var cb = MakeButton(right, "CallBoat", "Appeler", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(124, 40),
            () => ExpeditionManager.Instance?.CallNextExpedition(), 17);
        WithIcon(cb, "boat", 24);
        callBoatButton = cb.gameObject;

        pausedLabel = Label(root, "PAUSE", 56, GoldColor, Vector2.zero, new Vector2(400, 80), TextAlignmentOptions.Center, true);
        var prt = pausedLabel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.62f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        pausedLabel.gameObject.SetActive(false);
    }

    private void BuildToolbar()
    {
        sidePanel = SidePanel.Create(root);

        hintPanel = Panel("Hint", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-160, 14), new Vector2(1060, 66));
        hintPanel.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.08f, 0.75f);
        hintPanel.GetComponent<Image>().raycastTarget = false;
        hintPanel.Find("Liseré").gameObject.SetActive(false);
        hintText = Label(hintPanel, "", 21, TextColor, new Vector2(20, -6), new Vector2(1020, 56), TextAlignmentOptions.Center);
        hintText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void BuildSelectionPanel()
    {
        selectionPanel = Panel("SelectionPanel", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 96), new Vector2(360, 306));
        selectionPanel.Find("Liseré").GetComponent<Image>().color = UIKit.TrimSoft;
        selectionText = Label(selectionPanel, "", 23, TextColor, new Vector2(16, -10), new Vector2(330, 60), TextAlignmentOptions.Left);
        selectionText.textWrappingMode = TextWrappingModes.Normal;

        patrolButton = MakeButton(selectionPanel, "Patrol", "Patrouille", new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -76), new Vector2(160, 50),
            () => MonsterSelector.Instance?.SetSelectedBehavior(GobelinAI.GoblinBehavior.Patrol), 22).gameObject;
        holdButton = MakeButton(selectionPanel, "Hold", "Tenir", new Vector2(0, 1), new Vector2(0, 1), new Vector2(186, -76), new Vector2(160, 50),
            () => MonsterSelector.Instance?.SetSelectedBehavior(GobelinAI.GoblinBehavior.Stay), 22).gameObject;
        possessButton = MakeButton(selectionPanel, "Possess", "Posséder", new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -132), new Vector2(160, 50),
            PossessSelected, 22).gameObject;
        possessButton.GetComponent<Image>().color = UIKit.Accent;
        MakeButton(selectionPanel, "All", "Tous", new Vector2(0, 1), new Vector2(0, 1), new Vector2(186, -132), new Vector2(160, 50),
            () => MonsterSelector.Instance?.SelectAllGoblins(), 22);
        var eb = MakeButton(selectionPanel, "Evolve", "Évoluer", new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -188), new Vector2(332, 50),
            EvolveSelected, 21);
        eb.GetComponent<Image>().color = new Color(0.55f, 0.42f, 0.12f, 1f);
        evolveButton = eb.gameObject;
        evolveLabel = eb.GetComponentInChildren<TextMeshProUGUI>();
        var gb = MakeButton(selectionPanel, "Guardian", "Nommer boss", new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -244), new Vector2(332, 50),
            () => { var s = MonsterSelector.Instance; if (s != null && s.SelectedGoblins.Count == 1) NameBossDialog.Open(s.SelectedGoblins[0]); }, 21);
        gb.GetComponent<Image>().color = new Color(0.6f, 0.45f, 0.12f, 1f);
        guardianButton = gb.gameObject;
        var tb = MakeButton(selectionPanel, "Talk", "Parler", new Vector2(0, 1), new Vector2(0, 1), new Vector2(186, -188), new Vector2(160, 50),
            () => { var s = MonsterSelector.Instance; if (s != null && s.SelectedGoblins.Count == 1) MonsterMind.For(s.SelectedGoblins[0]).Talk(); }, 21);
        tb.GetComponent<Image>().color = new Color(0.3f, 0.25f, 0.5f, 1f);
        talkButton = tb.gameObject;
    }

    private void BuildPossessionPanel()
    {
        possessionPanel = NewRect("PossessionPanel", root);
        Stretch(possessionPanel);

        var joyRt = NewRect("Joystick", possessionPanel);
        Place(joyRt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 60), new Vector2(300, 300));
        var bg = joyRt.gameObject.AddComponent<Image>();
        bg.sprite = SpriteUtil.Circle;
        bg.color = new Color(1f, 1f, 1f, 0.15f);
        var handle = NewRect("Handle", joyRt);
        Place(handle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 130));
        var hi = handle.gameObject.AddComponent<Image>();
        hi.sprite = SpriteUtil.Circle;
        hi.color = new Color(1f, 1f, 1f, 0.45f);
        hi.raycastTarget = false;
        joystick = joyRt.gameObject.AddComponent<VirtualJoystick>();
        joystick.handle = handle;
        joystick.radius = 110f;

        var atk = MakeButton(possessionPanel, "Attack", "Attaque", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 80), new Vector2(230, 230),
            () => PossessionController.Instance?.RequestAttack(), 34);
        var atkImg = atk.GetComponent<Image>();
        atkImg.sprite = SpriteUtil.Circle;
        atkImg.type = Image.Type.Simple;
        atkImg.color = new Color(0.85f, 0.3f, 0.3f, 0.75f);
        atk.transform.Find("Liseré")?.gameObject.SetActive(false);   // bouton rond : pas de cadre carré

        // compétence de la famille du monstre possédé (charge, bond, salve, rugissement)
        var sk = MakeButton(possessionPanel, "Skill", "Compétence", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-320, 80), new Vector2(170, 170),
            () => PossessionController.Instance?.RequestSkill(), 22);
        var skImg = sk.GetComponent<Image>();
        skImg.sprite = SpriteUtil.Circle;
        skImg.type = Image.Type.Simple;
        skImg.color = new Color(0.35f, 0.25f, 0.6f, 0.75f);
        sk.transform.Find("Liseré")?.gameObject.SetActive(false);
        skillLabel = sk.GetComponentInChildren<TextMeshProUGUI>();
        skillCd = NewRect("Recharge", (RectTransform)sk.transform);
        skillCd.anchorMin = Vector2.zero; skillCd.anchorMax = new Vector2(1, 0); skillCd.pivot = new Vector2(0.5f, 0);
        skillCd.offsetMin = skillCd.offsetMax = Vector2.zero;
        var cdImg = skillCd.gameObject.AddComponent<Image>(); cdImg.color = new Color(0f, 0f, 0f, 0.45f); cdImg.raycastTarget = false;

        var exitPanel = Panel("PossessInfo", possessionPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(560, 110));
        possessionText = Label(exitPanel, "", 22, TextColor, new Vector2(0, -6), new Vector2(560, 36), TextAlignmentOptions.Center, true);
        MakeButton(exitPanel, "Exit", PointerUtil.IsTouch ? "Retour au cristal" : "Retour au cristal (E)", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -46), new Vector2(360, 54), () => PossessionController.Instance?.Release(), 22);
    }

    private void BuildFeed()
    {
        feed = NewRect("Feed", root);
        Place(feed, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -318), new Vector2(560, 420));
        var layout = feed.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 8;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        feed.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
    }

    private void BuildDialogue()
    {
        dialogueRoot = NewRect("Dialogue", root);
        Stretch(dialogueRoot);
        var blocker = dialogueRoot.gameObject.AddComponent<Image>();
        blocker.color = new Color(0, 0, 0, 0.35f);
        var blockBtn = dialogueRoot.gameObject.AddComponent<Button>();
        blockBtn.transition = Selectable.Transition.None;
        blockBtn.onClick.AddListener(OnDialogueClicked);

        var box = Panel("Box", dialogueRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(1320, 270));
        box.GetComponent<Image>().color = new Color(UIKit.Panel.r, UIKit.Panel.g, UIKit.Panel.b, 0.97f);
        box.GetComponent<Image>().raycastTarget = false;
        speakerText = Label(box, "", 28, GoldColor, new Vector2(34, -18), new Vector2(800, 40), TextAlignmentOptions.Left);
        UIKit.StyleHead(speakerText);
        dialogueText = Label(box, "", 30, TextColor, new Vector2(34, -64), new Vector2(1250, 150), TextAlignmentOptions.TopLeft);
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        continueText = Label(box, "", 20, SubTextColor, new Vector2(-30, -226), new Vector2(500, 32), TextAlignmentOptions.Right);
        var crt = continueText.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(1, 1);
        dialogueRoot.gameObject.SetActive(false);
    }

    private void BuildGameOver()
    {
        gameOverPanel = NewRect("GameOver", root);
        Stretch(gameOverPanel);
        gameOverPanel.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.04f, 0.82f);
        var card = Panel("Card", gameOverPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1240, 760));
        card.GetComponent<Image>().raycastTarget = false;

        var title = Label(gameOverPanel, "Votre cristal s'est brisé", 64, DangerColor, Vector2.zero, new Vector2(1200, 90), TextAlignmentOptions.Center, true);
        UIKit.StyleTitle(title);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.72f);
        title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        title.rectTransform.anchoredPosition = Vector2.zero;

        gameOverStats = Label(gameOverPanel, "", 28, TextColor, Vector2.zero, new Vector2(1160, 420), TextAlignmentOptions.Center, true);
        gameOverStats.rectTransform.anchorMin = gameOverStats.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        gameOverStats.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        gameOverStats.rectTransform.anchoredPosition = Vector2.zero;
        gameOverStats.textWrappingMode = TextWrappingModes.Normal;

        MakeButton(gameOverPanel, "Restart", "Renaître", new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 90),
            () => GameManager.Instance?.Restart(), 34).GetComponent<Image>().color = ButtonActiveColor;
        gameOverPanel.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    // MISE À JOUR
    // ─────────────────────────────────────────────

    public static bool debugInput;

    private RectTransform bossBar, bossBarFill;
    private TextMeshProUGUI bossBarName;

    /// <summary>Jauge du boss d'étage nommé engagé dans un combat (mode Donjon).</summary>
    private void UpdateBossBar()
    {
        if (bossBar == null)
        {
            bossBar = Panel("BossBar", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(680, 70));
            bossBarName = Label(bossBar, "", 21, new Color(1f, 0.78f, 0.6f), new Vector2(0, -6), new Vector2(660, 28), TextAlignmentOptions.Center);
            var bg = NewRect("Jauge", bossBar);
            Place(bg, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(640, 20));
            var bgi = bg.gameObject.AddComponent<Image>();
            bgi.color = new Color(0.15f, 0.05f, 0.06f, 1f);
            bossBarFill = NewRect("Fill", bg);
            bossBarFill.anchorMin = Vector2.zero; bossBarFill.anchorMax = Vector2.one;
            bossBarFill.offsetMin = new Vector2(3, 3); bossBarFill.offsetMax = new Vector2(-3, -3);
            bossBarFill.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.22f, 0.2f);
            foreach (float mark in new[] { 0.66f, 0.33f })
            {
                var m = NewRect("Phase", bg);
                m.anchorMin = new Vector2(mark, 0); m.anchorMax = new Vector2(mark, 1); m.sizeDelta = new Vector2(3, 0);
                m.gameObject.AddComponent<Image>().color = new Color(1f, 0.9f, 0.7f, 0.7f);
            }
        }
        var b = CrawlerMode.Active ? null : CrawlerBoss.Fighting();
        bool show = b != null && (MainMenu.Instance == null || !MainMenu.Instance.InHome);
        if (bossBar.gameObject.activeSelf != show) bossBar.gameObject.SetActive(show);
        if (!show) return;
        bossBarName.text = $"{b.Title}  <size=75%><color=#C8BFA8>phase {b.Phase}/3</color></size>";
        bossBarFill.anchorMax = new Vector2(b.Health01, 1f);
    }

    private void Update()
    {
        UpdateBossBar();
        if (pendingToasts.Count > 0 && feed != null && feed.gameObject.activeInHierarchy)
        {
            var pend = new List<KeyValuePair<string, Color>>(pendingToasts);
            pendingToasts.Clear();
            foreach (var p in pend) AddToast(p.Key, p.Value);
        }
        if (debugInput && Pointer.current != null && (Pointer.current.press.wasPressedThisFrame || Pointer.current.press.wasReleasedThisFrame))
        {
            var es = EventSystem.current;
            var mod = es != null ? es.currentInputModule as InputSystemUIInputModule : null;
            Debug.Log($"[INPUT] {(Pointer.current.press.wasPressedThisFrame ? "DOWN" : "UP")} dev={Pointer.current.name} pos={Pointer.current.position.ReadValue()} overUI={PointerUtil.IsOverUI()} " +
                      $"esOver={es?.IsPointerOverGameObject()} sel={es?.currentSelectedGameObject?.name} module={mod != null} clickEnabled={mod?.leftClick?.action?.enabled} pointEnabled={mod?.point?.action?.enabled} focus={Application.isFocused}");
        }
        ApplySafeArea();
        UpdateToasts();
        UpdateDialogueTyping();

        var kb = Keyboard.current;
        if (dialogueRoot.gameObject.activeSelf && kb != null &&
            (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            OnDialogueClicked();

        if (panelsHidden || CrawlerMode.Active) { ApplyPanelsHidden(); return; }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.1f;
            Refresh();
        }
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        var mana = ManaSystem.Instance;
        var lvl = PlayerLevel.Instance;
        var rep = ReputationSystem.Instance;
        var exp = ExpeditionManager.Instance;
        var builder = DungeonBuilder.Instance;
        var core = FindCore();

        if (core != null)
        {
            SetBar(hpFill, core.HealthPercent01);
            hpText.text = $"{Mathf.CeilToInt(core.currentHealth)}/{Mathf.RoundToInt(core.maxHealth)}";
        }
        if (mana != null)
        {
            SetBar(manaFill, mana.Max > 0 ? (float)mana.Current / mana.Max : 0f);
            int upkeep = UpkeepSystem.Instance != null ? UpkeepSystem.Instance.DailyCost : 0;
            manaText.text = $"{mana.Current}  <size=80%><color=#B9AEFF>+{mana.CurrentRegen:0.0}/s</color></size>";
            if (upkeep > 0) xpUpkeep = $"   <color=#C9A0FF>Entretien -{upkeep}/jour</color>";
            else xpUpkeep = "";
        }
        if (lvl != null)
        {
            xpText.text = $"Niveau {lvl.currentLevel}   <size=85%>XP {lvl.currentXP}/{lvl.xpForNextLevel}{xpUpkeep}</size>";
            SetBar(xpFill, (float)lvl.currentXP / Mathf.Max(1, lvl.xpForNextLevel));
            float prestige = PlacedObject.TotalPrestige();
            string pres = prestige > 0f ? $"   ·   <color=#FFD966>Prestige {prestige:0}</color>" : "";
            countsText.text = $"Monstres {lvl.GetCurrentMonsterCount()}   ·   Pièges {lvl.GetCurrentTrapCount()}{pres}";
        }
        if (gm != null)
        {
            var lay = DungeonLayers.Instance;
            string layerLabel = lay != null && lay.layers.Count > 1 ? $"  ·  {lay.layers[lay.CurrentView].name}" : "";
            var ev = IslandEvents.Instance;
            string evLabel = ev != null && ev.Current != IslandEvent.None ? $"  ·  <color=#FFB36B>{ev.Name}</color>" : "";
            if (talentBadge != null) { int tp = Talents.Points; talentBadge.text = tp > 0 ? $"{tp} point{(tp > 1 ? "s" : "")}" : ""; }
            dayText.text = $"Jour {gm.CurrentDay}  ·  {Seasons.CurrentName}  ·  {gm.GetTimeOfDayLabel()}{layerLabel}{evLabel}";
            SetBar(dayFill, gm.DayProgress);
            pauseButtonImage.color = gm.UserPaused ? ButtonActiveColor : SegmentColor;
            foreach (var kv in speedButtons)
                kv.Value.color = !gm.UserPaused && Mathf.Approximately(gm.GameSpeed, kv.Key) ? ButtonActiveColor : SegmentColor;
            pausedLabel.gameObject.SetActive(gm.UserPaused && gm.State == GameManager.GameState.Playing);
        }
        if (rep != null && exp != null)
        {
            int tier = Mathf.Clamp(rep.CurrentTier, 0, exp.tiers.Length - 1);
            var guild = GuildSystem.Instance;
            if (guild != null && guild.stage != GuildSystem.Stage.Unknown) threatText.text = guild.StatusLine();
            else
            {
                string known = rep.dungeonDiscovered ? "" : "  <size=80%><color=#9A96B0>(inconnu du monde)</color></size>";
                threatText.text = $"Menace : {exp.tiers[tier].displayName}{known}";
            }
            callBoatButton.SetActive(!exp.ExpeditionActive && gm != null && gm.CurrentDay >= 2);
            if (exp.ExpeditionActive)
                expeditionText.text = $"<color=#FF9A8A>Expédition : {exp.ActiveCount} intrus ({exp.CountInside()} dans le donjon)</color>";
            else
                // Aucune annonce : un bateau peut surgir à tout moment (la peur doit rester constante)
                expeditionText.text = $"<color=#9A96B0>La mer est calme... pour l'instant</color>   ·   Renommée {Mathf.RoundToInt(rep.renown)}";
        }

        var colony = SettlementManager.Instance;
        // Les menus de la colonie n'apparaissent qu'une fois le campement fondé
        bool hasColony = colony != null && colony.enabled && colony.stage > 0;
        if (colonyButton.gameObject.activeSelf != hasColony)
        {
            colonyButton.gameObject.SetActive(hasColony);
            colonyText.gameObject.SetActive(hasColony);
            ((RectTransform)viewButton.transform).anchoredPosition = new Vector2(588, hasColony ? -158 : -92);
            if (!hasColony && ColonyWindow.Instance != null && ColonyWindow.Instance.IsOpen) ColonyWindow.Instance.Toggle();
        }
        if (hasColony)
        {
            string mood = colony.morale < 25 ? "<color=#FF8A7A>terrifiée</color>" : colony.morale < 45 ? "<color=#FFC266>inquiète</color>" : "sereine";
            colonyText.text = $"Colonie : {colony.StageName} · {colony.Population}/{colony.Capacity} hab. · {mood} · bois {colony.wood} · pierre {colony.stone} · vivres {colony.food}";
        }
        else colonyText.text = "<color=#6F6A80>Aucune colonie humaine sur l'île</color>";
        var relief = MountainRelief.Instance;
        viewButton.gameObject.SetActive(relief != null);
        if (relief != null)
        {
            var vl = viewButton.GetComponentInChildren<TextMeshProUGUI>();
            if (vl != null) vl.text = relief.DungeonView ? "Vue île" : "Vue donjon";
        }
        var relSys = RelationSystem.Instance;
        if (colonyBadge != null)
        {
            string badge = "";
            if (relSys != null && relSys.PunitivePending) badge = "<color=#FF7A6A>vengeance !</color>";
            else if (relSys != null && relSys.truce) badge = "<color=#9CF0A8>trêve</color>";
            else if (RaidSystem.Instance != null && RaidSystem.Instance.Active) badge = "<color=#FFC266>raid...</color>";
            else if (gm != null && gm.IsNight && colony != null && colony.stage > 0) badge = "raid possible";
            colonyBadge.text = badge;
        }

        if (builder != null)
        {
            var mode = builder.GetCurrentMode();
            string hint = builder.GetModeHint(mode);
            if (mode == DungeonBuilder.BuildMode.Digging && builder.QueuedDigCount > 0)
                hint += $"\n<color=#FFC266>{builder.QueuedDigCount} case(s) en attente</color>";
            hintText.text = hint;
        }

        UpdateSelectionPanel();

        var pc = PossessionController.Instance;
        if (pc != null && pc.IsPossessing && pc.Possessed != null)
            possessionText.text = $"{pc.Possessed.gobelinName}  —  PV {Mathf.CeilToInt(pc.Possessed.currentHealth)}/{Mathf.RoundToInt(pc.Possessed.maxHealth)}";
    }

    private string xpUpkeep = "";
    private Button colonyButton, viewButton;
    private TextMeshProUGUI colonyBadge;
    private DungeonCore cachedCore;
    private DungeonCore FindCore()
    {
        if (cachedCore == null) cachedCore = FindFirstObjectByType<DungeonCore>();
        return cachedCore;
    }

    // ── Barre des sorts du Noyau ──
    private RectTransform spellBar;
    private readonly List<(CoreSpells.Spell s, Button b, RectTransform cd, TextMeshProUGUI label, Image icon)> spellButtons =
        new List<(CoreSpells.Spell, Button, RectTransform, TextMeshProUGUI, Image)>();

    private void BuildSpellBar()
    {
        spellBar = Place(NewRect("Sorts", root), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-160, 90), new Vector2(4 * 118 + 3 * 10, 100));
        float x = 0;
        foreach (var def in CoreSpells.Defs())
        {
            string id = def.id;
            var b = MakeButton(spellBar, "Sort_" + id, "", new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 0), new Vector2(118, 100),
                () => { var cs = CoreSpells.Instance; if (cs != null) cs.Cast(cs.spells.Find(z => z.id == id)); }, 16);
            var rt = (RectTransform)b.transform;
            var ic = UIKit.Icon(rt, def.icon, new Vector2(0, -10), 38, new Vector2(0.5f, 1));
            var name = Label(rt, def.name, 16, TextColor, new Vector2(0, -52), new Vector2(118, 22), TextAlignmentOptions.Center, true);
            UIKit.StyleHead(name);
            var label = Label(rt, "", 15, ManaColor, new Vector2(0, -74), new Vector2(118, 20), TextAlignmentOptions.Center, true);
            var cd = NewRect("Recharge", rt);
            cd.anchorMin = Vector2.zero; cd.anchorMax = new Vector2(1, 0); cd.pivot = new Vector2(0.5f, 0);
            cd.offsetMin = new Vector2(4, 4); cd.offsetMax = new Vector2(-4, 4);
            var ci = cd.gameObject.AddComponent<Image>(); ci.color = new Color(0f, 0f, 0f, 0.55f); ci.raycastTarget = false;
            spellButtons.Add((def, b, cd, label, ic));
            x += 128;
        }
    }

    private void UpdateSpellBar(bool possessing)
    {
        var cs = CoreSpells.Instance;
        spellBar.gameObject.SetActive(cs != null && !possessing);
        if (cs == null) return;
        foreach (var (def, b, cd, label, icon) in spellButtons)
        {
            var s = cs.spells.Find(z => z.id == def.id);
            bool ok = cs.CanCast(s, out string why);
            b.interactable = ok;
            icon.color = CoreSpells.Level >= s.level ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            label.text = ok ? $"{CoreSpells.CostOf(s)} mana" : why;
            label.color = ok ? ManaColor : SubTextColor;
            cd.anchorMax = new Vector2(1f, cs.Cooldown01(s));
        }
    }

    private void UpdateSelectionPanel()
    {
        bool possessing = PossessionController.Instance != null && PossessionController.Instance.IsPossessing;
        UpdateSpellBar(possessing);
        var pg = possessing ? PossessionController.Instance.Possessed : null;
        if (pg != null && skillLabel != null)
        {
            skillLabel.text = pg.SkillName;
            skillCd.anchorMax = new Vector2(1f, pg.SkillCooldown01);
        }
        bool selectMode = DungeonBuilder.Instance == null || DungeonBuilder.Instance.GetCurrentMode() == DungeonBuilder.BuildMode.None;
        selectionPanel.gameObject.SetActive(selectMode && !possessing);
        if (!selectionPanel.gameObject.activeSelf) return;

        var sel = MonsterSelector.Instance;
        int n = sel != null ? sel.SelectedGoblins.Count : 0;
        bool anchor = sel != null && sel.SelectedAnchor != null;
        MonsterUnit unit = n == 1 ? sel.SelectedGoblins[0].GetComponent<MonsterUnit>() : null;
        if (n == 1)
        {
            var g = sel.SelectedGoblins[0];
            string stage = unit != null && !unit.summoned ? $"  <size=80%><color=#FFD966>{(unit.stage == 1 ? "I" : unit.stage == 2 ? "II" : "III")}</color></size>" : "";
            string xp = unit != null && !unit.IsFinalForm && !unit.summoned ? $"\n<size=78%><color=#B9B3CC>PV {Mathf.CeilToInt(g.currentHealth)}/{Mathf.RoundToInt(g.maxHealth)} · XP {unit.xp}/{unit.xpToEvolve} · entretien {Mathf.RoundToInt(unit.upkeepPerDay * UpkeepSystem.DayScale * GameDifficulty.Upkeep)}/j</color></size>"
                      : $"\n<size=78%><color=#B9B3CC>PV {Mathf.CeilToInt(g.currentHealth)}/{Mathf.RoundToInt(g.maxHealth)}</color></size>";
            string lifeText = g.Life != null ? $"\n<size=74%>{g.Life.Describe()}</size>" : "";
            selectionText.text = g.gobelinName + stage + xp + lifeText;
        }
        else if (n > 0) selectionText.text = $"{n} monstres sélectionnés";
        else if (anchor) selectionText.text = "Ancre sélectionnée :\n<size=85%>touchez le sol pour la déplacer</size>";
        else selectionText.text = "<color=#9A96B0>Aucun monstre sélectionné</color>";

        patrolButton.SetActive(n > 0);
        holdButton.SetActive(n > 0);
        possessButton.SetActive(n == 1);
        bool canShowEvolve = unit != null && !unit.IsFinalForm && !unit.summoned;
        evolveButton.SetActive(canShowEvolve);
        var mind = n == 1 ? sel.SelectedGoblins[0].GetComponent<MonsterMind>() : null;
        bool nameable = n == 1 && unit != null && !unit.summoned && !CrawlerMode.Active;
        guardianButton.SetActive(nameable && (mind == null || !mind.IsGuardian));
        if (guardianButton.activeSelf)
        {
            bool ok = MonsterMind.CanPromote(sel.SelectedGoblins[0], out string why);
            guardianButton.GetComponent<Image>().color = ok ? new Color(0.6f, 0.45f, 0.12f, 1f) : new Color(0.25f, 0.22f, 0.18f, 1f);
            guardianButton.GetComponentInChildren<TextMeshProUGUI>().text = ok ? $"Nommer boss d'étage ({FloorBoss.Cost(sel.SelectedGoblins[0])} mana)" : $"<size=80%>Boss : {why}</size>";
        }
        talkButton.SetActive(n == 1 && MonsterMind.IsIntelligent(unit));
        if (canShowEvolve)
        {
            bool ok = unit.CanEvolveNow(out string why);
            string next = unit.nextStage != null ? unit.nextStage.GetComponent<MonsterUnit>()?.familyName ?? unit.nextStage.name : "";
            evolveLabel.text = ok ? $"Évoluer en {next} ({unit.EffectiveEvolveCost} mana)" : $"<color=#C8BFA8>Évolution : {why}</color>";
            evolveButton.GetComponent<Image>().color = ok ? new Color(0.62f, 0.46f, 0.1f, 1f) : new Color(0.25f, 0.22f, 0.18f, 1f);
        }
    }

    private void EvolveSelected()
    {
        var sel = MonsterSelector.Instance;
        if (sel == null || sel.SelectedGoblins.Count != 1) return;
        var unit = sel.SelectedGoblins[0].GetComponent<MonsterUnit>();
        unit?.Evolve();
    }

    private void PossessSelected()
    {
        var sel = MonsterSelector.Instance;
        if (sel == null || sel.SelectedGoblins.Count == 0) return;
        PossessionController.Instance?.Possess(sel.SelectedGoblins[0]);
    }

    private void OnPossessionChanged(bool possessing)
    {
        sidePanel.SetVisible(!possessing);
        hintPanel.gameObject.SetActive(!possessing);
        possessionPanel.gameObject.SetActive(possessing);
        // Le joystick n'est utile qu'en tactile
        joystick.gameObject.SetActive(possessing && (PointerUtil.IsTouch || Touchscreen.current != null));
    }

    // ─────────────────────────────────────────────
    // MESSAGES
    // ─────────────────────────────────────────────

    private void AddToast(string message, Color color)
    {
        // Pas de doublon consécutif
        if (toasts.Count > 0)
        {
            var lastText = toasts[toasts.Count - 1].rt.GetComponentInChildren<TextMeshProUGUI>();
            if (lastText != null && lastText.text == message)
            {
                toasts[toasts.Count - 1].life = 4.5f;
                return;
            }
        }

        var rt = NewRect("Toast", feed);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = SpriteUtil.RoundedUI;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.07f, 0.06f, 0.11f, 0.85f);
        img.raycastTarget = false;

        var trt = NewRect("Text", rt);
        Stretch(trt);
        trt.offsetMin = new Vector2(18, 8);
        trt.offsetMax = new Vector2(-18, -8);
        var t = trt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = message;
        t.fontSize = 22;
        t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;

        // Hauteur calculée selon le texte (largeur du fil - marges)
        float h = t.GetPreferredValues(message, 520f - 36f, 0f).y + 16f;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = Mathf.Max(44f, h);
        le.minHeight = le.preferredHeight;

        var group = rt.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        toasts.Add(new Toaster { rt = rt, group = group, life = 4.5f });

        while (toasts.Count > 6)
        {
            Destroy(toasts[0].rt.gameObject);
            toasts.RemoveAt(0);
        }
    }

    private void UpdateToasts()
    {
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var t = toasts[i];
            t.life -= Time.unscaledDeltaTime;
            t.group.alpha = Mathf.Clamp01(t.life / 0.6f);
            if (t.life <= 0f)
            {
                Destroy(t.rt.gameObject);
                toasts.RemoveAt(i);
            }
        }
    }

    // ─────────────────────────────────────────────
    // DIALOGUES
    // ─────────────────────────────────────────────

    public void ShowDialogue(string speaker, string text, bool isLast)
    {
        dialogueRoot.gameObject.SetActive(true);
        dialogueRoot.SetAsLastSibling();
        speakerText.text = string.IsNullOrEmpty(speaker) ? "" : speaker;
        // Les astuces de jeu se distinguent des personnages
        speakerText.color = speaker == StoryManager.SystemName ? new Color(0.55f, 0.85f, 1f) : speaker == StoryManager.SelfName ? GoldColor : new Color(0.95f, 0.75f, 0.55f);
        dialogueText.text = speaker == StoryManager.SelfName ? $"<i>« {text} »</i>" : text;
        dialogueText.fontStyle = string.IsNullOrEmpty(speaker) ? FontStyles.Italic : FontStyles.Normal;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        typeProgress = 0f;
        typing = true;
        string action = PointerUtil.IsTouch ? "Touchez" : "Cliquez / Espace";
        continueText.text = isLast ? $"{action} pour continuer" : $"{action} >";
    }

    public void HideDialogue()
    {
        dialogueRoot.gameObject.SetActive(false);
        PointerUtil.BlockWorldInput(0.3f);   // le clic qui ferme le dialogue ne doit pas agir sur le jeu
    }

    private void UpdateDialogueTyping()
    {
        if (!typing) return;
        typeProgress += Time.unscaledDeltaTime * 55f;
        int total = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(typeProgress));
        if (typeProgress >= total) typing = false;
    }

    private void OnDialogueClicked()
    {
        if (typing)
        {
            typing = false;
            dialogueText.maxVisibleCharacters = 99999;
            return;
        }
        StoryManager.Instance?.Advance();
    }

    // ─────────────────────────────────────────────
    // FIN DE PARTIE
    // ─────────────────────────────────────────────

    private void ShowGameOver()
    {
        var gm = GameManager.Instance;
        int lvl = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
        Achievements.Instance?.CheckAll();
        var p = ProfileStore.Active;
        if (p != null) { p.bestDay = Mathf.Max(p.bestDay, gm.CurrentDay); p.totalKills += gm.statKills; ProfileStore.Save(); }
        var guild = GuildSystem.Instance;
        string rank = guild != null && guild.officialRank >= 0 ? GuildSystem.RankTag(guild.officialRank) : "non classé";
        var colony = SettlementManager.Instance;
        string col = colony != null && colony.stage > 0 ? colony.StageName : "aucune";
        int nem = NemesisSystem.Instance != null ? NemesisSystem.Instance.Defeated : 0;
        string ach = "";
        if (Achievements.Instance != null && Achievements.Instance.unlockedThisGame.Count > 0)
        {
            ach = "\n\n<color=#FFD966>Succès débloqués :</color> ";
            foreach (var a in Achievements.Instance.unlockedThisGame) ach += a.name + "   ";
        }
        gameOverStats.text =
            "<i>Une fissure. Puis mille. Votre conscience se disperse pour la seconde fois...</i>\n\n" +
            $"Jours survécus : <b>{gm.CurrentDay}</b>     Niveau du Noyau : <b>{lvl}</b>     Difficulté : <b>{GameDifficulty.Name}</b>\n" +
            $"Intrus absorbés : <b>{gm.statKills}</b>     Fuyards : <b>{gm.statFled}</b>     Expéditions : <b>{gm.statExpeditions}</b>\n" +
            $"Rang du donjon : <b>{rank}</b>     Héros vaincus : <b>{gm.statHeroKills}</b>     Némésis abattues : <b>{nem}</b>\n" +
            $"Évolutions : <b>{gm.statEvolutions}</b>     Colonie : <b>{col}</b>" + ach;
        dialogueRoot.gameObject.SetActive(false);
        gameOverPanel.gameObject.SetActive(true);
        gameOverPanel.SetAsLastSibling();
    }

    // ─────────────────────────────────────────────
    // FABRIQUE D'ÉLÉMENTS UI
    // ─────────────────────────────────────────────

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = Place(NewRect(name, parent), anchor, pivot, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = SpriteUtil.RoundedUI;
        img.type = Image.Type.Sliced;
        img.color = PanelColor;
        UIKit.Frame(rt);
        return rt;
    }

    /// <summary>Bouton d'un contrôle segmenté : pas de cadre propre, fond transparent tant qu'il n'est pas actif.</summary>
    private static void Segment(Button b)
    {
        var trim = b.transform.Find("Liseré");
        if (trim != null) trim.gameObject.SetActive(false);
        b.GetComponent<Image>().color = SegmentColor;
    }

    /// <summary>Ajoute une icône pixel à gauche du libellé d'un bouton.</summary>
    private static Button WithIcon(Button b, string icon, float size = 30)
    {
        var rt = (RectTransform)b.transform;
        UIKit.Icon(rt, icon, new Vector2(12, 0), size, new Vector2(0, 0.5f));
        var t = rt.Find("Label") as RectTransform;
        if (t != null) t.offsetMin = new Vector2(12 + size, t.offsetMin.y);
        return b;
    }

    /// <summary>Texte positionné depuis le coin haut-gauche du parent (ou centré horizontalement).</summary>
    private static TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
        TextAlignmentOptions align, bool centerX = false)
    {
        var rt = NewRect("Text", parent);
        rt.anchorMin = rt.anchorMax = centerX ? new Vector2(0.5f, 1) : new Vector2(0, 1);
        rt.pivot = centerX ? new Vector2(0.5f, 1) : new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    private static void Bar(RectTransform parent, Vector2 pos, Vector2 size, Color color, out RectTransform fill)
    {
        var bg = Place(NewRect("Bar", parent), new Vector2(0, 1), new Vector2(0, 1), pos, size);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.03f, 0.025f, 0.05f, 0.9f);
        bgImg.raycastTarget = false;
        if (size.y >= 16) { bgImg.sprite = SpriteUtil.RoundedUI; bgImg.type = Image.Type.Sliced; }
        float inset = size.y >= 16 ? 4 : 2;
        fill = NewRect("Fill", bg);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(1, 1);
        fill.offsetMin = new Vector2(inset, inset);
        fill.offsetMax = new Vector2(-inset, -inset);
        var fImg = fill.gameObject.AddComponent<Image>();
        fImg.color = color;
        fImg.raycastTarget = false;
        if (size.y >= 16)
        {
            // reflet d'un « pixel » en haut de la jauge
            var hl = NewRect("Reflet", fill);
            hl.anchorMin = new Vector2(0, 1); hl.anchorMax = new Vector2(1, 1);
            hl.pivot = new Vector2(0.5f, 1); hl.anchoredPosition = Vector2.zero; hl.sizeDelta = new Vector2(0, 3);
            var hImg = hl.gameObject.AddComponent<Image>();
            hImg.color = new Color(1f, 1f, 1f, 0.28f);
            hImg.raycastTarget = false;
        }
    }

    private static void SetBar(RectTransform fill, float v)
    {
        if (fill == null) return;
        fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        UnityAction onClick, float fontSize)
    {
        var rt = Place(NewRect(name, parent), anchor, pivot, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = SpriteUtil.RoundedUI;
        img.type = Image.Type.Sliced;
        img.color = ButtonColor;
        UIKit.Frame(rt, UIKit.TrimSoft);
        var btn = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => Sfx.Play("ui_click", null, 0.6f, 0.04f));
        btn.onClick.AddListener(onClick);

        if (!string.IsNullOrEmpty(label))
        {
            var t = NewRect("Label", rt).gameObject.AddComponent<TextMeshProUGUI>();
            Stretch(t.rectTransform);
            t.text = label;
            t.fontSize = fontSize;
            t.color = TextColor;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            if (fontSize >= 20) UIKit.StyleHead(t);
        }
        return btn;
    }
}

/// <summary>Joystick virtuel pour le mode possession sur mobile.</summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform handle;
    public float radius = 110f;
    /// <summary>Dernière valeur du joystick (mode Aventurier).</summary>
    public static Vector2 Value;

    private RectTransform rt;

    private void Awake() => rt = (RectTransform)transform;

    public void OnPointerDown(PointerEventData e) => OnDrag(e);

    public void OnDrag(PointerEventData e)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local)) return;
        // le point local est relatif au pivot : on le ramène au centre visuel du joystick (là où repose la poignée)
        local -= rt.rect.center;
        float r = Mathf.Min(radius, rt.rect.width * 0.5f);
        Vector2 v = Vector2.ClampMagnitude(local, r);
        if (v.magnitude < r * 0.12f) v = Vector2.zero;   // zone morte au centre
        handle.anchoredPosition = v;
        Value = v / r;
        if (PossessionController.Instance != null) PossessionController.Instance.virtualStick = v / r;
    }

    public void OnPointerUp(PointerEventData e)
    {
        handle.anchoredPosition = Vector2.zero;
        Value = Vector2.zero;
        if (PossessionController.Instance != null) PossessionController.Instance.virtualStick = Vector2.zero;
    }

    private void OnDisable()
    {
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        Value = Vector2.zero;
        if (PossessionController.Instance != null) PossessionController.Instance.virtualStick = Vector2.zero;
    }
}
