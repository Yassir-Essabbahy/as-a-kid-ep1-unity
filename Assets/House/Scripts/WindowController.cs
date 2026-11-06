using UnityEngine;

public class WindowController : MonoBehaviour, IInteractable
{
    [Header("Window Settings")]
    public float slideDistance = 1f;
    public float smooth = 2f;
    public bool isLocked = false;
    public string lockedMessage = "النافذة مسدودة";

    public string PromptMessage => isOpen ? "Close Window" : "Open Window";

    private bool isOpen = false;
    private Vector3 closedPosition;
    private Vector3 openPosition;

    void Start()
    {
        closedPosition = transform.localPosition;
        openPosition = closedPosition + new Vector3(-slideDistance, 0, 0);
    }

    void Update()
    {
        Vector3 target = isOpen ? openPosition : closedPosition;
        transform.localPosition = Vector3.Lerp(
            transform.localPosition, target, Time.deltaTime * smooth);
    }

    public void Interact()
    {
        ToggleWindow();
    }

    public void ToggleWindow()
    {
        if (isLocked)
        {
            HouseUIBridge.ShowMessage(lockedMessage);
            return;
        }
        isOpen = !isOpen;
    }
}
