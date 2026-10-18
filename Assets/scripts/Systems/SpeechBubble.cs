using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// BULLE DE DIALOGUE au-dessus d'un personnage (intrus, colon, monstre) : fond pixel art avec une pointe vers
/// la tête, texte coloré, qui suit le personnage puis s'efface. Une seule bulle par personnage (la nouvelle
/// remplace l'ancienne) ; les bulles proches se décalent pour ne pas se chevaucher.
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    private static readonly Dictionary<int, SpeechBubble> byOwner = new Dictionary<int, SpeechBubble>();
    private static readonly List<SpeechBubble> all = new List<SpeechBubble>();
    private static Sprite bodySprite, tailSprite;

    private Transform owner;
    private int ownerId;
    private float life, maxLife, height;
    private TextMeshPro tmp;
    private SpriteRenderer body, tail;
    private float lift;

    public static void Show(Transform owner, string text, Color color, float duration = 2.6f, float height = 0.95f)
    {
        if (owner == null || string.IsNullOrEmpty(text)) return;
        int id = owner.GetInstanceID();
        if (byOwner.TryGetValue(id, out var old) && old != null) Destroy(old.gameObject);

        var go = new GameObject("Bulle");
        var b = go.AddComponent<SpeechBubble>();
        b.owner = owner;
        b.ownerId = id;
        b.height = height;
        b.maxLife = b.life = duration + Mathf.Min(2f, text.Length * 0.03f);
        byOwner[id] = b;
        all.Add(b);

        b.tmp = go.AddComponent<TextMeshPro>();
        b.tmp.text = text;
        b.tmp.fontSize = 2.2f;
        b.tmp.alignment = TextAlignmentOptions.Center;
        b.tmp.color = Color.Lerp(color, new Color(0.12f, 0.09f, 0.14f), 0.55f);
        b.tmp.textWrappingMode = TextWrappingModes.Normal;
        b.tmp.rectTransform.sizeDelta = new Vector2(3.4f, 1f);
        b.tmp.sortingOrder = 211;
        b.tmp.ForceMeshUpdate();
        var size = b.tmp.GetRenderedValues(false);
        size.x = Mathf.Clamp(size.x, 0.3f, 3.4f);
        size.y = Mathf.Max(0.25f, size.y);
        b.tmp.rectTransform.sizeDelta = new Vector2(size.x + 0.05f, size.y + 0.05f);

        var bodyGo = new GameObject("Fond");
        bodyGo.transform.SetParent(go.transform, false);
        b.body = bodyGo.AddComponent<SpriteRenderer>();
        b.body.sprite = Body();
        b.body.drawMode = SpriteDrawMode.Sliced;
        b.body.size = new Vector2(size.x + 0.32f, size.y + 0.22f);
        b.body.sortingOrder = 210;
        b.body.color = new Color(0.97f, 0.94f, 0.86f, 0.96f);

        var tailGo = new GameObject("Pointe");
        tailGo.transform.SetParent(go.transform, false);
        tailGo.transform.localPosition = new Vector3(-0.1f, -(size.y + 0.22f) / 2f + 0.02f, 0f);
        b.tail = tailGo.AddComponent<SpriteRenderer>();
        b.tail.sprite = Tail();
        b.tail.sortingOrder = 210;
        b.tail.color = b.body.color;
        // liseré de la couleur du locuteur (colère rouge, peur bleue...)
        var edgeGo = new GameObject("Liseré");
        edgeGo.transform.SetParent(go.transform, false);
        var edge = edgeGo.AddComponent<SpriteRenderer>();
        edge.sprite = Body();
        edge.drawMode = SpriteDrawMode.Sliced;
        edge.size = b.body.size + new Vector2(0.08f, 0.08f);
        edge.sortingOrder = 209;
        edge.color = Color.Lerp(color, Color.black, 0.35f);
        b.lift = (size.y + 0.22f) / 2f;
        b.Place();
    }

    private void OnDestroy()
    {
        all.Remove(this);
        if (byOwner.TryGetValue(ownerId, out var b) && b == this) byOwner.Remove(ownerId);
    }

    private float stack;

    private void Place()
    {
        if (owner == null) return;
        Vector3 basePos = owner.position + Vector3.up * (height + lift + stack);
        transform.position = basePos;
    }

    private void LateUpdate()
    {
        life -= Time.deltaTime;
        if (owner == null || life <= 0f || !owner.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        // pas de chevauchement : face à une bulle plus récente qui la recouvre, l'ancienne passe au-dessus
        Vector2 me = owner.position + Vector3.up * (height + lift);
        Vector2 half = body.size / 2f;
        float need = 0f;
        foreach (var o in all)
        {
            if (o == this || o == null || o.owner == null || o.life <= life) continue;
            Vector2 op = o.transform.position;
            Vector2 oh = o.body.size / 2f;
            if (Mathf.Abs(op.x - me.x) >= half.x + oh.x) continue;
            float top = op.y + oh.y + 0.06f;
            if (me.y + need - half.y < top && me.y + need + half.y > op.y - oh.y) need = top + half.y - me.y;
        }
        stack = Mathf.Lerp(stack, Mathf.Max(0f, need), Mathf.Clamp01(Time.deltaTime * 14f));
        Place();
        float age = maxLife - life;
        float s = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(age / 0.1f));
        transform.localScale = Vector3.one * s;
        float a = Mathf.Clamp01(life / 0.35f);
        tmp.alpha = a;
        body.color = new Color(body.color.r, body.color.g, body.color.b, 0.96f * a);
        tail.color = body.color;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            if (sr != body && sr != tail) { var c = sr.color; c.a = a; sr.color = c; }
    }

    // ── Sprites (pixel art, 16 px par unité comme les personnages)
    private static Sprite Body()
    {
        if (bodySprite != null) return bodySprite;
        const int s = 8;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int dx = Mathf.Min(x, s - 1 - x), dy = Mathf.Min(y, s - 1 - y);
                tex.SetPixel(x, y, dx + dy < 2 ? Color.clear : Color.white);
            }
        tex.Apply();
        bodySprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
        return bodySprite;
    }

    private static Sprite Tail()
    {
        if (tailSprite != null) return tailSprite;
        var tex = new Texture2D(5, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        string[] rows = { "xxxxx", ".xxx.", "..xx.", "...x." };   // de haut en bas
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 5; x++)
                tex.SetPixel(x, 3 - y, rows[y][x] == 'x' ? Color.white : Color.clear);
        tex.Apply();
        tailSprite = Sprite.Create(tex, new Rect(0, 0, 5, 4), new Vector2(0.5f, 1f), 32f);
        return tailSprite;
    }
}
