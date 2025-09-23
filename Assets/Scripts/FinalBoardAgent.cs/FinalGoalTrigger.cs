using UnityEngine;

public class FinalGoalTrigger : MonoBehaviour
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
            agent.AddReward(5f);
            Debug.Log($"[EPISODE END: goal] Cumulative Reward: {agent.GetCumulativeReward()}");
            agent.LogEpisodeStatsAndEnd();
        }
    }
}