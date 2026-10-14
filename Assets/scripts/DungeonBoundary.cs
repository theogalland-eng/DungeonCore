using UnityEngine;

/// <summary>
/// Frontière du donjon
/// - Monstres ne peuvent pas sortir
/// - Aventuriers sont safe dehors
/// Utilise un Collider2D (Trigger) pour marquer la zone
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DungeonBoundary : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Layer des monstres")]
    public LayerMask monsterLayer;
    
    [Tooltip("Distance safe hors du donjon (blocs)")]
    public float safeDistanceOutside = 5f;

    [Header("Visual")]
    [Tooltip("Afficher la frontière dans l'éditeur")]
    public bool showBoundary = true;
    
    public Color boundaryColor = Color.yellow;

    private Collider2D boundaryCollider;

    // Singleton pour accès facile
    public static DungeonBoundary Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        boundaryCollider = GetComponent<Collider2D>();
        boundaryCollider.isTrigger = true;
    }

    /// <summary>
    /// Vérifie si une position est dans le donjon
    /// </summary>
    public bool IsInsideDungeon(Vector2 position)
    {
        return boundaryCollider.OverlapPoint(position);
    }

    /// <summary>
    /// Vérifie si une position est en zone safe (hors donjon + distance)
    /// </summary>
    public bool IsInSafeZone(Vector2 position)
    {
        if (IsInsideDungeon(position))
            return false;

        // Calculer distance à la frontière
        Vector2 closestPoint = boundaryCollider.ClosestPoint(position);
        float dist = Vector2.Distance(position, closestPoint);
        
        return dist >= safeDistanceOutside;
    }

    /// <summary>
    /// Obtient le point le plus proche DANS le donjon depuis une position
    /// </summary>
    public Vector2 GetClosestPointInside(Vector2 position)
    {
        return boundaryCollider.ClosestPoint(position);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Bloquer les monstres qui tentent de sortir
        if (((1 << other.gameObject.layer) & monsterLayer) != 0)
        {
            GobelinAI goblin = other.GetComponent<GobelinAI>();
            if (goblin != null)
            {
                // Forcer le monstre à rester dans le donjon
                Vector2 insidePos = GetClosestPointInside(other.transform.position);
                other.transform.position = insidePos;
                
                Debug.LogWarning($"[DungeonBoundary] {goblin.gobelinName} ne peut pas sortir !");
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showBoundary || boundaryCollider == null) return;

        Gizmos.color = boundaryColor;
        
        // Dessiner le contour du collider
        if (boundaryCollider is BoxCollider2D box)
        {
            DrawBoxGizmo(box);
        }
        else if (boundaryCollider is PolygonCollider2D poly)
        {
            DrawPolygonGizmo(poly);
        }
        else if (boundaryCollider is CircleCollider2D circle)
        {
            Gizmos.DrawWireSphere(transform.position + (Vector3)circle.offset, circle.radius);
        }
    }

    private void DrawBoxGizmo(BoxCollider2D box)
    {
        Vector3 center = transform.position + (Vector3)box.offset;
        Vector3 size = box.size;
        
        Vector3[] corners = new Vector3[4];
        corners[0] = center + new Vector3(-size.x, -size.y) * 0.5f;
        corners[1] = center + new Vector3(size.x, -size.y) * 0.5f;
        corners[2] = center + new Vector3(size.x, size.y) * 0.5f;
        corners[3] = center + new Vector3(-size.x, size.y) * 0.5f;

        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        }
    }

    private void DrawPolygonGizmo(PolygonCollider2D poly)
    {
        for (int i = 0; i < poly.points.Length; i++)
        {
            Vector2 p1 = transform.position + (Vector3)poly.points[i];
            Vector2 p2 = transform.position + (Vector3)poly.points[(i + 1) % poly.points.Length];
            Gizmos.DrawLine(p1, p2);
        }
    }
}
