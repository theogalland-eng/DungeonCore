using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// REPÈRES DU PLAN DE LA CITÉ : un écriteau et son nom là où se dressera un lieu dont l'art n'est pas encore
/// importé (chapelle, bibliothèque, prison, manoir, moulin...). Sert à vérifier en jeu que le plan « colle ».
/// Le terrain du héros n'apparaît qu'en mode Aventurier.
/// </summary>
public class PlanMarkers : MonoBehaviour
{
    private readonly List<(GameObject go, bool adventureOnly)> signs = new List<(GameObject, bool)>();
    private float nextCheck;

    private void Start()
    {
        var site = GetComponent<SettlementSite>();
        if (site == null || site.planMarks == null) return;
        var art = SettlementArt.Get();
        var sprite = art != null ? art.Sprite("banner_red") : null;
        var root = new GameObject("Repères du plan").transform;
        foreach (var m in site.planMarks)
        {
            var go = new GameObject("Repère — " + m.name);
            go.transform.SetParent(root, false);
            go.transform.position = site.CellCenter(m.cell);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : SpriteUtil.Square;
            if (sprite == null) { sr.color = new Color(0.85f, 0.68f, 0.35f); go.transform.localScale = new Vector3(0.3f, 0.8f, 1f); }
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            NemesisSystem.AddTag(go.transform, Loc.T(m.name), new Color(1f, 0.86f, 0.55f));
            signs.Add((go, m.adventureOnly));
        }
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        Refresh();
    }

    private void Refresh()
    {
        // le terrain du héros : en mode Aventurier, tant que la maison n'est pas commandée
        bool show = CrawlerMode.Active && (SettlementManager.Instance == null || !SettlementManager.Instance.HasHeroHome);
        foreach (var (go, adv) in signs)
            if (go != null && adv && go.activeSelf != show) go.SetActive(show);
    }
}
