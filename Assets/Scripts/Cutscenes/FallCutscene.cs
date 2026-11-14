using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;

/// <summary>
/// First-person falling cutscene for psychological horror/narrative sequence.
/// Player falls through pitch darkness, slams into the ground, lies dazed,
/// and believably struggles to get up before control is handed over seamlessly.
/// </summary>
[DisallowMultipleComponent]
public class FallCutscene : MonoBehaviour
{
    public static FallCutscene Instance { get; private set; }
    public static bool HasSeenCutsceneThisSession = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HasSeenCutsceneThisSession = false;
    }

    public enum CutsceneStage
    {
        Inactive,
        FallingBlack,
        GroundReveal,
        ImpactFlash,
        ImpactBlack,
        LyingDazed,
        Rolling,
        PushingUp,
        Kneeling,
        Standing,
        Completed
    }

    [Header("Mode Configuration")]
    [Tooltip("If true, skips the sky fall and starts directly lying on the ground, waking up and getting up.")]
    public bool skipFallSequence = false;
    [Tooltip("If false, all sound effects, audio sources, and listener low-pass filters are disabled completely.")]
    public bool enableSound = true;

    [Header("Current State")]
    [SerializeField] private CutsceneStage currentStage = CutsceneStage.Inactive;
    public CutsceneStage CurrentStage => currentStage;
    public bool IsCinematicActive => isCinematicActive;

    [Header("Named Pose Transforms (Auto-Created/Found if null)")]
    public Transform fallStartPose;       // "Fall_Start"
    public Transform landImpactPose;      // "Land_Impact"
    public Transform landLyingPose;       // "Land_Lying"
    public Transform landKneelingPose;    // "Land_Kneeling"
    public Transform landStandingPose;    // "Land_Standing"

    [Header("Player & Camera References")]
    public GameObject player;
    public Camera mainCamera;
    public Canvas mobileControlsCanvas;
    public ObjectiveUI objectiveUI;

    [Header("Phase 2: Fall Timings & Dynamics")]
    [Tooltip("Total fall duration in pitch black before reveal (approx 7.0 - 8.5s)")]
    public float fallDuration = 7.5f;
    [Tooltip("Ground reveal duration before impact (approx 0.4s)")]
    public float groundRevealDuration = 0.4f;
    [Tooltip("Tumble noise rotation amplitude (roll and pitch drift)")]
    public float tumbleNoiseAmp = 14f;
    [Tooltip("Turbulence flutter noise frequency")]
    public float flutterFrequency = 28f;
    [Tooltip("Max turbulence flutter amplitude as speed peaks")]
    public float maxFlutterAmp = 2.5f;

    [Header("Phase 3: Impact")]
    [Tooltip("Impact flash duration in frames (2 to 3 frames)")]
    public int flashFrames = 3;
    [Tooltip("Duration of tinnitus ringing and low-pass muffled world (approx 6 to 8s)")]
    public float tinnitusDuration = 7.0f;
    public float initialMuffleCutoff = 450f;

    [Header("Phase 4: Lying Dazed Timings (approx 6 to 8s)")]
    public float blink1Open = 0.10f;
    public float blink1OpenDuration = 0.8f;
    public float blink1HoldDuration = 0.6f;
    public float blink1CloseDuration = 0.6f;
    public float blink1RestDuration = 0.7f;

    public float blink2Open = 0.40f;
    public float blink2OpenDuration = 1.1f;
    public float blink2HoldDuration = 0.8f;
    public float blink2CloseDuration = 0.8f;
    public float blink2RestDuration = 0.8f;

    public float blink3Open = 1.0f;
    public float blink3OpenDuration = 2.0f;
    public float blink3RestDuration = 1.0f;

    [Tooltip("Ragged breathing head bob amplitude")]
    public float breathingBobAmp = 0.012f;
    public float breathingSpeed = 1.6f;

    [Header("Phase 5: Getting Up Timings (approx 10 to 12s)")]
    public float rollDuration = 2.0f;
    public float pushUpDuration = 2.5f;
    public float kneelDuration = 3.2f;
    public float standDuration = 3.8f;

    [Header("Curves & Noise")]
    public AnimationCurve easeInOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve fallCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f));
    public float handheldNoisePos = 0.008f;
    public float handheldNoiseRot = 0.35f;

    [Header("Audio Clips")]
    public AudioClip windRushClip;
    public AudioClip clothFlapClip;
    public AudioClip airHissClip;
    public AudioClip heartbeatFastClip;
    public AudioClip breathGaspClip;
    public AudioClip impactClip;
    public AudioClip tinnitusClip;
    public AudioClip heartbeatSlowClip;
    public AudioClip raggedExhaleClip;
    public AudioClip coughClip;
    public AudioClip strainedGruntClip;
    public AudioClip handScrapeClip;
    public AudioClip footstepScuffClip;

    [Header("UI & Skip")]
    public GameObject skipButtonUI;
    public TextMeshProUGUI skipButtonText;
    public Canvas overlayCanvas;
    public CanvasGroup blackOverlay;
    public Image flashOverlay;
    public Volume heavyEyesVolume;

    // Private runtime components
    private FirstPersonController fpc;
    private AudioSource windAudioSource;
    private AudioSource clothAudioSource;
    private AudioSource hissAudioSource;
    private AudioSource heartbeatAudioSource;
    private AudioSource sfxAudioSource;
    private AudioSource tinnitusAudioSource;
    private AudioLowPassFilter listenerLowPass;
    private ParticleSystem dustParticles;

    private float originalNearClip = 0.3f;
    private float originalFOV = 60f;
    private bool isCinematicActive = false;
    private Coroutine activeCoroutine;
    private bool mobileSkipButtonVisible = false;
    private float mobileSkipTimer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Lock cursor on desktop
        if (!Application.isMobilePlatform && !Story.MobileUIAdapter.IsMobile)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        FindSceneReferences();
        EnsurePoseTransforms();
        EnsureCutsceneUI();
        EnsureAudioSources();
        EnsureDustParticles();
    }

    private void Start()
    {
        if (!Application.isMobilePlatform && !Story.MobileUIAdapter.IsMobile)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (HasSeenCutsceneThisSession)
        {
            activeCoroutine = StartCoroutine(ShortCutsceneRoutine());
        }
        else
        {
            activeCoroutine = StartCoroutine(FullCutsceneRoutine());
        }
    }

    private void Update()
    {
        if (!isCinematicActive) return;
        if (Story.MobilePauseMenu.Instance != null && Story.MobilePauseMenu.Instance.isPaused) return;

        // Keep player movement and look strictly locked during cinematic
        if (fpc != null && (fpc.playerCanMove || fpc.cameraCanMove || fpc.enabled))
        {
            fpc.SetControlLocked(true);
            fpc.enabled = false;
            fpc.playerCanMove = false;
            fpc.cameraCanMove = false;
        }

        // Ensure cursor is locked on PC during cutscene
        if (!Application.isMobilePlatform && !Story.MobileUIAdapter.IsMobile)
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.visible) Cursor.visible = false;
        }

        // Mobile Skip Button auto-hide timer
        if (mobileSkipButtonVisible && skipButtonUI != null)
        {
            mobileSkipTimer -= Time.deltaTime;
            if (mobileSkipTimer <= 0f)
            {
                mobileSkipButtonVisible = false;
                skipButtonUI.SetActive(false);
            }
        }

        // Skip detection:
        // PC: Any key down or mouse click
        // Mobile: Screen tap (if skip button not already showing, shows button; if showing or tapped, skips)
        bool pointerOverPause = DialogueTypewriterUtility.IsPointerOverPauseButton();
        if (!pointerOverPause)
        {
            if (Application.isMobilePlatform || Story.MobileUIAdapter.IsMobile)
            {
                if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                {
                    if (!mobileSkipButtonVisible)
                    {
                        ShowMobileSkipButton(3.5f);
                    }
                    else
                    {
                        SkipCutscene();
                    }
                }
            }
            else
            {
                if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
                {
                    SkipCutscene();
                }
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                {
                    SkipCutscene();
                }
            }
        }
    }

    public void ShowMobileSkipButton(float duration)
    {
        if (skipButtonUI != null)
        {
            skipButtonUI.SetActive(true);
            mobileSkipButtonVisible = true;
            mobileSkipTimer = duration;
        }
    }

    public void SkipCutscene()
    {
        if (!isCinematicActive) return;

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        StartCoroutine(QuickFinishRoutine());
    }

    private void FindSceneReferences()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            player = GameObject.Find("FirstPersonController");

        if (player != null)
        {
            fpc = player.GetComponent<FirstPersonController>();
            if (mainCamera == null)
                mainCamera = player.GetComponentInChildren<Camera>(true);

            if (mobileControlsCanvas == null)
            {
                var mcTransform = player.transform.Find("MobileControlsCanvas");
                if (mcTransform != null) mobileControlsCanvas = mcTransform.GetComponent<Canvas>();
            }
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (objectiveUI == null)
            objectiveUI = FindAnyObjectByType<ObjectiveUI>();

        // Auto load audio clips if not serialized
#if UNITY_EDITOR
        string audioDir = "Assets/Sounds/FallCutscene";
        if (windRushClip == null) windRushClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/WindRush_Loop.wav");
        if (clothFlapClip == null) clothFlapClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/ClothFlap_Loop.wav");
        if (airHissClip == null) airHissClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/AirHiss_Loop.wav");
        if (heartbeatFastClip == null) heartbeatFastClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/Heartbeat_Fast.wav");
        if (breathGaspClip == null) breathGaspClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/Breath_Gasp.wav");
        if (impactClip == null) impactClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/Impact_ThudBoomCrunch.wav");
        if (tinnitusClip == null) tinnitusClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/Tinnitus_Ringing.wav");
        if (heartbeatSlowClip == null) heartbeatSlowClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/Heartbeat_Slow.wav");
        if (raggedExhaleClip == null) raggedExhaleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/PainedExhale.wav");
        if (coughClip == null) coughClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Cought.wav");
        if (strainedGruntClip == null) strainedGruntClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/Grunt_Strained.wav");
        if (handScrapeClip == null) handScrapeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/HandScrape.wav");
        if (footstepScuffClip == null) footstepScuffClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/School/Footstep Dirt 1.ogg");
#endif
    }

    private void EnsurePoseTransforms()
    {
        // Named pose transforms in the scene (no hardcoded numbers in motion code):
        // "Fall_Start", "Land_Impact", "Land_Lying", "Land_Kneeling", "Land_Standing"
        Vector3 basePos = player != null ? player.transform.position : new Vector3(25.31f, 1.0f, 9.62f);
        float baseYaw = player != null ? player.transform.eulerAngles.y : 0f;

        // Raycast down to find true ground height
        float groundY = 0f;
        RaycastHit hit;
        int layerMask = ~LayerMask.GetMask("Ignore Raycast");
        if (player != null)
        {
            // Temporarily ignore player collider
            var col = player.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            if (Physics.Raycast(new Vector3(basePos.x, basePos.y + 10f, basePos.z), Vector3.down, out hit, 50f, layerMask))
            {
                groundY = hit.point.y;
            }
            if (col != null) col.enabled = true;
        }

        var posesRoot = GameObject.Find("FallCutscene_Poses");
        if (posesRoot == null) posesRoot = new GameObject("FallCutscene_Poses");

        if (fallStartPose == null)
        {
            var go = GameObject.Find("Fall_Start");
            if (go == null)
            {
                go = new GameObject("Fall_Start");
                go.transform.SetParent(posesRoot.transform, false);
                go.transform.position = new Vector3(basePos.x, groundY + 30f, basePos.z);
                go.transform.rotation = Quaternion.Euler(15f, baseYaw, 0f);
            }
            fallStartPose = go.transform;
        }

        if (landImpactPose == null)
        {
            var go = GameObject.Find("Land_Impact");
            if (go == null)
            {
                go = new GameObject("Land_Impact");
                go.transform.SetParent(posesRoot.transform, false);
                go.transform.position = new Vector3(basePos.x, groundY + 0.35f, basePos.z);
                go.transform.rotation = Quaternion.Euler(85f, baseYaw, 0f); // Camera facing down at ground
            }
            landImpactPose = go.transform;
        }

        if (landLyingPose == null)
        {
            var go = GameObject.Find("Land_Lying");
            if (go == null)
            {
                go = new GameObject("Land_Lying");
                go.transform.SetParent(posesRoot.transform, false);
                go.transform.position = new Vector3(basePos.x, groundY + 0.22f, basePos.z);
                go.transform.rotation = Quaternion.Euler(-65f, baseYaw, 7f); // Lying on back, looking slightly up with 7 deg roll
            }
            landLyingPose = go.transform;
        }

        if (landKneelingPose == null)
        {
            var go = GameObject.Find("Land_Kneeling");
            if (go == null)
            {
                go = new GameObject("Land_Kneeling");
                go.transform.SetParent(posesRoot.transform, false);
                go.transform.position = new Vector3(basePos.x, groundY + 0.90f, basePos.z);
                go.transform.rotation = Quaternion.Euler(28f, baseYaw - 2f, 2f); // Head hanging low, looking downward
            }
            landKneelingPose = go.transform;
        }

        if (landStandingPose == null)
        {
            var go = GameObject.Find("Land_Standing");
            if (go == null)
            {
                go = new GameObject("Land_Standing");
                go.transform.SetParent(posesRoot.transform, false);
                // Eye level when player standing (player base at groundY + 1.0, joint at 0.75 -> eye at 1.75)
                go.transform.position = new Vector3(basePos.x, groundY + 1.75f, basePos.z);
                go.transform.rotation = Quaternion.Euler(0f, baseYaw, 0f); // Level horizon, looking forward
            }
            landStandingPose = go.transform;
        }
    }

    private void EnsureCutsceneUI()
    {
        if (overlayCanvas == null)
        {
            var go = GameObject.Find("FallCutscene_OverlayCanvas");
            if (go == null)
            {
                go = new GameObject("FallCutscene_OverlayCanvas");
                overlayCanvas = go.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 45; // Under eyelids (order 50)

                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                go.AddComponent<GraphicRaycaster>();
            }
            else
            {
                overlayCanvas = go.GetComponent<Canvas>();
            }
        }

        // Solid Black Overlay
        if (blackOverlay == null && overlayCanvas != null)
        {
            var t = overlayCanvas.transform.Find("BlackOverlay");
            GameObject bGo;
            if (t == null)
            {
                bGo = new GameObject("BlackOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                bGo.transform.SetParent(overlayCanvas.transform, false);
            }
            else bGo = t.gameObject;

            var rect = bGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = bGo.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            blackOverlay = bGo.GetComponent<CanvasGroup>();
            blackOverlay.alpha = 1f;
            blackOverlay.blocksRaycasts = false;
        }

        // Instant White Flash Overlay
        if (flashOverlay == null && overlayCanvas != null)
        {
            var t = overlayCanvas.transform.Find("FlashOverlay");
            GameObject fGo;
            if (t == null)
            {
                fGo = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
                fGo.transform.SetParent(overlayCanvas.transform, false);
            }
            else fGo = t.gameObject;

            var rect = fGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            flashOverlay = fGo.GetComponent<Image>();
            flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            flashOverlay.raycastTarget = false;
        }

        // Skip Button UI
        if (skipButtonUI == null && overlayCanvas != null)
        {
            var t = overlayCanvas.transform.Find("SkipButtonUI");
            if (t != null)
            {
                skipButtonUI = t.gameObject;
                skipButtonText = skipButtonUI.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                var sGo = new GameObject("SkipButtonUI", typeof(RectTransform), typeof(Image), typeof(Button));
                sGo.transform.SetParent(overlayCanvas.transform, false);

                var sRect = sGo.GetComponent<RectTransform>();
                sRect.anchorMin = new Vector2(1f, 1f);
                sRect.anchorMax = new Vector2(1f, 1f);
                sRect.pivot = new Vector2(1f, 1f);
                sRect.anchoredPosition = new Vector2(-40f, -40f);
                sRect.sizeDelta = new Vector2(140f, 46f);

                var sImg = sGo.GetComponent<Image>();
                sImg.color = new Color(0.12f, 0.12f, 0.14f, 0.75f);

                var btn = sGo.GetComponent<Button>();
                btn.onClick.AddListener(SkipCutscene);

                var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(sGo.transform, false);
                var tRect = txtGo.GetComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero;
                tRect.anchorMax = Vector2.one;
                tRect.offsetMin = Vector2.zero;
                tRect.offsetMax = Vector2.zero;

                skipButtonText = txtGo.GetComponent<TextMeshProUGUI>();
                skipButtonText.alignment = TextAlignmentOptions.Center;
                skipButtonText.fontSize = 20;
                skipButtonText.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

                string skipLocalized = "Skip";
                if (LocalizationManager.Instance != null)
                    skipLocalized = LocalizationManager.Resolve("ui_skip", "Skip");
                skipButtonText.text = skipLocalized;

                skipButtonUI = sGo;
                skipButtonUI.SetActive(false);
            }
        }

        // Ensure EyelidBlink exists
        var eyelid = EyelidBlink.Instance;
        if (eyelid == null)
        {
            var eGo = new GameObject("EyelidBlink_Controller");
            eyelid = eGo.AddComponent<EyelidBlink>();
        }
        if (eyelid != null)
        {
            eyelid.EnsureUIBuilt();
            eyelid.SetOpenImmediate(0f); // Closed initially
        }
    }

    private void EnsureAudioSources()
    {
        if (!enableSound) return;

        windAudioSource = CreateAudioSource("Audio_WindRush", true, 0f);
        clothAudioSource = CreateAudioSource("Audio_ClothFlap", true, 0f);
        hissAudioSource = CreateAudioSource("Audio_AirHiss", true, 0f);
        heartbeatAudioSource = CreateAudioSource("Audio_Heartbeat", false, 0f);
        sfxAudioSource = CreateAudioSource("Audio_SFX", false, 0f);
        tinnitusAudioSource = CreateAudioSource("Audio_Tinnitus", false, 0f);
        tinnitusAudioSource.bypassListenerEffects = true; // Ringing stays clear while world is muffled

        // Low pass filter on listener (camera)
        if (mainCamera != null)
        {
            listenerLowPass = mainCamera.GetComponent<AudioLowPassFilter>();
            if (listenerLowPass == null) listenerLowPass = mainCamera.gameObject.AddComponent<AudioLowPassFilter>();
            listenerLowPass.enabled = false;
            listenerLowPass.cutoffFrequency = 22000f;
        }
    }

    private AudioSource CreateAudioSource(string name, bool loop, float spatialBlend)
    {
        if (!enableSound) return null;

        var child = transform.Find(name);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(transform, false);
        }
        else go = child.gameObject;

        var src = go.GetComponent<AudioSource>();
        if (src == null) src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = spatialBlend; // 2D normalized
        return src;
    }

    private void EnsureDustParticles()
    {
        if (skipFallSequence) return;

        var pChild = transform.Find("DustParticles");
        if (pChild != null)
        {
            dustParticles = pChild.GetComponent<ParticleSystem>();
            return;
        }

        var pGo = new GameObject("DustParticles");
        pGo.transform.SetParent(transform, false);
        dustParticles = pGo.AddComponent<ParticleSystem>();
        var main = dustParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.4f;
        main.startSpeed = 35f; // Fast upward rushing particles
        main.startSize = 0.02f;
        main.startColor = new Color(0.7f, 0.7f, 0.7f, 0.25f);
        main.maxParticles = 60; // Mobile friendly, very cheap
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = dustParticles.emission;
        emission.rateOverTime = 80f;

        var shape = dustParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2f, 0.5f, 2f);

        var velocity = dustParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(25f, 40f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var renderer = pGo.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
    }

    // =========================================================================
    // MAIN CUTSCENE SEQUENCE
    // =========================================================================

    private IEnumerator FullCutsceneRoutine()
    {
        isCinematicActive = true;

        // 1. Lock controls and hide UI
        if (fpc != null)
        {
            fpc.SetControlLocked(true);
            fpc.enabled = false;
            fpc.playerCanMove = false;
            fpc.cameraCanMove = false;
            fpc.SetCrosshairVisible(false);
        }

        var crosshairCanvas = GameObject.Find("CrosshairAndStamina");
        if (crosshairCanvas != null) crosshairCanvas.SetActive(false);

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(false);

        // Adjust near clip plane so ground/lying poses never clip
        if (mainCamera != null)
        {
            originalNearClip = mainCamera.nearClipPlane;
            originalFOV = mainCamera.fieldOfView;
            mainCamera.nearClipPlane = 0.03f;
        }

        // Close eyelids to pure black and enable black overlay
        if (EyelidBlink.Instance != null)
            EyelidBlink.Instance.SetOpenImmediate(0f);

        if (blackOverlay != null)
            blackOverlay.alpha = 1f;

        Vector3 lyingPos = landLyingPose != null ? landLyingPose.position : (player != null ? player.transform.position + Vector3.up * 0.22f : new Vector3(25.31f, 0.22f, 9.62f));
        Quaternion lyingRot = landLyingPose != null ? landLyingPose.rotation : Quaternion.Euler(-65f, (player != null ? player.transform.eulerAngles.y : 0f), 7f);

        if (skipFallSequence)
        {
            // Direct ground wake-up: place camera on ground pose in black
            currentStage = CutsceneStage.LyingDazed;
            if (mainCamera != null)
            {
                mainCamera.transform.position = lyingPos;
                mainCamera.transform.rotation = lyingRot;
            }

            yield return new WaitForSeconds(0.4f);

            if (blackOverlay != null)
                blackOverlay.alpha = 0f;
        }
        else
        {
            // Initial camera placement at Fall_Start
            currentStage = CutsceneStage.FallingBlack;
            Vector3 startPos = fallStartPose != null ? fallStartPose.position : new Vector3(25.31f, 30f, 9.62f);
            Quaternion startRot = fallStartPose != null ? fallStartPose.rotation : Quaternion.Euler(15f, 90f, 0f);

            if (mainCamera != null)
            {
                mainCamera.transform.position = startPos;
                mainCamera.transform.rotation = startRot;
            }

            yield return new WaitForSeconds(0.2f);

            // Start breath gasp near the start
            if (enableSound && sfxAudioSource != null && breathGaspClip != null)
            {
                sfxAudioSource.PlayOneShot(breathGaspClip, 0.85f);
            }

            // Start wind, cloth flap, air hiss, and fast heartbeat
            if (enableSound && windAudioSource != null && windRushClip != null)
            {
                windAudioSource.clip = windRushClip;
                windAudioSource.volume = 0.15f;
                windAudioSource.pitch = 0.85f;
                windAudioSource.Play();
            }

            if (enableSound && clothAudioSource != null && clothFlapClip != null)
            {
                clothAudioSource.clip = clothFlapClip;
                clothAudioSource.volume = 0.1f;
                clothAudioSource.Play();
            }

            if (enableSound && hissAudioSource != null && airHissClip != null)
            {
                hissAudioSource.clip = airHissClip;
                hissAudioSource.volume = 0.05f;
                hissAudioSource.Play();
            }

            if (enableSound && heartbeatAudioSource != null && heartbeatFastClip != null)
            {
                heartbeatAudioSource.clip = heartbeatFastClip;
                heartbeatAudioSource.volume = 0.5f;
                heartbeatAudioSource.Play();
            }

            if (dustParticles != null)
            {
                dustParticles.transform.position = startPos + Vector3.down * 1.5f;
                dustParticles.Play();
            }

            // ---------------------------------------------------------------------
            // PHASE 2: THE FALL (7.5s total in darkness, then 0.4s reveal)
            // ---------------------------------------------------------------------
            Vector3 impactPos = landImpactPose != null ? landImpactPose.position : new Vector3(25.31f, 0.35f, 9.62f);
            Quaternion impactRot = landImpactPose != null ? landImpactPose.rotation : Quaternion.Euler(85f, 90f, 0f);

            float elapsedFall = 0f;
            float darkFallDuration = Mathf.Max(0.1f, fallDuration - groundRevealDuration);

            while (elapsedFall < darkFallDuration)
            {
                elapsedFall += Time.deltaTime;
                float rawT = Mathf.Clamp01(elapsedFall / fallDuration);
                float t = fallCurve != null ? fallCurve.Evaluate(rawT) : (rawT * rawT);

                // Interpolate position downward
                Vector3 curPos = Vector3.Lerp(startPos, impactPos, t);

                // Camera tumbles slowly and unevenly (Perlin noise roll and pitch drift)
                float tumblePitch = (Mathf.PerlinNoise(Time.time * 0.9f, 0f) - 0.5f) * tumbleNoiseAmp;
                float tumbleRoll = (Mathf.PerlinNoise(10f, Time.time * 0.8f) - 0.5f) * tumbleNoiseAmp;
                float tumbleYaw = (Mathf.PerlinNoise(Time.time * 0.6f, 20f) - 0.5f) * (tumbleNoiseAmp * 0.5f);

                // Fast tiny turbulence flutter that increases as speed builds
                float flutterStrength = rawT * maxFlutterAmp;
                float flutterX = (Mathf.PerlinNoise(Time.time * flutterFrequency, 0f) - 0.5f) * flutterStrength;
                float flutterY = (Mathf.PerlinNoise(0f, Time.time * flutterFrequency) - 0.5f) * flutterStrength;
                float flutterZ = (Mathf.PerlinNoise(50f, Time.time * flutterFrequency) - 0.5f) * flutterStrength;

                Quaternion baseRot = Quaternion.Slerp(startRot, impactRot, t * 0.6f);
                Quaternion tumbleRot = baseRot * Quaternion.Euler(tumblePitch + flutterX, tumbleYaw + flutterY, tumbleRoll + flutterZ);

                if (mainCamera != null)
                {
                    mainCamera.transform.position = curPos;
                    mainCamera.transform.rotation = tumbleRot;
                }

                if (dustParticles != null)
                {
                    dustParticles.transform.position = curPos + Vector3.down * 1.5f;
                }

                // Audio crescendo and pitch rise
                if (enableSound && windAudioSource != null)
                {
                    windAudioSource.volume = Mathf.Lerp(0.15f, 0.9f, rawT);
                    windAudioSource.pitch = Mathf.Lerp(0.85f, 1.3f, rawT);
                }
                if (enableSound && clothAudioSource != null)
                {
                    clothAudioSource.volume = Mathf.Lerp(0.1f, 0.85f, rawT);
                }
                if (enableSound && hissAudioSource != null)
                {
                    hissAudioSource.volume = Mathf.Lerp(0.05f, 0.8f, rawT);
                }
                if (enableSound && heartbeatAudioSource != null)
                {
                    heartbeatAudioSource.volume = Mathf.Lerp(0.5f, 1.0f, rawT);
                }

                yield return null;
            }

            // ---------------------------------------------------------------------
            // GROUND REVEAL (Final ~0.4s: Darkness reveals ground rushing into view too late)
            // ---------------------------------------------------------------------
            currentStage = CutsceneStage.GroundReveal;
            float elapsedReveal = 0f;
            Vector3 revealStartPos = mainCamera != null ? mainCamera.transform.position : Vector3.Lerp(startPos, impactPos, 0.85f);
            Quaternion revealStartRot = mainCamera != null ? mainCamera.transform.rotation : impactRot;

            while (elapsedReveal < groundRevealDuration)
            {
                elapsedReveal += Time.deltaTime;
                float u = Mathf.Clamp01(elapsedReveal / groundRevealDuration);
                float smoothU = u * u; // Snapping acceleration

                Vector3 pos = Vector3.Lerp(revealStartPos, impactPos, smoothU);

                // Rapid pitch down to face the ground
                Quaternion rot = Quaternion.Slerp(revealStartRot, impactRot, smoothU);

                // Reveal ground silhouette through darkness
                if (blackOverlay != null)
                {
                    blackOverlay.alpha = Mathf.Lerp(1.0f, 0.15f, u);
                }

                if (mainCamera != null)
                {
                    mainCamera.transform.position = pos;
                    mainCamera.transform.rotation = rot;
                }

                if (enableSound && windAudioSource != null)
                {
                    windAudioSource.volume = Mathf.Lerp(0.9f, 1.0f, u);
                    windAudioSource.pitch = Mathf.Lerp(1.3f, 1.45f, u);
                }

                yield return null;
            }

            // ---------------------------------------------------------------------
            // PHASE 3: THE IMPACT (Hard cut, 2-3 frames near-white flash, then black)
            // ---------------------------------------------------------------------
            currentStage = CutsceneStage.ImpactFlash;

            // Instantly stop fall audio & particles
            if (windAudioSource != null) windAudioSource.Stop();
            if (clothAudioSource != null) clothAudioSource.Stop();
            if (hissAudioSource != null) hissAudioSource.Stop();
            if (heartbeatAudioSource != null) heartbeatAudioSource.Stop();
            if (dustParticles != null) dustParticles.Stop();

            // Snap camera in one single frame to ground pose (Land_Lying) - NO BOUNCE OR FLOAT
            if (mainCamera != null)
            {
                mainCamera.transform.position = lyingPos;
                mainCamera.transform.rotation = lyingRot;
            }

            // Play impact sound (heavy thud, sub boom, crunch, debris rattle)
            if (enableSound && sfxAudioSource != null && impactClip != null)
            {
                sfxAudioSource.PlayOneShot(impactClip, 1.0f);
            }

            // Near-white flash for 2 to 3 frames
            if (flashOverlay != null)
            {
                flashOverlay.color = new Color(0.98f, 0.98f, 1f, 0.96f);
            }
            if (blackOverlay != null) blackOverlay.alpha = 0f;

            for (int f = 0; f < flashFrames; f++)
            {
                yield return null;
            }

            // Cut to solid black
            currentStage = CutsceneStage.ImpactBlack;
            if (flashOverlay != null)
            {
                flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            }
            if (blackOverlay != null) blackOverlay.alpha = 1.0f;
            if (EyelidBlink.Instance != null) EyelidBlink.Instance.SetOpenImmediate(0f);

            // Muffle the world with low-pass filter
            if (enableSound && listenerLowPass != null)
            {
                listenerLowPass.enabled = true;
                listenerLowPass.cutoffFrequency = initialMuffleCutoff; // ~450 Hz
            }

            // Start high-pitched tinnitus ringing
            if (enableSound && tinnitusAudioSource != null && tinnitusClip != null)
            {
                tinnitusAudioSource.clip = tinnitusClip;
                tinnitusAudioSource.volume = 0.75f;
                tinnitusAudioSource.Play();
            }

            // Start slow heavy heartbeat
            if (enableSound && heartbeatAudioSource != null && heartbeatSlowClip != null)
            {
                heartbeatAudioSource.clip = heartbeatSlowClip;
                heartbeatAudioSource.volume = 0.7f;
                heartbeatAudioSource.Play();
            }

            // Silence with tinnitus and slow heartbeat in darkness (~1.5s)
            yield return new WaitForSeconds(1.5f);

            // Fade black overlay out; eyelids remain closed to handle the opening
            if (blackOverlay != null) blackOverlay.alpha = 0f;

            // Start fading tinnitus ringing and opening listener low-pass over 7 seconds
            if (enableSound)
            {
                StartCoroutine(FadeTinnitusAndClearLowPass(tinnitusDuration));
            }
        }

        // ---------------------------------------------------------------------
        // PHASE 4: LYING DAZED (Eyelid blinks: 10%, 40%, 100%)
        // ---------------------------------------------------------------------
        currentStage = CutsceneStage.LyingDazed;

        // BLINK 1: Thin slit (10%) onto blurry tilted ground (5-8 deg roll), then close
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(blink1Open, blink1OpenDuration);
            yield return StartCoroutine(LyingBreathMotion(lyingPos, lyingRot, blink1HoldDuration));
            yield return EyelidBlink.Instance.Close(blink1CloseDuration);
        }
        else
        {
            yield return new WaitForSeconds(blink1OpenDuration + blink1HoldDuration + blink1CloseDuration);
        }

        // Quiet pained exhale
        if (enableSound && sfxAudioSource != null && raggedExhaleClip != null)
        {
            sfxAudioSource.PlayOneShot(raggedExhaleClip, 0.75f);
        }

        yield return StartCoroutine(LyingBreathMotion(lyingPos, lyingRot, blink1RestDuration));

        // Cough or ragged sound
        if (enableSound && sfxAudioSource != null && coughClip != null)
        {
            sfxAudioSource.PlayOneShot(coughClip, 0.8f);
        }

        // BLINK 2: Half open (~40%), hold with wince, close
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(blink2Open, blink2OpenDuration);
            yield return StartCoroutine(LyingBreathMotion(lyingPos, lyingRot, blink2HoldDuration));
            yield return EyelidBlink.Instance.Close(blink2CloseDuration);
        }
        else
        {
            yield return new WaitForSeconds(blink2OpenDuration + blink2HoldDuration + blink2CloseDuration);
        }

        yield return StartCoroutine(LyingBreathMotion(lyingPos, lyingRot, blink2RestDuration));

        // BLINK 3: Full open (100%), clearing
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(blink3Open, blink3OpenDuration);
        }

        // Ragged breathing on the ground looking up and sideways
        yield return StartCoroutine(LyingBreathMotion(lyingPos, lyingRot, blink3RestDuration));

        // ---------------------------------------------------------------------
        // PHASE 5: GETTING UP (Realistic multi-stage motion)
        // ---------------------------------------------------------------------

        Vector3 standCamPos = landStandingPose != null ? landStandingPose.position : (player != null ? player.transform.position + Vector3.up * 1.75f : new Vector3(25.31f, 1.75f, 9.62f));
        Quaternion standRot = landStandingPose != null ? landStandingPose.rotation : (player != null ? player.transform.rotation : Quaternion.identity);
        float baseYaw = standRot.eulerAngles.y;
        Quaternion yawRot = Quaternion.Euler(0f, baseYaw, 0f);

        // 1. Roll: camera rolls from lying on back to side, with strained grunt
        currentStage = CutsceneStage.Rolling;
        if (enableSound && sfxAudioSource != null && strainedGruntClip != null)
        {
            sfxAudioSource.PlayOneShot(strainedGruntClip, 0.85f);
        }

        Vector3 rollPos = lyingPos + yawRot * new Vector3(0.05f, 0.06f, 0f);
        Quaternion rollRot = Quaternion.Euler(-25f, baseYaw + 5f, -40f);
        yield return StartCoroutine(AnimateMotionStep(lyingPos, lyingRot, rollPos, rollRot, rollDuration, easeInOutCurve));

        // 2. Push up: camera lifts to hand height, wobbles, slips down slightly, then pushes up firmly
        currentStage = CutsceneStage.PushingUp;
        if (enableSound && sfxAudioSource != null && handScrapeClip != null)
        {
            sfxAudioSource.PlayOneShot(handScrapeClip, 0.8f);
        }

        Vector3 pushTarget = lyingPos + yawRot * new Vector3(0.12f, 0.42f, 0f);
        Quaternion pushRot = Quaternion.Euler(15f, baseYaw + 2f, -15f);
        yield return StartCoroutine(AnimatePushUpWithSlip(rollPos, rollRot, pushTarget, pushRot, pushUpDuration));

        // 3. Kneel: rises to Land_Kneeling, head hanging low, pause with small sway as if dizzy
        currentStage = CutsceneStage.Kneeling;
        Vector3 kneelPos = landKneelingPose != null ? landKneelingPose.position : (lyingPos + Vector3.up * 0.68f);
        Quaternion kneelRot = landKneelingPose != null ? landKneelingPose.rotation : Quaternion.Euler(28f, baseYaw - 2f, 2f);

        yield return StartCoroutine(AnimateMotionStep(pushTarget, pushRot, kneelPos, kneelRot, kneelDuration * 0.6f, easeInOutCurve));
        // Pause and dizzy sway in kneeling position
        yield return StartCoroutine(KneelingDizzySway(kneelPos, kneelRot, kneelDuration * 0.4f));

        // 4. Stand: slow rise with stumble/step correction, settling bob, looks up and around once
        currentStage = CutsceneStage.Standing;
        yield return StartCoroutine(AnimateStandWithStumble(kneelPos, kneelRot, standCamPos, standRot, standDuration));

        // 5. Hand off control
        FinishAndHandoverControl(standCamPos, standRot);
    }

    // -------------------------------------------------------------------------
    // PROCEDURAL MOTION ROUTINES
    // -------------------------------------------------------------------------

    private IEnumerator AnimateMotionStep(Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = curve != null ? curve.Evaluate(rawT) : rawT;

            float noiseX = (Mathf.PerlinNoise(Time.time * 2f, 0f) - 0.5f) * handheldNoisePos;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * 2f) - 0.5f) * handheldNoisePos;
            float noiseRot = (Mathf.PerlinNoise(Time.time * 1.5f, 15f) - 0.5f) * handheldNoiseRot;

            Vector3 pos = Vector3.Lerp(p0, p1, t) + new Vector3(noiseX, noiseY, 0f);
            Quaternion rot = Quaternion.Slerp(r0, r1, t) * Quaternion.Euler(0f, 0f, noiseRot);

            if (mainCamera != null)
            {
                mainCamera.transform.position = pos;
                mainCamera.transform.rotation = rot;
            }
            yield return null;
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = p1;
            mainCamera.transform.rotation = r1;
        }
    }

    private IEnumerator AnimatePushUpWithSlip(Vector3 fromPos, Quaternion fromRot, Vector3 targetPos, Quaternion targetRot, float duration)
    {
        // 0.0 - 0.45: lifts up
        // 0.45 - 0.65: slips down slightly (failed first attempt)
        // 0.65 - 1.0: firmly pushes up to full hand height
        float elapsed = 0f;
        Vector3 midHigh = Vector3.Lerp(fromPos, targetPos, 0.7f);
        Vector3 slipPos = Vector3.Lerp(fromPos, targetPos, 0.4f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            Vector3 pos;
            Quaternion rot;

            if (rawT < 0.45f)
            {
                float subT = Mathf.SmoothStep(0f, 1f, rawT / 0.45f);
                pos = Vector3.Lerp(fromPos, midHigh, subT);
                rot = Quaternion.Slerp(fromRot, Quaternion.Slerp(fromRot, targetRot, 0.7f), subT);
            }
            else if (rawT < 0.65f)
            {
                float subT = Mathf.SmoothStep(0f, 1f, (rawT - 0.45f) / 0.20f);
                pos = Vector3.Lerp(midHigh, slipPos, subT);
                rot = Quaternion.Slerp(Quaternion.Slerp(fromRot, targetRot, 0.7f), Quaternion.Slerp(fromRot, targetRot, 0.4f), subT);
            }
            else
            {
                float subT = Mathf.SmoothStep(0f, 1f, (rawT - 0.65f) / 0.35f);
                pos = Vector3.Lerp(slipPos, targetPos, subT);
                rot = Quaternion.Slerp(Quaternion.Slerp(fromRot, targetRot, 0.4f), targetRot, subT);
            }

            float noise = (Mathf.PerlinNoise(Time.time * 3f, 0f) - 0.5f) * handheldNoisePos * 1.5f;

            if (mainCamera != null)
            {
                mainCamera.transform.position = pos + new Vector3(noise, noise, 0f);
                mainCamera.transform.rotation = rot;
            }
            yield return null;
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = targetPos;
            mainCamera.transform.rotation = targetRot;
        }
    }

    private IEnumerator KneelingDizzySway(Vector3 posePos, Quaternion poseRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float swayX = Mathf.Sin(Time.time * 1.8f) * 0.012f;
            float swayZ = Mathf.Cos(Time.time * 1.4f) * 0.008f;
            float swayRot = (Mathf.PerlinNoise(Time.time * 1.2f, 5f) - 0.5f) * 1.8f;

            if (mainCamera != null)
            {
                mainCamera.transform.position = posePos + new Vector3(swayX, 0f, swayZ);
                mainCamera.transform.rotation = poseRot * Quaternion.Euler(0f, swayRot, swayRot * 0.5f);
            }
            yield return null;
        }
    }

    private IEnumerator AnimateStandWithStumble(Vector3 kneelPos, Quaternion kneelRot, Vector3 standPos, Quaternion standRot, float duration)
    {
        float elapsed = 0f;
        bool playedScuff = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);

            // Stumble lurch around 45% of the rise
            float lurch = 0f;
            if (rawT >= 0.38f && rawT <= 0.62f)
            {
                float lurchT = (rawT - 0.38f) / 0.24f;
                lurch = Mathf.Sin(lurchT * Mathf.PI) * 0.08f; // Small sideways stumble

                if (!playedScuff && sfxAudioSource != null && footstepScuffClip != null)
                {
                    playedScuff = true;
                    sfxAudioSource.PlayOneShot(footstepScuffClip, 0.75f);
                }
            }

            // Settling bob as legs stabilize around 75% - 95%
            float bob = 0f;
            if (rawT >= 0.72f)
            {
                float bobT = (rawT - 0.72f) / 0.28f;
                bob = Mathf.Sin(bobT * Mathf.PI * 3f) * Mathf.Exp(-bobT * 3.5f) * 0.02f;
            }

            // Look around slowly once standing: yaw sweeps slightly left then right then settles
            float lookSweep = 0f;
            if (rawT >= 0.65f)
            {
                float sweepT = (rawT - 0.65f) / 0.35f;
                lookSweep = Mathf.Sin(sweepT * Mathf.PI * 2f) * 12f;
            }

            float t = easeInOutCurve != null ? easeInOutCurve.Evaluate(rawT) : Mathf.SmoothStep(0f, 1f, rawT);
            Vector3 pos = Vector3.Lerp(kneelPos, standPos, t) + new Vector3(0f, bob, lurch);
            Quaternion rot = Quaternion.Slerp(kneelRot, standRot, t) * Quaternion.Euler(0f, lookSweep, 0f);

            if (mainCamera != null)
            {
                mainCamera.transform.position = pos;
                mainCamera.transform.rotation = rot;
            }
            yield return null;
        }

        // Final exhale audio as breathing steadies
        if (sfxAudioSource != null && raggedExhaleClip != null)
        {
            sfxAudioSource.PlayOneShot(raggedExhaleClip, 0.6f);
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = standPos;
            mainCamera.transform.rotation = standRot;
        }
    }

    private IEnumerator LyingBreathMotion(Vector3 posePos, Quaternion poseRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float breath = Mathf.Sin(Time.time * breathingSpeed) * breathingBobAmp;
            float noiseX = (Mathf.PerlinNoise(Time.time * 1.1f, 0f) - 0.5f) * handheldNoisePos * 0.5f;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * 1.1f) - 0.5f) * handheldNoisePos * 0.5f;
            float noiseRot = (Mathf.PerlinNoise(Time.time * 0.9f, 8f) - 0.5f) * handheldNoiseRot * 0.5f;

            if (mainCamera != null)
            {
                mainCamera.transform.position = posePos + new Vector3(noiseX, breath + noiseY, 0f);
                mainCamera.transform.rotation = poseRot * Quaternion.Euler(0f, 0f, noiseRot);
            }
            yield return null;
        }
    }

    private IEnumerator FadeTinnitusAndClearLowPass(float duration)
    {
        float elapsed = 0f;
        float startTinnitusVol = tinnitusAudioSource != null ? tinnitusAudioSource.volume : 0.75f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (tinnitusAudioSource != null)
            {
                tinnitusAudioSource.volume = Mathf.Lerp(startTinnitusVol, 0f, t);
            }

            if (listenerLowPass != null)
            {
                listenerLowPass.cutoffFrequency = Mathf.Lerp(initialMuffleCutoff, 22000f, t * t);
            }

            if (heartbeatAudioSource != null && heartbeatAudioSource.isPlaying)
            {
                heartbeatAudioSource.volume = Mathf.Lerp(0.7f, 0f, t);
            }

            yield return null;
        }

        if (tinnitusAudioSource != null) tinnitusAudioSource.Stop();
        if (heartbeatAudioSource != null) heartbeatAudioSource.Stop();
        if (listenerLowPass != null)
        {
            listenerLowPass.cutoffFrequency = 22000f;
            listenerLowPass.enabled = false;
        }
    }

    // -------------------------------------------------------------------------
    // SHORT VERSION ROUTINE (If seen this session)
    // -------------------------------------------------------------------------

    private IEnumerator ShortCutsceneRoutine()
    {
        isCinematicActive = true;
        currentStage = CutsceneStage.LyingDazed;

        if (fpc != null)
        {
            fpc.enabled = false;
            fpc.playerCanMove = false;
            fpc.cameraCanMove = false;
            fpc.SetCrosshairVisible(false);
        }

        var crosshairCanvas = GameObject.Find("CrosshairAndStamina");
        if (crosshairCanvas != null) crosshairCanvas.SetActive(false);

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(false);

        if (mainCamera != null)
        {
            originalNearClip = mainCamera.nearClipPlane;
            originalFOV = mainCamera.fieldOfView;
            mainCamera.nearClipPlane = 0.03f;
        }

        if (blackOverlay != null) blackOverlay.alpha = 0f;

        Vector3 lyingPos = landLyingPose != null ? landLyingPose.position : new Vector3(25.31f, 0.22f, 9.62f);
        Quaternion lyingRot = landLyingPose != null ? landLyingPose.rotation : Quaternion.Euler(-65f, 90f, 7f);

        if (mainCamera != null)
        {
            mainCamera.transform.position = lyingPos;
            mainCamera.transform.rotation = lyingRot;
        }

        if (EyelidBlink.Instance != null)
        {
            EyelidBlink.Instance.SetOpenImmediate(0f);
            yield return EyelidBlink.Instance.OpenSlow(1.0f, 1.2f);
        }

        Vector3 standCamPos = landStandingPose != null ? landStandingPose.position : (player != null ? player.transform.position + Vector3.up * 1.75f : new Vector3(25.31f, 1.75f, 9.62f));
        Quaternion standRot = landStandingPose != null ? landStandingPose.rotation : (player != null ? player.transform.rotation : Quaternion.identity);
        float baseYaw = standRot.eulerAngles.y;

        Vector3 kneelPos = landKneelingPose != null ? landKneelingPose.position : (lyingPos + Vector3.up * 0.68f);
        Quaternion kneelRot = landKneelingPose != null ? landKneelingPose.rotation : Quaternion.Euler(28f, baseYaw - 2f, 2f);

        yield return StartCoroutine(AnimateMotionStep(lyingPos, lyingRot, kneelPos, kneelRot, 1.0f, easeInOutCurve));
        yield return StartCoroutine(AnimateMotionStep(kneelPos, kneelRot, standCamPos, standRot, 1.4f, easeInOutCurve));

        FinishAndHandoverControl(standCamPos, standRot);
    }

    // -------------------------------------------------------------------------
    // QUICK FINISH ROUTINE (On Skip)
    // -------------------------------------------------------------------------

    private IEnumerator QuickFinishRoutine()
    {
        // Stop audio
        if (windAudioSource != null) windAudioSource.Stop();
        if (clothAudioSource != null) clothAudioSource.Stop();
        if (hissAudioSource != null) hissAudioSource.Stop();
        if (heartbeatAudioSource != null) heartbeatAudioSource.Stop();
        if (sfxAudioSource != null) sfxAudioSource.Stop();
        if (tinnitusAudioSource != null) tinnitusAudioSource.Stop();
        if (dustParticles != null) dustParticles.Stop();

        // Restore overlays
        if (blackOverlay != null) blackOverlay.alpha = 0f;
        if (flashOverlay != null) flashOverlay.color = new Color(1f, 1f, 1f, 0f);
        if (EyelidBlink.Instance != null) EyelidBlink.Instance.SetOpenImmediate(1f);

        if (listenerLowPass != null)
        {
            listenerLowPass.cutoffFrequency = 22000f;
            listenerLowPass.enabled = false;
        }

        Vector3 standCamPos = landStandingPose != null ? landStandingPose.position : new Vector3(25.31f, 1.75f, 9.62f);
        Quaternion standRot = landStandingPose != null ? landStandingPose.rotation : Quaternion.Euler(0f, 90f, 0f);

        FinishAndHandoverControl(standCamPos, standRot);
        yield break;
    }

    // -------------------------------------------------------------------------
    // CRITICAL CONTROL HANDOFF (Zero snap, zero 180 flip)
    // -------------------------------------------------------------------------

    public void FinishAndHandoverControl(Vector3 standCamPos, Quaternion standRot)
    {
        // 1. Calculate player rig floor position
        // Camera eye height is offset by Joint (0.75m)
        float eyeHeight = 0.75f;
        if (player != null)
        {
            var joint = player.transform.Find("Joint");
            if (joint != null) eyeHeight = joint.position.y - player.transform.position.y;
        }
        Vector3 playerFloorPos = new Vector3(standCamPos.x, standCamPos.y - eyeHeight, standCamPos.z);

        // 2. Align player rig transform
        if (player != null)
        {
            player.transform.position = playerFloorPos;
            player.transform.rotation = Quaternion.Euler(0f, standRot.eulerAngles.y, 0f);
        }

        // 3. Restore camera transform and lens properties
        if (mainCamera != null)
        {
            mainCamera.nearClipPlane = originalNearClip;
            mainCamera.fieldOfView = originalFOV;
            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.transform.localRotation = Quaternion.Euler(standRot.eulerAngles.x, 0f, 0f);
        }

        // 4. CRITICAL: Synchronize internal controller yaw and pitch in FirstPersonController's own variables
        if (fpc != null)
        {
            fpc.SetViewAngles(standRot.eulerAngles.y, standRot.eulerAngles.x);
            fpc.SnapViewRotation(Quaternion.Euler(0f, standRot.eulerAngles.y, 0f));

            fpc.enabled = true;
            fpc.SetControlLocked(false);
            fpc.playerCanMove = true;
            fpc.cameraCanMove = true;
            fpc.SetCrosshairVisible(true);
        }

        // 5. Restore HUD and mobile controls
        var crosshairCanvas = GameObject.Find("CrosshairAndStamina");
        if (crosshairCanvas != null) crosshairCanvas.SetActive(true);

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(true);

        if (skipButtonUI != null)
            skipButtonUI.SetActive(false);

        if (!Application.isMobilePlatform && !Story.MobileUIAdapter.IsMobile)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 6. Show objective using the project's existing objective UI
        ShowCutsceneObjective();

        // 7. Update status
        isCinematicActive = false;
        currentStage = CutsceneStage.Completed;
        HasSeenCutsceneThisSession = true;
    }

    private void ShowCutsceneObjective()
    {
        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.SetObjective("obj_find_teddy", "Find Teddy...");
            return;
        }

        string objectiveKey = "fall_objective_find_way_out";
        string fallbackText = "Find a way out";
        string locText = fallbackText;
        if (LocalizationManager.Instance != null)
        {
            locText = LocalizationManager.Resolve(objectiveKey, fallbackText);
        }

        if (objectiveUI != null)
        {
            objectiveUI.SetObjective(locText);
            objectiveUI.gameObject.SetActive(true);
        }
        else
        {
            var txtObj = GameObject.Find("StoryObjectiveText");
            if (txtObj != null)
            {
                var tmp = txtObj.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    LocalizationManager.ApplyText(tmp, locText, true);
                    tmp.gameObject.SetActive(true);
                }
            }
        }
    }
}
