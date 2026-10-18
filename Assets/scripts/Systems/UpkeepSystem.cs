using UnityEngine;

/// <summary>
/// Entretien du donjon : chaque matin, les monstres et les pièges consomment une quantité fixe de mana.
/// Si la réserve ne suffit pas, tout est payé autant que possible puis :
///   - les monstres, affamés, perdent une partie de leurs PV ;
///   - les pièges s'arrêtent jusqu'au lendemain.
/// Empêche d'empiler des dizaines de monstres et de pièges sans réfléchir.
/// </summary>
public class UpkeepSystem : MonoBehaviour
{
    public static UpkeepSystem Instance { get; private set; }

    [Tooltip("PV perdus par les monstres quand l'entretien n'est pas payé (part des PV max)")]
    [Range(0f, 1f)] public float starvationDamage = 0.35f;

    public int LastPaid { get; private set; }
    public int LastDue { get; private set; }

    /// <summary>Coût quotidien : ajusté à la durée du jour (même coût par minute réelle) et à la difficulté.</summary>
    public int DailyCost => Mathf.RoundToInt((MonsterUnit.TotalUpkeep() * ThemedRooms.MonsterUpkeepMult() + PlacedObject.TotalUpkeep() * Talents.ObjectUpkeepMult) * DayScale * GameDifficulty.Upkeep);
    public static float DayScale => GameManager.Instance != null ? GameManager.Instance.dayLengthSeconds / 90f : 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay;
    }

    private void OnNewDay(int day)
    {
        if (CrawlerMode.Active) return;
        var mana = ManaSystem.Instance;
        int due = DailyCost;
        LastDue = due;
        if (due <= 0 || mana == null) { LastPaid = 0; SetTrapsStarved(false); return; }

        if (mana.Current >= due)
        {
            mana.TrySpend(due, false);
            LastPaid = due;
            SetTrapsStarved(false);
            GameHUD.Toast($"Entretien du donjon : -{due} mana", GameHUD.ManaColor);
            return;
        }

        // Réserve insuffisante : on paie ce qu'on peut, le reste du donjon souffre
        LastPaid = mana.Current;
        mana.TrySpend(mana.Current, false);
        foreach (var g in GobelinAI.All.ToArray())
        {
            if (g == null || !g.IsAlive()) continue;
            var u = g.GetComponent<MonsterUnit>();
            if (u != null && u.summoned) continue;
            g.TakeDamage(g.maxHealth * starvationDamage);
        }
        SetTrapsStarved(true);
        GameHUD.Toast($"Mana insuffisant pour l'entretien ({due}) : monstres affamés, pièges à l'arrêt !", GameHUD.DangerColor);
        StoryManager.Instance?.OnUpkeepFailed();
    }

    private static void SetTrapsStarved(bool starved)
    {
        foreach (var p in PlacedObject.All.ToArray())
            if (p != null && p.upkeepPerDay > 0 && p.Starved != starved) p.SetStarved(starved);
    }
}
