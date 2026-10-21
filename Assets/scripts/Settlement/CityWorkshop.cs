using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Bâtiment modèle de l'atelier : ce qui sera bâti sur l'île, tel qu'on l'a retouché.</summary>
public class CityTemplate : MonoBehaviour
{
    public string id;
    /// <summary>Position du modèle sur l'île (coin bas-gauche) à la création de l'atelier.</summary>
    public Vector3 islandOrigin;
    /// <summary>Emprise du bâtiment sur l'île à la création de l'atelier (cases).</summary>
    public RectInt rect;
    /// <summary>Seuil de la porte, relatif au modèle.</summary>
    public Vector3 doorLocal;
}

/// <summary>
/// ATELIER DE LA CITÉ : à côté de l'île, la cité finale (tous les bâtiments, rues, enceintes, décor) et chaque
/// intérieur, posés dans la scène pour être retouchés à la main (déplacer, supprimer, ajouter des objets, peindre
/// les rues). En jeu, l'atelier est caché et sert de modèle : chaque bâtiment, l'enceinte, le décor et les
/// intérieurs sont copiés depuis lui, à leur place sur l'île.
/// </summary>
public class CityWorkshop : MonoBehaviour
{
    [Tooltip("Décalage de la cité de l'atelier par rapport à l'île (monde)")]
    public Vector3 cityOffset = new Vector3(300f, 0f, 0f);
    public Transform buildings, wallsStone, wallsPalisade, decor, interiors, reference;
    public Tilemap streets;

    private static CityWorkshop instance;
    private static bool searched;
    private readonly Dictionary<string, GameObject> byId = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> rooms = new Dictionary<string, GameObject>();

    public static CityWorkshop Instance
    {
        get
        {
            if (instance == null && (!searched || !Application.isPlaying))
            {
                searched = true;
                instance = FindFirstObjectByType<CityWorkshop>(FindObjectsInactive.Include);
                if (instance != null) instance.Index();
            }
            return instance;
        }
    }

    private void Awake()
    {
        instance = this;
        Index();
        if (!Application.isPlaying) return;
        // en jeu, l'atelier ne se voit pas et ne gêne rien : ses modèles sont seulement copiés
        foreach (var t in new[] { buildings, wallsStone, wallsPalisade, decor, interiors, reference })
            if (t != null) t.gameObject.SetActive(false);
        if (streets != null) streets.gameObject.SetActive(false);
    }

    private void OnDestroy() { if (instance == this) { instance = null; searched = false; } }

    private void Index()
    {
        byId.Clear();
        rooms.Clear();
        if (buildings != null)
            foreach (Transform t in buildings)
            {
                var ct = t.GetComponent<CityTemplate>();
                if (ct != null && !string.IsNullOrEmpty(ct.id)) byId[ct.id] = t.gameObject;
            }
        if (interiors != null)
            foreach (Transform t in interiors)
                if (t.name.StartsWith("interior:")) rooms[t.name.Substring(9).Split(' ')[0]] = t.gameObject;
    }

    public GameObject Template(string id) => id != null && byId.TryGetValue(id, out var go) && go != null ? go : null;
    public GameObject Room(string id) => id != null && rooms.TryGetValue(id, out var go) && go != null ? go : null;

    /// <summary>Copie d'un modèle de l'atelier, ramenée à sa place sur l'île.</summary>
    public GameObject Spawn(GameObject template, Transform parent)
    {
        var go = Instantiate(template, parent);
        go.transform.position = template.transform.position - cityOffset;
        go.SetActive(true);
        return go;
    }

    /// <summary>Cases des rues peintes dans l'atelier (coordonnées de l'île).</summary>
    public List<Vector3Int> StreetCells()
    {
        var list = new List<Vector3Int>();
        if (streets == null) return list;
        var off = Vector3Int.RoundToInt(cityOffset);
        foreach (var c in streets.cellBounds.allPositionsWithin)
            if (streets.HasTile(c))
            {
                // la grille de l'atelier est posée sur le décalage : ses cases sont déjà celles de l'île
                var w = streets.layoutGrid.transform.position;
                list.Add(c + Vector3Int.RoundToInt(w) - off);
            }
        return list;
    }
}
