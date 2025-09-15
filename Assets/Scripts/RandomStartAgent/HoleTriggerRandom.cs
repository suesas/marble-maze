using UnityEngine;

public class HoleTriggerRandom : MonoBehaviour
{
    private RandomStartAgent agent;

    void Awake()
    {
        agent = GetComponentInParent<RandomStartAgent>();
        if (agent == null)
        {
            Debug.LogError("No RandomStartAgent found in the scene!");
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