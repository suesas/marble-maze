using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// ML-Agents Agent for the tilt board. Observes marble/goal positions, board tilt, nearby holes,
/// guidance vectors, path progress, and obstacle rays. Outputs two continuous actions driving tilt.
/// Includes milestone rewards and episode success/failure handling via triggers.
/// </summary>
public class BoardAgent : Agent
{
    // === Board & Marble References ===
    public Transform marble;
    public Transform goal;
    [SerializeField] private MeshRenderer floorRenderer;
    [SerializeField] private TiltController tiltController;

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
    [SerializeField] private float stallTimeoutSeconds = 6f;

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

    /// <summary>
    /// Caches references and derives board bounds and initial marble position.
    /// </summary>
    void Start()
    {
        boardName = gameObject.name;
        SetMarbleSizeFromMesh();
        if (marble != null)
        {
            marbleRb = marble.GetComponent<Rigidbody>();
        }
        else
        {
            Debug.LogWarning("BoardAgent: 'marble' reference is not assigned.");
        }
        SetMaxBoardCoordinates();
        initialMarbleLocalPos = marble != null ? transform.InverseTransformPoint(marble.position) : Vector3.zero;
        if (tiltController == null)
        {
            tiltController = GetComponentInParent<TiltController>();
            if (tiltController == null)
            {
                tiltController = Object.FindObjectOfType<TiltController>();
            }
        }
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        navPath = new NavMeshPath();
        InitializeHoles();
    }

    /// <inheritdoc />
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
        currentTiltX = 0f;
        currentTiltZ = 0f;

        // Reset rig/controller so the agent starts from neutral tilt
        if (tiltController == null)
        {
            tiltController = GetComponentInParent<TiltController>();
            if (tiltController == null)
            {
                tiltController = Object.FindObjectOfType<TiltController>();
            }
        }
        if (tiltController != null)
        {
            tiltController.keyboardInput = false;
            // Do not override controller limits/speed; use values supplied by importer/UI
            tiltController.ResetRig();
            tiltController.SetTilt(0f, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }

        // Add random offset to marble start position
        Vector3 randomOffset = new Vector3(
            Random.Range(-7.0f, 7.0f),
            0f,
            Random.Range(-7.0f, 7.0f)
        );

        // Reset the marble's position and velocity
        if (marble != null && marbleRb != null)
        {
            Vector3 resetPosition = transform.TransformPoint(initialMarbleLocalPos + randomOffset);
            marble.position = resetPosition;
            marbleRb.velocity = Vector3.zero;
            marbleRb.angularVelocity = Vector3.zero;
        }
        else
        {
            Debug.LogWarning("BoardAgent: Missing marble or Rigidbody; skipping marble reset.");
        }

        //Reset Milestone tracking
        lastMilestonePaid = 0;
        pathLength = 0f;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override void OnActionReceived(ActionBuffers actions)
    {
        HandleBoardTiltActions(actions);
        UpdatePathAndProgress();
        DrawIdealPathDebug();
        HandleEpisodeTimeout();
        AddReward(-0.005f);
    }

    /// <inheritdoc />
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        a[0] = Input.GetAxis("Vertical");
        a[1] = -Input.GetAxis("Horizontal");
    }

    /// <summary>
    /// Visualizes the nearest observed holes in the Scene view for debugging.
    /// </summary>
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

    /// <summary>
    /// Adds normalized marble position in board-local space.
    /// </summary>
    private void AddMarblePositionObservation(VectorSensor sensor)
    {
        if (marble == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }
        Vector3 localmarblePos = transform.InverseTransformPoint(marble.position);
        float marbleNx = ToNormalized(localmarblePos.x, -boardMaxX, boardMaxX);
        float marbleNz = ToNormalized(localmarblePos.z, -boardMaxZ, boardMaxZ);

        sensor.AddObservation(marbleNx);
        sensor.AddObservation(marbleNz);
    }

    /// <summary>
    /// Adds normalized goal position in board-local space.
    /// </summary>
    private void AddGoalPositionObservation(VectorSensor sensor)
    {
        if (goal == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }
        Vector3 localGoalPos = transform.InverseTransformPoint(goal.position);
        float goalNx = ToNormalized(localGoalPos.x, -boardMaxX, boardMaxX);
        float goalNz = ToNormalized(localGoalPos.z, -boardMaxZ, boardMaxZ);

        sensor.AddObservation(goalNx);
        sensor.AddObservation(goalNz);
    }

    /// <summary>
    /// Adds normalized current tilt (X/Z) based on controller limits.
    /// </summary>
    private void AddBoardTiltObservation(VectorSensor sensor)
    {
        float limit = tiltController != null ? tiltController.maxTilt : maxTilt;
        float tiltNx = ToNormalized(currentTiltX, -limit, limit);
        float tiltNz = ToNormalized(currentTiltZ, -limit, limit);

        sensor.AddObservation(tiltNx);
        sensor.AddObservation(tiltNz);
    }

    /// <summary>
    /// Adds positions of the k nearest holes in board-local space.
    /// </summary>
    private void AddHolePositionsObservation(VectorSensor sensor)
    {
        if (holes == null)
        {
            for (int i = 0; i < kNearestHoles; i++)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
            return;
        }
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

    /// <summary>
    /// Adds a normalized guidance vector pointing towards the next path point.
    /// </summary>
    private void AddGuidanceObservation(VectorSensor sensor)
    {
        if (marble != null && idealPath != null && idealPath.Count > 1)
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

    /// <summary>
    /// Adds normalized progress along the current path (0..1).
    /// </summary>
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

    /// <summary>
    /// Adds normalized obstacle distances from radial raycasts around the marble.
    /// </summary>
    private void AddRaycastObservations(VectorSensor sensor)
    {
        if (marble == null)
        {
            for (int i = 0; i < rayCount; i++)
            {
                sensor.AddObservation(1f);
            }
            return;
        }
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

    /// <summary>
    /// Converts actions into target tilt angles and applies them to the controller or self.
    /// </summary>
    private void HandleBoardTiltActions(ActionBuffers actions)
    {
        float inputX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float inputZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        // Source tilt speed/limits from the shared controller when available
        float speed = tiltController != null ? tiltController.tiltSpeed : tiltSpeed;
        float limit = tiltController != null ? tiltController.maxTilt : maxTilt;

        // Update the tilt angles based on the input
        currentTiltX = Mathf.Clamp(currentTiltX + inputX * speed * Time.deltaTime, -limit, limit);
        currentTiltZ = Mathf.Clamp(currentTiltZ + inputZ * speed * Time.deltaTime, -limit, limit);

        // Apply the tilt via TiltController/GimbalRig if available, else rotate self
        if (tiltController != null)
        {
            tiltController.SetTilt(currentTiltX, currentTiltZ);
        }
        else
        {
            transform.rotation = Quaternion.Euler(currentTiltX, 0, currentTiltZ);
        }
    }

    /// <summary>
    /// Updates NavMesh path, nearest point, progress measurement, and milestone rewards.
    /// </summary>
    private void UpdatePathAndProgress()
    {
        if (marble == null || goal == null || navPath == null)
        {
            return;
        }
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

    /// <summary>
    /// Rewards positive progress; tracks timeouts when stalled.
    /// </summary>
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
            if (timeSinceLastProgress > stallTimeoutSeconds) // timeout without progress
            {
                Debug.Log($"[EPISODE END: timeout] Cumulative Reward: {GetCumulativeReward()}");
                LogEpisodeStatsAndEnd();
            }
        }
    }

    /// <summary>
    /// Draws the interpolated path for visualization.
    /// </summary>
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

    /// <summary>
    /// Ends the episode with no reward when the max step budget is consumed.
    /// </summary>
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

    /// <summary>
    /// Caches transforms tagged as holes for nearest-neighbor observations.
    /// </summary>
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

            if (!IsPathClearForMarble(currentPosition, target))
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

    /// <summary>
    /// Checks if a sphere cast along the segment is free of obstacles for the marble.
    /// </summary>
    bool IsPathClearForMarble(Vector3 from, Vector3 to)
    {
        from.y += rayHeight;
        to.y += rayHeight;

        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist < 1e-4f) return true;

        dir /= dist;

        return !Physics.SphereCast(from, marbleRadius * clearanceFactor, dir, out _, dist, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Computes distance along a polyline up to the vertex nearest to <paramref name="point"/>.
    /// </summary>
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

    /// <summary>
    /// Linearly interpolates between corners to create a denser path with a fixed spacing.
    /// </summary>
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

    

    // ===========================
    // === Reward & Stats Methods
    // ===========================

    /// <summary>
    /// Provides discrete bonus rewards based on fractional progress along the path.
    /// </summary>
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

    /// <summary>
    /// Records episode stats and ends the episode.
    /// </summary>
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

    /// <summary>
    /// Tracks a successful episode and logs a concise summary.
    /// </summary>
    public void RegisterSuccess()
    {
        successfulEpisodes++;
        Debug.Log($"[SUCCESS] Board: {boardName}, Total Episodes: {totalEpisodes}, Successful: {successfulEpisodes}, Success Rate: {(successfulEpisodes / (float)totalEpisodes * 100f):F2}%");
    }

    /// <summary>
    /// Logs a failure episode for quick feedback.
    /// </summary>
    public void RegisterFailure()
    {
        Debug.Log($"[FAILURE] Board: {boardName}, Total Episodes: {totalEpisodes}, Successful: {successfulEpisodes}, Success Rate: {(successfulEpisodes / (float)totalEpisodes * 100f):F2}%");
    }

    // ===========================
    // === Utility Methods
    // ===========================

    /// <summary>
    /// Maps a value in [min, max] to [-1, 1] with clamping.
    /// </summary>
    float ToNormalized(float value, float min, float max)
    {
        float denom = max - min;
        if (Mathf.Abs(denom) < 1e-6f) return 0f;
        return Mathf.Clamp((value - min) / denom * 2f - 1f, -1f, 1f);
    }

    /// <summary>
    /// Derives marble radius from its renderer bounds; warns if missing.
    /// </summary>
    void SetMarbleSizeFromMesh()
    {
        if (marble == null)
        {
            Debug.LogWarning("BoardAgent: 'marble' reference is not assigned; cannot derive size.");
            return;
        }
        var rend = marble.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            Vector3 size = rend.bounds.size;
            marbleRadius = size.x * 0.5f;
        }
        else
        {
            Debug.LogWarning("Marble is missing a MeshRenderer!");
        }
    }

    /// <summary>
    /// Computes board extents from the floor renderer to normalize observations.
    /// </summary>
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

    // ===========================
    // === Public Read-only HUD Accessors
    // ===========================

    /// <summary>
    /// Seconds since the last positive progress delta.
    /// </summary>
    public float TimeSinceLastProgress => timeSinceLastProgress;

    /// <summary>
    /// Timeout in seconds used to end stalled episodes.
    /// </summary>
    public float StallTimeoutSeconds => stallTimeoutSeconds;

    /// <summary>
    /// Fractional completion along the current path [0,1].
    /// </summary>
    public float PathCompletion01 => (pathLength > 0f) ? Mathf.Clamp01(currentProgress / pathLength) : 0f;
}
