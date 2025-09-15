using UnityEngine;

public class GoalTriggerRandom : MonoBehaviour
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
            agent.AddReward(5f);
            Debug.Log($"[EPISODE END: goal] Cumulative Reward: {agent.GetCumulativeReward()}");
            agent.LogEpisodeStatsAndEnd();
        }
    }
}