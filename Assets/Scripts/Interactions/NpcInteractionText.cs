using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Cinemachine;
using AsAKid.Conversation;
using AsAKid.UI;

public class NpcInteractionText : MonoBehaviour
{
    [Header("UI & Settings")]
    public TextMeshProUGUI InteractText;
    public GameObject promptBox;
    public RectTransform promptBoxRect;
    public TextMeshProUGUI promptBoxText;
    public UnityEngine.UI.Button promptButton;
    public bool usePromptBox = true;
    public CenterInteractionReticle centerReticle;
    public float InteractionDistance = 5f;
    private bool CanInteract = true;

    public static NpcInteractionText Instance { get; private set; }

    // Cached current interaction targets
    private InteractivePlaceholderItem currentPlaceholder;
    private FindableItem currentFindable;
    private bool currentIsTeddy;
    private CarryableItem currentCarryable;
    private NpcConversation currentNpcConv;
    private NpcQuestionSet currentNpcQuestionSet;
    private NpcLookAt currentNpcLook;
    private Collider currentHitCol;
    private Vector3 currentHitPoint;
    private string currentPromptString = "";
    private IInteractable currentInteractable;
    private int lastDialogueFrame = -100;
    private bool promptOwnedInternally = false;
    private int lastExternalPromptFrame = -10;
    private System.Action externalPromptClickAction;

    [Header("Cameras & Cinemachine")]
    public Transform playerCamera;
    public CinemachineBrain cinemachineBrain;
    public CinemachineCamera playerVcam;
    public CinemachineCamera talkZoomVcam;

    [Header("Cinematic Dialogue Zoom")]
    public float dialogueZoomFOV = 40f;
    public float blendInDuration = 0.6f;
    public float blendOutDuration = 0.5f;
    public Vector3 aimOffset = new Vector3(0, 1.3f, 0);

    [Header("Player Control")]
    public MonoBehaviour playerController;

    private FirstPersonController _fpsController;
    private Rigidbody _rigidbody;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Keep existing singleton instance
        }
        else
        {
            Instance = this;
        }

        ResolveReferences();
        ClearPrompt();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (playerController == null)
            playerController = GetComponent<FirstPersonController>();

        _fpsController = playerController as FirstPersonController;
        if (_fpsController == null && playerController != null)
            _fpsController = playerController.GetComponent<FirstPersonController>();
        if (_fpsController == null)
            _fpsController = GetComponent<FirstPersonController>();

        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (playerCamera == null && _fpsController != null && _fpsController.playerCamera != null)
            playerCamera = _fpsController.playerCamera.transform;

        if (cinemachineBrain == null && playerCamera != null)
            cinemachineBrain = playerCamera.GetComponent<CinemachineBrain>();

        if (playerVcam == null)
        {
            var pv = GameObject.Find("PlayerVcam");
            if (pv != null) playerVcam = pv.GetComponent<CinemachineCamera>();
        }

        if (talkZoomVcam == null)
        {
            var tv = GameObject.Find("TalkZoomVcam");
            if (tv != null) talkZoomVcam = tv.GetComponent<CinemachineCamera>();
        }

        if (promptBox == null)
        {
            var pb = GameObject.Find("PromptBox");
            if (pb == null)
            {
                var canvas = GameObject.Find("Canvas");
                if (canvas != null)
                {
                    var t = canvas.transform.Find("PromptBox");
                    if (t != null) pb = t.gameObject;
                }
            }
            if (pb != null)
            {
                promptBox = pb;
                promptBoxRect = pb.GetComponent<RectTransform>();
                promptButton = pb.GetComponent<UnityEngine.UI.Button>();
            }
        }

        if (promptBox != null)
        {
            if (promptBoxRect == null)
                promptBoxRect = promptBox.GetComponent<RectTransform>();
            if (promptButton == null)
                promptButton = promptBox.GetComponent<UnityEngine.UI.Button>();
            if (promptBoxText == null)
                promptBoxText = promptBox.GetComponentInChildren<TextMeshProUGUI>(true);

            if (promptBoxText != null && !promptBoxText.gameObject.activeSelf)
            {
                promptBoxText.gameObject.SetActive(true);
            }
        }

        // Prevent InteractText from mistakenly pointing to PromptText / child of PromptBox
        if (InteractText != null && promptBox != null && (InteractText == promptBoxText || InteractText.transform.IsChildOf(promptBox.transform)))
        {
            InteractText = null;
        }

        if (InteractText == null)
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                var it = canvas.transform.Find("InteractText");
                if (it != null) InteractText = it.GetComponent<TextMeshProUGUI>();
            }
        }

        if (InteractText != null)
        {
            InteractText.gameObject.SetActive(false);
        }

        if (promptButton != null)
        {
            promptButton.onClick.RemoveListener(TriggerCurrentInteraction);
            promptButton.onClick.AddListener(TriggerCurrentInteraction);
        }

        if (centerReticle == null)
        {
            centerReticle = FindAnyObjectByType<CenterInteractionReticle>(FindObjectsInactive.Include);
        }

        if (!usePromptBox && promptBox != null)
        {
            promptBox.SetActive(false);
        }
    }

    public string GetCurrentPrompt() => currentPromptString;

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetPrompt(string text)
    {
        SetPromptInternal(text, isInternal: false, onClick: null);
    }

    public void SetPrompt(string text, System.Action onClick)
    {
        SetPromptInternal(text, isInternal: false, onClick: onClick);
    }

    private void SetInternalPrompt(string text)
    {
        SetPromptInternal(text, isInternal: true, onClick: null);
    }

    private void SetPromptInternal(string text, bool isInternal, System.Action onClick)
    {
        if ((NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning) ||
            ConversationUI.IsAnyActive)
        {
            ClearPrompt();
            return;
        }

        string adapted = Story.MobileUIAdapter.AdaptPrompt(text);
        currentPromptString = adapted ?? "";

        if (string.IsNullOrEmpty(text))
        {
            ClearPrompt();
            return;
        }

        promptOwnedInternally = isInternal;
        if (!isInternal)
        {
            lastExternalPromptFrame = Time.frameCount;
            externalPromptClickAction = onClick;
        }
        else
        {
            externalPromptClickAction = null;
        }

        // Remove all white text press e when interact
        if (InteractText != null && InteractText != promptBoxText && (promptBox == null || !InteractText.transform.IsChildOf(promptBox.transform)))
        {
            InteractText.text = "";
            InteractText.gameObject.SetActive(false);
        }

        if (centerReticle == null)
        {
            centerReticle = FindAnyObjectByType<CenterInteractionReticle>(FindObjectsInactive.Include);
        }
        if (centerReticle != null)
        {
            centerReticle.SetInteractable(true, TriggerCurrentInteraction);
        }

        if (!usePromptBox)
        {
            if (promptBox != null && promptBox.activeSelf)
            {
                promptBox.SetActive(false);
            }
            return;
        }

        if (promptBox != null)
        {
            promptBox.SetActive(true);
            if (promptBoxText == null)
            {
                promptBoxText = promptBox.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (promptBoxText != null)
            {
                promptBoxText.gameObject.SetActive(true);
                LocalizationManager.ApplyText(promptBoxText, currentPromptString, preserveAlignment: true);
            }
            if (promptBoxRect != null)
            {
                float preferred = promptBoxText != null ? promptBoxText.GetPreferredValues(promptBoxText.text).x : (currentPromptString.Length * 9.5f);
                float targetWidth = Mathf.Max(160f, preferred + 40f);
                promptBoxRect.sizeDelta = new Vector2(targetWidth, 44f);
            }
        }
    }

    public void ClearPrompt()
    {
        currentPromptString = "";
        currentFindable = null;
        currentPlaceholder = null;
        currentNpcConv = null;
        currentNpcQuestionSet = null;
        currentNpcLook = null;
        currentInteractable = null;
        promptOwnedInternally = false;
        lastExternalPromptFrame = -10;
        externalPromptClickAction = null;

        if (InteractText != null && InteractText != promptBoxText && (promptBox == null || !InteractText.transform.IsChildOf(promptBox.transform)))
        {
            InteractText.text = "";
            InteractText.gameObject.SetActive(false);
        }

        if (promptBox != null)
        {
            promptBox.SetActive(false);
        }

        if (centerReticle != null)
        {
            centerReticle.SetInteractable(false, null);
        }
    }

    private static bool IsInteractTriggeredThisFrame()
    {
        if (Input.GetKeyDown(KeyCode.E) || ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame)
            return true;

        if (!Application.isMobilePlatform && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            return true;

        return false;
    }

    public void TriggerCurrentInteraction()
    {
        if (currentInteractable != null)
        {
            var target = currentInteractable;
            currentInteractable = null;
            target.Interact();
            return;
        }
        if ((NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning) ||
            ConversationUI.IsAnyActive)
            return;

        if (currentFindable != null)
        {
            currentFindable.Collect();
            currentFindable = null;
            return;
        }

        if (currentPlaceholder != null)
        {
            currentPlaceholder.TriggerInteraction();
            return;
        }

        if (currentNpcQuestionSet != null)
        {
            if (currentNpcQuestionSet.AreAllAsked && !currentNpcQuestionSet.HasDoneLine())
                return;

            var convUI = ConversationUI.Instance ?? FindAnyObjectByType<ConversationUI>();
            if (convUI != null)
            {
                var targetNpc = currentNpcQuestionSet;
                var targetLook = currentNpcLook;
                ClearPrompt();
                convUI.StartConversation(targetNpc, targetLook);
            }
            else
            {
                Debug.LogWarning("[NpcInteractionText] No ConversationUI found in the scene. Drag Assets/Prefabs/UI/ConversationUI.prefab into your scene.");
            }
            return;
        }

        if (currentNpcConv != null)
        {
            if (!currentNpcConv.CanPlay)
            {
                ClearPrompt();
                return;
            }

            if (currentIsTeddy)
            {
                if (currentCarryable != null && !currentCarryable.IsBeingCarried)
                {
                    var cp = GameObject.Find("CarryPoint")?.transform;
                    if (cp != null) currentCarryable.StartCarrying(cp);
                }

                if (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.TryHandleTeddyInteraction())
                {
                    return;
                }
            }

            StartCoroutine(TalkSequence(currentNpcConv, currentNpcLook, currentHitCol, currentHitPoint));
            return;
        }

        if (MetroFuseBoxPuzzle.Instance != null && MetroFuseBoxPuzzle.Instance.IsPlayerNear)
        {
            MetroFuseBoxPuzzle.Instance.OpenInspection();
            return;
        }

        if (externalPromptClickAction != null)
        {
            externalPromptClickAction.Invoke();
            return;
        }
    }

    void Update()
    {
        // Keep the prompt box hidden whenever any dialogue or conversation UI is running
        if ((NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning) ||
            ConversationUI.IsAnyActive)
        {
            lastDialogueFrame = Time.frameCount;
            ClearPrompt();
            return;
        }

        if (Time.frameCount <= lastDialogueFrame + 2)
        {
            ClearPrompt();
            return;
        }

        if (!CanInteract)
        {
            ClearPrompt();
            return;
        }

        Transform camT = playerCamera != null ? playerCamera : (_fpsController != null && _fpsController.playerCamera != null ? _fpsController.playerCamera.transform : (Camera.main != null ? Camera.main.transform : transform));
        Vector3 rayOrigin = camT != null ? camT.position : transform.position;
        Vector3 rayDir = camT != null ? camT.forward : transform.forward;
        Ray ray = new Ray(rayOrigin, rayDir);

        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, InteractionDistance);
        if (!hasHit)
        {
            hasHit = Physics.SphereCast(ray, 0.4f, out hit, InteractionDistance);
        }

        if (hasHit)
        {
            var findable = hit.collider.GetComponent<FindableItem>() ?? hit.collider.GetComponentInParent<FindableItem>();
            if (findable != null && !findable.IsCollected)
            {
                currentFindable = findable;
                currentPlaceholder = null;
                currentNpcConv = null;
                currentNpcQuestionSet = null;
                currentInteractable = null;
                SetInternalPrompt(LocalizationManager.Resolve("prompt_tap_interact", "Tap to interact"));

                if (IsInteractTriggeredThisFrame())
                {
                    TriggerCurrentInteraction();
                }
                return;
            }

            var placeholder = hit.collider.GetComponent<InteractivePlaceholderItem>() ?? hit.collider.GetComponentInParent<InteractivePlaceholderItem>();
            if (placeholder != null && (!placeholder.oneTimeOnly || !placeholder.hasBeenInteracted))
            {
                currentPlaceholder = placeholder;
                currentNpcConv = null;
                currentNpcQuestionSet = null;
                currentInteractable = null;
                SetInternalPrompt(LocalizationManager.Resolve("prompt_tap_interact", "Tap to interact"));

                if (IsInteractTriggeredThisFrame())
                {
                    TriggerCurrentInteraction();
                }
                return;
            }

            var interactable = hit.collider.GetComponent<IInteractable>() ?? hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                string prompt = interactable.PromptMessage;
                if (!string.IsNullOrEmpty(prompt))
                {
                    currentInteractable = interactable;
                    currentPlaceholder = null;
                    currentFindable = null;
                    currentNpcConv = null;
                    currentNpcQuestionSet = null;
                    SetInternalPrompt(prompt);

                    if (IsInteractTriggeredThisFrame())
                    {
                        TriggerCurrentInteraction();
                    }
                    return;
                }
            }

            var questionSet = hit.collider.GetComponent<NpcQuestionSet>() ?? hit.collider.GetComponentInParent<NpcQuestionSet>();
            if (questionSet != null && questionSet.isActiveAndEnabled)
            {
                currentPlaceholder = null;
                currentFindable = null;
                currentInteractable = null;
                currentNpcConv = null;
                currentNpcQuestionSet = questionSet;
                currentNpcLook = hit.collider.GetComponent<NpcLookAt>() ?? hit.collider.GetComponentInParent<NpcLookAt>();
                currentHitCol = hit.collider;
                currentHitPoint = hit.point;

                bool canTalk = !questionSet.AreAllAsked || questionSet.HasDoneLine();
                string prompt = questionSet.AreAllAsked
                    ? LocalizationManager.Resolve("ui_conv_nothing_more", "Nothing more to say")
                    : LocalizationManager.Resolve("prompt_tap_talk", "Tap to talk");
                SetInternalPrompt(prompt);

                if (canTalk && IsInteractTriggeredThisFrame())
                {
                    TriggerCurrentInteraction();
                }
                return;
            }

            if (hit.collider.CompareTag("InteractNPC"))
            {
                var npcConv = hit.collider.GetComponent<NpcConversation>();
                bool isTeddyObj = hit.collider.gameObject.name.Contains("Teddy") || (MetroStorySequenceController.Instance != null && hit.collider.gameObject == MetroStorySequenceController.Instance.teddyBearObject);
                var carryableObj = isTeddyObj ? hit.collider.GetComponent<CarryableItem>() : null;
                bool isCarried = carryableObj != null && carryableObj.IsBeingCarried;

                if (npcConv != null && npcConv.CanPlay && !(isTeddyObj && isCarried))
                {
                    currentPlaceholder = null;
                    currentHitCol = hit.collider;
                    currentHitPoint = hit.point;
                    currentIsTeddy = isTeddyObj;
                    currentCarryable = carryableObj;
                    currentNpcConv = npcConv;
                    currentNpcLook = hit.collider.GetComponent<NpcLookAt>();

                    string prompt = (currentIsTeddy && !isCarried) ? LocalizationManager.Resolve("prompt_tap_interact", "Tap to interact") : LocalizationManager.Resolve("prompt_tap_talk", "Tap to talk");
                    SetInternalPrompt(prompt);

                    if (IsInteractTriggeredThisFrame())
                    {
                        TriggerCurrentInteraction();
                    }
                    return;
                }
            }
        }

        // Proximity check for Metro Fusebox
        if (MetroFuseBoxPuzzle.Instance != null && MetroFuseBoxPuzzle.Instance.IsPlayerNear)
        {
            currentPlaceholder = null;
            currentNpcConv = null;
            currentNpcQuestionSet = null;
            currentInteractable = null;
            string prompt = MetroFuseBoxPuzzle.Instance.isPowerRestored
                ? MetroFuseBoxPuzzle.Instance.promptPoweredText
                : MetroFuseBoxPuzzle.Instance.promptOpenText;
            SetInternalPrompt(prompt);

            if (Input.GetKeyDown(KeyCode.E) || ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame)
            {
                TriggerCurrentInteraction();
            }
            return;
        }

        currentPlaceholder = null;
        currentNpcConv = null;
        currentNpcQuestionSet = null;
        currentInteractable = null;
        if (promptOwnedInternally || Time.frameCount > lastExternalPromptFrame + 1)
        {
            ClearPrompt();
        }
    }

    private Vector3 GetTargetLookPosition(NpcConversation conv, Collider hitCol, Vector3 hitPoint)
    {
        Transform npc = conv.transform;

        // 1. Recursive check for dedicated look target or head bones across all descendants
        var allDescendants = npc.GetComponentsInChildren<Transform>(true);
        foreach (var t in allDescendants)
        {
            string lower = t.name.ToLower();
            if (lower == "looktarget" || lower == "beggarhead" || lower == "head" || 
                lower == "mixamorig:head" || lower.EndsWith(":head") || lower.EndsWith("_head"))
            {
                return t.position;
            }
        }

        // 2. Check for humanoid animator head bone
        var anim = npc.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
                return headBone.position;
        }

        // 3. If there is an animator with named head/face bones in generic rig
        foreach (var t in allDescendants)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("head") || lower.Contains("face"))
                return t.position;
        }

        // 4. For props, items, dolls, or objects (NO Animator, like Teddy Bear):
        // If it's a character or NPC, aim near the top (82% height) for eye level
        var rend = hitCol != null ? hitCol.GetComponent<Renderer>() : null;
        if (rend == null) rend = npc.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds b = rend.bounds;
            if (b.size.y > 1.0f)
            {
                return new Vector3(b.center.x, b.min.y + b.size.y * 0.82f, b.center.z);
            }
            return b.center;
        }

        // 5. Use raycast hit point if available
        if (hitPoint != Vector3.zero)
            return hitPoint;

        // 6. Fallback to collider bounds
        Collider col = hitCol != null ? hitCol : npc.GetComponent<Collider>();
        if (col == null) col = npc.GetComponentInChildren<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            if (b.size.y > 1.0f)
            {
                return new Vector3(b.center.x, b.min.y + b.size.y * 0.82f, b.center.z);
            }
            return b.center;
        }

        return npc.position + Vector3.up * 1.5f;
    }

    IEnumerator TalkSequence(NpcConversation conv, NpcLookAt look, Collider hitCollider = null, Vector3 hitPoint = default)
    {
        CanInteract = false;
        ClearPrompt();

        ResolveReferences();

        // 1. Immediately halt movement and lock player input
        if (_fpsController != null)
        {
            _fpsController.SetControlLocked(true);
        }
        else if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (look != null)
            look.IKActive = true;

        // 2. Find accurate target look position (face height or object center)
        Vector3 targetLookPos = GetTargetLookPosition(conv, hitCollider, hitPoint);

        // Keep cursor locked and invisible throughout dialogue start
        if (!Application.isMobilePlatform) Cursor.lockState = CursorLockMode.Locked;
        if (!Application.isMobilePlatform) Cursor.visible = false;

        // Teddy bear interaction: NO camera zoom or pitch snap to ground
        bool isTeddyConv = currentIsTeddy || (conv != null && conv.gameObject.name.IndexOf("Teddy", System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (isTeddyConv)
        {
            yield return StartCoroutine(conv.Play());

            if (look != null) look.IKActive = false;
            if (!Application.isMobilePlatform) Cursor.lockState = CursorLockMode.Locked;
            if (!Application.isMobilePlatform) Cursor.visible = false;

            if (_fpsController != null)
            {
                _fpsController.SetControlLocked(false);
            }
            else if (playerController != null)
            {
                playerController.enabled = true;
            }

            CanInteract = true;
            yield break;
        }
        // 3. Direct smooth camera zoom and look rotation
        Camera cam = playerCamera != null ? playerCamera.GetComponent<Camera>() : null;
        float startFov = cam != null ? cam.fieldOfView : (_fpsController != null ? _fpsController.fov : 60f);
        float targetFov = dialogueZoomFOV > 0f ? dialogueZoomFOV : 40f;

        // Ensure CinemachineBrain is disabled so it doesn't fight our camera orientation or displace localPosition
        if (cinemachineBrain != null)
            cinemachineBrain.enabled = false;

        if (playerCamera != null)
        {
            playerCamera.localPosition = Vector3.zero;

            Vector3 camPos = playerCamera.position;
            Vector3 lookDirection = targetLookPos - camPos;
            if (lookDirection.sqrMagnitude < 0.001f)
                lookDirection = playerCamera.forward;
            lookDirection.Normalize();

            // Calculate target body yaw
            float targetYaw = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;

            // Calculate target pitch (camera pitch: looking down is positive, looking up is negative)
            float targetPitch = -Mathf.Asin(Mathf.Clamp(lookDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (_fpsController != null)
                targetPitch = Mathf.Clamp(targetPitch, -_fpsController.maxLookAngle, _fpsController.maxLookAngle);

            float startYaw = transform.eulerAngles.y;
            float startPitch = 0f;
            if (_fpsController != null)
            {
                startPitch = _fpsController.Pitch;
            }
            else
            {
                startPitch = playerCamera.localEulerAngles.x;
                if (startPitch > 180f) startPitch -= 360f;
            }

            // Also mirror to TalkZoomVcam if present in the scene
            if (talkZoomVcam != null)
            {
                talkZoomVcam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(lookDirection));
                talkZoomVcam.Lens.FieldOfView = targetFov;
            }

            float t = 0f;
            while (t < blendInDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / blendInDuration));

                float currentYaw = Mathf.LerpAngle(startYaw, targetYaw, s);
                float currentPitch = Mathf.Lerp(startPitch, targetPitch, s);
                float currentFov = Mathf.Lerp(startFov, targetFov, s);

                if (_fpsController != null)
                {
                    _fpsController.SetViewAngles(currentYaw, currentPitch);
                }
                else
                {
                    transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
                    playerCamera.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
                    playerCamera.localPosition = Vector3.zero;
                }

                if (cam != null)
                    cam.fieldOfView = currentFov;

                yield return null;
            }

            if (_fpsController != null)
            {
                _fpsController.SetViewAngles(targetYaw, targetPitch);
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
                playerCamera.localRotation = Quaternion.Euler(targetPitch, 0f, 0f);
                playerCamera.localPosition = Vector3.zero;
            }
            if (cam != null)
                cam.fieldOfView = targetFov;
        }

        // 4. Play conversation
        yield return StartCoroutine(conv.Play());

        // 5. Smoothly zoom FOV back out
        if (playerCamera != null && cam != null)
        {
            float t2 = 0f;
            while (t2 < blendOutDuration)
            {
                t2 += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t2 / blendOutDuration));
                cam.fieldOfView = Mathf.Lerp(targetFov, startFov, s);
                playerCamera.localPosition = Vector3.zero;
                yield return null;
            }
            cam.fieldOfView = startFov;
            playerCamera.localPosition = Vector3.zero;
        }

        // 6. Disable NPC look-at
        if (look != null)
            look.IKActive = false;

        // 7. Ensure cursor stays locked
        if (!Application.isMobilePlatform) Cursor.lockState = CursorLockMode.Locked;
        if (!Application.isMobilePlatform) Cursor.visible = false;

        // 8. Re-enable player controls smoothly
        if (_fpsController != null && playerCamera != null)
        {
            playerCamera.localPosition = Vector3.zero;
            _fpsController.SnapViewRotation(playerCamera.rotation);
            _fpsController.SetControlLocked(false);
        }
        else if (playerController != null)
        {
            playerController.enabled = true;
        }

        CanInteract = true;
    }
}
