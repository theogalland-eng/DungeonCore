using System.Collections;
using UnityEngine;

/// <summary>
/// Bateau d'une expédition : arrive au ponton, tangue pendant que l'équipage explore l'île,
/// puis repart (vide si personne n'a survécu).
/// </summary>
public class ExpeditionBoat : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector3 moorPos;
    private bool leaving;

    /// <summary>Crée un bateau amarré à côté de la position donnée (bout du ponton).</summary>
    public static ExpeditionBoat Create(Sprite sprite, Vector3 dockEnd) => Create(new[] { sprite }, dockEnd, null);

    public static ExpeditionBoat Create(Sprite[] frames, Vector3 dockEnd, Vector3? mooring)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null) return null;
        var sprite = frames[0];
        var go = new GameObject("Bateau");
        var boat = go.AddComponent<ExpeditionBoat>();
        boat.sr = go.AddComponent<SpriteRenderer>();
        boat.sr.sprite = sprite;
        boat.sr.sortingOrder = 2;   // sous les personnages, au-dessus de l'eau
        if (frames.Length > 1)
        {
            var anim = go.AddComponent<SpriteAnim>();
            anim.frames = frames;
            anim.fps = 6f;
        }
        // amarré à droite du ponton, la coque le long des planches
        float w = sprite.bounds.size.x, h = sprite.bounds.size.y;
        boat.moorPos = mooring ?? dockEnd + new Vector3(w * 0.5f + 1.6f, 0.3f, 0f);
        boat.StartCoroutine(boat.Arrive());
        return boat;
    }

    private IEnumerator Arrive()
    {
        Vector3 from = moorPos + new Vector3(14f, -2f, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 2.5f)
        {
            transform.position = Vector3.Lerp(from, moorPos, 1f - (1f - t) * (1f - t));
            sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 3f));
            yield return null;
        }
        transform.position = moorPos;
        sr.color = Color.white;
    }

    private void Update()
    {
        if (leaving) return;
        // tangage léger
        transform.position = new Vector3(transform.position.x, moorPos.y + Mathf.Sin(Time.time * 1.3f) * 0.04f, 0f);
    }

    public void Leave()
    {
        if (leaving) return;
        leaving = true;
        StopAllCoroutines();
        StartCoroutine(Depart());
    }

    private IEnumerator Depart()
    {
        Vector3 from = transform.position, to = from + new Vector3(16f, -3f, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 4f)
        {
            transform.position = Vector3.Lerp(from, to, t * t);
            sr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((t - 0.6f) / 0.4f));
            yield return null;
        }
        Destroy(gameObject);
    }
}
