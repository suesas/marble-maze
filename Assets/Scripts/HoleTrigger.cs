using UnityEngine;

public class HoleTrigger : MonoBehaviour
{
    private BoardAgent agent;

    void Awake()
    {
        agent = GetComponentInParent<BoardAgent>();
        if (agent == null)
        {
            Debug.LogError("No BoardAgent found in the scene!");
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (agent != null && other.attachedRigidbody.transform == agent.ball)
        {
            agent.AddReward(-1f);
            Debug.Log($"[EPISODE END: hole] Cumulative Reward: {agent.GetCumulativeReward()}");
            agent.LogEpisodeStatsAndEnd(agent.GetCumulativeReward());
        }
    }
}