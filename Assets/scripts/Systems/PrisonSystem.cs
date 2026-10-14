using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class PrisonerSave
{
    public string name, prefab;
    public int role, rank = -1, tier, days;
    public float x, y;
}

/// <summary>
/// PRISONNIERS : quand les captures sont activées et qu'une cellule est libre, un intrus vaincu dans le donjon
/// est assommé et enfermé au lieu de mourir. Chaque matin il nourrit le cristal ; au bout de quelques jours il s'éteint.
/// On peut le LIBÉRER (la colonie apprécie), le rendre contre RANÇON (s'il y a une colonie) ou l'ABSORBER.
/// Tant que des prisonniers croupissent ici, la Guilde peut monter une expédition de sauvetage.
/// </summary>
public class PrisonSystem : MonoBehaviour
{
    public static PrisonSystem Instance { get; private set; }

    public bool captureEnabled;
    public int maxDays = 6;

    public readonly List<AdventurerAI> prisoners = new List<AdventurerAI>();
    private readonly Dictionary<AdventurerAI, int> days = new Dictionary<AdventurerAI, int>();
    /// <summary>Sauvetage en préparation : membres en plus à la prochaine expédition.</summary>
    public int RescueBonus { get; private set; }

    private Canvas canvas;
    private Button captureBtn, listBtn;
    private RectTransform modal;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // ─────────────────────────────────────────────
    // CAPTURE
    // ─────────────────────────────────────────────

    /// <summary>Appelé à la « mort » d'un intrus : true s'il est capturé à la place.</summary>
    public bool TryCapture(AdventurerAI a)
    {
        if (!captureEnabled || a == null || !a.IsInsideDungeon || a.IsPrisoner) return false;
        var cell = PrisonCell.All.Find(c => c != null && c.Free && c.isActiveAndEnabled);
        if (cell == null) return false;
        Imprison(a, cell, 0);
        GameHUD.Toast($"{a.DisplayName} est assommé et jeté en cellule", GameHUD.ManaColor);
        Sfx.Play("trap_snap", cell.transform.position, 0.8f);
        // pour le monde extérieur, il a disparu
        GuildSystem.Instance?.NoteDeath(a);
        ExpeditionManager.Instance?.OnMemberGone(a, true);
        StoryManager.Instance?.OnFirstPrisoner(a);
        return true;
    }

    private void Imprison(AdventurerAI a, PrisonCell cell, int dayCount)
    {
        cell.occupant = a;
        a.BecomePrisoner(cell.Slot);
        prisoners.Add(a);
        days[a] = dayCount;
    }

    private void OnNewDay(int day)
    {
        prisoners.RemoveAll(p => p == null);
        if (prisoners.Count == 0) { RescueBonus = 0; return; }
        int mana = 0;
        foreach (var p in prisoners.ToArray())
        {
            int r = Mathf.Max(0, p.Rank);
            mana += Mathf.RoundToInt((25 + 12 * r) * UpkeepSystem.DayScale);
            days[p] = days.TryGetValue(p, out int d) ? d + 1 : 1;
            if (days[p] >= maxDays)
            {
                GameHUD.Toast($"{p.DisplayName} s'éteint dans sa cellule", GameHUD.InfoColor);
                ManaSystem.Instance?.Add(30 + 20 * r);
                Remove(p);
            }
        }
        ManaSystem.Instance?.Add(mana);
        GameHUD.Toast($"Vos prisonniers nourrissent le cristal : +{mana} mana", GameHUD.ManaColor);
        // la Guilde n'abandonne pas les siens... en général
        if (RescueBonus == 0 && UnityEngine.Random.value < 0.25f)
        {
            RescueBonus = 2;
            GameHUD.Toast("Des rumeurs parlent d'un sauvetage...", GameHUD.WarningColor);
        }
    }

    /// <summary>Consommé par la prochaine expédition (membres supplémentaires).</summary>
    public int TakeRescueBonus()
    {
        int b = RescueBonus;
        RescueBonus = 0;
        return b;
    }

    private void Remove(AdventurerAI p)
    {
        foreach (var c in PrisonCell.All) if (c.occupant == p) c.occupant = null;
        prisoners.Remove(p);
        days.Remove(p);
        if (p != null) Destroy(p.gameObject);
    }

    // ─────────────────────────────────────────────
    // DÉCISIONS
    // ─────────────────────────────────────────────

    public void Release(AdventurerAI p, bool escaped = false)
    {
        if (p == null) return;
        Fx.Play("shine", p.transform.position + Vector3.up * 0.3f, 1.2f, 12f, 17);
        if (!escaped)
        {
            if (RelationSystem.Instance != null) RelationSystem.Instance.relation = Mathf.Min(100f, RelationSystem.Instance.relation + 8f);
            if (ReputationSystem.Instance != null) ReputationSystem.Instance.renown += 3f;
            if (GuildSystem.Instance != null) GuildSystem.Instance.hostility = Mathf.Max(0f, GuildSystem.Instance.hostility - 0.05f);
            GameHUD.Toast($"{p.DisplayName} est libéré : on parlera de votre clémence", GameHUD.InfoColor);
        }
        else GameHUD.Toast($"{p.DisplayName} s'est échappé !", GameHUD.WarningColor);
        Remove(p);
    }

    public void Ransom(AdventurerAI p)
    {
        if (p == null) return;
        var colony = SettlementManager.Instance;
        if (colony == null || colony.stage == 0) { GameHUD.Toast("Aucune colonie pour payer la rançon", GameHUD.WarningColor); return; }
        int r = Mathf.Max(0, p.Rank);
        int mana = 80 + 40 * r;
        ManaSystem.Instance?.Add(mana);
        if (RelationSystem.Instance != null) RelationSystem.Instance.relation = Mathf.Min(100f, RelationSystem.Instance.relation + 3f);
        GameHUD.Toast($"La colonie paie la rançon de {p.DisplayName} : +{mana} mana", GameHUD.GoldColor);
        Sfx.Play("coin");
        Remove(p);
    }

    public void Absorb(AdventurerAI p)
    {
        if (p == null) return;
        int r = Mathf.Max(0, p.Rank);
        int mana = 60 + 45 * r + 8 * r * r;
        ManaSystem.Instance?.Add(mana);
        FloatingText.Spawn(p.transform.position + Vector3.up * 0.6f, $"+{mana} mana", GameHUD.ManaColor, 1.4f);
        Fx.Play("smoke_skull", p.transform.position + Vector3.up * 0.3f, 1.2f, 14f, 17);
        Sfx.Play("death_human", p.transform.position);
        if (GuildSystem.Instance != null) GuildSystem.Instance.hostility = Mathf.Min(1f, GuildSystem.Instance.hostility + 0.04f);
        Remove(p);
    }

    // ─────────────────────────────────────────────
    // INTERFACE
    // ─────────────────────────────────────────────

    private void BuildUI()
    {
        var go = new GameObject("Prison_Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();
        var root = UIKit.Stretch(UIKit.Rect("Root", go.transform));
        captureBtn = UIKit.BtnAt(root, "Captures : non", () => { captureEnabled = !captureEnabled; Refresh(); },
            new Vector2(0, 1), new Vector2(16, -252), new Vector2(270, 52), 20);
        listBtn = UIKit.BtnAt(root, "Prisonniers", ShowList, new Vector2(0, 1), new Vector2(296, -252), new Vector2(280, 52), 20, new Color(0.3f, 0.2f, 0.36f, 0.96f));
        ((RectTransform)captureBtn.transform).pivot = ((RectTransform)listBtn.transform).pivot = new Vector2(0, 1);
    }

    private void Refresh()
    {
        UIKit.SetLabel(captureBtn, captureEnabled ? "Captures : oui" : "Captures : non");
        captureBtn.GetComponent<Image>().color = captureEnabled ? new Color(0.45f, 0.3f, 0.6f, 0.96f) : UIKit.Card;
        prisoners.RemoveAll(p => p == null);
        UIKit.SetLabel(listBtn, $"Prisonniers ({prisoners.Count})");
    }

    private float nextRefresh;
    private void Update()
    {
        if (canvas == null) return;
        bool hud = !CrawlerMode.Active && GameHUD.Instance != null && GameHUD.Instance.HudVisible && (MainMenu.Instance == null || !MainMenu.Instance.InHome);
        bool anyCell = PrisonCell.All.Count > 0;
        captureBtn.gameObject.SetActive(hud && anyCell);
        listBtn.gameObject.SetActive(hud && prisoners.Count > 0);
        if (Time.unscaledTime > nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; Refresh(); }
    }

    private void CloseModal()
    {
        if (modal != null) Destroy(modal.gameObject);
        modal = null;
        GameManager.Instance?.PopPause();
    }

    private void ShowList()
    {
        if (modal != null) { CloseModal(); return; }
        GameManager.Instance?.PushPause();
        modal = UIKit.Stretch(UIKit.Rect("Modal", canvas.transform));
        UIKit.Blocker(modal, 0.5f);
        var box = UIKit.PanelBox("Box", modal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 180 + 110 * Mathf.Max(1, prisoners.Count)));
        box.pivot = new Vector2(0.5f, 0.5f);
        UIKit.LabelAt(box, "Prisonniers", 40, UIKit.Gold, new Vector2(32, -22), new Vector2(700, 56));
        UIKit.BtnAt(box, "Fermer", CloseModal, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(130, 50), 22);
        UIKit.LabelAt(box, "Chaque matin, ils nourrissent le cristal. Tant qu'ils sont ici, la Guilde peut tenter un sauvetage.", 20, UIKit.Sub, new Vector2(32, -84), new Vector2(1040, 30));
        float y = 130f;
        bool colony = SettlementManager.Instance != null && SettlementManager.Instance.stage > 0;
        foreach (var p in prisoners.ToArray())
        {
            if (p == null) continue;
            var card = UIKit.PanelBox("P", box, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(1040, 96), UIKit.Card);
            card.pivot = new Vector2(0.5f, 1);
            int d = days.TryGetValue(p, out int dd) ? dd : 0;
            UIKit.LabelAt(card, $"{p.DisplayName}\n<size=75%><color=#9A96B0>en cellule depuis {d} jour(s) · s'éteindra dans {maxDays - d} jour(s)</color></size>", 22, UIKit.Text, new Vector2(18, -12), new Vector2(560, 76));
            var who = p;
            UIKit.BtnAt(card, "Absorber", () => { Absorb(who); CloseModal(); }, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(140, 58), 20, new Color(0.45f, 0.15f, 0.2f, 0.96f));
            UIKit.BtnAt(card, "Rançon", () => { Ransom(who); CloseModal(); }, new Vector2(1, 0.5f), new Vector2(-168, 0), new Vector2(140, 58), 20, colony ? new Color(0.5f, 0.4f, 0.12f, 0.96f) : UIKit.Disabled);
            UIKit.BtnAt(card, "Libérer", () => { Release(who); CloseModal(); }, new Vector2(1, 0.5f), new Vector2(-320, 0), new Vector2(140, 58), 20, new Color(0.2f, 0.4f, 0.3f, 0.96f));
            y += 110f;
        }
    }

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    public void Capture(SaveData d)
    {
        d.captureEnabled = captureEnabled;
        d.prisoners = new List<PrisonerSave>();
        foreach (var p in prisoners)
        {
            if (p == null) continue;
            d.prisoners.Add(new PrisonerSave
            {
                name = p.combat.adventurerName, prefab = p.name.Replace("(Clone)", "").Trim(), role = (int)p.role, rank = p.Rank, tier = p.TierIndex,
                days = days.TryGetValue(p, out int dd) ? dd : 0, x = p.transform.position.x, y = p.transform.position.y,
            });
        }
    }

    public void Restore(SaveData d)
    {
        captureEnabled = d.captureEnabled;
        if (d.prisoners == null) return;
        foreach (var s in d.prisoners)
        {
            var prefab = NemesisSystem.FindPrefab(s.prefab);
            if (prefab == null) continue;
            PrisonCell best = null;
            float bd = 1.5f;
            foreach (var c in PrisonCell.All)
            {
                float dist = Vector2.Distance(c.Slot, new Vector2(s.x, s.y));
                if (c.Free && dist < bd) { bd = dist; best = c; }
            }
            if (best == null) continue;
            var go = Instantiate(prefab, best.Slot, Quaternion.identity);
            var ai = go.GetComponent<AdventurerAI>();
            if (ai == null) { Destroy(go); continue; }
            ai.role = (AdventurerAI.Role)s.role;
            ai.ApplyRank(s.rank);
            ai.combat.adventurerName = s.name;
            Imprison(ai, best, s.days);
        }
    }
}
