using UnityEngine;
using TMPro;

public class DoorInteractable : MonoBehaviour
{
    [Header("Settings")]
    public float interactionDistance = 4.5f;
    public float aimHeightOffset = 1.35f;
    public bool autoTriggerOnApproach = true;
    public float autoTriggerDistance = 1.65f;
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

    private void ShowDoorPrompt()
    {
        playerInRange = true;
        string doorPrompt = LocalizationManager.Resolve("prompt_open_door_f3", "Tap to open door");
        if (NpcInteractionText.Instance != null)
        {
            NpcInteractionText.Instance.SetPrompt(doorPrompt, InteractWithDoor);
        }
        else if (interactText != null)
        {
            interactText.gameObject.SetActive(true);
            LocalizationManager.ApplyText(interactText, Story.MobileUIAdapter.AdaptPrompt(doorPrompt), preserveAlignment: true);
        }
    }

    private void ClearDoorPrompt()
    {
        playerInRange = false;
        if (NpcInteractionText.Instance != null) NpcInteractionText.Instance.ClearPrompt();
        if (interactText != null) { interactText.text = ""; interactText.gameObject.SetActive(false); }
    }

    private void Update()
    {
        var storyCtrl = MetroStorySequenceController.Instance;
        if (storyCtrl == null || storyCtrl.currentPhase != MetroStorySequenceController.StoryPhase.DoorWaiting)
        {
            if (playerInRange) ClearDoorPrompt();
            return;
        }

        if ((NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning) ||
            AsAKid.Conversation.ConversationUI.IsAnyActive ||
            MetroFuseBoxPuzzle.IsAnyInspecting ||
            (WaitingPlatformLoopController.Instance != null && WaitingPlatformLoopController.Instance.IsJumpscareRunning))
        {
            if (playerInRange) ClearDoorPrompt();
            return;
        }

        Camera activeCam = Camera.main;
        if (activeCam == null && FirstPersonController.ActiveInstance != null)
        {
            activeCam = FirstPersonController.ActiveInstance.playerCamera;
        }
        if (activeCam == null) return;

        Transform cam = activeCam.transform;
        Vector3 doorCenter = transform.position + Vector3.up * aimHeightOffset;
        float dist = Vector3.Distance(doorCenter, cam.position);
        float horizDist = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(cam.position.x, cam.position.z)
        );
        float vertDelta = Mathf.Abs(cam.position.y - doorCenter.y);

        if (autoTriggerOnApproach && horizDist <= autoTriggerDistance && vertDelta <= 2.2f)
        {
            InteractWithDoor();
            return;
        }

        if (dist <= interactionDistance)
        {
            Ray ray = new Ray(cam.position, cam.forward);
            bool isLookingAtDoor = false;
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject == gameObject ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.name.ToLower().Contains("door"))
                {
                    isLookingAtDoor = true;
                }
            }
            if (!isLookingAtDoor && Vector3.Dot(cam.forward, (doorCenter - cam.position).normalized) > 0.45f)
            {
                isLookingAtDoor = true;
            }

            if (isLookingAtDoor)
            {
                ShowDoorPrompt();

                bool desktopClick = !Application.isMobilePlatform &&
                                    Cursor.lockState == CursorLockMode.Locked &&
                                    Input.GetMouseButtonDown(0);
                if (Input.GetKeyDown(interactKey) ||
                    ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame ||
                    desktopClick)
                {
                    InteractWithDoor();
                }
                return;
            }
        }

        if (playerInRange)
        {
            ClearDoorPrompt();
        }
    }

    public void InteractWithDoor()
    {
        if (MetroStorySequenceController.Instance == null ||
            MetroStorySequenceController.Instance.currentPhase != MetroStorySequenceController.StoryPhase.DoorWaiting)
        {
            return;
        }

        ClearDoorPrompt();
        MetroStorySequenceController.Instance.OnDoorInteracted();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (MetroStorySequenceController.Instance == null) return;
        if (MetroStorySequenceController.Instance.currentPhase == MetroStorySequenceController.StoryPhase.DoorWaiting)
        {
            ShowDoorPrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        ClearDoorPrompt();
    }
}
