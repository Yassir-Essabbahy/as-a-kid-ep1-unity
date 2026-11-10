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
            if (textObj == null)
            {
                var canvas = GameObject.Find("Canvas");
                if (canvas != null)
                {
                    var t = canvas.transform.Find("InteractText");
                    if (t != null) textObj = t.gameObject;
                }
            }
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
                if (NpcInteractionText.Instance != null) NpcInteractionText.Instance.ClearPrompt();
                if (interactText != null) { interactText.text = ""; interactText.gameObject.SetActive(false); }
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
                string doorPrompt = LocalizationManager.Resolve("prompt_open_door_f3", "Tap to open door");
                if (NpcInteractionText.Instance != null)
                {
                    NpcInteractionText.Instance.SetPrompt(doorPrompt);
                }
                else if (interactText != null)
                {
                    interactText.gameObject.SetActive(true);
                    LocalizationManager.ApplyText(interactText, Story.MobileUIAdapter.AdaptPrompt(doorPrompt), preserveAlignment: true);
                }

                if (Input.GetKeyDown(interactKey) || ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame)
                {
                    if (NpcInteractionText.Instance != null) NpcInteractionText.Instance.ClearPrompt();
                    if (interactText != null) { interactText.text = ""; interactText.gameObject.SetActive(false); }
                    MetroStorySequenceController.Instance.OnDoorInteracted();
                }
                return;
            }
        }

        if (playerInRange)
        {
            playerInRange = false;
            if (NpcInteractionText.Instance != null) NpcInteractionText.Instance.ClearPrompt();
            if (interactText != null) { interactText.text = ""; interactText.gameObject.SetActive(false); }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (MetroStorySequenceController.Instance == null) return;
        if (MetroStorySequenceController.Instance.currentPhase == MetroStorySequenceController.StoryPhase.DoorWaiting)
        {
            string doorPrompt = LocalizationManager.Resolve("prompt_open_door_f3", "Tap to open door");
            if (NpcInteractionText.Instance != null)
            {
                NpcInteractionText.Instance.SetPrompt(doorPrompt);
            }
            else if (interactText != null)
            {
                interactText.gameObject.SetActive(true);
                LocalizationManager.ApplyText(interactText, Story.MobileUIAdapter.AdaptPrompt(doorPrompt), preserveAlignment: true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (NpcInteractionText.Instance != null) NpcInteractionText.Instance.ClearPrompt();
        if (interactText != null) { interactText.text = ""; interactText.gameObject.SetActive(false); }
    }
}
