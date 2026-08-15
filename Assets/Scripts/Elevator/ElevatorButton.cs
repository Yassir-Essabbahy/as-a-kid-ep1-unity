using System.Collections;
using UnityEngine;

public class ElevatorButton : MonoBehaviour
{
    [Header("Interact")]
    public float interactDistance = 2f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Quest Gate")]
    public TeddyFindQuest currentFloorQuest;

    [Header("Doors")]
    public Animator doorAnimator;
    public string closeTrigger = "Close";
    public string openTrigger = "Open";
    public float doorCloseTime = 1.2f;
    public float doorOpenTime = 1.2f;

    [Header("Elevator Audio")]
    public AudioSource audioSource;
    public AudioClip doorCloseSound;
    public AudioClip elevatorSong;
    public AudioClip doorOpenSound;

    [Header("Travel Timing")]
    public float elevatorTravelTime = 2f;

    [Header("False Stop — Train Track")]
    public GameObject voidTrackScene;
    public float falseStopDuration = 3f;
    public float hornDelay = 1.2f;

    [Header("Train Audio")]
    public AudioSource trainAudioSource;
    public AudioClip trainApproachSound;
    public AudioClip trainHornSound;
    public float trainFadeOutTime = 0.8f;

    [Header("Train Light")]
    public Light trainHeadlightFlash;
    public Transform trainLightStart;
    public Transform trainLightEnd;
    public float trainLightTravelTime = 0.8f;
    public float trainLightMaxIntensity = 8f;

    [Header("Shake")]
    public Transform shakeTarget;
    public float shakeIntensity = 0.03f;
    public float shakeSpeed = 20f;

    [Header("Teddy Dialogue After False Stop")]
    public NpcConversation teddyReactionConversation;

    [Header("Player")]
    public Transform playerTransform;

    private bool isRunning;

    public bool CanAcceptDoorZoneRequest => !isRunning;

    public void RequestDoorOpenFromZone()
    {
        if (!CanAcceptDoorZoneRequest) return;
        SetDoors(true);
    }

    void Update()
    {
        if (isRunning) return;
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactDistance && Input.GetKeyDown(interactKey))
        {
            if (currentFloorQuest != null && !currentFloorQuest.questCompleted) return;
            if (FloorManager.Instance == null || !FloorManager.Instance.HasNextFloor()) return;

            StartCoroutine(RunElevator());
        }
    }

    IEnumerator RunElevator()
    {
        isRunning = true;

        // 1. Close doors, travel
        SetDoors(false);
        yield return new WaitForSeconds(doorCloseTime);

        PlayOneShot(audioSource, elevatorSong);
        Coroutine shake = StartCoroutine(Shake(elevatorTravelTime));
        yield return new WaitForSeconds(elevatorTravelTime);
        if (shake != null) StopCoroutine(shake);
        ResetShakeTarget();

        // 2. Open onto the void track (false stop)
        if (voidTrackScene != null) voidTrackScene.SetActive(true);
        SetDoors(true);
        yield return new WaitForSeconds(doorOpenTime);

        shake = StartCoroutine(Shake(falseStopDuration));
        PlayTrainApproach();

        float safeHornDelay = Mathf.Clamp(hornDelay, 0f, falseStopDuration);
        yield return new WaitForSeconds(safeHornDelay);

        PlayOneShot(trainAudioSource != null ? trainAudioSource : audioSource, trainHornSound);
        StartCoroutine(MoveTrainLight());

        float remaining = falseStopDuration - safeHornDelay;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        if (shake != null) StopCoroutine(shake);
        ResetShakeTarget();

        // 3. Close doors again, fade train sound, hide void
        SetDoors(false);
        FadeOutTrain();
        yield return new WaitForSeconds(doorCloseTime);

        StopTrainLight();
        if (voidTrackScene != null) voidTrackScene.SetActive(false);

        // 4. Teddy reacts
        if (teddyReactionConversation != null)
        {
            yield return StartCoroutine(teddyReactionConversation.Play());
        }

        // 5. Real arrival travel + floor swap
        shake = StartCoroutine(Shake(elevatorTravelTime));
        yield return new WaitForSeconds(elevatorTravelTime);
        if (shake != null) StopCoroutine(shake);
        ResetShakeTarget();

        int nextFloor = FloorManager.Instance.currentFloorIndex + 1;
        FloorManager.Instance.ActivateFloor(nextFloor);

        SetDoors(true);
        yield return new WaitForSeconds(doorOpenTime);

        isRunning = false;
    }

    void SetDoors(bool open)
    {
        if (doorAnimator != null)
        {
            doorAnimator.SetTrigger(open ? openTrigger : closeTrigger);
        }
        PlayOneShot(audioSource, open ? doorOpenSound : doorCloseSound);
    }

    void PlayOneShot(AudioSource source, AudioClip clip)
    {
        if (source != null && clip != null) source.PlayOneShot(clip);
    }

    void PlayTrainApproach()
    {
        AudioSource src = trainAudioSource != null ? trainAudioSource : audioSource;
        if (src == null || trainApproachSound == null) return;

        src.Stop();
        src.clip = trainApproachSound;
        src.volume = 1f;
        src.loop = false;
        src.Play();
    }

    void FadeOutTrain()
    {
        if (trainAudioSource == null || !trainAudioSource.isPlaying) return;
        StartCoroutine(FadeOutRoutine());
    }

    IEnumerator FadeOutRoutine()
    {
        float startVol = trainAudioSource.volume;
        float elapsed = 0f;

        while (elapsed < trainFadeOutTime)
        {
            trainAudioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / trainFadeOutTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        trainAudioSource.Stop();
        trainAudioSource.volume = startVol;
    }

    IEnumerator MoveTrainLight()
    {
        if (trainHeadlightFlash == null || trainLightStart == null || trainLightEnd == null) yield break;

        trainHeadlightFlash.enabled = true;
        trainHeadlightFlash.intensity = 0f;

        Quaternion targetRot = Quaternion.LookRotation(trainLightEnd.position - trainLightStart.position);

        float elapsed = 0f;
        while (elapsed < trainLightTravelTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / trainLightTravelTime);
            trainHeadlightFlash.transform.position = Vector3.Lerp(trainLightStart.position, trainLightEnd.position, t);
            trainHeadlightFlash.transform.rotation = Quaternion.Slerp(trainLightStart.rotation, targetRot, t);
            trainHeadlightFlash.intensity = Mathf.Lerp(0f, trainLightMaxIntensity, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        trainHeadlightFlash.enabled = false;
        trainHeadlightFlash.intensity = 0f;
    }

    void StopTrainLight()
    {
        if (trainHeadlightFlash != null)
        {
            trainHeadlightFlash.enabled = false;
            trainHeadlightFlash.intensity = 0f;
        }
    }

    IEnumerator Shake(float duration)
    {
        if (shakeTarget == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector3 original = shakeTarget.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = (Mathf.PerlinNoise(Time.time * shakeSpeed, 0f) - 0.5f) * shakeIntensity;
            float y = (Mathf.PerlinNoise(0f, Time.time * shakeSpeed) - 0.5f) * shakeIntensity;
            shakeTarget.localPosition = original + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        shakeTarget.localPosition = original;
    }

    void ResetShakeTarget()
    {
        // safety hook if a shake coroutine is ever cut early; currently unused
        // since all Shake() calls run to natural completion and self-reset.
    }
}