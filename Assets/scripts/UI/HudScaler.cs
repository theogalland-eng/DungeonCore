using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TAILLE DE L'INTERFACE (Options) : au lieu d'agrandir tout le canevas — ce qui faisait déborder les colonnes de
/// boutons et chevaucher les panneaux du haut, dessinés pour 1920×1080 — chaque bloc grandit depuis son coin
/// d'ancrage (sa position est mise à l'échelle avec lui, les groupes gardent donc leur disposition).
/// L'échelle de chaque rangée est plafonnée pour que rien ne se chevauche ni ne sorte de l'écran :
///   - rangée du haut : les blocs de gauche, du centre et de droite doivent tenir côte à côte ;
///   - panneaux étirés en hauteur (panneau de construction) : leur hauteur visible reste celle de l'écran.
/// Les blocs plein écran (dialogues, fin de partie...) ne sont pas touchés.
/// Si le code du jeu déplace un bloc, sa nouvelle position est reprise comme position de base.
/// </summary>
public class HudScaler : MonoBehaviour
{
    private class Entry
    {
        public RectTransform rt;
        public Vector2 basePos, applied;
        public Vector2 baseOffMin, baseOffMax, appliedOffMin, appliedOffMax;
        public bool stretchY;
    }

    private readonly Dictionary<RectTransform, Entry> entries = new Dictionary<RectTransform, Entry>();
    private RectTransform root;

    /// <summary>Échelle appliquée à la rangée du haut et au reste (lue par le panneau de construction pour sa poignée).</summary>
    public static float TopScale = 1f, Scale = 1f;

    private void Awake() => root = (RectTransform)transform;

    private static bool IsPoint(RectTransform r) => r.anchorMin == r.anchorMax;
    private static bool IsFull(RectTransform r) => r.anchorMin == Vector2.zero && r.anchorMax == Vector2.one;
    private static bool IsStretchY(RectTransform r) => r.anchorMin.x == r.anchorMax.x && r.anchorMin.y == 0f && r.anchorMax.y == 1f;

    private void LateUpdate()
    {
        float W = root.rect.width, H = root.rect.height;
        if (W <= 0f || H <= 0f) return;
        float s = Mathf.Clamp(GameSettings.UiScale, 0.7f, 1.6f);

        // largeurs de base de la rangée du haut (blocs ancrés en haut à gauche / au centre / à droite)
        float left = 0f, center = 0f, right = 0f;
        foreach (Transform t in root)
        {
            var r = t as RectTransform;
            if (r == null || !r.gameObject.activeSelf || !IsPoint(r) || r.anchorMin.y < 0.99f) continue;
            var e = Get(r);
            float w = r.rect.width;
            if (r.anchorMin.x < 0.01f) left = Mathf.Max(left, e.basePos.x + w);
            else if (r.anchorMin.x > 0.99f) right = Mathf.Max(right, -e.basePos.x + w);
            else center = Mathf.Max(center, w);
        }
        float gap = 12f;
        float fitL = left + center * 0.5f > 0f ? (W * 0.5f - gap) / (left + center * 0.5f) : s;
        float fitR = right + center * 0.5f > 0f ? (W * 0.5f - gap) / (right + center * 0.5f) : s;
        TopScale = Mathf.Min(s, fitL, fitR);
        Scale = Mathf.Min(s, Mathf.Max(TopScale, s * 0.9f));

        foreach (Transform t in root)
        {
            var r = t as RectTransform;
            if (r == null || IsFull(r)) continue;
            var e = Get(r);
            if (IsPoint(r))
            {
                // le jeu a déplacé le bloc : nouvelle position de base
                if ((r.anchoredPosition - e.applied).sqrMagnitude > 0.01f) e.basePos = r.anchoredPosition;
                float k = r.anchorMin.y > 0.99f ? TopScale : Scale;
                r.localScale = new Vector3(k, k, 1f);
                e.applied = e.basePos * k;
                r.anchoredPosition = e.applied;
            }
            else if (e.stretchY)
            {
                if ((r.offsetMin - e.appliedOffMin).sqrMagnitude > 0.01f || (r.offsetMax - e.appliedOffMax).sqrMagnitude > 0.01f)
                { e.baseOffMin = r.offsetMin; e.baseOffMax = r.offsetMax; }
                float k = Scale;
                float bottom = e.baseOffMin.y, top = -e.baseOffMax.y;           // marges visibles, mises à l'échelle
                float h = (H - (bottom + top) * k) / k;                         // hauteur non mise à l'échelle
                float yc = bottom * k + (H - (bottom + top) * k) * r.pivot.y;   // position visible du pivot
                float w = e.baseOffMax.x - e.baseOffMin.x;
                float px = r.pivot.x;
                float pivotX = (e.baseOffMin.x + w * px) * k;                    // le pivot suit la marge mise à l'échelle
                r.localScale = new Vector3(k, k, 1f);
                var offMin = new Vector2(pivotX - w * px, yc - h * r.pivot.y);
                var offMax = new Vector2(offMin.x + w, yc + h * (1f - r.pivot.y) - H);
                r.offsetMin = offMin; r.offsetMax = offMax;
                e.appliedOffMin = r.offsetMin; e.appliedOffMax = r.offsetMax;
            }
        }
    }

    private Entry Get(RectTransform r)
    {
        if (entries.TryGetValue(r, out var e)) return e;
        e = new Entry { rt = r, basePos = r.anchoredPosition, applied = r.anchoredPosition, stretchY = IsStretchY(r),
                        baseOffMin = r.offsetMin, baseOffMax = r.offsetMax, appliedOffMin = r.offsetMin, appliedOffMax = r.offsetMax };
        entries[r] = e;
        return e;
    }
}
