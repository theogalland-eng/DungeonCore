using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// PIÈGES DESSINÉS POUR LE PROJET (Tools~/make_traps.py) : chacun tient dans une case, prévient avant de frapper
/// (lueur, ombre, déclic), s'oriente seul selon le couloir ou le mur, et vise tout ce qui porte un AdventurerCombat
/// (intrus, héros et compagnons du mode Aventurier). Base commune + un comportement par piège.
/// </summary>
public abstract class TrapKit : MonoBehaviour
{
    public Sprite[] frames;
    public SpriteRenderer sr;
    protected Vector3Int cell;
    protected Vector2 Pos => transform.position;
    protected float clock;
    private PlacedObject po;
    private readonly Dictionary<int, float> hitCooldown = new Dictionary<int, float>();

    protected virtual void Start()
    {
        po = GetComponent<PlacedObject>();
        var b = DungeonBuilder.Instance;
        if (b != null) cell = b.floorTilemap.WorldToCell(transform.position);
        Show(0);
    }

    protected void Show(int i)
    {
        if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[Mathf.Clamp(i, 0, frames.Length - 1)];
    }

    /// <summary>Victimes dans un cercle (intrus, héros, compagnons).</summary>
    protected static List<AdventurerCombat> Victims(Vector2 c, float r)
    {
        var list = new List<AdventurerCombat>();
        foreach (var h in Physics2D.OverlapCircleAll(c, r, 1 << 7))
        {
            var ac = h.GetComponent<AdventurerCombat>();
            if (ac != null && ac.currentHealth > 0f && !list.Contains(ac)) list.Add(ac);
        }
        return list;
    }

    protected bool AnyIn(Vector2 c, float r) => Physics2D.OverlapCircle(c, r, 1 << 7) != null;

    /// <summary>Dégâts (une fois par intrus et par délai) avec comptage pour les rumeurs.</summary>
    protected void Hurt(AdventurerCombat v, float dmg, float cooldown = 0f)
    {
        if (v == null || dmg <= 0f) return;
        if (cooldown > 0f)
        {
            int id = v.GetInstanceID();
            if (hitCooldown.TryGetValue(id, out float t) && Time.time < t) return;
            hitCooldown[id] = Time.time + cooldown;
        }
        v.TakeDamage(dmg);
        if (po != null) po.damageDealt += dmg;
    }

    protected static void Slow(AdventurerCombat v, float mult, float secs) { var a = v != null ? v.GetComponent<AdventurerAI>() : null; if (a != null) a.Slow(mult, secs); }
    protected static void Hold(AdventurerCombat v, float secs) { var a = v != null ? v.GetComponent<AdventurerAI>() : null; if (a != null) a.Hold(secs); }

    // ── Orientation
    protected static bool Floor(Vector3Int c)
    {
        var t = DungeonTerritory.Instance;
        return t == null || t.IsInsideCell(c);
    }

    /// <summary>Axe du couloir : horizontal si la case s'ouvre davantage à gauche et à droite.</summary>
    protected bool HorizontalCorridor()
    {
        int h = 0, v = 0;
        for (int k = 1; k <= 3; k++)
        {
            if (Floor(cell + new Vector3Int(k, 0, 0))) h++; if (Floor(cell + new Vector3Int(-k, 0, 0))) h++;
            if (Floor(cell + new Vector3Int(0, k, 0))) v++; if (Floor(cell + new Vector3Int(0, -k, 0))) v++;
        }
        return h >= v;
    }

    /// <summary>Mur voisin (piège mural) : direction de tir (opposée au mur), ou zéro.</summary>
    public static Vector2Int AwayFromWall(Vector3Int c)
    {
        if (!Floor(c + Vector3Int.left)) return Vector2Int.right;
        if (!Floor(c + Vector3Int.right)) return Vector2Int.left;
        if (!Floor(c + Vector3Int.up)) return Vector2Int.down;
        if (!Floor(c + Vector3Int.down)) return Vector2Int.up;
        return Vector2Int.zero;
    }

    /// <summary>Tourne le sprite (dessiné vers la droite) pour qu'il tire dans « dir ».</summary>
    protected void Face(Vector2Int dir)
    {
        if (sr == null) return;
        var t = sr.transform;
        if (dir == Vector2Int.left) { sr.flipX = true; t.localPosition = new Vector3(-t.localPosition.x, t.localPosition.y, 0f); }
        else if (dir == Vector2Int.up || dir == Vector2Int.down)
        {
            t.localRotation = Quaternion.Euler(0, 0, dir == Vector2Int.up ? 90f : -90f);
            t.localPosition = Vector3.zero;
        }
    }

    /// <summary>Cases libres en ligne depuis la case du piège (sans traverser de mur).</summary>
    protected int FreeRun(Vector2Int dir, int max)
    {
        int n = 0;
        for (int k = 1; k <= max; k++) { if (!Floor(cell + new Vector3Int(dir.x * k, dir.y * k, 0))) break; n = k; }
        return n;
    }

    protected Vector2 CellCenter(int dx, int dy)
    {
        var b = DungeonBuilder.Instance;
        return b != null ? (Vector2)b.floorTilemap.GetCellCenterWorld(cell + new Vector3Int(dx, dy, 0)) : Pos + new Vector2(dx, dy);
    }

    protected static void Light(GameObject go, Color c, float r, float i)
    {
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = c;
        l.pointLightOuterRadius = r;
        l.intensity = i;
    }
}

/// <summary>Règles de pose des pièges : thème d'étage exigé, mur voisin exigé, anciens identifiants.</summary>
public static class TrapRules
{
    public static readonly Dictionary<string, string> ThemeOnly = new Dictionary<string, string>
    {
        { "geyser", "lava" }, { "whirlpool", "aqua" }, { "stalactite", "ice" }, { "spores", "fungal" },
        { "prism", "crystal" }, { "grasp", "crypt" }, { "crossbow", "fortress" },
    };
    /// <summary>Les mains squelettiques vivent aussi dans les catacombes.</summary>
    public static bool ThemeOk(string id, string theme) =>
        !ThemeOnly.TryGetValue(id ?? "", out var t) || t == theme || (id == "grasp" && theme == "catacomb");

    public static readonly HashSet<string> WallMounted = new HashSet<string> { "wallspears", "darts", "crossbow" };

    /// <summary>Anciens pièges remplacés (parties sauvegardées).</summary>
    public static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
    {
        { "blade", "pendulum" }, { "blades", "saw" }, { "guillotine", "cage" }, { "bomb", "rune" }, { "column", "wallspears" },
    };

    public static bool CanPlace(string id, Vector3Int cell, Vector2 center, out string reason)
    {
        reason = null;
        if (CrawlerMode.Active) return true;
        string theme = MonsterHabitat.ThemeAt(center);
        if (!ThemeOk(id, theme)) { reason = $"Seulement sur un étage : {MonsterHabitat.ThemeName(ThemeOnly[id])}"; return false; }
        if (WallMounted.Contains(id) && TrapKit.AwayFromWall(cell) == Vector2Int.zero) { reason = "Se fixe au mur : posez-le contre une paroi"; return false; }
        return true;
    }
}
