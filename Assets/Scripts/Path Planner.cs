using UnityEngine;
using UnityEngine.AI;

public class PathPlanner : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;

    void Update()
    {
        if (startPoint == null || endPoint == null) return;

        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(startPoint.position, endPoint.position, NavMesh.AllAreas, path))
        {
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                Debug.DrawLine(path.corners[i], path.corners[i + 1], Color.red, 1f);
            }

            float pathLength = GetPathLength(path);
            Debug.Log("Pfadlänge: " + pathLength);
        }
    }

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