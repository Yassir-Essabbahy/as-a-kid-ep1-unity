using UnityEngine;
using UnityEngine.SceneManagement;

public class HouseOutsideDoor : MonoBehaviour, IInteractable
{
    [Header("Scene Transition")]
    [Tooltip("Target scene to load on exit (School / Start scene).")]
    public string targetScene = "Start";

    [Header("Transition Settings")]
    public float fadeDuration = 1.5f;
    public AudioClip doorSound;
    public AudioSource audioSource;

    public string PromptMessage
    {
        get
        {
            if (HouseObjectiveController.Instance != null &&
                HouseObjectiveController.Instance.hasTalkedToMama)
            {
                return LocalizationManager.Resolve("prompt_leave_school", "Leave for School");
            }
            return LocalizationManager.Resolve("prompt_front_door", "Front Door");
        }
    }

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (string.IsNullOrWhiteSpace(targetScene))
            targetScene = "Start";
    }

    public void Interact()
    {
        if (HouseObjectiveController.Instance != null)
        {
            if (!HouseObjectiveController.Instance.hasTalkedToMama)
            {
                HouseUIBridge.ShowMessage("house_need_talk_mom");
                return;
            }
        }

        if (audioSource != null && doorSound != null)
        {
            audioSource.PlayOneShot(doorSound);
        }

        if (string.IsNullOrWhiteSpace(targetScene))
        {
            targetScene = "Start";
        }

        ScreenFader.TransitionToScene(targetScene, fadeDuration);
    }
}
