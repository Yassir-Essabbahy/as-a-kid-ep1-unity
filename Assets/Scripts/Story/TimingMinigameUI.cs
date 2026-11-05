using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Search the Trash" timing minigame overlay.
/// Styled with a pure black background and crisp border to match PromptBox and TalkPanel.
/// Freezes gameplay, joystick, and camera look.
/// A marker ping-pongs across a horizontal bar; the player taps/presses interact
/// to stop it. If stopped inside the random green success zone, awards the item.
/// On fail, provides shake, red flash, and SFX feedback, then retries with a new zone.
/// </summary>
[DisallowMultipleComponent]
public class TimingMinigameUI : MonoBehaviour
{
    private static TimingMinigameUI _instance;
    public static TimingMinigameUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<TimingMinigameUI>();
                if (_instance == null)
                {
                    Canvas canvas = FindAnyObjectByType<Canvas>();
                    GameObject host = new GameObject("TimingMinigameUI");
                    if (canvas != null)
                    {
                        host.transform.SetParent(canvas.transform, false);
                    }
                    _instance = host.AddComponent<TimingMinigameUI>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("UI Root & Hierarchy")]
    public GameObject minigameRoot;
    public RectTransform shakeContainer;
    public RectTransform barTrackRect;
    public RectTransform successZoneRect;
    public RectTransform markerRect;
    public Image flashOverlay;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI hintText;
    public Button fullScreenTapButton;

    [Header("Tuning Settings")]
    [Tooltip("Marker travel speed in bar-lengths per second.")]
    public float initialMarkerSpeed = 1.35f;
    [Tooltip("Green zone width as a fraction of the bar (e.g. 0.22 = 22% of bar width).")]
    [Range(0.05f, 0.5f)]
    public float successZoneWidthFraction = 0.22f;
    [Tooltip("Marker speed increase added per failed attempt.")]
    public float speedIncreasePerFail = 0.15f;
    [Tooltip("Maximum clamp on marker speed.")]
    public float maxMarkerSpeed = 3.2f;

    [Header("Feedback Settings")]
    public float failShakeDuration = 0.35f;
    public float failShakeMagnitude = 14f;
    public float failFlashDuration = 0.3f;
    public Color failFlashColor = new Color(0.85f, 0.1f, 0.1f, 0.45f);

    [Header("Audio SFX (Serialized & Optional)")]
    public AudioSource audioSource;
    public AudioClip rummagingLoopClip;
    public AudioClip tickClip;
    public AudioClip successClip;
    public AudioClip failClip;

    // Runtime state
    private TrashTicketSource _activeSource;
    private bool _isMinigameActive = false;
    private bool _isHandlingFeedback = false;
    private float _currentSpeed;
    private float _markerProgress = 0f; // 0.0 to 1.0
    private float _markerDirection = 1f; // +1 = moving right, -1 = moving left
    private float _zoneStartNormalized = 0.3f;
    private float _zoneEndNormalized = 0.52f;
    private float _openInputCooldown = 0.18f;
    private float _openTimer = 0f;

    // Player lock state tracking
    private FirstPersonController _fpsController;
    private MonoBehaviour _genericPlayerController;
    private CursorLockMode _preMinigameCursorLock;
    private bool _preMinigameCursorVisible;
    private bool _playerWasLocked = false;

    // Procedural audio fallbacks
    private AudioClip _procRummageLoop;
    private AudioClip _procTick;
    private AudioClip _procSuccess;
    private AudioClip _procFail;

    public bool IsActive => _isMinigameActive;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (minigameRoot == null)
        {
            BuildDefaultMinigameUI();
        }

        SetupAudio();
        GenerateProceduralAudio();

        if (fullScreenTapButton != null)
        {
            fullScreenTapButton.onClick.RemoveListener(OnTapOrInteractPressed);
            fullScreenTapButton.onClick.AddListener(OnTapOrInteractPressed);
        }

        if (minigameRoot != null)
        {
            minigameRoot.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (_isMinigameActive)
        {
            AbortMinigame();
        }
    }

    private void OnDestroy()
    {
        if (_isMinigameActive)
        {
            AbortMinigame();
        }
    }

    /// <summary>
    /// Launches the Search the Trash minigame.
    /// </summary>
    public void StartMinigame(TrashTicketSource source)
    {
        if (!TrashTicketSource.IsFuseboxSolved)
        {
            Debug.LogWarning("[TimingMinigameUI] Attempted to start minigame before fusebox puzzle is solved. Aborted.");
            return;
        }

        _activeSource = source;
        _isMinigameActive = true;
        _isHandlingFeedback = false;
        _openTimer = 0f;
        _currentSpeed = initialMarkerSpeed;
        _markerProgress = 0f;
        _markerDirection = 1f;

        // Localized Header
        if (titleText != null)
        {
            string t = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("trash_searching_title") : "Searching the trash...";
            titleText.text = t;
        }

        if (hintText != null)
        {
            string h = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("trash_searching_hint") : "Stop the marker in the green zone!";
            hintText.text = h;
        }

        // Lock Player Movement and Camera
        LockPlayerControls();

        // Roll random success zone position
        SetupRandomSuccessZone();

        // Position Marker at Start
        UpdateMarkerVisual();

        // Enable UI Root
        if (minigameRoot != null)
        {
            minigameRoot.SetActive(true);
        }

        // Start Rummaging Loop Audio
        PlayRummageLoop();
    }

    private void Update()
    {
        if (!_isMinigameActive || _isHandlingFeedback)
            return;

        _openTimer += Time.unscaledDeltaTime;

        // Ping-pong marker movement
        _markerProgress += _markerDirection * _currentSpeed * Time.unscaledDeltaTime;

        if (_markerProgress >= 1f)
        {
            _markerProgress = 1f;
            _markerDirection = -1f;
            PlaySFX(tickClip != null ? tickClip : _procTick, 0.4f);
        }
        else if (_markerProgress <= 0f)
        {
            _markerProgress = 0f;
            _markerDirection = 1f;
            PlaySFX(tickClip != null ? tickClip : _procTick, 0.4f);
        }

        UpdateMarkerVisual();

        // Input check (PC / Gamepad / Mobile)
        if (_openTimer >= _openInputCooldown && CheckStopInput())
        {
            OnTapOrInteractPressed();
        }
    }

    private bool CheckStopInput()
    {
        if (Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetMouseButtonDown(0) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.JoystickButton0))
        {
            return true;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Invoked when the player taps the screen or presses interact to stop the marker.
    /// </summary>
    public void OnTapOrInteractPressed()
    {
        if (!_isMinigameActive || _isHandlingFeedback || _openTimer < _openInputCooldown)
            return;

        EvaluateAttempt();
    }

    private void EvaluateAttempt()
    {
        _isHandlingFeedback = true;
        StopRummageLoop();

        // Check if marker is within the green success zone
        bool isSuccess = (_markerProgress >= _zoneStartNormalized && _markerProgress <= _zoneEndNormalized);

        if (isSuccess)
        {
            StartCoroutine(HandleSuccessRoutine());
        }
        else
        {
            StartCoroutine(HandleFailRoutine());
        }
    }

    private IEnumerator HandleSuccessRoutine()
    {
        bool hasTicket = _activeSource != null && _activeSource.hasTicket;

        if (hasTicket)
        {
            PlaySFX(successClip != null ? successClip : _procSuccess, 0.9f);

            if (hintText != null)
            {
                string s = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("trash_searching_success") : "Found a train ticket!";
                hintText.text = s;
            }

            // Brief freeze to show successful timing
            yield return new WaitForSecondsRealtime(0.6f);

            CloseMinigame();

            if (_activeSource != null)
            {
                _activeSource.OnMinigameSuccessTicket();
            }
        }
        else
        {
            // Empty trash can: play tick / rummage feedback
            PlaySFX(tickClip != null ? tickClip : _procTick, 0.8f);

            if (hintText != null)
            {
                string emptyStr = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("trash_searching_empty") : null;
                if (string.IsNullOrEmpty(emptyStr) || emptyStr.StartsWith("["))
                {
                    emptyStr = "Nothing here but old trash... Try another trash can!";
                }
                hintText.text = emptyStr;
            }

            yield return new WaitForSecondsRealtime(0.85f);

            CloseMinigame();

            if (_activeSource != null)
            {
                _activeSource.OnMinigameSuccessEmpty();
            }
        }
    }

    private IEnumerator HandleFailRoutine()
    {
        PlaySFX(failClip != null ? failClip : _procFail, 0.85f);

        if (hintText != null)
        {
            string f = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("trash_searching_failed") : "Nothing found! Try again...";
            hintText.text = f;
        }

        // Increase speed slightly on failure
        _currentSpeed = Mathf.Min(_currentSpeed + speedIncreasePerFail, maxMarkerSpeed);

        // Shake container & red flash simultaneously
        float elapsed = 0f;
        Vector2 origPos = shakeContainer != null ? shakeContainer.anchoredPosition : Vector2.zero;

        while (elapsed < failShakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float norm = Mathf.Clamp01(elapsed / failShakeDuration);

            // Screen/Container Shake
            if (shakeContainer != null)
            {
                float mag = failShakeMagnitude * (1f - norm);
                shakeContainer.anchoredPosition = origPos + UnityEngine.Random.insideUnitCircle * mag;
            }

            // Red Flash
            if (flashOverlay != null)
            {
                float flashAlpha = Mathf.Sin(norm * Mathf.PI) * failFlashColor.a;
                flashOverlay.color = new Color(failFlashColor.r, failFlashColor.g, failFlashColor.b, flashAlpha);
            }

            yield return null;
        }

        if (shakeContainer != null)
            shakeContainer.anchoredPosition = origPos;

        if (flashOverlay != null)
            flashOverlay.color = new Color(failFlashColor.r, failFlashColor.g, failFlashColor.b, 0f);

        // Reset with new randomized green zone
        SetupRandomSuccessZone();

        // Restart rummaging SFX and resume ping-pong
        PlayRummageLoop();
        _isHandlingFeedback = false;
    }

    private void SetupRandomSuccessZone()
    {
        float clampedFraction = Mathf.Clamp(successZoneWidthFraction, 0.06f, 0.5f);
        _zoneStartNormalized = UnityEngine.Random.Range(0.04f, 1f - clampedFraction - 0.04f);
        _zoneEndNormalized = _zoneStartNormalized + clampedFraction;

        if (barTrackRect != null && successZoneRect != null)
        {
            float barWidth = Mathf.Max(60f, barTrackRect.rect.width);
            float zoneWidth = barWidth * clampedFraction;

            successZoneRect.sizeDelta = new Vector2(zoneWidth, successZoneRect.sizeDelta.y);

            // Compute center in bar track coordinates (-barWidth/2 to +barWidth/2)
            float zoneCenterNorm = (_zoneStartNormalized + _zoneEndNormalized) * 0.5f;
            float zoneCenterX = Mathf.Lerp(-barWidth * 0.5f, barWidth * 0.5f, zoneCenterNorm);

            successZoneRect.anchoredPosition = new Vector2(zoneCenterX, successZoneRect.anchoredPosition.y);
        }
    }

    private void UpdateMarkerVisual()
    {
        if (barTrackRect != null && markerRect != null)
        {
            float barWidth = Mathf.Max(60f, barTrackRect.rect.width);
            float markerX = Mathf.Lerp(-barWidth * 0.5f, barWidth * 0.5f, _markerProgress);
            markerRect.anchoredPosition = new Vector2(markerX, markerRect.anchoredPosition.y);
        }
    }

    public void CloseMinigame()
    {
        _isMinigameActive = false;
        _isHandlingFeedback = false;
        StopRummageLoop();

        if (minigameRoot != null)
        {
            minigameRoot.SetActive(false);
        }

        UnlockPlayerControls();
    }

    public void AbortMinigame()
    {
        _isMinigameActive = false;
        _isHandlingFeedback = false;
        StopRummageLoop();

        if (minigameRoot != null)
        {
            minigameRoot.SetActive(false);
        }

        UnlockPlayerControls();
    }

    private void LockPlayerControls()
    {
        if (_fpsController == null)
            _fpsController = FindAnyObjectByType<FirstPersonController>();

        if (_fpsController != null)
        {
            _fpsController.SetControlLocked(true);
        }
        else
        {
            var pc = GameObject.FindWithTag("Player");
            if (pc != null)
            {
                _genericPlayerController = pc.GetComponent<MonoBehaviour>();
                if (_genericPlayerController != null) _genericPlayerController.enabled = false;
            }
        }

        _preMinigameCursorLock = Cursor.lockState;
        _preMinigameCursorVisible = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _playerWasLocked = true;
    }

    private void UnlockPlayerControls()
    {
        if (!_playerWasLocked) return;

        if (_fpsController != null)
        {
            _fpsController.SetControlLocked(false);
        }
        else if (_genericPlayerController != null)
        {
            _genericPlayerController.enabled = true;
        }

        Cursor.lockState = _preMinigameCursorLock;
        Cursor.visible = _preMinigameCursorVisible;
        _playerWasLocked = false;
    }

    private void SetupAudio()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }
    }

    private void PlaySFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private void PlayRummageLoop()
    {
        AudioClip loop = rummagingLoopClip != null ? rummagingLoopClip : _procRummageLoop;
        if (loop != null && audioSource != null)
        {
            audioSource.clip = loop;
            audioSource.loop = true;
            audioSource.volume = 0.45f;
            audioSource.Play();
        }
    }

    private void StopRummageLoop()
    {
        if (audioSource != null && audioSource.isPlaying && audioSource.loop)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Auto-constructs the PSX timing minigame UI hierarchy under canvas if unassigned.
    /// Matches the pure black theme, subtle white border, and pixel font of PromptBox & TalkPanel.
    /// </summary>
    public void BuildDefaultMinigameUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null) transform.SetParent(canvas.transform, false);
        }

        // Apply project font: GeistPixel-Regular-VariableFont_ELSH SDF
        TMP_FontAsset pixelFont = Resources.Load<TMP_FontAsset>("Fonts/GeistPixel-Regular-VariableFont_ELSH SDF");
        if (pixelFont == null)
        {
            var existingTmps = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var t in existingTmps)
            {
                if (t.font != null && t.font.name.Contains("GeistPixel"))
                {
                    pixelFont = t.font;
                    break;
                }
            }
        }

        // Root Container
        minigameRoot = new GameObject("TimingMinigame_Root", typeof(RectTransform));
        minigameRoot.transform.SetParent(transform, false);
        RectTransform rootRect = minigameRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // Fullscreen Tap Button (for mobile anywhere-tap)
        GameObject fullBtnObj = new GameObject("FullScreenTapTarget", typeof(RectTransform), typeof(Image), typeof(Button));
        fullBtnObj.transform.SetParent(minigameRoot.transform, false);
        RectTransform fbRect = fullBtnObj.GetComponent<RectTransform>();
        fbRect.anchorMin = Vector2.zero;
        fbRect.anchorMax = Vector2.one;
        fbRect.offsetMin = Vector2.zero;
        fbRect.offsetMax = Vector2.zero;
        Image fbImg = fullBtnObj.GetComponent<Image>();
        fbImg.color = new Color(0f, 0f, 0f, 0.65f); // Dimmed background
        fullScreenTapButton = fullBtnObj.GetComponent<Button>();

        // Shake Container
        GameObject shakeObj = new GameObject("ShakeContainer", typeof(RectTransform));
        shakeObj.transform.SetParent(minigameRoot.transform, false);
        shakeContainer = shakeObj.GetComponent<RectTransform>();
        shakeContainer.anchorMin = new Vector2(0.5f, 0.5f);
        shakeContainer.anchorMax = new Vector2(0.5f, 0.5f);
        shakeContainer.pivot = new Vector2(0.5f, 0.5f);
        shakeContainer.sizeDelta = new Vector2(500f, 200f);

        // Minigame Frame Box
        GameObject boxObj = new GameObject("MinigameFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
        boxObj.transform.SetParent(shakeContainer, false);
        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.anchorMin = Vector2.zero;
        boxRect.anchorMax = Vector2.one;
        boxRect.offsetMin = Vector2.zero;
        boxRect.offsetMax = Vector2.zero;
        Image boxImg = boxObj.GetComponent<Image>();
        boxImg.color = new Color(0.0f, 0.0f, 0.0f, 0.95f); // Pure Black matching canvas
        Outline border = boxObj.GetComponent<Outline>();
        border.effectColor = new Color(1.0f, 1.0f, 1.0f, 0.45f); // White outline matching PromptBox / buttons
        border.effectDistance = new Vector2(1.2f, -1.2f);

        // Header Title
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(boxObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -14f);
        titleRect.sizeDelta = new Vector2(460f, 32f);
        titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.fontSize = 22f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        if (pixelFont != null) titleText.font = pixelFont;

        // Bar Track
        GameObject trackObj = new GameObject("BarTrack", typeof(RectTransform), typeof(Image), typeof(Outline));
        trackObj.transform.SetParent(boxObj.transform, false);
        barTrackRect = trackObj.GetComponent<RectTransform>();
        barTrackRect.anchorMin = new Vector2(0.5f, 0.5f);
        barTrackRect.anchorMax = new Vector2(0.5f, 0.5f);
        barTrackRect.pivot = new Vector2(0.5f, 0.5f);
        barTrackRect.sizeDelta = new Vector2(420f, 26f);
        barTrackRect.anchoredPosition = new Vector2(0f, -5f);
        Image trackImg = trackObj.GetComponent<Image>();
        trackImg.color = new Color(0.06f, 0.06f, 0.06f, 1f); // Deep black track
        Outline trackBorder = trackObj.GetComponent<Outline>();
        trackBorder.effectColor = new Color(1.0f, 1.0f, 1.0f, 0.35f);
        trackBorder.effectDistance = new Vector2(1f, -1f);

        // Success Zone (Green)
        GameObject zoneObj = new GameObject("SuccessZone", typeof(RectTransform), typeof(Image));
        zoneObj.transform.SetParent(barTrackRect, false);
        successZoneRect = zoneObj.GetComponent<RectTransform>();
        successZoneRect.anchorMin = new Vector2(0.5f, 0.5f);
        successZoneRect.anchorMax = new Vector2(0.5f, 0.5f);
        successZoneRect.pivot = new Vector2(0.5f, 0.5f);
        successZoneRect.sizeDelta = new Vector2(90f, 26f);
        Image zoneImg = zoneObj.GetComponent<Image>();
        zoneImg.color = new Color(0.20f, 0.85f, 0.40f, 0.95f); // Crisp vibrant green

        // Moving Marker
        GameObject markerObj = new GameObject("Marker", typeof(RectTransform), typeof(Image), typeof(Outline));
        markerObj.transform.SetParent(barTrackRect, false);
        markerRect = markerObj.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(10f, 36f);
        Image markerImg = markerObj.GetComponent<Image>();
        markerImg.color = Color.white; // Crisp white cursor
        Outline markerOutline = markerObj.GetComponent<Outline>();
        markerOutline.effectColor = Color.black;
        markerOutline.effectDistance = new Vector2(1f, -1f);

        // Instruction / Hint Text
        GameObject hintObj = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintObj.transform.SetParent(boxObj.transform, false);
        RectTransform hintRect = hintObj.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 14f);
        hintRect.sizeDelta = new Vector2(460f, 28f);
        hintText = hintObj.GetComponent<TextMeshProUGUI>();
        hintText.fontSize = 17f;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(0.85f, 0.85f, 0.85f, 1.0f);
        if (pixelFont != null) hintText.font = pixelFont;

        // Red Flash Overlay
        GameObject flashObj = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
        flashObj.transform.SetParent(boxObj.transform, false);
        RectTransform flashRect = flashObj.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
        flashOverlay = flashObj.GetComponent<Image>();
        flashOverlay.raycastTarget = false;
        flashOverlay.color = new Color(1f, 0f, 0f, 0f);

        minigameRoot.SetActive(false);
    }

    #region Procedural PSX Audio Synthesis
    private void GenerateProceduralAudio()
    {
        _procRummageLoop = CreateRummageLoopClip();
        _procTick = CreateTickClip();
        _procSuccess = CreateSuccessFanfareClip();
        _procFail = CreateFailBuzzerClip();
    }

    private AudioClip CreateRummageLoopClip()
    {
        int sampleRate = 22050;
        float duration = 1.0f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.35f;
            float scrape = Mathf.Sin(2f * Mathf.PI * 180f * ((float)i / sampleRate)) * 0.15f;
            data[i] = (noise + scrape) * 0.3f;
        }

        var clip = AudioClip.Create("PSX_RummageLoop", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateTickClip()
    {
        int sampleRate = 44100;
        float duration = 0.04f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float tone = Mathf.Sin(2f * Mathf.PI * 880f * ((float)i / sampleRate));
            float env = Mathf.Exp(-t * 24f);
            data[i] = tone * env * 0.4f;
        }

        var clip = AudioClip.Create("PSX_MinigameTick", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateSuccessFanfareClip()
    {
        int sampleRate = 44100;
        float duration = 0.55f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = t < 0.14f ? 523.25f : (t < 0.28f ? 659.25f : 783.99f); // C5, E5, G5
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f;
            float harm = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;
            float env = Mathf.Exp(-((float)i / samples) * 4f);
            data[i] = (tone + harm) * env * 0.5f;
        }

        var clip = AudioClip.Create("PSX_MinigameSuccess", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateFailBuzzerClip()
    {
        int sampleRate = 44100;
        float duration = 0.3f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float tone = Mathf.Sin(2f * Mathf.PI * 130f * ((float)i / sampleRate)); // Low buzzy tone
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.3f;
            float env = Mathf.Exp(-t * 6f);
            data[i] = (tone + noise) * env * 0.55f;
        }

        var clip = AudioClip.Create("PSX_MinigameFail", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
    #endregion
}
