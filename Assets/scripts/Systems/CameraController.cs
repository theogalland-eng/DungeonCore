using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Caméra de jeu.
/// PC     : ZQSD/WASD ou flèches, molette = zoom, clic droit/molette glissé = déplacer.
/// Mobile : glisser un doigt (en mode Sélection) ou deux doigts = déplacer, pincer = zoom.
/// Suit le monstre possédé en mode exploration.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Déplacement")]
    public float keyboardPanSpeed = 12f;
    [Header("Zoom")]
    public float minZoom = 4f;
    public float maxZoom = 16f;
    public float scrollZoomStep = 1f;
    [Header("Limites (auto depuis la roche du donjon)")]
    public bool autoBounds = true;
    public Rect bounds = new Rect(-80, -10, 70, 100);
    [Header("Suivi")]
    public float followLerp = 8f;

    /// <summary>Une cinématique pilote la caméra : pas de contrôle joueur ni de limites.</summary>
    [HideInInspector] public bool cinematic;

    private Camera cam;
    private Transform followTarget;
    private Vector3 dragOriginWorld;
    private bool mouseDragging;
    private float lastPinchDist;
    private Vector2 lastTwoFingerMid;
    private bool touchPanning;
    private Vector2 touchPanStartScreen;
    private bool touchPanStartedOnUI;
    private float shakeTime, shakeAmp;
    private Vector3 shakeOffset;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (DungeonLayers.Instance != null && DungeonLayers.Instance.layers.Count > 1)
        {
            DungeonLayers.Instance.SetView(0);
        }
        else if (autoBounds && DungeonBuilder.Instance != null && DungeonBuilder.Instance.stoneTilemap != null)
        {
            var tm = DungeonBuilder.Instance.stoneTilemap;
            tm.CompressBounds();
            var b = tm.localBounds;
            Vector3 min = tm.transform.TransformPoint(b.min);
            Vector3 max = tm.transform.TransformPoint(b.max);
            bounds = Rect.MinMaxRect(min.x - 6f, min.y - 6f, max.x + 30f, max.y + 6f);
        }
    }

    public void SetFollowTarget(Transform t) => followTarget = t;

    /// <summary>Limites de déplacement (l'étage affiché).</summary>
    public void SetBounds(Rect r)
    {
        autoBounds = false;
        bounds = r;
        Clamp();
    }

    public void Shake(float amplitude, float duration)
    {
        if (!GameSettings.ScreenShake) return;
        shakeAmp = amplitude;
        shakeTime = duration;
    }

    public void CenterOn(Vector2 pos)
    {
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        Clamp();
    }

    private void LateUpdate()
    {
        transform.position -= shakeOffset;
        shakeOffset = Vector3.zero;

        float dt = Time.unscaledDeltaTime;
        if (cinematic)
        {
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                shakeOffset = (Vector3)(Random.insideUnitCircle * shakeAmp);
                transform.position += shakeOffset;
            }
            return;
        }

        if (followTarget != null)
        {
            DungeonLayers.Instance?.SyncViewTo(followTarget.position);
            Vector3 target = new Vector3(followTarget.position.x, followTarget.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followLerp * dt));
        }
        else
        {
            HandleKeyboard(dt);
            HandleMouse();
        }
        HandleTouch();
        HandleScrollZoom();
        Clamp();

        if (shakeTime > 0f)
        {
            shakeTime -= dt;
            shakeOffset = (Vector3)(Random.insideUnitCircle * shakeAmp);
            transform.position += shakeOffset;
        }
    }

    private void HandleKeyboard(float dt)
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        Vector2 dir = Vector2.zero;
        // Touches physiques : W/A/S/D = Z/Q/S/D sur clavier AZERTY
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;
        if (dir != Vector2.zero)
            transform.position += (Vector3)(dir.normalized * keyboardPanSpeed * (cam.orthographicSize / 8f) * dt);
    }

    private void HandleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        bool dragButton = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
        Vector2 sp = mouse.position.ReadValue();

        if ((mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame) && !PointerUtil.IsOverUI())
        {
            mouseDragging = true;
            dragOriginWorld = ScreenToWorld(sp);
        }
        if (!dragButton) mouseDragging = false;

        if (mouseDragging)
        {
            Vector3 now = ScreenToWorld(sp);
            transform.position += dragOriginWorld - now;
        }
    }

    private void HandleScrollZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null || PointerUtil.IsTouch) return;
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f || PointerUtil.IsOverUI()) return;

        Vector3 before = ScreenToWorld(mouse.position.ReadValue());
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - Mathf.Sign(scroll) * scrollZoomStep, minZoom, maxZoom);
        Vector3 after = ScreenToWorld(mouse.position.ReadValue());
        if (followTarget == null) transform.position += before - after;   // zoom vers le curseur
    }

    private void HandleTouch()
    {
        var ts = Touchscreen.current;
        if (ts == null) return;

        int count = 0;
        Vector2 p0 = Vector2.zero, p1 = Vector2.zero;
        foreach (var t in ts.touches)
        {
            if (!t.isInProgress) continue;
            if (count == 0) p0 = t.position.ReadValue();
            else if (count == 1) p1 = t.position.ReadValue();
            count++;
        }

        if (count >= 2)
        {
            touchPanning = false;
            float dist = Vector2.Distance(p0, p1);
            Vector2 mid = (p0 + p1) / 2f;
            if (lastPinchDist > 0f)
            {
                float ratio = lastPinchDist / Mathf.Max(1f, dist);
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * ratio, minZoom, maxZoom);
                if (followTarget == null)
                    transform.position += ScreenToWorld(lastTwoFingerMid) - ScreenToWorld(mid);
            }
            lastPinchDist = dist;
            lastTwoFingerMid = mid;
            return;
        }
        lastPinchDist = 0f;

        // Un doigt : déplacer la vue (sauf quand on creuse, qui utilise le glisser)
        if (count == 1 && followTarget == null && AllowOneFingerPan())
        {
            if (!touchPanning)
            {
                touchPanning = true;
                touchPanStartScreen = p0;
                touchPanStartedOnUI = PointerUtil.IsOverUI();
                dragOriginWorld = ScreenToWorld(p0);
            }
            else if (!touchPanStartedOnUI && Vector2.Distance(p0, touchPanStartScreen) > 18f)
            {
                transform.position += dragOriginWorld - ScreenToWorld(p0);
            }
        }
        else if (count == 0)
        {
            touchPanning = false;
        }
    }

    private bool AllowOneFingerPan()
    {
        var b = DungeonBuilder.Instance;
        return b == null || b.GetCurrentMode() != DungeonBuilder.BuildMode.Digging;
    }

    private Vector3 ScreenToWorld(Vector2 sp)
    {
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 0f));
        w.z = 0f;
        return w;
    }

    private void Clamp()
    {
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, bounds.xMin, bounds.xMax);
        p.y = Mathf.Clamp(p.y, bounds.yMin, bounds.yMax);
        transform.position = p;
    }
}
