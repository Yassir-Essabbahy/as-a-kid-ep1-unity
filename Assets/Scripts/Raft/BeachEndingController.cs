using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Coordinates the psychological ending cutscene in Moroccan Beach:
/// 1. Raft Ready: When the final piece (cloth) is placed, Teddy calls out to the child
///    telling him the boat is ready and to get on the raft.
/// 2. Boarding Interaction: The player walks up to the raft and presses 'E' to board.
/// 3. Raft Departure: Controls lock, player steps onto the raft deck (standing upright on top of the logs),
///    and the raft begins drifting slowly out into the ocean with gentle wave bobbing.
/// 4. Looking Back at Teddy: While on the boat, the player camera smoothly rotates to look back
///    at Teddy sitting on the beach table under the umbrella. The camera tracks Teddy as the raft
///    sails far away into the open water, showing the beach and Teddy receding into the distance.
/// 5. Atmospheric Muffle & Classroom Reality Transition: Ambient ocean sound muffles via LowPassFilter
///    and fades to silence. Screen fades smoothly to black during deep-sea drift, activates
///    ClassroomEndingController.IsEndingActive, and transitions to scene S1.
/// </summary>
public class BeachEndingController : MonoBehaviour
{
    public static BeachEndingController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

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

    [Header("Raft Ready & Dialogue")]
    public string boardRaftDialogueKey = "beach_ending_ready";
    public Color dialogueColor = Color.white;
    public TMP_FontAsset dialogueFont;
    public bool CanBoardRaft { get; private set; } = false;

    [Header("Phase 1: Raft Departure & Teddy Look Settings")]
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

    [Tooltip("World offset for player relative to assembledRaftVisuals.position (ensures player stands on top of the logs with clear line-of-sight to Teddy)")]
    public Vector3 playerRaftWorldOffset = new Vector3(-0.6f, 1.25f, -0.6f);

    [Tooltip("Time taken for player camera on the boat to smoothly rotate and face Teddy")]
    public float rotateToTeddyDuration = 2.0f;

    [Tooltip("Whether to continuously track Teddy as the raft drifts farther away into the ocean")]
    public bool trackTeddyDuringDrift = true;

    [Tooltip("Whether to keep player camera on the boat rotating to Teddy (true) or cut to stationary beach camera (false)")]
    public bool usePlayerCameraDrift = true;

    [Header("Audio & Transition Settings")]
    [Tooltip("Time to sweep low-pass filter and fade ocean volume to zero")]
    public float audioMuffleDuration = 5.0f;
    public string targetSceneName = "Ending";
    public float fadeToBlackDuration = 2.0f;

    [Header("Status")]
    public bool isEndingActive = false;
    public string currentPhase = "Idle";

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
    }

    /// <summary>
    /// Invoked when all 4 raft pieces have been placed.
    /// Triggers Teddy's callout to tell the child to board the boat!
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

        // 1. Get localized line for Teddy telling the child to get on the raft
        string line = null;
        if (LocalizationManager.Instance != null)
        {
            line = LocalizationManager.Instance.Get(boardRaftDialogueKey);
        }
        if (string.IsNullOrEmpty(line) || line == boardRaftDialogueKey)
        {
            line = "Teddy: The boat is ready! Come on, get on the raft!";
        }

        // Replace placeholder tokens
        string teddyName = MetroStorySequenceController.TeddyName;
        if (line.Contains("{TEDDY_NAME}")) line = line.Replace("{TEDDY_NAME}", teddyName);
        if (line.Contains("{CHILD_NAME}")) line = line.Replace("{CHILD_NAME}", MetroStorySequenceController.ChildName);

        // 2. Play dialogue through NpcDialogueManager
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

        // 3. Enable ability to press E to board the raft
        CanBoardRaft = true;

        // Update the objective tracker to show "Boat: Ready! Press E to board"
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
    /// Called when the player presses 'E' while looking at or near the completed raft.
    /// </summary>
    public void StartRaftDeparture()
    {
        if (isEndingActive) return;
        CanBoardRaft = false;
        isEndingActive = true;
        Debug.Log("[BeachEndingController] Player pressed E to board raft! Commencing departure cutscene.");
        StartCoroutine(EndingSequenceRoutine());
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
            // Compute target deck position using unrotated world offset so camera is on top of logs
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

        // 3. Resolve Teddy transform if not already cached
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

            // Cache player starting camera rotation before turning to Teddy
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

                // Move outward into the ocean along driftDirection
                raftBasePos += driftDirection.normalized * (raftDriftSpeed * Time.deltaTime);

                // Calculate sinusoidal wave bobbing
                float waveY = Mathf.Sin(Time.time * waveFrequency) * waveAmplitude;
                float rollZ = Mathf.Sin(Time.time * (waveFrequency * 0.8f)) * 1.2f;
                float pitchX = Mathf.Cos(Time.time * (waveFrequency * 0.7f)) * 0.8f;

                assembledRaftVisuals.position = raftBasePos + new Vector3(0f, waveY, 0f);
                assembledRaftVisuals.rotation = raftBaseRot * Quaternion.Euler(pitchX, 0f, rollZ);

                // Update player position directly by the raft's translation delta (no rotated parenting)
                if (playerController != null)
                {
                    Vector3 raftDelta = assembledRaftVisuals.position - prevRaftPos;
                    playerController.transform.position += raftDelta;

                    // Rotate player camera on the boat to look back towards Teddy
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
                            // Smoothly pan camera from initial view towards Teddy
                            float rotT = Mathf.Clamp01(driftElapsed / rotateToTeddyDuration);
                            float smoothRotT = Mathf.SmoothStep(0f, 1f, rotT);
                            currentYaw = Mathf.LerpAngle(startYaw, targetYaw, smoothRotT);
                            currentPitch = Mathf.Lerp(startPitch, targetPitch, smoothRotT);
                        }
                        else if (trackTeddyDuringDrift)
                        {
                            // Continuously track Teddy as the raft drifts farther away
                            currentYaw = targetYaw;
                            currentPitch = targetPitch;
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

                // Trigger smooth fade to black and transition to S1 as raft reaches deep water
                if (!transitionTriggered && driftElapsed >= (raftDriftDuration - fadeToBlackDuration))
                {
                    transitionTriggered = true;
                    currentPhase = "TransitionToClassroom";
                    Debug.Log("[BeachEndingController] Drifting far away: Fading to black and transitioning to Classroom Reality (S1).");
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

        // Fallback: If player camera drift was disabled, execute stationary camera cut
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
