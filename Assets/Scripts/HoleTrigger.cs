using UnityEngine;

/// <summary>
/// Detects when the marble falls into the catch trigger below the board.
/// In agent mode, it penalizes and ends the episode; in manual mode, it resets the ball and rig.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Hole Trigger")]
[RequireComponent(typeof(Collider))]
public class HoleTrigger : MonoBehaviour
{
    private BoardAgent agent;
    // Manual mode helpers
    private Transform manualMarble;
    private Rigidbody manualMarbleRb;
    private Vector3 manualSpawnPosition;
    private TiltController tiltController;

    /// <summary>
    /// Cache references for both agent and manual modes.
    /// </summary>
    void Awake()
    {
        agent = GetComponentInParent<BoardAgent>();
        // If no agent exists (manual play), operate as a no-op without spamming errors
        if (agent == null)
        {
            Debug.Log("HoleTrigger active without BoardAgent; running in manual mode.");
            // Try to locate the marble and remember its spawn in world space
            var marbleGo = GameObject.Find("Marble");
            if (marbleGo != null)
            {
                manualMarble = marbleGo.transform;
                manualMarbleRb = marbleGo.GetComponent<Rigidbody>();
                manualSpawnPosition = manualMarble.position;
            }
            // Try to find the tilt controller to reset the rig
            tiltController = Object.FindObjectOfType<TiltController>();
        }
    }
    /// <summary>
    /// Handles marble entry for both manual and agent modes.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (agent == null)
        {
            // Manual mode: reset board and marble when the marble hits the catch trigger
            if (manualMarble == null) return;
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            if (rb.transform != manualMarble) return;

            if (tiltController != null) tiltController.ResetRig();
            manualMarble.position = manualSpawnPosition;
            manualMarble.rotation = Quaternion.identity;
            if (manualMarbleRb != null)
            {
                manualMarbleRb.velocity = Vector3.zero;
                manualMarbleRb.angularVelocity = Vector3.zero;
            }
            return;
        }

        // Agent mode: propagate failure and end episode
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var tr = rb.transform;
            if (tr == null || agent.marble == null) return;
            if (tr == agent.marble)
            {
                agent.AddReward(-0.2f);
                agent.RegisterFailure();
                Debug.Log($"[EPISODE END: hole] Cumulative Reward: {agent.GetCumulativeReward()}");
                agent.LogEpisodeStatsAndEnd();
            }
        }
    }
}