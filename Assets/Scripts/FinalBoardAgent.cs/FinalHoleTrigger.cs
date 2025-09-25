using UnityEngine;

public class FinalHoleTrigger : MonoBehaviour
{
    private FinalBoardAgent agent;

    void Awake()
    {
        agent = GetComponentInParent<FinalBoardAgent>();
        if (agent == null)
        {
            Debug.LogError("No FinalBoardAgent found in the scene!");
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (agent != null && other.attachedRigidbody.transform == agent.ball)
        {
            agent.AddReward(-0.2f);
            agent.RegisterFailure();
            Debug.Log($"[EPISODE END: hole] Cumulative Reward: {agent.GetCumulativeReward()}");
            agent.LogEpisodeStatsAndEnd();
        }
    }
}