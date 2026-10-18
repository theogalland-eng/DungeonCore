using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// OBJECTIF DE FIN DE PARTIE (mode Noyau) : dominer l'île. Trois accomplissements, dans l'ordre qu'on veut :
///   1. la colonie humaine est soumise (trêve et relation au plus haut) ou conquise (plus un seul habitant) ;
///   2. le Champion de la Guilde, envoyé quand le donjon atteint le rang S, est tombé dans vos galeries ;
///   3. le cristal repose au Cœur du volcan (le troisième étage souterrain).
/// Les trois réunis : victoire (on peut continuer à régner). Retenus dans les drapeaux de l'histoire (sauvegardés).
/// </summary>
public class Domination : MonoBehaviour
{
    public static Domination Instance { get; private set; }

    public const string ColonyFlag = "dom_colony", ChampionFlag = "dom_champion", HeartFlag = "dom_heart", VictoryFlag = "dom_victory";
    [Tooltip("Rang de la Guilde à partir duquel le Champion est envoyé (6 = S)")] public int championRank = 6;
    [Tooltip("Jours avant le retour d'un Champion qui a fui")] public int championReturnDays = 3;

    private float nextCheck;
    private int championDueDay = -1;

    private static StoryManager Story => StoryManager.Instance;
    public static bool Has(string flag) => Story != null && Story.HasFlag(flag);
    public static int Count => (Has(ColonyFlag) ? 1 : 0) + (Has(ChampionFlag) ? 1 : 0) + (Has(HeartFlag) ? 1 : 0);

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (CrawlerMode.Active || Story == null || Time.time < nextCheck) return;
        nextCheck = Time.time + 2f;
        CheckColony();
        CheckHeart();
        CheckChampion();
        if (Count == 3 && !Has(VictoryFlag)) Victory();
    }

    private void Achieve(string flag, string toast, string line)
    {
        if (Has(flag)) return;
        Story.SetFlag(flag);
        GameHUD.Toast($"Domination de l'île ({Count}/3) : {toast}", GameHUD.GoldColor);
        Story.Play(new StoryManager.Line(StoryManager.SelfName, line));
    }

    private void CheckColony()
    {
        if (Has(ColonyFlag)) return;
        var colony = SettlementManager.Instance;
        var rel = RelationSystem.Instance;
        if (rel != null && rel.truce && rel.relation >= 90f)
            Achieve(ColonyFlag, "la colonie s'est soumise", "Ils m'apportent leurs offrandes et baissent les yeux. Cette colonie vit désormais à mon ombre.");
        else if (colony != null && colony.stage >= 2 && colony.Population == 0 && colony.EverPopulated)
            Achieve(ColonyFlag, "la colonie est tombée", "Plus une âme dans leurs maisons. Leurs murs seront les miens.");
    }

    private void CheckHeart()
    {
        if (Has(HeartFlag)) return;
        var layers = DungeonLayers.Instance;
        var core = FindFirstObjectByType<DungeonCore>();
        if (layers == null || core == null || layers.layers.Count < 4) return;
        if (layers.layers[3].unlocked && layers.IndexAt(core.transform.position) == 3)
            Achieve(HeartFlag, "le cristal repose au Cœur du volcan", "Ici, au plus profond, le magma chante avec moi. Nulle part je ne serai plus fort.");
    }

    private void CheckChampion()
    {
        if (Has(ChampionFlag)) return;
        var guild = GuildSystem.Instance;
        var gm = GameManager.Instance;
        if (guild == null || gm == null || guild.officialRank < championRank) return;
        foreach (var a in AdventurerAI.All) if (a != null && a.IsChampion) return;   // il est déjà là
        if (championDueDay < 0) championDueDay = gm.CurrentDay;
        if (gm.CurrentDay < championDueDay || gm.IsNight || ExpeditionManager.Instance == null || ExpeditionManager.Instance.ExpeditionActive) return;
        if (ExpeditionManager.Instance.LaunchChampion()) championDueDay = gm.CurrentDay + championReturnDays;
    }

    /// <summary>Le Champion vient de tomber.</summary>
    public void OnChampionSlain()
    {
        Achieve(ChampionFlag, "le Champion de la Guilde est tombé", "Leur meilleur. Le plus grand d'entre eux. Et il n'est plus qu'un souvenir dans mes galeries.");
    }

    // ─────────────────────────────────────────────
    // VICTOIRE
    // ─────────────────────────────────────────────

    private void Victory()
    {
        Story.SetFlag(VictoryFlag);
        var go = new GameObject("Victoire");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        AdaptiveScaler.Setup(scaler);
        go.AddComponent<GraphicRaycaster>();
        var root = UIKit.Stretch(UIKit.Rect("Root", go.transform));
        UIKit.Blocker(root, 0.75f);
        var box = UIKit.PanelBox("Carte", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 620));
        box.pivot = new Vector2(0.5f, 0.5f);
        var t = UIKit.LabelAt(box, "L'île est à vous", 64, UIKit.Gold, new Vector2(0, -50), new Vector2(1000, 90), TMPro.TextAlignmentOptions.Top, new Vector2(0.5f, 1));
        UIKit.StyleTitle(t);
        var gm = GameManager.Instance;
        string stats = gm != null ? $"Jours : <b>{gm.CurrentDay}</b>   ·   Intrus absorbés : <b>{gm.statKills}</b>   ·   Héros vaincus : <b>{gm.statHeroKills}</b>   ·   Évolutions : <b>{gm.statEvolutions}</b>" : "";
        UIKit.LabelAt(box, "La colonie plie le genou, le Champion de la Guilde n'est plus, et votre cristal pulse au Cœur du volcan.\n" +
            "Sur tout le continent, on murmure votre nom.\n\n" + stats, 26, UIKit.Text, new Vector2(0, -170), new Vector2(980, 260), TMPro.TextAlignmentOptions.Top, new Vector2(0.5f, 1));
        UIKit.BtnAt(box, "Continuer à régner", () => Destroy(go), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(420, 84), 30, UIKit.Accent);
        Sfx.Play("shine", null, 1f);
    }

    /// <summary>Texte du suivi (fenêtre des talents).</summary>
    public static string Tracker()
    {
        string L(bool done, string text) => done ? $"<color=#9CF0A8>accompli</color>  <color=#A59FB6><s>{text}</s></color>" : $"<color=#E8C77F>·</color> {text}";
        return "<b>Domination de l'île</b>\n" +
               L(Has(ColonyFlag), "Soumettre ou conquérir la colonie") + "\n" +
               L(Has(ChampionFlag), "Abattre le Champion de la Guilde (rang S)") + "\n" +
               L(Has(HeartFlag), "Installer le cristal au Cœur du volcan");
    }
}
