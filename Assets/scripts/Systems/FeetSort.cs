using UnityEngine;

/// <summary>
/// TRI PAR LES PIEDS : les personnages sont des images de 64 px dont le pivot est au centre, mais leurs pieds sont
/// bien plus bas (≈ 0,4 unité). Triés par leur centre, ils passaient derrière un buisson devant lequel ils se
/// tenaient, ou devant un arbre dont ils longeaient l'arrière. L'axe de tri du Renderer 2D tient compte de la
/// profondeur (0, 1, 1) : un décalage z égal à la hauteur des pieds fait trier le personnage à ses pieds,
/// sans rien changer à l'image, aux collisions ni aux positions du jeu (l'orthographique ignore z).
/// </summary>
public class FeetSort : MonoBehaviour
{
    private float z;

    public static void Attach(GameObject go)
    {
        if (go == null || go.GetComponent<FeetSort>() != null) return;
        go.AddComponent<FeetSort>();
    }

    private void Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        float feet = 0.45f;   // personnage de 64 px à 32 px/unité, pieds à ~17 px du bas
        if (sr != null && sr.sprite != null)
        {
            var s = sr.sprite;
            float half = s.rect.height / s.pixelsPerUnit * 0.5f;
            float low = -s.bounds.min.y;                     // maillage serré : distance du pivot au pixel le plus bas
            feet = low > 0.05f && low < half - 0.02f ? low : half * 0.45f;
        }
        z = -feet * Mathf.Abs(transform.lossyScale.y);
        Apply();
    }

    // une téléportation remet z à 0 : on le rétablit (le moteur 2D, lui, ne le touche pas)
    private void LateUpdate()
    {
        if (Mathf.Abs(transform.position.z - z) > 0.001f) Apply();
    }

    private void Apply()
    {
        var p = transform.position;
        transform.position = new Vector3(p.x, p.y, z);
    }
}
