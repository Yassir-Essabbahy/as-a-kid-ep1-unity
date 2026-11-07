using UnityEngine;
using System.Collections;

/// <summary>
/// Cinematic wake-up sequence for the kid's bedroom:
/// - Uses named scene pose transforms (WakePose_Lying, WakePose_Sitting, WakePose_Standing).
/// - Integrates organic EyelidBlink system (slits, drowsy blinks, heavy-eyes wash).
/// - Low-pass filtered alarm audio that clears as eyes open.
/// - Clock button click and sleepy breath on waking.
/// - Camera motion with ease-in/out and subtle handheld Perlin noise.
/// - Adjusts near-clip plane during bed sequence to avoid blanket/pillow clipping.
/// - Seamless handoff syncing controller's internal yaw & pitch variables (no 180° flip).
/// - Skip feature for replayability (tap or short version if already seen).
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
    public float ringStartDuration = 2.0f;
    public float blink1OpenDuration = 1.8f;
    public float blink1HoldDuration = 1.0f;
    public float blink1CloseDuration = 1.3f;
    public float blink1RestDuration = 1.0f;

    public float blink2OpenDuration = 2.0f;
    public float blink2HoldDuration = 1.2f;
    public float blink2CloseDuration = 1.2f;
    public float blink2RestDuration = 0.8f;

    public float blink3OpenDuration = 2.2f;
    public float blink3RestDuration = 1.2f;

    [Header("Timings (Getting Up - approx 5s total)")]
    [Tooltip("Total duration for getting up from bed to standing (approx 5s)")]
    public float getUpDuration = 5.0f;
    public float sitUpFraction = 0.52f;    // ~2.6s
    public float sitPauseFraction = 0.12f;  // ~0.6s
    public float standUpFraction = 0.36f;   // ~1.8s

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

        FindSceneReferences();
    }

    private void Start()
    {
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
        if (alarmClip == null)
            alarmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/AlarmClockRing.wav");
        if (clockClickClip == null)
            clockClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/ClockClick.wav");
        if (morningBreathClip == null)
            morningBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/MorningBreath.wav");
        if (morningAmbienceClip == null)
            morningAmbienceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/MorningAmbience.wav");
        if (clothRustleClip == null)
            clothRustleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/ClothRustle.wav");
#endif
    }

    private void Update()
    {
        if (!isCinematicActive) return;

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

        // 1. Lock controls and hide mobile controls
        if (fpc != null)
        {
            fpc.enabled = false;
            fpc.playerCanMove = false;
            fpc.cameraCanMove = false;
        }

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(false);

        // Adjust near clip plane so pillow/blanket does not clip
        if (mainCamera != null)
        {
            originalNearClip = mainCamera.nearClipPlane;
            mainCamera.nearClipPlane = 0.03f;
        }

        // Close eyelids to pure black
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

        // Take time on the bed in darkness on the pillow
        yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, ringStartDuration));

        // 3. BLINK 1: Slow sleepy slit open (~22%), hold drowsily on pillow, slowly close
        CurrentStage = CinematicStage.SlitOpen;
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(0.22f, blink1OpenDuration);
            yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, blink1HoldDuration));
            CurrentStage = CinematicStage.Blink1_Close;
            yield return EyelidBlink.Instance.Close(blink1CloseDuration);
        }
        else
        {
            yield return new WaitForSeconds(blink1OpenDuration + blink1HoldDuration + blink1CloseDuration);
        }

        // Rest on pillow between blinks in darkness
        yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, blink1RestDuration));

        // 4. BLINK 2: Slow half-open (~55%), alarm clearer (1800 Hz), hold on pillow, slowly close
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

        // 5. BLINK 3: Slow full open (100%), heavy eyes clearing, alarm full audio (22000 Hz)
        if (alarmLowPass != null) alarmLowPass.cutoffFrequency = 22000f;

        CurrentStage = CinematicStage.Blink3_Open;
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(1.0f, blink3OpenDuration);
        }

        // Take a little time on the bed looking out across the room from this perspective
        yield return StartCoroutine(RestOnBed(lyingPos, lyingRot, blink3RestDuration));

        // 6. Awake moment: click sound, alarm stops, morning breath sigh, room ambience fades in
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

        // 7. GETTING UP FROM THIS PERSPECTIVE (approx 5s total duration)
        float totalGetUp = Mathf.Max(getUpDuration, 2.0f);
        float sitUpDur = totalGetUp * sitUpFraction;
        float sitPauseDur = totalGetUp * sitPauseFraction;
        float standUpDur = totalGetUp * standUpFraction;

        // Cloth rustle as body begins moving out of bed
        if (clothRustleClip != null && alarmAudioSource != null)
        {
            alarmAudioSource.PlayOneShot(clothRustleClip, 0.7f);
        }

        Vector3 sitPos = wakePoseSitting != null ? wakePoseSitting.position : new Vector3(104.15f, 7.65f, 119.70f);
        Quaternion sitRot = wakePoseSitting != null ? wakePoseSitting.rotation : Quaternion.Euler(0f, 290.00f, 0f);

        // Sit up smoothly from pillow with natural anatomical pitch arc
        yield return StartCoroutine(AnimateSitUp(lyingPos, lyingRot, sitPos, sitRot, sitUpDur));
        CurrentStage = CinematicStage.Sitting;

        // Brief pause sitting upright on the bed edge, gathering thoughts
        if (sitPauseDur > 0.01f)
        {
            yield return StartCoroutine(RestOnBed(sitPos, sitRot, sitPauseDur));
        }

        // 8. Stand up onto floor beside bed
        Vector3 standFloor = wakePoseStanding != null ? wakePoseStanding.position : new Vector3(103.50f, 7.094f, 120.20f);
        Quaternion standRot = wakePoseStanding != null ? wakePoseStanding.rotation : Quaternion.Euler(0f, 325f, 0f);
        float eyeHeight = 0.45f;
        if (player != null)
        {
            var joint = player.transform.Find("Joint");
            if (joint != null) eyeHeight = joint.position.y - player.transform.position.y;
        }
        Vector3 standCamPos = standFloor + Vector3.up * eyeHeight;

        yield return StartCoroutine(AnimateCameraPose(sitPos, sitRot, standCamPos, standRot, standUpDur));
        CurrentStage = CinematicStage.StandingEnd;

        // 9. Complete cinematic and hand off control seamlessly
        FinishAndHandoverControl(standFloor, standRot);
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

        Vector3 sitPos = wakePoseSitting != null ? wakePoseSitting.position : new Vector3(104.15f, 7.65f, 119.70f);
        Quaternion sitRot = wakePoseSitting != null ? wakePoseSitting.rotation : Quaternion.Euler(0f, 290.00f, 0f);

        yield return StartCoroutine(AnimateSitUp(lyingPos, lyingRot, sitPos, sitRot, 1.2f));

        Vector3 standFloor = wakePoseStanding != null ? wakePoseStanding.position : new Vector3(103.50f, 7.094f, 120.20f);
        Quaternion standRot = wakePoseStanding != null ? wakePoseStanding.rotation : Quaternion.Euler(0f, 325f, 0f);
        float eyeHeight = 0.45f;
        if (player != null)
        {
            var joint = player.transform.Find("Joint");
            if (joint != null) eyeHeight = joint.position.y - player.transform.position.y;
        }
        Vector3 standCamPos = standFloor + Vector3.up * eyeHeight;

        yield return StartCoroutine(AnimateCameraPose(sitPos, sitRot, standCamPos, standRot, 1.0f));

        FinishAndHandoverControl(standFloor, standRot);
    }

    private IEnumerator QuickFinishRoutine()
    {
        if (alarmAudioSource != null && alarmAudioSource.isPlaying)
            alarmAudioSource.Stop();

        if (EyelidBlink.Instance != null)
            EyelidBlink.Instance.SetOpenImmediate(1f);

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
        }

        // 4. Restore mobile controls
        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(true);

        // 5. Update objectives
        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.phase = 0;
            HouseObjectiveController.Instance.UpdateObjectiveUI();
        }

        isCinematicActive = false;
        CurrentStage = CinematicStage.Completed;
        HasSeenIntroThisSession = true;
    }

    private IEnumerator AnimateSitUp(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = motionCurve != null ? motionCurve.Evaluate(rawT) : rawT;

            // Subtle biomechanical pitch dip while pushing up off mattress (slight look down at blanket, returning to level)
            float pitchDip = Mathf.Sin(rawT * Mathf.PI) * 7f;

            // Subtle handheld Perlin noise
            float noiseX = (Mathf.PerlinNoise(Time.time * 1.5f, 0f) - 0.5f) * handheldNoisePos;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * 1.5f) - 0.5f) * handheldNoisePos;
            float noiseRot = (Mathf.PerlinNoise(Time.time * 1.2f, 10f) - 0.5f) * handheldNoiseRot;

            Vector3 basePos = Vector3.Lerp(fromPos, toPos, t);

            float yaw = Mathf.LerpAngle(fromRot.eulerAngles.y, toRot.eulerAngles.y, t);
            float pitch = Mathf.LerpAngle(fromRot.eulerAngles.x, toRot.eulerAngles.x, t) + pitchDip;
            float roll = Mathf.LerpAngle(fromRot.eulerAngles.z, toRot.eulerAngles.z, t);
            Quaternion baseRot = Quaternion.Euler(pitch, yaw, roll);

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
