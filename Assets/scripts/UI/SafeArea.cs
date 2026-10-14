using UnityEngine;

/// <summary>
/// Garde une interface dans la zone sûre de l'écran (encoche, coins arrondis, barre de gestes des téléphones).
/// À poser sur un RectTransform étiré plein écran : ses ancres suivent Screen.safeArea.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform rt;
    private Rect last;
    private Vector2Int lastSize;

    public static void Apply(RectTransform t)
    {
        if (t != null && t.GetComponent<SafeArea>() == null) t.gameObject.AddComponent<SafeArea>();
    }

    private void Awake() => rt = (RectTransform)transform;

    private void Update()
    {
        var area = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (area == last && size == lastSize) return;
        last = area;
        lastSize = size;
        if (size.x <= 0 || size.y <= 0) return;
        rt.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
        rt.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
