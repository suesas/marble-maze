using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // oder "Ball", je nachdem
        {
            Debug.Log("Goal reached!");
            var agent = FindObjectOfType<BoardAgent>();
            if (agent != null)
            {
                agent.SetReward(5f);
                Debug.Log($"[EPISODE END] Cumulative Reward: {agent.GetCumulativeReward()}");
                agent.EndEpisode();
            }
        }
    }
}