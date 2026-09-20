using UnityEngine;
using TMPro;

public class DoorInteractable : MonoBehaviour
{
    [Header("Settings")]
    public float interactionDistance = 4f;
    public TextMeshProUGUI interactText;
    public KeyCode interactKey = KeyCode.E;

    private bool playerInRange = false;

    private void Start()
    {
        if (interactText == null)
        {
            var textObj = GameObject.Find("Canvas/InteractText");
            if (textObj != null) interactText = textObj.GetComponent<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        if (MetroStorySequenceController.Instance == null) return;
        if (MetroStorySequenceController.Instance.currentPhase != MetroStorySequenceController.StoryPhase.DoorWaiting)
        {
            if (playerInRange)
            {
                playerInRange = false;
                if (interactText != null) interactText.text = "";
            }
            return;
        }

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;

        float dist = Vector3.Distance(transform.position, cam.position);
        if (dist <= interactionDistance)
        {
            Ray ray = new Ray(cam.position, cam.forward);
            bool isLookingAtDoor = false;
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                if (hit.collider.gameObject == gameObject || 
                    hit.collider.transform.IsChildOf(transform) || 
                    hit.collider.name.ToLower().Contains("door"))
                {
                    isLookingAtDoor = true;
                }
            }
            if (!isLookingAtDoor && Vector3.Dot(cam.forward, (transform.position - cam.position).normalized) > 0.6f)
            {
                isLookingAtDoor = true;
            }

            if (isLookingAtDoor)
            {
                playerInRange = true;
                if (interactText != null) interactText.text = "Press 'E' to open door";

                if (Input.GetKeyDown(interactKey))
                {
                    if (interactText != null) interactText.text = "";
                    MetroStorySequenceController.Instance.OnDoorInteracted();
                }
                return;
            }
        }

        if (playerInRange)
        {
            playerInRange = false;
            if (interactText != null) interactText.text = "";
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (MetroStorySequenceController.Instance == null) return;
        if (MetroStorySequenceController.Instance.currentPhase == MetroStorySequenceController.StoryPhase.DoorWaiting)
        {
            if (interactText != null) interactText.text = "Press 'E' to open door";
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (interactText != null) interactText.text = "";
    }
}
