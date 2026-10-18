using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identité d'un monstre : famille, stade d'évolution (1 à 3), expérience et entretien journalier.
/// Les monstres gagnent de l'XP en tuant des intrus et en survivant ; au seuil, le joueur
/// peut les faire évoluer (nouvelle apparence, stats supérieures) contre du mana.
/// </summary>
public class MonsterUnit : MonoBehaviour
{
    public static readonly List<MonsterUnit> All = new List<MonsterUnit>();

    [Header("Famille")]
    public string familyId;
    public string familyName;
    [Range(1, 3)] public int stage = 1;

    [Header("Évolution")]
    [Tooltip("Prefab du stade suivant (vide = forme finale)")]
    public GameObject nextStage;
    public int xpToEvolve = 60;
    public int evolveCost = 50;
    public int evolveLevelRequired = 2;
    public int xp;

    [Header("Entretien")]
    [Tooltip("Mana prélevé chaque matin pour garder ce monstre")]
    public int upkeepPerDay = 4;
    [Tooltip("Invocation temporaire (squelette de liche) : ni entretien, ni limite de monstres")]
    public bool summoned;

    public bool IsFinalForm => nextStage == null;
    /// <summary>Coût réel de l'évolution (une forge des monstres le réduit).</summary>
    public int EffectiveEvolveCost => Mathf.RoundToInt(evolveCost * ThemedRooms.EvolveCostMult(transform.position));
    public bool ReadyToEvolve => !IsFinalForm && xp >= xpToEvolve;
    public bool CanEvolveNow(out string reason)
    {
        reason = null;
        if (IsFinalForm) { reason = "Forme ultime"; return false; }
        if (DungeonBuilder.UnderAttack) { reason = "Impossible pendant une attaque"; return false; }
        if (xp < xpToEvolve) { reason = $"Expérience {xp}/{xpToEvolve}"; return false; }
        int lvl = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 99;
        if (lvl < evolveLevelRequired) { reason = $"Niveau {evolveLevelRequired} du Noyau requis"; return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(EffectiveEvolveCost)) { reason = $"{EffectiveEvolveCost} mana requis"; return false; }
        return true;
    }

    private GameObject badge;
    private bool announced;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private void OnNewDay(int day)
    {
        if (!summoned) AddXp(6);   // l'expérience vient aussi avec le temps
    }

    public void AddXp(int amount)
    {
        if (IsFinalForm || summoned) return;
        xp += amount;
        if (ReadyToEvolve && !announced)
        {
            announced = true;
            GameHUD.Toast($"{GetComponent<GobelinAI>()?.gobelinName ?? familyName} peut évoluer !", GameHUD.GoldColor);
            FloatingText.Spawn(transform.position + Vector3.up * 0.9f, "Évolution prête", GameHUD.GoldColor, 1.2f);
        }
    }

    /// <summary>Appelé quand ce monstre achève un intrus.</summary>
    /// <summary>Intrus tués par ce monstre (rumeurs de la taverne).</summary>
    public int kills;

    public void OnKill(int tierIndex) { kills++; AddXp(12 + 10 * Mathf.Max(0, tierIndex)); }

    private void Update()
    {
        // Petit losange doré au-dessus des monstres prêts à évoluer
        bool show = ReadyToEvolve;
        if (show && badge == null)
        {
            badge = new GameObject("EvolveBadge");
            badge.transform.SetParent(transform, false);
            var sr = badge.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteUtil.Diamond;
            sr.color = GameHUD.GoldColor;
            sr.sortingOrder = 20;
            badge.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
        }
        if (badge != null)
        {
            badge.SetActive(show);
            if (show) badge.transform.localPosition = new Vector3(0f, 0.85f + Mathf.Sin(Time.time * 4f) * 0.05f, 0f);
        }
    }

    /// <summary>Fait évoluer le monstre (paie le mana) : remplacé par la forme suivante au même endroit.</summary>
    public MonsterUnit Evolve()
    {
        if (!CanEvolveNow(out string reason)) { GameHUD.Toast(reason, GameHUD.WarningColor); return null; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(EffectiveEvolveCost)) return null;
        if (GameManager.Instance != null) GameManager.Instance.statEvolutions++;

        var old = GetComponent<GobelinAI>();
        var go = Instantiate(nextStage, transform.position, Quaternion.identity, transform.parent);
        var ai = go.GetComponent<GobelinAI>();
        if (ai != null && old != null)
        {
            ai.dungeonCore = old.dungeonCore;
            ai.anchorPoint = old.anchorPoint;
            var pa = old.anchorPoint != null ? old.anchorPoint.GetComponent<PatrolAnchor>() : null;
            if (pa != null) { pa.UnregisterGoblin(old); pa.RegisterGoblin(ai); }
            ai.gobelinName = go.GetComponent<MonsterUnit>()?.familyName ?? go.name;
        }
        go.name = ai != null ? ai.gobelinName : go.name;
        var unit = go.GetComponent<MonsterUnit>();
        if (unit != null) unit.xp = 0;
        EvolutionFx.Play(transform.position);
        GameHUD.Toast($"Évolution : {ai?.gobelinName ?? go.name} !", GameHUD.GoldColor);
        MonsterSelector.Instance?.DeselectAll();
        Destroy(gameObject);
        return unit;
    }

    public static int TotalUpkeep()
    {
        int t = 0;
        // nourri de chair aujourd'hui : moitié moins d'entretien
        foreach (var m in All)
            if (m != null && !m.summoned)
            {
                var life = m.GetComponent<MonsterLife>();
                t += life != null && life.FedToday ? Mathf.CeilToInt(m.upkeepPerDay * 0.5f) : m.upkeepPerDay;
            }
        return t;
    }
}
