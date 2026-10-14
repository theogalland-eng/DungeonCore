using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Montagne-volcan en relief (bandes générées par ReliefBuilder / Tools~/make_relief.py).
///   - Vue ÎLE : le volcan est plein, fume, rougeoie ; l'intérieur du donjon est caché.
///   - Vue DONJON : le relief s'efface en fondu et révèle l'intérieur (vue de construction).
/// La vue donjon s'impose d'elle-même quand on construit, qu'on possède un monstre ou qu'on regarde un étage profond.
/// </summary>
public class MountainRelief : MonoBehaviour
{
    public static MountainRelief Instance { get; private set; }

    public List<SpriteRenderer> bands = new List<SpriteRenderer>();
    public Vector2 craterWorld;
    [Tooltip("Durée du fondu entre les vues (s)")]
    public float fadeTime = 0.35f;

    /// <summary>Choix du joueur (bouton du HUD). La vue effective peut être forcée en donjon.</summary>
    public bool UserDungeonView { get; private set; } = true;
    public bool DungeonView => UserDungeonView || Forced;
    public event System.Action<bool> OnViewChanged;

    private float alpha = 1f;
    private bool lastShown;
    private float hideTimer;
    private Light2D craterLight;
    private float nextGeyser;
    private readonly HashSet<Renderer> hiddenR = new HashSet<Renderer>();
    private readonly HashSet<Light2D> hiddenL = new HashSet<Light2D>();

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (craterWorld != Vector2.zero)
        {
            // Lueur et fumées du cratère (en boucle)
            var lgo = new GameObject("LueurCratere");
            lgo.transform.SetParent(transform, false);
            lgo.transform.position = craterWorld;
            craterLight = lgo.AddComponent<Light2D>();
            craterLight.lightType = Light2D.LightType.Point;
            craterLight.color = new Color(1f, 0.45f, 0.15f);
            craterLight.intensity = 1.4f;
            craterLight.pointLightOuterRadius = 7f;
            craterLight.pointLightInnerRadius = 1f;
            var smoke = Fx.Play("smoke_column", (Vector3)craterWorld + new Vector3(0.2f, 3.2f, 0f), 6.5f, 7f, 40, new Color(0.55f, 0.52f, 0.55f, 0.85f), true);
            if (smoke != null) smoke.transform.SetParent(transform, true);
            var smoke2 = Fx.Play("smoke_column", (Vector3)craterWorld + new Vector3(-0.7f, 2.6f, 0f), 5f, 6f, 40, new Color(0.45f, 0.42f, 0.46f, 0.7f), true);
            if (smoke2 != null) smoke2.transform.SetParent(transform, true);
        }
        nextGeyser = Time.time + 4f;
    }

    /// <summary>Raisons qui imposent la vue donjon.</summary>
    private bool Forced
    {
        get
        {
            if (MainMenu.Instance != null && MainMenu.Instance.InHome) return false;   // l'accueil montre l'île
            var b = DungeonBuilder.Instance;
            if (b != null && b.GetCurrentMode() != DungeonBuilder.BuildMode.None) return true;
            if (PossessionController.Instance != null && PossessionController.Instance.IsPossessing) return true;
            if (DungeonLayers.Instance != null && DungeonLayers.Instance.CurrentView != 0) return true;
            if (CinematicDirector.Instance != null && CinematicDirector.Instance.ForceDungeonView) return true;
            return false;
        }
    }

    public void SetUserView(bool dungeon)
    {
        UserDungeonView = dungeon;
        OnViewChanged?.Invoke(DungeonView);
    }

    public void Toggle() => SetUserView(!UserDungeonView);

    private void Update()
    {
        bool showRelief = !DungeonView || (MainMenu.Instance != null && MainMenu.Instance.InHome);
        float target = showRelief ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
        foreach (var b in bands)
        {
            if (b == null) continue;
            b.enabled = alpha > 0.001f;
            var c = b.color;
            c.a = alpha;
            b.color = c;
        }
        if (craterLight != null) craterLight.intensity = alpha * (1.2f + 0.35f * Mathf.PerlinNoise(Time.time * 1.7f, 0f));
        if (showRelief != lastShown)
        {
            lastShown = showRelief;
            OnViewChanged?.Invoke(!showRelief);
            hideTimer = 0f;
        }

        // Geyser de magma de temps en temps (vue île)
        if (showRelief && craterWorld != Vector2.zero && Time.time > nextGeyser)
        {
            nextGeyser = Time.time + Random.Range(7f, 16f);
            Fx.Play("magma_geyser", (Vector3)craterWorld + new Vector3(Random.Range(-0.8f, 0.8f), 0.9f, 0f), 3f, 12f, 41, null, false, null, new Color(1f, 0.5f, 0.2f));
        }

        // L'intérieur de la montagne disparaît en vue île (monstres, pièges, torches, cristal)
        hideTimer -= Time.unscaledDeltaTime;
        if (hideTimer <= 0f)
        {
            hideTimer = 0.25f;
            UpdateInteriorVisibility(showRelief && alpha > 0.5f);
        }
    }

    private void UpdateInteriorVisibility(bool hide)
    {
        var t = DungeonTerritory.Instance;
        var layers = DungeonLayers.Instance;
        if (t == null) return;
        bool Inside(Vector3 p) => t.IsInside(p) && (layers == null || layers.IndexAt(p) == 0);

        // On ne touche qu'aux rendus éteints par nous (un piège qui se réarme reste caché, etc.)
        void Apply(Component c)
        {
            if (c == null) return;
            bool h = hide && Inside(c.transform.position);
            foreach (var r in c.GetComponentsInChildren<Renderer>(true))
            {
                if (h && r.enabled) { r.enabled = false; hiddenR.Add(r); }
                else if (!h && hiddenR.Remove(r)) r.enabled = true;
            }
            foreach (var l in c.GetComponentsInChildren<Light2D>(true))
            {
                if (h && l.enabled) { l.enabled = false; hiddenL.Add(l); }
                else if (!h && hiddenL.Remove(l)) l.enabled = true;
            }
        }
        hiddenR.RemoveWhere(r => r == null);
        hiddenL.RemoveWhere(l => l == null);
        foreach (var g in GobelinAI.All) Apply(g);
        foreach (var a in AdventurerAI.All) Apply(a);
        foreach (var p in PlacedObject.All) Apply(p);
        foreach (var s in DungeonStairs.All) Apply(s);
        var core = FindCore();
        if (core != null) Apply(core);
    }

    private DungeonCore core;
    private DungeonCore FindCore()
    {
        if (core == null) core = FindFirstObjectByType<DungeonCore>();
        return core;
    }
}
