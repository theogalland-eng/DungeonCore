using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

/// <summary>
/// Construction du donjon par le Noyau.
///
/// Le panneau de droite (SidePanel) sélectionne l'outil ou l'élément du catalogue.
/// Raccourcis PC :
///   Échap      Sélection (commander les monstres)
///   1          Creuser     : marquer la roche à creuser (glisser pour plusieurs cases)
///   2          Remblayer   : reboucher une case de sol
///   3          Gomme       : retirer un objet / décor / marque de creusage
///   PgUp/PgDn  Changer d'étage affiché
///   Clic droit (sans glisser) : revenir à la sélection
/// </summary>
public class DungeonBuilder : MonoBehaviour
{
    public static DungeonBuilder Instance { get; private set; }

    [Header("Tilemaps")]
    [Tooltip("Tilemap des murs de PIERRE (creusables)")]
    public Tilemap stoneTilemap;
    [Tooltip("Tilemap des murs PLACÉS par le joueur (décor)")]
    public Tilemap placedWallTilemap;
    [Tooltip("Tilemap du sol")]
    public Tilemap floorTilemap;
    [Tooltip("Transform parent pour les objets placés")]
    public Transform objectsParent;

    [Header("Placed Wall Tiles - Auto-Tiling")]
    public TileBase wallConnectedNorth;
    public TileBase wallConnectedSouth;
    public TileBase wallConnectedWest;
    public TileBase wallConnectedEast;
    public TileBase wallCornerNW;
    public TileBase wallCornerNE;
    public TileBase wallCornerSW;
    public TileBase wallCornerSE;
    public TileBase wallIsolated;

    [Header("Floor / Stone Tiles")]
    public TileBase floorTile;
    [Tooltip("Tile de roche pour remblayer (auto : la plus fréquente de la tilemap de pierre)")]
    public TileBase stoneTile;

    [Header("Objets plaçables")]
    public GameObject trapPrefab;
    public GameObject doorPrefab;
    public GameObject towerPrefab;

    [Header("Costs & Settings")]
    public int miningCost = 10;
    public float miningTime = 1f;
    public int wallPlacementCost = 5;
    public int trapPlacementCost = 20;
    public int fillCost = 5;
    [Range(0f, 1f)] public float refundRatio = 0.5f;

    [Header("Déplacement du cœur")]
    [Tooltip("Coût du déplacement du cœur, en part de la réserve MAX de mana")]
    [Range(0.1f, 1f)] public float moveCoreCostRatio = 0.9f;

    [Header("UI Reference")]
    public DungeonBuilderUI uiController;

    // L'ordre des premières valeurs est conservé (compatibilité SetModeFromUI)
    public enum BuildMode
    {
        None,          // Sélection
        PlacingWall,   // Décor
        PlacingFloor,
        PlacingTrap,
        PlacingDoor,
        PlacingTower,
        Erasing,
        Digging,
        Filling,
        Summoning,
        PlacingItem,    // élément du catalogue (monstre, piège, décor)
        PlacingStairs,  // escalier vers l'étage inférieur
        MovingCore      // déplacer le cœur (coûte presque toute la réserve)
    }
    private BuildMode currentMode = BuildMode.None;

    public event System.Action<BuildMode> OnModeChanged;

    // Creusage
    private readonly List<Vector3Int> digQueue = new List<Vector3Int>();
    private readonly Dictionary<Vector3Int, SpriteRenderer> digMarks = new Dictionary<Vector3Int, SpriteRenderer>();
    private bool isMining;
    private float miningProgress;
    private Vector3Int currentMiningCell;
    private bool dragMarking = true;
    private Vector3Int lastDragCell = new Vector3Int(int.MinValue, 0, 0);

    // Pointeur
    private bool pressActive;
    private bool pressIsDrag;
    private Vector2 pressScreenPos;
    private Vector2 rightPressPos;
    private const float TapThreshold = 18f;

    // Curseur de case
    private SpriteRenderer cellCursor;

    /// <summary>Élément du catalogue sélectionné (mode PlacingItem).</summary>
    public CatalogItem SelectedItem { get; private set; }

    public int QueuedDigCount => digQueue.Count;
    public bool IsMining => isMining;
    public float MiningProgress01 => isMining ? Mathf.Clamp01(miningProgress / EffectiveMiningTime) : 0f;

    private float EffectiveMiningTime
    {
        get
        {
            int lvl = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
            return miningTime / (1f + 0.15f * (lvl - 1));
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (objectsParent == null)
            objectsParent = new GameObject("DungeonObjects").transform;

        if (stoneTile == null) stoneTile = FindMostCommonTile(stoneTilemap);
        // Les murs passent devant les objets plats du sol (pics, toiles, décors au sol)
        var wallRenderer = stoneTilemap != null ? stoneTilemap.GetComponent<TilemapRenderer>() : null;
        if (wallRenderer != null && wallRenderer.sortingOrder < 2) wallRenderer.sortingOrder = 2;
        // murs du bord des galeries triés avec les personnages : ce qui se tient derrière un mur est masqué par lui
        WallOcclusion.Create(stoneTilemap, placedWallTilemap);

        var cursorGo = new GameObject("CellCursor");
        cellCursor = cursorGo.AddComponent<SpriteRenderer>();
        cellCursor.sprite = SpriteUtil.Square;
        cellCursor.sortingOrder = 30;
        cellCursor.enabled = false;

        UpdateUI();
    }

    private static TileBase FindMostCommonTile(Tilemap tm)
    {
        if (tm == null) return null;
        var counts = new Dictionary<TileBase, int>();
        TileBase best = null;
        int bestN = 0;
        foreach (var p in tm.cellBounds.allPositionsWithin)
        {
            var t = tm.GetTile(p);
            if (t == null) continue;
            counts.TryGetValue(t, out int n);
            counts[t] = ++n;
            if (n > bestN) { bestN = n; best = t; }
        }
        return best;
    }

    private bool InputBlocked =>
        (PossessionController.Instance != null && PossessionController.Instance.IsPossessing) ||
        (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing);

    private void Update()
    {
        // une attaque commence : on range les outils
        if (currentMode != BuildMode.None && UnderAttack) { AttackRefusal(); SetMode(BuildMode.None); }
        if (!InputBlocked)
        {
            HandleKeyboard();
            HandlePointer();
        }
        else
        {
            pressActive = false;
        }

        UpdateCursor();
        ProcessDigQueue();
    }

    // ─────────────────────────────────────────────
    // INPUT
    // ─────────────────────────────────────────────

    private void HandleKeyboard()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) SetMode(BuildMode.Digging);
        else if (kb.digit2Key.wasPressedThisFrame) SetMode(BuildMode.Filling);
        else if (kb.digit3Key.wasPressedThisFrame) SetMode(BuildMode.Erasing);
        else if (kb.escapeKey.wasPressedThisFrame) SetMode(BuildMode.None);
        else if (kb.pageDownKey.wasPressedThisFrame && DungeonLayers.Instance != null)
            DungeonLayers.Instance.SetView(DungeonLayers.Instance.CurrentView + 1);
        else if (kb.pageUpKey.wasPressedThisFrame && DungeonLayers.Instance != null)
            DungeonLayers.Instance.SetView(DungeonLayers.Instance.CurrentView - 1);
    }

    private void HandlePointer()
    {
        // Clic droit sans glisser = retour à la sélection (PC)
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame) rightPressPos = mouse.position.ReadValue();
            if (mouse.rightButton.wasReleasedThisFrame &&
                Vector2.Distance(rightPressPos, mouse.position.ReadValue()) < TapThreshold &&
                currentMode != BuildMode.None)
                SetMode(BuildMode.None);
        }

        if (currentMode == BuildMode.None || !PointerUtil.Available) return;

        if (PointerUtil.PressedThisFrame)
        {
            if (PointerUtil.IsOverUI() || PointerUtil.TouchCount > 1) return;
            pressActive = true;
            pressIsDrag = false;
            pressScreenPos = PointerUtil.ScreenPos;
            lastDragCell = new Vector3Int(int.MinValue, 0, 0);

            if (currentMode == BuildMode.Digging)
            {
                Vector3Int c = CellAtPointer();
                dragMarking = !digMarks.ContainsKey(c);
                ApplyDigMark(c);
            }
        }

        if (!pressActive) return;

        if (PointerUtil.TouchCount > 1)
        {
            pressActive = false;   // pincement caméra
            return;
        }

        if (PointerUtil.Pressed)
        {
            if (!pressIsDrag && Vector2.Distance(PointerUtil.ScreenPos, pressScreenPos) > TapThreshold)
                pressIsDrag = true;

            if (currentMode == BuildMode.Digging)
                ApplyDigMark(CellAtPointer());
        }

        if (PointerUtil.ReleasedThisFrame)
        {
            pressActive = false;
            if (!pressIsDrag && currentMode != BuildMode.Digging)
                HandleTap(PointerUtil.ScreenToWorld(pressScreenPos));
        }
    }

    private void HandleTap(Vector2 worldPos)
    {
        Vector3Int cell = floorTilemap.WorldToCell(worldPos);
        switch (currentMode)
        {
            case BuildMode.Filling: TryFill(cell); break;
            case BuildMode.PlacingWall: PlaceWall(cell); break;
            case BuildMode.PlacingFloor: PlaceFloor(cell); break;
            case BuildMode.PlacingDoor: PlaceObject(doorPrefab, floorTilemap.GetCellCenterWorld(cell), 30, "Porte"); break;
            case BuildMode.PlacingTower: PlaceObject(towerPrefab, floorTilemap.GetCellCenterWorld(cell), 50, "Tour"); break;
            case BuildMode.Erasing: TryErase(cell); break;
            case BuildMode.PlacingItem: TryPlaceItem(SelectedItem, cell); break;
            case BuildMode.PlacingStairs:
                if (DungeonLayers.Instance != null && GetPlacedAt(cell) == null && DungeonLayers.Instance.PlaceStairsDown(cell))
                    SetMode(BuildMode.None);
                break;
            case BuildMode.MovingCore:
                if (TryMoveCore(cell)) SetMode(BuildMode.None);
                break;
        }
    }

    // ─────────────────────────────────────────────
    // DÉPLACEMENT DU CŒUR
    // ─────────────────────────────────────────────

    /// <summary>Le tout premier déplacement du cœur est gratuit (le temps de trouver le bon endroit).</summary>
    public bool FirstCoreMoveFree => StoryManager.Instance != null && !StoryManager.Instance.HasFlag("first_core_move");

    /// <summary>Coût actuel : presque toute la réserve maximale de mana (gratuit la première fois).</summary>
    public int MoveCoreCost
    {
        get
        {
            if (FirstCoreMoveFree) return 0;
            int max = ManaSystem.Instance != null ? ManaSystem.Instance.Max : 500;
            return Mathf.RoundToInt(max * moveCoreCostRatio);
        }
    }

    public bool CanMoveCoreTo(Vector3Int c, out string reason)
    {
        reason = null;
        var t = DungeonTerritory.Instance;
        var core = FindFirstObjectByType<DungeonCore>();
        if (t == null || core == null) { reason = "Cœur introuvable"; return false; }
        if (core.IsRelocating) { reason = "Le cœur est déjà en mouvement"; return false; }
        if (!t.IsInsideCell(c)) { reason = "Le cœur doit rester dans le donjon"; return false; }
        if (c == t.CoreCell) { reason = "Le cœur est déjà ici"; return false; }
        Vector3 center = floorTilemap.GetCellCenterWorld(c);
        if (GetPlacedAt(c) != null || DungeonStairs.At(center) != null) { reason = "Case occupée"; return false; }
        if (ManaSystem.Instance != null && ManaSystem.Instance.IntrudersInside > 0) { reason = "Impossible pendant une intrusion"; return false; }
        if (core.currentHealth < core.maxHealth * 0.5f) { reason = "Le cœur est trop affaibli pour se déplacer"; return false; }
        return true;
    }

    /// <summary>Déplace le cœur sur une case du donjon (n'importe quel étage relié).</summary>
    public bool TryMoveCore(Vector3Int c)
    {
        if (!CanMoveCoreTo(c, out string reason))
        {
            GameHUD.Toast(reason, GameHUD.WarningColor);
            return false;
        }
        bool free = FirstCoreMoveFree;
        if (!free && ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(MoveCoreCost)) return false;

        var core = FindFirstObjectByType<DungeonCore>();
        Vector3 from = core.transform.position;
        Vector3 to = floorTilemap.GetCellCenterWorld(c);
        var layers = DungeonLayers.Instance;
        core.RelocateTo(to, () =>
        {
            DungeonTerritory.Instance?.Recompute();
            if (AstarPath.active != null)
            {
                AstarPath.active.UpdateGraphs(new Bounds(from, new Vector3(3f, 3f, 1f)), 0.05f);
                AstarPath.active.UpdateGraphs(new Bounds(to, new Vector3(3f, 3f, 1f)), 0.05f);
            }
            if (layers != null)
            {
                int li = layers.IndexAt(to);
                if (li != layers.CurrentView) layers.SetView(li);
                GameHUD.Toast($"Le cœur se reforme dans {layers.layers[li].name}", GameHUD.GoldColor);
            }
            CameraController.Instance?.CenterOn(to);
            UpdateUI();
        });
        StoryManager.Instance?.OnCoreMoved(free);
        return true;
    }

    /// <summary>Sélectionne un élément du catalogue (depuis le panneau).</summary>
    public void SelectItem(CatalogItem item)
    {
        if (item == null) { SetMode(BuildMode.None); return; }
        if (item.kind == ItemKind.Tool) { SetMode(item.tool); return; }
        SelectedItem = item;
        currentMode = BuildMode.None;   // force la notification
        SetMode(BuildMode.PlacingItem);
        SelectedItem = item;
    }

    /// <summary>
    /// Le donjon est attaqué (des intrus sont dedans) : impossible de creuser, poser, invoquer ou déplacer le cœur.
    /// Il faut être prêt AVANT leur arrivée.
    /// </summary>
    public static bool UnderAttack => !CrawlerMode.Active && ManaSystem.Instance != null && ManaSystem.Instance.IntrudersInside > 0;
    private float nextAttackToast;

    private void AttackRefusal()
    {
        if (Time.unscaledTime < nextAttackToast) return;
        nextAttackToast = Time.unscaledTime + 2f;
        GameHUD.Toast("Le donjon est attaqué : impossible de bâtir ou d'invoquer tant qu'il reste des intrus !", GameHUD.DangerColor);
    }

    public void SetMode(BuildMode mode)
    {
        if (mode != BuildMode.None && UnderAttack) { AttackRefusal(); mode = BuildMode.None; }
        if (currentMode == mode) return;
        currentMode = mode;
        if (mode != BuildMode.PlacingItem) SelectedItem = null;
        pressActive = false;
        if (mode != BuildMode.None) MonsterSelector.Instance?.DeselectAll();
        OnModeChanged?.Invoke(mode);
        UpdateUI();
    }

    /// <summary>Appelé par les boutons UI pour changer de mode.</summary>
    public void SetModeFromUI(int modeIndex) => SetMode((BuildMode)modeIndex);

    private Vector3Int CellAtPointer() => floorTilemap.WorldToCell(PointerUtil.WorldPos);

    // ─────────────────────────────────────────────
    // CREUSAGE (file d'attente)
    // ─────────────────────────────────────────────

    private void ApplyDigMark(Vector3Int c)
    {
        if (c == lastDragCell) return;
        lastDragCell = c;

        if (dragMarking)
        {
            if (!stoneTilemap.HasTile(c) || digMarks.ContainsKey(c)) return;
            digQueue.Add(c);
            var go = new GameObject("DigMark");
            go.transform.position = floorTilemap.GetCellCenterWorld(c);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteUtil.Square;
            sr.color = new Color(1f, 0.6f, 0.1f, 0.45f);
            sr.sortingOrder = 25;
            digMarks[c] = sr;
        }
        else
        {
            RemoveDigMark(c);
        }
    }

    private void RemoveDigMark(Vector3Int c)
    {
        if (!digMarks.TryGetValue(c, out var sr)) return;
        if (sr != null) Destroy(sr.gameObject);
        digMarks.Remove(c);
        digQueue.Remove(c);
        if (isMining && currentMiningCell == c) StopMining(true);
    }

    public void ClearDigQueue()
    {
        foreach (var c in new List<Vector3Int>(digQueue)) RemoveDigMark(c);
    }

    private void ProcessDigQueue()
    {
        if (isMining)
        {
            miningProgress += Time.deltaTime;
            if (digMarks.TryGetValue(currentMiningCell, out var sr) && sr != null)
            {
                float pulse = 0.45f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
                sr.color = new Color(1f, 0.85f, 0.2f, pulse);
            }
            if (miningProgress >= EffectiveMiningTime) CompleteMining();
            UpdateUI();
            return;
        }

        if (digQueue.Count == 0) return;
        var territory = DungeonTerritory.Instance;

        // Prochaine case accessible (voisine du donjon)
        for (int i = 0; i < digQueue.Count; i++)
        {
            Vector3Int c = digQueue[i];
            if (!stoneTilemap.HasTile(c))
            {
                RemoveDigMark(c);
                return;
            }
            if (territory != null && !territory.CanDigCell(c)) continue;

            var mana = ManaSystem.Instance;
            if (mana != null && !mana.CanAfford(miningCost)) return;   // attend d'avoir du mana
            if (mana != null) mana.TrySpend(Mathf.RoundToInt(miningCost * Talents.DigCostMult * ColonyTrade.DigCostMult), false);

            isMining = true;
            miningProgress = 0f;
            currentMiningCell = c;
            return;
        }
    }

    private void CompleteMining()
    {
        Vector3Int c = currentMiningCell;
        stoneTilemap.SetTile(c, null);
        AutoWallTile.RefreshAround(stoneTilemap, c);
        var layers = DungeonLayers.Instance;
        floorTilemap.SetTile(c, layers != null ? layers.FloorTileFor(c) : floorTile);
        layers?.TintCell(floorTilemap, c, false);
        placedWallTilemap.SetTile(c, null);

        RefreshStoneColliders();
        Sfx.Play("dig", floorTilemap.GetCellCenterWorld(c), 0.8f);
        DungeonTerritory.Instance?.OnCellDug(c);
        UpdatePathfindingGraph(c);

        isMining = false;
        miningProgress = 0f;
        if (digMarks.TryGetValue(c, out var sr) && sr != null) Destroy(sr.gameObject);
        digMarks.Remove(c);
        digQueue.Remove(c);

        StoryManager.Instance?.OnCellDug();
        UpdateUI();
    }

    private void StopMining(bool refund)
    {
        if (isMining && refund) ManaSystem.Instance?.Add(miningCost);
        isMining = false;
        miningProgress = 0f;
    }

    // ─────────────────────────────────────────────
    // REMBLAYER
    // ─────────────────────────────────────────────

    private void TryFill(Vector3Int c)
    {
        var territory = DungeonTerritory.Instance;
        if (territory == null || !territory.IsInsideCell(c))
        {
            GameHUD.Toast("On ne peut remblayer que l'intérieur du donjon", GameHUD.WarningColor);
            return;
        }
        Vector3 center = floorTilemap.GetCellCenterWorld(c);
        if (HasUnitNear(center, 0.6f) || GetPlacedAt(c) != null || DungeonStairs.At(center) != null)
        {
            GameHUD.Toast("Case occupée", GameHUD.WarningColor);
            return;
        }
        if (territory.WouldFillDisconnect(c, territory.EntranceCell))
        {
            GameHUD.Toast("Impossible : cela couperait le chemin vers le cœur", GameHUD.WarningColor);
            return;
        }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(fillCost)) return;

        var wall = DungeonLayers.Instance != null ? DungeonLayers.Instance.WallTileFor(c) : stoneTile;
        stoneTilemap.SetTile(c, wall != null ? wall : stoneTile);
        DungeonLayers.Instance?.TintCell(stoneTilemap, c, true);
        placedWallTilemap.SetTile(c, null);
        RefreshStoneColliders();
        territory.OnCellFilled(c);
        UpdatePathfindingGraph(c);
        UpdateUI();
    }

    private static bool HasUnitNear(Vector3 pos, float radius)
    {
        foreach (var g in GobelinAI.All)
            if (g != null && Vector2.Distance(g.transform.position, pos) < radius) return true;
        foreach (var a in AdventurerAI.All)
            if (a != null && Vector2.Distance(a.transform.position, pos) < radius) return true;
        return false;
    }

    // ─────────────────────────────────────────────
    // ÉLÉMENTS DU CATALOGUE (monstres, pièges, décors)
    // ─────────────────────────────────────────────

    public PlacedObject GetPlacedAt(Vector3Int c)
    {
        foreach (var p in PlacedObject.All)
            if (p != null && floorTilemap.WorldToCell(p.transform.position) == c) return p;
        return null;
    }

    /// <summary>Compatibilité : piège sur la case.</summary>
    public PlacedObject GetTrapAt(Vector3Int c)
    {
        var p = GetPlacedAt(c);
        return p != null && p.kind == ItemKind.Trap ? p : null;
    }

    public bool CanPlaceItemAt(CatalogItem item, Vector3Int c, out string reason)
    {
        reason = null;
        bool trophy = item != null && LootSystem.IsTrophyItem(item);
        if (item == null || (item.prefab == null && !trophy)) { reason = "Élément indisponible"; return false; }
        int lvl = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
        if (lvl < item.unlockLevel) { reason = $"Niveau {item.unlockLevel} requis"; return false; }

        Vector3 center = floorTilemap.GetCellCenterWorld(c);
        if (item.kind == ItemKind.Monster)
        {
            if (MonsterSpawner.Instance == null) { reason = "Pas de spawner"; return false; }
            string fam = MonsterHabitat.FamilyOf(item), theme = MonsterHabitat.ThemeAt(center);
            if (!CrawlerMode.Active && !MonsterHabitat.Allowed(fam, theme))
            { reason = $"Ne vit qu'ici : {MonsterHabitat.HomesText(fam)}"; return false; }
            // la mimique se cache parmi les coffres : seulement dans une salle du trésor
            if (!CrawlerMode.Active && fam == "mimic" && !ThemedRooms.InRoom(center, ThemedRooms.Kind.Treasury))
            { reason = "Ne se cache que dans une salle du trésor"; return false; }
            return MonsterSpawner.Instance.CanSummonAt(center, Mathf.RoundToInt(item.cost * Talents.SummonCostMult), out reason);
        }

        var territory = DungeonTerritory.Instance;
        if (territory != null && !territory.IsInsideCell(c)) { reason = "À placer sur le sol du donjon"; return false; }
        if (territory != null && c == territory.CoreCell) { reason = "Pas sur le cœur !"; return false; }
        if (GetPlacedAt(c) != null || DungeonStairs.At(center) != null) { reason = "Case déjà occupée"; return false; }
        if (item.kind == ItemKind.Trap && !TrapRules.CanPlace(item.id, c, center, out reason)) return false;
        // un objet encombrant ne doit pas couper le donjon en deux (milieu d'un couloir, entrée d'une salle...)
        if (PlacedObject.IsBlocking(item.id) && territory != null)
        {
            var blocked = PlacedObject.BlockedCells();
            blocked.Add(c);
            if (!territory.StaysConnected(blocked)) { reason = "Encombrant : il bloquerait le passage (posez-le contre un mur ou dans un recoin)"; return false; }
        }
        if (item.kind == ItemKind.Trap && PlayerLevel.Instance != null && !PlayerLevel.Instance.CanPlaceTrap())
        {
            reason = $"Limite de pièges atteinte ({PlayerLevel.Instance.GetMaxTraps()}) — montez de niveau";
            return false;
        }
        if (trophy && LootSystem.Instance != null && LootSystem.Instance.CountOf(LootSystem.TrophyLootId(item)) <= 0)
        {
            reason = "Plus d'exemplaire de cet objet dans le trésor";
            return false;
        }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(CostOf(item)))
        {
            reason = $"Pas assez de mana ({CostOf(item)} requis)";
            return false;
        }
        return true;
    }

    /// <summary>Pose un élément sans payer (chargement d'une sauvegarde).</summary>
    public PlacedObject PlaceItemFree(CatalogItem item, Vector3Int c)
    {
        if (item == null || item.prefab == null) return null;
        Vector3 pos = floorTilemap.GetCellCenterWorld(c);
        GameObject obj = Instantiate(item.prefab, pos, Quaternion.identity, objectsParent);
        obj.name = item.displayName;
        var po = obj.GetComponent<PlacedObject>();
        if (po == null) po = obj.AddComponent<PlacedObject>();
        po.kind = item.kind;
        po.itemId = item.id;
        po.cost = item.cost;
        po.upkeepPerDay = item.upkeep;
        Sfx.Play("place", po.transform.position, 0.8f);
        po.prestige = item.prestige;
        return po;
    }

    /// <summary>Mur décoratif sans coût (chargement).</summary>
    public void RestorePlacedWall(Vector3Int c)
    {
        placedWallTilemap.SetTile(c, wallIsolated);
        UpdatePlacedWallTile(c);
    }

    public Transform ObjectsParent
    {
        get
        {
            if (objectsParent == null) objectsParent = new GameObject("DungeonObjects").transform;
            return objectsParent;
        }
    }

    private void TryPlaceItem(CatalogItem item, Vector3Int c)
    {
        if (!CanPlaceItemAt(item, c, out string reason))
        {
            GameHUD.Toast(reason, GameHUD.WarningColor);
            return;
        }
        Vector3 pos = floorTilemap.GetCellCenterWorld(c);

        if (item.kind == ItemKind.Monster)
        {
            MonsterSpawner.Instance.TrySummonAt(pos, item.prefab, Mathf.RoundToInt(item.cost * Talents.SummonCostMult), item.displayName);
            return;
        }
        if (LootSystem.IsTrophyItem(item))
        {
            LootSystem.Instance?.PlaceTrophy(LootSystem.TrophyLootId(item), c);
            // plus d'exemplaire : retour à la sélection
            if (LootSystem.Instance == null || LootSystem.Instance.CountOf(LootSystem.TrophyLootId(item)) <= 0) SetMode(BuildMode.None);
            UpdateUI();
            return;
        }

        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(CostOf(item))) return;
        GameObject obj = Instantiate(item.prefab, pos, Quaternion.identity, objectsParent);
        obj.name = item.displayName;
        var po = obj.GetComponent<PlacedObject>();
        if (po == null) po = obj.AddComponent<PlacedObject>();
        po.kind = item.kind;
        po.itemId = item.id;
        po.cost = item.cost;
        po.upkeepPerDay = item.upkeep;
        Sfx.Play("place", po.transform.position, 0.8f);
        po.prestige = item.prestige;
        FloatingText.Spawn(pos + Vector3.up * 0.4f, $"-{CostOf(item)}", GameHUD.ManaColor, 1f);
        if (item.kind == ItemKind.Decor) StoryManager.Instance?.OnDecorPlaced();
        UpdateUI();
    }

    // ─────────────────────────────────────────────
    // DÉCOR (murs placés)
    // ─────────────────────────────────────────────

    private void PlaceWall(Vector3Int cellPos)
    {
        if (stoneTilemap.HasTile(cellPos) || placedWallTilemap.HasTile(cellPos))
        {
            GameHUD.Toast("Il y a déjà un mur ici", GameHUD.WarningColor);
            return;
        }
        if (DungeonTerritory.Instance != null && !DungeonTerritory.Instance.IsInsideCell(cellPos))
        {
            GameHUD.Toast("Le décor se pose dans le donjon", GameHUD.WarningColor);
            return;
        }
        if (!HasStoneAdjacent(cellPos))
        {
            GameHUD.Toast("Le décor doit être contre la roche", GameHUD.WarningColor);
            return;
        }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(wallPlacementCost)) return;

        placedWallTilemap.SetTile(cellPos, wallIsolated);
        UpdatePlacedWallTile(cellPos);
        UpdateUI();
    }

    private void PlaceFloor(Vector3Int cellPos)
    {
        if (stoneTilemap.HasTile(cellPos)) return;
        placedWallTilemap.SetTile(cellPos, null);
        floorTilemap.SetTile(cellPos, floorTile);
        UpdateUI();
    }

    private void PlaceObject(GameObject prefab, Vector3 position, int cost, string objectName)
    {
        if (prefab == null)
        {
            GameHUD.Toast($"{objectName} : pas encore disponible", GameHUD.WarningColor);
            return;
        }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(cost)) return;
        GameObject obj = Instantiate(prefab, position, Quaternion.identity, objectsParent);
        obj.name = objectName;
        UpdateUI();
    }

    // ─────────────────────────────────────────────
    // GOMME
    // ─────────────────────────────────────────────

    private void TryErase(Vector3Int cellPos)
    {
        if (digMarks.ContainsKey(cellPos))
        {
            RemoveDigMark(cellPos);
            return;
        }

        var placed = GetPlacedAt(cellPos);
        if (placed != null && placed.itemId == LootSystem.TrophyId)
        {
            // un trophée retiré retourne dans le trésor
            LootSystem.Instance?.Add(placed.data, 1, false);
            FloatingText.Spawn(placed.transform.position + Vector3.up * 0.4f, "Rangé au trésor", GameHUD.GoldColor, 0.9f);
            Destroy(placed.gameObject);
            UpdateUI();
            return;
        }
        if (placed != null)
        {
            int refund = Mathf.RoundToInt(placed.cost * refundRatio);
            ManaSystem.Instance?.Add(refund);
            FloatingText.Spawn(placed.transform.position + Vector3.up * 0.4f, $"+{refund}", GameHUD.ManaColor, 1f);
            Destroy(placed.gameObject);
            UpdateUI();
            return;
        }

        if (placedWallTilemap.HasTile(cellPos))
        {
            placedWallTilemap.SetTile(cellPos, null);
            UpdateUI();
            return;
        }

        GameHUD.Toast("Rien à effacer ici", GameHUD.InfoColor);
    }

    // ─────────────────────────────────────────────
    // AUTO-TILING DES MURS PLACÉS (regarde uniquement la pierre)
    // ─────────────────────────────────────────────

    private void UpdatePlacedWallTile(Vector3Int cellPos)
    {
        bool n = stoneTilemap.HasTile(cellPos + Vector3Int.up);
        bool s = stoneTilemap.HasTile(cellPos + Vector3Int.down);
        bool w = stoneTilemap.HasTile(cellPos + Vector3Int.left);
        bool e = stoneTilemap.HasTile(cellPos + Vector3Int.right);

        TileBase tile = DeterminePlacedWallTile(n, s, w, e);
        if (tile != null) placedWallTilemap.SetTile(cellPos, tile);
    }

    private bool HasStoneAdjacent(Vector3Int c) =>
        stoneTilemap.HasTile(c + Vector3Int.up) || stoneTilemap.HasTile(c + Vector3Int.down) ||
        stoneTilemap.HasTile(c + Vector3Int.left) || stoneTilemap.HasTile(c + Vector3Int.right);

    private TileBase DeterminePlacedWallTile(bool n, bool s, bool w, bool e)
    {
        if (n && w && !s && !e) return wallCornerNW;
        if (n && e && !s && !w) return wallCornerNE;
        if (s && w && !n && !e) return wallCornerSW;
        if (s && e && !n && !w) return wallCornerSE;
        if (n) return wallConnectedNorth;
        if (s) return wallConnectedSouth;
        if (w) return wallConnectedWest;
        if (e) return wallConnectedEast;
        return wallIsolated;
    }

    // ─────────────────────────────────────────────
    // COLLISIONS & PATHFINDING
    // ─────────────────────────────────────────────

    public void RefreshStoneCollidersPublic() => RefreshStoneColliders();

    /// <summary>
    /// Creuse d'un coup un ensemble de cases (mode Aventurier : donjon généré). Une seule mise à jour
    /// des collisions, du territoire et du graphe de chemins à la fin.
    /// </summary>
    public int DigBatch(IEnumerable<Vector3Int> cells)
    {
        var layers = DungeonLayers.Instance;
        var dug = new List<Vector3Int>();
        foreach (var c in cells)
            if (stoneTilemap.HasTile(c)) { stoneTilemap.SetTile(c, null); dug.Add(c); }
        if (dug.Count == 0) return 0;
        var bounds = new Bounds(floorTilemap.GetCellCenterWorld(dug[0]), Vector3.one);
        foreach (var c in dug)
        {
            AutoWallTile.RefreshAround(stoneTilemap, c);
            floorTilemap.SetTile(c, layers != null ? layers.FloorTileFor(c) : floorTile);
            layers?.TintCell(floorTilemap, c, false);
            placedWallTilemap.SetTile(c, null);
            bounds.Encapsulate(floorTilemap.GetCellCenterWorld(c));
        }
        RefreshStoneColliders();
        DungeonTerritory.Instance?.Recompute();
        bounds.Expand(new Vector3(3f, 3f, 0f));
        if (AstarPath.active != null) AstarPath.active.UpdateGraphs(bounds);
        return dug.Count;
    }

    private void RefreshStoneColliders()
    {
        var tc = stoneTilemap.GetComponent<TilemapCollider2D>();
        if (tc != null) tc.ProcessTilemapChanges();
        var comp = stoneTilemap.GetComponent<CompositeCollider2D>();
        if (comp != null) comp.GenerateGeometry();
    }

    private void UpdatePathfindingGraph(Vector3Int cellPos)
    {
        if (AstarPath.active == null) return;
        Vector3 center = floorTilemap.GetCellCenterWorld(cellPos);
        AstarPath.active.UpdateGraphs(new Bounds(center, new Vector3(3f, 3f, 1f)), 0.05f);
    }

    // ─────────────────────────────────────────────
    // CURSEUR DE CASE
    // ─────────────────────────────────────────────

    private void UpdateCursor()
    {
        if (cellCursor == null) return;
        bool show = currentMode != BuildMode.None && !InputBlocked && PointerUtil.Available &&
                    !PointerUtil.IsTouch && !PointerUtil.IsOverUI();
        cellCursor.enabled = show;
        if (!show) return;

        Vector3Int c = CellAtPointer();
        cellCursor.transform.position = floorTilemap.GetCellCenterWorld(c);
        cellCursor.color = IsValidTarget(c) ? new Color(0.3f, 1f, 0.4f, 0.35f) : new Color(1f, 0.25f, 0.25f, 0.35f);
    }

    private bool IsValidTarget(Vector3Int c)
    {
        var t = DungeonTerritory.Instance;
        switch (currentMode)
        {
            case BuildMode.Digging: return stoneTilemap.HasTile(c);
            case BuildMode.PlacingItem: return CanPlaceItemAt(SelectedItem, c, out _);
            case BuildMode.PlacingStairs:
                return t != null && t.IsInsideCell(c) && GetPlacedAt(c) == null && DungeonLayers.Instance != null &&
                       DungeonLayers.Instance.PendingConnectionLayer() == DungeonLayers.Instance.IndexAtCell(c) + 1;
            case BuildMode.Filling: return t != null && t.IsInsideCell(c) && c != t.CoreCell;
            case BuildMode.MovingCore: return CanMoveCoreTo(c, out _);
            case BuildMode.PlacingWall:
                return !stoneTilemap.HasTile(c) && !placedWallTilemap.HasTile(c) && HasStoneAdjacent(c) &&
                       (t == null || t.IsInsideCell(c));
            case BuildMode.Erasing: return digMarks.ContainsKey(c) || GetPlacedAt(c) != null || placedWallTilemap.HasTile(c);
            default: return true;
        }
    }

    // ─────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────

    private void UpdateUI()
    {
        if (uiController != null)
            uiController.UpdateDisplay(GetMana(), currentMode, isMining, miningProgress, EffectiveMiningTime);
    }

    public static string GetModeLabel(BuildMode mode)
    {
        switch (mode)
        {
            case BuildMode.None: return "Sélection";
            case BuildMode.Digging: return "Creuser";
            case BuildMode.Filling: return "Remblayer";
            case BuildMode.PlacingTrap: return "Piège";
            case BuildMode.Summoning: return "Gobelin";
            case BuildMode.PlacingWall: return "Décor";
            case BuildMode.Erasing: return "Gomme";
            case BuildMode.PlacingFloor: return "Sol";
            case BuildMode.PlacingDoor: return "Porte";
            case BuildMode.PlacingTower: return "Tour";
            case BuildMode.PlacingItem: return "Placer";
            case BuildMode.PlacingStairs: return "Escalier";
            case BuildMode.MovingCore: return "Déplacer le cœur";
            default: return mode.ToString();
        }
    }

    public string GetModeHint(BuildMode mode)
    {
        bool touch = PointerUtil.IsTouch;
        string tap = touch ? "Touchez" : "Cliquez";
        switch (mode)
        {
            case BuildMode.None:
                return touch ? "Touchez un gobelin pour le sélectionner, puis le sol pour le déplacer. Glissez pour déplacer la vue."
                             : "Clic : sélectionner un gobelin / le déplacer. Glisser : sélection de zone. Clic droit/molette glissé : déplacer la vue.";
            case BuildMode.Digging:
                return $"{tap} ou glissez sur la roche pour la marquer. Le Noyau creuse les cases voisines du donjon ({miningCost} mana/case).";
            case BuildMode.Filling:
                return $"{tap} une case du donjon pour la reboucher ({fillCost} mana). Le chemin vers le cœur doit rester ouvert.";
            case BuildMode.PlacingTrap:
                return $"{tap} le sol du donjon pour poser une roue tranchante ({trapPlacementCost} mana).";
            case BuildMode.Summoning:
                int cost = MonsterSpawner.Instance != null ? MonsterSpawner.Instance.cost1Goblin : 50;
                return $"{tap} le sol du donjon pour invoquer un gobelin ({cost} mana).";
            case BuildMode.PlacingWall:
                return $"{tap} une case contre la roche pour embellir votre donjon ({wallPlacementCost} mana).";
            case BuildMode.Erasing:
                return $"{tap} un piège ou un objet (remboursé à {Mathf.RoundToInt(refundRatio * 100)} %), un décor mural ou une marque de creusage.";
            case BuildMode.PlacingItem:
                if (SelectedItem == null) return "";
                return $"<b>{SelectedItem.displayName}</b> ({SelectedItem.cost} mana) : {SelectedItem.description}  <color=#9A96B0>[{tap} pour placer · Échap : annuler]</color>";
            case BuildMode.PlacingStairs:
                return $"{tap} une case du donjon pour creuser l'escalier vers l'étage inférieur ({(DungeonLayers.Instance != null ? DungeonLayers.Instance.stairsCost : 0)} mana).";
            case BuildMode.MovingCore:
                return FirstCoreMoveFree
                    ? $"{tap} une case du donjon (n'importe quel étage relié) pour y transférer le cœur. <color=#9CF0A8>Premier déplacement gratuit</color> ; les suivants coûteront 90 % de votre réserve max. Impossible pendant une intrusion."
                    : $"{tap} une case du donjon (n'importe quel étage relié) pour y transférer le cœur. Coût : <color=#7FD4FF>{MoveCoreCost} mana</color> (90 % de votre réserve max). Impossible pendant une intrusion.";
            default:
                return "";
        }
    }

    public int GetModeCost(BuildMode mode)
    {
        switch (mode)
        {
            case BuildMode.Digging: return miningCost;
            case BuildMode.Filling: return fillCost;
            case BuildMode.PlacingTrap: return trapPlacementCost;
            case BuildMode.Summoning: return MonsterSpawner.Instance != null ? MonsterSpawner.Instance.cost1Goblin : 50;
            case BuildMode.PlacingWall: return wallPlacementCost;
            case BuildMode.PlacingItem: return SelectedItem != null ? SelectedItem.cost : 0;
            case BuildMode.MovingCore: return MoveCoreCost;
            default: return 0;
        }
    }

    // ─────────────────────────────────────────────
    // API PUBLIQUE (compatibilité)
    // ─────────────────────────────────────────────

    public void AddMana(int amount) => ManaSystem.Instance?.Add(amount);
    public int GetMana() => ManaSystem.Instance != null ? ManaSystem.Instance.Current : 0;
    public BuildMode GetCurrentMode() => currentMode;

    /// <summary>Prix réel d'un élément du catalogue (talents : pièges moins chers).</summary>
    public static int CostOf(CatalogItem item) =>
        item == null ? 0 : item.kind == ItemKind.Trap ? Mathf.RoundToInt(item.cost * Talents.TrapCostMult) : item.cost;
}
