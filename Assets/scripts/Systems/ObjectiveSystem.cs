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
        public Objective How(string h, Func<string> t) { hint = h; target = t; return this; }
        public string text;
        public Func<int> progress;   // valeur actuelle
        public int goal;
        public int mana, xp;
        public string hint;              // comment faire (tutoriel)
        public Func<string> target;      // bouton du HUD à mettre en lumière (TutorialGuide)
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
            O("Creuser un vrai repaire", () => DungeonTerritory.Instance != null ? DungeonTerritory.Instance.CellCount : 0, 20, 60, 20)
                .How("Panneau de droite → Bâtir → Creuser, puis touchez la roche autour du cristal.",
                    () => Mode() == DungeonBuilder.BuildMode.Digging ? null : PanelStep(CatalogCategory.Build, "Item_dig")),
            O("Donner vie à une première créature", () => MonsterUnit.All.Count, 1, 40, 15)
                .How("Onglet Monstres : choisissez une créature puis touchez le sol creusé.",
                    () => Placing(CatalogCategory.Monster) ? null : PanelStep(CatalogCategory.Monster, "Item_*")),
            O("Tendre un piège dans mes couloirs", () => PlacedObject.Count(ItemKind.Trap), 1, 40, 15)
                .How("Onglet Pièges : posez-le sur le passage des intrus.",
                    () => Placing(CatalogCategory.Trap) ? null : PanelStep(CatalogCategory.Trap, "Item_*")),
            O("Aménager une première salle", () => ThemedRooms.Instance != null ? ThemedRooms.Instance.rooms.Count : 0, 1, 60, 25)
                .How("Onglet Salles : trois objets d'un même thème rapprochés (ex. trois ossements) forment une salle.",
                    () => PanelStep(CatalogCategory.Rooms, null)),
            O("Absorber l'essence d'un intrus", () => gm() != null ? gm().statKills : 0, 1, 80, 30)
                .How("Les intrus viennent par bateau. « Appeler » en fait venir sans attendre.", () => "CallBoat"),
            O("Survivre à l'évaluation de la Guilde", () => GuildSystem.Instance != null && GuildSystem.Instance.Assessed ? 1 : 0, 1, 150, 60)
                .How("Après quelques visites, la Guilde juge le donjon : tenez bon.", null),
            O("Faire évoluer une de mes créatures", () => gm() != null ? gm().statEvolutions : 0, 1, 120, 50)
                .How("Touchez une créature aguerrie, puis « Évoluer ».", () => "Evolve"),
            O("Choisir un talent du Noyau", () => Talents.Spent, 1, 80, 30)
                .How("Chaque niveau donne un point : bouton « Talents ».", () => "Talents"),
            O("Ouvrir un nouvel étage dans la roche", () => UnlockedLayers(), 2, 150, 60)
                .How("Onglet Étages : débloquez l'étage suivant, puis creusez un escalier.",
                    () => PanelStep(CatalogCategory.Layer, null)),
            O("Être classé au rang C par la Guilde", () => GuildSystem.Instance != null ? GuildSystem.Instance.officialRank + 1 : 0, 4, 200, 80),
            O("Absorber vingt intrus", () => gm() != null ? gm().statKills : 0, 20, 250, 100),
            O("Être classé au rang A : l'élite viendra", () => GuildSystem.Instance != null ? GuildSystem.Instance.officialRank + 1 : 0, 6, 400, 150),
            O("Vaincre un héros", () => gm() != null ? gm().statHeroKills : 0, 1, 800, 300),
            O("Devenir une légende : rang SSS", () => GuildSystem.Instance != null ? GuildSystem.Instance.officialRank + 1 : 0, 9, 1500, 500),
        };
    }

    private static Objective O(string t, Func<int> p, int goal, int mana, int xp) => new Objective { text = t, progress = p, goal = goal, mana = mana, xp = xp };

    private static DungeonBuilder.BuildMode Mode() => DungeonBuilder.Instance != null ? DungeonBuilder.Instance.GetCurrentMode() : DungeonBuilder.BuildMode.None;

    private static bool Placing(CatalogCategory cat)
    {
        var b = DungeonBuilder.Instance;
        return b != null && b.GetCurrentMode() == DungeonBuilder.BuildMode.PlacingItem && b.SelectedItem != null && b.SelectedItem.category == cat;
    }

    /// <summary>Une étape à la fois : déplier le panneau, puis l'onglet, puis l'élément.</summary>
    private static string PanelStep(CatalogCategory cat, string item)
    {
        var sp = GameHUD.Instance != null ? GameHUD.Instance.Side : null;
        if (sp == null) return null;
        if (!sp.Expanded) return "PanelHandle";
        if (sp.CurrentTab != cat) return "Tab" + (int)cat;
        return item;
    }

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
        if (!show) { TutorialGuide.Point(null); return; }

        var o = list[Index];
        int p = Mathf.Min(o.progress(), o.goal);
        if (p >= o.goal)
        {
            Complete(o);
            return;
        }
        string prog = o.goal > 1 ? $"  <color=#9A96B0>({p}/{o.goal})</color>" : "";
        bool tuto = GameSettings.TutorialHints;
        string hint = tuto && !string.IsNullOrEmpty(o.hint) ? $"\n<size=86%><color=#E8C872>» {o.hint}</color></size>" : "";
        label.text = $"<size=80%><color=#B79CFF>Pensées du Noyau</color></size>\n{o.text}{prog}{hint}";
        float h = hint.Length == 0 ? 64f : label.GetPreferredValues(label.text, 530f, 0f).y + 18f;
        panel.sizeDelta = new Vector2(560f, Mathf.Max(64f, h));
        label.rectTransform.sizeDelta = new Vector2(530f, panel.sizeDelta.y - 14f);
        TutorialGuide.Point(tuto && o.target != null ? o.target() : null);
    }

    private void Complete(Objective o)
    {
        Index++;
        TutorialGuide.Point(null);
        ManaSystem.Instance?.Add(o.mana);
        PlayerLevel.Instance?.AddXP(o.xp);
        GameHUD.Toast($"Accompli : {o.text}  (+{o.mana} mana)", GameHUD.GoldColor);
        Sfx.Play("coin");
    }
}
