using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Requêtes physiques sans allocation : Physics2D.OverlapCircleAll crée un tableau à chaque appel (des dizaines
/// par image pour l'IA, les pièges, les pouvoirs), ce qui déclenche le ramasse-miettes et fait saccader le jeu sur
/// téléphone. Ici les résultats vont dans des listes réutilisées (un petit anneau pour les boucles imbriquées).
/// La liste rendue n'est valable que jusqu'aux prochains appels : la parcourir tout de suite, ne pas la garder.
/// </summary>
public static class Phys
{
    private static readonly List<Collider2D>[] ring = new List<Collider2D>[32];   // assez pour les boucles imbriquées (coup → éclaboussure → flèche perçante...)
    private static int next;

    public static List<Collider2D> Circle(Vector2 center, float radius, int layerMask = Physics2D.AllLayers)
    {
        var list = ring[next] ?? (ring[next] = new List<Collider2D>(32));
        next = (next + 1) % ring.Length;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = layerMask, useTriggers = Physics2D.queriesHitTriggers };
        Physics2D.OverlapCircle(center, radius, filter, list);
        return list;
    }
}
