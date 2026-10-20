using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Écran d'accueil de l'application et menu pause en jeu.
///   Accueil : profil joueur, Continuer, Nouvelle partie, Charger, Modes de jeu (à venir), Options.
///   En jeu (bouton Menu / Échap) : Reprendre, Sauvegarder, Charger, Options, Retour à l'accueil.
/// L'île vit en arrière-plan (caméra qui survole lentement) sans que la partie n'avance.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public static MainMenu Instance { get; private set; }

    public enum Boot { Home, NewGame, Load }
    /// <summary>Ce que fait la scène à son prochain démarrage.</summary>
    public static Boot StartMode = Boot.Home;
    /// <summary>L'introduction attend l'accueil ou le chargement.</summary>
    public static bool HoldIntro { get; private set; }
    /// <summary>Accueil affiché : le temps de jeu est figé (jour, mana).</summary>
    public static bool Frozen => Instance != null && Instance.InHome;

    public const string Version = "v0.6";

    public bool InHome { get; private set; }
    public bool InPause { get; private set; }

    private Canvas canvas;
    private RectTransform root, home, pause, modal;
    private TextMeshProUGUI profileName, continueSub;
    private Image profileBadge, orbGlow, orbCore;
    private TextMeshProUGUI profileInitial;
    private Button continueBtn;
    private float camT;
    private DungeonBuilder.BuildMode lastMode;
    private bool pausePushed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        StartMode = Boot.Home;
        HoldIntro = false;
        SaveSystem.PendingLoad = null;
    }

    private void Awake()
    {
        Instance = this;
        var mode = StartMode;
        StartMode = Boot.Home;   // consommé : un rechargement « normal » revient à l'accueil
        HoldIntro = mode != Boot.NewGame;
        InHome = mode == Boot.Home;
        BuildCanvas();
        BuildHome();
        home.gameObject.SetActive(InHome);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private void Start()
    {
        if (!InHome) return;
        GameHUD.Instance?.SetHudVisible(false);
        if (CameraController.Instance != null) CameraController.Instance.cinematic = true;
        var cam = Camera.main;
        if (cam != null) cam.orthographicSize = 15f;
        RefreshHome();
        if (!ProfileStore.HasProfile) ShowProfileCreate(true);
    }

    private void Update()
    {
        if (InHome)
        {
            // survol lent de l'île
            camT += Time.unscaledDeltaTime;
            var cam = Camera.main;
            if (cam != null)
            {
                var p = new Vector3(Mathf.Sin(camT * 0.045f) * 20f, Mathf.Cos(camT * 0.031f) * 14f - 4f, cam.transform.position.z);
                cam.transform.position = p;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f);
            LayoutHomeButtons();
            if (orbGlow != null) { orbGlow.color = new Color(0.55f, 0.45f, 1f, 0.12f + 0.14f * pulse); orbGlow.rectTransform.localScale = Vector3.one * (1f + 0.08f * pulse); }
            if (orbCore != null) orbCore.rectTransform.localScale = Vector3.one * (1f + 0.03f * pulse);
            return;
        }

        // Échap en jeu : ouvre / ferme le menu (si aucun outil n'était actif)
        var kb = Keyboard.current;
        var builder = DungeonBuilder.Instance;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && modal == null)
        {
            bool dialog = StoryManager.Instance != null && StoryManager.Instance.IsShowing;
            bool cine = CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying;
            if (InPause) ClosePause();
            else if (!dialog && !cine && lastMode == DungeonBuilder.BuildMode.None) OpenPause();
        }
        lastMode = builder != null ? builder.GetCurrentMode() : DungeonBuilder.BuildMode.None;
    }

    // ─────────────────────────────────────────────
    // CONSTRUCTION
    // ─────────────────────────────────────────────

    private void BuildCanvas()
    {
        var go = new GameObject("MainMenu_Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();
        root = UIKit.Stretch(UIKit.Rect("Root", go.transform));
        SafeArea.Apply(root);
    }

    private void BuildHome()
    {
        home = UIKit.Stretch(UIKit.Rect("Home", root));
        home.gameObject.AddComponent<HudScaler>();   // colonne du menu et carte du profil suivent la taille d'interface
        // l'île en fond, assombrie ; un grand panneau dégradé à gauche porte le titre et le menu
        var veil = UIKit.Stretch(UIKit.Rect("Veil", home));
        UIKit.Image(veil, new Color(0.02f, 0.015f, 0.05f, 0.3f), false);
        var shade = UIKit.Rect("LeftShade", home);
        shade.anchorMin = Vector2.zero; shade.anchorMax = new Vector2(0f, 1f);
        shade.pivot = new Vector2(0f, 0.5f);
        shade.sizeDelta = new Vector2(1180f, 0f);
        shade.anchoredPosition = Vector2.zero;
        var sh = UIKit.Image(shade, new Color(0.02f, 0.012f, 0.05f, 0.9f), false, false);
        sh.sprite = UIKit.HGradient;

        // colonne centrale du panneau (tout est placé depuis le haut)
        var colRoot = UIKit.Rect("Column", home);
        colRoot.anchorMin = new Vector2(0f, 0f); colRoot.anchorMax = new Vector2(0f, 1f);
        colRoot.pivot = new Vector2(0.5f, 1f);
        colRoot.sizeDelta = new Vector2(640f, 0f);
        colRoot.anchoredPosition = new Vector2(400f, 0f);

        // Orbe
        var orb = UIKit.Place(UIKit.Rect("Orb", colRoot), new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(104, 104));
        orbGlow = UIKit.Image(UIKit.Stretch(UIKit.Rect("Glow", orb), -34, -34, -34, -34), new Color(0.55f, 0.45f, 1f, 0.25f), false, false);
        orbGlow.sprite = SpriteUtil.Circle;
        var coreObj = FindFirstObjectByType<DungeonCore>();
        var coreSprite = coreObj != null && coreObj.spriteRenderer != null ? coreObj.spriteRenderer.sprite : null;
        int pad = coreSprite != null ? 0 : 26;
        orbCore = UIKit.Image(UIKit.Stretch(UIKit.Rect("Core", orb), pad, pad, pad, pad), coreSprite != null ? Color.white : new Color(0.7f, 0.85f, 1f, 1f), false, false);
        orbCore.sprite = coreSprite != null ? coreSprite : SpriteUtil.Circle;
        orbCore.preserveAspect = true;

        // Titre
        var title = UIKit.LabelAt(colRoot, GameTitle, 86, new Color(1f, 0.84f, 0.45f), new Vector2(0, -168), new Vector2(760, 120), TextAlignmentOptions.Center, new Vector2(0.5f, 1f));
        UIKit.StyleTitle(title);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true; title.fontSizeMin = 48; title.fontSizeMax = 86;
        // filet orné : ──── ◆ ────
        var gold = new Color(0.85f, 0.66f, 0.32f, 0.9f);
        foreach (float dir in new[] { -1f, 1f })
        {
            var line = UIKit.Image(UIKit.Place(UIKit.Rect("Filet", colRoot), new Vector2(0.5f, 1f), new Vector2(dir * 130f, -300), new Vector2(200, 2)), gold, false, false);
            line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
        var gem = UIKit.Image(UIKit.Place(UIKit.Rect("Gemme", colRoot), new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(16, 16)), gold, false, false);
        gem.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        gem.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        var sub = UIKit.LabelAt(colRoot, GameSubtitle, 30, new Color(0.8f, 0.76f, 0.96f), new Vector2(0, -318), new Vector2(700, 44), TextAlignmentOptions.Center, new Vector2(0.5f, 1f));
        UIKit.StyleHead(sub);
        sub.fontStyle = FontStyles.Italic;

        // Boutons : leur hauteur s'adapte à la place disponible (rien ne dépasse en bas, même sur téléphone)
        var labels = new System.Collections.Generic.List<(string, UnityEngine.Events.UnityAction, Color?)>
        {
            ("Nouvelle partie", () => NewGameClicked(), null), ("Charger une partie", () => ShowSlots(false), null),
            ("Modes de jeu", ShowModes, null), ("Succès", ShowAchievements, null), ("Options", ShowOptions, null),
        };
#if !UNITY_EDITOR && !UNITY_IOS
        labels.Add(("Quitter", Application.Quit, new Color(0.22f, 0.1f, 0.13f, 0.92f)));
#endif
        var col = UIKit.Rect("Buttons", colRoot);
        col.anchorMin = new Vector2(0.5f, 0f); col.anchorMax = new Vector2(0.5f, 1f);
        col.pivot = new Vector2(0.5f, 1f);
        col.offsetMin = new Vector2(-260f, 70f);   // marge du bas (pied de page)
        col.offsetMax = new Vector2(260f, -392f);  // sous le sous-titre
        menuButtons = col;
        menuLabels = labels;

        // Profil (haut droite)
        var card = UIKit.PanelBox("Profile", home, new Vector2(1, 1), new Vector2(-28, -28), new Vector2(420, 110), new Color(0.07f, 0.06f, 0.11f, 0.88f));
        profileBadge = UIKit.Image(UIKit.Place(UIKit.Rect("Badge", card), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(74, 74)), UIKit.Accent, false, false);
        profileBadge.sprite = SpriteUtil.Circle;
        profileInitial = UIKit.Label(profileBadge.transform, "", 40, Color.white);
        UIKit.Stretch(profileInitial.rectTransform);
        profileInitial.fontStyle = FontStyles.Bold;
        UIKit.LabelAt(card, "Profil", 18, UIKit.Sub, new Vector2(108, -14), new Vector2(290, 26));
        profileName = UIKit.LabelAt(card, "", 30, UIKit.Text, new Vector2(108, -38), new Vector2(290, 40));
        profileName.textWrappingMode = TextWrappingModes.NoWrap;
        profileName.overflowMode = TextOverflowModes.Ellipsis;
        var change = UIKit.BtnAt(card, "Changer", ShowProfiles, new Vector2(1, 0), new Vector2(-12, 10), new Vector2(128, 38), 18);
        change.GetComponent<Image>().color = new Color(0.22f, 0.19f, 0.33f, 1f);

        var foot = UIKit.LabelAt(home, $"{Version}  ·  Profils enregistrés sur cet appareil", 18, new Color(0.55f, 0.52f, 0.66f), new Vector2(24, 18), new Vector2(900, 30), TextAlignmentOptions.BottomLeft, new Vector2(0, 0));
        foot.rectTransform.pivot = new Vector2(0, 0);
        LayoutHomeButtons();
    }

    /// <summary>Titre et sous-titre du jeu (écran d'accueil).</summary>
    public const string GameTitle = "Dungeon Core";
    public const string GameSubtitle = "L'éveil du Noyau";

    private RectTransform menuButtons;
    private System.Collections.Generic.List<(string, UnityEngine.Events.UnityAction, Color?)> menuLabels;
    private float laidOutHeight = -1f;

    /// <summary>Range les boutons dans la hauteur disponible (recalculé si l'écran change).</summary>
    private void LayoutHomeButtons()
    {
        if (menuButtons == null) return;
        float avail = menuButtons.rect.height;
        if (avail <= 0f) avail = continueBtn == null ? 1080f - 462f : laidOutHeight;   // mise en page pas encore calculée
        if (Mathf.Abs(avail - laidOutHeight) < 1f) return;
        laidOutHeight = avail;
        UIKit.Clear(menuButtons);
        int n = menuLabels.Count + 1;
        const float gap = 12f;
        float h = Mathf.Clamp((avail - gap * n) / (n + 0.75f), 28f, 70f);   // la colonne est agrandie par la taille d'interface : on peut descendre bas
        float y = 0f;
        float hc = h * 1.75f;   // le bouton Continuer porte deux lignes (titre + date de la partie)
        continueBtn = HomeButton(menuButtons, "Continuer", () => ContinueGame(), ref y, UIKit.Accent, hc, gap);
        continueSub = UIKit.Label(continueBtn.transform, "", Mathf.Clamp(h * 0.36f, 12f, 18f), new Color(0.88f, 0.85f, 1f));
        UIKit.Place(continueSub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, hc * 0.1f), new Vector2(500, hc * 0.38f));
        continueSub.rectTransform.pivot = new Vector2(0.5f, 0f);
        var ct = continueBtn.transform.Find("Text") as RectTransform;
        if (ct != null) ct.offsetMin = new Vector2(8, hc * 0.42f);
        foreach (var (label, act, color) in menuLabels) HomeButton(menuButtons, label, act, ref y, color, h, gap);
        if (profileName != null) RefreshHome();
    }

    private Button HomeButton(RectTransform parent, string label, UnityEngine.Events.UnityAction a, ref float y, Color? c = null, float h = 72, float gap = 14f)
    {
        var b = UIKit.BtnAt(parent, label, a, new Vector2(0.5f, 1f), new Vector2(0, -y), new Vector2(500, h), Mathf.Min(30f, h * 0.46f), c ?? new Color(0.1f, 0.08f, 0.16f, 0.86f));
        ((RectTransform)b.transform).pivot = new Vector2(0.5f, 1f);
        var t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) { UIKit.StyleHead(t); t.characterSpacing = 2f; }
        // liseré doré à gauche
        var edge = UIKit.Image(UIKit.Rect("Liseré", (RectTransform)b.transform), new Color(0.85f, 0.66f, 0.32f, 0.85f), false, false);
        edge.rectTransform.anchorMin = new Vector2(0, 0.18f); edge.rectTransform.anchorMax = new Vector2(0, 0.82f);
        edge.rectTransform.pivot = new Vector2(0, 0.5f);
        edge.rectTransform.sizeDelta = new Vector2(4f, 0f);
        edge.rectTransform.anchoredPosition = new Vector2(10f, 0f);
        y += h + gap;
        return b;
    }

    private void RefreshHome()
    {
        var p = ProfileStore.Active;
        profileName.text = p != null ? p.name : "Aucun profil";
        profileInitial.text = p != null && p.name.Length > 0 ? p.name.Substring(0, 1).ToUpperInvariant() : "?";
        profileBadge.color = p != null ? ProfileStore.Colors[p.color % ProfileStore.Colors.Length] : UIKit.Disabled;
        var latest = p != null ? ProfileStore.Latest() : null;
        continueBtn.interactable = latest != null;
        continueBtn.GetComponent<Image>().color = latest != null ? UIKit.Accent : new Color(0.16f, 0.14f, 0.22f, 0.9f);
        continueSub.text = latest != null
            ? $"Jour {latest.day} · Niveau {latest.level}{(string.IsNullOrEmpty(latest.colony) ? "" : " · " + latest.colony)} · {latest.savedAt}"
            : "Aucune partie sauvegardée";
    }

    // ─────────────────────────────────────────────
    // ACTIONS DE L'ACCUEIL
    // ─────────────────────────────────────────────

    private void ContinueGame()
    {
        var latest = ProfileStore.Latest();
        if (latest != null) SaveSystem.Load(latest.slot);
    }

    private void NewGameClicked() => ShowDifficulty();

    /// <summary>Choix de la difficulté avant une nouvelle partie.</summary>
    private void ShowDifficulty()
    {
        var box = OpenModal("Difficulté", new Vector2(1100, 800));
        UIKit.LabelAt(box, "Choisissez votre niveau de jeu. Il ne pourra plus être changé pendant la partie.", 22, UIKit.Sub, new Vector2(34, -84), new Vector2(1000, 32));
        float y = 132f;
        for (int i = 0; i < GameDifficulty.Names.Length; i++)
        {
            var d = (Difficulty)i;
            bool rec = d == Difficulty.Moyen;
            var card = UIKit.PanelBox("Diff", box, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(1040, 146), rec ? new Color(0.2f, 0.15f, 0.34f, 0.98f) : UIKit.Card);
            card.pivot = new Vector2(0.5f, 1);
            UIKit.LabelAt(card, GameDifficulty.Names[i] + (rec ? "  <size=65%><color=#B9B3CC>(recommandé)</color></size>" : ""), 32, GameDifficulty.Colors[i], new Vector2(24, -14), new Vector2(760, 42));
            UIKit.LabelAt(card, GameDifficulty.Descriptions[i], 21, UIKit.Text, new Vector2(24, -60), new Vector2(760, 80));
            UIKit.BtnAt(card, "Choisir", () => { GameDifficulty.Current = d; ConfirmNewGame(); },
                new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(200, 64), 26, rec ? UIKit.Accent : (Color?)null);
            y += 158f;
        }
    }

    private void ConfirmNewGame()
    {
        CloseModal();
        ProfileStore.EnsureActive();
        var auto = ProfileStore.Summary("auto");
        if (auto.exists)
            Confirm("Nouvelle partie", $"Une partie est en cours (jour {auto.day}). Ses sauvegardes restent disponibles, mais la sauvegarde automatique sera remplacée.",
                "Commencer", StartNewGame);
        else StartNewGame();
    }

    private void StartNewGame()
    {
        var p = ProfileStore.EnsureActive();
        p.gamesStarted++;
        ProfileStore.Save();
        LeaveHome();
        HoldIntro = false;
        StoryManager.Instance?.StartIntro();
    }

    private void LeaveHome()
    {
        CloseModal();
        InHome = false;
        home.gameObject.SetActive(false);
        GameHUD.Instance?.SetHudVisible(true);
        if (CameraController.Instance != null) CameraController.Instance.cinematic = false;
        var cam = Camera.main;
        if (cam != null) cam.orthographicSize = 6f;
        DungeonLayers.Instance?.SetView(0);
        var core = FindFirstObjectByType<DungeonCore>();
        if (core != null) CameraController.Instance?.CenterOn(core.transform.position);
    }

    // ─────────────────────────────────────────────
    // MENU PAUSE (EN JEU)
    // ─────────────────────────────────────────────

    public void OpenPause()
    {
        if (InHome || InPause) return;
        InPause = true;
        if (!pausePushed) { GameManager.Instance?.PushPause(); pausePushed = true; }
        pause = UIKit.Stretch(UIKit.Rect("Pause", root));
        UIKit.Blocker(pause, 0.55f);
        var box = UIKit.PanelBox("Box", pause, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 752));
        box.pivot = new Vector2(0.5f, 0.5f);
        UIKit.LabelAt(box, "Pause", 48, UIKit.Gold, new Vector2(0, -26), new Vector2(560, 60), TextAlignmentOptions.Center, new Vector2(0.5f, 1)).rectTransform.pivot = new Vector2(0.5f, 1);
        var gm = GameManager.Instance;
        string info = gm != null ? $"Jour {gm.CurrentDay} · {gm.GetTimeOfDayLabel()} · {GameDifficulty.Name} · profil {ProfileStore.EnsureActive().name}" : "";
        UIKit.LabelAt(box, info, 20, UIKit.Sub, new Vector2(0, -88), new Vector2(520, 30), TextAlignmentOptions.Center, new Vector2(0.5f, 1)).rectTransform.pivot = new Vector2(0.5f, 1);
        float y = 136f;
        PauseButton(box, "Reprendre", ClosePause, ref y, UIKit.Accent);
        if (GameDifficulty.ManualSaves) PauseButton(box, "Sauvegarder", () => ShowSlots(true), ref y);
        else PauseButton(box, "Sauvegarder (Impitoyable : auto)", () => { }, ref y, new Color(0.18f, 0.16f, 0.22f, 0.9f));
        PauseButton(box, "Charger", () => ShowSlots(false), ref y);
        PauseButton(box, "Chroniques de l'île", ChronicleWindow.Show, ref y);
        PauseButton(box, "Options", ShowOptions, ref y);
        PauseButton(box, "Retour à l'accueil", () => Confirm("Retour à l'accueil",
            "La progression depuis la dernière sauvegarde sera perdue. Sauvegarder d'abord dans la sauvegarde automatique ?",
            "Sauvegarder et quitter", () => { SaveSystem.Instance?.Save("auto"); GoHome(); }, "Quitter sans sauvegarder", GoHome), ref y, new Color(0.3f, 0.14f, 0.18f, 0.96f));
    }

    private void PauseButton(RectTransform box, string label, UnityEngine.Events.UnityAction a, ref float y, Color? c = null)
    {
        var b = UIKit.BtnAt(box, label, a, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(440, 76), 30, c);
        ((RectTransform)b.transform).pivot = new Vector2(0.5f, 1);
        y += 92f;
    }

    public void ClosePause()
    {
        CloseModal();
        if (pause != null) Destroy(pause.gameObject);
        pause = null;
        InPause = false;
        if (pausePushed) { GameManager.Instance?.PopPause(); pausePushed = false; }
        PointerUtil.BlockWorldInput(0.25f);
    }

    public void GoHome()
    {
        StartMode = Boot.Home;
        Time.timeScale = 1f;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.name : scene.path);
    }

    // ─────────────────────────────────────────────
    // FENÊTRES
    // ─────────────────────────────────────────────

    private RectTransform OpenModal(string title, Vector2 size)
    {
        CloseModal();
        modal = UIKit.Stretch(UIKit.Rect("Modal", root));
        UIKit.Blocker(modal, 0.5f);
        var box = UIKit.PanelBox("Box", modal, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        box.pivot = new Vector2(0.5f, 0.5f);
        var t = UIKit.LabelAt(box, title, 40, UIKit.Gold, new Vector2(32, -22), new Vector2(size.x - 160, 56));
        t.textWrappingMode = TextWrappingModes.NoWrap;
        UIKit.BtnAt(box, "Fermer", CloseModal, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(130, 50), 22);
        return box;
    }

    public void CloseModal()
    {
        if (modal != null) Destroy(modal.gameObject);
        modal = null;
        if (InHome) RefreshHome();
    }

    private void Confirm(string title, string text, string okLabel, Action ok, string altLabel = null, Action alt = null)
    {
        var box = OpenModal(title, new Vector2(820, 380));
        UIKit.LabelAt(box, text, 26, UIKit.Text, new Vector2(32, -96), new Vector2(756, 150));
        float x = -24f;
        var okB = UIKit.BtnAt(box, okLabel, () => { CloseModal(); ok?.Invoke(); }, new Vector2(1, 0), new Vector2(x, 24), new Vector2(340, 64), 24, UIKit.Accent);
        if (altLabel != null)
            UIKit.BtnAt(box, altLabel, () => { CloseModal(); alt?.Invoke(); }, new Vector2(0, 0), new Vector2(24, 24), new Vector2(340, 64), 22, new Color(0.3f, 0.14f, 0.18f, 0.96f));
    }

    // ── Emplacements de sauvegarde
    private void ShowSlots(bool saving)
    {
        var box = OpenModal(saving ? "Sauvegarder la partie" : "Charger une partie", new Vector2(980, 760));
        UIKit.LabelAt(box, $"Profil : <b>{ProfileStore.EnsureActive().name}</b>", 22, UIKit.Sub, new Vector2(34, -80), new Vector2(700, 30));
        float y = 124f;
        foreach (var slot in ProfileStore.Slots)
        {
            var sum = ProfileStore.Summary(slot);
            var card = UIKit.PanelBox("Slot", box, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(920, 136), UIKit.Card);
            card.pivot = new Vector2(0.5f, 1);
            UIKit.LabelAt(card, ProfileStore.SlotLabel(slot), 28, slot == "auto" ? new Color(0.7f, 0.85f, 1f) : UIKit.Text, new Vector2(24, -16), new Vector2(520, 38));
            string info = sum.exists
                ? $"Jour {sum.day} · Niveau {sum.level}{(string.IsNullOrEmpty(sum.colony) ? "" : " · colonie : " + sum.colony)}\n<size=85%><color=#9A96B0>{sum.savedAt}</color></size>"
                : "<color=#6F6A80>Vide</color>";
            UIKit.LabelAt(card, info, 22, UIKit.Sub, new Vector2(24, -58), new Vector2(520, 70));
            float bx = -20f;
            string s = slot;
            if (saving && slot != "auto")
            {
                var sb = UIKit.BtnAt(card, sum.exists ? "Écraser" : "Sauvegarder", () =>
                {
                    if (sum.exists) Confirm("Écraser la sauvegarde ?", $"{ProfileStore.SlotLabel(s)} (jour {sum.day}) sera remplacé.", "Écraser", () => { SaveSystem.Instance?.Save(s); ShowSlots(true); });
                    else { SaveSystem.Instance?.Save(s); ShowSlots(true); }
                }, new Vector2(1, 0.5f), new Vector2(bx, 0), new Vector2(190, 60), 22, UIKit.Accent);
                bx -= 202f;
            }
            if (!saving && sum.exists)
            {
                UIKit.BtnAt(card, "Charger", () =>
                {
                    if (!InHome) Confirm("Charger cette partie ?", "La progression non sauvegardée sera perdue.", "Charger", () => SaveSystem.Load(s));
                    else SaveSystem.Load(s);
                }, new Vector2(1, 0.5f), new Vector2(bx, 0), new Vector2(170, 60), 22, UIKit.Accent);
                bx -= 182f;
            }
            if (sum.exists)
                UIKit.BtnAt(card, "Suppr.", () => Confirm("Supprimer la sauvegarde ?", $"{ProfileStore.SlotLabel(s)} (jour {sum.day}) sera définitivement effacé.", "Supprimer",
                    () => { ProfileStore.DeleteSlot(s); ShowSlots(saving); }), new Vector2(1, 0.5f), new Vector2(bx, 0), new Vector2(120, 60), 20, new Color(0.3f, 0.14f, 0.18f, 0.96f));
            y += 148f;
        }
        if (saving) UIKit.LabelAt(box, "La sauvegarde automatique se fait à chaque aube et quand l'application passe en arrière-plan.", 19, new Color(0.55f, 0.52f, 0.66f), new Vector2(34, 26), new Vector2(900, 30), TextAlignmentOptions.BottomLeft, new Vector2(0, 0)).rectTransform.pivot = Vector2.zero;
    }

    // ── Modes de jeu
    private void ShowModes()
    {
        var box = OpenModal("Modes de jeu", new Vector2(1100, 720));
        var modes = new (string name, string desc, bool ready)[]
        {
            ("Campagne : l'Île du Noyau", "Creusez, invoquez, piégez. Affrontez pêcheurs, aventuriers, chevaliers puis le Héros, pendant qu'une colonie humaine grandit sur l'île.", true),
            ("Survie : les Vagues", "Des expéditions sans fin, de plus en plus fortes. Combien de jours tiendrez-vous ?", false),
            ("Aventurier : l'Appel du donjon", "Incarnez un aventurier : préparez-vous en ville, descendez dans le donjon du Noyau et brisez son cristal.", true),
            ("Défi du jour", "Une île, des règles et un score partagés par tous les joueurs, chaque jour.", false),
        };
        float y = 96f;
        foreach (var m in modes)
        {
            var card = UIKit.PanelBox("Mode", box, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(1040, 138), m.ready ? new Color(0.2f, 0.15f, 0.34f, 0.98f) : UIKit.Card);
            card.pivot = new Vector2(0.5f, 1);
            UIKit.LabelAt(card, m.name, 30, m.ready ? UIKit.Gold : new Color(0.6f, 0.58f, 0.7f), new Vector2(24, -14), new Vector2(760, 40));
            UIKit.LabelAt(card, m.desc, 21, m.ready ? UIKit.Text : new Color(0.55f, 0.52f, 0.66f), new Vector2(24, -56), new Vector2(760, 76));
            if (m.ready)
                UIKit.BtnAt(card, "Jouer", m.name.StartsWith("Aventurier") ? (UnityEngine.Events.UnityAction)ShowClassSelect : () => { CloseModal(); NewGameClicked(); },
                    new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(200, 64), 26, UIKit.Accent);
            else
            {
                var tag = UIKit.PanelBox("Soon", card, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(200, 50), new Color(0.3f, 0.26f, 0.12f, 1f));
                UIKit.Stretch(UIKit.Label(tag, "Bientôt", 22, UIKit.Gold).rectTransform);
            }
            y += 150f;
        }
    }

    // ── Succès
    private void ShowAchievements()
    {
        var box = OpenModal("Succès", new Vector2(1100, 900));
        int got = 0;
        foreach (var d in Achievements.All) if (Achievements.IsUnlocked(d.id)) got++;
        UIKit.LabelAt(box, $"{got} / {Achievements.All.Count} débloqués · profil {ProfileStore.EnsureActive().name}", 22, UIKit.Sub, new Vector2(34, -80), new Vector2(900, 30));
        float y = 124f;
        int i = 0;
        foreach (var d in Achievements.All)
        {
            bool ok = Achievements.IsUnlocked(d.id);
            float x = i % 2 == 0 ? -262f : 262f;
            var card = UIKit.PanelBox("Ach", box, new Vector2(0.5f, 1), new Vector2(x, -y), new Vector2(510, 84), ok ? new Color(0.22f, 0.18f, 0.1f, 0.98f) : UIKit.Card);
            card.pivot = new Vector2(0.5f, 1);
            UIKit.LabelAt(card, (ok ? "" : "<color=#6F6A80>") + d.name, 24, ok ? UIKit.Gold : UIKit.Sub, new Vector2(18, -10), new Vector2(480, 32));
            UIKit.LabelAt(card, d.desc, 18, ok ? UIKit.Text : new Color(0.5f, 0.48f, 0.6f), new Vector2(18, -46), new Vector2(480, 30));
            if (i % 2 == 1) y += 94f;
            i++;
        }
    }

    // ── Mode Aventurier : choix de la classe
    private void ShowClassSelect()
    {
        var box = OpenModal("Choisissez votre aventurier", new Vector2(1100, 760));
        float y = 100f;
        for (int i = 0; i < CrawlerMode.ClassNames.Length; i++)
        {
            var role = (AdventurerAI.Role)i;
            var card = UIKit.PanelBox("Classe", box, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(1040, 140), UIKit.Card);
            card.pivot = new Vector2(0.5f, 1);
            UIKit.LabelAt(card, CrawlerMode.ClassNames[i], 32, UIKit.Gold, new Vector2(24, -14), new Vector2(700, 42));
            UIKit.LabelAt(card, CrawlerMode.ClassDescs[i], 21, UIKit.Text, new Vector2(24, -60), new Vector2(760, 70));
            UIKit.BtnAt(card, "Choisir", () => ShowCustomize(role), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(200, 64), 26, UIKit.Accent);
            y += 156f;
        }
    }

    // ── Mode Aventurier : apparence et nom du héros
    private void ShowCustomize(AdventurerAI.Role role)
    {
        HeroLook.Load();
        var box = OpenModal("Votre aventurier", new Vector2(1180, 820));
        string[] looks = { "swordsman_1", "adv_archer_1", "adv_mage_1", "adv_healer_1" };
        var cat = DungeonCatalog.Get();
        Sprite baseSprite = cat != null && cat.lookPreview != null && cat.lookPreview.Length > (int)role ? cat.lookPreview[(int)role] : null;

        // aperçu
        var frame = UIKit.PanelBox("Apercu", box, new Vector2(0, 1), new Vector2(40, -96), new Vector2(380, 470), UIKit.Card);
        frame.pivot = new Vector2(0, 1);
        var pv = UIKit.Rect("Heros", frame);
        pv.anchorMin = pv.anchorMax = pv.pivot = new Vector2(0.5f, 0.55f);
        pv.sizeDelta = new Vector2(340, 340);
        var img = pv.gameObject.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        UIKit.LabelAt(frame, CrawlerMode.ClassNames[(int)role], 30, UIKit.Gold, new Vector2(0, -410), new Vector2(380, 44), TextAlignmentOptions.Center);
        Sprite shown = null;
        void Preview()
        {
            if (shown != null && shown != baseSprite) { Destroy(shown.texture); }
            var m = HeroLook.Customized ? HeroLook.Mapping.Build(looks[(int)role], HeroLook.Hair, HeroLook.Skin, HeroLook.Outfit) : null;
            shown = m != null ? HeroLook.RecolorSprite(baseSprite, m) : baseSprite;
            img.sprite = shown;
            img.enabled = shown != null;
        }

        // nom
        float x0 = 460f;
        UIKit.LabelAt(box, "Nom", 24, UIKit.Sub, new Vector2(x0, -96), new Vector2(200, 34));
        var name = UIKit.Input(box, "Sans nom, on vous appellera par votre classe", new Vector2(0, 1), new Vector2(x0, -134), new Vector2(660, 64), 26);
        ((RectTransform)name.transform).pivot = new Vector2(0, 1);
        name.characterLimit = 18;
        name.text = HeroLook.HeroName;
        name.onValueChanged.AddListener(v => HeroLook.HeroName = v.Trim());

        // nuanciers
        float y = 230f;
        void Row(string label, Color[] choices, System.Func<Color?> get, System.Action<Color?> set)
        {
            UIKit.LabelAt(box, label, 24, UIKit.Sub, new Vector2(x0, -y), new Vector2(200, 34));
            var buttons = new System.Collections.Generic.List<(Button b, Color? c)>();
            void Mark()
            {
                foreach (var (b, c) in buttons)
                {
                    bool on = get().HasValue == c.HasValue && (!c.HasValue || get().Value == c.Value);
                    b.transform.localScale = Vector3.one * (on ? 1.18f : 1f);
                    UIKit.SetLabel(b, on ? "<b>•</b>" : "");
                }
            }
            for (int i = 0; i <= choices.Length; i++)
            {
                Color? c = i == 0 ? (Color?)null : choices[i - 1];
                var b = UIKit.BtnAt(box, i == 0 ? "" : "", () => { set(c); Mark(); Preview(); }, new Vector2(0, 1),
                    new Vector2(x0 + 30 + i * 84, -(y + 44)), new Vector2(68, 68), 30, c ?? new Color(0.25f, 0.22f, 0.32f, 1f));
                if (i == 0) UIKit.SetLabel(b, "—");
                buttons.Add((b, c));
            }
            Mark();
            if (!get().HasValue) UIKit.SetLabel(buttons[0].b, "<b>—</b>");
            y += 140f;
        }
        Row("Cheveux", HeroLook.HairChoices, () => HeroLook.Hair, c => HeroLook.Hair = c);
        Row("Peau", HeroLook.SkinChoices, () => HeroLook.Skin, c => HeroLook.Skin = c);
        Row("Tenue", HeroLook.OutfitChoices, () => HeroLook.Outfit, c => HeroLook.Outfit = c);
        UIKit.LabelAt(box, "« — » : couleur d'origine. Les cheveux disparaissent sous le casque des armures lourdes.", 18, UIKit.Sub, new Vector2(x0, -y + 20), new Vector2(680, 50));

        UIKit.BtnAt(box, "Retour", ShowClassSelect, new Vector2(0, 0), new Vector2(40, 36), new Vector2(220, 70), 26);
        UIKit.BtnAt(box, "Partir à l'aventure", () => { HeroLook.Save(); StartCrawler(role); }, new Vector2(1, 0), new Vector2(-40, 36), new Vector2(380, 70), 28, UIKit.Accent);
        Preview();
    }

    private void StartCrawler(AdventurerAI.Role role)
    {
        CloseModal();
        ProfileStore.EnsureActive();
        LeaveHome();
        HoldIntro = false;
        var cm = FindFirstObjectByType<CrawlerMode>();
        if (cm == null) cm = new GameObject("CrawlerMode").AddComponent<CrawlerMode>();
        cm.Begin(role);
    }

    // ── Options
    private void ShowOptions()
    {
        var box = OpenModal("Options", new Vector2(1760, 900));
        float y = 100f;
        colX = -440f;
        VolumeRow(box, "Volume général", "Tous les sons du jeu", () => GameSettings.Volume, v => GameSettings.Volume = v, ref y);
        VolumeRow(box, "Musique", "Thèmes du jour, de la nuit, des combats", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, ref y);
        VolumeRow(box, "Ambiance", "Grotte, ressac, battements de cœur", () => GameSettings.AmbienceVolume, v => GameSettings.AmbienceVolume = v, ref y);
        VolumeRow(box, "Bruitages", "Combats, pièges, creusage, interface", () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, ref y);
        VolumeRow(box, "Taille de l'interface", "Boutons et textes plus grands (ou plus petits)", () => (GameSettings.UiScale - 0.8f) / 0.6f,
            v => GameSettings.UiScale = 0.8f + Mathf.Clamp01(v) * 0.6f, ref y, () => $"{Mathf.RoundToInt(GameSettings.UiScale * 100)} %", 1f / 6f);
        ToggleRow(box, "Tutoriel guidé", "Conseils et flèche vers le bouton à toucher", () => GameSettings.TutorialHints, v => GameSettings.TutorialHints = v, ref y);
        y = 100f;
        colX = 440f;
        VolumeRow(box, "Qualité graphique", "Économie : plus fluide sur un téléphone modeste", () => GameSettings.QualityLevel / 2f,
            v => GraphicsQuality.Set((GraphicsQuality.Level)Mathf.Clamp(Mathf.RoundToInt(v * 2f), 0, 2), true), ref y,
            () => GraphicsQuality.Name(GraphicsQuality.Current), 0.5f);
        ToggleRow(box, "Cinématiques", "Naissance de l'orbe, premier bateau", () => GameSettings.Cinematics, v => GameSettings.Cinematics = v, ref y);
        ToggleRow(box, "Effets de lumière", "Halo, vignettage, couleurs (désactiver sur un téléphone modeste)", () => GameSettings.PostProcessing, v => GameSettings.PostProcessing = v, ref y);
        ToggleRow(box, "Particules d'ambiance", "Lucioles, nuages, poussière, reflets sur l'eau", () => GameSettings.Ambient, v => GameSettings.Ambient = v, ref y);
        ToggleRow(box, "Tremblements de l'écran", "Secousses lors des impacts", () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v, ref y);
        ToggleRow(box, "Sauvegarde automatique", "À chaque aube et en quittant l'application", () => GameSettings.AutoSave, v => GameSettings.AutoSave = v, ref y);
        ToggleRow(box, "Vibrations", "Quand le cristal est frappé ou que votre héros est blessé", () => GameSettings.Vibration, v => GameSettings.Vibration = v, ref y);
        ToggleRow(box, "English", "Interface en anglais / English interface", () => GameSettings.Language == "en", v => { GameSettings.Language = v ? "en" : "fr"; Loc.Apply(v); }, ref y);
        colX = 0f;
    }

    private float colX;

    private void VolumeRow(RectTransform box, string label, string desc, Func<float> get, Action<float> set, ref float y, Func<string> format = null, float step = 0.1f)
    {
        var row = UIKit.PanelBox("Row", box, new Vector2(0.5f, 1), new Vector2(colX, -y), new Vector2(840, 86), UIKit.Card);
        row.pivot = new Vector2(0.5f, 1);
        UIKit.LabelAt(row, label, 28, UIKit.Text, new Vector2(24, -14), new Vector2(300, 36));
        var val = UIKit.LabelAt(row, "", 24, UIKit.Gold, new Vector2(-150, 0), new Vector2(110, 40), TextAlignmentOptions.Center, new Vector2(1, 0.5f));
        void Show() => val.text = format != null ? format() : $"{Mathf.RoundToInt(get() * 100)} %";
        Show();
        UIKit.BtnAt(row, "-", () => { set(get() - step); Show(); }, new Vector2(1, 0.5f), new Vector2(-270, 0), new Vector2(70, 60), 34);
        UIKit.BtnAt(row, "+", () => { set(get() + step); Show(); }, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(70, 60), 34);
        UIKit.LabelAt(row, desc, 18, UIKit.Sub, new Vector2(24, -50), new Vector2(460, 30));
        y += 94f;
    }

    private void ToggleRow(RectTransform box, string label, string desc, Func<bool> get, Action<bool> set, ref float y)
    {
        var row = UIKit.PanelBox("Row", box, new Vector2(0.5f, 1), new Vector2(colX, -y), new Vector2(840, 86), UIKit.Card);
        row.pivot = new Vector2(0.5f, 1);
        UIKit.LabelAt(row, label, 28, UIKit.Text, new Vector2(24, -14), new Vector2(560, 36));
        UIKit.LabelAt(row, desc, 18, UIKit.Sub, new Vector2(24, -50), new Vector2(560, 30));
        Button b = null;
        void Show()
        {
            bool on = get();
            UIKit.SetLabel(b, on ? "Oui" : "Non");
            b.GetComponent<Image>().color = on ? UIKit.Accent : UIKit.Disabled;
        }
        b = UIKit.BtnAt(row, "", () => { set(!get()); Show(); }, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(150, 60), 26);
        var t = UIKit.Label(b.transform, "", 26, UIKit.Text);
        UIKit.Stretch(t.rectTransform);
        Show();
        y += 94f;
    }

    // ── Profils
    private void ShowProfiles()
    {
        var box = OpenModal("Profils", new Vector2(960, 780));
        UIKit.LabelAt(box, "Chaque profil a ses propres parties et statistiques. Ils sont enregistrés sur cet appareil.", 21, UIKit.Sub, new Vector2(32, -84), new Vector2(880, 30));
        var area = UIKit.Rect("List", box);
        area.anchorMin = new Vector2(0, 0); area.anchorMax = new Vector2(1, 1);
        area.offsetMin = new Vector2(24, 110); area.offsetMax = new Vector2(-24, -126);
        var content = UIKit.ScrollArea(area, out _);
        float y = 0f;
        var list = ProfileStore.List;
        foreach (var p in list.profiles)
        {
            bool active = p.id == list.activeId;
            var card = UIKit.PanelBox("P", content, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(880, 110), active ? new Color(0.2f, 0.15f, 0.34f, 0.98f) : UIKit.Card);
            card.pivot = new Vector2(0.5f, 1);
            var badge = UIKit.Image(UIKit.Place(UIKit.Rect("Badge", card), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(70, 70)), ProfileStore.Colors[p.color % ProfileStore.Colors.Length], false, false);
            badge.sprite = SpriteUtil.Circle;
            var ini = UIKit.Label(badge.transform, p.name.Length > 0 ? p.name.Substring(0, 1).ToUpperInvariant() : "?", 36, Color.white);
            UIKit.Stretch(ini.rectTransform);
            UIKit.LabelAt(card, p.name + (active ? "  <size=70%><color=#FFD966>(actif)</color></size>" : ""), 28, UIKit.Text, new Vector2(104, -14), new Vector2(460, 38));
            var ts = TimeSpan.FromSeconds(p.playSeconds);
            UIKit.LabelAt(card, $"{(int)ts.TotalHours} h {ts.Minutes:00} de jeu · meilleur jour : {p.bestDay} · parties : {p.gamesStarted}", 19, UIKit.Sub, new Vector2(104, -58), new Vector2(520, 30));
            string id = p.id;
            if (!active)
                UIKit.BtnAt(card, "Choisir", () => { ProfileStore.SetActive(id); ShowProfiles(); }, new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(150, 56), 22, UIKit.Accent);
            UIKit.BtnAt(card, "Suppr.", () => Confirm("Supprimer le profil ?", $"« {p.name} » et toutes ses sauvegardes seront définitivement effacés.", "Supprimer",
                () => { ProfileStore.Delete(id); if (ProfileStore.HasProfile) ShowProfiles(); else ShowProfileCreate(true); }),
                new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(120, 56), 20, new Color(0.3f, 0.14f, 0.18f, 0.96f));
            y += 122f;
        }
        content.sizeDelta = new Vector2(0, y);
        UIKit.BtnAt(box, "Nouveau profil", () => ShowProfileCreate(false), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(360, 64), 26, UIKit.Accent)
            .transform.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
    }

    private void ShowProfileCreate(bool first)
    {
        var box = OpenModal(first ? "Bienvenue, Noyau" : "Nouveau profil", new Vector2(820, 420));
        if (first)
        {
            // pas de fermeture possible sans profil
            var close = box.Find("Btn_Fermer");
            if (close != null) close.gameObject.SetActive(false);
        }
        UIKit.LabelAt(box, first ? "Comment s'appelle la conscience qui s'éveille dans cette île ?" : "Nom du nouveau profil :", 24, UIKit.Text, new Vector2(32, -92), new Vector2(760, 60));
        var input = UIKit.Input(box, "Nom du profil", new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(740, 76), 32);
        input.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);
        UIKit.BtnAt(box, "Créer", () =>
        {
            ProfileStore.Create(input.text);
            CloseModal();
        }, new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(320, 68), 28, UIKit.Accent).GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
    }
}
