using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class PlayerProfile
{
    public string id;
    public string name = "Noyau";
    public int color;
    public long created;
    public float playSeconds;
    public int gamesStarted;
    public int bestDay;
    public int totalKills;
    public List<string> achievements = new List<string>();
}

[Serializable]
public class ProfileList
{
    public string activeId;
    public List<PlayerProfile> profiles = new List<PlayerProfile>();
}

/// <summary>
/// Comptes joueur LOCAUX (sur l'appareil) : chaque profil a ses propres sauvegardes et statistiques.
/// Fichiers : persistentDataPath/profiles.json et persistentDataPath/saves/&lt;profil&gt;/&lt;emplacement&gt;.json.
/// Le format est prêt à être synchronisé avec un service en ligne plus tard (identifiant stable par profil).
/// </summary>
public static class ProfileStore
{
    public static readonly string[] Slots = { "auto", "slot1", "slot2", "slot3" };
    /// <summary>Emplacements du mode Aventurier : distincts, une aventure n'écrase jamais une partie du Noyau.</summary>
    public static readonly string[] AdvSlots = { "adv_auto", "adv1", "adv2", "adv3" };
    public static string[] SlotsFor(bool adventure) => adventure ? AdvSlots : Slots;
    public static bool IsAuto(string slot) => slot == "auto" || slot == "adv_auto";
    public static readonly Color[] Colors =
    {
        new Color(0.55f, 0.45f, 1f), new Color(0.35f, 0.8f, 1f), new Color(1f, 0.55f, 0.35f),
        new Color(0.45f, 0.9f, 0.5f), new Color(1f, 0.8f, 0.3f), new Color(1f, 0.45f, 0.65f),
    };

    private static ProfileList list;

    private static string Root => Application.persistentDataPath;
    private static string ListPath => Path.Combine(Root, "profiles.json");

    public static ProfileList List
    {
        get
        {
            if (list == null) Load();
            return list;
        }
    }

    public static PlayerProfile Active
    {
        get
        {
            var l = List;
            var p = l.profiles.Find(x => x.id == l.activeId);
            if (p == null && l.profiles.Count > 0) { p = l.profiles[0]; l.activeId = p.id; }
            return p;
        }
    }

    public static bool HasProfile => List.profiles.Count > 0;

    private static void Load()
    {
        try
        {
            list = File.Exists(ListPath) ? JsonUtility.FromJson<ProfileList>(File.ReadAllText(ListPath)) : null;
        }
        catch (Exception e) { Debug.LogWarning("[Profils] lecture impossible : " + e.Message); list = null; }
        if (list == null) list = new ProfileList();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(ListPath, JsonUtility.ToJson(List, true));
        }
        catch (Exception e) { Debug.LogWarning("[Profils] écriture impossible : " + e.Message); }
    }

    public static PlayerProfile Create(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Noyau" : name.Trim();
        var p = new PlayerProfile
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 12),
            name = name,
            color = List.profiles.Count % Colors.Length,
            created = DateTime.Now.Ticks,
        };
        List.profiles.Add(p);
        List.activeId = p.id;
        Save();
        return p;
    }

    public static void SetActive(string id)
    {
        if (List.profiles.Exists(p => p.id == id)) { List.activeId = id; Save(); }
    }

    public static void Delete(string id)
    {
        List.profiles.RemoveAll(p => p.id == id);
        if (List.activeId == id) List.activeId = List.profiles.Count > 0 ? List.profiles[0].id : null;
        try
        {
            string dir = Path.Combine(Root, "saves", id);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (Exception e) { Debug.LogWarning("[Profils] suppression : " + e.Message); }
        Save();
    }

    /// <summary>Profil actif garanti (créé à la volée si besoin).</summary>
    public static PlayerProfile EnsureActive() => Active ?? Create("Noyau");

    // ─────────────────────────────────────────────
    // EMPLACEMENTS DE SAUVEGARDE
    // ─────────────────────────────────────────────

    public static string SlotPath(string slot)
    {
        var p = EnsureActive();
        return Path.Combine(Root, "saves", p.id, slot + ".json");
    }

    public static string SlotLabel(string slot) =>
        slot == "auto" ? "Sauvegarde automatique" : slot == "adv_auto" ? "Aventure · sauvegarde automatique"
        : slot.StartsWith("adv") ? $"Aventure · emplacement {slot.Substring(3)}" : $"Emplacement {slot.Substring(4)}";

    public static bool WriteSlot(string slot, SaveData data)
    {
        try
        {
            string path = SlotPath(slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // écriture atomique : un crash pendant l'écriture ne corrompt pas l'ancienne sauvegarde
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Sauvegarde] écriture impossible : " + e.Message);
            return false;
        }
    }

    public static SaveData ReadSlot(string slot)
    {
        try
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Sauvegarde] lecture impossible : " + e.Message);
            return null;
        }
    }

    public static void DeleteSlot(string slot)
    {
        try { File.Delete(SlotPath(slot)); } catch { }
    }

    public static SaveSummary Summary(string slot)
    {
        var d = ReadSlot(slot);
        if (d == null) return new SaveSummary { slot = slot, exists = false };
        return new SaveSummary
        {
            slot = slot, exists = true, day = d.day, level = d.level, savedAt = d.savedAt, savedTicks = d.savedTicks,
            colony = d.colony != null && d.colony.stage > 0 ? SettlementPlan.StageNames[Mathf.Clamp(d.colony.stage, 0, SettlementPlan.StageNames.Length - 1)] : "",
            adventure = d.mode == "crawler" && d.crawler != null,
            hero = d.mode == "crawler" && d.crawler != null
                ? $"{(string.IsNullOrWhiteSpace(d.crawler.heroName) ? CrawlerMode.ClassLabel((AdventurerAI.Role)d.crawler.role, false) : d.crawler.heroName)} · record : étage {d.crawler.deepest} · {d.crawler.gold} or"
                : "",
        };
    }

    /// <summary>Emplacement le plus récent (pour « Continuer »), ou null.</summary>
    public static SaveSummary Latest()
    {
        SaveSummary best = null;
        foreach (var list in new[] { Slots, AdvSlots })
            foreach (var s in list)
            {
                var sum = Summary(s);
                if (sum.exists && (best == null || sum.savedTicks > best.savedTicks)) best = sum;
            }
        return best;
    }
}
