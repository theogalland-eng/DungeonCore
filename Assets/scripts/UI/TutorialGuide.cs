using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TUTORIEL GUIDÉ : un cadre doré qui pulse autour du bouton à toucher et une flèche qui rebondit vers lui.
/// La cible est un nom d'objet du HUD (« PanelHandle », « Tab1 », « Item_dig », « Item_* » = premier élément
/// de la liste) fourni par l'objectif en cours (ObjectiveSystem) ; une seule chose à la fois.
/// </summary>
public class TutorialGuide : MonoBehaviour
{
    public static TutorialGuide Instance { get; private set; }

    private RectTransform frame, arrow;
    private Image frameImg, arrowImg;
    private string targetName;
    private RectTransform target;
    private float nextFind;

    public static void Point(string name)
    {
        if (Instance == null)
        {
            if (string.IsNullOrEmpty(name) || GameHUD.Instance == null || GameHUD.Instance.Root == null) return;
            var go = new GameObject("TutorialGuide", typeof(RectTransform));
            go.transform.SetParent(GameHUD.Instance.Root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Instance = go.AddComponent<TutorialGuide>();
            Instance.Build();
        }
        if (Instance.targetName != name) { Instance.targetName = name; Instance.target = null; Instance.nextFind = 0f; }
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Build()
    {
        frame = NewImage("Frame", FrameSprite(), out frameImg);
        frameImg.type = Image.Type.Sliced;
        arrow = NewImage("Arrow", ArrowSprite(), out arrowImg);
        arrow.sizeDelta = new Vector2(48, 48);
    }

    private RectTransform NewImage(string name, Sprite s, out Image img)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        img = go.AddComponent<Image>();
        img.sprite = s;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private void LateUpdate()
    {
        transform.SetAsLastSibling();
        if (string.IsNullOrEmpty(targetName)) { Show(false); return; }
        if ((target == null || !target.gameObject.activeInHierarchy) && Time.unscaledTime >= nextFind)
        {
            nextFind = Time.unscaledTime + 0.25f;
            target = Find(targetName);
        }
        if (target == null || !target.gameObject.activeInHierarchy) { Show(false); return; }
        Show(true);

        // le cadre épouse la cible (dans l'espace de ce calque)
        var self = (RectTransform)transform;
        var c = new Vector3[4];
        target.GetWorldCorners(c);
        Vector2 a = self.InverseTransformPoint(c[0]), b = self.InverseTransformPoint(c[2]);
        Vector2 center = (a + b) * 0.5f, size = b - a;
        float t = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
        frame.anchoredPosition = center;
        frame.sizeDelta = size + Vector2.one * (14f + pulse * 6f);
        frameImg.color = new Color(1f, 0.85f, 0.35f, 0.55f + 0.45f * pulse);

        // la flèche vient du côté où il y a de la place, et rebondit vers la cible
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * 0.5f);
        Vector2 dir;   // direction vers laquelle pointe la flèche
        if (screen.x > Screen.width * 0.62f) dir = Vector2.right;
        else if (screen.x < Screen.width * 0.2f) dir = Vector2.left;
        else if (screen.y > Screen.height * 0.5f) dir = Vector2.up;
        else dir = Vector2.down;
        float half = Mathf.Abs(Vector2.Dot(size, dir)) * 0.5f;
        float bob = Mathf.Abs(Mathf.Sin(t * 4f)) * 14f;
        arrow.anchoredPosition = center - dir * (half + 40f + bob);
        arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        arrowImg.color = new Color(1f, 0.88f, 0.4f, 1f);
    }

    private void Show(bool v)
    {
        if (frame.gameObject.activeSelf != v) frame.gameObject.SetActive(v);
        if (arrow.gameObject.activeSelf != v) arrow.gameObject.SetActive(v);
    }

    private static RectTransform Find(string name)
    {
        var root = GameHUD.Instance != null ? GameHUD.Instance.Root : null;
        if (root == null) return null;
        bool prefix = name.EndsWith("*");
        string key = prefix ? name.Substring(0, name.Length - 1) : name;
        return Search(root, key, prefix);
    }

    private static RectTransform Search(Transform t, string key, bool prefix)
    {
        foreach (Transform ch in t)
        {
            if (!ch.gameObject.activeInHierarchy) continue;
            if (prefix ? ch.name.StartsWith(key) : ch.name == key) return ch as RectTransform;
            var r = Search(ch, key, prefix);
            if (r != null) return r;
        }
        return null;
    }

    // ─────────────────────────────────────────────
    // Sprites (pixel art procédural, 1 texel = 3 unités de canevas)
    // ─────────────────────────────────────────────

    private static Sprite frameSprite, arrowSprite;

    private static Sprite FrameSprite()
    {
        if (frameSprite != null) return frameSprite;
        const int s = 16;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int dx = Mathf.Min(x, s - 1 - x), dy = Mathf.Min(y, s - 1 - y);
                int d = Mathf.Min(dx, dy);
                Color col = Color.clear;
                if (dx + dy < 3) col = Color.clear;                                   // coins taillés
                else if (d == 0 || dx + dy == 3) col = new Color(1f, 1f, 1f, 0.35f);   // halo
                else if (d <= 2 || dx + dy <= 5) col = Color.white;                      // trait épais
                tex.SetPixel(x, y, col);
            }
        tex.Apply();
        frameSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f / 3f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
        return frameSprite;
    }

    private static Sprite ArrowSprite()
    {
        if (arrowSprite != null) return arrowSprite;
        const int s = 16;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var dark = new Color(0.25f, 0.15f, 0.05f, 1f);
        // flèche vers la droite : tige de 4 px, pointe triangulaire
        bool In(int x, int y) => (x >= 2 && x <= 8 && y >= 6 && y <= 9) || (x >= 8 && x <= 14 && Mathf.Abs(y - 7.5f) <= 14 - x + 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Color col = Color.clear;
                if (In(x, y)) col = y >= 8 ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
                else if (In(x - 1, y) || In(x + 1, y) || In(x, y - 1) || In(x, y + 1)) col = dark;
                tex.SetPixel(x, y, col);
            }
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f / 3f);
        return arrowSprite;
    }
}
