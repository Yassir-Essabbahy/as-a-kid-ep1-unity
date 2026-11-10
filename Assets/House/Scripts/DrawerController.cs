using UnityEngine;

public class DrawerController : MonoBehaviour, IInteractable
{
    [Header("Drawer Settings")]
    public float slideDistance = 0.5f;
    public float smooth = 2f;
    public bool isLocked = false;
    public string lockedMessage = "الدرج مسدود";

    [Header("Sound")]
    public AudioClip openCloseSound;

    public string PromptMessage => isOpen ? LocalizationManager.Resolve("prompt_close_drawer", "Close Drawer") : LocalizationManager.Resolve("prompt_open_drawer", "Open Drawer");

    private AudioSource audioSource;
    private bool isOpen = false;

    private Vector3 closedPosition;
    private Vector3 openPosition;

    void Start()
    {
        closedPosition = transform.localPosition;
        openPosition = closedPosition + new Vector3(0, 0, -slideDistance);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }
    }

    void Update()
    {
        Vector3 target = isOpen ? openPosition : closedPosition;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            target,
            Time.deltaTime * smooth
        );
    }

    public void Interact()
    {
        ToggleDrawer();
    }

    public void ToggleDrawer()
    {
        if (isLocked)
        {
            HouseUIBridge.ShowMessage(lockedMessage);
            return;
        }

        isOpen = !isOpen;

        // Play sound only if available
        if (openCloseSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openCloseSound);
        }
    }
}
