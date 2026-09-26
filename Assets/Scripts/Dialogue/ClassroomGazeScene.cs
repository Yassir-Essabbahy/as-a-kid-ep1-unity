using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

/// <summary>
/// Life is Strange 1 style Classroom Controller:
/// - Seated camera free look with clamped yaw & pitch and gentle breathing sway.
/// - Minimalist reticle & LiS-style interaction prompt ([E] Look • Pen).
/// - Modular desk interactables: player inspects all required desk items while the teacher lectures.
/// - Once all table items are inspected, teacher cues: "Can you open to page 54?".
/// - Player opens the book on the table, self-talk monologue plays.
/// - Cinematic surreal transition: slow push-in to page illustration, muffled audio, sub-bass rumble, smooth fade to Gameplay3.
/// </summary>
public class ClassroomGazeScene : MonoBehaviour
{
    public static ClassroomGazeScene Instance { get; private set; }

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera teacherVCam;
    [SerializeField] private CinemachineCamera bookVCam;
    [SerializeField] private CinemachineCamera bookZoomVCam;
    [SerializeField] private CinemachineImpulseSource catchImpulse;

    [Header("Seated Look Settings")]
    [SerializeField] private Vector3 seatedEyePosition = new Vector3(-1.75f, 2.38f, -1.85f);
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float lookSmoothTime = 0.08f;
    [SerializeField] private float minPitch = -22f; // Looking up towards blackboard/clock
    [SerializeField] private float maxPitch = 58f;  // Looking down at desk surface
    [SerializeField] private float minYaw = -75f;   // Looking left towards classroom door
    [SerializeField] private float maxYaw = 75f;    // Looking right towards windows
    [SerializeField] private float maxInteractionDistance = 2.4f;

    [Header("Scene Transition")]
    [SerializeField] private bool transitionToNextScene = true;
    [SerializeField] private string targetSceneName = "Gameplay3";
    [SerializeField] private float sceneTransitionDuration = 2.2f;

    [Header("Player handoff (Legacy fallback)")]
    [SerializeField] private GameObject playerRig;
    [SerializeField] private Transform playerHandoffPoint;
    [SerializeField] private float teleportFadeDuration = 0.75f;

    [Header("Dialogue Sequences")]
    [SerializeField] private DialogueSequence teacherIntroLines;
    [SerializeField] private DialogueSequence entityLines;
    [SerializeField] private DialogueSequence teacher2Lines;
    [SerializeField] private DialogueSequence player2Lines;

    [Header("Book")]
    [SerializeField] private Transform bookTransform;
    [SerializeField] private float startAngleX = 90.2f;
    [SerializeField] private float endAngleX = 269.0f;
    [SerializeField] private float openDuration = 1.2f;
    [SerializeField] private ClassroomDeskInteractable bookInteractable;

    [Header("Audio")]
    [SerializeField] private AudioSource teacherAudioSource;
    [SerializeField] private AudioSource transitionRumbleSource;
    [SerializeField] private AudioClip transitionRumbleClip;
    [SerializeField] private AudioSource classroomClockAudio;

    [Header("Zoom transition")]
    [SerializeField] private Transform bookZoomPoint;
    [SerializeField] private UnityEvent onZoomComplete;

    // State machine
    public enum ClassroomPhase
    {
        TeacherLectureAndExploration, // Seated free look, inspecting desk items while teacher speaks
        AwaitingBookOpen,            // Teacher instructed class to open page 54; book unlocked
        ReadingBook,                 // Camera focusing on book, self-talk dialogue playing
        CinematicTransition          // Slow creepy zoom into illustration, audio muffling, fade to Gameplay3
    }

    [Header("Debug / State")]
    public ClassroomPhase currentPhase = ClassroomPhase.TeacherLectureAndExploration;
    public bool isSeatedLookActive = true;
    public bool allRequiredDeskItemsInspected = false;
    public bool teacherIntroCompleted = false;

    private float _targetYaw = 0f;
    private float _targetPitch = 12f; // Slight downward angle towards teacher
    private float _currentYaw = 0f;
    private float _currentPitch = 12f;
    private float _yawVelocity = 0f;
    private float _pitchVelocity = 0f;

    private Transform _activeCameraTransform;
    private ClassroomDeskInteractable _currentHoverItem;
    private List<ClassroomDeskInteractable> _registeredDeskItems = new();
    private Coroutine _lectureCoroutine;
    private Coroutine _bookSequenceCoroutine;
    private Coroutine _rotateCoroutine;
    private bool _transitionTriggered = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResolveReferences();
    }

    private void Start()
    {
        if (ClassroomEndingController.IsEndingActive)
        {
            Debug.Log("[ClassroomGazeScene] Ending cutscene active. Disabling normal intro.");
            enabled = false;
            return;
        }

        if (playerRig != null) playerRig.SetActive(false);

        // Lock cursor for natural first-person head rotation
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Position camera rig at seated eye position
        if (teacherVCam != null)
        {
            teacherVCam.transform.position = seatedEyePosition;
            teacherVCam.transform.rotation = Quaternion.Euler(_targetPitch, _targetYaw, 0f);
            teacherVCam.gameObject.SetActive(true);
            _activeCameraTransform = teacherVCam.transform;
        }
        if (bookVCam != null) bookVCam.gameObject.SetActive(false);
        if (bookZoomVCam != null) bookZoomVCam.gameObject.SetActive(false);

        // Register all desk interactables in scene
        RefreshDeskItems();

        // Lock book initially until required table items are inspected
        if (bookInteractable != null)
        {
            bookInteractable.isInteractable = false;
            bookInteractable.actionPrompt = "Open Book";
        }

        // Start teacher's ambient lecture
        _lectureCoroutine = StartCoroutine(TeacherLectureRoutine());
    }

    public void ResolveReferences()
    {
        if (teacherVCam == null)
        {
            var tv = GameObject.Find("TeacherVCam");
            if (tv != null) teacherVCam = tv.GetComponent<CinemachineCamera>();
        }
        if (bookVCam == null)
        {
            var bv = GameObject.Find("BookVCam");
            if (bv != null) bookVCam = bv.GetComponent<CinemachineCamera>();
        }
        if (bookZoomVCam == null)
        {
            var bzv = GameObject.Find("BookZoomVCam");
            if (bzv != null) bookZoomVCam = bzv.GetComponent<CinemachineCamera>();
        }
        if (bookTransform == null)
        {
            var page = GameObject.Find("Book/Page1");
            if (page != null) bookTransform = page.transform;
        }
        if (bookInteractable == null)
        {
            var bookObj = GameObject.Find("Book");
            if (bookObj != null)
            {
                bookInteractable = bookObj.GetComponent<ClassroomDeskInteractable>();
                if (bookInteractable == null)
                {
                    bookInteractable = bookObj.AddComponent<ClassroomDeskInteractable>();
                    bookInteractable.itemName = "Page 54";
                    bookInteractable.actionPrompt = "Open Book";
                    bookInteractable.isRequiredForProgression = false;
                    bookInteractable.isInteractable = false;
                }
            }
        }
        if (teacherAudioSource == null)
        {
            var teacher = GameObject.Find("Teacher");
            if (teacher != null)
            {
                teacherAudioSource = teacher.GetComponent<AudioSource>();
                if (teacherAudioSource == null)
                {
                    teacherAudioSource = teacher.AddComponent<AudioSource>();
                    teacherAudioSource.spatialBlend = 0.85f;
                    teacherAudioSource.minDistance = 2f;
                    teacherAudioSource.maxDistance = 25f;
                }
            }
        }
        if (classroomClockAudio == null)
        {
            var clockObj = GameObject.Find("WallClock");
            if (clockObj != null) classroomClockAudio = clockObj.GetComponentInChildren<AudioSource>();
        }
        if (transitionRumbleClip == null)
        {
            transitionRumbleClip = Resources.Load<AudioClip>("Sounds/EarthQuake/EarthQuakeSoundEffect");
#if UNITY_EDITOR
            if (transitionRumbleClip == null)
            {
                transitionRumbleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/EarthQuake/EarthQuakeSoundEffect.WAV");
            }
#endif
        }
        if (transitionRumbleSource == null)
        {
            transitionRumbleSource = gameObject.GetComponent<AudioSource>();
            if (transitionRumbleSource == null)
            {
                transitionRumbleSource = gameObject.AddComponent<AudioSource>();
            }
            transitionRumbleSource.clip = transitionRumbleClip;
            transitionRumbleSource.loop = true;
            transitionRumbleSource.playOnAwake = false;
            transitionRumbleSource.volume = 0f;
        }

        // Ensure Life is Strange UI exists
        var ui = FindAnyObjectByType<ClassroomLifeIsStrangeUI>();
        if (ui == null)
        {
            var uiGo = new GameObject("ClassroomLifeIsStrangeUI");
            uiGo.AddComponent<ClassroomLifeIsStrangeUI>();
        }
    }

    /// <summary>
    /// Scans the scene for all desk interactables. Allows user to add any objects to the table later!
    /// </summary>
    public void RefreshDeskItems()
    {
        _registeredDeskItems.Clear();
        var allItems = FindObjectsByType<ClassroomDeskInteractable>(FindObjectsSortMode.None);
        foreach (var item in allItems)
        {
            if (item != bookInteractable && item.isRequiredForProgression)
            {
                _registeredDeskItems.Add(item);
                item.OnItemInspected += HandleDeskItemInspected;
            }
        }
        Debug.Log($"[ClassroomGazeScene] Registered {_registeredDeskItems.Count} required desk interactables.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (!isSeatedLookActive) return;

        HandleSeatedMouseLook();
        HandleInteractionRaycast();
        HandleInteractionInput();
    }

    /// <summary>
    /// Smooth, clamped mouse look while seated in the classroom chair.
    /// </summary>
    private void HandleSeatedMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        _targetYaw = Mathf.Clamp(_targetYaw + mouseX, minYaw, maxYaw);
        _targetPitch = Mathf.Clamp(_targetPitch - mouseY, minPitch, maxPitch);

        _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, lookSmoothTime);
        _currentPitch = Mathf.SmoothDampAngle(_currentPitch, _targetPitch, ref _pitchVelocity, lookSmoothTime);

        // Subtle idle breathing sway
        float breathPitch = Mathf.Sin(Time.time * 1.5f) * 0.15f;
        float breathRoll = Mathf.Cos(Time.time * 0.8f) * 0.1f;

        Quaternion lookRotation = Quaternion.Euler(_currentPitch + breathPitch, _currentYaw, breathRoll);

        if (_activeCameraTransform != null)
        {
            _activeCameraTransform.rotation = lookRotation;
            _activeCameraTransform.position = seatedEyePosition;
        }
    }

    /// <summary>
    /// Raycasts from screen center to detect desk items (Life is Strange 1 style).
    /// </summary>
    private void HandleInteractionRaycast()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        ClassroomDeskInteractable hitItem = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance))
        {
            hitItem = hit.collider.GetComponentInParent<ClassroomDeskInteractable>();
        }

        if (hitItem != _currentHoverItem)
        {
            _currentHoverItem = hitItem;

            if (_currentHoverItem != null && _currentHoverItem.isInteractable)
            {
                string action = _currentHoverItem.actionPrompt;
                string name = _currentHoverItem.itemName;
                ClassroomLifeIsStrangeUI.Instance?.ShowPrompt(_currentHoverItem.keyPrompt, action, name);
            }
            else
            {
                ClassroomLifeIsStrangeUI.Instance?.HidePrompt();
            }
        }
    }

    /// <summary>
    /// Processes player input (E key or mouse click) to inspect hovered desk items or the book.
    /// </summary>
    private void HandleInteractionInput()
    {
        bool interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);

        if (interactPressed && _currentHoverItem != null)
        {
            if (_currentHoverItem == bookInteractable)
            {
                if (bookInteractable.isInteractable)
                {
                    StartBookSequence();
                }
                else
                {
                    // If player tries to open book before teacher says so
                    string loc = LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.Get("desk_book_locked_01")
                        : "I should listen to what the teacher is saying first.";
                    ClassroomLifeIsStrangeUI.Instance?.ShowDialogueLine("Child", loc, 2.5f);
                }
            }
            else if (_currentHoverItem.isInteractable)
            {
                // Inspect desk item
                _currentHoverItem.Interact();

                string thought = _currentHoverItem.GetNextThought();
                if (!string.IsNullOrEmpty(thought))
                {
                    ClassroomLifeIsStrangeUI.Instance?.ShowDialogueLine("Child", thought, 3.8f);
                }

                CheckDeskProgression();
            }
        }
    }

    private void HandleDeskItemInspected(ClassroomDeskInteractable item)
    {
        CheckDeskProgression();
    }

    /// <summary>
    /// Checks if all required items on the desk have been inspected.
    /// If so, cues the teacher to ask the class to open page 54!
    /// </summary>
    public void CheckDeskProgression()
    {
        if (allRequiredDeskItemsInspected) return;

        bool allDone = true;
        foreach (var item in _registeredDeskItems)
        {
            if (item != null && item.isRequiredForProgression && !item.hasBeenInspected)
            {
                allDone = false;
                break;
            }
        }

        if (allDone)
        {
            allRequiredDeskItemsInspected = true;
            Debug.Log("[ClassroomGazeScene] All required desk items inspected!");

            // If teacher already finished lines 1-3, cue line 4 immediately!
            if (teacherIntroCompleted && currentPhase == ClassroomPhase.TeacherLectureAndExploration)
            {
                StartCoroutine(CueTeacherPage54());
            }
        }
    }

    /// <summary>
    /// Teacher speaks lines 1-3 naturally while student has free look and interaction.
    /// </summary>
    private IEnumerator TeacherLectureRoutine()
    {
        yield return new WaitForSeconds(1.0f);

        // Line 1: Good morning, everyone.
        yield return StartCoroutine(PlayTeacherLine("classroom_intro_01", "Teacher_Intro", 0));
        yield return new WaitForSeconds(1.4f);

        // Line 2: We have been late on our lesson.
        yield return StartCoroutine(PlayTeacherLine("classroom_intro_02", "Teacher_Intro", 1));
        yield return new WaitForSeconds(1.4f);

        // Line 3: Let's start right away.
        yield return StartCoroutine(PlayTeacherLine("classroom_intro_03", "Teacher_Intro", 2));
        yield return new WaitForSeconds(0.8f);

        teacherIntroCompleted = true;

        // If player already inspected all desk items, immediately transition to Line 4!
        if (allRequiredDeskItemsInspected)
        {
            yield return StartCoroutine(CueTeacherPage54());
        }
        else
        {
            Debug.Log("[ClassroomGazeScene] Teacher waiting for student to inspect desk items...");
        }
    }

    /// <summary>
    /// Line 4: Teacher tells class to open page 54, unlocking the book!
    /// </summary>
    private IEnumerator CueTeacherPage54()
    {
        currentPhase = ClassroomPhase.AwaitingBookOpen;

        yield return new WaitForSeconds(0.6f);

        // Line 4: Can you open to page 54?
        yield return StartCoroutine(PlayTeacherLine("classroom_intro_04", "Teacher_Intro", 3));

        // Unlock Book for interaction!
        if (bookInteractable != null)
        {
            bookInteractable.isInteractable = true;
            bookInteractable.actionPrompt = "Open Book";
            bookInteractable.itemName = "Page 54";
            Debug.Log("[ClassroomGazeScene] Book unlocked! Ready for player interaction.");
        }
    }

    private IEnumerator PlayTeacherLine(string locKey, string seqId, int lineIndex)
    {
        string text = (LocalizationManager.Instance != null) ? LocalizationManager.Instance.Get(locKey) : locKey;
        AudioClip clip = null;

        if (teacherIntroLines != null && lineIndex < teacherIntroLines.lines.Count)
        {
            clip = teacherIntroLines.lines[lineIndex].voiceClip;
        }

        if (clip == null)
        {
            string path = $"Sounds/TrafehVoiceLines/0{lineIndex + 1}_Trafeh";
            clip = Resources.Load<AudioClip>(path);
#if UNITY_EDITOR
            if (clip == null)
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Sounds/TrafehVoiceLines/0{lineIndex + 1}_Trafeh.MP3");
            }
#endif
        }

        if (teacherAudioSource != null && clip != null)
        {
            teacherAudioSource.clip = clip;
            teacherAudioSource.Play();
        }

        float duration = clip != null ? clip.length : 3.2f;
        ClassroomLifeIsStrangeUI.Instance?.ShowDialogueLine("Teacher", text, duration);

        yield return new WaitForSeconds(duration);
    }

    /// <summary>
    /// Starts the book opening and self-talk sequence when player presses E on the book.
    /// </summary>
    public void StartBookSequence()
    {
        if (currentPhase == ClassroomPhase.ReadingBook || currentPhase == ClassroomPhase.CinematicTransition) return;

        currentPhase = ClassroomPhase.ReadingBook;
        isSeatedLookActive = false;

        ClassroomLifeIsStrangeUI.Instance?.HidePrompt();

        _bookSequenceCoroutine = StartCoroutine(BookSequenceRoutine());
    }

    private IEnumerator BookSequenceRoutine()
    {
        // 1. Smooth camera blend to Book view
        Vector3 bookEyePos = new Vector3(-1.643f, 2.441f, -1.736f);
        Quaternion bookEyeRot = Quaternion.Euler(50.08f, 0f, 0f);

        float blendTime = 1.0f;
        float elapsed = 0f;
        Vector3 startPos = _activeCameraTransform != null ? _activeCameraTransform.position : seatedEyePosition;
        Quaternion startRot = _activeCameraTransform != null ? _activeCameraTransform.rotation : Quaternion.identity;

        while (elapsed < blendTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / blendTime);
            if (_activeCameraTransform != null)
            {
                _activeCameraTransform.position = Vector3.Lerp(startPos, bookEyePos, t);
                _activeCameraTransform.rotation = Quaternion.Slerp(startRot, bookEyeRot, t);
            }
            yield return null;
        }

        // 2. Open the book cover
        OnLookAtBook();
        yield return new WaitForSeconds(0.4f);

        // 3. Child self-talk monologue lines
        // Line 1: I am getting enough of this.
        string line1 = GetLoc("player_intro_01", "I am getting enough of this.");
        ClassroomLifeIsStrangeUI.Instance?.ShowDialogueLine("Child", line1, 3.2f);
        yield return StartCoroutine(WaitForSelfTalkAdvance(3.2f));

        // Line 2: Didn't we study that last week?
        string line2 = GetLoc("player_intro_02", "Didn't we study that last week?");
        ClassroomLifeIsStrangeUI.Instance?.ShowDialogueLine("Child", line2, 3.2f);
        yield return StartCoroutine(WaitForSelfTalkAdvance(3.2f));

        // Line 3: Wait... this illustration wasn't here before.
        string line3 = GetLoc("lookback_book_01", "Wait... this illustration wasn't here before.");
        ClassroomLifeIsStrangeUI.Instance?.ShowDialogueLine("Child", line3, 3.5f);
        yield return StartCoroutine(WaitForSelfTalkAdvance(3.5f));

        // Line 4: It feels like... it's calling to me.
        string line4 = GetLoc("lookback_book_02", "It feels like... it's calling to me.");
        ClassroomLifeIsStrangeUI.Instance?.ShowDialogueLine("Child", line4, 3.5f);
        yield return StartCoroutine(WaitForSelfTalkAdvance(3.5f));

        // 4. Trigger the cinematic psychological thriller transition into Gameplay3!
        yield return StartCoroutine(BetterCinematicTransitionRoutine());
    }

    private IEnumerator WaitForSelfTalkAdvance(float autoDuration)
    {
        float timer = 0f;
        while (timer < autoDuration)
        {
            timer += Time.deltaTime;
            // Allow skipping/advancing line with Space, E, or Mouse Click after a short grace period
            if (timer > 0.8f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
            {
                break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// Significantly improved cinematic transition:
    /// - Slow ominous push-in towards the dark void illustration on the page.
    /// - Classroom sounds and clock ticking muffle and drown away.
    /// - Deep sub-bass heartbeat/rumble swells up.
    /// - Subtle camera breathing wobble.
    /// - Smooth dissolve to black into Gameplay3.
    /// </summary>
    private IEnumerator BetterCinematicTransitionRoutine()
    {
        currentPhase = ClassroomPhase.CinematicTransition;
        Debug.Log("[ClassroomGazeScene] Beginning cinematic transition to Gameplay3...");

        // Start sub-bass rumble
        if (transitionRumbleSource != null && transitionRumbleClip != null)
        {
            transitionRumbleSource.clip = transitionRumbleClip;
            transitionRumbleSource.volume = 0f;
            transitionRumbleSource.Play();
        }

        Vector3 pushStartPos = _activeCameraTransform != null ? _activeCameraTransform.position : new Vector3(-1.643f, 2.441f, -1.736f);
        // Push in deep towards the page illustration at (-1.55, 1.70, -1.0)
        Vector3 pushTargetPos = new Vector3(-1.58f, 1.95f, -1.25f);
        Quaternion pushStartRot = _activeCameraTransform != null ? _activeCameraTransform.rotation : Quaternion.Euler(50.08f, 0f, 0f);
        Quaternion pushTargetRot = Quaternion.Euler(62.0f, 0f, 0f);

        float transitionDuration = 3.6f;
        float elapsed = 0f;

        float initialClockVol = classroomClockAudio != null ? classroomClockAudio.volume : 1f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Subtle eerie camera wobble
            float wobbleX = Mathf.Sin(elapsed * 8f) * 0.003f * t;
            float wobbleY = Mathf.Cos(elapsed * 11f) * 0.003f * t;
            Vector3 wobble = new Vector3(wobbleX, wobbleY, 0f);

            if (_activeCameraTransform != null)
            {
                _activeCameraTransform.position = Vector3.Lerp(pushStartPos, pushTargetPos, smoothT) + wobble;
                _activeCameraTransform.rotation = Quaternion.Slerp(pushStartRot, pushTargetRot, smoothT);
            }

            // Audio: Muffle/fade out clock & classroom, swell sub-bass rumble
            if (classroomClockAudio != null)
            {
                classroomClockAudio.volume = Mathf.Lerp(initialClockVol, 0.05f, smoothT);
            }
            if (transitionRumbleSource != null)
            {
                transitionRumbleSource.volume = Mathf.Lerp(0f, 0.65f, smoothT);
            }

            // Start screen fader dissolve halfway through
            if (t >= 0.45f && !_transitionTriggered)
            {
                _transitionTriggered = true;
                ScreenFader.TransitionToScene(targetSceneName, sceneTransitionDuration, shouldFadeAudio: true);
            }

            yield return null;
        }

        // Final transition trigger if not already active
        if (!_transitionTriggered && transitionToNextScene && !string.IsNullOrEmpty(targetSceneName))
        {
            _transitionTriggered = true;
            ScreenFader.TransitionToScene(targetSceneName, sceneTransitionDuration, shouldFadeAudio: true);
        }
    }

    public void OnLookAtBook()
    {
        if (_rotateCoroutine != null) StopCoroutine(_rotateCoroutine);
        _rotateCoroutine = StartCoroutine(RotateBook());
    }

    private IEnumerator RotateBook()
    {
        if (bookTransform == null) yield break;

        Quaternion start = Quaternion.Euler(startAngleX, 0f, 0f);
        Quaternion end = Quaternion.Euler(endAngleX, 0f, 0f);
        float t = 0f;

        while (t < openDuration)
        {
            t += Time.deltaTime;
            bookTransform.localRotation = Quaternion.Slerp(start, end, t / openDuration);
            yield return null;
        }

        bookTransform.localRotation = end;
    }

    private string GetLoc(string key, string fallback)
    {
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get(key);
            if (!string.IsNullOrEmpty(loc) && loc != key)
            {
                if (loc.StartsWith("Child: ")) loc = loc.Substring(7);
                return loc;
            }
        }
        return fallback;
    }
}