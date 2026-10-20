using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Illustrations de la colonie (Resources/SettlementArt), générées par le menu DungeonCore.</summary>
[CreateAssetMenu(menuName = "DungeonCore/Illustrations de la colonie", fileName = "SettlementArt")]
public class SettlementArt : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string name;
        public Sprite[] frames;
    }

    /// <summary>Morceau d'un tampon : posé par le milieu de son bord inférieur (trié à ses pieds).</summary>
    [Serializable]
    public class StampPiece
    {
        public Sprite[] frames;
        /// <summary>Milieu du bord inférieur, en unités depuis le coin bas-gauche du tampon.</summary>
        public Vector2 pos;
        public Vector2 size;
        public float fps;
        public bool solid;
        public string kind;
        /// <summary>Couches et images d'origine (repérer le bibliothécaire, le lit...).</summary>
        public string names;
    }

    /// <summary>Scène complète d'un pack (bâtiment, cour, jardin, habitants) : sol plat + morceaux triés (Tools~/make_stamps.py).</summary>
    [Serializable]
    public class Stamp
    {
        public string id;
        /// <summary>Taille en unités (cases).</summary>
        public Vector2 size;
        /// <summary>Seuil de la porte principale, en unités depuis le coin bas-gauche.</summary>
        public Vector2 door;
        public Sprite ground;
        public List<StampPiece> pieces = new List<StampPiece>();
        /// <summary>Intérieurs : zones solides (murs, hors de la pièce), en unités depuis le coin bas-gauche.</summary>
        public List<Rect> solids = new List<Rect>();
    }

    public List<Entry> entries = new List<Entry>();
    public List<Stamp> stamps = new List<Stamp>();
    public UnityEngine.Tilemaps.TileBase pierLeft, pierRight, pierPlatform;
    public UnityEngine.Tilemaps.TileBase seaFloor;
    public Sprite[] stumps;

    private static SettlementArt cached;
    private Dictionary<string, Entry> map;

    public static SettlementArt Get()
    {
        if (cached == null) cached = Resources.Load<SettlementArt>("SettlementArt");
        return cached;
    }

    public Sprite[] Frames(string name)
    {
        if (map == null)
        {
            map = new Dictionary<string, Entry>();
            foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.name)) map[e.name] = e;
        }
        return map.TryGetValue(name ?? "", out var en) ? en.frames : null;
    }

    private Dictionary<string, Stamp> stampMap;

    public Stamp GetStamp(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (stampMap == null)
        {
            stampMap = new Dictionary<string, Stamp>();
            foreach (var st in stamps) if (st != null && !string.IsNullOrEmpty(st.id)) stampMap[st.id] = st;
        }
        return stampMap.TryGetValue(id, out var r) ? r : null;
    }

    public Sprite Sprite(string name)
    {
        var f = Frames(name);
        return f != null && f.Length > 0 ? f[0] : null;
    }
}
