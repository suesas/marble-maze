using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple NavMesh path visualization between two points for debugging.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Debug/Path Planner")]
public class PathPlanner : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;
    /// <summary>Draw the computed path in the Scene view for one second.</summary>
    [SerializeField] private bool debugDraw = true;

    void Update()
    {
        if (startPoint == null || endPoint == null) return;

        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(startPoint.position, endPoint.position, NavMesh.AllAreas, path))
        {
            if (debugDraw)
            {
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    Debug.DrawLine(path.corners[i], path.corners[i + 1], Color.red, 1f);
                }
            }

            // Compute length if needed by callers (currently unused in Update)
            // float pathLength = GetPathLength(path);
        }
    }

    /// <summary>
    /// Computes the total length of a NavMeshPath by summing segment lengths.
    /// </summary>
    float GetPathLength(NavMeshPath path)
    {
        float length = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        }
        return length;
    }
}


