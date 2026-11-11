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
    public Vector3 trainStartPosition = new Vector3(-8.0f, 11.25f, 101.94f);
    public Vector3 trainEndPosition = new Vector3(-38.2f, 11.25f, 101.94f);
    public float trainApproachDuration = 2.4f;
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
        if (playerTransform == null)
        {
            if (FirstPersonController.ActiveInstance != null)
            {
                playerTransform = FirstPersonController.ActiveInstance.transform;
            }
            else
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) playerTransform = playerObj.transform;
                else
                {
                    var fpc = FindAnyObjectByType<FirstPersonController>();
                    if (fpc != null) playerTransform = fpc.transform;
                }
            }
        }

        if (MetroStorySequenceController.Instance != null)
        {
            isPowered = MetroStorySequenceController.Instance.isPowerRestored;
        }

        PrewarmVoidTrainAndAudio();
    }

    private void PrewarmVoidTrainAndAudio()
    {
        PrewarmClip(doorCloseSound);
        PrewarmClip(elevatorSong);
        PrewarmClip(doorOpenSound);
        PrewarmClip(trainApproachSound);
        PrewarmClip(trainHornSound);

        ResolveVoidTrainReferences();
        if (voidTrainObject != null)
        {
            voidTrainObject.gameObject.SetActive(false);
        }
    }

    private static void PrewarmClip(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    public void OnPowerRestored()
    {
        isPowered = true;
    }

    private bool isRunning;
    private bool playerInRange;
    private Rigidbody voidTrainRb;
    private readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
    private Transform activeShakeTransform;
    private Vector3 shakeOriginalLocalPos;
    private bool hasActiveShakeOrigin = false;

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

    void OnDisable()
    {
        playerInRange = false;
        ClearPromptIfOurs();
    }

    private string GetLocalizedButtonPrompt()
    {
        return LocalizationManager.Resolve("prompt_press_elevator_button", "Press 'E' to Call Elevator");
    }

    private bool IsPlayerWithinRange()
    {
        if (playerTransform == null)
        {
            if (FirstPersonController.ActiveInstance != null)
            {
                playerTransform = FirstPersonController.ActiveInstance.transform;
            }
            else
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) playerTransform = playerObj.transform;
            }
        }
        if (playerTransform == null) return false;

        float effectiveDist = Mathf.Max(interactDistance, 2.2f);
        if (Vector3.Distance(transform.position, playerTransform.position) <= effectiveDist)
            return true;

        Camera cam = Camera.main;
        return cam != null && Vector3.Distance(transform.position, cam.transform.position) <= effectiveDist;
    }

    private void ClearPromptIfOurs()
    {
        if (NpcInteractionText.Instance == null) return;
        string p = GetLocalizedButtonPrompt();
        string cur = NpcInteractionText.Instance.GetCurrentPrompt();
        if (cur == p || cur == Story.MobileUIAdapter.AdaptPrompt(p))
        {
            NpcInteractionText.Instance.ClearPrompt();
        }
    }

    void Update()
    {
        bool paused = Story.MobilePauseMenu.Instance != null && Story.MobilePauseMenu.Instance.isPaused;
        bool dialogueRunning = NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning;

        if (isRunning || paused || dialogueRunning)
        {
            if (playerInRange)
            {
                playerInRange = false;
                ClearPromptIfOurs();
            }
            return;
        }

        if (IsPlayerWithinRange())
        {
            playerInRange = true;
            if (NpcInteractionText.Instance != null)
            {
                NpcInteractionText.Instance.SetPrompt(GetLocalizedButtonPrompt(), TryActivateElevator);
            }

            if (Input.GetKeyDown(interactKey) || ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame)
            {
                TryActivateElevator();
            }
        }
        else if (playerInRange)
        {
            playerInRange = false;
            ClearPromptIfOurs();
        }
    }

    public void TryActivateElevator()
    {
        if (isRunning) return;
        if (Story.MobilePauseMenu.Instance != null && Story.MobilePauseMenu.Instance.isPaused) return;
        if (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning) return;
        if (!IsPlayerWithinRange()) return;

        playerInRange = false;
        ClearPromptIfOurs();

        bool powerReady = isPowered || (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.isPowerRestored);
        bool storyUnlocked = MetroStorySequenceController.Instance == null || MetroStorySequenceController.Instance.IsElevatorUnlocked;
        bool questCompleted = currentFloorQuest == null || currentFloorQuest.questCompleted;

        if (!powerReady)
        {
            StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                new string[] { MetroStorySequenceController.GetLoc("elevator_btn_no_power") },
                false,
                Color.white,
                null
            ));
            return;
        }

        if (!storyUnlocked || !questCompleted)
        {
            StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                new string[] { MetroStorySequenceController.GetLoc("elevator_btn_no_ticket") },
                false,
                Color.white,
                null
            ));
            return;
        }

        if (MetroStorySequenceController.Instance != null && !MetroStorySequenceController.Instance.IsTeddyCarried)
        {
            StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                new string[] { MetroStorySequenceController.GetLoc("elevator_btn_no_teddy") },
                false,
                Color.white,
                null
            ));
            return;
        }

        if (FloorManager.Instance == null || !FloorManager.Instance.HasNextFloor()) return;

        StartCoroutine(RunElevator());
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
            SnapVoidTrain(trainStartPosition, Quaternion.Euler(0f, -90f, 0f));
            voidTrainObject.gameObject.SetActive(true);
        }
        SetTrainHeadlights(false, 0f);

        if (voidTrackScene != null) voidTrackScene.SetActive(true);
        SetDoors(true);
        yield return new WaitForSeconds(doorOpenTime);

        // 1. Suspense pause as player looks out into the quiet, dark void tracks
        yield return new WaitForSeconds(1.0f);

        // 2. Distant low rumble begins, headlights flicker faintly in the darkness
        PlayTrainApproach();
        SetTrainHeadlights(true, 6f);
        yield return new WaitForSeconds(0.6f);

        // 3. Horn blares loudly and headlights flash to full intensity!
        SetTrainHeadlights(true, 30f);
        PlayOneShot(trainAudioSource != null ? trainAudioSource : audioSource, trainHornSound);

        // 4. Train charges down the tunnel directly towards the elevator!
        // Camera shake builds violently as train approaches
        shake = StartCoroutine(ShakeRamp(trainApproachDuration, 0.02f, 0.09f, 35f));
        Coroutine trainMove = StartCoroutine(MoveTrainRoutine(trainApproachDuration));

        // 5. In panic, elevator doors start closing so they slam shut right at near-miss impact!
        float waitBeforeDoorClose = Mathf.Max(0.1f, trainApproachDuration - doorCloseTime);
        yield return new WaitForSeconds(waitBeforeDoorClose);

        SetDoors(false);

        // Wait for the rushing train to reach the doorway right as doors seal
        yield return trainMove;

        // Finish remaining door close time if any
        float remainingDoorTime = doorCloseTime - (trainApproachDuration - waitBeforeDoorClose);
        if (remainingDoorTime > 0f)
            yield return new WaitForSeconds(remainingDoorTime);

        FadeOutTrain();

        if (shake != null) StopCoroutine(shake);
        ResetShakeTarget();
        SetTrainHeadlights(false, 0f);

        if (voidTrackScene != null) voidTrackScene.SetActive(false);
        if (elevatorDoorBarrier != null) elevatorDoorBarrier.gameObject.SetActive(false);

        // 6. Brief breathless silence for tension
        yield return new WaitForSeconds(0.8f);

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

    void EnsureVoidTrainRigidbody()
    {
        if (voidTrainObject == null) return;
        voidTrainRb = voidTrainObject.GetComponent<Rigidbody>();
        if (voidTrainRb == null)
        {
            voidTrainRb = voidTrainObject.gameObject.AddComponent<Rigidbody>();
        }
        voidTrainRb.isKinematic = true;
        voidTrainRb.useGravity = false;
        voidTrainRb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void SnapVoidTrain(Vector3 pos, Quaternion rot)
    {
        if (voidTrainObject == null) return;
        EnsureVoidTrainRigidbody();
        if (voidTrainRb != null)
        {
            voidTrainRb.interpolation = RigidbodyInterpolation.None;
            voidTrainRb.position = pos;
            voidTrainRb.rotation = rot;
        }
        voidTrainObject.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
    }

    IEnumerator MoveTrainRoutine(float duration)
    {
        if (voidTrainObject == null)
        {
            yield return StartCoroutine(MoveTrainLight());
            yield break;
        }

        EnsureVoidTrainRigidbody();
        yield return waitForFixedUpdate;
        if (voidTrainRb != null)
        {
            voidTrainRb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return waitForFixedUpdate;
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Smooth continuous acceleration down the tunnel towards the elevator
            float smoothT = Mathf.Lerp(t, t * t, 0.35f);
            Vector3 nextPos = Vector3.Lerp(trainStartPosition, trainEndPosition, smoothT);

            if (voidTrainRb != null)
            {
                voidTrainRb.MovePosition(nextPos);
            }
            else
            {
                voidTrainObject.position = nextPos;
            }

            float intensity = Mathf.Lerp(15f, 40f, smoothT);
            UpdateHeadlightsIntensity(intensity);
        }

        if (voidTrainRb != null)
        {
            voidTrainRb.MovePosition(trainEndPosition);
        }
        else
        {
            voidTrainObject.position = trainEndPosition;
        }
    }

    void ResolveVoidTrainReferences()
    {
        if (voidTrainObject == null && voidTrackScene != null)
        {
            var train = voidTrackScene.transform.Find("VoidTrain");
            if (train != null)
            {
                voidTrainObject = train;
                SnapVoidTrain(trainStartPosition, Quaternion.Euler(0f, -90f, 0f));
                var hl = voidTrainObject.Find("Headlights");
                if (hl != null) { hl.localPosition = new Vector3(1.166f, 2.0f, 13.85f); hl.localRotation = Quaternion.identity; }
                var cg = voidTrainObject.Find("CabinGlow");
                if (cg != null) { cg.localPosition = new Vector3(1.166f, 2.2f, 2.0f); }
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
                    EnsureVoidTrainRigidbody();
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
                hlObj.transform.localPosition = new Vector3(1.166f, 2.0f, 13.85f);
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
                glowObj.transform.localPosition = new Vector3(1.166f, 2.2f, 2.0f);
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

    private Transform GetEffectiveShakeTarget()
    {
        if (shakeTarget == null) return null;

        // Avoid mutating a dynamic Rigidbody transform (causes physics vs Update judder on the player capsule)
        var rb = shakeTarget.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            var cam = shakeTarget.GetComponentInChildren<Camera>(true);
            if (cam != null) return cam.transform;
        }

        return shakeTarget;
    }

    IEnumerator Shake(float duration)
    {
        yield return StartCoroutine(Shake(duration, shakeIntensity, shakeSpeed));
    }

    IEnumerator Shake(float duration, float intensity, float speed)
    {
        Transform target = GetEffectiveShakeTarget();
        if (target == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        activeShakeTransform = target;
        shakeOriginalLocalPos = target.localPosition;
        hasActiveShakeOrigin = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = (Mathf.PerlinNoise(Time.time * speed, 0f) - 0.5f) * intensity;
            float y = (Mathf.PerlinNoise(0f, Time.time * speed) - 0.5f) * intensity;
            target.localPosition = shakeOriginalLocalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetShakeTarget();
    }

    IEnumerator ShakeRamp(float duration, float startIntensity, float endIntensity, float speed)
    {
        Transform target = GetEffectiveShakeTarget();
        if (target == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        activeShakeTransform = target;
        shakeOriginalLocalPos = target.localPosition;
        hasActiveShakeOrigin = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentIntensity = Mathf.Lerp(startIntensity, endIntensity, t);
            float x = (Mathf.PerlinNoise(Time.time * speed, 0f) - 0.5f) * currentIntensity;
            float y = (Mathf.PerlinNoise(0f, Time.time * speed) - 0.5f) * currentIntensity;
            target.localPosition = shakeOriginalLocalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetShakeTarget();
    }

    void ResetShakeTarget()
    {
        if (hasActiveShakeOrigin && activeShakeTransform != null)
        {
            activeShakeTransform.localPosition = shakeOriginalLocalPos;
        }
        hasActiveShakeOrigin = false;
        activeShakeTransform = null;
    }
}