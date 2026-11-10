using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the train movement through the metro station:
/// - Preloads and streams audio on background threads with zero hitching.
/// - Performs the initial pass after an inspector-configured delay.
/// - Resets off-screen and triggers recurring passes periodically ("from while to while").
/// - Integrates directional 3D audio, fading, horn cues, and station PA announcements.
/// </summary>
public class TrainArrivalSequence : MonoBehaviour
{
    [Header("Points")]
    public Transform pointA;
    public Transform pointB;

    [Header("Timing")]
    [Tooltip("Delay in seconds before the first pass begins.")]
    public float delayBeforeMove = 3f;
    [Tooltip("Speed of train movement along the tracks.")]
    public float speed = 20f;

    [Header("Recurring Passes")]
    [Tooltip("If true, the train resets to Point A after exiting and passes again periodically.")]
    public bool loopPasses = true;
    [Tooltip("Minimum wait in seconds between train passes.")]
    public float minIntervalBetweenPasses = 45f;
    [Tooltip("Maximum wait in seconds between train passes.")]
    public float maxIntervalBetweenPasses = 75f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioSource hornAudioSource;
    public AudioClip arrivalSound;   // plays at initial start (optional)
    public AudioClip moveSound;      // continuous rail loop (Railway.mp3)
    public AudioClip hornSound;      // train horn cue (TrainHorns.wav)
    [Range(0f, 1f)] public float moveVolume = 0.85f;
    [Range(0f, 1f)] public float hornVolume = 0.90f;
    [Tooltip("Distance threshold along path to trigger train horn.")]
    public float hornTriggerDistance = 80f;

    [Header("Diagnostics")]
    [SerializeField] private bool isMoving = false;
    [SerializeField] private bool hasArrived = false;
    [SerializeField] private float timeUntilNextPass = 0f;
    [SerializeField] private int passCount = 0;

    private bool hasBlownHorn = false;
    private Coroutine passLoopCoroutine;
    private Coroutine audioFadeCoroutine;
    private Rigidbody cachedRb;
    private readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

    public bool IsMoving => isMoving;
    public int PassCount => passCount;
    public float TimeUntilNextPass => timeUntilNextPass;

    private void Awake()
    {
        // 1. Preload audio clips to guarantee zero main-thread decompression stalls
        PrewarmAudioClips();

        // 2. Add kinematic Rigidbody if missing to eliminate static-collider physics hierarchy recalculation stalls
        EnsureKinematicRigidbody();

        // 3. Ensure AudioSources are properly set up for 3D spatialized railway sound
        SetupAudioSources();
    }

    private void Start()
    {
        if (pointA != null)
        {
            SnapToPosition(pointA.position);
        }

        if (audioSource != null && arrivalSound != null)
        {
            audioSource.clip = arrivalSound;
            audioSource.loop = false;
            audioSource.Play();
        }

        // Start pass loop
        passLoopCoroutine = StartCoroutine(PassSequenceRoutine());
    }

    private void PrewarmAudioClips()
    {
        if (moveSound != null && moveSound.loadState == AudioDataLoadState.Unloaded)
        {
            moveSound.LoadAudioData();
        }
        if (hornSound != null && hornSound.loadState == AudioDataLoadState.Unloaded)
        {
            hornSound.LoadAudioData();
        }
        if (arrivalSound != null && arrivalSound.loadState == AudioDataLoadState.Unloaded)
        {
            arrivalSound.LoadAudioData();
        }
    }

    private void EnsureKinematicRigidbody()
    {
        cachedRb = GetComponent<Rigidbody>();
        if (cachedRb == null)
        {
            cachedRb = gameObject.AddComponent<Rigidbody>();
        }
        cachedRb.isKinematic = true;
        cachedRb.useGravity = false;
        cachedRb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void SnapToPosition(Vector3 targetPos)
    {
        if (cachedRb != null)
        {
            cachedRb.interpolation = RigidbodyInterpolation.None;
            cachedRb.position = targetPos;
            transform.position = targetPos;
            Physics.SyncTransforms();
        }
        else
        {
            transform.position = targetPos;
        }
    }

    private void SetupAudioSources()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1.0f; // Full 3D audio for realistic track sound
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 15f;
        audioSource.maxDistance = 150f;
        audioSource.dopplerLevel = 0.8f;

        if (hornAudioSource == null)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length > 1)
            {
                hornAudioSource = sources[1];
            }
            else
            {
                hornAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        hornAudioSource.playOnAwake = false;
        hornAudioSource.loop = false;
        hornAudioSource.spatialBlend = 1.0f;
        hornAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        hornAudioSource.minDistance = 25f;
        hornAudioSource.maxDistance = 200f;
        hornAudioSource.dopplerLevel = 1.0f;
    }

    private IEnumerator PassSequenceRoutine()
    {
        // First Pass Initial Delay
        timeUntilNextPass = delayBeforeMove;
        while (timeUntilNextPass > 0f)
        {
            // Give pre-arrival announcement warning 7 seconds before departure
            if (timeUntilNextPass <= 7f && timeUntilNextPass + Time.deltaTime > 7f)
            {
                NotifyPreArrival();
            }

            yield return null;
            timeUntilNextPass -= Time.deltaTime;
        }

        // Execute first pass
        yield return StartCoroutine(ExecuteSinglePass());

        // Recurring passes ("from while to while")
        while (loopPasses)
        {
            float interval = Random.Range(minIntervalBetweenPasses, maxIntervalBetweenPasses);
            timeUntilNextPass = interval;

            bool preArrivalFired = false;
            while (timeUntilNextPass > 0f)
            {
                if (!preArrivalFired && timeUntilNextPass <= 8f)
                {
                    preArrivalFired = true;
                    NotifyPreArrival();
                }

                yield return null;
                timeUntilNextPass -= Time.deltaTime;
            }

            yield return StartCoroutine(ExecuteSinglePass());
        }
    }

    private IEnumerator ExecuteSinglePass()
    {
        if (pointA == null || pointB == null) yield break;

        // Snap cleanly to start point offscreen with interpolation cleared so we never streak across the map
        SnapToPosition(pointA.position);
        yield return waitForFixedUpdate;
        if (cachedRb != null)
        {
            cachedRb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        hasArrived = false;
        hasBlownHorn = false;
        isMoving = true;
        passCount++;

        // Start movement sound with smooth fade-in
        StartMoveAudio();

        float totalDistance = Vector3.Distance(pointA.position, pointB.position);
        float midDistance = totalDistance * 0.45f;
        Vector3 currentPos = pointA.position;

        while (isMoving && !hasArrived)
        {
            yield return waitForFixedUpdate;

            currentPos = Vector3.MoveTowards(currentPos, pointB.position, speed * Time.fixedDeltaTime);
            if (cachedRb != null)
            {
                cachedRb.MovePosition(currentPos);
            }
            else
            {
                transform.position = currentPos;
            }

            float currentDistFromStart = Vector3.Distance(currentPos, pointA.position);

            // Trigger horn as train approaches the platform
            if (!hasBlownHorn && currentDistFromStart >= midDistance)
            {
                hasBlownHorn = true;
                PlayHorn();
            }

            // Check if arrived at end tunnel point
            if (Vector3.Distance(currentPos, pointB.position) < 0.2f)
            {
                hasArrived = true;
                isMoving = false;
            }
        }

        // Stop audio with smooth fade-out
        StopMoveAudio();

        // Snap back to Point A deep in tunnel so train is hidden during the wait
        SnapToPosition(pointA.position);
    }

    private void StartMoveAudio()
    {
        if (audioSource != null && moveSound != null)
        {
            if (audioFadeCoroutine != null) StopCoroutine(audioFadeCoroutine);
            audioFadeCoroutine = StartCoroutine(FadeAudio(audioSource, moveSound, 0f, moveVolume, 1.2f));
        }
    }

    private void StopMoveAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            if (audioFadeCoroutine != null) StopCoroutine(audioFadeCoroutine);
            audioFadeCoroutine = StartCoroutine(FadeAudio(audioSource, null, audioSource.volume, 0f, 1.5f));
        }
    }

    private IEnumerator FadeAudio(AudioSource src, AudioClip clip, float startVol, float targetVol, float duration)
    {
        if (clip != null)
        {
            src.clip = clip;
            src.volume = startVol;
            if (!src.isPlaying) src.Play();
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            src.volume = Mathf.Lerp(startVol, targetVol, elapsed / duration);
            yield return null;
        }

        src.volume = targetVol;
        if (targetVol <= 0.001f)
        {
            src.Stop();
        }
    }

    private void PlayHorn()
    {
        if (hornSound == null) return;

        if (hornAudioSource != null)
        {
            hornAudioSource.pitch = Random.Range(0.96f, 1.04f);
            hornAudioSource.PlayOneShot(hornSound, hornVolume);
        }
        else if (audioSource != null)
        {
            audioSource.PlayOneShot(hornSound, hornVolume);
        }
    }

    private void NotifyPreArrival()
    {
        var metroAudio = MetroSoundSystem.Instance;
        if (metroAudio != null)
        {
            metroAudio.OnTrainApproaching();
        }
    }

    [ContextMenu("Trigger Train Pass Now")]
    public void TriggerPassNow()
    {
        if (isMoving) return;

        if (passLoopCoroutine != null) StopCoroutine(passLoopCoroutine);
        timeUntilNextPass = 0f;
        passLoopCoroutine = StartCoroutine(TriggerImmediatePassRoutine());
    }

    private IEnumerator TriggerImmediatePassRoutine()
    {
        yield return StartCoroutine(ExecuteSinglePass());
        if (loopPasses)
        {
            passLoopCoroutine = StartCoroutine(PassSequenceRoutine());
        }
    }

    // Kept for backward compatibility with external calls / invokes
    public void BeginMove()
    {
        TriggerPassNow();
    }
}
