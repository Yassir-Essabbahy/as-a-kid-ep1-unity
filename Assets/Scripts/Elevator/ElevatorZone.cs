using UnityEngine;

public class ElevatorZone : MonoBehaviour
{
    public string playerTag = "Player";
    [Tooltip("Optional explicit reference. If empty, the zone finds the scene elevator once.")]
    public ElevatorButton elevator;

    // Kept serialized so existing scene data is not lost; the zone no longer
    // controls the Animator or audio directly.
    public Animator doorAnimator;
    public string openTrigger = "Open";
    public AudioSource audioSource;
    public AudioClip doorOpenSound;

    private bool hasOpened = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasOpened) return;
        if (!other.CompareTag(playerTag)) return;

        hasOpened = true;

        if (elevator == null)
            elevator = FindAnyObjectByType<ElevatorButton>();

        if (elevator != null)
            elevator.RequestDoorOpenFromZone();
    }

    void OnTriggerExit(Collider other)
    {
        // allow doors to open again if the player leaves without pressing the button
        if (other.CompareTag(playerTag)) hasOpened = false;
    }
}