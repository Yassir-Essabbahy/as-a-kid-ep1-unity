using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central persistent manager for game settings (Audio, Sensitivity, FPS Cap, FPS Counter).
/// Persists settings to PlayerPrefs and applies changes live in real-time across all scenes.
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    private static GameSettingsManager _instance;
    public static GameSettingsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GameSettingsManager>();
                if (_instance == null)
                {
                    var go = new GameObject("GameSettingsManager");
                    _instance = go.AddComponent<GameSettingsManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (_instance == null)
        {
            var init = Instance;
        }
    }

    // PlayerPrefs Keys
    public const string KEY_SOUND_VOLUME = "Settings_SoundVolume";
    public const string KEY_SENSITIVITY = "Settings_Sensitivity";
    public const string KEY_LOCK_60_FPS = "Settings_Lock60FPS";
    public const string KEY_SHOW_FPS = "Settings_ShowFPS";

    // Defaults
    public const float DEFAULT_SOUND_VOLUME = 100f; // 0 to 100 %
    public const float DEFAULT_SENSITIVITY = 50f;   // 1 to 100 (50 = normal 2.0x)
    public const bool DEFAULT_LOCK_60_FPS = true;
    public const bool DEFAULT_SHOW_FPS = true;

    // Events
    public static event System.Action<float> OnSensitivityChanged;
    public static event System.Action<float> OnSoundVolumeChanged;
    public static event System.Action<bool> OnLockFPSChanged;
    public static event System.Action<bool> OnShowFPSChanged;

    // Live State
    private float _soundVolume = DEFAULT_SOUND_VOLUME;
    private float _sensitivity = DEFAULT_SENSITIVITY;
    private bool _lock60FPS = DEFAULT_LOCK_60_FPS;
    private bool _showFPS = DEFAULT_SHOW_FPS;

    // FPS Display elements
    private Canvas _fpsCanvas;
    private TextMeshProUGUI _fpsText;
    private RectTransform _fpsTextRect;
    private float _fpsAccumulator = 0f;
    private int _fpsFrames = 0;
    private int _lastDisplayedFps = 60;
    private float _fpsTimeLeft = 0.35f;
    private const float FPS_UPDATE_INTERVAL = 0.35f;

    #region Properties

    public float SoundVolume
    {
        get => _soundVolume;
        set
        {
            _soundVolume = Mathf.Clamp(value, 0f, 100f);
            PlayerPrefs.SetFloat(KEY_SOUND_VOLUME, _soundVolume);
            PlayerPrefs.Save();
            ApplySoundVolume();
            OnSoundVolumeChanged?.Invoke(_soundVolume);
        }
    }

    public float Sensitivity
    {
        get => _sensitivity;
        set
        {
            _sensitivity = Mathf.Clamp(value, 1f, 100f);
            PlayerPrefs.SetFloat(KEY_SENSITIVITY, _sensitivity);
            PlayerPrefs.Save();
            OnSensitivityChanged?.Invoke(_sensitivity);
        }
    }

    public bool Lock60FPS
    {
        get => _lock60FPS;
        set
        {
            _lock60FPS = value;
            PlayerPrefs.SetInt(KEY_LOCK_60_FPS, _lock60FPS ? 1 : 0);
            PlayerPrefs.Save();
            ApplyFrameRateLock();
            OnLockFPSChanged?.Invoke(_lock60FPS);
        }
    }

    public bool ShowFPSCounter
    {
        get => _showFPS;
        set
        {
            _showFPS = value;
            PlayerPrefs.SetInt(KEY_SHOW_FPS, _showFPS ? 1 : 0);
            PlayerPrefs.Save();
            ApplyFPSCounterVisibility();
            OnShowFPSChanged?.Invoke(_showFPS);
        }
    }

    /// <summary>
    /// Calculated mouse sensitivity for FirstPersonController (default at 50 is 2.0f).
    /// </summary>
    public float MouseSensitivity => Mathf.Max(0.1f, (_sensitivity / 50f) * 2.0f);

    /// <summary>
    /// Calculated mobile touch look sensitivity X (default at 50 is 0.18f).
    /// </summary>
    public float MobileLookSensitivityX => Mathf.Max(0.02f, (_sensitivity / 50f) * 0.18f);

    /// <summary>
    /// Calculated mobile touch look sensitivity Y (default at 50 is 0.14f).
    /// </summary>
    public float MobileLookSensitivityY => Mathf.Max(0.02f, (_sensitivity / 50f) * 0.14f);

    #endregion

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        ApplyAllSettings();
        CreateFPSCounterUI();

        SceneManager.sceneLoaded += OnSceneLoaded;
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }
    }

    private void HandleLanguageChanged(string _)
    {
        ApplyFPSText(_lastDisplayedFps);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reapply audio and framerate on scene load to ensure scenes don't override them
        ApplyAllSettings();
        UpdateFPSCounterLayoutForScene(scene.name);
    }

    public void LoadSettings()
    {
        _soundVolume = PlayerPrefs.GetFloat(KEY_SOUND_VOLUME, DEFAULT_SOUND_VOLUME);
        _sensitivity = PlayerPrefs.GetFloat(KEY_SENSITIVITY, DEFAULT_SENSITIVITY);
        _lock60FPS = PlayerPrefs.GetInt(KEY_LOCK_60_FPS, DEFAULT_LOCK_60_FPS ? 1 : 0) == 1;
        _showFPS = PlayerPrefs.GetInt(KEY_SHOW_FPS, DEFAULT_SHOW_FPS ? 1 : 0) == 1;
    }

    public void ApplyAllSettings()
    {
        ApplySoundVolume();
        ApplyFrameRateLock();
        ApplyFPSCounterVisibility();
    }

    public void ApplySoundVolume()
    {
        AudioListener.volume = Mathf.Clamp01(_soundVolume / 100f);
    }

    public void ApplyFrameRateLock()
    {
        QualitySettings.vSyncCount = 0; // Must be 0 for targetFrameRate to work in Unity
        Application.targetFrameRate = _lock60FPS ? 60 : -1;
    }

    public void ApplyFPSCounterVisibility()
    {
        if (_fpsCanvas != null)
        {
            _fpsCanvas.gameObject.SetActive(_showFPS);
        }
    }

    private void CreateFPSCounterUI()
    {
        if (_fpsCanvas != null) return;

        var canvasObj = new GameObject("FPSCounterCanvas", typeof(RectTransform));
        canvasObj.transform.SetParent(transform, false);

        _fpsCanvas = canvasObj.AddComponent<Canvas>();
        _fpsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _fpsCanvas.sortingOrder = 32760; // Always top-most

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Container Pill
        var pillObj = new GameObject("FPS_Pill", typeof(RectTransform));
        pillObj.transform.SetParent(canvasObj.transform, false);
        _fpsTextRect = pillObj.GetComponent<RectTransform>();
        _fpsTextRect.anchorMin = new Vector2(1f, 1f);
        _fpsTextRect.anchorMax = new Vector2(1f, 1f);
        _fpsTextRect.pivot = new Vector2(1f, 1f);
        _fpsTextRect.sizeDelta = new Vector2(130f, 36f);

        // Subtle semi-transparent dark pill background for readability
        var bgImg = pillObj.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.65f);
        bgImg.raycastTarget = false;

        // Text Element
        var textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(pillObj.transform, false);
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 0f);
        textRt.offsetMax = new Vector2(-8f, 0f);

        _fpsText = textObj.AddComponent<TextMeshProUGUI>();
        var font = Resources.Load<TMP_FontAsset>("Fonts/GeistPixel-Regular-VariableFont_ELSH SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/FriendlySans-Regular SDF");
        if (font != null) _fpsText.font = font;

        _fpsText.fontSize = 20f;
        _fpsText.fontStyle = FontStyles.Bold;
        _fpsText.color = new Color(0.35f, 1.0f, 0.45f, 1.0f); // Retro phosphor green
        _fpsText.alignment = TextAlignmentOptions.Center;
        _fpsText.raycastTarget = false;
        ApplyFPSText(60);

        UpdateFPSCounterLayoutForScene(SceneManager.GetActiveScene().name);
        ApplyFPSCounterVisibility();
    }

    private void ApplyFPSText(int fpsValue)
    {
        if (_fpsText == null) return;
        _lastDisplayedFps = fpsValue;
        string text = LocalizationManager.Format("ui_fps_format", "{0} FPS", fpsValue);
        LocalizationManager.ApplyText(_fpsText, text, preserveAlignment: true);
    }

    private void UpdateFPSCounterLayoutForScene(string sceneName)
    {
        if (_fpsTextRect == null) return;

        // Position neatly in the top-right corner, avoiding language dropdown in MainMenu
        if (sceneName == "MainMenu")
        {
            _fpsTextRect.anchoredPosition = new Vector2(-305f, -22f);
        }
        else
        {
            // In gameplay scenes: place at -105 to avoid mobile pause button (at -36, -36)
            _fpsTextRect.anchoredPosition = new Vector2(-105f, -22f);
        }
    }

    private void Update()
    {
        if (!_showFPS || _fpsText == null) return;

        _fpsTimeLeft -= Time.unscaledDeltaTime;
        _fpsAccumulator += 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        _fpsFrames++;

        if (_fpsTimeLeft <= 0f)
        {
            float fps = _fpsAccumulator / _fpsFrames;
            int roundedFps = Mathf.RoundToInt(fps);
            ApplyFPSText(roundedFps);

            // Color-code FPS smoothly: Green (55+), Yellow (30-54), Red (<30)
            if (roundedFps >= 55)
                _fpsText.color = new Color(0.35f, 1.0f, 0.45f, 1.0f); // Green
            else if (roundedFps >= 30)
                _fpsText.color = new Color(1.0f, 0.85f, 0.25f, 1.0f); // Yellow
            else
                _fpsText.color = new Color(1.0f, 0.30f, 0.30f, 1.0f); // Red

            _fpsTimeLeft = FPS_UPDATE_INTERVAL;
            _fpsAccumulator = 0f;
            _fpsFrames = 0;
        }
    }
}
