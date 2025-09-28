using UnityEngine;

/// <summary>
/// Detects when the agent-controlled marble reaches the goal trigger and ends the episode with success.
/// Operates as a no-op when no <see cref="BoardAgent"/> is present (manual mode).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Goal Trigger")]
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    private BoardAgent agent;

    /// <summary>
    /// Cache optional agent reference; in manual mode, keep silent operation.
    /// </summary>
    void Awake()
    {
        agent = GetComponentInParent<BoardAgent>();
        // If no agent exists (manual play), operate as a no-op without spamming errors
        if (agent == null) { Debug.Log("GoalTrigger active without BoardAgent; running in manual mode."); }
    }
    /// <summary>
    /// On marble entry, reward and end the episode through the agent.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (agent == null) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var tr = rb.transform;
        if (tr == null || agent.marble == null) return;
        if (tr == agent.marble)
        {
            agent.AddReward(5f);
            Debug.Log($"[EPISODE END: goal] Cumulative Reward: {agent.GetCumulativeReward()}");
            agent.RegisterSuccess();
            agent.LogEpisodeStatsAndEnd();
        }
    }
}