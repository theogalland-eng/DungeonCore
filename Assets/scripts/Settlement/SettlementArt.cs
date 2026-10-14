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

    public List<Entry> entries = new List<Entry>();
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

    public Sprite Sprite(string name)
    {
        var f = Frames(name);
        return f != null && f.Length > 0 ? f[0] : null;
    }
}
