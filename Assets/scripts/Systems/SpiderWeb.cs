using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toile tissée par une araignée (Tisseuse, Reine-araignée) : englue les intrus qui la traversent
/// (vitesse -60 %), puis se défait au bout d'un moment. Rien à payer, rien à sauvegarder.
/// </summary>
public class SpiderWeb : MonoBehaviour
{
    public static readonly List<SpiderWeb> All = new List<SpiderWeb>();

    public float life = 50f;
    private float born, nextCheck;
    private SpriteRenderer sr;

    public static SpiderWeb Spin(Vector2 pos, Sprite sprite, Transform parent)
    {
        var go = new GameObject("Toile tissée");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        var w = go.AddComponent<SpiderWeb>();
        w.sr = go.AddComponent<SpriteRenderer>();
        w.sr.sprite = sprite;
        w.sr.sortingOrder = 1;
        w.sr.color = new Color(1f, 1f, 1f, 0f);
        go.transform.localScale = Vector3.one * 1.05f;
        go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 4) * 90f);
        return w;
    }

    public static bool Near(Vector2 p, float r)
    {
        foreach (var w in All) if (w != null && Vector2.Distance(w.transform.position, p) < r) return true;
        return false;
    }

    private void OnEnable() { All.Add(this); born = Time.time; }
    private void OnDisable() => All.Remove(this);

    private void Update()
    {
        float age = Time.time - born;
        // apparaît en se tissant, s'efface en fin de vie
        float a = Mathf.Min(1f, age / 0.8f) * Mathf.Clamp01((life - age) / 4f);
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.85f * a);
        if (age > life) { Destroy(gameObject); return; }
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;
        foreach (var h in Phys.Circle(transform.position, 0.45f, 1 << 7))
        {
            var ai = h.GetComponent<AdventurerAI>();
            if (ai != null) ai.Slow(0.4f, 0.6f);
        }
    }
}
