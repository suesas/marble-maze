using UnityEngine;

public class HoleTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // oder "Ball", je nachdem
        {
            Debug.Log("Fell into hole!");
            var agent = FindObjectOfType<BoardAgent>();
            if (agent != null)
            {
                agent.SetReward(-1f);
                agent.EndEpisode();
            }
        }
    }
}