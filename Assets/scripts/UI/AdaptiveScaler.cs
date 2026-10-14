using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mise à l'échelle des interfaces selon la forme de l'écran : sur un écran allongé (téléphone 19,5:9, 20:9...),
/// on cale la HAUTEUR (1080 unités) pour que rien ne déborde en bas ; sur un écran plus carré (tablette 4:3),
/// on cale la LARGEUR (1920 unités).
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class AdaptiveScaler : MonoBehaviour
{
    private CanvasScaler scaler;
    private float lastAspect = -1f;

    public static void Setup(CanvasScaler s)
    {
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        if (s.GetComponent<AdaptiveScaler>() == null) s.gameObject.AddComponent<AdaptiveScaler>();
        s.matchWidthOrHeight = Match();
    }

    private static float Match()
    {
        float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
        return aspect >= 1.7f ? 1f : 0f;
    }

    private void Awake() => scaler = GetComponent<CanvasScaler>();

    private void Update()
    {
        float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 0f;
        if (Mathf.Abs(aspect - lastAspect) < 0.001f) return;
        lastAspect = aspect;
        scaler.matchWidthOrHeight = Match();
    }
}
