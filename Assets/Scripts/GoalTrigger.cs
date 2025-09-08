using UnityEngine;

public class GoalTrigger : MonoBehaviour
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
            agent.AddReward(10f);
            Debug.Log($"[EPISODE END: goal] Cumulative Reward: {agent.GetCumulativeReward()}");
            agent.LogEpisodeStatsAndEnd(agent.GetCumulativeReward());
        }
    }
}