using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class FinalBoardAgent : Agent
{
    // === Board & Marble References ===
    public Transform marble;
    public Transform goal;
    [SerializeField] private MeshRenderer floorRenderer;

    // === Board & Marble Properties ===
    private Rigidbody marbleRb;
    private float marbleRadius;
    private float boardMaxX;
    private float boardMaxZ;
    private Vector3 initialMarbleLocalPos;

    // === Board Tilt Control ===
    public float tiltSpeed = 90f;
    public float maxTilt = 10f;
    private float currentTiltX = 0f;
    private float currentTiltZ = 0f;

    // === Pathfinding & Progress Tracking ===
    private NavMeshPath navPath;
    private List<Vector3> idealPath;
    private int currentPathIndex = 0;
    private int lastCheckpointIndex = 0;
    private int achievedCheckpoints = 0;
    private float previousProgress = 0f;
    private float currentProgress = 0f;
    private float pathLength;
    private float timeSinceLastProgress = 0f;

    // === Milestone Reward ===
    [SerializeField] int milestoneBins = 10;
    [SerializeField] float milestoneBonus = 0.5f;
    private int lastMilestonePaid;

    // === Holes Observation ===
    private List<Transform> holes;
    public int kNearestHoles = 5;
    private List<Vector3> debugObservedHoles = new List<Vector3>();

    // === Raycast Observation ===
    public int rayCount = 8;
    public float maxDistance = 5f;
    [SerializeField] float rayHeight = 0.2f;
    [SerializeField] LayerMask obstacleMask;
    [SerializeField] float clearanceFactor = 0.8f;

    // === Episode Statistics ===
    private int totalEpisodes = 0;
    private int successfulEpisodes = 0;
    string boardName;

    // ===========================
    // === Unity Lifecycle Methods
    // ===========================

    void Start()
    {
        boardName = gameObject.name;
        SetMarbleSizeFromMesh();
        marbleRb = marble.GetComponent<Rigidbody>();
        SetMaxBoardCoordinates();
        initialMarbleLocalPos = transform.InverseTransformPoint(marble.position);
    }

    public override void Initialize()
    {
        navPath = new NavMeshPath();
        InitializeHoles();
    }

    public override void OnEpisodeBegin()
    {
        totalEpisodes++;
        if (totalEpisodes == 50)
        {
            Debug.Log($"[EPISODE] Board: {boardName}, Total Episodes: {totalEpisodes}, Successful: {successfulEpisodes}, Success Rate: {(successfulEpisodes / (float)totalEpisodes * 100f):F2}%");
        }

        // Reset internal state
        timeSinceLastProgress = 0f;
        achievedCheckpoints = 0;
        lastCheckpointIndex = 0;
        currentPathIndex = 0;

        // Reset the board's rotation and internal state
        previousProgress = 0f;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        currentTiltX = 0f;
        currentTiltZ = 0f;

        // Add random offset to marble start position
        Vector3 randomOffset = new Vector3(
            Random.Range(-7.0f, 7.0f),
            0f,
            Random.Range(-7.0f, 7.0f)
        );

        // Reset the marble's position and velocity
        Vector3 resetPosition = transform.TransformPoint(initialMarbleLocalPos + randomOffset);
        marble.position = resetPosition;
        marbleRb.velocity = Vector3.zero;
        marbleRb.angularVelocity = Vector3.zero;

        //Reset Milestone tracking
        lastMilestonePaid = 0;
        pathLength = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        debugObservedHoles.Clear();

        AddMarblePositionObservation(sensor);
        AddGoalPositionObservation(sensor);
        AddBoardTiltObservation(sensor);
        AddHolePositionsObservation(sensor);
        AddGuidanceObservation(sensor);
        AddPathProgressObservation(sensor);
        AddRaycastObservations(sensor);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        HandleBoardTiltActions(actions);
        UpdatePathAndProgress();
        DrawIdealPathDebug();
        HandleEpisodeTimeout();
        AddReward(-0.005f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        a[0] = Input.GetAxis("Vertical");
        a[1] = -Input.GetAxis("Horizontal");
    }

    private void OnDrawGizmos()
    {
        if (debugObservedHoles == null) return;
        Gizmos.color = Color.magenta;
        foreach (var holePos in debugObservedHoles)
        {
            var p = holePos + Vector3.up * 0.01f;
            Gizmos.DrawWireSphere(p, 0.75f); // Radius passend zur Boardgröße
        }
    }

    // ===========================
    // === Observation Methods
    // ===========================

    private void AddMarblePositionObservation(VectorSensor sensor)
    {
        Vector3 localmarblePos = transform.InverseTransformPoint(marble.position);
        float marbleNx = ToNormalized(localmarblePos.x, -boardMaxX, boardMaxX);
        float marbleNz = ToNormalized(localmarblePos.z, -boardMaxZ, boardMaxZ);

        sensor.AddObservation(marbleNx);
        sensor.AddObservation(marbleNz);
    }

    private void AddGoalPositionObservation(VectorSensor sensor)
    {
        Vector3 localGoalPos = transform.InverseTransformPoint(goal.position);
        float goalNx = ToNormalized(localGoalPos.x, -boardMaxX, boardMaxX);
        float goalNz = ToNormalized(localGoalPos.z, -boardMaxZ, boardMaxZ);

        sensor.AddObservation(goalNx);
        sensor.AddObservation(goalNz);
    }

    private void AddBoardTiltObservation(VectorSensor sensor)
    {
        float tiltNx = ToNormalized(currentTiltX, -maxTilt, maxTilt);
        float tiltNz = ToNormalized(currentTiltZ, -maxTilt, maxTilt);

        sensor.AddObservation(tiltNx);
        sensor.AddObservation(tiltNz);
    }

    private void AddHolePositionsObservation(VectorSensor sensor)
    {
        holes.Sort((a, b) =>
        {
            float da = (a.position - marble.position).sqrMagnitude;
            float db = (b.position - marble.position).sqrMagnitude;
            return da.CompareTo(db);
        });

        int count = Mathf.Min(kNearestHoles, holes.Count);
        for (int i = 0; i < count; i++)
        {
            Vector3 local = transform.InverseTransformPoint(holes[i].position);
            debugObservedHoles.Add(holes[i].position);

            float hx = ToNormalized(local.x, -boardMaxX, boardMaxX);
            float hz = ToNormalized(local.z, -boardMaxZ, boardMaxZ);

            sensor.AddObservation(hx);
            sensor.AddObservation(hz);
        }

        for (int i = count; i < kNearestHoles; i++)
        {
            sensor.AddObservation(0f); // hx
            sensor.AddObservation(0f); // hz
        }
    }

    private void AddGuidanceObservation(VectorSensor sensor)
    {
        if (idealPath != null && idealPath.Count > 1)
        {
            int nextIdx = Mathf.Min(currentPathIndex + 1, idealPath.Count - 1);
            Vector3 nextTarget = idealPath[nextIdx];

            Vector3 flat = nextTarget - marble.position;
            flat.y = 0f;

            Vector3 localGuidance = transform.InverseTransformDirection(flat);
            sensor.AddObservation(localGuidance.normalized);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
        }
    }

    private void AddPathProgressObservation(VectorSensor sensor)
    {
        if (pathLength != 0f)
        {
            float distAlongPath = currentProgress / pathLength;
            sensor.AddObservation(distAlongPath); // ∈ [0,1]
        }
        else
        {
            sensor.AddObservation(0f);
        }
    }

    private void AddRaycastObservations(VectorSensor sensor)
    {
        for (int i = 0; i < rayCount; i++)
        {
            float angle = (360f / rayCount) * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Vector3 origin = marble.position + Vector3.up * rayHeight;

            float normalizedDist = 1f;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                normalizedDist = hit.distance / maxDistance;
            }

            sensor.AddObservation(normalizedDist);
            Debug.DrawRay(origin, dir * maxDistance, Color.red, 0f);
        }
    }

    // ===========================
    // === Action & Progress Methods
    // ===========================

    private void HandleBoardTiltActions(ActionBuffers actions)
    {
        float inputX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float inputZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        // Update the tilt angles based on the input
        currentTiltX = Mathf.Clamp(currentTiltX + inputX * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);
        currentTiltZ = Mathf.Clamp(currentTiltZ + inputZ * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);

        // Apply the tilt to the board
        transform.rotation = Quaternion.Euler(currentTiltX, 0, currentTiltZ);
    }

    private void UpdatePathAndProgress()
    {
        if (NavMesh.CalculatePath(transform.TransformPoint(initialMarbleLocalPos), goal.position, NavMesh.AllAreas, navPath))
        {
            idealPath = InterpolatePath(navPath, 0.25f);
            pathLength = GetDistanceAlongPath(goal.position, navPath.corners);

            int nearestIndex;
            Vector3 nearest = FindVisibleNearestPathPoint(marble.position, idealPath, currentPathIndex, out nearestIndex, 3);
            currentPathIndex = Mathf.Max(currentPathIndex, nearestIndex);
            currentProgress = GetDistanceAlongPath(nearest, idealPath.ToArray());

            if (nearestIndex > lastCheckpointIndex)
            {
                achievedCheckpoints += (nearestIndex - lastCheckpointIndex);
                lastCheckpointIndex = nearestIndex;
            }
            HandleProgress(currentProgress - previousProgress);

            RewardMilestones();
        }
    }

    private void HandleProgress(float progressDelta)
    {
        if (progressDelta > 0.01f)
        {
            AddReward(progressDelta * 0.5f);
            previousProgress = currentProgress;
            timeSinceLastProgress = 0f;
        }
        else
        {
            timeSinceLastProgress += Time.deltaTime;
            if (timeSinceLastProgress > 6f) // 6 seconds without progress
            {
                Debug.Log($"[EPISODE END: timeout] Cumulative Reward: {GetCumulativeReward()}");
                LogEpisodeStatsAndEnd();
            }
        }
    }

    private void DrawIdealPathDebug()
    {
        if (idealPath == null) return;
        Color[] colors = new Color[] { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan };
        for (int i = 0; i < idealPath.Count - 1; i++)
        {
            Color lineColor = colors[i % colors.Length];
            Debug.DrawLine(idealPath[i], idealPath[i + 1], lineColor, 1f);
        }
    }

    private void HandleEpisodeTimeout()
    {
        if (StepCount >= MaxStep - 1)
        {
            SetReward(0);
            Debug.Log($"[EPISODE END: timeout] Cumulative Reward: {GetCumulativeReward()}");
            LogEpisodeStatsAndEnd();
        }
    }

    // ===========================
    // === Pathfinding & Utility Methods
    // ===========================

    private void InitializeHoles()
    {
        var holeObjects = GameObject.FindGameObjectsWithTag("Hole");
        holes = new List<Transform>(holeObjects.Length);
        foreach (var h in holeObjects) holes.Add(h.transform);
    }

    private Vector3 FindVisibleNearestPathPoint(
        Vector3 currentPosition,
        List<Vector3> path,
        int currentIndex,
        out int nearestIndex,
        int maxForwardJump = 2
    )
    {
        nearestIndex = currentIndex;
        if (path == null || path.Count == 0)
            return currentPosition;

        int start = Mathf.Clamp(currentIndex, 0, path.Count - 1);
        int end = Mathf.Clamp(currentIndex + Mathf.Max(0, maxForwardJump), 0, path.Count - 1);

        float bestDistance = float.MaxValue;
        Vector3 bestPoint = path[currentIndex];

        for (int i = start; i <= end; i++)
        {
            Vector3 target = path[i];

            if (!IsPathClearFormarble(currentPosition, target))
                continue;

            if (NavMesh.Raycast(currentPosition, target, out _, NavMesh.AllAreas))
                continue;

            float distance = (target - currentPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = target;
                nearestIndex = i;
            }
        }

        return bestPoint;
    }

    bool IsPathClearFormarble(Vector3 from, Vector3 to)
    {
        from.y += rayHeight;
        to.y += rayHeight;

        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist < 1e-4f) return true;

        dir /= dist;

        return !Physics.SphereCast(from, marbleRadius * clearanceFactor, dir, out _, dist, obstacleMask, QueryTriggerInteraction.Ignore);
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

    List<float> BuildCumulativeDistances(List<Vector3> path)
    {
        var dist = new List<float>();
        float total = 0f;
        dist.Add(0f);

        for (int i = 0; i < path.Count - 1; i++)
        {
            total += Vector3.Distance(path[i], path[i + 1]);
            dist.Add(total);
        }

        return dist;
    }

    // ===========================
    // === Reward & Stats Methods
    // ===========================

    void RewardMilestones()
    {
        if (pathLength <= 0f) return;

        float progressDist = currentProgress;

        int reached = Mathf.FloorToInt((progressDist / pathLength) * milestoneBins);

        while (lastMilestonePaid < reached && lastMilestonePaid < milestoneBins)
        {
            lastMilestonePaid++;
            AddReward(milestoneBonus);

            Debug.Log($"[MILESTONE] Reached {lastMilestonePaid}/{milestoneBins}, total reward now {GetCumulativeReward()}");
        }
    }

    public void LogEpisodeStatsAndEnd()
    {
        Academy.Instance.StatsRecorder.Add("achieved_checkpoints", achievedCheckpoints);

        if (idealPath != null && idealPath.Count > 1)
        {
            float pathCompletion = lastCheckpointIndex / (float)(idealPath.Count - 1);
            Academy.Instance.StatsRecorder.Add("path_completion_ratio", pathCompletion);
        }

        bool success = false;
        if (idealPath != null && lastCheckpointIndex >= idealPath.Count - 2)
        {
            success = true;
        }

        Academy.Instance.StatsRecorder.Add("episode_success", success ? 1f : 0f);

        EndEpisode();
    }

    public void RegisterSuccess()
    {
        successfulEpisodes++;
        Debug.Log($"[SUCCESS] Board: {boardName}, Total Episodes: {totalEpisodes}, Successful: {successfulEpisodes}, Success Rate: {(successfulEpisodes / (float)totalEpisodes * 100f):F2}%");
    }

    public void RegisterFailure()
    {
        Debug.Log($"[FAILURE] Board: {boardName}, Total Episodes: {totalEpisodes}, Successful: {successfulEpisodes}, Success Rate: {(successfulEpisodes / (float)totalEpisodes * 100f):F2}%");
    }

    // ===========================
    // === Utility Methods
    // ===========================

    float ToNormalized(float value, float min, float max)
    {
        return Mathf.Clamp((value - min) / (max - min) * 2f - 1f, -1f, 1f);
    }

    void SetMarbleSizeFromMesh()
    {
        var rend = marble.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            Vector3 size = rend.bounds.size;
            marbleRadius = size.x * 0.5f;
        }
        else
        {
            Debug.LogWarning("marble hat keinen MeshRenderer!");
        }
    }

    void SetMaxBoardCoordinates()
    {
        if (floorRenderer != null)
        {
            var size = floorRenderer.bounds.size;
            boardMaxX = (size.x - marbleRadius) * 10f;
            boardMaxZ = (size.z - marbleRadius) * 10f;
        }
        else
        {
            Debug.LogWarning("FloorRenderer not assigned!");
        }
    }
}
