using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sélection et commandement des monstres (outil "Sélection").
///
///   Tap / clic sur un gobelin   → sélectionner (Shift = ajouter)
///   Glisser (PC)                → sélection de zone
///   Tap / clic sur le sol       → déplacer la sélection ici
///   Tap sur une ancre           → rattacher la sélection / sélectionner l'ancre
///   Ancre sélectionnée + tap    → déplacer l'ancre
///   P / H                       → Patrouille / Tenir la position
///   ESC                         → désélectionner
/// </summary>
public class MonsterSelector : MonoBehaviour
{
    public static MonsterSelector Instance { get; private set; }

    [Header("Layers")]
    public LayerMask monsterLayer;
    public LayerMask anchorLayer;

    [Header("Selection Visual")]
    public Color selectionColor = Color.green;
    public float selectionCircleRadius = 0.7f;

    [Header("Box Selection")]
    public Color boxColor = new Color(0, 1, 0, 0.3f);
    public Color boxBorderColor = Color.green;

    [Header("Move Target Marker")]
    public float moveMarkerDuration = 0.8f;

    [Tooltip("Distance (pixels) au-delà de laquelle un appui devient un glissement")]
    public float dragThresholdPixels = 18f;

    private readonly List<GobelinAI> selectedGoblins = new List<GobelinAI>();
    private PatrolAnchor selectedAnchor;

    public IReadOnlyList<GobelinAI> SelectedGoblins
    {
        get
        {
            selectedGoblins.RemoveAll(g => g == null);
            return selectedGoblins;
        }
    }
    public PatrolAnchor SelectedAnchor => selectedAnchor;

    private bool pressActive;
    private bool isDragging;
    private Vector2 pressScreenPos;
    private Vector2 boxStartScreen, boxEndScreen;

    private readonly List<GameObject> goblinIndicators = new List<GameObject>();
    private GameObject anchorIndicator;
    private GameObject moveMarker;
    private float moveMarkerTimer;
    private Material lineMaterial;

    private void Awake()
    {
        Instance = this;
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        CleanupAllIndicators();
    }

    private bool IsActiveTool =>
        (DungeonBuilder.Instance == null || DungeonBuilder.Instance.GetCurrentMode() == DungeonBuilder.BuildMode.None) &&
        (PossessionController.Instance == null || !PossessionController.Instance.IsPossessing) &&
        (GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Playing);

    private void Update()
    {
        var keyboard = Keyboard.current;

        if (!IsActiveTool)
        {
            pressActive = false;
            isDragging = false;
            UpdateSelectionIndicators();
            return;
        }

        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame) DeselectAll();
            if (selectedGoblins.Count > 0)
            {
                if (keyboard.pKey.wasPressedThisFrame) SetSelectedBehavior(GobelinAI.GoblinBehavior.Patrol);
                else if (keyboard.hKey.wasPressedThisFrame) SetSelectedBehavior(GobelinAI.GoblinBehavior.Stay);
            }
        }

        HandlePointer();

        if (moveMarker != null)
        {
            moveMarkerTimer -= Time.unscaledDeltaTime;
            if (moveMarkerTimer <= 0f)
            {
                Destroy(moveMarker);
                moveMarker = null;
            }
        }

        UpdateSelectionIndicators();
    }

    // ═══════════════════════════════════════════
    // POINTEUR (souris / tactile)
    // ═══════════════════════════════════════════

    private void HandlePointer()
    {
        if (!PointerUtil.Available) return;

        if (PointerUtil.PressedThisFrame)
        {
            if (PointerUtil.IsOverUI() || PointerUtil.TouchCount > 1) return;
            pressActive = true;
            isDragging = false;
            pressScreenPos = PointerUtil.ScreenPos;
        }

        if (!pressActive) return;

        if (PointerUtil.TouchCount > 1)
        {
            // Geste à deux doigts = caméra
            pressActive = false;
            isDragging = false;
            return;
        }

        if (PointerUtil.Pressed && !isDragging &&
            Vector2.Distance(PointerUtil.ScreenPos, pressScreenPos) > dragThresholdPixels)
        {
            isDragging = true;
            boxStartScreen = pressScreenPos;
        }

        if (isDragging) boxEndScreen = PointerUtil.ScreenPos;

        if (PointerUtil.ReleasedThisFrame)
        {
            pressActive = false;
            if (isDragging)
            {
                // Sur tactile, glisser = déplacer la caméra (géré par CameraController)
                if (!PointerUtil.IsTouch) CompleteBoxSelection();
                isDragging = false;
            }
            else
            {
                HandleTap(PointerUtil.ScreenToWorld(pressScreenPos));
            }
        }
    }

    private bool ShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

    private void HandleTap(Vector2 worldPos)
    {
        PatrolAnchor clickedAnchor = GetAnchorAtPosition(worldPos);
        if (clickedAnchor != null)
        {
            HandleAnchorClick(clickedAnchor);
            return;
        }

        GobelinAI clickedGoblin = GetGoblinAtPosition(worldPos);
        if (clickedGoblin != null)
        {
            HandleGoblinClick(clickedGoblin, ShiftHeld);
            return;
        }

        // un intrus touché : sa fiche (traits observés, faim, ce qu'il fait)
        if (selectedGoblins.Count == 0)
        {
            AdventurerAI tapped = null;
            float bd = 0.7f;
            foreach (var adv in AdventurerAI.All)
            {
                if (adv == null || adv.IsDead) continue;
                float d = Vector2.Distance((Vector2)adv.transform.position + Vector2.up * 0.3f, worldPos);
                if (d < bd) { bd = d; tapped = adv; }
            }
            if (tapped != null) { IntruderCard.Show(tapped); return; }
        }

        if (selectedGoblins.Count > 0)
        {
            OrderMoveToPosition(worldPos);
            return;
        }

        if (selectedAnchor != null)
        {
            if (DungeonTerritory.Instance == null || DungeonTerritory.Instance.IsInside(worldPos))
            {
                selectedAnchor.transform.position = worldPos;
                ShowMoveMarker(worldPos);
            }
            else GameHUD.Toast("L'ancre doit rester dans le donjon", GameHUD.WarningColor);
            return;
        }

        DeselectAll();
    }

    private void HandleGoblinClick(GobelinAI goblin, bool addToSelection)
    {
        if (selectedGoblins.Contains(goblin))
        {
            selectedGoblins.Remove(goblin);
            return;
        }
        if (!addToSelection)
        {
            selectedGoblins.Clear();
            selectedAnchor = null;
        }
        selectedGoblins.Add(goblin);
    }

    private void HandleAnchorClick(PatrolAnchor anchor)
    {
        if (selectedGoblins.Count > 0)
        {
            RattachGoblinsToAnchor(anchor);
        }
        else
        {
            selectedAnchor = (selectedAnchor == anchor) ? null : anchor;
        }
    }

    private void CompleteBoxSelection()
    {
        Rect selectionRect = GetSelectionRect();
        if (!ShiftHeld) selectedGoblins.Clear();
        selectedAnchor = null;

        foreach (var goblin in GobelinAI.All)
        {
            if (goblin == null) continue;
            Vector2 screenPos = PointerUtil.Cam.WorldToScreenPoint(goblin.transform.position);
            if (selectionRect.Contains(screenPos) && !selectedGoblins.Contains(goblin))
                selectedGoblins.Add(goblin);
        }
    }

    // ═══════════════════════════════════════════
    // ORDRES (aussi appelés par le HUD)
    // ═══════════════════════════════════════════

    public void OrderMoveToPosition(Vector2 targetPos)
    {
        if (selectedGoblins.Count == 0) return;
        if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInside(targetPos))
        {
            GameHUD.Toast("Les monstres ne peuvent pas quitter le donjon", GameHUD.WarningColor);
            return;
        }

        ShowMoveMarker(targetPos);
        int count = selectedGoblins.Count;

        // Le groupe sélectionné reçoit sa propre ancre de patrouille à destination
        var anchorGo = new GameObject($"Anchor_{System.Guid.NewGuid().ToString().Substring(0, 6)}");
        anchorGo.transform.position = targetPos;
        var parent = GameObject.Find("PatrolAnchors");
        if (parent != null) anchorGo.transform.SetParent(parent.transform);
        var newAnchor = anchorGo.AddComponent<PatrolAnchor>();
        foreach (var g in selectedGoblins)
        {
            if (g == null) continue;
            var old = g.anchorPoint != null ? g.anchorPoint.GetComponent<PatrolAnchor>() : null;
            g.anchorPoint = newAnchor.transform;
            newAnchor.RegisterGoblin(g);
            if (old != null && old != newAnchor) old.UnregisterGoblin(g);
        }
        for (int i = 0; i < count; i++)
        {
            GobelinAI goblin = selectedGoblins[i];
            if (goblin == null) continue;

            Vector2 offset = Vector2.zero;
            if (count > 1)
            {
                float angle = (360f / count) * i * Mathf.Deg2Rad;
                float spread = Mathf.Min(0.4f * (count - 1), 1.2f);
                offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spread;
                if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInside(targetPos + offset))
                    offset = Vector2.zero;
            }
            goblin.MoveToPosition(targetPos + offset);
        }
    }

    public void SetSelectedBehavior(GobelinAI.GoblinBehavior behavior)
    {
        foreach (var goblin in selectedGoblins)
            if (goblin != null) goblin.SetBehavior(behavior);
        GameHUD.Toast(behavior == GobelinAI.GoblinBehavior.Stay ? "Ordre : tenir la position" : "Ordre : patrouiller", GameHUD.InfoColor);
    }

    public void SelectAllGoblins()
    {
        selectedAnchor = null;
        selectedGoblins.Clear();
        foreach (var g in GobelinAI.All) if (g != null) selectedGoblins.Add(g);
    }

    private void RattachGoblinsToAnchor(PatrolAnchor newAnchor)
    {
        foreach (var goblin in selectedGoblins)
        {
            if (goblin == null) continue;
            if (goblin.anchorPoint != null)
            {
                PatrolAnchor oldAnchor = goblin.anchorPoint.GetComponent<PatrolAnchor>();
                if (oldAnchor != null && oldAnchor != newAnchor) oldAnchor.UnregisterGoblin(goblin);
            }
            goblin.anchorPoint = newAnchor.transform;
            newAnchor.RegisterGoblin(goblin);
            goblin.SetBehavior(GobelinAI.GoblinBehavior.Patrol);
        }
        GameHUD.Toast($"{selectedGoblins.Count} gobelin(s) rattaché(s) à l'ancre", GameHUD.InfoColor);
        DeselectAll();
    }

    // ═══════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════

    private GobelinAI GetGoblinAtPosition(Vector2 worldPos)
    {
        // Tolérance généreuse pour le tactile
        float radius = PointerUtil.IsTouch ? 0.7f : 0.45f;
        GobelinAI best = null;
        float bestD = radius;
        foreach (var g in GobelinAI.All)
        {
            if (g == null) continue;
            float d = Vector2.Distance(worldPos, (Vector2)g.transform.position + Vector2.up * 0.2f);
            if (d < bestD) { bestD = d; best = g; }
        }
        return best;
    }

    private PatrolAnchor GetAnchorAtPosition(Vector2 worldPos)
    {
        float closestDist = PointerUtil.IsTouch ? 0.5f : 0.35f;
        PatrolAnchor closest = null;
        foreach (var anchor in FindObjectsByType<PatrolAnchor>(FindObjectsSortMode.None))
        {
            float dist = Vector2.Distance(worldPos, anchor.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = anchor;
            }
        }
        return closest;
    }

    private Rect GetSelectionRect()
    {
        float minX = Mathf.Min(boxStartScreen.x, boxEndScreen.x);
        float minY = Mathf.Min(boxStartScreen.y, boxEndScreen.y);
        return new Rect(minX, minY, Mathf.Abs(boxStartScreen.x - boxEndScreen.x), Mathf.Abs(boxStartScreen.y - boxEndScreen.y));
    }

    public void DeselectAll()
    {
        selectedGoblins.Clear();
        selectedAnchor = null;
    }

    // ═══════════════════════════════════════════
    // INDICATEURS VISUELS
    // ═══════════════════════════════════════════

    private void UpdateSelectionIndicators()
    {
        selectedGoblins.RemoveAll(g => g == null);

        while (goblinIndicators.Count > selectedGoblins.Count)
        {
            int last = goblinIndicators.Count - 1;
            if (goblinIndicators[last] != null) Destroy(goblinIndicators[last]);
            goblinIndicators.RemoveAt(last);
        }
        while (goblinIndicators.Count < selectedGoblins.Count)
            goblinIndicators.Add(CreateSelectionCircle(selectionColor, selectionCircleRadius));

        for (int i = 0; i < selectedGoblins.Count; i++)
        {
            GobelinAI goblin = selectedGoblins[i];
            goblinIndicators[i].SetActive(true);
            goblinIndicators[i].transform.position = goblin.transform.position;
            SetLineRendererColor(goblinIndicators[i], BehaviorColor(goblin.currentBehavior));
        }

        if (selectedAnchor != null)
        {
            if (anchorIndicator == null) anchorIndicator = CreateSelectionSquare();
            anchorIndicator.SetActive(true);
            anchorIndicator.transform.position = selectedAnchor.transform.position;
        }
        else if (anchorIndicator != null)
        {
            anchorIndicator.SetActive(false);
        }
    }

    private Color BehaviorColor(GobelinAI.GoblinBehavior behavior)
    {
        switch (behavior)
        {
            case GobelinAI.GoblinBehavior.Stay: return Color.red;
            case GobelinAI.GoblinBehavior.MoveTo: return Color.cyan;
            default: return selectionColor;
        }
    }

    private static void SetLineRendererColor(GameObject obj, Color color)
    {
        LineRenderer lr = obj.GetComponent<LineRenderer>();
        if (lr != null) { lr.startColor = color; lr.endColor = color; }
    }

    private void CleanupAllIndicators()
    {
        foreach (var ind in goblinIndicators) if (ind != null) Destroy(ind);
        goblinIndicators.Clear();
        if (anchorIndicator != null) Destroy(anchorIndicator);
        if (moveMarker != null) Destroy(moveMarker);
    }

    private LineRenderer NewLine(GameObject obj, int points, Color color, int order)
    {
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.positionCount = points;
        lr.startWidth = 0.06f;
        lr.endWidth = 0.06f;
        lr.material = lineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = order;
        lr.useWorldSpace = false;
        return lr;
    }

    private GameObject CreateSelectionCircle(Color color, float radius)
    {
        var obj = new GameObject("SelectionIndicator_Circle");
        const int segments = 32;
        var lr = NewLine(obj, segments + 1, color, 100);
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.6f, 0));
        }
        return obj;
    }

    private GameObject CreateSelectionSquare()
    {
        var obj = new GameObject("SelectionIndicator_Square");
        var lr = NewLine(obj, 5, Color.yellow, 100);
        const float s = 0.45f;
        lr.SetPosition(0, new Vector3(-s, -s, 0));
        lr.SetPosition(1, new Vector3(s, -s, 0));
        lr.SetPosition(2, new Vector3(s, s, 0));
        lr.SetPosition(3, new Vector3(-s, s, 0));
        lr.SetPosition(4, new Vector3(-s, -s, 0));
        return obj;
    }

    private void ShowMoveMarker(Vector2 worldPos)
    {
        if (moveMarker != null) Destroy(moveMarker);
        moveMarker = new GameObject("MoveMarker");
        moveMarker.transform.position = worldPos;

        var lr1 = NewLine(moveMarker, 2, Color.cyan, 101);
        lr1.SetPosition(0, new Vector3(-0.3f, 0, 0));
        lr1.SetPosition(1, new Vector3(0.3f, 0, 0));

        var crossV = new GameObject("CrossV");
        crossV.transform.SetParent(moveMarker.transform, false);
        var lr2 = NewLine(crossV, 2, Color.cyan, 101);
        lr2.SetPosition(0, new Vector3(0, -0.3f, 0));
        lr2.SetPosition(1, new Vector3(0, 0.3f, 0));

        moveMarkerTimer = moveMarkerDuration;
    }

    // Rectangle de sélection de zone (PC)
    private void OnGUI()
    {
        if (!isDragging || PointerUtil.IsTouch) return;
        Rect r = GetSelectionRect();
        // OnGUI a l'origine en haut à gauche
        Rect gui = new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);
        GUI.color = boxColor;
        GUI.DrawTexture(gui, Texture2D.whiteTexture);
        GUI.color = boxBorderColor;
        GUI.DrawTexture(new Rect(gui.x, gui.y, gui.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x, gui.yMax - 2, gui.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x, gui.y, 2, gui.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.xMax - 2, gui.y, 2, gui.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
