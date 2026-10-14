using System;
using TMPro;
using UnityEngine;

/// <summary>
/// « Pensées du Noyau » : une suite d'objectifs discrets (pas de fenêtre de quête, ce monde n'a pas de système)
/// qui guident la progression. Chaque objectif atteint rapporte un peu de mana et d'expérience.
/// </summary>
public class ObjectiveSystem : MonoBehaviour
{
    public static ObjectiveSystem Instance { get; private set; }

    private class Objective
    {
        public string text;
        public Func<int> progress;   // valeur actuelle
        public int goal;
        public int mana, xp;
    }

    public int Index { get; private set; }

    private Objective[] list;
    private RectTransform panel;
    private TextMeshProUGUI label;
    private float nextCheck;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        var gm = (Func<GameManager>)(() => GameManager.Instance);
        list = new[]
        {
            O("Creuser un vrai repaire", () => DungeonTerritory.Instance != null ? DungeonTerritory.Instance.CellCount : 0, 20, 60, 20),
            O("Donner vie à une première créature", () => MonsterUnit.All.Count, 1, 40, 15),
            O("Tendre un piège dans mes couloirs", () => PlacedObject.Count(ItemKind.Trap), 1, 40, 15),
            O("Absorber l'essence d'un intrus", () => gm() != null ? gm().statKills : 0, 1, 80, 30),
            O("Survivre à l'évaluation de la Guilde", () => GuildSystem.Instance != null && GuildSystem.Instance.Assessed ? 1 : 0, 1, 150, 60),
            O("Faire évoluer une de mes créatures", () => gm() != null ? gm().statEvolutions : 0, 1, 120, 50),
            O("Ouvrir un nouvel étage dans la roche", () => UnlockedLayers(), 2, 150, 60),
            O("Être classé au rang C par la Guilde", () => GuildSystem.Instance != null ? GuildSystem.Instance.officialRank + 1 : 0, 4, 200, 80),
            O("Absorber vingt intrus", () => gm() != null ? gm().statKills : 0, 20, 250, 100),
            O("Être classé au rang A : l'élite viendra", () => GuildSystem.Instance != null ? GuildSystem.Instance.officialRank + 1 : 0, 6, 400, 150),
            O("Vaincre un héros", () => gm() != null ? gm().statHeroKills : 0, 1, 800, 300),
            O("Devenir une légende : rang SSS", () => GuildSystem.Instance != null ? GuildSystem.Instance.officialRank + 1 : 0, 9, 1500, 500),
        };
    }

    private static Objective O(string t, Func<int> p, int goal, int mana, int xp) => new Objective { text = t, progress = p, goal = goal, mana = mana, xp = xp };

    private static int UnlockedLayers()
    {
        if (DungeonLayers.Instance == null) return 1;
        int n = 0;
        foreach (var l in DungeonLayers.Instance.layers) if (l.unlocked) n++;
        return n;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Restore(int index) => Index = Mathf.Clamp(index, 0, list.Length);

    private void Update()
    {
        if (panel == null && GameHUD.Instance != null)
        {
            panel = GameHUD.Instance.AddPanel("Objectif", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -176), new Vector2(560, 64));
            label = GameHUD.AddLabel(panel, "", 19, new Color(0.85f, 0.82f, 0.95f), new Vector2(16, -8), new Vector2(530, 50), TextAlignmentOptions.TopLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
        }
        if (panel == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;
        bool home = MainMenu.Instance != null && MainMenu.Instance.InHome;
        bool show = !home && !CrawlerMode.Active && Index < list.Length && GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.Playing;
        panel.gameObject.SetActive(show);
        if (!show) return;

        var o = list[Index];
        int p = Mathf.Min(o.progress(), o.goal);
        if (p >= o.goal)
        {
            Complete(o);
            return;
        }
        string prog = o.goal > 1 ? $"  <color=#9A96B0>({p}/{o.goal})</color>" : "";
        label.text = $"<size=80%><color=#B79CFF>Pensées du Noyau</color></size>\n{o.text}{prog}";
    }

    private void Complete(Objective o)
    {
        Index++;
        ManaSystem.Instance?.Add(o.mana);
        PlayerLevel.Instance?.AddXP(o.xp);
        GameHUD.Toast($"Accompli : {o.text}  (+{o.mana} mana)", GameHUD.GoldColor);
        Sfx.Play("coin");
    }
}
