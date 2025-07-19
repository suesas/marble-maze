using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.AI;

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
    private int stepsRemaining;
    private NavMeshPath navPath;


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
    }
    public override void Initialize()
    {
        navPath = new NavMeshPath();
    }

    public override void OnEpisodeBegin()
    {
        stepsRemaining = MaxStep;
        transform.rotation = Quaternion.identity;
        currentTiltX = 0f;
        currentTiltZ = 0f;

        Vector3 resetPosition = transform.TransformPoint(initialBallLocalPos + Vector3.up * 0.2f);
        ball.position = resetPosition;

        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;


    }

    void Update()
    {
        // Optional: Reset the agent if it falls off the board
        if (Input.GetKeyDown(KeyCode.R))
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
        float inputX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float inputZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        currentTiltX = Mathf.Clamp(currentTiltX + inputX * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);
        currentTiltZ = Mathf.Clamp(currentTiltZ + inputZ * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);


        transform.rotation = Quaternion.Euler(currentTiltX, 0, currentTiltZ);
        //Debug.DrawLine(ball.position, goal.position, Color.magenta);


        Vector3 localBall = transform.InverseTransformPoint(ball.position);
        Vector3 localGoal = transform.InverseTransformPoint(goal.position);
        float dist = Vector3.Distance(localBall, localGoal);

        stepsRemaining--;

        if (NavMesh.CalculatePath(ball.position, goal.position, NavMesh.AllAreas, navPath))
        {
            float pathLength = 0f;
            for (int i = 0; i < navPath.corners.Length - 1; i++)
            {
                pathLength += Vector3.Distance(navPath.corners[i], navPath.corners[i + 1]);
            }

            // Optional für Debug:
            for (int i = 0; i < navPath.corners.Length - 1; i++)
            {
                Debug.DrawLine(navPath.corners[i], navPath.corners[i + 1], Color.cyan);
            }

            AddReward(-pathLength * 0.001f);
        }
        else
        {
            // Fallback wenn Pfad nicht berechenbar
            AddReward(-1f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        a[0] = Input.GetAxis("Vertical");
        a[1] = -Input.GetAxis("Horizontal");
    }
}