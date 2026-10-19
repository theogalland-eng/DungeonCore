using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Un intrus qui a survécu au donjon et qui s'en souvient.</summary>
[Serializable]
public class NemesisRecord
{
    public string name;          // prénom
    public string epithet;       // surnom gagné dans le donjon
    public string prefab;        // apparence
    public int role;
    public int rank = -1;
    public int encounters = 1;
    public int lastSeenDay;
    public int monstersKilled;
    public bool dead;
    public List<int> traps = new List<int>();   // cases des pièges connus (x0, y0, x1, y1...)
    public List<int> map = new List<int>();     // sa carte du donjon (x0, y0, x1, y1...)
    public bool coreKnown;
    public int coreX, coreY;
}

/// <summary>
/// NÉMÉSIS : un survivant marqué par le donjon (blessé, terrifié, ou qui a vu le cristal) peut revenir
/// plus tard, monté en rang, avec un surnom. Il se souvient des pièges qu'il a vus et les désamorce.
/// Chaque retour le rend plus dangereux ; sa mort est un petit événement.
/// </summary>
public class NemesisSystem : MonoBehaviour
{
    public static NemesisSystem Instance { get; private set; }

    [Tooltip("Chance qu'une Némésis rejoigne une expédition de la Guilde")]
    [Range(0f, 1f)] public float joinChance = 0.35f;
    public int maxRecords = 6;

    public readonly List<NemesisRecord> records = new List<NemesisRecord>();

    private static readonly string[] Epithets =
    {
        "Cicatrice", "Increvable", "Deux-Vies", "Mains-Brûlées", "Œil-Vif", "Sans-Peur", "Pied-Léger", "Revenant", "Tête-Dure", "Cendre",
    };

    private int Today => GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int AliveCount { get { int n = 0; foreach (var r in records) if (!r.dead) n++; return n; } }
    public int Defeated { get { int n = 0; foreach (var r in records) if (r.dead) n++; return n; } }

    // ─────────────────────────────────────────────
    // SURVIVANTS ET MORTS
    // ─────────────────────────────────────────────

    /// <summary>Un intrus rentre vivant : s'il a été marqué par le donjon, il deviendra (ou reste) une Némésis.</summary>
    public void OnSurvivor(AdventurerAI a)
    {
        if (a == null || a.Spared) return;
        var r = a.Nemesis;
        bool marked = r != null || a.DamageTakenFraction >= 0.35f || a.MonstersSeen >= 3 || a.HasSeenCore;
        if (!marked) return;
        if (r == null)
        {
            if (AliveCount >= maxRecords) return;
            string full = a.DisplayName;
            string first = full.Split(' ').Length > 1 ? full.Split(' ')[1] : full;
            r = new NemesisRecord
            {
                name = first,
                epithet = Epithets[UnityEngine.Random.Range(0, Epithets.Length)],
                prefab = a.name.Replace("(Clone)", "").Trim(),
                role = (int)a.role,
                rank = Mathf.Max(0, a.Rank),
            };
            records.Add(r);
        }
        else r.encounters++;
        r.lastSeenDay = Today;
        foreach (var c in a.SeenTraps)
            if (!HasTrap(r, c)) { r.traps.Add(c.x); r.traps.Add(c.y); }
        // il se souvient de la carte qu'il a dressée
        if (a.Map != null)
        {
            r.map = new List<int>();
            a.Map.Export(r.map, out r.coreKnown, out r.coreX, out r.coreY);
        }
    }

    public void OnDeath(AdventurerAI a)
    {
        var r = a != null ? a.Nemesis : null;
        if (r == null || r.dead) return;
        r.dead = true;
        GameHUD.Toast($"{r.name} « {r.epithet} » est tombé pour de bon", GameHUD.GoldColor);
        StoryManager.Instance?.Play(new StoryManager.Line(StoryManager.SelfName,
            r.encounters >= 3
                ? $"{r.name}... {r.encounters} fois tu es revenu. Étrangement, tu vas presque me manquer."
                : $"{r.name} « {r.epithet} » ne reviendra plus raconter mes secrets."));
    }

    private static bool HasTrap(NemesisRecord r, Vector2Int c)
    {
        for (int i = 0; i + 1 < r.traps.Count; i += 2) if (r.traps[i] == c.x && r.traps[i + 1] == c.y) return true;
        return false;
    }

    public static HashSet<Vector2Int> TrapSet(NemesisRecord r)
    {
        var set = new HashSet<Vector2Int>();
        for (int i = 0; i + 1 < r.traps.Count; i += 2) set.Add(new Vector2Int(r.traps[i], r.traps[i + 1]));
        return set;
    }

    // ─────────────────────────────────────────────
    // RETOURS
    // ─────────────────────────────────────────────

    /// <summary>Une Némésis prête à revenir (au moins deux jours après sa dernière visite) ?</summary>
    public NemesisRecord PickForExpedition()
    {
        var pool = records.FindAll(r => !r.dead && Today - r.lastSeenDay >= 2);
        if (pool.Count == 0) return null;
        float hostility = GuildSystem.Instance != null ? GuildSystem.Instance.hostility : 0f;
        if (UnityEngine.Random.value > joinChance + hostility * 0.2f) return null;
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    public static GameObject FindPrefab(string name)
    {
        var cat = DungeonCatalog.Get();
        if (cat == null || string.IsNullOrEmpty(name)) return null;
        foreach (var p in new[] { cat.archerPrefab, cat.magePrefab, cat.healerPrefab })
            if (p != null && p.name == name) return p;
        foreach (var p in cat.adventurerPrefabs) if (p != null && p.name == name) return p;
        foreach (var tv in cat.tierVariants)
            if (tv != null) foreach (var p in tv.prefabs) if (p != null && p.name == name) return p;
        return null;
    }

    /// <summary>Petit nom flottant au-dessus d'un personnage.</summary>
    public static void AddTag(Transform t, string text, Color c)
    {
        var go = new GameObject("Nom");
        go.transform.SetParent(t, false);
        go.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 2.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = c;
        UIKit.Outline(tmp, new Color32(10, 5, 10, 255), 0.25f);   // matériau partagé (pas un par texte)
        tmp.rectTransform.sizeDelta = new Vector2(6f, 1f);
        tmp.sortingOrder = 40;
    }

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    public void Capture(SaveData d)
    {
        d.nemeses = new List<NemesisRecord>(records);
    }

    public void Restore(SaveData d)
    {
        records.Clear();
        if (d.nemeses != null) records.AddRange(d.nemeses);
    }
}
