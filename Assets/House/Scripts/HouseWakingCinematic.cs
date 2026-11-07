using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// Cinematic wake-up sequence for the kid's bedroom:
/// - In the first second, the bedroom door is open and Mama is posed at the doorway,
///   looking at the kid in bed, telling him to get up ("Yallah fyeq a wlidi, nod lmdrassa...").
/// - Mama closes the bedroom door, which latches and locks shut.
/// - Mama returns to the kitchen counter to prepare tea/breakfast.
/// - Kid experiences 3 organic eyelid blinks with muffled alarm clearing up.
/// - Reaching for the alarm clock, morning breath sigh, and ambient room tone.
/// - Smooth 5-second continuous Bezier get-up motion from pillow to standing.
/// - Seamless view synchronization with controller yaw & pitch (no 180° flip).
/// - Full mobile and PC support with skip capabilities.
/// </summary>
[DisallowMultipleComponent]
public class HouseWakingCinematic : MonoBehaviour
{
    public static HouseWakingCinematic Instance { get; private set; }
    public static bool HasSeenIntroThisSession = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HasSeenIntroThisSession = false;
    }

    public enum CinematicStage
    {
        Init,
        Black,
        MamaDoorway,  // First second: Mama waking the kid in the doorway
        SlitOpen,     // Blink 1 Open
        Blink1_Close,
        HalfOpen,     // Blink 2 Open
        Blink2_Close,
        Blink3_Open,  // Blink 3 Full Open
        ClockReach,
        Sitting,
        StandingEnd,
        Completed
    }
    public CinematicStage CurrentStage { get; private set; } = CinematicStage.Init;
    public bool IsCinematicActive => isCinematicActive;

    [Header("Named Poses (Auto-Found if Null)")]
    public Transform wakePoseLying;
    public Transform wakePoseSitting;
    public Transform wakePoseStanding;

    [Header("Player & Camera")]
    public GameObject player;
    public Camera mainCamera;
    public Canvas mobileControlsCanvas;

    [Header("Doorway Sequence (Mama Waking)")]
    public GameObject mamaObject;
    public GameObject mamaDoorwayObject;
    public DoorController bedroomDoorCtrl;
    public AudioClip momVoiceClip;
    public AudioClip doorCloseClip;
    public AudioClip doorOpenClip;
    public Vector3 mamaDoorwayPos = new Vector3(101.70f, 6.47f, 122.85f);
    public Quaternion mamaDoorwayRot = Quaternion.Euler(0f, 131.00f, 0f);
    public Vector3 mamaKitchenPos = new Vector3(103.80f, 6.47f, 129.50f);
    public Quaternion mamaKitchenRot = Quaternion.Euler(0f, 160.00f, 0f);
    public float doorOpenAngle = 85f;

    [Header("Alarm Clock & Audio")]
    public AudioSource alarmAudioSource;
    public AudioLowPassFilter alarmLowPass;
    public AudioSource ambientAudioSource;
    public AudioClip alarmClip;
    public AudioClip clockClickClip;
    public AudioClip morningBreathClip;
    public AudioClip morningAmbienceClip;
    public AudioClip clothRustleClip;

    [Header("Timings (Bed Awakening & 3 Slow Blinks)")]
    public float blink1OpenDuration = 0.8f;
    public float mamaSpeechHoldDuration = 3.2f;
    public float doorCloseDuration = 0.9f;
    public float blink1CloseDuration = 1.0f;
    public float blink1RestDuration = 0.8f;

    public float blink2OpenDuration = 2.0f;
    public float blink2HoldDuration = 1.2f;
    public float blink2CloseDuration = 1.2f;
    public float blink2RestDuration = 0.8f;

    public float blink3OpenDuration = 2.2f;
    public float blink3RestDuration = 1.2f;

    [Header("Timings (Getting Up - approx 5s total)")]
    [Tooltip("Total duration for getting up from bed to standing (approx 5s)")]
    public float getUpDuration = 5.0f;

    [Header("Curves & Noise")]
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float handheldNoisePos = 0.008f;
    public float handheldNoiseRot = 0.35f;

    [Header("Skip UI")]
    public GameObject skipPromptUI;

    private FirstPersonController fpc;
    private float originalNearClip = 0.3f;
    private bool isCinematicActive = false;
    private Coroutine cinematicCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CurrentStage = CinematicStage.Init;

        // Ensure cursor is locked and hidden right from awake
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        FindSceneReferences();
    }

    private void Start()
    {
        // Ensure cursor remains locked and hidden
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (HasSeenIntroThisSession)
        {
            cinematicCoroutine = StartCoroutine(ShortWakingRoutine());
        }
        else
        {
            cinematicCoroutine = StartCoroutine(FullWakingSequenceRoutine());
        }
    }

    private void FindSceneReferences()
    {
        if (wakePoseLying == null)
            wakePoseLying = GameObject.Find("WakePose_Lying")?.transform;
        if (wakePoseSitting == null)
            wakePoseSitting = GameObject.Find("WakePose_Sitting")?.transform;
        if (wakePoseStanding == null)
            wakePoseStanding = GameObject.Find("WakePose_Standing")?.transform;

        if (player == null)
            player = GameObject.Find("FirstPersonController");

        if (player != null)
        {
            fpc = player.GetComponent<FirstPersonController>();
            if (mainCamera == null) mainCamera = player.GetComponentInChildren<Camera>();
            if (mobileControlsCanvas == null)
            {
                var m = player.transform.Find("MobileControlsCanvas");
                if (m != null) mobileControlsCanvas = m.GetComponent<Canvas>();
            }
        }

        if (mainCamera == null) mainCamera = Camera.main;

        if (mamaDoorwayObject == null)
        {
            var md = GameObject.Find("Mama_Doorway");
            if (md != null) mamaDoorwayObject = md;
            else
            {
                foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (g.name == "Mama_Doorway" && g.scene.isLoaded)
                    {
                        mamaDoorwayObject = g;
                        break;
                    }
                }
            }
        }

        if (mamaObject == null)
            mamaObject = GameObject.Find("Mama");

        if (bedroomDoorCtrl == null)
        {
            var d5 = GameObject.Find("Door5");
            if (d5 != null) bedroomDoorCtrl = d5.GetComponentInChildren<DoorController>();
        }

        if (alarmAudioSource == null)
        {
            var clock = GameObject.Find("AlarmClock");
            if (clock != null)
            {
                alarmAudioSource = clock.GetComponent<AudioSource>();
                if (alarmAudioSource == null) alarmAudioSource = clock.AddComponent<AudioSource>();
            }
        }

        if (alarmAudioSource != null)
        {
            alarmLowPass = alarmAudioSource.GetComponent<AudioLowPassFilter>();
            if (alarmLowPass == null) alarmLowPass = alarmAudioSource.gameObject.AddComponent<AudioLowPassFilter>();
        }

        if (ambientAudioSource == null)
        {
            ambientAudioSource = GetComponent<AudioSource>();
            if (ambientAudioSource == null) ambientAudioSource = gameObject.AddComponent<AudioSource>();
            ambientAudioSource.playOnAwake = false;
            ambientAudioSource.loop = true;
            ambientAudioSource.spatialBlend = 0f; // 2D ambient room tone
        }

#if UNITY_EDITOR
        if (alarmClip == null || alarmClip.name == "AlarmClockRing")
        {
            var userAlarm = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/alarm.mp3");
            if (userAlarm != null) alarmClip = userAlarm;
            else if (alarmClip == null)
                alarmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/AlarmClockRing.wav");
        }
        if (clockClickClip == null)
            clockClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/ClockClick.wav");
        if (morningBreathClip == null)
            morningBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/MorningBreath.wav");
        if (morningAmbienceClip == null)
            morningAmbienceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/MorningAmbience.wav");
        if (clothRustleClip == null)
            clothRustleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/ClothRustle.wav");
        if (momVoiceClip == null)
            momVoiceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/MomVoice.wav");
        if (doorCloseClip == null)
            doorCloseClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/DoorClose.wav");
        if (doorOpenClip == null)
            doorOpenClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/DoorOpen.wav");
#endif
    }

    private void Update()
    {
        if (!isCinematicActive) return;

        // Keep cursor strictly locked and hidden during cinematic
        if (Cursor.lockState != CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.Locked;
        if (Cursor.visible)
            Cursor.visible = false;

        // Skip detection: Space, Escape, or screen tap
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            SkipCinematic();
        }
    }

    public void SkipCinematic()
    {
        if (!isCinematicActive) return;
        if (cinematicCoroutine != null) StopCoroutine(cinematicCoroutine);
        StartCoroutine(QuickFinishRoutine());
    }

    private IEnumerator FullWakingSequenceRoutine()
    {
        isCinematicActive = true;

        // Lock cursor and hide OS arrow
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 1. Lock controls and hide mobile controls & crosshair UI ("no point")
        if (fpc != null)
        {
            fpc.enabled = false;
            fpc.playerCanMove = false;
            fpc.cameraCanMove = false;
            fpc.SetCrosshairVisible(false);
        }

        var crosshairCanvas = GameObject.Find("CrosshairAndStamina");
        if (crosshairCanvas != null)
            crosshairCanvas.SetActive(false);

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(false);

        // Adjust near clip plane so pillow/blanket does not clip
        if (mainCamera != null)
        {
            originalNearClip = mainCamera.nearClipPlane;
            mainCamera.nearClipPlane = 0.03f;
        }

        // Close eyelids to pure black initially
        if (EyelidBlink.Instance != null)
            EyelidBlink.Instance.SetOpenImmediate(0f);

        // Snap player and camera to Lying pose directly on the pillow from the specified perspective
        Vector3 lyingPos = wakePoseLying != null ? wakePoseLying.position : new Vector3(105.61f, 7.21f, 119.74f);
        Quaternion lyingRot = wakePoseLying != null ? wakePoseLying.rotation : Quaternion.Euler(352.02f, 273.02f, 0f);

        if (player != null)
        {
            player.transform.position = lyingPos;
            player.transform.rotation = Quaternion.Euler(0f, lyingRot.eulerAngles.y, 0f);
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = lyingPos;
            mainCamera.transform.rotation = lyingRot;
        }

        // PREPARE DOORWAY SCENE: Bedroom door is open, Mama is in doorway looking at bed
        if (bedroomDoorCtrl != null)
        {
            bedroomDoorCtrl.isCinematicControlled = true;
            bedroomDoorCtrl.transform.localRotation = Quaternion.Euler(0f, 0f, doorOpenAngle);
        }

        if (mamaDoorwayObject != null)
        {
            mamaDoorwayObject.transform.position = mamaDoorwayPos;
            mamaDoorwayObject.transform.rotation = mamaDoorwayRot;
            mamaDoorwayObject.SetActive(true);
            var doorwayAnim = mamaDoorwayObject.GetComponent<Animator>();
            if (doorwayAnim != null) doorwayAnim.Play("Talking");
        }

        if (mamaObject != null)
        {
            mamaObject.transform.position = mamaKitchenPos;
            mamaObject.transform.rotation = mamaKitchenRot;
            var mamaAnim = mamaObject.GetComponent<Animator>();
            if (mamaAnim != null) mamaAnim.Play("Cooking_Loop");
        }

        yield return new WaitForSeconds(0.2f);
        CurrentStage = CinematicStage.Black;

        // 2. Alarm starts ringing (muffled low-pass 550 Hz through sleep)
        if (alarmAudioSource != null)
        {
            if (alarmClip != null) alarmAudioSource.clip = alarmClip;
            alarmAudioSource.loop = true;
            if (alarmLowPass != null)
            {
                alarmLowPass.enabled = true;
                alarmLowPass.cutoffFrequency = 550f;
            }
            alarmAudioSource.Play();
        }

        // Rest momentarily on bed in darkness (0.5s)
        yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, 0.5f));

        // 3. THE FIRST SECOND: BLINK 1 - Eyes slit open, Mama in doorway tells kid to get up
        CurrentStage = CinematicStage.MamaDoorway;
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(0.40f, blink1OpenDuration);
        }

        // Play Mama's voice: "Yallah fyeq a wlidi, nod lmdrassa..."
        PlayMamaVoice();

        // Show dialogue matching the other scenes with cinematic letterbox borders
        Color motherLilac = new Color(0.77f, 0.71f, 0.99f);
        string mamaLine = "Mama: Yallah fyeq a wlidi, nod lmdrassa...\n<size=75%><color=#D1D5DB><i>(Wake up, sweetheart, it's time for school.)</i></color></size>";
        if (NpcDialogueManager.Instance != null)
        {
            NpcDialogueManager.Instance.ShowInteractableMessage(mamaLine, mamaSpeechHoldDuration, motherLilac);
        }

        // Kid looks at Mama from pillow while she speaks
        yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, mamaSpeechHoldDuration));

        // 4. MAMA STEPS BACK AND CLOSES THE BEDROOM DOOR (Clear of any door collision)
        yield return StartCoroutine(AnimateMamaStepBackAndDoorClose(doorCloseDuration));

        // Doorway Mama is now inactive and kitchen Mama is cooking at the stove

        // Close eyes drowsily (Blink 1 Close)
        CurrentStage = CinematicStage.Blink1_Close;
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.Close(blink1CloseDuration);
        }

        // Rest on pillow between blinks in darkness
        yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, blink1RestDuration));

        // 5. BLINK 2: Slow half-open (~55%), alarm clearer (1800 Hz), hold on pillow, slowly close
        if (alarmLowPass != null) alarmLowPass.cutoffFrequency = 1800f;

        CurrentStage = CinematicStage.HalfOpen;
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(0.55f, blink2OpenDuration);
            yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, blink2HoldDuration));
            CurrentStage = CinematicStage.Blink2_Close;
            yield return EyelidBlink.Instance.Close(blink2CloseDuration);
        }
        else
        {
            yield return new WaitForSeconds(blink2OpenDuration + blink2HoldDuration + blink2CloseDuration);
        }

        // Rest on pillow between blinks in darkness
        yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, blink2RestDuration));

        // 6. BLINK 3: Slow full open (100%), heavy eyes clearing, alarm full audio (22000 Hz)
        if (alarmLowPass != null) alarmLowPass.cutoffFrequency = 22000f;

        CurrentStage = CinematicStage.Blink3_Open;
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(1.0f, blink3OpenDuration);
        }

        // Take a little time on the bed looking out across the room from this perspective
        yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, blink3RestDuration));

        // 7. Awake moment: click sound, alarm stops, morning breath sigh, room ambience fades in
        CurrentStage = CinematicStage.ClockReach;
        Vector3 clockPos = alarmAudioSource != null ? alarmAudioSource.transform.position : new Vector3(105.75f, 7.35f, 120.00f);
        if (alarmAudioSource != null && alarmAudioSource.isPlaying)
        {
            alarmAudioSource.Stop();
            if (clockClickClip != null)
                AudioSource.PlayClipAtPoint(clockClickClip, clockPos, 1.0f);
        }

        if (morningBreathClip != null && alarmAudioSource != null)
        {
            alarmAudioSource.PlayOneShot(morningBreathClip, 0.8f);
        }

        if (ambientAudioSource != null && morningAmbienceClip != null)
        {
            ambientAudioSource.clip = morningAmbienceClip;
            ambientAudioSource.volume = 0f;
            ambientAudioSource.Play();
            StartCoroutine(FadeAmbienceVolume(0.4f, 2.5f));
        }

        // 8. GETTING UP FROM THIS PERSPECTIVE (fluid, continuous 5s get-up motion)
        float totalGetUp = Mathf.Max(getUpDuration, 2.0f);

        // Cloth rustle as body begins moving out of bed
        if (clothRustleClip != null && alarmAudioSource != null)
        {
            alarmAudioSource.PlayOneShot(clothRustleClip, 0.7f);
        }

        Vector3 sitPos = wakePoseSitting != null ? wakePoseSitting.position : new Vector3(104.25f, 7.65f, 119.80f);
        Quaternion sitRot = wakePoseSitting != null ? wakePoseSitting.rotation : Quaternion.Euler(0f, 295.00f, 0f);

        Vector3 standFloor = wakePoseStanding != null ? wakePoseStanding.position : new Vector3(103.50f, 7.094f, 120.20f);
        Quaternion standRot = wakePoseStanding != null ? wakePoseStanding.rotation : Quaternion.Euler(0f, 325f, 0f);
        float eyeHeight = 0.45f;
        if (player != null)
        {
            var joint = player.transform.Find("Joint");
            if (joint != null) eyeHeight = joint.position.y - player.transform.position.y;
        }
        Vector3 standCamPos = standFloor + Vector3.up * eyeHeight;

        // Fluid, continuous 5-second get-up movement through sitting into standing
        yield return StartCoroutine(AnimateSmoothGetUp(lyingPos, lyingRot, sitPos, sitRot, standCamPos, standRot, totalGetUp));

        // 9. Complete cinematic and hand off control seamlessly
        FinishAndHandoverControl(standFloor, standRot);
    }

    private IEnumerator AnimateMamaStepBackAndDoorClose(float duration)
    {
        Quaternion openRot = bedroomDoorCtrl != null ? bedroomDoorCtrl.transform.localRotation : Quaternion.Euler(0f, 0f, doorOpenAngle);
        Quaternion closedRot = Quaternion.identity;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = Mathf.SmoothStep(0f, 1f, rawT);

            // Door rotates smoothly shut without Mama sliding
            if (bedroomDoorCtrl != null)
            {
                bedroomDoorCtrl.transform.localRotation = Quaternion.Slerp(openRot, closedRot, t);
            }

            yield return null;
        }

        // Deactivate Doorway Mama once bedroom door closes
        if (mamaDoorwayObject != null)
        {
            mamaDoorwayObject.SetActive(false);
        }

        if (bedroomDoorCtrl != null)
        {
            bedroomDoorCtrl.transform.localRotation = closedRot;
            bedroomDoorCtrl.isCinematicControlled = false;
            bedroomDoorCtrl.SetOpenImmediate(false);
            bedroomDoorCtrl.isLocked = false;

            // Play door close audio with solid latch
            if (doorCloseClip != null)
            {
                var doorSrc = bedroomDoorCtrl.GetComponent<AudioSource>();
                if (doorSrc == null) doorSrc = bedroomDoorCtrl.gameObject.AddComponent<AudioSource>();
                doorSrc.spatialBlend = 0.8f;
                doorSrc.PlayOneShot(doorCloseClip, 1.0f);
            }
        }
    }

    private void PlayMamaVoice()
    {
        var voiceTarget = (mamaDoorwayObject != null && mamaDoorwayObject.activeInHierarchy) ? mamaDoorwayObject : mamaObject;
        if (voiceTarget == null || momVoiceClip == null) return;

        var src = voiceTarget.GetComponent<AudioSource>();
        if (src == null)
        {
            src = voiceTarget.AddComponent<AudioSource>();
            src.spatialBlend = 0.5f;
            src.minDistance = 2f;
            src.maxDistance = 20f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.playOnAwake = false;
        }
        src.clip = momVoiceClip;
        src.volume = 1.0f;
        src.Play();
    }

    private IEnumerator ShortWakingRoutine()
    {
        isCinematicActive = true;

        if (fpc != null)
        {
            fpc.enabled = false;
            fpc.playerCanMove = false;
            fpc.cameraCanMove = false;
        }

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(false);

        if (EyelidBlink.Instance != null)
            EyelidBlink.Instance.SetOpenImmediate(0f);

        Vector3 lyingPos = wakePoseLying != null ? wakePoseLying.position : new Vector3(105.61f, 7.21f, 119.74f);
        Quaternion lyingRot = wakePoseLying != null ? wakePoseLying.rotation : Quaternion.Euler(352.02f, 273.02f, 0f);

        if (mainCamera != null)
        {
            mainCamera.transform.position = lyingPos;
            mainCamera.transform.rotation = lyingRot;
        }

        yield return new WaitForSeconds(0.3f);

        if (EyelidBlink.Instance != null)
            yield return EyelidBlink.Instance.OpenSlow(1.0f, 1.2f);

        Vector3 sitPos = wakePoseSitting != null ? wakePoseSitting.position : new Vector3(104.25f, 7.65f, 119.80f);
        Quaternion sitRot = wakePoseSitting != null ? wakePoseSitting.rotation : Quaternion.Euler(0f, 295.00f, 0f);

        Vector3 standFloor = wakePoseStanding != null ? wakePoseStanding.position : new Vector3(103.50f, 7.094f, 120.20f);
        Quaternion standRot = wakePoseStanding != null ? wakePoseStanding.rotation : Quaternion.Euler(0f, 325f, 0f);
        float eyeHeight = 0.45f;
        if (player != null)
        {
            var joint = player.transform.Find("Joint");
            if (joint != null) eyeHeight = joint.position.y - player.transform.position.y;
        }
        Vector3 standCamPos = standFloor + Vector3.up * eyeHeight;

        yield return StartCoroutine(AnimateSmoothGetUp(lyingPos, lyingRot, sitPos, sitRot, standCamPos, standRot, 1.8f));

        FinishAndHandoverControl(standFloor, standRot);
    }

    private IEnumerator QuickFinishRoutine()
    {
        if (alarmAudioSource != null && alarmAudioSource.isPlaying)
            alarmAudioSource.Stop();

        if (EyelidBlink.Instance != null)
            EyelidBlink.Instance.SetOpenImmediate(1f);

        if (NpcDialogueManager.Instance != null)
        {
            NpcDialogueManager.Instance.ForceEndDialogue();
        }

        if (bedroomDoorCtrl != null)
        {
            bedroomDoorCtrl.isCinematicControlled = false;
            bedroomDoorCtrl.SetOpenImmediate(false);
            bedroomDoorCtrl.isLocked = false;
        }

        if (mamaDoorwayObject != null)
        {
            mamaDoorwayObject.SetActive(false);
        }

        if (mamaObject != null)
        {
            mamaObject.transform.position = mamaKitchenPos;
            mamaObject.transform.rotation = mamaKitchenRot;
            var mamaAnim = mamaObject.GetComponent<Animator>();
            if (mamaAnim != null) mamaAnim.Play("Cooking_Loop");
        }

        Vector3 standFloor = wakePoseStanding != null ? wakePoseStanding.position : new Vector3(103.50f, 7.20f, 120.20f);
        Quaternion standRot = wakePoseStanding != null ? wakePoseStanding.rotation : Quaternion.Euler(0f, 325f, 0f);

        FinishAndHandoverControl(standFloor, standRot);
        yield break;
    }

    private void FinishAndHandoverControl(Vector3 standFloor, Quaternion standRot)
    {
        // 1. Restore near clip plane
        if (mainCamera != null)
        {
            mainCamera.nearClipPlane = originalNearClip;
            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.transform.localRotation = Quaternion.Euler(standRot.eulerAngles.x, 0f, 0f);
        }

        // 2. Align player rig transform
        if (player != null)
        {
            player.transform.position = standFloor;
            player.transform.rotation = Quaternion.Euler(0f, standRot.eulerAngles.y, 0f);
        }

        // 3. SYNCHRONIZE INTERNAL CONTROLLER YAW & PITCH (Prevents the 180 flip!)
        if (fpc != null)
        {
            fpc.SetViewAngles(standRot.eulerAngles.y, standRot.eulerAngles.x);
            fpc.SnapViewRotation(Quaternion.Euler(0f, standRot.eulerAngles.y, 0f));

            fpc.enabled = true;
            fpc.playerCanMove = true;
            fpc.cameraCanMove = true;
            fpc.SetCrosshairVisible(true);
        }

        // 4. Restore crosshair and mobile controls
        var crosshairCanvas = GameObject.Find("CrosshairAndStamina");
        if (crosshairCanvas != null)
            crosshairCanvas.SetActive(true);

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 5. Update objectives
        if (HouseObjectiveController.Instance != null && !HouseObjectiveController.Instance.hasWashedFace)
        {
            HouseObjectiveController.Instance.phase = 0;
            HouseObjectiveController.Instance.UpdateObjectiveUI();
        }

        isCinematicActive = false;
        CurrentStage = CinematicStage.Completed;
        HasSeenIntroThisSession = true;
    }

    private IEnumerator AnimateSmoothGetUp(Vector3 p0, Quaternion r0, Vector3 pMid, Quaternion rMid, Vector3 p2, Quaternion r2, float duration)
    {
        // Smooth quadratic Bezier control point passing through pMid at u=0.5
        Vector3 p1 = 2f * pMid - 0.5f * (p0 + p2);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);

            // S-curve easing across the entire getting-up motion
            float u = Mathf.SmoothStep(0f, 1f, rawT);

            // Continuous quadratic Bezier position
            Vector3 pos = (1f - u) * (1f - u) * p0 + 2f * (1f - u) * u * p1 + u * u * p2;

            // Continuous spherical rotation interpolation
            Quaternion rot;
            if (u < 0.5f)
            {
                float subU = Mathf.SmoothStep(0f, 1f, u * 2f);
                rot = Quaternion.Slerp(r0, rMid, subU);
            }
            else
            {
                float subU = Mathf.SmoothStep(0f, 1f, (u - 0.5f) * 2f);
                rot = Quaternion.Slerp(rMid, r2, subU);
            }

            if (mainCamera != null)
            {
                mainCamera.transform.position = pos;
                mainCamera.transform.rotation = rot;
            }

            if (rawT >= 0.48f && CurrentStage < CinematicStage.Sitting)
            {
                CurrentStage = CinematicStage.Sitting;
            }

            yield return null;
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = p2;
            mainCamera.transform.rotation = r2;
        }

        CurrentStage = CinematicStage.StandingEnd;
    }

    private IEnumerator AnimateCameraPose(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = motionCurve != null ? motionCurve.Evaluate(rawT) : rawT;

            // Subtle handheld Perlin noise
            float noiseX = (Mathf.PerlinNoise(Time.time * 1.5f, 0f) - 0.5f) * handheldNoisePos;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * 1.5f) - 0.5f) * handheldNoisePos;
            float noiseRot = (Mathf.PerlinNoise(Time.time * 1.2f, 10f) - 0.5f) * handheldNoiseRot;

            Vector3 basePos = Vector3.Lerp(fromPos, toPos, t);
            Quaternion baseRot = Quaternion.Slerp(fromRot, toRot, t);

            if (mainCamera != null)
            {
                mainCamera.transform.position = basePos + new Vector3(noiseX, noiseY, 0f);
                mainCamera.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, noiseRot);
            }

            yield return null;
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = toPos;
            mainCamera.transform.rotation = toRot;
        }
    }

    private IEnumerator FadeAmbienceVolume(float targetVolume, float duration)
    {
        if (ambientAudioSource == null) yield break;
        float elapsed = 0f;
        float startVol = ambientAudioSource.volume;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ambientAudioSource.volume = Mathf.Lerp(startVol, targetVolume, elapsed / duration);
            yield return null;
        }
        ambientAudioSource.volume = targetVolume;
    }

    private IEnumerator RestOnBed(Vector3 posePos, Quaternion poseRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float breath = Mathf.Sin(Time.time * 1.8f) * 0.003f;
            float noiseX = (Mathf.PerlinNoise(Time.time * 1.2f, 0f) - 0.5f) * handheldNoisePos * 0.5f;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * 1.2f) - 0.5f) * handheldNoisePos * 0.5f;
            float noiseRot = (Mathf.PerlinNoise(Time.time * 1.0f, 10f) - 0.5f) * handheldNoiseRot * 0.5f;

            if (mainCamera != null)
            {
                mainCamera.transform.position = posePos + new Vector3(noiseX, breath + noiseY, 0f);
                mainCamera.transform.rotation = poseRot * Quaternion.Euler(0f, 0f, noiseRot);
            }
            yield return null;
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = posePos;
            mainCamera.transform.rotation = poseRot;
        }
    }
}