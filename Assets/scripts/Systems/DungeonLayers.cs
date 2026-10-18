using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class DungeonLayerDef
{
    public string name = "Étage";
    [Tooltip("Zone de l'étage (en cases)")]
    public RectInt cellBounds;
    public int unlockLevel = 1;
    public int unlockCost;
    public bool unlocked;
    public int themeIndex;
    [Tooltip("Salle d'arrivée (centre) des étages souterrains")]
    public Vector3Int landingCell;
    [Tooltip("Relié à l'étage supérieur par un escalier")]
    public bool connected;
    [Tooltip("Case de l'escalier de descente (étage supérieur)")]
    public Vector3Int stairsCell;
}

/// <summary>
/// Étages du donjon. L'étage 0 est la montagne de l'île ; les suivants sont des profondeurs
/// débloquées avec le niveau du Noyau, chacune avec un thème (lave, aquatique...).
/// Les zones des étages sont définies par le générateur d'île.
/// </summary>
public class DungeonLayers : MonoBehaviour
{
    public static DungeonLayers Instance { get; private set; }

    public List<DungeonLayerDef> layers = new List<DungeonLayerDef>();
    public int stairsCost = 40;

    public int CurrentView { get; private set; }
    public event Action OnLayersChanged;
    public event Action<int> OnViewChanged;

    private DungeonCatalog catalog;
    private DungeonBuilder builder;

    private void Awake()
    {
        Instance = this;
        catalog = DungeonCatalog.Get();
        if (layers.Count == 0)
        {
            // Scène non générée : un seul étage couvrant tout
            layers.Add(new DungeonLayerDef { name = "Surface", cellBounds = new RectInt(-1000, -1000, 2000, 2000), unlocked = true, connected = true });
        }
        layers[0].unlocked = true;
        layers[0].connected = true;
        // Progression des étages : étage 2 au niveau 4, étage 3 au niveau 9, étage 4 au niveau 14
        int[] lv = { 1, 4, 9, 14 }, cost = { 0, 250, 500, 800 };
        for (int i = 1; i < layers.Count && i < lv.Length; i++) { layers[i].unlockLevel = lv[i]; layers[i].unlockCost = cost[i]; }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        builder = DungeonBuilder.Instance;
    }

    // ─────────────────────────────────────────────
    // REQUÊTES
    // ─────────────────────────────────────────────

    public int IndexAtCell(Vector3Int c)
    {
        for (int i = layers.Count - 1; i >= 0; i--)
            if (layers[i].cellBounds.Contains(new Vector2Int(c.x, c.y))) return i;
        return 0;
    }

    public int IndexAt(Vector2 world)
    {
        var t = DungeonTerritory.Instance;
        return t != null ? IndexAtCell(t.WorldToCell(world)) : 0;
    }

    public LayerTheme ThemeOf(int layer)
    {
        if (catalog == null || catalog.themes.Count == 0 || layer < 0 || layer >= layers.Count) return null;
        return catalog.themes[Mathf.Clamp(layers[layer].themeIndex, 0, catalog.themes.Count - 1)];
    }

    public LayerTheme ThemeAt(Vector2 world) => ThemeOf(IndexAt(world));

    public TileBase FloorTileFor(Vector3Int cell)
    {
        var theme = ThemeOf(IndexAtCell(cell));
        if (theme != null && theme.floorTile != null) return theme.floorTile;
        return builder != null ? builder.floorTile : null;
    }

    /// <summary>Mur auto-connecté du thème de la case (sinon la roche par défaut).</summary>
    public TileBase WallTileFor(Vector3Int cell)
    {
        var theme = ThemeOf(IndexAtCell(cell));
        if (theme != null && theme.wallTile != null) return theme.wallTile;
        return builder != null ? builder.stoneTile : null;
    }

    /// <summary>Applique la teinte du thème sur une case (sol ou roche).</summary>
    public void TintCell(Tilemap tm, Vector3Int c, bool stone)
    {
        var theme = ThemeOf(IndexAtCell(c));
        if (theme == null || tm == null || !tm.HasTile(c)) return;
        if (tm.GetTile(c) is AutoWallTile) return;   // murs : couleurs déjà dans les sprites
        tm.SetTileFlags(c, TileFlags.None);
        tm.SetColor(c, stone ? theme.stoneTint : theme.floorTint);
    }

    public Rect WorldRect(int layer)
    {
        var t = DungeonTerritory.Instance;
        var b = layers[Mathf.Clamp(layer, 0, layers.Count - 1)].cellBounds;
        if (t == null) return new Rect(b.x, b.y, b.width, b.height);
        Vector3 min = t.CellCenter(new Vector3Int(b.xMin, b.yMin, 0));
        Vector3 max = t.CellCenter(new Vector3Int(b.xMax - 1, b.yMax - 1, 0));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public bool CanUnlock(int i, out string reason)
    {
        reason = null;
        if (i <= 0 || i >= layers.Count) { reason = "Étage invalide"; return false; }
        var l = layers[i];
        if (l.unlocked) { reason = "Déjà débloqué"; return false; }
        if (!layers[i - 1].unlocked) { reason = "Débloquez d'abord l'étage précédent"; return false; }
        int lvl = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
        if (lvl < l.unlockLevel) { reason = $"Niveau {l.unlockLevel} requis"; return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(UnlockCost(i))) { reason = $"{UnlockCost(i)} mana requis"; return false; }
        return true;
    }

    // ─────────────────────────────────────────────
    // ACTIONS
    // ─────────────────────────────────────────────

    public bool Unlock(int i, int themeIndex)
    {
        if (!CanUnlock(i, out string reason))
        {
            GameHUD.Toast(reason, GameHUD.WarningColor);
            return false;
        }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(UnlockCost(i))) return false;

        ApplyUnlock(i, themeIndex);
        var theme = ThemeOf(i);
        GameHUD.Toast($"{layers[i].name} débloqué : {theme.displayName}", GameHUD.GoldColor);
        StoryManager.Instance?.OnLayerUnlocked(i, theme);
        OnLayersChanged?.Invoke();
        return true;
    }

    /// <summary>Débloque l'étage (roche du thème + salle d'arrivée), sans coût ni message (utilisé aussi au chargement).</summary>
    public void ApplyUnlock(int i, int themeIndex)
    {
        if (builder == null) builder = DungeonBuilder.Instance;
        var l = layers[i];
        l.unlocked = true;
        l.themeIndex = Mathf.Clamp(themeIndex, 0, catalog.themes.Count - 1);

        // Roche de l'étage aux couleurs du thème (murs auto-connectés)
        var stone = builder.stoneTilemap;
        var wall = ThemeOf(i)?.wallTile;
        var cellsToSet = new List<Vector3Int>();
        foreach (var p in l.cellBounds.allPositionsWithin)
        {
            var c = new Vector3Int(p.x, p.y, 0);
            if (stone.HasTile(c)) cellsToSet.Add(c);
        }
        if (wall != null)
        {
            var tiles = new TileBase[cellsToSet.Count];
            for (int k = 0; k < tiles.Length; k++) tiles[k] = wall;
            stone.SetTiles(cellsToSet.ToArray(), tiles);
        }
        else foreach (var c in cellsToSet) TintCell(stone, c, true);

        // Salle d'arrivée 3x3
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                CarveCell(l.landingCell + new Vector3Int(dx, dy, 0));
    }

    public void CarveCell(Vector3Int c)
    {
        builder.stoneTilemap.SetTile(c, null);
        AutoWallTile.RefreshAround(builder.stoneTilemap, c);
        builder.floorTilemap.SetTile(c, FloorTileFor(c));
        TintCell(builder.floorTilemap, c, false);
        builder.RefreshStoneCollidersPublic();
        AstarPath.active?.UpdateGraphs(new Bounds(builder.floorTilemap.GetCellCenterWorld(c), new Vector3(3, 3, 1)), 0.05f);
    }

    /// <summary>Premier étage débloqué mais pas encore relié (où poser la descente) ; -1 sinon.</summary>
    public int PendingConnectionLayer()
    {
        for (int i = 1; i < layers.Count; i++)
            if (layers[i].unlocked && !layers[i].connected) return i;
        return -1;
    }

    /// <summary>Pose l'escalier de descente vers l'étage 'target' depuis une case de l'étage au-dessus.</summary>
    public bool PlaceStairsDown(Vector3Int cell)
    {
        int target = PendingConnectionLayer();
        if (target < 0) { GameHUD.Toast("Aucun étage à relier", GameHUD.WarningColor); return false; }
        var territory = DungeonTerritory.Instance;
        if (IndexAtCell(cell) != target - 1)
        {
            GameHUD.Toast($"L'escalier se pose à l'étage {target} (au-dessus)", GameHUD.WarningColor);
            return false;
        }
        if (territory == null || !territory.IsInsideCell(cell) || cell == territory.CoreCell)
        {
            GameHUD.Toast("Placez l'escalier sur le sol du donjon", GameHUD.WarningColor);
            return false;
        }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(StairsCost)) return false;

        ConnectLayer(target, cell);
        GameHUD.Toast($"Escalier creusé vers {layers[target].name}", GameHUD.GoldColor);
        OnLayersChanged?.Invoke();
        return true;
    }

    /// <summary>Crée la paire d'escaliers et relie l'étage (sans coût ; aussi au chargement).</summary>
    public void ConnectLayer(int target, Vector3Int cell)
    {
        if (builder == null) builder = DungeonBuilder.Instance;
        var territory = DungeonTerritory.Instance;
        var l = layers[target];
        l.stairsCell = cell;
        var down = CreateStairs(builder.floorTilemap.GetCellCenterWorld(cell), true, target - 1);
        var up = CreateStairs(builder.floorTilemap.GetCellCenterWorld(l.landingCell + Vector3Int.up), false, target);
        down.partner = up;
        up.partner = down;

        var link = down.gameObject.AddComponent<NodeLink2>();
        link.end = up.transform;

        l.connected = true;
        territory?.Recompute();
    }

    public void NotifyChanged() => OnLayersChanged?.Invoke();

    /// <summary>Prix réels (talent « Architecte de l'abîme »).</summary>
    public int UnlockCost(int i) => Mathf.RoundToInt(layers[i].unlockCost * Talents.LayerCostMult);
    public int StairsCost => Mathf.RoundToInt(stairsCost * Talents.LayerCostMult);

    private DungeonStairs CreateStairs(Vector3 pos, bool down, int layer)
    {
        var go = new GameObject(down ? "Escalier (descente)" : "Escalier (montée)");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = down ? catalog.stairsDownSprite : catalog.stairsUpSprite;
        if (sr.sprite == null) { sr.sprite = SpriteUtil.Square; sr.color = down ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.6f, 0.5f, 0.4f); }
        sr.sortingOrder = 1;
        var s = go.AddComponent<DungeonStairs>();
        s.goesDown = down;
        s.layerIndex = layer;
        return s;
    }

    /// <summary>Déplace le cœur dans la salle d'arrivée d'un étage relié (même coût que le déplacement libre).</summary>
    public bool MoveCoreTo(int i)
    {
        if (i <= 0 || i >= layers.Count || !layers[i].connected)
        {
            GameHUD.Toast("L'étage doit être relié par un escalier", GameHUD.WarningColor);
            return false;
        }
        return builder != null && builder.TryMoveCore(layers[i].landingCell + Vector3Int.down);
    }

    public void SetView(int i)
    {
        i = Mathf.Clamp(i, 0, layers.Count - 1);
        if (!layers[i].unlocked) return;
        CurrentView = i;
        var cam = CameraController.Instance;
        if (cam != null)
        {
            cam.SetBounds(WorldRect(i));
            Vector2 focus;
            var core = FindFirstObjectByType<DungeonCore>();
            if (core != null && IndexAt(core.transform.position) == i) focus = core.transform.position;
            else if (i > 0) focus = builder.floorTilemap.GetCellCenterWorld(layers[i].landingCell);
            else focus = WorldRect(0).center;
            cam.CenterOn(focus);
        }
        OnViewChanged?.Invoke(i);
    }

    /// <summary>Met à jour la vue si la caméra suit une unité qui change d'étage.</summary>
    public void SyncViewTo(Vector2 worldPos)
    {
        int i = IndexAt(worldPos);
        if (i == CurrentView) return;
        CurrentView = i;
        CameraController.Instance?.SetBounds(WorldRect(i));
        OnViewChanged?.Invoke(i);
    }
}
