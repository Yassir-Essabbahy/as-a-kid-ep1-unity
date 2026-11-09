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
    public float hornDelay = 0.5f;

    [Header("Train Visuals and Movement")]
    public Transform voidTrainObject;
    public Vector3 trainStartPosition = new Vector3(-10.0f, 11.25f, 103.04f);
    public Vector3 trainEndPosition = new Vector3(-37.5f, 11.25f, 103.04f);
    public float trainApproachDuration = 2.2f;
    public Light[] trainHeadlights;
    public BoxCollider elevatorDoorBarrier;

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

    public event System.Action OnElevatorRideStarted;
    public event System.Action OnFalseStopTriggered;
    public event System.Action OnArrivalOnNewFloor;

    [Header("Power Grid")]
    public bool isPowered = false;

    void Start()
    {
        if (MetroStorySequenceController.Instance != null)
        {
            isPowered = MetroStorySequenceController.Instance.isPowerRestored;
        }
    }

    public void OnPowerRestored()
    {
        isPowered = true;
    }

    private bool isRunning;

    public bool CanAcceptDoorZoneRequest => !isRunning && 
        (isPowered || (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.isPowerRestored)) &&
        (MetroStorySequenceController.Instance == null || MetroStorySequenceController.Instance.IsElevatorUnlocked);

    public void RequestDoorOpenFromZone()
    {
        if (!CanAcceptDoorZoneRequest) return;
        SetDoors(true);
    }

    public void NotifyDoorBlocked()
    {
        if (isRunning) return;
        if (NpcDialogueManager.Instance != null && !NpcDialogueManager.Instance.IsDialogueRunning)
        {
            bool powerReady = isPowered || (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.isPowerRestored);
            bool hasTicket = MetroStorySequenceController.Instance == null || MetroStorySequenceController.Instance.IsElevatorUnlocked;

            if (!powerReady)
            {
                StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                    new string[] { MetroStorySequenceController.GetLoc("elevator_door_no_power") },
                    false,
                    Color.white,
                    null
                ));
            }
            else if (!hasTicket)
            {
                if (currentFloorQuest == null || currentFloorQuest.questActive)
                {
                    StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                        new string[] { MetroStorySequenceController.GetLoc("elevator_door_no_ticket") },
                        false,
                        Color.white,
                        null
                    ));
                }
            }
        }
    }

    void Update()
    {
        if (isRunning) return;
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactDistance && Input.GetKeyDown(interactKey))
        {
            bool powerReady = isPowered || (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.isPowerRestored);
            bool storyUnlocked = MetroStorySequenceController.Instance == null || MetroStorySequenceController.Instance.IsElevatorUnlocked;
            bool questCompleted = currentFloorQuest == null || currentFloorQuest.questCompleted;

            if (!powerReady)
            {
                if (NpcDialogueManager.Instance != null && !NpcDialogueManager.Instance.IsDialogueRunning)
                {
                    StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                        new string[] { MetroStorySequenceController.GetLoc("elevator_btn_no_power") },
                        false,
                        Color.white,
                        null
                    ));
                }
                return;
            }

            if (!storyUnlocked || !questCompleted)
            {
                if (NpcDialogueManager.Instance != null && !NpcDialogueManager.Instance.IsDialogueRunning)
                {
                    StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                        new string[] { MetroStorySequenceController.GetLoc("elevator_btn_no_ticket") },
                        false,
                        Color.white,
                        null
                    ));
                }
                return;
            }

            if (MetroStorySequenceController.Instance != null && !MetroStorySequenceController.Instance.IsTeddyCarried)
            {
                if (NpcDialogueManager.Instance != null && !NpcDialogueManager.Instance.IsDialogueRunning)
                {
                    StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                        new string[] { MetroStorySequenceController.GetLoc("elevator_btn_no_teddy") },
                        false,
                        Color.white,
                        null
                    ));
                }
                return;
            }

            if (FloorManager.Instance == null || !FloorManager.Instance.HasNextFloor()) return;

            StartCoroutine(RunElevator());
        }
    }

    IEnumerator RunElevator()
    {
        isRunning = true;
        OnElevatorRideStarted?.Invoke();
        if (MetroStorySequenceController.Instance != null)
            MetroStorySequenceController.Instance.OnElevatorStarted();

        // 1. Close doors, travel from Floor 1
        SetDoors(false);
        yield return new WaitForSeconds(doorCloseTime);

        PlayOneShot(audioSource, elevatorSong);
        Coroutine shake = StartCoroutine(Shake(elevatorTravelTime, shakeIntensity, shakeSpeed));
        yield return new WaitForSeconds(elevatorTravelTime);
        if (shake != null) StopCoroutine(shake);
        ResetShakeTarget();

        // 2. Open onto the void track (false stop)
        OnFalseStopTriggered?.Invoke();
        if (MetroStorySequenceController.Instance != null)
            MetroStorySequenceController.Instance.OnElevatorFalseStop();

        // Temporarily hide Floor 1 so the void track / dark tunnel is isolated
        if (FloorManager.Instance != null && FloorManager.Instance.floors != null && FloorManager.Instance.floors.Length > 0)
        {
            if (FloorManager.Instance.floors[0] != null && FloorManager.Instance.floors[0].floorRoot != null)
                FloorManager.Instance.floors[0].floorRoot.SetActive(false);
        }

        ResolveVoidTrainReferences();
        if (elevatorDoorBarrier != null) elevatorDoorBarrier.gameObject.SetActive(true);

        if (voidTrainObject != null)
        {
            voidTrainObject.position = trainStartPosition;
            voidTrainObject.rotation = Quaternion.Euler(0f, -90f, 0f);
            voidTrainObject.gameObject.SetActive(true);
        }
        SetTrainHeadlights(false, 0f);

        if (voidTrackScene != null) voidTrackScene.SetActive(true);
        SetDoors(true);
        yield return new WaitForSeconds(doorOpenTime);

        // Distant rumble begins as player looks out into the dark tracks
        PlayTrainApproach();
        yield return new WaitForSeconds(0.4f);

        // Sudden headlight flare in the distance and horn blast!
        SetTrainHeadlights(true, 15f);
        PlayOneShot(trainAudioSource != null ? trainAudioSource : audioSource, trainHornSound);

        // Train charges down the tunnel directly towards the elevator!
        // Camera shake builds violently as train approaches
        shake = StartCoroutine(ShakeRamp(trainApproachDuration, 0.02f, 0.09f, 35f));
        yield return StartCoroutine(MoveTrainRoutine(trainApproachDuration));

        // When train reaches doorway near-miss threshold, doors slam shut just in time!
        SetDoors(false);
        FadeOutTrain();
        yield return new WaitForSeconds(doorCloseTime);

        if (shake != null) StopCoroutine(shake);
        ResetShakeTarget();
        SetTrainHeadlights(false, 0f);

        if (voidTrackScene != null) voidTrackScene.SetActive(false);
        if (elevatorDoorBarrier != null) elevatorDoorBarrier.gameObject.SetActive(false);

        // Brief silence for tension
        yield return new WaitForSeconds(0.5f);

        // 4. Teddy & Child dialogue reaction (reflecting trauma and mental haven)
        if (teddyReactionConversation != null)
        {
            yield return StartCoroutine(teddyReactionConversation.Play());
        }
        else
        {
            string[] trainReaction = new string[] {
                MetroStorySequenceController.GetLoc("elevator_train_reaction_01"),
                MetroStorySequenceController.GetLoc("elevator_train_reaction_02"),
                MetroStorySequenceController.GetLoc("elevator_train_reaction_03")
            };
            var dm = NpcDialogueManager.Instance;
            if (dm != null && Application.isPlaying)
            {
                yield return StartCoroutine(dm.ShowDialogue(trainReaction, false, Color.white, null));
            }
        }

        // 5. Real arrival travel + floor swap to Floor 3
        PlayOneShot(audioSource, elevatorSong);
        shake = StartCoroutine(Shake(elevatorTravelTime, shakeIntensity, shakeSpeed));
        yield return new WaitForSeconds(elevatorTravelTime);
        if (shake != null) StopCoroutine(shake);
        ResetShakeTarget();

        int nextFloor = FloorManager.Instance != null ? FloorManager.Instance.currentFloorIndex + 1 : 1;
        if (FloorManager.Instance != null)
        {
            FloorManager.Instance.ActivateFloor(nextFloor);
        }

        SetDoors(true);
        yield return new WaitForSeconds(doorOpenTime);

        OnArrivalOnNewFloor?.Invoke();
        if (MetroStorySequenceController.Instance != null)
            MetroStorySequenceController.Instance.OnFloorArrived(nextFloor);

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

    IEnumerator MoveTrainRoutine(float duration)
    {
        if (voidTrainObject == null)
        {
            yield return StartCoroutine(MoveTrainLight());
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Smooth acceleration down the tunnel
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            voidTrainObject.position = Vector3.Lerp(trainStartPosition, trainEndPosition, smoothT);

            float intensity = Mathf.Lerp(12f, 35f, smoothT);
            UpdateHeadlightsIntensity(intensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        voidTrainObject.position = trainEndPosition;
    }

    void ResolveVoidTrainReferences()
    {
        if (voidTrainObject == null && voidTrackScene != null)
        {
            var train = voidTrackScene.transform.Find("VoidTrain");
            if (train != null)
            {
                voidTrainObject = train;
                voidTrainObject.position = trainStartPosition;
                voidTrainObject.rotation = Quaternion.Euler(0f, -90f, 0f);
            }
            else
            {
                var prefab = Resources.Load<GameObject>("Prefabs/VoidTrain");
                if (prefab == null) prefab = Resources.Load<GameObject>("VoidTrain");
                if (prefab != null)
                {
                    var instance = Instantiate(prefab, voidTrackScene.transform);
                    instance.name = "VoidTrain";
                    instance.transform.position = trainStartPosition;
                    instance.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    voidTrainObject = instance.transform;

                    foreach (var c in instance.GetComponentsInChildren<Collider>(true))
                    {
                        c.isTrigger = true;
                    }
                    SetLayerRecursively(instance, 3);
                }
            }
        }

        if (voidTrainObject != null && (trainHeadlights == null || trainHeadlights.Length == 0))
        {
            var existingLights = voidTrainObject.GetComponentsInChildren<Light>(true);
            if (existingLights != null && existingLights.Length > 0)
            {
                trainHeadlights = existingLights;
            }
            else
            {
                var hlObj = new GameObject("Headlights");
                hlObj.transform.SetParent(voidTrainObject, false);
                hlObj.transform.localPosition = new Vector3(0f, 2.0f, 13.5f);
                hlObj.transform.localRotation = Quaternion.identity;
                hlObj.layer = 3;

                var leftObj = new GameObject("LeftLight");
                leftObj.transform.SetParent(hlObj.transform, false);
                leftObj.transform.localPosition = new Vector3(-0.9f, 0f, 0f);
                leftObj.layer = 3;
                var leftL = leftObj.AddComponent<Light>();
                leftL.type = LightType.Spot;
                leftL.range = 40f;
                leftL.spotAngle = 65f;
                leftL.color = new Color(1f, 0.95f, 0.8f);
                leftL.intensity = 0f;
                leftL.enabled = false;

                var rightObj = new GameObject("RightLight");
                rightObj.transform.SetParent(hlObj.transform, false);
                rightObj.transform.localPosition = new Vector3(0.9f, 0f, 0f);
                rightObj.layer = 3;
                var rightL = rightObj.AddComponent<Light>();
                rightL.type = LightType.Spot;
                rightL.range = 40f;
                rightL.spotAngle = 65f;
                rightL.color = new Color(1f, 0.95f, 0.8f);
                rightL.intensity = 0f;
                rightL.enabled = false;

                var glowObj = new GameObject("CabinGlow");
                glowObj.transform.SetParent(voidTrainObject, false);
                glowObj.transform.localPosition = new Vector3(0f, 2.0f, 2.0f);
                glowObj.layer = 3;
                var glowL = glowObj.AddComponent<Light>();
                glowL.type = LightType.Point;
                glowL.range = 15f;
                glowL.color = new Color(1f, 0.72f, 0.38f);
                glowL.intensity = 2.5f;
                glowL.enabled = true;

                trainHeadlights = new Light[] { leftL, rightL };
            }
        }

        if (elevatorDoorBarrier == null)
        {
            var elev = transform.parent != null ? transform.parent : transform;
            var b = elev.Find("ElevatorDoorBarrier");
            if (b != null)
            {
                elevatorDoorBarrier = b.GetComponent<BoxCollider>();
            }
            else
            {
                var barrierObj = new GameObject("ElevatorDoorBarrier");
                barrierObj.transform.SetParent(elev, false);
                barrierObj.transform.localPosition = new Vector3(1.56f, 0.58f, 0f);
                barrierObj.layer = 3;
                elevatorDoorBarrier = barrierObj.AddComponent<BoxCollider>();
                elevatorDoorBarrier.size = new Vector3(0.3f, 3.0f, 3.0f);
                elevatorDoorBarrier.isTrigger = false;
                barrierObj.SetActive(false);
            }
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    void SetTrainHeadlights(bool enable, float intensity)
    {
        if (trainHeadlights != null)
        {
            foreach (var l in trainHeadlights)
            {
                if (l != null)
                {
                    l.enabled = enable;
                    l.intensity = intensity;
                }
            }
        }
    }

    void UpdateHeadlightsIntensity(float intensity)
    {
        if (trainHeadlights != null)
        {
            foreach (var l in trainHeadlights)
            {
                if (l != null) l.intensity = intensity;
            }
        }
    }

    IEnumerator Shake(float duration)
    {
        yield return StartCoroutine(Shake(duration, shakeIntensity, shakeSpeed));
    }

    IEnumerator Shake(float duration, float intensity, float speed)
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
            float x = (Mathf.PerlinNoise(Time.time * speed, 0f) - 0.5f) * intensity;
            float y = (Mathf.PerlinNoise(0f, Time.time * speed) - 0.5f) * intensity;
            shakeTarget.localPosition = original + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        shakeTarget.localPosition = original;
    }

    IEnumerator ShakeRamp(float duration, float startIntensity, float endIntensity, float speed)
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
            float t = elapsed / duration;
            float currentIntensity = Mathf.Lerp(startIntensity, endIntensity, t);
            float x = (Mathf.PerlinNoise(Time.time * speed, 0f) - 0.5f) * currentIntensity;
            float y = (Mathf.PerlinNoise(0f, Time.time * speed) - 0.5f) * currentIntensity;
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