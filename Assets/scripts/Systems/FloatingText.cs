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

    public static void Spawn(Vector3 position, string text, Color color, float size = 1f)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = position + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.15f);
        var ft = go.AddComponent<FloatingText>();
        ft.tmp = go.AddComponent<TextMeshPro>();
        ft.tmp.text = text;
        ft.tmp.fontSize = 3.2f * size;
        ft.tmp.alignment = TextAlignmentOptions.Center;
        ft.tmp.color = color;
        ft.tmp.fontStyle = FontStyles.Bold;
        ft.tmp.outlineWidth = 0.25f;
        ft.tmp.outlineColor = new Color32(0, 0, 0, 200);
        ft.tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
        ft.tmp.sortingOrder = 200;
        ft.color = color;
        ft.maxLife = ft.life = 0.9f;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        transform.position += Vector3.up * (Time.deltaTime * 0.9f);
        Color c = color;
        c.a = Mathf.Clamp01(life / maxLife * 1.5f);
        tmp.color = c;
        if (life <= 0f) Destroy(gameObject);
    }
}
