using TMPro;
using UnityEngine;

/// <summary>
/// Texte flottant dans le monde (dégâts, mana gagné...).
/// </summary>
public class FloatingText : MonoBehaviour
{
    private TextMeshPro tmp;
    private float life, maxLife;
    private Color color;
    private Vector3 vel;

    // recyclés : un combat en affiche des centaines par minute (créer puis détruire un texte coûte cher)
    private static readonly System.Collections.Generic.Stack<FloatingText> pool = new System.Collections.Generic.Stack<FloatingText>();

    public static void Spawn(Vector3 position, string text, Color color, float size = 1f)
    {
        FloatingText ft = null;
        while (pool.Count > 0 && ft == null) ft = pool.Pop();
        if (ft == null)
        {
            var go = new GameObject("FloatingText");
            ft = go.AddComponent<FloatingText>();
            ft.tmp = go.AddComponent<TextMeshPro>();
            ft.tmp.alignment = TextAlignmentOptions.Center;
            ft.tmp.fontStyle = FontStyles.Bold;
            UIKit.Outline(ft.tmp, new Color32(0, 0, 0, 200), 0.25f);
            ft.tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
            ft.tmp.sortingOrder = 200;
        }
        var g = ft.gameObject;
        g.SetActive(true);
        g.transform.position = position + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.15f);
        ft.tmp.text = text;
        ft.tmp.fontSize = 3.2f * size;
        ft.tmp.color = color;
        ft.color = color;
        ft.maxLife = ft.life = 0.9f;
        // jaillit vers le haut en arc, d'un côté ou de l'autre
        ft.vel = new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 2.2f, 0f);
        g.transform.localScale = Vector3.one * 1.5f;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        // pop : gros à l'apparition, puis taille normale ; montée qui ralentit (gravité)
        float age = maxLife - life;
        transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(age / 0.12f));
        vel.y -= 4.2f * Time.deltaTime;
        vel.x *= 1f - 3f * Time.deltaTime;
        transform.position += vel * Time.deltaTime * (vel.y > 0f ? 1f : 0.35f);
        Color c = color;
        c.a = Mathf.Clamp01(life / maxLife * 1.5f);
        tmp.color = c;
        if (life <= 0f) { gameObject.SetActive(false); pool.Push(this); }
    }
}
