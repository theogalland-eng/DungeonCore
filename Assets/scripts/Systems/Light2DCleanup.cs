using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Une Light2D crée son maillage à la demande et ne le détruit jamais : chaque lumière éphémère (projectile, sort,
/// butin, évolution...) laissait un maillage orphelin en mémoire, et le jeu s'alourdissait au fil de la partie.
/// Posé à côté des lumières créées en jeu, ce composant libère ces maillages quand l'objet disparaît.
/// </summary>
public class Light2DCleanup : MonoBehaviour
{
    private static readonly FieldInfo MeshField = typeof(Light2D).GetField("m_Mesh", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void Attach(Light2D l)
    {
        if (l != null && l.GetComponent<Light2DCleanup>() == null) l.gameObject.AddComponent<Light2DCleanup>();
    }

    private void OnDestroy()
    {
        if (MeshField == null) return;
        foreach (var l in GetComponents<Light2D>())
            if (MeshField.GetValue(l) is Mesh m && m != null) Destroy(m);
    }
}
