using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ÉNIGMES DU DONJON : leviers et pierres runiques. Une porte scellée (DungeonDoor.sealSwitches > 0) ne se force pas :
/// elle ne s'ouvre que lorsque assez de mécanismes de son type sont actionnés dans les environs (même étage).
///   - Porte à levier : un levier suffit, il reste baissé 40 s.
///   - Porte runique : trois pierres runiques doivent briller en même temps (chacune 25 s).
/// Les intrus bloqués partent à leur recherche (AdventurerLife) : du temps perdu, des proies isolées pour vos monstres.
/// </summary>
public class PuzzleSwitch : MonoBehaviour
{
    public static readonly List<PuzzleSwitch> All = new List<PuzzleSwitch>();

    public enum Kind { Lever, Rune }
    public Kind kind = Kind.Lever;
    public Sprite offSprite, onSprite;
    public float activeSeconds = 40f;
    [Tooltip("Rayon dans lequel il commande les portes scellées (cases)")] public float reach = 12f;

    public bool IsActive => Time.time < activeUntil;
    [System.NonSerialized] public Object reservedBy;
    private float activeUntil = -1f;
    private SpriteRenderer sr;
    private UnityEngine.Rendering.Universal.Light2D glow;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        glow = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
        Refresh();
    }

    public void Activate()
    {
        bool was = IsActive;
        activeUntil = Time.time + activeSeconds;
        reservedBy = null;
        if (!was)
        {
            Sfx.Play(kind == Kind.Lever ? "door_break" : "shine", transform.position, 0.6f);
            FloatingText.Spawn(transform.position + Vector3.up * 0.6f, kind == Kind.Lever ? "Clac !" : "La rune s'illumine", kind == Kind.Lever ? new Color(0.9f, 0.85f, 0.7f) : new Color(0.75f, 0.65f, 1f), 0.8f);
        }
        Refresh();
    }

    private void Update()
    {
        // retour au repos : le mécanisme se réarme tout seul
        bool on = IsActive;
        if (sr != null && (on ? onSprite : offSprite) != null && sr.sprite != (on ? onSprite : offSprite)) Refresh();
        if (glow != null) glow.intensity = on ? 0.9f + 0.15f * Mathf.Sin(Time.time * 4f) : 0f;
    }

    private void Refresh()
    {
        if (sr == null) return;
        bool on = IsActive;
        var s = on ? onSprite : offSprite;
        if (s != null) sr.sprite = s;
    }

    private static int LayerAt(Vector2 p) => DungeonLayers.Instance != null ? DungeonLayers.Instance.IndexAt(p) : 0;

    public static Kind KindFor(DungeonDoor d) => d.sealSwitches >= 3 ? Kind.Rune : Kind.Lever;

    /// <summary>Mécanismes qui commandent cette porte.</summary>
    public static void For(DungeonDoor door, List<PuzzleSwitch> into)
    {
        into.Clear();
        var k = KindFor(door);
        int layer = LayerAt(door.transform.position);
        foreach (var s in All)
            if (s != null && s.kind == k && LayerAt(s.transform.position) == layer &&
                Vector2.Distance(s.transform.position, door.transform.position) <= s.reach) into.Add(s);
    }

    private static readonly List<PuzzleSwitch> buf = new List<PuzzleSwitch>();

    /// <summary>La porte peut-elle s'ouvrir ? (assez de mécanismes actifs en même temps)</summary>
    public static bool Satisfied(DungeonDoor door)
    {
        if (door.sealSwitches <= 0) return true;
        For(door, buf);
        int n = 0;
        foreach (var s in buf) if (s.IsActive) n++;
        return n >= door.sealSwitches;
    }

    /// <summary>Mécanisme inactif le plus proche (non réservé par un autre intrus) pour cette porte.</summary>
    public static PuzzleSwitch NearestIdle(DungeonDoor door, Vector2 from, Object who)
    {
        For(door, buf);
        PuzzleSwitch best = null;
        float bd = float.MaxValue;
        foreach (var s in buf)
        {
            if (s.IsActive || (s.reservedBy != null && s.reservedBy != who)) continue;
            float d = Vector2.Distance(s.transform.position, from);
            if (d < bd) { bd = d; best = s; }
        }
        return best;
    }
}
