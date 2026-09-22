using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Controls the introductory sequence of Moroccan Beach:
/// 1. Starts from black and smoothly fades into the scene.
/// 2. Keeps player movement and raft interactions locked during the intro.
/// 3. Frames the kid and Teddy (Mistfox) on the table.
/// 4. Plays a short localized dialogue where Teddy encourages the kid to build a raft.
///    - Dynamically pans the camera to look at the ocean/beach when discussing the brother's drowning.
///    - Dynamically pans back to Teddy when discussing building the raft.
/// 5. Unlocks player controls, reveals the raft tracker HUD, and enables picking up pieces.
/// </summary>
public class BeachIntroController : MonoBehaviour
{
    public static BeachIntroController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Header("Debug Status")]
    public string currentPhase = "Init";

    [Header("Timing & Fade")]
    [Tooltip("Duration in seconds of the smooth fade-in from black")]
    public float fadeDuration = 2.0f;

    [Tooltip("Pause between fade-in finishing and dialogue starting")]
    public float preDialogueDelay = 0.5f;

    [Tooltip("Pause between dialogue ending and player controls unlocking")]
    public float postDialogueDelay = 0.4f;

    [Header("Dialogue Setup")]
    public NpcDialogueManager dialogueManager;
    public string[] dialogueKeys = new string[]
    {
        "beach_intro_01",
        "beach_intro_02",
        "beach_intro_03",
        "beach_intro_04",
        "beach_intro_05",
        "beach_intro_06"
    };

    public Color dialogueColor = new Color(1.0f, 0.65f, 0.30f, 1.0f);
    public TMP_FontAsset dialogueFont;

    [Header("Player & Cutscene Setup")]
    public FirstPersonController playerController;
    public Transform playerStartLookTarget;
    public bool positionPlayerAtTable = true;
    public Vector3 playerStartPosition = new Vector3(19.2f, 1.4f, 14.6f);

    [Header("Camera Pan Choreography")]
    public bool enableCameraPan = true;
    public float cameraPanDuration = 2.2f;
    [Tooltip("Pitch, Yaw, Roll looking towards the ocean / open beach")]
    public Vector3 beachLookAngles = new Vector3(2f, 275f, 0f);
    [Tooltip("Pitch, Yaw, Roll looking back at Teddy on the table")]
    public Vector3 teddyLookAngles = new Vector3(-8f, 21f, 0f);

    [Header("Raft Gameplay")]
    public RaftPlayerInteraction raftInteraction;
    public GameObject raftTrackerPanel;
    public GameObject promptBox;

    [Header("Screen Fader")]
    public ScreenFader screenFader;
    public CanvasGroup faderOverlay;

    public event Action OnIntroFinished;
    private bool isIntroRunning = false;
    private Coroutine _cameraPanCoroutine;

    public bool IsIntroRunning => isIntroRunning;

    private void Awake()
    {
        Instance = this;
        Debug.Log("[BeachIntroController] Awake executed.");

        ResolveReferences();

        // 1. Ensure screen starts completely black
        if (faderOverlay != null)
        {
            faderOverlay.alpha = 1f;
            faderOverlay.blocksRaycasts = true;
        }

        // 2. Hide raft HUD and prompt boxes immediately
        if (raftTrackerPanel != null)
        {
            raftTrackerPanel.SetActive(false);
        }

        if (promptBox != null)
        {
            promptBox.SetActive(false);
        }

        // 3. Disable raft interaction until dialogue finishes
        if (raftInteraction != null)
        {
            raftInteraction.enabled = false;
        }

        // 4. Lock player movement and position at table
        if (playerController != null)
        {
            playerController.SetControlLocked(true);

            if (positionPlayerAtTable)
            {
                playerController.transform.position = playerStartPosition;
            }

            if (playerStartLookTarget != null)
            {
                Vector3 dir = playerStartLookTarget.position - playerController.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    playerController.SnapViewRotation(Quaternion.LookRotation(dir));
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        StartCoroutine(IntroSequenceRoutine());
    }

    private void Update()
    {
        // Enforce lock as long as the intro is active
        if (isIntroRunning)
        {
            if (playerController != null)
            {
                playerController.SetControlLocked(true);
            }
            if (raftInteraction != null && raftInteraction.enabled)
            {
                raftInteraction.enabled = false;
            }
        }
    }

    public void ResolveReferences()
    {
        if (playerController == null)
            playerController = FindAnyObjectByType<FirstPersonController>();

        if (raftInteraction == null)
            raftInteraction = FindAnyObjectByType<RaftPlayerInteraction>();

        if (screenFader == null)
            screenFader = FindAnyObjectByType<ScreenFader>();

        if (faderOverlay == null && screenFader != null)
            faderOverlay = screenFader.GetComponent<CanvasGroup>();

        if (dialogueManager == null)
            dialogueManager = FindAnyObjectByType<NpcDialogueManager>();

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

        if (playerStartLookTarget == null)
        {
            var teddy = GameObject.Find("Teddy");
            if (teddy != null) playerStartLookTarget = teddy.transform;
        }
    }

    public void PanCameraTo(Vector3 targetEuler, float duration)
    {
        if (_cameraPanCoroutine != null)
            StopCoroutine(_cameraPanCoroutine);
        _cameraPanCoroutine = StartCoroutine(SmoothRotateCameraRoutine(targetEuler, duration));
    }

    private IEnumerator SmoothRotateCameraRoutine(Vector3 targetEuler, float duration)
    {
        if (playerController == null) yield break;

        float elapsed = 0f;
        float startYaw = playerController.transform.eulerAngles.y;
        float targetYaw = targetEuler.y;

        float startPitch = 0f;
        if (playerController.playerCamera != null)
        {
            startPitch = playerController.playerCamera.transform.localEulerAngles.x;
            if (startPitch > 180f) startPitch -= 360f;
        }
        float targetPitch = targetEuler.x;
        if (targetPitch > 180f) targetPitch -= 360f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float currentYaw = Mathf.LerpAngle(startYaw, targetYaw, smoothT);
            float currentPitch = Mathf.Lerp(startPitch, targetPitch, smoothT);

            playerController.transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            if (playerController.playerCamera != null)
            {
                playerController.playerCamera.transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }
            playerController.SnapViewRotation(Quaternion.Euler(currentPitch, currentYaw, 0f));

            yield return null;
        }

        playerController.SnapViewRotation(Quaternion.Euler(targetPitch, targetYaw, 0f));
        _cameraPanCoroutine = null;
    }

    private void HandleDialogueLineStarted(int lineIndex, string lineText)
    {
        if (!enableCameraPan) return;

        Debug.Log($"[BeachIntroController] Line {lineIndex} started: {lineText}");

        // Line 0 ("This is the beach where everything changed...") -> Pan camera to ocean / open beach!
        if (lineIndex == 0)
        {
            PanCameraTo(beachLookAngles, cameraPanDuration);
        }
        // Line 3 ("Because you can't run forever... let's build a boat!") -> Pan camera back to Teddy!
        else if (lineIndex == 3)
        {
            PanCameraTo(teddyLookAngles, cameraPanDuration);
        }
    }

    private IEnumerator IntroSequenceRoutine()
    {
        isIntroRunning = true;
        currentPhase = "StartingFromBlack";

        if (playerController != null)
        {
            playerController.SetControlLocked(true);
            if (positionPlayerAtTable)
            {
                playerController.transform.position = playerStartPosition;
            }
            if (playerStartLookTarget != null)
            {
                Vector3 dir = playerStartLookTarget.position - playerController.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    playerController.SnapViewRotation(Quaternion.LookRotation(dir));
                }
            }
        }

        // Give initial scene frames a moment to initialize
        yield return null;

        // 1. Smooth Fade-in from Black
        currentPhase = "FadingFromBlack";
        if (screenFader == null) screenFader = ScreenFader.Instance ?? FindAnyObjectByType<ScreenFader>();
        if (screenFader != null)
        {
            bool fadeDone = false;
            screenFader.FadeFromBlack(fadeDuration, () => fadeDone = true);
            while (!fadeDone) yield return null;
        }
        else if (faderOverlay != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                faderOverlay.alpha = 1f - t;
                AudioListener.volume = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
            faderOverlay.alpha = 0f;
            faderOverlay.blocksRaycasts = false;
            AudioListener.volume = 1f;
        }
        else
        {
            AudioListener.volume = 1f;
            yield return new WaitForSeconds(fadeDuration);
        }

        // 2. Pre-dialogue pause
        currentPhase = "PreDialoguePause";
        if (preDialogueDelay > 0f)
        {
            yield return new WaitForSeconds(preDialogueDelay);
        }

        // 3. Play Dialogue with Camera Choreography
        currentPhase = "PlayingDialogue";
        if (dialogueManager == null)
            dialogueManager = NpcDialogueManager.Instance;

        if (dialogueManager != null && dialogueKeys != null && dialogueKeys.Length > 0)
        {
            string[] resolvedLines = new string[dialogueKeys.Length];
            for (int i = 0; i < dialogueKeys.Length; i++)
            {
                string key = dialogueKeys[i];
                string line = null;
                if (LocalizationManager.Instance != null)
                {
                    line = LocalizationManager.Instance.Get(key);
                }

                if (string.IsNullOrEmpty(line) || line == key)
                {
                    line = GetDefaultFallbackLine(key);
                }

                resolvedLines[i] = line;
            }

            // Start dialogue coroutine on the dialogueManager itself, with line callback
            dialogueManager.StartCoroutine(dialogueManager.ShowDialogue(
                resolvedLines,
                hasChoice: false,
                dialogueColor: dialogueColor,
                dialogueFont: dialogueFont,
                voiceSource: null,
                voiceClips: null,
                onLineStarted: HandleDialogueLineStarted
            ));

            // Wait until dialogue is running
            yield return null;

            // Wait until dialogue completes
            while (dialogueManager.IsDialogueRunning)
            {
                yield return null;
            }
        }

        // 4. Post-dialogue pause
        currentPhase = "PostDialoguePause";
        if (postDialogueDelay > 0f)
        {
            yield return new WaitForSeconds(postDialogueDelay);
        }

        // 5. Unlock Player Movement & Mouse Look
        currentPhase = "UnlockingGameplay";
        isIntroRunning = false;

        if (playerController != null)
        {
            playerController.SetControlLocked(false);
        }

        // 6. Reveal Raft Objective Tracker HUD
        if (raftTrackerPanel != null)
        {
            raftTrackerPanel.SetActive(true);
        }

        // 7. Enable Raft Piece Grabbing
        if (raftInteraction != null)
        {
            raftInteraction.enabled = true;
            raftInteraction.UpdateObjectiveTracker();
        }

        currentPhase = "Completed";
        Debug.Log("[BeachIntroController] Intro completed! Raft gameplay unlocked.");
        OnIntroFinished?.Invoke();
    }

    private string GetDefaultFallbackLine(string key)
    {
        switch (key)
        {
            case "beach_intro_01":
                return "{TEDDY_NAME}: Look at that ocean... This is the beach where everything changed.";
            case "beach_intro_02":
                return "{TEDDY_NAME}: The waves were so rough that day. You were drowning... and your brother jumped in to save you.";
            case "beach_intro_03":
                return "Child: Stop... Why are you making me remember this?";
            case "beach_intro_04":
                return "{TEDDY_NAME}: Because you can't run forever. He would want you to move forward. Come on... let's build a boat!";
            case "beach_intro_05":
                return "{TEDDY_NAME}: Find some wood logs and a cloth on the sand for the sail.";
            case "beach_intro_06":
                return "Child: A boat...? Alright, let's build a raft.";
            default:
                return key;
        }
    }
}
