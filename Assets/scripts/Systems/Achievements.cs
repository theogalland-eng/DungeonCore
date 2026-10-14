using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Succès du profil (conservés d'une partie à l'autre). Vérifiés régulièrement et à la fin de partie.
/// </summary>
public class Achievements : MonoBehaviour
{
    public static Achievements Instance { get; private set; }

    public class Def
    {
        public string id, name, desc;
        public Func<bool> check;
    }

    public static readonly List<Def> All = new List<Def>();
    /// <summary>Succès débloqués pendant la partie en cours (écran de fin).</summary>
    public readonly List<Def> unlockedThisGame = new List<Def>();
    private float nextCheck;

    private static GameManager G => GameManager.Instance;
    private static int Rank => GuildSystem.Instance != null ? GuildSystem.Instance.officialRank : -1;

    static Achievements()
    {
        Add("first_blood", "Premier sang", "Absorber l'essence d'un premier intrus.", () => G.statKills >= 1);
        Add("butcher", "Boucher des profondeurs", "Absorber 50 intrus en une partie.", () => G.statKills >= 50);
        Add("days_10", "Tenace", "Survivre 10 jours.", () => G.CurrentDay >= 10);
        Add("days_30", "Immuable", "Survivre 30 jours.", () => G.CurrentDay >= 30);
        Add("evolve", "Métamorphose", "Faire évoluer une créature.", () => G.statEvolutions >= 1);
        Add("assessed", "Fiché", "Être classé par la Guilde des Aventuriers.", () => Rank >= 0);
        Add("rank_c", "Vétéran des donjons", "Atteindre le rang C.", () => Rank >= 3);
        Add("rank_a", "Élite des profondeurs", "Atteindre le rang A.", () => Rank >= 5);
        Add("rank_sss", "Apocalypse", "Atteindre le rang SSS.", () => Rank >= 8);
        Add("hero", "Tueur de héros", "Vaincre un héros.", () => G.statHeroKills >= 1);
        Add("nemesis", "Rancune éteinte", "Abattre une Némésis.", () => NemesisSystem.Instance != null && NemesisSystem.Instance.Defeated >= 1);
        Add("deep", "Abysses", "Ouvrir trois étages.", () => CountLayers() >= 3);
        Add("rich", "Trésor de dragon", "Atteindre 100 de prestige.", () => PlacedObject.TotalPrestige() >= 100f);
        Add("city", "Ville du donjon", "Voir la colonie devenir une ville.", () => SettlementManager.Instance != null && SettlementManager.Instance.stage >= 5);
        Add("storm", "Œil du cyclone", "Traverser une tempête.", () => IslandEvents.Instance != null && IslandEvents.Instance.Current == IslandEvent.Storm);
        Add("merciless", "Sans pitié", "Survivre 10 jours en Impitoyable.", () => GameDifficulty.Current == Difficulty.Impitoyable && G.CurrentDay >= 10);
    }

    private static void Add(string id, string name, string desc, Func<bool> check) =>
        All.Add(new Def { id = id, name = name, desc = desc, check = check });

    private static int CountLayers()
    {
        if (DungeonLayers.Instance == null) return 1;
        int n = 0;
        foreach (var l in DungeonLayers.Instance.layers) if (l.unlocked) n++;
        return n;
    }

    public static bool IsUnlocked(string id)
    {
        var p = ProfileStore.Active;
        return p != null && p.achievements != null && p.achievements.Contains(id);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck || G == null) return;
        nextCheck = Time.unscaledTime + 2f;
        if (MainMenu.Instance != null && MainMenu.Instance.InHome) return;
        if (CrawlerMode.Active) return;   // succès du Noyau seulement
        CheckAll();
    }

    public void CheckAll()
    {
        var p = ProfileStore.Active;
        if (p == null || G == null) return;
        if (p.achievements == null) p.achievements = new List<string>();
        bool changed = false;
        foreach (var d in All)
        {
            if (p.achievements.Contains(d.id)) continue;
            bool ok;
            try { ok = d.check(); } catch { ok = false; }
            if (!ok) continue;
            p.achievements.Add(d.id);
            unlockedThisGame.Add(d);
            changed = true;
            GameHUD.Toast($"Succès débloqué : {d.name}", GameHUD.GoldColor);
            Sfx.Play("levelup", null, 0.7f);
        }
        if (changed) ProfileStore.Save();
    }
}
