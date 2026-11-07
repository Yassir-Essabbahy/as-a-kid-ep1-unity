using UnityEngine;

public class HouseBackpackInteractable : MonoBehaviour, IInteractable
{
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pickupSound;

    public string PromptMessage => "Take Backpack";

    private bool collected = false;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }
        }
    }

    public void Interact()
    {
        if (collected) return;
        collected = true;

        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnBackpackTaken();
        }

        gameObject.SetActive(false);
    }
}
