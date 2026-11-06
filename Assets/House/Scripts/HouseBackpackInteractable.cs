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

        if (audioSource != null && pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnBackpackTaken();
        }

        string label = "School Backpack Collected";
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get("house_backpack_taken");
            if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) label = loc;
        }

        if (ItemPickupToast.Instance != null)
        {
            ItemPickupToast.Instance.Show(null, label, 1);
        }

        HouseUIBridge.ShowMessage("house_backpack_taken", 2.0f);

        gameObject.SetActive(false);
    }
}
