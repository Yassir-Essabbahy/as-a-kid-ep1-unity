using UnityEngine;

public class PlatformEndTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.OnPlatformEndReached();
        }
    }
}
