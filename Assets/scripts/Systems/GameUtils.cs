using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Logs verbeux de l'IA : désactivés par défaut (Debug.Log coûte cher, surtout sur mobile).
/// Activer avec DLog.verbose = true pour déboguer.
/// </summary>
public static class DLog
{
    public static bool verbose = false;

    public static void Log(string msg)
    {
        if (verbose) Debug.Log(msg);
    }
}

/// <summary>
/// Entrée pointeur unifiée : fonctionne avec la souris (PC) et le tactile (mobile).
/// </summary>
public static class PointerUtil
{
    private static Camera cam;
    private static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private static int uiCacheFrame = -1;
    private static bool uiCacheValue;

    public static Camera Cam
    {
        get
        {
            if (cam == null) cam = Camera.main;
            return cam;
        }
    }

    public static Pointer Current => Pointer.current;
    public static bool Available => Pointer.current != null;
    public static bool IsTouch => Pointer.current is Touchscreen;

    public static Vector2 ScreenPos => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
    public static bool Pressed => Pointer.current != null && Pointer.current.press.isPressed;
    public static bool PressedThisFrame => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
    public static bool ReleasedThisFrame => Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;

    public static Vector2 WorldPos => ScreenToWorld(ScreenPos);

    public static Vector2 ScreenToWorld(Vector2 screen)
    {
        if (Cam == null) return Vector2.zero;
        Vector3 w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        return new Vector2(w.x, w.y);
    }

    /// <summary>Nombre de doigts posés sur l'écran (0 sur PC).</summary>
    public static int TouchCount
    {
        get
        {
            var ts = Touchscreen.current;
            if (ts == null) return 0;
            int n = 0;
            foreach (var t in ts.touches)
                if (t.isInProgress) n++;
            return n;
        }
    }

    private static float blockUntil;

    /// <summary>Ignore les clics sur le monde pendant un court instant (ex : après un dialogue).</summary>
    public static void BlockWorldInput(float seconds) => blockUntil = Time.unscaledTime + seconds;

    /// <summary>Le pointeur est-il au-dessus d'un élément d'interface (ou les clics monde sont-ils bloqués) ?</summary>
    public static bool IsOverUI()
    {
        if (Time.unscaledTime < blockUntil) return true;
        if (uiCacheFrame == Time.frameCount) return uiCacheValue;
        uiCacheFrame = Time.frameCount;
        uiCacheValue = false;

        var es = EventSystem.current;
        if (es == null || Pointer.current == null) return false;

        var data = new PointerEventData(es) { position = ScreenPos };
        uiHits.Clear();
        es.RaycastAll(data, uiHits);
        uiCacheValue = uiHits.Count > 0;
        return uiCacheValue;
    }
}

/// <summary>
/// Sprites générés par code (en attendant de vrais visuels).
/// </summary>
public static class SpriteUtil
{
    private static Sprite square, circle, rounded, diamond;

    /// <summary>Carré blanc d'une unité monde.</summary>
    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return square;
        }
    }

    /// <summary>Disque doux d'une unité monde de diamètre.</summary>
    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int s = 32;
                var tex = new Texture2D(s, s) { filterMode = FilterMode.Bilinear };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 4f)));
                    }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            }
            return circle;
        }
    }

    private static Sprite glow;

    /// <summary>Halo très doux (brume, lueurs) : dégradé progressif jusqu'au bord, sans contour.</summary>
    public static Sprite Glow
    {
        get
        {
            if (glow == null)
            {
                const int s = 64;
                var tex = new Texture2D(s, s) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a * a * (3f - 2f * a)));
                    }
                tex.Apply();
                glow = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            }
            return glow;
        }
    }

    /// <summary>Losange (marqueur de point d'ancrage).</summary>
    public static Sprite Diamond
    {
        get
        {
            if (diamond == null)
            {
                const int s = 32;
                var tex = new Texture2D(s, s) { filterMode = FilterMode.Bilinear };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Abs(x + 0.5f - s / 2f) + Mathf.Abs(y + 0.5f - s / 2f);
                        float a = d < s / 2f - 1 ? (d > s / 2f - 5 ? 1f : 0.35f) : 0f;
                        tex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                tex.Apply();
                diamond = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            }
            return diamond;
        }
    }

    /// <summary>Rectangle arrondi 9-slice pour l'UI.</summary>
    public static Sprite RoundedUI
    {
        get
        {
            if (rounded == null)
            {
                const int s = 48; const float r = 14f;
                var tex = new Texture2D(s, s) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, s - r);
                        float cy = Mathf.Clamp(y + 0.5f, r, s - r);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
                    }
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            }
            return rounded;
        }
    }
}

/// <summary>
/// Passage d'escalier : quand un chemin A* emprunte un lien entre deux étages
/// (deux points très éloignés), l'unité est téléportée à l'autre bout.
/// </summary>
public static class PathPortal
{
    public static bool TryTeleport(Rigidbody2D rb, Pathfinding.Path path, int wp)
    {
        var vp = path.vectorPath;
        if (wp >= vp.Count - 1) return false;
        Vector2 a = vp[wp], b = vp[wp + 1];
        if ((b - a).sqrMagnitude < 16f) return false;
        if (DungeonStairs.At(a, 1.2f) == null && DungeonStairs.At(b, 1.2f) == null) return false;
        rb.position = b;
        rb.transform.position = b;
        return true;
    }
}
