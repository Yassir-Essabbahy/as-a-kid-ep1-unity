using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MetroFuseBoxPuzzle : MonoBehaviour
{
    private static MetroFuseBoxPuzzle _instance;
    public static MetroFuseBoxPuzzle Instance => _instance;

    [Header("Cabinet Model & Door")]
    public Transform cabinetDoorTransform; // Cube.001_1
    public Vector3 doorClosedLocalEuler = new Vector3(0f, 0f, 240f);
    public Vector3 doorOpenLocalEuler = new Vector3(0f, 0f, 130f);

    [Header("Switch Triggers (Physical 3D Switches)")]
    public SwitchTrigger[] switchTriggers = new SwitchTrigger[5];
    public Vector3 switchOffEuler = new Vector3(0f, 270f, 0f);
    public Vector3 switchOnEuler = new Vector3(0f, 270f, -90f);

    [Header("Camera & Inspection")]
    public Transform inspectCameraAnchor;
    public Vector3 inspectPosition = new Vector3(-38.68f, 7.65f, 104.20f);
    public Vector3 inspectLookTarget = new Vector3(-38.68f, 7.60f, 105.10f);
    public float transitionDuration = 0.45f;

    [Header("Player & Interaction")]
    public Transform playerTransform;
    public float interactionDistance = 2.5f;
    public string promptOpenText = "Press 'E' to Open Fuse Box";
    public string promptPoweredText = "Fuse Box (Power Restored)";

    [Header("Mobile UI (Moroccan Beach Style)")]
    public GameObject mobileUIRoot;
    public Button mobileOpenButton;
    public Button mobileCloseButton;

    [Header("Cabinet Lighting & Feedback")]
    public Light cabinetLight;
    public AudioSource audioSource;

    [Header("Puzzle State")]
    public bool isPowerRestored = false;
    public bool isInspecting = false;
    public bool hasTeddyHintPlayed = false;
    private bool isTransitioning = false;
    private bool isCompleting = false;

    public bool IsPlayerNear
    {
        get
        {
            if (isInspecting || playerTransform == null) return false;
            return Vector3.Distance(transform.position, playerTransform.position) <= interactionDistance;
        }
    }

    // References
    private FirstPersonController fpsController;
    private Camera playerCam;
    private TextMeshProUGUI interactUI;
    private Vector3 originalCamLocalPos;
    private Quaternion originalCamLocalRot;
    private Coroutine activeTransitionCoroutine;
    private Coroutine activeDoorCoroutine;

    // Procedural Audio Clips
    private AudioClip switchSnapClip;
    private AudioClip doorCreakClip;
    private AudioClip powerSurgeClip;
    private AudioClip humLoopClip;

    [System.Serializable]
    public class SwitchTrigger
    {
        public string switchName;
        public Transform switchTransform;
        public bool isOn = false;
        [HideInInspector] public Quaternion baseRotation;
        [HideInInspector] public Coroutine rotateRoutine;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;

        GenerateProceduralAudio();
    }

    private void Start()
    {
        ResolveReferences();
        InitializeSwitches();
        InitializeDoor();
        HookMobileButtons();

        if (mobileOpenButton != null) mobileOpenButton.gameObject.SetActive(false);
        if (mobileCloseButton != null) mobileCloseButton.gameObject.SetActive(false);
    }

    public void ResetPuzzle()
    {
        isPowerRestored = false;
        isInspecting = false;
        isTransitioning = false;
        isCompleting = false;

        InitializeSwitches();
        InitializeDoor();

        if (mobileOpenButton != null) mobileOpenButton.gameObject.SetActive(false);
        if (mobileCloseButton != null) mobileCloseButton.gameObject.SetActive(false);
    }

    public void ResolveReferences()
    {
        if (fpsController == null)
            fpsController = FindAnyObjectByType<FirstPersonController>();

        if (fpsController != null)
        {
            if (playerTransform == null)
                playerTransform = fpsController.transform;

            if (playerCam == null && fpsController.playerCamera != null)
                playerCam = fpsController.playerCamera;
        }

        if (playerCam == null)
            playerCam = Camera.main;

        var npcInteract = FindAnyObjectByType<NpcInteractionText>();
        if (npcInteract != null)
            interactUI = npcInteract.InteractText;

        if (cabinetDoorTransform == null)
        {
            var door = transform.Find("root/GLTF_SceneRootNode/Cube.001_1");
            if (door != null) cabinetDoorTransform = door;
        }

        if (cabinetLight == null)
        {
            var lGo = GameObject.Find("PointRoom2 1 (1)");
            if (lGo != null) cabinetLight = lGo.GetComponent<Light>();
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0.85f;
            audioSource.minDistance = 1.5f;
            audioSource.maxDistance = 15.0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
        }

        ResolveSwitchTransforms();
    }

    private void ResolveSwitchTransforms()
    {
        string[] defaultNames = new string[] {
            "Cylinder.005_9",   // 0: Leftmost
            "Cylinder.007_11",  // 1: Mid-Left
            "Cylinder.004_8",   // 2: Center
            "Cylinder.006_10",  // 3: Mid-Right
            "Cylinder.003_7"    // 4: Rightmost
        };

        if (switchTriggers == null || switchTriggers.Length != 5)
        {
            switchTriggers = new SwitchTrigger[5];
        }

        for (int i = 0; i < 5; i++)
        {
            if (switchTriggers[i] == null)
                switchTriggers[i] = new SwitchTrigger();

            switchTriggers[i].switchName = defaultNames[i];

            if (switchTriggers[i].switchTransform == null)
            {
                var sw = transform.Find($"root/GLTF_SceneRootNode/{defaultNames[i]}");
                if (sw != null) switchTriggers[i].switchTransform = sw;
            }

            // Ensure switch has a box collider for raycast clicks & touches
            if (switchTriggers[i].switchTransform != null)
            {
                var col = switchTriggers[i].switchTransform.GetComponent<BoxCollider>();
                if (col == null)
                {
                    col = switchTriggers[i].switchTransform.gameObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.09f, 0.14f, 0.14f);
                    col.center = Vector3.zero;
                }
            }
        }
    }

    private void InitializeSwitches()
    {
        for (int i = 0; i < switchTriggers.Length; i++)
        {
            var sw = switchTriggers[i];
            if (sw == null || sw.switchTransform == null) continue;

            // Initial puzzle configuration: Breakers all start OFF at (0, 270, 0)
            sw.isOn = false;

            ApplySwitchRotation(sw, sw.isOn, instant: true);
        }
    }

    private void InitializeDoor()
    {
        if (cabinetDoorTransform != null)
        {
            cabinetDoorTransform.localEulerAngles = doorClosedLocalEuler;
        }
    }

    private void HookMobileButtons()
    {
        if (mobileOpenButton != null)
        {
            mobileOpenButton.onClick.RemoveAllListeners();
            mobileOpenButton.onClick.AddListener(OpenInspection);
        }

        if (mobileCloseButton != null)
        {
            mobileCloseButton.onClick.RemoveAllListeners();
            mobileCloseButton.onClick.AddListener(CloseInspection);
        }
    }

    private void Update()
    {
        if (playerTransform == null && fpsController != null)
            playerTransform = fpsController.transform;

        if (isInspecting)
        {
            HandleInspectionInput();
            return;
        }

        // Outside inspection: Check distance to fusebox
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool inRange = dist <= interactionDistance;

        if (inRange)
        {
            if (interactUI != null)
            {
                interactUI.text = isPowerRestored ? promptPoweredText : promptOpenText;
            }

            if (mobileOpenButton != null && !mobileOpenButton.gameObject.activeSelf)
            {
                mobileOpenButton.gameObject.SetActive(true);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                OpenInspection();
            }
        }
        else
        {
            if (interactUI != null && (interactUI.text == promptOpenText || interactUI.text == promptPoweredText))
            {
                interactUI.text = "";
            }

            if (mobileOpenButton != null && mobileOpenButton.gameObject.activeSelf)
            {
                mobileOpenButton.gameObject.SetActive(false);
            }
        }
    }

    private void HandleInspectionInput()
    {
        if (isTransitioning) return;

        // PC Exit keys
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            if (!isCompleting)
            {
                CloseInspection();
                return;
            }
        }

        // Don't interact while completing sequence
        if (isCompleting) return;

        // Handle Switch Click (Mouse or Mobile Touch)
        bool pointerClicked = false;
        Vector2 pointerPos = Vector2.zero;

        // Check mobile touches
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                // Verify touch isn't over UI
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(t.fingerId))
                {
                    pointerClicked = true;
                    pointerPos = t.position;
                }
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            // Verify mouse isn't over UI
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                pointerClicked = true;
                pointerPos = Input.mousePosition;
            }
        }

        if (pointerClicked && playerCam != null)
        {
            Ray ray = playerCam.ScreenPointToRay(pointerPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                for (int i = 0; i < switchTriggers.Length; i++)
                {
                    var sw = switchTriggers[i];
                    if (sw == null || sw.switchTransform == null) continue;

                    if (hit.collider.transform == sw.switchTransform || hit.collider.transform.IsChildOf(sw.switchTransform))
                    {
                        ToggleSwitch(sw);
                        break;
                    }
                }
            }
        }
    }

    public void ToggleSwitch(SwitchTrigger sw)
    {
        if (sw == null || sw.switchTransform == null || isCompleting) return;

        sw.isOn = !sw.isOn;
        ApplySwitchRotation(sw, sw.isOn, instant: false);

        PlayClip(switchSnapClip, 1.0f);

        CheckPuzzleSolved();
    }

    private void ApplySwitchRotation(SwitchTrigger sw, bool on, bool instant)
    {
        if (sw == null || sw.switchTransform == null) return;

        Quaternion targetRot = Quaternion.Euler(on ? switchOnEuler : switchOffEuler);

        if (instant || !Application.isPlaying)
        {
            sw.switchTransform.localRotation = targetRot;
        }
        else
        {
            if (sw.rotateRoutine != null) StopCoroutine(sw.rotateRoutine);
            sw.rotateRoutine = StartCoroutine(AnimateSwitchRotation(sw.switchTransform, targetRot, 0.12f));
        }
    }

    private IEnumerator AnimateSwitchRotation(Transform swT, Quaternion targetRot, float duration)
    {
        if (!Application.isPlaying)
        {
            swT.localRotation = targetRot;
            yield break;
        }

        Quaternion startRot = swT.localRotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            swT.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        swT.localRotation = targetRot;
    }

    private void CheckPuzzleSolved()
    {
        if (isPowerRestored || isCompleting) return;

        bool allOn = true;
        for (int i = 0; i < switchTriggers.Length; i++)
        {
            if (switchTriggers[i] != null && !switchTriggers[i].isOn)
            {
                allOn = false;
                break;
            }
        }

        if (allOn)
        {
            if (Application.isPlaying)
            {
                StartCoroutine(PuzzleCompletionRoutine());
            }
            else
            {
                var iter = PuzzleCompletionRoutine();
                while (iter.MoveNext()) { }
            }
        }
    }

    private IEnumerator PuzzleCompletionRoutine()
    {
        isCompleting = true;
        isPowerRestored = true;

        // Notify Elevator Button immediately
        var elevBtn = FindAnyObjectByType<ElevatorButton>();
        if (elevBtn != null)
        {
            elevBtn.OnPowerRestored();
        }

        // Notify Story Sequence Controller immediately
        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.OnPowerRestored();
        }

        PlayClip(powerSurgeClip, 1.0f);

        // Flash cabinet light to simulate power surge
        if (cabinetLight != null && Application.isPlaying)
        {
            float origIntensity = cabinetLight.intensity;
            cabinetLight.intensity = origIntensity * 2.2f;
            yield return new WaitForSeconds(0.12f);
            cabinetLight.intensity = origIntensity * 0.7f;
            yield return new WaitForSeconds(0.08f);
            cabinetLight.intensity = origIntensity * 1.8f;
            yield return new WaitForSeconds(0.15f);
            cabinetLight.intensity = origIntensity * 1.3f;
        }

        StartElectricalHum();

        if (Application.isPlaying) yield return new WaitForSeconds(1.2f);
        else yield return null;

        isCompleting = false;

        // Automatically close inspection smoothly after power restored
        CloseInspection();
    }

    public void OpenInspection()
    {
        if (isInspecting || isTransitioning) return;
        isInspecting = true;

        if (interactUI != null) interactUI.text = "";

        if (mobileOpenButton != null) mobileOpenButton.gameObject.SetActive(false);
        if (mobileCloseButton != null) mobileCloseButton.gameObject.SetActive(true);

        if (fpsController != null)
        {
            fpsController.SetControlLocked(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PlayClip(doorCreakClip, 0.75f);

        // Animate door open
        if (activeDoorCoroutine != null) StopCoroutine(activeDoorCoroutine);
        activeDoorCoroutine = StartCoroutine(AnimateDoor(doorOpenLocalEuler, transitionDuration));

        // Animate camera to inspection view
        Vector3 targetPos = inspectCameraAnchor != null ? inspectCameraAnchor.position : inspectPosition;
        Quaternion targetRot = inspectCameraAnchor != null
            ? inspectCameraAnchor.rotation
            : Quaternion.LookRotation(inspectLookTarget - inspectPosition);

        if (activeTransitionCoroutine != null) StopCoroutine(activeTransitionCoroutine);
        activeTransitionCoroutine = StartCoroutine(AnimateCameraToInspection(targetPos, targetRot, transitionDuration));

        // Trigger Teddy Bear hint conversation on first inspection before power is restored
        if (!hasTeddyHintPlayed && !isPowerRestored)
        {
            hasTeddyHintPlayed = true;
            StartCoroutine(PlayTeddyHintRoutine());
        }
    }

    private IEnumerator PlayTeddyHintRoutine()
    {
        yield return new WaitForSeconds(transitionDuration + 0.15f);

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.OnFuseBoxInspected();
        }

        if (NpcDialogueManager.Instance != null && LocalizationManager.Instance != null)
        {
            string line1 = LocalizationManager.Instance.Get("teddy_fuse_hint_01");
            string line2 = LocalizationManager.Instance.Get("teddy_fuse_hint_02");

            yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                new string[] { line1, line2 },
                hasChoice: false,
                dialogueColor: NpcDialogueManager.GetSpeakerColor("teddy"),
                dialogueFont: NpcDialogueManager.DefaultDialogueFont
            ));
        }
    }

    public void CloseInspection()
    {
        if (!isInspecting || isTransitioning) return;
        isInspecting = false;

        if (mobileCloseButton != null) mobileCloseButton.gameObject.SetActive(false);

        PlayClip(doorCreakClip, 0.65f);

        // Animate door close
        if (activeDoorCoroutine != null) StopCoroutine(activeDoorCoroutine);
        activeDoorCoroutine = StartCoroutine(AnimateDoor(doorClosedLocalEuler, transitionDuration * 0.85f));

        // Return camera to player
        if (activeTransitionCoroutine != null) StopCoroutine(activeTransitionCoroutine);
        activeTransitionCoroutine = StartCoroutine(AnimateCameraBackToPlayer(transitionDuration * 0.85f));
    }

    private IEnumerator AnimateDoor(Vector3 targetLocalEuler, float duration)
    {
        if (cabinetDoorTransform == null) yield break;

        if (!Application.isPlaying)
        {
            cabinetDoorTransform.localEulerAngles = targetLocalEuler;
            yield break;
        }

        Quaternion startRot = cabinetDoorTransform.localRotation;
        Quaternion targetRot = Quaternion.Euler(targetLocalEuler);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cabinetDoorTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cabinetDoorTransform.localRotation = targetRot;
    }

    private IEnumerator AnimateCameraToInspection(Vector3 targetWorldPos, Quaternion targetWorldRot, float duration)
    {
        isTransitioning = true;
        if (playerCam != null)
        {
            originalCamLocalPos = playerCam.transform.localPosition;
            originalCamLocalRot = playerCam.transform.localRotation;

            if (!Application.isPlaying)
            {
                playerCam.transform.position = targetWorldPos;
                playerCam.transform.rotation = targetWorldRot;
                isTransitioning = false;
                yield break;
            }

            Vector3 startPos = playerCam.transform.position;
            Quaternion startRot = playerCam.transform.rotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                playerCam.transform.position = Vector3.Lerp(startPos, targetWorldPos, t);
                playerCam.transform.rotation = Quaternion.Slerp(startRot, targetWorldRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            playerCam.transform.position = targetWorldPos;
            playerCam.transform.rotation = targetWorldRot;
        }
        isTransitioning = false;
    }

    private IEnumerator AnimateCameraBackToPlayer(float duration)
    {
        isTransitioning = true;
        if (playerCam != null)
        {
            if (!Application.isPlaying)
            {
                playerCam.transform.localPosition = originalCamLocalPos;
                playerCam.transform.localRotation = originalCamLocalRot;
                isTransitioning = false;
            }
            else
            {
                Vector3 startPos = playerCam.transform.position;
                Quaternion startRot = playerCam.transform.rotation;

                Transform jointOrParent = playerCam.transform.parent;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    Vector3 endPos = jointOrParent != null ? jointOrParent.TransformPoint(originalCamLocalPos) : playerCam.transform.position;
                    Quaternion endRot = jointOrParent != null ? jointOrParent.rotation * originalCamLocalRot : playerCam.transform.rotation;

                    playerCam.transform.position = Vector3.Lerp(startPos, endPos, t);
                    playerCam.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                playerCam.transform.localPosition = originalCamLocalPos;
                playerCam.transform.localRotation = originalCamLocalRot;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (fpsController != null)
        {
            fpsController.SetControlLocked(false);
        }

        isTransitioning = false;
    }

    private void StartElectricalHum()
    {
        if (audioSource != null && humLoopClip != null)
        {
            audioSource.clip = humLoopClip;
            audioSource.loop = true;
            audioSource.volume = 0.35f;
            audioSource.Play();
        }
    }

    private void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }
    }

    #region Procedural PSX Audio Synthesis
    private void GenerateProceduralAudio()
    {
        switchSnapClip = CreateClickClip();
        doorCreakClip = CreateDoorCreakClip();
        powerSurgeClip = CreatePowerUpClip();
        humLoopClip = CreateHumLoopClip();
    }

    private AudioClip CreateClickClip()
    {
        int sampleRate = 44100;
        float duration = 0.07f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = Mathf.Lerp(1600f, 240f, t);
            float env = Mathf.Exp(-t * 22f);
            float noise = (Random.value * 2f - 1f) * 0.45f;
            float tone = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate));
            data[i] = (tone * 0.75f + noise) * env;
        }

        var clip = AudioClip.Create("PSX_BreakerSnap", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateDoorCreakClip()
    {
        int sampleRate = 44100;
        float duration = 0.35f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float normT = (float)i / samples;
            float wobble = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.4f;
            float creak = Mathf.Sin(2f * Mathf.PI * (320f + wobble * 60f) * t) * 0.5f;
            float latch = normT < 0.15f ? (Random.value * 2f - 1f) * 0.6f * Mathf.Exp(-normT * 30f) : 0f;
            float env = Mathf.Sin(normT * Mathf.PI);
            data[i] = (creak * 0.6f + latch) * env * 0.5f;
        }

        var clip = AudioClip.Create("PSX_CabinetHinge", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreatePowerUpClip()
    {
        int sampleRate = 44100;
        float duration = 1.5f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = Mathf.Lerp(45f, 110f, Mathf.Pow(t, 1.4f));
            float tone1 = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate));
            float tone2 = Mathf.Sin(2f * Mathf.PI * (freq * 2f) * ((float)i / sampleRate)) * 0.45f;
            float noise = (Random.value * 2f - 1f) * Mathf.Lerp(0.3f, 0.02f, t);
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 5f)) * (t > 0.8f ? Mathf.Lerp(1f, 0.7f, (t - 0.8f) / 0.2f) : 1f);
            data[i] = (tone1 + tone2 + noise) * env * 0.65f;
        }

        var clip = AudioClip.Create("PSX_PowerSurge", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateHumLoopClip()
    {
        int sampleRate = 44100;
        float duration = 2.0f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float h1 = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.65f;
            float h2 = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.25f;
            float h3 = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.1f;
            data[i] = (h1 + h2 + h3) * 0.35f;
        }

        var clip = AudioClip.Create("PSX_ElectricalHum", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
    #endregion
}
