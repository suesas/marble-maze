using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
public class BoardAgent : Agent
{
    public Rigidbody ballRb;
    public Transform ball;
    public Transform goal;

    public float tiltSpeed = 15f;
    public float maxTilt = 30f;

    private float currentTiltX = 0f;
    private float currentTiltZ = 0f;
    private Quaternion initialRotation;
    private Vector3 initialBallLocalPos;
    private NavMeshPath navPath;

    private float previousProgress = 0f;
    private float currentProgress = 0f;
    private List<Vector3> idealPath;

    private int currentPathIndex = 0;
    void Start()
    {
        //Time.timeScale = 0.1f; // Alles läuft 10x langsamer
        BitmapLevelBuilder levelBuilder = FindObjectOfType<BitmapLevelBuilder>();
        if (levelBuilder != null)
        {
            ball = levelBuilder.GetMarbleInstance().transform;
            ballRb = ball.GetComponent<Rigidbody>();
            initialBallLocalPos = transform.InverseTransformPoint(ball.position);
        }
        initialBallLocalPos = transform.InverseTransformPoint(ball.position);
    }
    public override void Initialize()
    {
        navPath = new NavMeshPath();
    }

    public override void OnEpisodeBegin()
    {

        currentPathIndex = 0;
        // Reset the agent's position and rotation
        previousProgress = 0f;
        transform.rotation = Quaternion.identity;
        currentTiltX = 0f;
        currentTiltZ = 0f;

        Vector3 resetPosition = transform.TransformPoint(initialBallLocalPos);
        ball.position = resetPosition;
        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
    }

    void Update()
    {
        // Optional: Reset the agent if it falls off the board
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log($"[EPISODE END] Cumulative Reward: {GetCumulativeReward()}");
            //EndEpisode();
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            EndEpisode();
        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformPoint(ball.position));
        sensor.AddObservation(transform.InverseTransformPoint(goal.position));
        sensor.AddObservation(ballRb.velocity);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Handle continuous actions for tilting the board
        float inputX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float inputZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        // Update the tilt angles based on the input
        currentTiltX = Mathf.Clamp(currentTiltX + inputX * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);
        currentTiltZ = Mathf.Clamp(currentTiltZ + inputZ * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);

        // Apply the tilt to the board
        transform.rotation = Quaternion.Euler(currentTiltX, 0, currentTiltZ);

        if (NavMesh.CalculatePath(transform.TransformPoint(initialBallLocalPos), goal.position, NavMesh.AllAreas, navPath))
        {
            idealPath = InterpolatePath(navPath, 0.5f);

            // Suche nächsten gültigen Punkt
            int nearestIndex;
            Vector3 nearest = FindVisibleNearestPathPoint(ball.position, idealPath.ToArray(), out nearestIndex);
            currentProgress = GetDistanceAlongPath(nearest, idealPath.ToArray());
            Debug.DrawLine(ball.position, nearest, Color.black, 1f);

            float rewardDelta = currentProgress - previousProgress;
            previousProgress = currentProgress;
            AddReward(rewardDelta * 0.05f);
            
        }

        Color[] colors = new Color[] { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan };
        for (int i = 0; i < idealPath.Count - 1; i++)
        {
            Color lineColor = colors[i % colors.Length]; // Wechselt durch die Farben
            Debug.DrawLine(idealPath[i], idealPath[i + 1], lineColor, 1f);
        }

        if (StepCount >= MaxStep - 1)
        {
            Debug.Log($"[EPISODE END] Cumulative Reward: {GetCumulativeReward()}");
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        a[0] = Input.GetAxis("Vertical");
        a[1] = -Input.GetAxis("Horizontal");
    }


    private Vector3 FindVisibleNearestPathPoint(Vector3 currentPosition, Vector3[] pathCorners, out int nearestIndex, int maxAllowedDelta = 2)
    {
        float minDist = float.MaxValue;
        Vector3 nearest = Vector3.zero;
        bool found = false;
        int bestIndex = -1;

        for (int i = bestIndex + 1; i < pathCorners.Length; i++)
        {
            Vector3 target = pathCorners[i];
            NavMeshHit hit;

            // Sichtprüfung: nur wenn nichts zwischen currentPosition und target liegt
            if (!NavMesh.Raycast(currentPosition, target, out hit, NavMesh.AllAreas))
            {
                float dist = Vector3.Distance(currentPosition, target);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = target;
                    bestIndex = i;
                    found = true;
                }
            }
        }

        if (found)
        {
            nearestIndex = bestIndex;
            return nearest;
        }
        else
        {
            // Kein sichtbarer Punkt → fallback auf vorherigen gültigen Index
            nearestIndex = currentPathIndex;
            return pathCorners[currentPathIndex];
        }
    }

    private float GetDistanceAlongPath(Vector3 point, Vector3[] pathCorners)
    {
        float totalDist = 0f;
        for (int i = 0; i < pathCorners.Length - 1; i++)
        {
            if (point == pathCorners[i])
            {
                return totalDist;
            }
            float segmentDist = Vector3.Distance(pathCorners[i], pathCorners[i + 1]);
            totalDist += segmentDist;

            // Prüfen, ob das nächste Segment den nächsten Punkt enthält
            if (Vector3.Distance(point, pathCorners[i + 1]) < 0.01f)
                break;
        }
        return totalDist;
    }

    List<Vector3> InterpolatePath(NavMeshPath path, float spacing)
    {
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Vector3 start = path.corners[i];
            Vector3 end = path.corners[i + 1];
            float length = Vector3.Distance(start, end);
            int divisions = Mathf.CeilToInt(length / spacing);

            for (int j = 0; j <= divisions; j++)
            {
                float t = j / (float)divisions;
                points.Add(Vector3.Lerp(start, end, t));
            }
        }

        return points;
    }
}