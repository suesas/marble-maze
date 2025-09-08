using UnityEngine;

public class HoleTriggerNew : MonoBehaviour
{
    private NewAgent agent;

    void Awake()
    {
        agent = GetComponentInParent<NewAgent>();
        if (agent == null)
        {
            Debug.LogError("No NewAgent found in the scene!");
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (agent != null && other.attachedRigidbody.transform == agent.ball)
        {
            agent.AddReward(-0.2f);
            Debug.Log($"[EPISODE END: hole] Cumulative Reward: {agent.GetCumulativeReward()}");
            agent.LogEpisodeStatsAndEnd();
        }
    }
}