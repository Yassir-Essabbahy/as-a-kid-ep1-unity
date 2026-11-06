using UnityEngine;
using UnityEngine.SceneManagement;

public class HouseOutsideDoor : MonoBehaviour, IInteractable
{
    [Header("Scene Transition")]
    [Tooltip("Target scene to load on exit. Leave empty to configure later.")]
    public string targetScene = "";

    [Header("Transition Settings")]
    public float fadeDuration = 1.0f;
    public AudioClip doorSound;
    public AudioSource audioSource;

    public string PromptMessage => "Go Outside";

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void Interact()
    {
        if (audioSource != null && doorSound != null)
        {
            audioSource.PlayOneShot(doorSound);
        }

        if (string.IsNullOrWhiteSpace(targetScene))
        {
            Debug.LogWarning("[House] OutsideDoor has no target scene configured");
            return;
        }

        ScreenFader.TransitionToScene(targetScene, fadeDuration);
    }
}
