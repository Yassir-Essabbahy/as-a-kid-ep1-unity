using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EndingVariant { LeaveTeddy, SendTeddy }
public enum TeddyEndingState { Idle, WaitingForPickup, Carrying, Sending, Done }

/// <summary>
/// Coordinates the ending cutscenes in Moroccan Beach:
/// 1. Raft Ready: When the final piece (cloth) is placed, Teddy calls out to the child.
/// 2. Choice: The player walks up to the raft and interacts, presenting a choice:
///    Option 1: "Leave him and go" -> Existing ending where player boards and departs.
///    Option 2: "Put Teddy on the boat and send him away" -> New flow:
///      a. Choice closes, player is free to move. Teddy stays on table.
///      b. Player walks up to Teddy; in range shows "Press E to take Teddy".
///      c. Player presses E: picks up Teddy (carried).
///      d. Place spot on boat active only while Teddy is carried: "Press E to put Teddy on the boat".
///      e. Player presses E: Teddy lerps smoothly onto boat, boat drifts away with gentle wave bob,
///         camera lingers, farewell dialogue line plays, fade out, loads ending scene.
/// </summary>
public class BeachEndingController : MonoBehaviour
{
    public static BeachEndingController Instance { get; private set; }
    public static EndingVariant ChosenEnding { get; private set; } = EndingVariant.LeaveTeddy;
    public static TeddyEndingState CurrentTeddyState => Instance != null ? Instance.teddyState : TeddyEndingState.Idle;

    [Header("Dependencies")]
    public RaftBuildArea raftBuildArea;
    public Transform assembledRaftVisuals;
    public FirstPersonController playerController;
    public Transform teddyTransform;
    public Camera stationaryToyCamera;
    public AudioSource oceanAudioSource;

    [Header("UI Elements")]
    public GameObject raftTrackerPanel;
    public GameObject promptBox;
    public GameObject mobileInteractButton;

    [Header("Raft Ready & Dialogue")]
    public string boardRaftDialogueKey = "beach_ending_ready";
    public Color dialogueColor = Color.white;
    public TMP_FontAsset dialogueFont;
    public bool CanBoardRaft { get; private set; } = false;

    [Header("Ending Choice Localization")]
    [SerializeField] private string choicePromptKey = "beach_choice_prompt";
    [SerializeField] private string leaveChoiceKey = "beach_choice_leave";
    [SerializeField] private string sendChoiceKey = "beach_choice_send";

    [Header("Teddy Boat Ending Settings")]
    [SerializeField] private float teddyPickupDistance = 3.5f;
    [SerializeField] private float teddyPlaceDistance = 4.5f;
    [SerializeField] private float teddyPlacementLerpDuration = 0.8f;
    [SerializeField] private float teddyDriftDistance = 14f;
    [SerializeField] private Vector3 teddyBoatOffset = new Vector3(0f, 0.38f, 0.13f);
    [SerializeField] private Vector3 teddyBoatRotationEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] private AudioClip teddyPlaceSound;

    [Header("Teddy Prompts & Localization")]
    [SerializeField] private string takeTeddyPromptKey = "beach_teddy_take";
    [SerializeField] private string takeTeddyPromptFallback = "Press E to take Teddy";
    [SerializeField] private string putTeddyPromptKey = "beach_teddy_put";
    [SerializeField] private string putTeddyPromptFallback = "Press E to put Teddy on the boat";
    [SerializeField] private string teddyFarewellKey = "beach_teddy_farewell";

    [Header("Raft Departure Settings")]
    [Tooltip("Movement speed drifting towards the open sea")]
    public float raftDriftSpeed = 1.7f;
    [Tooltip("Total duration the raft sails out into the ocean")]
    public float raftDriftDuration = 7.0f;
    [Tooltip("Direction the raft drifts towards the ocean")]
    public Vector3 driftDirection = Vector3.left;
    [Tooltip("Wave bobbing frequency (cycles per second)")]
    public float waveFrequency = 2.0f;
    [Tooltip("Wave bobbing vertical amplitude (meters)")]
    public float waveAmplitude = 0.06f;

    [Tooltip("World offset for player relative to assembledRaftVisuals.position")]
    public Vector3 playerRaftWorldOffset = new Vector3(-0.6f, 1.25f, -0.6f);

    [Tooltip("Time taken for player camera on the boat to smoothly rotate and face Teddy")]
    public float rotateToTeddyDuration = 2.0f;
    public bool trackTeddyDuringDrift = true;
    public bool usePlayerCameraDrift = true;

    [Header("Audio & Transition Settings")]
    public float audioMuffleDuration = 5.0f;
    public string targetSceneName = "Ending";
    public float fadeToBlackDuration = 2.0f;

    [Header("Status")]
    public bool isEndingActive = false;
    public string currentPhase = "Idle";

    // Internal State
    private bool choosingEnding;
    private TeddyEndingState teddyState = TeddyEndingState.Idle;
    private RaftPlayerInteraction playerInteraction;
    private CarryableItem teddyCarry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        ChosenEnding = EndingVariant.LeaveTeddy;
    }

    private void Awake()
    {
        Instance = this;
        ResolveReferences();

        if (stationaryToyCamera != null)
        {
            stationaryToyCamera.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (raftBuildArea != null)
        {
            raftBuildArea.OnRaftCompleted.RemoveListener(OnRaftCompleted);
            raftBuildArea.OnRaftCompleted.AddListener(OnRaftCompleted);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (raftBuildArea != null)
        {
            raftBuildArea.OnRaftCompleted.RemoveListener(OnRaftCompleted);
        }
    }

    public void ResolveReferences()
    {
        if (raftBuildArea == null)
            raftBuildArea = FindAnyObjectByType<RaftBuildArea>();

        if (assembledRaftVisuals == null)
        {
            var r = GameObject.Find("AssembledRaftVisuals");
            if (r != null) assembledRaftVisuals = r.transform;
        }

        if (playerController == null)
            playerController = FindAnyObjectByType<FirstPersonController>();

        if (playerInteraction == null && playerController != null)
            playerInteraction = playerController.GetComponent<RaftPlayerInteraction>();
        if (playerInteraction == null)
            playerInteraction = FindAnyObjectByType<RaftPlayerInteraction>();

        if (teddyTransform == null)
        {
            var t = GameObject.Find("Teddy");
            if (t != null) teddyTransform = t.transform;
        }

        if (oceanAudioSource == null)
        {
            var oa = GameObject.Find("Ocean_Waves_Audio");
            if (oa != null) oceanAudioSource = oa.GetComponent<AudioSource>();
        }

        if (raftTrackerPanel == null)
        {
            var p = GameObject.Find("RaftTrackerPanel");
            if (p != null) raftTrackerPanel = p;
        }

        if (promptBox == null)
        {
            var pb = GameObject.Find("PromptBox");
            if (pb != null) promptBox = pb;
        }

        if (stationaryToyCamera == null)
        {
            var tc = GameObject.Find("ToyShotCamera");
            if (tc != null) stationaryToyCamera = tc.GetComponent<Camera>();
        }

        if (mobileInteractButton == null)
        {
            var mb = GameObject.Find("MobileInteractButton");
            if (mb != null) mobileInteractButton = mb;
        }
    }

    /// <summary>
    /// Invoked when all 4 raft pieces have been placed.
    /// </summary>
    public void OnRaftCompleted()
    {
        if (currentPhase != "Idle") return;
        Debug.Log("[BeachEndingController] Final piece placed! Starting RaftReadyRoutine.");
        StartCoroutine(RaftReadyRoutine());
    }

    private IEnumerator RaftReadyRoutine()
    {
        currentPhase = "RaftReadyWaitingForPlayer";

        string line = null;
        if (LocalizationManager.Instance != null)
        {
            line = LocalizationManager.Instance.Get(boardRaftDialogueKey);
        }
        if (string.IsNullOrEmpty(line) || line == boardRaftDialogueKey)
        {
            line = "Teddy: The boat is ready! Come on, get on the raft!";
        }

        string teddyName = MetroStorySequenceController.TeddyName;
        if (!string.IsNullOrEmpty(teddyName))
        {
            line = line.Replace("{TEDDY_NAME}", teddyName).Replace("[TEDDY NAME]", teddyName);
        }
        line = line.Replace("{CHILD_NAME}", MetroStorySequenceController.ChildName);

        var diag = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();
        if (diag != null && Application.isPlaying)
        {
            diag.StartCoroutine(diag.ShowDialogue(
                new string[] { line },
                hasChoice: false,
                dialogueColor: dialogueColor,
                dialogueFont: dialogueFont
            ));
        }

        CanBoardRaft = true;

        if (raftTrackerPanel != null)
        {
            var tmp = raftTrackerPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "Boat: Ready!\nPress [E] to board";
            }
        }

        yield return null;
    }

    /// <summary>
    /// Called when the player presses interact near the completed raft.
    /// </summary>
    public void StartRaftDeparture()
    {
        if (isEndingActive || choosingEnding || !CanBoardRaft) return;
        StartCoroutine(ChooseEndingRoutine());
    }

    private IEnumerator ChooseEndingRoutine()
    {
        choosingEnding = true;
        CanBoardRaft = false;

        var diag = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();
        if (diag == null || diag.choicePack == null || diag.dialogueText == null)
        {
            Debug.LogWarning("[BeachEndingController] Choice UI unavailable; running leave ending.");
            choosingEnding = false;
            BeginLeaveEnding();
            yield break;
        }

        while (diag.IsDialogueRunning) yield return null;

        if (playerController != null) playerController.SetControlLocked(true);

        var loc = LocalizationManager.Instance;
        string promptStr = loc != null ? loc.Get(choicePromptKey) : "What should I do?";
        string leaveStr = loc != null ? loc.Get(leaveChoiceKey) : "Leave him and go";
        string sendStr = loc != null ? loc.Get(sendChoiceKey) : "Put Teddy on the boat and send him away";

        diag.SetChoiceLabels(leaveStr, sendStr);
        yield return diag.ShowDialogue(new[] { promptStr }, true, dialogueColor, dialogueFont);

        int choice = diag.lastChoiceIndex;
        choosingEnding = false;

        if (choice == 1)
        {
            ChosenEnding = EndingVariant.SendTeddy;
            StartWaitingForTeddyPickup();
        }
        else
        {
            BeginLeaveEnding();
        }
    }

    private void BeginLeaveEnding()
    {
        ChosenEnding = EndingVariant.LeaveTeddy;
        CanBoardRaft = false;
        isEndingActive = true;
        StartCoroutine(EndingSequenceRoutine());
    }

    public void StartWaitingForTeddyPickup()
    {
        if (teddyState != TeddyEndingState.Idle) return;
        ResolveReferences();

        CanBoardRaft = false;

        if (playerController != null)
        {
            playerController.SetControlLocked(false);
        }

        if (playerInteraction != null)
        {
            playerInteraction.enabled = false;
        }

        teddyCarry = teddyTransform != null ? teddyTransform.GetComponent<CarryableItem>() : null;
        if (teddyCarry == null && teddyTransform != null)
        {
            teddyCarry = teddyTransform.gameObject.AddComponent<CarryableItem>();
        }
        if (teddyCarry != null && teddyTransform != null)
        {
            teddyCarry.SetSafeResetPosition(teddyTransform.position);
        }

        teddyState = TeddyEndingState.WaitingForPickup;
        currentPhase = "WaitingForPickup";

        if (raftTrackerPanel != null)
        {
            var tmp = raftTrackerPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "Objective:\nTake Teddy from the table";
            }
        }
    }

    private void Update()
    {
        if (teddyState == TeddyEndingState.WaitingForPickup)
        {
            HandleWaitingForPickupState();
        }
        else if (teddyState == TeddyEndingState.Carrying)
        {
            HandleCarryingState();
        }
    }

    private void HandleWaitingForPickupState()
    {
        if (playerController == null || teddyTransform == null || playerInteraction == null) return;
        var input = playerInteraction.interactInput;
        if (input == null) return;

        float distToTeddy = Vector3.Distance(playerController.transform.position, teddyTransform.position);
        if (distToTeddy <= teddyPickupDistance)
        {
            string promptText = GetTakeTeddyPrompt();
            SetPrompt(promptText);
            input.SetInteractionAvailable(true, promptText);

            if (input.WasPressed)
            {
                input.ConsumePress();

                Transform holdPoint = playerInteraction.carryPoint;
                if (holdPoint == null && playerController.playerCamera != null)
                {
                    holdPoint = playerController.playerCamera.transform;
                }

                if (teddyCarry != null)
                {
                    teddyCarry.StartCarrying(holdPoint);
                }

                teddyState = TeddyEndingState.Carrying;
                currentPhase = "CarryingTeddy";

                ClearPrompt();
                input.SetInteractionAvailable(false);

                if (raftTrackerPanel != null)
                {
                    var tmp = raftTrackerPanel.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = "Objective:\nBring Teddy to the raft";
                    }
                }
            }
        }
        else
        {
            ClearPrompt();
            input.SetInteractionAvailable(false);
        }
    }

    private void HandleCarryingState()
    {
        if (playerInteraction == null || playerController == null || teddyTransform == null) return;
        var input = playerInteraction.interactInput;
        if (input == null) return;

        // If player dropped Teddy (e.g. Q key), allow picking him back up
        if (teddyCarry != null && !teddyCarry.IsBeingCarried)
        {
            float distToTeddy = Vector3.Distance(playerController.transform.position, teddyTransform.position);
            if (distToTeddy <= teddyPickupDistance)
            {
                string promptText = GetTakeTeddyPrompt();
                SetPrompt(promptText);
                input.SetInteractionAvailable(true, promptText);

                if (input.WasPressed)
                {
                    input.ConsumePress();
                    Transform holdPoint = playerInteraction.carryPoint ?? playerController.playerCamera?.transform;
                    teddyCarry.StartCarrying(holdPoint);
                    ClearPrompt();
                    input.SetInteractionAvailable(false);
                }
            }
            else
            {
                ClearPrompt();
                input.SetInteractionAvailable(false);
            }
            return;
        }

        // Teddy is carried: allow dropping with Q key
        if (Input.GetKeyDown(playerInteraction.dropKey))
        {
            Vector3 dropPos = playerController.transform.position + playerController.transform.forward * 0.8f;
            teddyCarry.Drop(dropPos, playerController.transform.forward);
            ClearPrompt();
            input.SetInteractionAvailable(false);
            return;
        }

        // Check range to boat place spot
        Vector3 boatCenter = raftBuildArea != null ? raftBuildArea.transform.position : assembledRaftVisuals.position;
        float distToBoat = Vector3.Distance(playerController.transform.position, boatCenter);

        if (distToBoat <= teddyPlaceDistance)
        {
            string promptText = GetPutTeddyPrompt();
            SetPrompt(promptText);
            input.SetInteractionAvailable(true, promptText);

            if (input.WasPressed)
            {
                input.ConsumePress();
                ClearPrompt();
                input.SetInteractionAvailable(false);

                teddyState = TeddyEndingState.Sending;
                currentPhase = "PlacingTeddy";
                StartCoroutine(PlaceAndSendTeddyRoutine());
            }
        }
        else
        {
            // Away from boat: pressing E does nothing (Teddy stays in hand)
            if (input.WasPressed)
            {
                input.ConsumePress();
            }

            ClearPrompt();
            input.SetInteractionAvailable(false);
        }
    }

    private string GetTakeTeddyPrompt()
    {
        string text = null;
        if (LocalizationManager.Instance != null)
        {
            text = LocalizationManager.Instance.Get(takeTeddyPromptKey);
        }
        if (string.IsNullOrEmpty(text) || text == takeTeddyPromptKey)
        {
            text = takeTeddyPromptFallback;
        }
        return text;
    }

    private string GetPutTeddyPrompt()
    {
        string text = null;
        if (LocalizationManager.Instance != null)
        {
            text = LocalizationManager.Instance.Get(putTeddyPromptKey);
        }
        if (string.IsNullOrEmpty(text) || text == putTeddyPromptKey)
        {
            text = putTeddyPromptFallback;
        }
        return text;
    }

    private void SetPrompt(string text)
    {
        if (playerInteraction != null && playerInteraction.promptText != null)
        {
            playerInteraction.promptText.text = text;
        }
        if (promptBox != null && !promptBox.activeSelf)
        {
            promptBox.SetActive(true);
        }
    }

    private void ClearPrompt()
    {
        if (playerInteraction != null && playerInteraction.promptText != null)
        {
            playerInteraction.promptText.text = "";
        }
        if (promptBox != null && promptBox.activeSelf)
        {
            promptBox.SetActive(false);
        }
    }

    private IEnumerator PlaceAndSendTeddyRoutine()
    {
        isEndingActive = true;

        if (raftTrackerPanel != null) raftTrackerPanel.SetActive(false);
        if (promptBox != null) promptBox.SetActive(false);

        if (playerController != null)
        {
            playerController.SetControlLocked(true);
        }

        // Release Teddy from carrying and remove physics/colliders so he does not detach or interfere
        if (teddyCarry != null)
        {
            teddyCarry.StopCarrying();
            teddyCarry.enabled = false;
            Destroy(teddyCarry);
        }

        var rb = teddyTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Destroy(rb);
        }

        var colliders = teddyTransform.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col != null) Destroy(col);
        }

        // Determine seat position on raft deck (surface of logs at Y ≈ 0.33)
        Vector3 effectiveOffset = teddyBoatOffset;
        if (effectiveOffset == new Vector3(0f, 1f, 0f) || effectiveOffset == new Vector3(0f, 0f, 0.25f) || effectiveOffset == Vector3.zero)
        {
            effectiveOffset = new Vector3(0f, 0.38f, 0.13f);
        }

        Vector3 startPos = teddyTransform.position;
        Quaternion startRot = teddyTransform.rotation;
        Vector3 targetPos = assembledRaftVisuals.TransformPoint(effectiveOffset);

        // Calculate rotation so Teddy sits upright facing the player on the shore
        Vector3 playerPos = playerController != null ? playerController.transform.position : Vector3.zero;
        Vector3 toPlayer = playerPos - targetPos;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f)
        {
            toPlayer = Vector3.right;
        }
        else
        {
            toPlayer.Normalize();
        }

        // Teddy model is Blender Z-up: local +Z is head (up), local +Y is front (facing player)
        Quaternion targetRot = Quaternion.LookRotation(toPlayer, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);

        // Lerp Teddy smoothly onto boat deck
        float lerpElapsed = 0f;
        while (lerpElapsed < teddyPlacementLerpDuration)
        {
            lerpElapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lerpElapsed / Mathf.Max(0.01f, teddyPlacementLerpDuration)));
            teddyTransform.position = Vector3.Lerp(startPos, targetPos, t);
            teddyTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        teddyTransform.position = targetPos;
        teddyTransform.rotation = targetRot;
        teddyTransform.SetParent(assembledRaftVisuals, true);

        // Record Teddy's local transform relative to assembledRaftVisuals to lock him firmly onto the boat
        Vector3 localPosOnBoat = teddyTransform.localPosition;
        Quaternion localRotOnBoat = teddyTransform.localRotation;

        if (teddyPlaceSound != null)
        {
            AudioSource.PlayClipAtPoint(teddyPlaceSound, teddyTransform.position);
        }

        // Begin Sending phase: Raft drifts out over water with wave bobbing
        teddyState = TeddyEndingState.Sending;
        currentPhase = "SendingTeddy";

        AudioLowPassFilter lpf = null;
        if (oceanAudioSource != null)
        {
            lpf = oceanAudioSource.GetComponent<AudioLowPassFilter>();
            if (lpf == null) lpf = oceanAudioSource.gameObject.AddComponent<AudioLowPassFilter>();
            lpf.enabled = true;
            lpf.cutoffFrequency = 22000f;
        }
        float startOceanVol = oceanAudioSource != null ? oceanAudioSource.volume : 0.05f;

        Vector3 raftBasePos = assembledRaftVisuals.position;
        Quaternion raftBaseRot = assembledRaftVisuals.rotation;
        float travel = 0f;
        float totalDriftTime = teddyDriftDistance / Mathf.Max(0.5f, raftDriftSpeed);
        float driftElapsed = 0f;

        bool dialogueTriggered = false;
        bool transitionTriggered = false;

        while (driftElapsed < totalDriftTime)
        {
            driftElapsed += Time.deltaTime;
            travel += Mathf.Max(0.01f, raftDriftSpeed) * Time.deltaTime;

            float waveY = Mathf.Sin(Time.time * waveFrequency) * waveAmplitude;
            float rollZ = Mathf.Sin(Time.time * (waveFrequency * 0.8f)) * 1.2f;
            float pitchX = Mathf.Cos(Time.time * (waveFrequency * 0.7f)) * 0.8f;

            assembledRaftVisuals.position = raftBasePos + driftDirection.normalized * Mathf.Min(travel, teddyDriftDistance) + new Vector3(0f, waveY, 0f);
            assembledRaftVisuals.rotation = raftBaseRot * Quaternion.Euler(pitchX, 0f, rollZ);

            // Keep Teddy firmly seated and moving with the boat
            if (teddyTransform != null)
            {
                teddyTransform.localPosition = localPosOnBoat;
                teddyTransform.localRotation = localRotOnBoat;
            }

            // Camera smoothly tracks the drifting raft and Teddy
            if (playerController != null && playerController.playerCamera != null && teddyTransform != null)
            {
                Vector3 toTeddy = (teddyTransform.position - playerController.playerCamera.transform.position).normalized;
                if (toTeddy.sqrMagnitude > 0.001f)
                {
                    Quaternion targetLook = Quaternion.LookRotation(toTeddy);
                    playerController.playerCamera.transform.rotation = Quaternion.Slerp(playerController.playerCamera.transform.rotation, targetLook, Time.deltaTime * 2.5f);
                    playerController.SnapViewRotation(playerController.playerCamera.transform.rotation);
                }
            }

            // Localized farewell dialogue line
            if (!dialogueTriggered && driftElapsed >= 1.5f)
            {
                dialogueTriggered = true;
                string line = null;
                if (LocalizationManager.Instance != null)
                {
                    line = LocalizationManager.Instance.Get(teddyFarewellKey);
                }
                if (string.IsNullOrEmpty(line) || line == teddyFarewellKey)
                {
                    line = "Child: Goodbye, Teddy.";
                }

                var diag = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();
                if (diag != null)
                {
                    diag.StartCoroutine(diag.ShowDialogue(new[] { line }, false, dialogueColor, dialogueFont));
                }
            }

            // Audio muffling
            float muffleT = Mathf.Clamp01(driftElapsed / Mathf.Max(0.01f, audioMuffleDuration));
            float smoothMuffle = Mathf.SmoothStep(0f, 1f, muffleT);
            if (lpf != null) lpf.cutoffFrequency = Mathf.Lerp(22000f, 350f, smoothMuffle);
            if (oceanAudioSource != null) oceanAudioSource.volume = Mathf.Lerp(startOceanVol, 0f, smoothMuffle);

            // Screen fade to black & transition
            if (!transitionTriggered && driftElapsed >= (totalDriftTime - fadeToBlackDuration))
            {
                transitionTriggered = true;
                currentPhase = "TransitionToClassroom";
                ClassroomEndingController.IsEndingActive = true;
                ScreenFader.TransitionToScene(targetSceneName, fadeToBlackDuration);
            }

            yield return null;
        }

        if (oceanAudioSource != null) oceanAudioSource.volume = 0f;
        teddyState = TeddyEndingState.Done;
        ChosenEnding = EndingVariant.SendTeddy;
        ClassroomEndingController.IsEndingActive = true;
    }

    private IEnumerator EndingSequenceRoutine()
    {
        currentPhase = "RaftDeparture";

        // 1. Lock player movement and dismiss interaction UI
        if (playerController != null)
        {
            playerController.SetControlLocked(true);
        }

        if (raftTrackerPanel != null)
        {
            raftTrackerPanel.SetActive(false);
        }

        if (promptBox != null)
        {
            promptBox.SetActive(false);
        }

        var interaction = FindAnyObjectByType<RaftPlayerInteraction>();
        if (interaction != null)
        {
            interaction.enabled = false;
        }

        // 2. Step player smoothly onto the raft deck
        if (playerController != null && assembledRaftVisuals != null)
        {
            var cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Vector3 startPos = playerController.transform.position;
            Vector3 targetDeckPos = assembledRaftVisuals.position + playerRaftWorldOffset;

            float stepElapsed = 0f;
            float stepDuration = 1.0f;

            while (stepElapsed < stepDuration)
            {
                stepElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(stepElapsed / stepDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                playerController.transform.position = Vector3.Lerp(startPos, targetDeckPos, smoothT);
                yield return null;
            }

            playerController.transform.position = targetDeckPos;
        }

        // 3. Resolve Teddy transform
        if (teddyTransform == null)
        {
            var t = GameObject.Find("Teddy");
            if (t != null) teddyTransform = t.transform;
        }

        // 4. Setup AudioLowPassFilter for progressive ocean muffling
        AudioLowPassFilter lpf = null;
        if (oceanAudioSource != null)
        {
            lpf = oceanAudioSource.GetComponent<AudioLowPassFilter>();
            if (lpf == null) lpf = oceanAudioSource.gameObject.AddComponent<AudioLowPassFilter>();
            lpf.enabled = true;
            lpf.cutoffFrequency = 22000f;
        }
        float startOceanVol = oceanAudioSource != null ? oceanAudioSource.volume : 0.05f;

        // 5. Raft Drifts Out to Sea & Camera Rotates to Teddy
        if (assembledRaftVisuals != null)
        {
            Vector3 raftBasePos = assembledRaftVisuals.position;
            Quaternion raftBaseRot = assembledRaftVisuals.rotation;
            float driftElapsed = 0f;

            float startYaw = playerController != null ? playerController.transform.eulerAngles.y : 0f;
            float startPitch = 0f;
            if (playerController != null && playerController.playerCamera != null)
            {
                startPitch = playerController.playerCamera.transform.localEulerAngles.x;
                if (startPitch > 180f) startPitch -= 360f;
            }

            bool transitionTriggered = false;

            while (driftElapsed < raftDriftDuration)
            {
                driftElapsed += Time.deltaTime;
                Vector3 prevRaftPos = assembledRaftVisuals.position;

                raftBasePos += driftDirection.normalized * (raftDriftSpeed * Time.deltaTime);

                float waveY = Mathf.Sin(Time.time * waveFrequency) * waveAmplitude;
                float rollZ = Mathf.Sin(Time.time * (waveFrequency * 0.8f)) * 1.2f;
                float pitchX = Mathf.Cos(Time.time * (waveFrequency * 0.7f)) * 0.8f;

                assembledRaftVisuals.position = raftBasePos + new Vector3(0f, waveY, 0f);
                assembledRaftVisuals.rotation = raftBaseRot * Quaternion.Euler(pitchX, 0f, rollZ);

                if (playerController != null)
                {
                    Vector3 raftDelta = assembledRaftVisuals.position - prevRaftPos;
                    playerController.transform.position += raftDelta;

                    if (usePlayerCameraDrift && teddyTransform != null)
                    {
                        Vector3 camPos = playerController.playerCamera != null
                            ? playerController.playerCamera.transform.position
                            : playerController.transform.position + Vector3.up * 0.8f;

                        Vector3 toTeddy = (teddyTransform.position - camPos).normalized;
                        Quaternion targetLookRot = Quaternion.LookRotation(toTeddy);

                        float targetYaw = targetLookRot.eulerAngles.y;
                        float targetPitch = targetLookRot.eulerAngles.x;
                        if (targetPitch > 180f) targetPitch -= 360f;

                        float currentYaw;
                        float currentPitch;

                        if (driftElapsed < rotateToTeddyDuration)
                        {
                            float rotT = Mathf.Clamp01(driftElapsed / rotateToTeddyDuration);
                            float smoothRotT = Mathf.SmoothStep(0f, 1f, rotT);
                            currentYaw = Mathf.LerpAngle(startYaw, targetYaw, smoothRotT);
                            currentPitch = Mathf.Lerp(startPitch, targetPitch, smoothRotT);
                        }
                        else
                        {
                            currentYaw = targetYaw;
                            currentPitch = targetPitch;
                        }

                        playerController.transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
                        if (playerController.playerCamera != null)
                        {
                            playerController.playerCamera.transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
                        }
                        playerController.SnapViewRotation(Quaternion.Euler(currentPitch, currentYaw, 0f));
                    }
                }

                // Progressive audio muffle & volume fade
                float muffleT = Mathf.Clamp01(driftElapsed / audioMuffleDuration);
                float smoothMuffle = Mathf.SmoothStep(0f, 1f, muffleT);
                if (lpf != null)
                {
                    lpf.cutoffFrequency = Mathf.Lerp(22000f, 350f, smoothMuffle);
                }
                if (oceanAudioSource != null)
                {
                    oceanAudioSource.volume = Mathf.Lerp(startOceanVol, 0f, smoothMuffle);
                }

                if (!transitionTriggered && driftElapsed >= (raftDriftDuration - fadeToBlackDuration))
                {
                    transitionTriggered = true;
                    currentPhase = "TransitionToClassroom";
                    ClassroomEndingController.IsEndingActive = true;
                    ScreenFader.TransitionToScene(targetSceneName, fadeToBlackDuration);
                }

                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(raftDriftDuration);
        }

        if (oceanAudioSource != null) oceanAudioSource.volume = 0f;

        if (!usePlayerCameraDrift && stationaryToyCamera != null)
        {
            currentPhase = "LeftBehindToy";
            stationaryToyCamera.gameObject.SetActive(true);

            var toyListener = stationaryToyCamera.GetComponent<AudioListener>();
            if (toyListener == null) toyListener = stationaryToyCamera.gameObject.AddComponent<AudioListener>();
            toyListener.enabled = true;

            if (playerController != null && playerController.playerCamera != null)
            {
                var playerListener = playerController.playerCamera.GetComponent<AudioListener>();
                if (playerListener != null) playerListener.enabled = false;
                playerController.playerCamera.enabled = false;
            }

            yield return new WaitForSeconds(3.0f);
            ClassroomEndingController.IsEndingActive = true;
            ScreenFader.TransitionToScene(targetSceneName, fadeToBlackDuration);
        }
        else
        {
            ClassroomEndingController.IsEndingActive = true;
        }
    }

    [ContextMenu("Debug: Force Trigger Departure")]
    public void DebugForceDeparture()
    {
        StartRaftDeparture();
    }
}
