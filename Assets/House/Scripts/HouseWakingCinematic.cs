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

    public enum CinematicStage
    {
        Init,
        Black,
        SlitOpen,
        HalfOpen,
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

    [Header("Timings (Total ~13-15s)")]
    public float ringStartDuration = 1.2f;
    public float attempt1OpenDuration = 1.5f;
    public float attempt1CloseDuration = 1.0f;
    public float attempt2OpenDuration = 1.8f;
    public float attempt2CloseDuration = 0.9f;
    public float attempt3OpenDuration = 2.0f;
    public float lookClockDuration = 1.2f;
    public float sitUpDuration = 2.0f;
    public float standUpDuration = 1.8f;

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
            mainCamera.nearClipPlane = 0.05f;
        }

        // Close eyelids to pure black
        if (EyelidBlink.Instance != null)
            EyelidBlink.Instance.SetOpenImmediate(0f);

        // Snap player and camera to Lying pose
        Vector3 lyingPos = wakePoseLying != null ? wakePoseLying.position : new Vector3(104.70f, 7.15f, 118.90f);
        Quaternion lyingRot = wakePoseLying != null ? wakePoseLying.rotation : Quaternion.Euler(-30f, 45f, 4f);

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

        // 2. Alarm starts ringing (muffled low-pass 650 Hz)
        if (alarmAudioSource != null)
        {
            if (alarmClip != null) alarmAudioSource.clip = alarmClip;
            alarmAudioSource.loop = true;
            if (alarmLowPass != null)
            {
                alarmLowPass.enabled = true;
                alarmLowPass.cutoffFrequency = 650f;
            }
            alarmAudioSource.Play();
        }

        yield return new WaitForSeconds(ringStartDuration);

        // 3. Attempt 1: eyelids crack open thin slit (12-15%), blurry and bright, then close slowly
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(0.14f, attempt1OpenDuration);
            CurrentStage = CinematicStage.SlitOpen;
            yield return new WaitForSeconds(0.4f);
            yield return EyelidBlink.Instance.Close(attempt1CloseDuration);
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        yield return new WaitForSeconds(0.5f);

        // 4. Attempt 2: open further (~45%), alarm gets clearer (low-pass 2400 Hz), heavy blink closes
        if (alarmLowPass != null) alarmLowPass.cutoffFrequency = 2400f;

        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(0.45f, attempt2OpenDuration);
            CurrentStage = CinematicStage.HalfOpen;
            yield return new WaitForSeconds(0.5f);
            yield return EyelidBlink.Instance.Blink(attempt2CloseDuration);
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        yield return new WaitForSeconds(0.3f);

        // 5. Attempt 3: open fully (100%), blur clears, low-pass disabled
        if (alarmLowPass != null) alarmLowPass.cutoffFrequency = 22000f;

        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.OpenSlow(1.0f, attempt3OpenDuration);
        }

        // Camera slowly turns toward nightstand and clock
        Vector3 clockPos = alarmAudioSource != null ? alarmAudioSource.transform.position : (lyingPos + new Vector3(1f, 0.2f, 1f));
        Vector3 dirToClock = (clockPos - lyingPos).normalized;
        Quaternion lookClockRot = Quaternion.LookRotation(dirToClock, Vector3.up);

        yield return StartCoroutine(AnimateCameraPose(lyingPos, lyingRot, lyingPos, lookClockRot, lookClockDuration));
        CurrentStage = CinematicStage.ClockReach;

        // 6. Player reaches clock: click sound, alarm stops, sleepy breath, morning ambience fades in
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

        yield return new WaitForSeconds(0.6f);

        // 7. Rise to WakePose_Sitting (with cloth rustle)
        if (clothRustleClip != null && alarmAudioSource != null)
        {
            alarmAudioSource.PlayOneShot(clothRustleClip, 0.7f);
        }

        Vector3 sitPos = wakePoseSitting != null ? wakePoseSitting.position : new Vector3(104.05f, 7.65f, 119.65f);
        Quaternion sitRot = wakePoseSitting != null ? wakePoseSitting.rotation : Quaternion.Euler(0f, 323f, 0f);

        yield return StartCoroutine(AnimateCameraPose(lyingPos, lookClockRot, sitPos, sitRot, sitUpDuration));
        CurrentStage = CinematicStage.Sitting;

        yield return new WaitForSeconds(0.5f);

        // 8. Rise to WakePose_Standing (standing next to bed on floor)
        Vector3 standFloor = wakePoseStanding != null ? wakePoseStanding.position : new Vector3(103.50f, 7.094f, 120.20f);
        Quaternion standRot = wakePoseStanding != null ? wakePoseStanding.rotation : Quaternion.Euler(0f, 325f, 0f);
        float eyeHeight = 0.45f;
        if (player != null)
        {
            var joint = player.transform.Find("Joint");
            if (joint != null) eyeHeight = joint.position.y - player.transform.position.y;
        }
        Vector3 standCamPos = standFloor + Vector3.up * eyeHeight;

        yield return StartCoroutine(AnimateCameraPose(sitPos, sitRot, standCamPos, standRot, standUpDuration));
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

        Vector3 sitPos = wakePoseSitting != null ? wakePoseSitting.position : new Vector3(104.05f, 7.65f, 119.65f);
        Quaternion sitRot = wakePoseSitting != null ? wakePoseSitting.rotation : Quaternion.Euler(0f, 323f, 0f);

        if (mainCamera != null)
        {
            mainCamera.transform.position = sitPos;
            mainCamera.transform.rotation = sitRot;
        }

        yield return new WaitForSeconds(0.3f);

        if (EyelidBlink.Instance != null)
            yield return EyelidBlink.Instance.OpenSlow(1.0f, 1.2f);

        Vector3 standFloor = wakePoseStanding != null ? wakePoseStanding.position : new Vector3(103.50f, 7.20f, 120.20f);
        Quaternion standRot = wakePoseStanding != null ? wakePoseStanding.rotation : Quaternion.Euler(0f, 325f, 0f);
        float eyeHeight = 0.75f;
        if (player != null)
        {
            var joint = player.transform.Find("Joint");
            if (joint != null) eyeHeight = joint.localPosition.y;
        }
        Vector3 standCamPos = standFloor + Vector3.up * eyeHeight;

        yield return StartCoroutine(AnimateCameraPose(sitPos, sitRot, standCamPos, standRot, 1.2f));

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
}
