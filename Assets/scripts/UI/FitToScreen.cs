using UnityEngine;

/// <summary>
/// Fenêtre centrée (dialogues, choix d'évolution, options...) : suit la taille d'interface choisie mais ne dépasse
/// jamais l'écran — réduite si besoin (téléphone en paysage, grande taille d'interface).
/// </summary>
public class FitToScreen : MonoBehaviour
{
    private RectTransform rt, parent;
    private float lastW = -1f, lastH = -1f, lastScale = -1f;

    private void Awake()
    {
        rt = (RectTransform)transform;
        parent = rt.parent as RectTransform;
    }

    private void LateUpdate()
    {
        if (parent == null) return;
        float W = parent.rect.width, H = parent.rect.height, ui = GameSettings.UiScale;
        if (Mathf.Approximately(W, lastW) && Mathf.Approximately(H, lastH) && Mathf.Approximately(ui, lastScale)) return;
        lastW = W; lastH = H; lastScale = ui;
        float w = Mathf.Max(1f, rt.rect.width), h = Mathf.Max(1f, rt.rect.height);
        float k = Mathf.Min(ui, W * 0.96f / w, H * 0.94f / h);
        rt.localScale = new Vector3(k, k, 1f);
    }
}
