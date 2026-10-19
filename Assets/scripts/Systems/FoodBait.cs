using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Garde-manger (appât du Noyau) : jambons, pains et fruits posés dans le donjon. Un intrus affamé qui le voit
/// accourt et se goinfre, immobile et sans méfiance — le moment rêvé pour une embuscade.
/// Il contient quelques portions ; le Noyau le regarnit chaque matin.
/// </summary>
public class FoodBait : MonoBehaviour
{
    public static readonly List<FoodBait> All = new List<FoodBait>();

    public int maxPortions = 4;
    [Tooltip("Secondes pour manger une portion")] public float eatDuration = 4f;
    public int Portions { get; private set; }

    private readonly Dictionary<AdventurerLife, float> eating = new Dictionary<AdventurerLife, float>();
    private readonly HashSet<AdventurerLife> reserved = new HashSet<AdventurerLife>();
    private SpriteRenderer sr;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        // le garde-manger nourrit les monstres du donjon ET appâte les intrus : 10 portions par jour
        if (maxPortions < 10) maxPortions = 10;
        Portions = maxPortions;
        sr = GetComponentInChildren<SpriteRenderer>();
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay += Refill;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= Refill;
    }

    private void Refill(int day)
    {
        Portions = maxPortions;
        if (sr != null) sr.color = Color.white;
    }

    /// <summary>Garde-manger garni le plus proche, dans un rayon donné.</summary>
    public static FoodBait Nearest(Vector2 p, float radius, AdventurerLife who)
    {
        FoodBait best = null;
        float bd = radius;
        foreach (var b in All)
        {
            if (b == null || b.Portions <= 0) continue;
            if (b.reserved.Count >= b.Portions && !b.reserved.Contains(who)) continue;
            float d = Vector2.Distance(b.transform.position, p);
            if (d < bd) { bd = d; best = b; }
        }
        return best;
    }

    public void Reserve(AdventurerLife who) => reserved.Add(who);
    public void Release(AdventurerLife who) { reserved.Remove(who); eating.Remove(who); }

    public Vector2 EatSpot(Vector2 from)
    {
        Vector2 p = transform.position;
        Vector2 d = from - p;
        return p + (d.sqrMagnitude > 0.01f ? d.normalized : Vector2.down) * 0.8f;
    }

    /// <summary>Un monstre du donjon se sert : une portion de moins (false si le garde-manger est vide).</summary>
    public bool TakePortion()
    {
        if (Portions <= 0) return false;
        Portions--;
        if (sr != null && Portions <= 0) sr.color = new Color(0.55f, 0.5f, 0.5f);
        return true;
    }

    /// <summary>Un pas de repas ; true quand la portion est avalée.</summary>
    public bool EatTick(AdventurerLife who, float dt)
    {
        if (Portions <= 0) return true;
        eating.TryGetValue(who, out float t);
        t += dt;
        eating[who] = t;
        if (t < eatDuration) return false;
        eating.Remove(who);
        reserved.Remove(who);
        Portions--;
        if (sr != null && Portions <= 0) sr.color = new Color(0.55f, 0.5f, 0.5f);   // vide : en attente du matin
        return true;
    }
}
