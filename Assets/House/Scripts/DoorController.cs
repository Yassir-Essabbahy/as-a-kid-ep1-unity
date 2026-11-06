using UnityEngine;

public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    public float openAngle = 90f;
    public float smooth = 2f;
    public bool isLocked;
    public string lockedMessage = "Baba makay5linich nd5l hna";

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    public string PromptMessage => isOpen ? "Close Door" : "Open Door";

    private bool isOpen = false;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0, 0, openAngle);

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

    void Update()
    {
        if (isOpen)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, openRotation, Time.deltaTime * smooth);
        else
            transform.localRotation = Quaternion.Slerp(transform.localRotation, closedRotation, Time.deltaTime * smooth);
    }

    public void Interact()
    {
        ToggleDoor();
    }

    public void ToggleDoor()
    {
        if (isLocked)
        {
            if (audioSource != null && lockedSound != null)
                audioSource.PlayOneShot(lockedSound);

            HouseUIBridge.ShowMessage(lockedMessage);
            return;
        }

        isOpen = !isOpen;

        if (isOpen)
        {
            if (audioSource != null && openSound != null)
                audioSource.PlayOneShot(openSound);
        }
        else
        {
            if (audioSource != null && closeSound != null)
                audioSource.PlayOneShot(closeSound);
        }
    }
}
