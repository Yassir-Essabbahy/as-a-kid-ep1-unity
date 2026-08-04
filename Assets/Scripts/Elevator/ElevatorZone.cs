using UnityEngine;

public class ElevatorZone : MonoBehaviour
{
    public string playerTag = "Player";
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

        if (doorAnimator != null) doorAnimator.SetTrigger(openTrigger);
        if (audioSource != null && doorOpenSound != null) audioSource.PlayOneShot(doorOpenSound);
    }

    void OnTriggerExit(Collider other)
    {
        // allow doors to open again if the player leaves without pressing the button
        if (other.CompareTag(playerTag)) hasOpened = false;
    }
}