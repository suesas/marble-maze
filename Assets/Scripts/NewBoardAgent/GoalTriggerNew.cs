using UnityEngine;

public class GoalTriggerNew : MonoBehaviour
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
            agent.AddReward(5f);
            Debug.Log($"[EPISODE END: goal] Cumulative Reward: {agent.GetCumulativeReward()}");
            agent.LogEpisodeStatsAndEnd();
        }
    }
}