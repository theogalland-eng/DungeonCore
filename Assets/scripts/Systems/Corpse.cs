using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cadavre d'intrus tombé dans le donjon : vos monstres affamés viennent le dévorer (quelques portions).
/// Un monstre nourri ainsi coûte moitié moins d'entretien en mana ce jour-là. Le corps disparaît au bout d'un jour et demi.
/// </summary>
public class Corpse : MonoBehaviour
{
    public static readonly List<Corpse> All = new List<Corpse>();

    public int Portions { get; private set; } = 3;
    private float rotAt;
    private SpriteRenderer sr;
    private static Sprite sprite;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public static Corpse Spawn(Vector2 at, int portions = 3)
    {
        if (sprite == null)
        {
            var tex = Resources.Load<Texture2D>("Props/corpse");
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.3f), 16f);
            }
        }
        var go = new GameObject("Cadavre");
        go.transform.position = at;
        var c = go.AddComponent<Corpse>();
        c.Portions = portions;
        c.sr = go.AddComponent<SpriteRenderer>();
        c.sr.sprite = sprite;
        c.sr.sortingOrder = 2;
        c.sr.flipX = Random.value < 0.5f;
        float day = GameManager.Instance != null ? GameManager.Instance.dayLengthSeconds : 180f;
        c.rotAt = Time.time + day * 1.5f;
        return c;
    }

    /// <summary>Une bouchée (false s'il ne reste rien).</summary>
    public bool Eat()
    {
        if (Portions <= 0) return false;
        Portions--;
        if (Portions <= 0) rotAt = Mathf.Min(rotAt, Time.time + 2f);
        return true;
    }

    private void Update()
    {
        if (Time.time < rotAt) return;
        var col = sr.color;
        col.a -= Time.deltaTime;
        sr.color = col;
        if (col.a <= 0f) Destroy(gameObject);
    }
}
