using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
public class NewAgent : Agent
{
    public Rigidbody ballRb;
    public Transform ball;
    public Transform goal;

    public float tiltSpeed = 15f;
    public float maxTilt = 30f;

    private float currentTiltX = 0f;
    private float currentTiltZ = 0f;
    private Vector3 initialBallLocalPos;
    private NavMeshPath navPath;

    private float previousProgress = 0f;
    private float currentProgress = 0f;
    private List<Vector3> idealPath;

    private List<Transform> holeTs;
    public int kNearestHoles = 5;        // wie viele Löcher max. einfließen
    private float boardHalfSizeX;
    private float boardHalfSizeZ;

    private int currentPathIndex = 0;

    private int achievedCheckpoints = 0;
    private int lastCheckpointIndex = 0;
    private List<Vector3> debugObservedHoles = new List<Vector3>();
    private float timeSinceLastProgress = 0f;
    private Quaternion baseRotation;

    // === Milestone-Reward ===
    [SerializeField] int milestoneBins = 10;       // 10 => alle 10 % der Distanz
    [SerializeField] float milestoneBonus = 3f; // Bonus pro Meilenstein
    private float pathLength;                      // gesamte Pfadlänge
    private int lastMilestonePaid;                 // letzter gezahlter Index (0..milestoneBins)
    private List<float> cumPathDist;


    [SerializeField] float rayHeight = 0.02f;      // leicht über dem Boden casten
    [SerializeField] LayerMask obstacleMask;       // nur Wände/Barrieren-Layer
    [SerializeField] QueryTriggerInteraction tri = QueryTriggerInteraction.Ignore;

    private float ballRadius;
    [SerializeField] float clearanceFactor = 0.8f; // etwas kleiner als die Kugel

    private float maxSoFar = 0f; // für Debug-Zwecke

    public int rayCount = 8;
    public float maxDistance = 5f;
    void Start()
    {
        // Marble Size ermitteln
        SetBallSizeFromMesh();

        // Board Size ermitteln
        SetBoardSizeFromMesh();

        // Startposition der Kugel merken (lokal)
        initialBallLocalPos = transform.InverseTransformPoint(ball.position);

        //Time.timeScale = 0.8f; // Alles läuft 2x langsamer
    }
    public override void Initialize()
    {
        // Pathfinding initialisieren
        navPath = new NavMeshPath();

        // Alle Löcher im Level finden
        var holes = GameObject.FindGameObjectsWithTag("Hole");
        holeTs = new List<Transform>(holes.Length);
        foreach (var h in holes) holeTs.Add(h.transform);
    }

    public override void OnEpisodeBegin()
    {
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

        // Reset the ball's position and velocity
        Vector3 resetPosition = transform.TransformPoint(initialBallLocalPos);
        ball.position = resetPosition;
        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        //Reset Milestone tracking
        lastMilestonePaid = 0;
        pathLength = 0f;

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log($"[EPISODE END] Cumulative Reward: {GetCumulativeReward()}");
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            LogEpisodeStatsAndEnd();
        }
        float speed = ballRb.velocity.x;
        if (speed > maxSoFar)
        {
            maxSoFar = speed;
            Debug.Log($"New max speed: {maxSoFar}");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Debug-Liste für Gizmos zurücksetzen
        debugObservedHoles.Clear();

        // Marble Position normalized
        Vector3 localBallPos = transform.InverseTransformPoint(ball.position);
        float marbleNx = ToNormalized(localBallPos.x, -190f, 190f);
        float marbleNz = ToNormalized(localBallPos.z, -160f, 160f);

        sensor.AddObservation(marbleNx);
        sensor.AddObservation(marbleNz);

        // --- Goal-Position (lokal, x/z normiert; y wie gehabt) ---
        Vector3 localGoalPos = transform.InverseTransformPoint(goal.position);
        float goalNx = ToNormalized(localGoalPos.x, -190f, 190f);
        float goalNz = ToNormalized(localGoalPos.z, -160f, 160f);

        sensor.AddObservation(goalNx);
        sensor.AddObservation(goalNz);

        /*
        // --- Velocity (skaliert, damit Zahlenbereiche stabil bleiben) ---
        Vector3 v = ballRb.velocity;

        sensor.AddObservation(ToNormalized(v.x, -7.975821f, 7.975821f));
        sensor.AddObservation(ToNormalized(v.z, -7.975821f, 7.975821f));
        */

        //--- Board-Tilt normiert ---
        float tiltNx = ToNormalized(currentTiltX, -maxTilt, maxTilt);
        float tiltNz = ToNormalized(currentTiltZ, -maxTilt, maxTilt);

        sensor.AddObservation(tiltNx);
        sensor.AddObservation(tiltNz);

        // --- K nächste Löcher: nur (nx, nz) je Loch als zwei Floats ---
        // Nach Distanz zur Kugel sortieren
        holeTs.Sort((a, b) =>
        {
            float da = (a.position - ball.position).sqrMagnitude;
            float db = (b.position - ball.position).sqrMagnitude;
            return da.CompareTo(db);
        });

        int count = Mathf.Min(kNearestHoles, holeTs.Count);
        for (int i = 0; i < count; i++)
        {
            Vector3 local = transform.InverseTransformPoint(holeTs[i].position);
            debugObservedHoles.Add(holeTs[i].position); // fürs Gizmo-Highlight

            // normierte Position (x/z)
            float hx = ToNormalized(local.x, -190f, 190f);
            float hz = ToNormalized(local.z, -160f, 160f);

            sensor.AddObservation(hx);
            sensor.AddObservation(hz);
        }

        // Padding, falls weniger als k Löcher gefunden
        for (int i = count; i < kNearestHoles; i++)
        {
            sensor.AddObservation(0f); // hx
            sensor.AddObservation(0f); // hz
        }

        // ---- Richtungsvektor zum nächsten Pfadpunkt ----
        if (idealPath != null && idealPath.Count > 1)
        {
            int nextIdx = Mathf.Min(currentPathIndex + 1, idealPath.Count - 1);
            Vector3 nextTarget = idealPath[nextIdx];

            Vector3 flat = nextTarget - ball.position;
            flat.y = 0f; // nur XZ-Ebene

            Vector3 localGuidance = transform.InverseTransformDirection(flat);
            sensor.AddObservation(localGuidance.normalized);

            //Debug.DrawLine(ball.position, ball.position + flat.normalized * 0.75f, Color.magenta, 0f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
        }

        // --- Path Progress (0..1) ---
        if (pathLength != 0f)
        {
            float distAlongPath = currentProgress / pathLength;
            sensor.AddObservation(distAlongPath); // ∈ [0,1]
        }

        // --- Raycast-Distanzinformationen  ---

        for (int i = 0; i < rayCount; i++)
        {
            bool isClear = true;
            float angle = (360f / rayCount) * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Vector3 origin = ball.position + Vector3.up * rayHeight;

            float normalizedDist = 1f;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, obstacleMask, tri))
            {
                //Debug.Log($"Hit {hit.collider.name} at distance {hit.distance}");
                normalizedDist = hit.distance / maxDistance;
                isClear = false;
            }

            sensor.AddObservation(normalizedDist);
            //sensor.AddObservation(isClear ? 1f : 0f);
            //Debug.Log($"Ray {i}: angle={angle}°, isClear={isClear}, normDist={normalizedDist}");

            // Debug optional
            Debug.DrawRay(origin, dir * maxDistance, Color.red, 0f);
        }
    }

    float ToNormalized(float value, float min, float max)
    {
        return Mathf.Clamp((value - min) / (max - min) * 2f - 1f, -1f, 1f);
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

        // --- Pathfinding & Rewarding ---
        if (NavMesh.CalculatePath(transform.TransformPoint(initialBallLocalPos), goal.position, NavMesh.AllAreas, navPath))
        {
            idealPath = InterpolatePath(navPath, 0.25f); 
            cumPathDist = BuildCumulativeDistances(idealPath);
            pathLength = cumPathDist[cumPathDist.Count - 1];

            // Suche nächsten gültigen Punkt
            int nearestIndex;
            Vector3 nearest = FindVisibleNearestPathPointConstrained(ball.position, idealPath, currentPathIndex, out nearestIndex, 3);
            currentPathIndex = Mathf.Max(currentPathIndex, nearestIndex);
            currentProgress = GetDistanceAlongPath(nearest, idealPath.ToArray());
            Debug.DrawLine(ball.position, nearest, Color.black, 1f);
            currentPathIndex = Mathf.Max(currentPathIndex, nearestIndex);
            if (nearestIndex > lastCheckpointIndex)
            {
                achievedCheckpoints += (nearestIndex - lastCheckpointIndex);
                lastCheckpointIndex = nearestIndex;
            }

            float rewardDelta = currentProgress - previousProgress;
            float progressDelta = currentProgress - previousProgress;

            if (progressDelta > 0.01f)
            {
                AddReward(progressDelta * 0.5f); // old *1.5f
                previousProgress = currentProgress;
                timeSinceLastProgress = 0f; // Reset, wenn Fortschritt gemacht wurde
            }
            else
            {
                timeSinceLastProgress += Time.deltaTime;
                if (timeSinceLastProgress > 6f) // 6 Sekunden ohne Fortschritt
                {
                    Debug.Log($"[EPISODE END: timeout] Cumulative Reward: {GetCumulativeReward()}");
                    LogEpisodeStatsAndEnd();
                }
            }
            RewardMilestones();
        }

        Color[] colors = new Color[] { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan };
        for (int i = 0; i < idealPath.Count - 1; i++)
        {
            Color lineColor = colors[i % colors.Length]; // Wechselt durch die Farben
            Debug.DrawLine(idealPath[i], idealPath[i + 1], lineColor, 1f);
        }

        if (StepCount >= MaxStep - 1)
        {
            SetReward(0);
            Debug.Log($"[EPISODE END: timeout] Cumulative Reward: {GetCumulativeReward()}");
            LogEpisodeStatsAndEnd();
        }
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
            //Gizmos.DrawWireSphere(p, 0.75f); // Radius passend zur Boardgröße
        }
    }

    private Vector3 FindVisibleNearestPathPointConstrained(
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

        float best = float.MaxValue;
        Vector3 bestPt = path[currentIndex];

        for (int i = start; i <= end; i++)
        {
            Vector3 target = path[i];
            bool clear = IsPathClearForBall(currentPosition, target);

            // Sichtprüfung: darf nicht durch Wände blockiert sein
            NavMeshHit hit;
            if (NavMesh.Raycast(currentPosition, target, out hit, NavMesh.AllAreas))
                continue;
            if (!clear) continue;

            float d = (target - currentPosition).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestPt = target;
                nearestIndex = i;
            }
        }

        return bestPt;
    }

    bool IsLineClearPhysics(Vector3 from, Vector3 to)
    {
        // leicht anheben, damit wir nicht am Boden „kratzen“
        from.y += rayHeight;
        to.y += rayHeight;
        // Prüft, ob IRGENDETWAS dazwischen liegt
        return !Physics.Linecast(from, to, obstacleMask, tri);
    }

    bool IsPathClearForBall(Vector3 from, Vector3 to)
    {
        from.y += rayHeight;
        to.y += rayHeight;

        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist < 1e-4f) return true;

        dir /= dist; // normieren

        // „dicker“ Strahl in Kugelbreite
        return !Physics.SphereCast(from, ballRadius * clearanceFactor, dir, out _, dist, obstacleMask, tri);
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

    public void LogEpisodeStatsAndEnd()
    {
        Academy.Instance.StatsRecorder.Add("achieved_checkpoints", achievedCheckpoints);

        if (idealPath != null && idealPath.Count > 1)
            Academy.Instance.StatsRecorder.Add("progress_norm", lastCheckpointIndex / (float)(idealPath.Count - 1));
        EndEpisode();
    }

    List<float> BuildCumulativeDistances(List<Vector3> path)
    {
        var cum = new List<float>();
        float total = 0f;
        cum.Add(0f);

        for (int i = 0; i < path.Count - 1; i++)
        {
            total += Vector3.Distance(path[i], path[i + 1]);
            cum.Add(total);
        }

        return cum;
    }

    void RewardMilestones()
    {
        if (pathLength <= 0f) return;

        // aktueller Fortschritt in Metern (oder Unity-Einheiten)
        float progressDist = currentProgress;

        // welcher Meilenstein wurde erreicht?
        int reached = Mathf.FloorToInt((progressDist / pathLength) * milestoneBins);

        // alle noch nicht bezahlten Meilensteine ausschütten
        while (lastMilestonePaid < reached && lastMilestonePaid < milestoneBins)
        {
            lastMilestonePaid++;
            AddReward(milestoneBonus);

            // optional: fürs Logging
            Debug.Log($"[MILESTONE] Reached {lastMilestonePaid}/{milestoneBins}, total reward now {GetCumulativeReward()}");
        }
    }
    void SetBallSizeFromMesh()
    {
        var rend = ball.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            Vector3 size = rend.bounds.size;
            ballRadius = size.x * 0.5f;
        }
        else
        {
            Debug.LogWarning("Ball hat keinen MeshRenderer!");
        }
    }

    void SetBoardSizeFromMesh()
    {
        var rend = GetComponentInChildren<MeshRenderer>();
        if (rend != null)
        {
            var size = rend.bounds.size;
            boardHalfSizeX = size.x * 0.5f;
            boardHalfSizeZ = size.z * 0.5f;
        }
        else
        {
            // Fallback falls kein MeshRenderer gefunden
            Debug.LogWarning("No MeshRenderer found on BoardAgent; using default board size values.");
            if (boardHalfSizeX <= 0f) boardHalfSizeX = 0.115f;
            if (boardHalfSizeZ <= 0f) boardHalfSizeZ = 0.14f;
        }
    }
}


