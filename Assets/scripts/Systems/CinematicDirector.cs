using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Cinématiques : naissance de l'orbe (début de partie) et arrivée du premier bateau.
/// Bandes noires, sous-titres, caméra pilotée ; « Passer » (bouton, Échap ou Espace).
/// </summary>
public partial class CinematicDirector : MonoBehaviour
{
    public static CinematicDirector Instance { get; private set; }

    public bool playIntroCinematic = true;
    public bool IsPlaying { get; private set; }

    private Canvas canvas;
    private RectTransform topBar, bottomBar;
    private Image fade;
    private TextMeshProUGUI caption, titleText, subText, stampText;
    private Image card;
    private CanvasGroup cardGroup;
    private GameObject skipButton;
    private bool skip;

    /// <summary>Passe la cinématique en cours (bouton, Échap, tests).</summary>
    public void Skip() => skip = true;

    /// <summary>La cinématique plonge dans la montagne : vue « donjon » (relief effacé).</summary>
    public bool ForceDungeonView { get; set; }

    private Camera cam;
    private CameraController camCtrl;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // ─────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────

    private void BuildUI()
    {
        var go = new GameObject("Cinematic_Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();

        fade = NewImage(go.transform, "Fade", Color.black);
        Stretch(fade.rectTransform);
        fade.raycastTarget = false;
        topBar = Bar(go.transform, true);
        bottomBar = Bar(go.transform, false);

        var capGo = new GameObject("Caption", typeof(RectTransform));
        capGo.transform.SetParent(go.transform, false);
        caption = capGo.AddComponent<TextMeshProUGUI>();
        caption.alignment = TextAlignmentOptions.Center;
        caption.fontSize = 34;
        caption.color = new Color(0.95f, 0.93f, 1f);
        caption.textWrappingMode = TextWrappingModes.Normal;
        caption.raycastTarget = false;
        var crt = caption.rectTransform;
        crt.anchorMin = new Vector2(0.1f, 0f);
        crt.anchorMax = new Vector2(0.9f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, 24f);
        crt.sizeDelta = new Vector2(0f, 110f);

        // Panneau « document » (registre de la Guilde, cartons de titre)
        card = NewImage(go.transform, "Card", new Color(0.15f, 0.1f, 0.08f, 0.94f));
        card.raycastTarget = false;
        var cart = card.rectTransform;
        cart.anchorMin = cart.anchorMax = cart.pivot = new Vector2(0.5f, 0.5f);
        cart.anchoredPosition = new Vector2(0f, 60f);
        cart.sizeDelta = new Vector2(1240f, 620f);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        cardGroup.blocksRaycasts = false;
        // liseré doré (4 filets)
        foreach (var (amin, amax, size) in new[]
        {
            (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 4f)), (new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f)),
            (new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(4f, 0f)), (new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f)),
        })
        {
            var edge = NewImage(card.transform, "Edge", new Color(0.85f, 0.68f, 0.35f, 1f));
            edge.raycastTarget = false;
            var ert = edge.rectTransform;
            ert.anchorMin = amin; ert.anchorMax = amax; ert.pivot = new Vector2(0.5f, 0.5f);
            ert.anchoredPosition = Vector2.zero; ert.sizeDelta = size;
        }
        card.gameObject.SetActive(false);

        titleText = NewCenterText(go.transform, "Title", 76, new Color(1f, 0.86f, 0.5f), 70f, 110f);
        titleText.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;
        titleText.characterSpacing = 6f;
        subText = NewCenterText(go.transform, "Subtitle", 32, new Color(0.88f, 0.86f, 0.95f), -10f, 70f);
        subText.fontStyle = FontStyles.Italic;
        stampText = NewCenterText(go.transform, "Stamp", 260, Color.white, 150f, 300f);
        stampText.fontStyle = FontStyles.Bold;
        stampText.outlineWidth = 0.12f;
        stampText.outlineColor = new Color32(20, 10, 25, 255);

        var skipImg = NewImage(go.transform, "Skip", new Color(0f, 0f, 0f, 0.45f));
        skipButton = skipImg.gameObject;
        var srt = skipImg.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(1f, 1f);
        srt.anchoredPosition = new Vector2(-24f, -24f);
        srt.sizeDelta = new Vector2(190f, 60f);
        var btn = skipButton.AddComponent<Button>();
        btn.onClick.AddListener(() => skip = true);
        var st = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        st.transform.SetParent(skipButton.transform, false);
        st.text = "Passer >>";
        st.fontSize = 26;
        st.alignment = TextAlignmentOptions.Center;
        st.color = Color.white;
        st.raycastTarget = false;
        Stretch(st.rectTransform);

        canvas.enabled = false;
    }

    private static Image NewImage(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private RectTransform Bar(Transform parent, bool top)
    {
        var img = NewImage(parent, top ? "BarTop" : "BarBottom", Color.black);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 0f);
        return rt;
    }

    private void Update()
    {
        if (!IsPlaying) return;
        PointerUtil.BlockWorldInput(0.25f);
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) skip = true;
    }

    // ─────────────────────────────────────────────
    // OUTILS
    // ─────────────────────────────────────────────

    private void Begin()
    {
        IsPlaying = true;
        skip = false;
        cam = Camera.main;
        camCtrl = cam != null ? cam.GetComponent<CameraController>() : null;
        if (camCtrl != null) camCtrl.cinematic = true;
        canvas.enabled = true;
        caption.text = "";
        // L'écran part toujours du jeu visible (le voile noir de départ ne sert qu'à l'intro)
        fade.color = new Color(0f, 0f, 0f, 0f);
        ClearCards();
        GameHUD.Instance?.SetHudVisible(false);
    }

    private IEnumerator RevealInsideLater(float delay)
    {
        for (float t = 0f; t < delay && !skip; t += Time.unscaledDeltaTime) yield return null;
        ForceDungeonView = true;
    }

    private void End()
    {
        HideBubble();
        // on revient en vue donjon (construction)
        if (ForceDungeonView) MountainRelief.Instance?.SetUserView(true);
        ForceDungeonView = false;
        caption.text = "";
        ClearCards();
        topBar.sizeDelta = bottomBar.sizeDelta = Vector2.zero;
        fade.color = new Color(0f, 0f, 0f, 0f);
        canvas.enabled = false;
        if (camCtrl != null) camCtrl.cinematic = false;
        GameHUD.Instance?.SetHudVisible(true);
        IsPlaying = false;
    }

    private IEnumerator Bars(bool show, float duration = 0.6f)
    {
        float h0 = topBar.sizeDelta.y, h1 = show ? 120f : 0f;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            float h = Mathf.Lerp(h0, h1, Mathf.SmoothStep(0f, 1f, t));
            topBar.sizeDelta = bottomBar.sizeDelta = new Vector2(0f, h);
            yield return null;
        }
        topBar.sizeDelta = bottomBar.sizeDelta = new Vector2(0f, h1);
    }

    private IEnumerator Fade(float from, float to, float duration, Color? color = null)
    {
        var c = color ?? Color.black;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            fade.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t));
            yield return null;
        }
        fade.color = new Color(c.r, c.g, c.b, to);
    }

    private static readonly Color NarratorColor = new Color(0.95f, 0.93f, 1f);
    private bool thoughtMode;

    private IEnumerator Caption(string text, float hold)
    {
        if (!thoughtMode) { caption.fontStyle = FontStyles.Normal; caption.color = NarratorColor; }
        caption.text = "";
        caption.alpha = 1f;
        // machine à écrire à vitesse constante (≈ 55 caractères par seconde, quel que soit le nombre d'images)
        for (float el = 0f; !skip; el += Time.unscaledDeltaTime)
        {
            int n = Mathf.Min(text.Length, Mathf.FloorToInt(el * 55f) + 1);
            caption.text = text.Substring(0, n);
            if (n >= text.Length) break;
            yield return null;
        }
        caption.text = text;
        for (float t = 0f; t < hold && !skip; t += Time.unscaledDeltaTime) yield return null;
    }

    /// <summary>Monologue intérieur du Noyau : sous-titre en italique, bleu pâle.</summary>
    private IEnumerator Thought(string text, float hold)
    {
        caption.fontStyle = FontStyles.Italic;
        caption.color = new Color(0.72f, 0.88f, 1f);
        thoughtMode = true;
        yield return Caption(text, hold);
        thoughtMode = false;
    }

    /// <summary>La lueur de l'orbe bat comme un cœur.</summary>
    private IEnumerator Pulse(Light2D l, float baseI, float duration, float amp = 0.8f)
    {
        if (l == null) yield break;
        for (float t = 0f; t < duration && !skip; t += Time.unscaledDeltaTime)
        {
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 5.5f)), 6f);
            l.intensity = baseI * (0.55f + amp * beat);
            yield return null;
        }
        l.intensity = baseI;
    }

    private IEnumerator MoveCamera(Vector2 to, float size, float duration)
    {
        if (cam == null) yield break;
        Vector3 p0 = cam.transform.position;
        float s0 = cam.orthographicSize;
        Vector3 p1 = new Vector3(to.x, to.y, p0.z);
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            cam.transform.position = Vector3.Lerp(p0, p1, e);
            cam.orthographicSize = Mathf.Lerp(s0, size, e);
            yield return null;
        }
        cam.transform.position = p1;
        cam.orthographicSize = size;
    }

    private IEnumerator Wait(float s)
    {
        for (float t = 0f; t < s && !skip; t += Time.unscaledDeltaTime) yield return null;
    }

    // ─────────────────────────────────────────────
    // INTRO : voir CinematicIntro.cs
    // ─────────────────────────────────────────────

    // ─────────────────────────────────────────────
    // PREMIER BATEAU
    // ─────────────────────────────────────────────

    public void PlayFirstBoat(Transform boat, Action onEnd) => Enqueue(() => FirstBoatRoutine(boat, onEnd));

    private IEnumerator FirstBoatRoutine(Transform boat, Action onEnd)
    {
        Begin();
        Vector3 back = cam != null ? cam.transform.position : Vector3.zero;
        float backSize = cam != null ? cam.orthographicSize : 8f;
        // le bateau se regarde depuis l'île (volcan en relief), puis on revient à la vue du joueur
        bool wasDungeon = MountainRelief.Instance == null || MountainRelief.Instance.UserDungeonView;
        MountainRelief.Instance?.SetUserView(false);
        yield return Bars(true);
        Vector3 dock = boat != null ? boat.position : back;
        var spawner = FindFirstObjectByType<AdventurerSpawner>();
        if (spawner != null) dock = spawner.SpawnPosition;
        StartCoroutine(Caption("Une voile, sur l'horizon.", 2f));
        yield return MoveCamera(dock + new Vector3(2f, 1.5f, 0f), 6.5f, 2.6f);
        yield return Caption("Une barque de pêcheurs, déroutée par le mauvais temps, vient s'échouer au ponton.", 2.2f);
        yield return Caption("Ils ne savent pas encore ce qui dort sous la montagne.", 2.2f);
        if (spawner != null) yield return MoveCamera(spawner.SpawnPosition + new Vector3(0f, 3f, 0f), 7f, 2.5f);
        yield return Bars(false);
        if (cam != null) { cam.transform.position = back; cam.orthographicSize = backSize; }
        MountainRelief.Instance?.SetUserView(wasDungeon);
        End();
        onEnd?.Invoke();
    }

    // ─────────────────────────────────────────────
    // FILE D'ATTENTE (une cinématique à la fois, jamais par-dessus un dialogue ou un menu)
    // ─────────────────────────────────────────────

    private readonly Queue<Func<IEnumerator>> pending = new Queue<Func<IEnumerator>>();

    /// <summary>Joue la cinématique dès que possible (après la cinématique ou le dialogue en cours).</summary>
    public void Enqueue(Func<IEnumerator> routine) => pending.Enqueue(routine);

    private void LateUpdate()
    {
        if (IsPlaying || pending.Count == 0) return;
        var menu = MainMenu.Instance;
        if (menu != null && (menu.InHome || menu.InPause)) return;
        if (StoryManager.Instance != null && StoryManager.Instance.IsShowing) return;
        if (SaveSystem.Instance != null && SaveSystem.Instance.IsLoading) return;
        StartCoroutine(pending.Dequeue()());
    }

    // ─────────────────────────────────────────────
    // CARTONS (titre, sous-titre, tampon de rang)
    // ─────────────────────────────────────────────

    private TextMeshProUGUI NewCenterText(Transform parent, string name, float size, Color c, float y, float h)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = size;
        t.color = c;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0.08f, 0.5f);
        rt.anchorMax = new Vector2(0.92f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, h);
        t.alpha = 0f;
        return t;
    }

    private IEnumerator FadeText(TextMeshProUGUI t, float to, float duration)
    {
        float from = t.alpha;
        for (float k = 0f; k < 1f && !skip; k += Time.unscaledDeltaTime / duration)
        {
            t.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }
        t.alpha = to;
    }

    /// <summary>Grand titre centré (lieu, étape) sur un bandeau sombre, avec un sous-titre.</summary>
    private IEnumerator TitleCard(string title, string sub, float hold)
    {
        titleText.text = title;
        subText.text = sub;
        card.rectTransform.sizeDelta = new Vector2(1400f, 240f);
        card.rectTransform.anchoredPosition = new Vector2(0f, 35f);
        StartCoroutine(ShowCard(true, 0.6f, 0.8f));
        StartCoroutine(FadeText(subText, 1f, 0.8f));
        yield return FadeText(titleText, 1f, 0.8f);
        yield return Wait(hold);
        StartCoroutine(FadeText(subText, 0f, 0.6f));
        StartCoroutine(ShowCard(false, 0.6f));
        yield return FadeText(titleText, 0f, 0.6f);
        card.rectTransform.sizeDelta = new Vector2(1240f, 620f);
        card.rectTransform.anchoredPosition = new Vector2(0f, 60f);
    }

    /// <summary>Tampon de la Guilde : la lettre du rang s'abat sur le rapport.</summary>
    private IEnumerator Stamp(int rank)
    {
        stampText.text = GuildSystem.RankName(rank);
        stampText.color = GuildSystem.RankColor(rank);
        stampText.alpha = 1f;
        var rt = stampText.rectTransform;
        for (float k = 0f; k < 1f && !skip; k += Time.unscaledDeltaTime / 0.28f)
        {
            float e = 1f - (1f - k) * (1f - k);
            rt.localScale = Vector3.one * Mathf.Lerp(3.2f, 1f, e);
            stampText.alpha = Mathf.Clamp01(k * 2f);
            yield return null;
        }
        rt.localScale = Vector3.one;
        stampText.alpha = 1f;
        camCtrl?.Shake(0.1f, 0.3f);
    }

    private IEnumerator ShowCard(bool show, float duration = 0.5f, float alpha = 0.94f)
    {
        card.gameObject.SetActive(true);
        if (show) card.color = new Color(card.color.r, card.color.g, card.color.b, alpha);
        float a0 = cardGroup.alpha, a1 = show ? 1f : 0f;
        for (float k = 0f; k < 1f && !skip; k += Time.unscaledDeltaTime / duration)
        {
            cardGroup.alpha = Mathf.Lerp(a0, a1, k);
            yield return null;
        }
        cardGroup.alpha = a1;
        if (!show) card.gameObject.SetActive(false);
    }

    private void LayoutCards(float titleY, float subY, float stampY)
    {
        titleText.rectTransform.anchoredPosition = new Vector2(0f, titleY);
        subText.rectTransform.anchoredPosition = new Vector2(0f, subY);
        stampText.rectTransform.anchoredPosition = new Vector2(0f, stampY);
    }

    private void ClearCards()
    {
        if (titleText != null) LayoutCards(70f, -10f, 150f);
        if (card != null) { cardGroup.alpha = 0f; card.gameObject.SetActive(false); card.rectTransform.sizeDelta = new Vector2(1240f, 620f); card.rectTransform.anchoredPosition = new Vector2(0f, 60f); }
        if (titleText != null) titleText.alpha = 0f;
        if (subText != null) subText.alpha = 0f;
        if (stampText != null) { stampText.alpha = 0f; stampText.rectTransform.localScale = Vector3.one; }
    }

    // ─────────────────────────────────────────────
    // GUILDE : ÉQUIPE D'ÉVALUATION
    // ─────────────────────────────────────────────

    /// <summary>Au siège de la Guilde, puis l'arrivée de l'équipe d'évaluation sur l'île.</summary>
    public void PlayGuildArrival(bool fromWitness, string summary, int rank, Action onEnd) =>
        Enqueue(() => GuildArrivalRoutine(fromWitness, summary, rank, onEnd));

    private IEnumerator GuildArrivalRoutine(bool fromWitness, string summary, int rank, Action onEnd)
    {
        Begin();
        GameManager.Instance?.PushPause();
        Vector3 back = cam != null ? cam.transform.position : Vector3.zero;
        float backSize = cam != null ? cam.orthographicSize : 8f;
        bool wasDungeon = MountainRelief.Instance == null || MountainRelief.Instance.UserDungeonView;

        // Sur le continent : au hall de la Guilde, le témoin raconte
        yield return Bars(true, 0.4f);
        yield return Fade(0f, 1f, 0.8f);
        string told = string.IsNullOrEmpty(summary) ? "une grotte au cœur d'une île perdue" : summary;
        yield return GuildHallScene(fromWitness, told, rank);

        // Sur l'île : le navire accoste, l'équipe se présente
        MountainRelief.Instance?.SetUserView(false);
        var spawner = FindFirstObjectByType<AdventurerSpawner>();
        Vector3 dock = spawner != null ? spawner.SpawnPosition : back;
        if (cam != null) { cam.transform.position = new Vector3(dock.x, dock.y + 1f, cam.transform.position.z); cam.orthographicSize = 5.5f; }
        caption.text = "";
        yield return Fade(1f, 0f, 1.2f);
        var exp = ExpeditionManager.Instance;
        if (exp != null)
            foreach (var m in exp.Members)
            {
                if (skip) break;
                if (m == null) continue;
                StartCoroutine(MoveCamera(m.transform.position + new Vector3(0f, 0.6f, 0f), 4f, 0.7f));
                yield return Caption($"{m.DisplayName}", 1f);
            }
        yield return MoveCamera(dock + new Vector3(0f, 2f, 0f), 6.5f, 1.2f);
        yield return Caption("Leur mission : mesurer le danger. Et revenir le raconter.", 2f);
        yield return Bars(false);

        ClearCards();
        if (cam != null) { cam.transform.position = back; cam.orthographicSize = backSize; }
        MountainRelief.Instance?.SetUserView(wasDungeon);
        End();
        GameManager.Instance?.PopPause();
        onEnd?.Invoke();
    }

    // ─────────────────────────────────────────────
    // SCÈNE : HALL DE LA GUILDE (sur le continent)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Le pêcheur rescapé (ou une villageoise inquiète) fait irruption au hall de la Guilde et raconte
    /// au maître de guilde ; celui-ci ordonne l'évaluation. Bulles de dialogue au-dessus des personnages.
    /// </summary>
    private IEnumerator GuildHallScene(bool fromWitness, string told, int rank)
    {
        var interior = InteriorSystem.Instance;
        Transform room = interior != null ? interior.ShowForCinematic("house_4") : null;
        if (room == null || cam == null)
        {
            yield return Caption(fromWitness ? $"Les pêcheurs ont tout raconté au comptoir de la Guilde : {told}." : $"Sur le continent, on s'inquiète : {told}.", 2.4f);
            yield return Caption($"La Guilde dépêche une équipe d'évaluation de rang {GuildSystem.RankName(rank)}.", 1.8f);
            yield break;
        }
        Vector3 o = room.position;
        Transform master = room.Find("npc_guildmaster");
        Vector3 masterHead = master != null ? master.position : o + new Vector3(0f, 0.75f, 0f);
        cam.transform.position = new Vector3(o.x, o.y + 0.2f, cam.transform.position.z);
        cam.orthographicSize = 4.3f;

        // le visiteur entre par la porte du bas
        var cat = DungeonCatalog.Get();
        GameObject prefab = cat.adventurerPrefabs.Count > (fromWitness ? 0 : 1) ? cat.adventurerPrefabs[fromWitness ? 0 : 1] : null;
        var actor = SpawnActor(prefab, o + new Vector3(0.2f, -3.6f, 0f));
        string who = fromWitness ? "Pêcheur" : "Villageoise";

        yield return Fade(1f, 0f, 1f);
        yield return TitleCard("La Guilde des Aventuriers", "Sur le continent, quelques jours plus tôt", 1.2f);
        if (actor != null)
        {
            camCtrl?.Shake(0.05f, 0.25f);
            Sfx.Play("door_break", null, 0.5f);
            StartCoroutine(MoveCamera(o + new Vector3(0f, -0.2f, 0f), 3.6f, 2.2f));
            yield return Walk(actor, o + new Vector3(0.2f, -0.55f, 0f), fromWitness ? 3.6f : 2.2f);
        }
        Transform t = actor != null ? actor.transform : room;
        Color visitor = new Color(0.75f, 0.9f, 1f), guild = new Color(1f, 0.82f, 0.45f);
        if (fromWitness)
        {
            yield return Say(t, who, visitor, "Maître ! Maître de guilde ! L'île... l'île du volcan !", 1.4f);
            yield return Say(master, "Maître de guilde", guild, "Calme-toi, l'ami. Reprends ton souffle, et raconte.", 1.4f);
            yield return Say(t, who, visitor, $"Une grotte, sous la montagne... On y a vu {told} !", 2.2f);
            yield return Say(t, who, visitor, "Et tout au fond, une lueur. Comme un cœur qui bat dans la roche.", 1.8f);
        }
        else
        {
            yield return Say(t, who, visitor, "Mon mari et ses frères sont partis pêcher près de l'île du volcan...", 1.6f);
            yield return Say(t, who, visitor, "Ils ne sont jamais revenus. Personne ne revient de là-bas.", 1.6f);
            yield return Say(master, "Maître de guilde", guild, $"Ce n'est pas la première barque... On parle de {told}.", 2f);
        }
        yield return Say(master, "Maître de guilde", guild, "Un donjon. Sur une île que personne ne réclame...", 1.6f);
        yield return Say(master, "Maître de guilde", guild, $"Qu'on prépare une équipe d'évaluation. Rang {GuildSystem.RankName(rank)}. Ils partent demain.", 1.6f);
        HideBubble();
        // l'ordre de mission est tamponné
        LayoutCards(300f, -80f, 110f);
        subText.text = $"Ordre de mission : évaluation — équipe de rang {GuildSystem.RankName(rank)}";
        yield return Stamp(rank);
        yield return FadeText(subText, 1f, 0.5f);
        yield return Wait(1.4f);
        StartCoroutine(FadeText(subText, 0f, 0.5f));
        yield return FadeText(stampText, 0f, 0.5f);
        yield return Say(t, who, visitor, fromWitness ? "Que les dieux les protègent..." : "Ramenez-les-moi... je vous en prie.", 1.4f);
        HideBubble();
        yield return Fade(0f, 1f, 0.8f);
        if (actor != null) Destroy(actor);
        interior.HideCinematic("house_4");
        ClearCards();
    }

    /// <summary>Un personnage de décor (sans IA ni combat), animé en temps réel.</summary>
    private static GameObject SpawnActor(GameObject prefab, Vector3 at)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, at, Quaternion.identity);
        var ai = go.GetComponent<AdventurerAI>(); if (ai != null) DestroyImmediate(ai);
        var c = go.GetComponent<AdventurerCombat>(); if (c != null) DestroyImmediate(c);
        var sk = go.GetComponent<Pathfinding.Seeker>(); if (sk != null) DestroyImmediate(sk);
        foreach (var col in go.GetComponentsInChildren<Collider2D>()) col.enabled = false;
        var rb = go.GetComponent<Rigidbody2D>(); if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        var anim = go.GetComponent<Animator>();
        if (anim != null) { anim.updateMode = AnimatorUpdateMode.UnscaledTime; anim.SetFloat("MoveY", 1f); }
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>()) sr.sortingOrder = Mathf.Max(sr.sortingOrder, 3);
        return go;
    }

    private IEnumerator Walk(GameObject actor, Vector3 to, float speed)
    {
        var anim = actor.GetComponent<Animator>();
        Vector3 from = actor.transform.position;
        Vector2 dir = (to - from).normalized;
        if (anim != null) { anim.SetFloat("Speed", 1f); anim.SetFloat("MoveX", dir.x); anim.SetFloat("MoveY", dir.y); }
        while (!skip && Vector2.Distance(actor.transform.position, to) > 0.02f)
        {
            actor.transform.position = Vector3.MoveTowards(actor.transform.position, to, speed * Time.unscaledDeltaTime);
            yield return null;
        }
        actor.transform.position = to;
        if (anim != null) { anim.SetFloat("Speed", 0f); anim.SetFloat("MoveX", 0f); anim.SetFloat("MoveY", 1f); }
    }

    // ── Bulles de dialogue ──
    private GameObject bubble;
    private TextMeshPro bubbleText;
    private Transform bubbleBg, bubbleTail;

    private void EnsureBubble()
    {
        if (bubble != null) return;
        bubble = new GameObject("Bulle");
        var bg = new GameObject("Fond").AddComponent<SpriteRenderer>();
        bg.transform.SetParent(bubble.transform, false);
        bg.sprite = SpriteUtil.Square;
        bg.color = new Color(0.06f, 0.05f, 0.1f, 0.92f);
        bg.sortingOrder = 60;
        bubbleBg = bg.transform;
        var tail = new GameObject("Pointe").AddComponent<SpriteRenderer>();
        tail.transform.SetParent(bubble.transform, false);
        tail.sprite = SpriteUtil.Diamond;
        tail.color = bg.color;
        tail.sortingOrder = 60;
        tail.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
        bubbleTail = tail.transform;
        bubbleText = new GameObject("Texte").AddComponent<TextMeshPro>();
        bubbleText.transform.SetParent(bubble.transform, false);
        bubbleText.rectTransform.sizeDelta = new Vector2(5.2f, 2f);
        bubbleText.fontSize = 2.3f;
        bubbleText.alignment = TextAlignmentOptions.Center;
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        bubbleText.color = new Color(0.95f, 0.94f, 1f);
        bubbleText.sortingOrder = 61;
        bubble.SetActive(false);
    }

    private void HideBubble() { if (bubble != null) bubble.SetActive(false); }

    /// <summary>Bulle au-dessus d'un personnage, texte qui s'écrit, puis temps de lecture.</summary>
    private IEnumerator Say(Transform who, string speaker, Color nameColor, string text, float hold)
    {
        EnsureBubble();
        if (who == null) yield break;
        bubble.SetActive(true);
        string full = $"<size=80%><color=#{ColorUtility.ToHtmlStringRGB(nameColor)}><b>{speaker}</b></color></size>\n{text}";
        bubbleText.text = full;
        bubbleText.ForceMeshUpdate();
        Vector2 size = bubbleText.GetRenderedValues(false);
        size = new Vector2(Mathf.Max(1.6f, size.x) + 0.45f, size.y + 0.32f);
        bubbleBg.localScale = new Vector3(size.x, size.y, 1f);
        // au-dessus de la tête, sans sortir du cadre
        Vector3 at = who.position + new Vector3(0f, 1.25f + size.y * 0.5f, 0f);
        if (cam != null)
        {
            float halfW = cam.orthographicSize * cam.aspect - size.x * 0.5f - 0.2f;
            at.x = Mathf.Clamp(at.x, cam.transform.position.x - halfW, cam.transform.position.x + halfW);
            // sous la bande noire du haut (120 px sur 1080)
            float top = cam.transform.position.y + cam.orthographicSize * (1f - 2f * 130f / 1080f) - size.y * 0.5f;
            at.y = Mathf.Min(at.y, top);
        }
        bubble.transform.position = at;
        bubbleTail.position = new Vector3(Mathf.Clamp(who.position.x, at.x - size.x * 0.4f, at.x + size.x * 0.4f), at.y - size.y * 0.5f, 0f);
        int total = bubbleText.textInfo.characterCount;
        for (int i = 0; i <= total && !skip; i++)
        {
            bubbleText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(0.02f);
        }
        bubbleText.maxVisibleCharacters = 99999;
        for (float k = 0f; k < hold && !skip; k += Time.unscaledDeltaTime) yield return null;
    }

    /// <summary>Le rapport de la Guilde : le donjon reçoit son rang.</summary>
    public void PlayGuildVerdict(int rank, string outcome, Action onEnd) =>
        Enqueue(() => GuildVerdictRoutine(rank, outcome, onEnd));

    private IEnumerator GuildVerdictRoutine(int rank, string outcome, Action onEnd)
    {
        Begin();
        GameManager.Instance?.PushPause();
        yield return Bars(true, 0.4f);
        yield return Fade(0f, 0.9f, 0.8f, new Color(0.05f, 0.03f, 0.08f));
        LayoutCards(300f, -80f, 110f);
        StartCoroutine(ShowCard(true, 0.6f));
        titleText.text = "Rapport d'évaluation";
        subText.text = "Guilde des Aventuriers — registre des donjons";
        StartCoroutine(FadeText(subText, 1f, 0.8f));
        yield return FadeText(titleText, 1f, 0.8f);
        yield return Caption(outcome, 2f);
        StartCoroutine(FadeText(subText, 0f, 0.4f));
        yield return Caption("Après délibération, la Guilde inscrit le donjon de l'île au registre :", 1f);
        yield return Stamp(rank);
        subText.text = $"Donjon de rang {GuildSystem.RankName(rank)} — {GuildSystem.DungeonRankDesc(rank)}";
        yield return FadeText(subText, 1f, 0.6f);
        yield return Wait(2.6f);
        StartCoroutine(FadeText(subText, 0f, 0.5f));
        StartCoroutine(FadeText(titleText, 0f, 0.5f));
        StartCoroutine(ShowCard(false, 0.5f));
        yield return FadeText(stampText, 0f, 0.5f);
        caption.text = "";
        yield return Fade(0.9f, 0f, 0.8f, new Color(0.05f, 0.03f, 0.08f));
        yield return Bars(false);
        ClearCards();
        End();
        GameManager.Instance?.PopPause();
        onEnd?.Invoke();
    }

    // ─────────────────────────────────────────────
    // COLONIE : CAMP, PORT, VILLAGE, VILLE
    // ─────────────────────────────────────────────

    public void PlayColonyStage(int stage, Action onEnd) => Enqueue(() => ColonyRoutine(stage, onEnd));

    private IEnumerator ColonyRoutine(int stage, Action onEnd)
    {
        var colony = SettlementManager.Instance;
        if (colony == null || colony.Site == null) { onEnd?.Invoke(); yield break; }
        Begin();
        GameManager.Instance?.PushPause();
        Vector3 back = cam != null ? cam.transform.position : Vector3.zero;
        float backSize = cam != null ? cam.orthographicSize : 8f;
        bool wasDungeon = MountainRelief.Instance == null || MountainRelief.Instance.UserDungeonView;
        MountainRelief.Instance?.SetUserView(false);

        Vector3 focus = colony.StageFocus(stage);
        Vector3 landing = colony.Site.LandingWorld;
        Vector3 town = colony.StageFocus(0);
        int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        yield return Bars(true);
        yield return Fade(0f, 1f, 0.6f);

        switch (stage)
        {
            case 1:
                if (cam != null) { cam.transform.position = new Vector3(landing.x, landing.y + 1f, cam.transform.position.z); cam.orthographicSize = 7f; }
                yield return Fade(1f, 0f, 1f);
                yield return Caption("Au petit matin, un navire aux couleurs de la Guilde jette l'ancre au large de l'île.", 2f);
                StartCoroutine(Caption("On décharge des caisses, on plante des tentes. Un feu s'allume sur la plage.", 2.4f));
                yield return MoveCamera(focus, 5.5f, 3f);
                Fx.Play("smoke_rise", focus + new Vector3(0.3f, 0.4f, 0f), 2.2f, 8f, 20, new Color(0.8f, 0.8f, 0.85f, 0.8f));
                yield return Wait(1f);
                yield return TitleCard("Camp de la Guilde", $"Jour {day} — les humains s'installent sur l'île", 1.8f);
                break;
            case 2:
                if (cam != null) { cam.transform.position = new Vector3(focus.x - 3f, focus.y + 2f, cam.transform.position.z); cam.orthographicSize = 7.5f; }
                yield return Fade(1f, 0f, 1f);
                StartCoroutine(Caption("Des charpentiers débarquent avec des planches, des pieux et des cordages.", 2.2f));
                yield return MoveCamera(focus, 5.5f, 2.6f);
                yield return Caption("Le ponton s'allonge vers le large : bientôt, les grands navires pourront accoster.", 2f);
                yield return TitleCard("Le Port", $"Jour {day} — l'île est reliée au continent", 1.8f);
                break;
            case 4:
                if (cam != null) { cam.transform.position = new Vector3(town.x, town.y, cam.transform.position.z); cam.orthographicSize = 10f; }
                yield return Fade(1f, 0f, 1f);
                yield return Caption("Des familles entières arrivent du continent, avec leurs outils et leurs bêtes.", 2f);
                StartCoroutine(Caption("On défriche, on laboure, on dresse des étals. Le hameau déborde de ses palissades.", 2.4f));
                yield return MoveCamera(focus, 6f, 3f);
                yield return TitleCard("Le Village", $"Jour {day} — un village vit au pied de la montagne", 1.8f);
                break;
            default:
                if (cam != null) { cam.transform.position = new Vector3(focus.x, focus.y, cam.transform.position.z); cam.orthographicSize = 5.5f; }
                yield return Fade(1f, 0f, 1f);
                yield return Caption("Armuriers, mages, maîtres d'armes : on vient désormais ici pour devenir aventurier.", 2.2f);
                StartCoroutine(Caption("Au pied du volcan, une ville entière vit du donjon... et rêve de sa chute.", 2.6f));
                yield return MoveCamera(new Vector3(town.x, town.y + 4f, 0f), 12f, 3.5f);
                yield return TitleCard("La Ville du Donjon", $"Jour {day} — la cité est née", 2f);
                break;
        }

        yield return Bars(false);
        ClearCards();
        if (cam != null) { cam.transform.position = back; cam.orthographicSize = backSize; }
        MountainRelief.Instance?.SetUserView(wasDungeon);
        End();
        GameManager.Instance?.PopPause();
        onEnd?.Invoke();
    }
}
