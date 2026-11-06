using UnityEngine;
using UnityEngine.SceneManagement;

public class HouseOutsideDoor : MonoBehaviour, IInteractable
{
    [Header("Scene Transition")]
    [Tooltip("Target scene to load on exit.")]
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
                HouseObjectiveController.Instance.hasTalkedToMama &&
                HouseObjectiveController.Instance.hasMoney)
            {
                return "Leave for School";
            }
            return "Front Door";
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
                HouseUIBridge.ShowMessage("door_cant_leave_no_papers");
                return;
            }

            if (!HouseObjectiveController.Instance.hasMoney)
            {
                HouseUIBridge.ShowMessage("door_cant_leave_no_money");
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
