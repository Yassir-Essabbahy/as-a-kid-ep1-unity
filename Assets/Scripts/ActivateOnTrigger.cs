using UnityEngine;

public class ActivateOnTrigger : MonoBehaviour
{
    [Tooltip("Objects to activate when triggered")]
    public GameObject[] objectsToActivate;

    [Tooltip("Audio source to lower")]
    public AudioSource audioSource;

    [Range(0f, 1f)]
    public float newVolume = 0.2f;

    [Tooltip("Tag required to trigger this (leave empty to allow anything)")]
    public string requiredTag = "Player";

    [Tooltip("Only trigger once")]
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;

        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        // Activate objects
        foreach (GameObject obj in objectsToActivate)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        // Lower the audio volume
        if (audioSource != null)
        {
            audioSource.volume = newVolume;
        }

        hasTriggered = true;
    }
}