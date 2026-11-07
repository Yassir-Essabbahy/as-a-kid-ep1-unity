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

    public bool IsOpen => isOpen;
    public bool isCinematicControlled = false;

    private bool isOpen = false;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Awake()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0, 0, openAngle);
    }

    void Start()
    {
        if (closedRotation == Quaternion.identity && openRotation == Quaternion.identity)
        {
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0, 0, openAngle);
        }

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
        if (isCinematicControlled) return;

        if (isOpen)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, openRotation, Time.deltaTime * smooth);
        else
            transform.localRotation = Quaternion.Slerp(transform.localRotation, closedRotation, Time.deltaTime * smooth);
    }

    public void SetOpenImmediate(bool open)
    {
        isOpen = open;
        if (closedRotation == Quaternion.identity && openRotation == Quaternion.identity)
        {
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0, 0, openAngle);
        }
        transform.localRotation = open ? openRotation : closedRotation;
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
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
