using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mode exploration : le Noyau projette sa conscience dans un de ses monstres
/// et le contrôle directement (base du mode "dungeon crawler").
/// PC     : ZQSD/WASD ou flèches pour bouger, Espace/clic pour attaquer, E ou Échap pour quitter.
/// Mobile : joystick virtuel + bouton d'attaque (HUD).
/// </summary>
public class PossessionController : MonoBehaviour
{
    public static PossessionController Instance { get; private set; }

    [Tooltip("Coût en mana pour posséder un monstre")]
    public int possessionCost = 0;

    public GobelinAI Possessed { get; private set; }
    public bool IsPossessing => Possessed != null;

    public event System.Action<bool> OnPossessionChanged;

    /// <summary>Valeur fournie par le joystick virtuel du HUD.</summary>
    [HideInInspector] public Vector2 virtualStick;
    private bool virtualAttackRequested;
    private float savedZoom;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Possess(GobelinAI goblin)
    {
        if (goblin == null || !goblin.IsAlive()) return false;
        if (IsPossessing) Release();
        if (possessionCost > 0 && ManaSystem.Instance != null && !ManaSystem.Instance.TrySpend(possessionCost)) return false;

        Possessed = goblin;
        goblin.SetPossessed(true);
        DungeonBuilder.Instance?.SetMode(DungeonBuilder.BuildMode.None);
        MonsterSelector.Instance?.DeselectAll();

        var cc = CameraController.Instance;
        if (cc != null)
        {
            cc.SetFollowTarget(goblin.transform);
            var cam = cc.GetComponent<Camera>();
            savedZoom = cam.orthographicSize;
            cam.orthographicSize = Mathf.Min(savedZoom, 6f);
        }

        OnPossessionChanged?.Invoke(true);
        StoryManager.Instance?.OnFirstPossession();
        return true;
    }

    public void Release()
    {
        if (Possessed != null) Possessed.SetPossessed(false);
        Possessed = null;
        virtualStick = Vector2.zero;

        var cc = CameraController.Instance;
        if (cc != null)
        {
            cc.SetFollowTarget(null);
            if (savedZoom > 0f) cc.GetComponent<Camera>().orthographicSize = savedZoom;
        }
        OnPossessionChanged?.Invoke(false);
    }

    public void OnPossessedDied(GobelinAI goblin)
    {
        if (goblin != Possessed) return;
        Possessed = null;
        GameHUD.Toast("Votre réceptacle a été détruit ! Votre conscience retourne au cristal.", GameHUD.DangerColor);
        Release();
    }

    /// <summary>Bouton d'attaque du HUD.</summary>
    public void RequestAttack() => virtualAttackRequested = true;
    private bool virtualSkillRequested;
    public void RequestSkill() => virtualSkillRequested = true;
    private Transform vision;

    private void Update()
    {
        if (!IsPossessing) return;
        if (Possessed == null) { Release(); return; }

        var kb = Keyboard.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
        {
            Release();
            return;
        }

        bool attack = virtualAttackRequested;
        virtualAttackRequested = false;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) attack = true;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerUtil.IsOverUI() && !PointerUtil.IsTouch) attack = true;
        var pad = Gamepad.current;
        if (pad != null && pad.buttonSouth.wasPressedThisFrame) attack = true;

        if (attack) Possessed.ManualAttack();

        bool skill = virtualSkillRequested;
        virtualSkillRequested = false;
        if (kb != null && kb.qKey.wasPressedThisFrame) skill = true;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame && !PointerUtil.IsOverUI() && !PointerUtil.IsTouch) skill = true;
        if (pad != null && pad.buttonWest.wasPressedThisFrame) skill = true;
        if (skill) Possessed.ManualSkill();
    }

    private void LateUpdate()
    {
        // vision du monstre : seul ce qu'il voit autour de lui reste clair
        if (IsPossessing && Possessed != null)
        {
            if (vision == null) vision = MakeVision();
            vision.gameObject.SetActive(true);
            vision.position = Possessed.transform.position;
            float r = Mathf.Max(2.6f, Possessed.detectionRange * 0.7f);
            vision.localScale = new Vector3(r, r, 1f);
        }
        else if (vision != null) vision.gameObject.SetActive(false);
    }

    private static Sprite visionSprite;

    /// <summary>Voile sombre percé d'un cercle (rayon 1 unité à l'échelle 1) : la vue du monstre possédé.</summary>
    private Transform MakeVision()
    {
        if (visionSprite == null)
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);   // 0 au centre, 1 au bord
                    float a = Mathf.SmoothStep(0f, 0.88f, Mathf.InverseLerp(0.07f, 0.13f, d));
                    tex.SetPixel(x, y, new Color(0.01f, 0.01f, 0.03f, a));
                }
            tex.Apply();
            // le cercle clair (d < 0.1 de la demi-largeur) mesure 1 unité de rayon : 128 px * 0.1 = 12,8 px par unité
            visionSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 12.8f);
        }
        var go = new GameObject("Vision du monstre");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = visionSprite;
        sr.sortingOrder = 55;
        return go.transform;
    }

    private void FixedUpdate()
    {
        if (!IsPossessing || Possessed == null) return;

        Vector2 dir = virtualStick;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;
        }
        var pad = Gamepad.current;
        if (pad != null) dir += pad.leftStick.ReadValue();

        Possessed.ManualMove(Vector2.ClampMagnitude(dir, 1f));
    }
}
