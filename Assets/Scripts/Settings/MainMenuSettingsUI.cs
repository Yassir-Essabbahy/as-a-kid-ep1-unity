using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attached to Canvas/OptionsMenu/Settings in MainMenu.
/// Hooks up the UI Sliders and Toggles to GameSettingsManager for live, real-time updates.
/// </summary>
public class MainMenuSettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    public Slider sensitivitySlider;
    public TextMeshProUGUI sensitivityLabel;
    public Slider soundSlider;
    public TextMeshProUGUI soundLabel;

    [Header("Toggles")]
    public Toggle fpsCounterToggle;
    public Toggle lockFPSToggle;

    private bool _isUpdatingUI = false;

    private void Awake()
    {
        AutoResolveReferences();
        EnsureRaycastTargets();
    }

    private void OnEnable()
    {
        AutoResolveReferences();
        SyncFromSettings();
        BindListeners();
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }
    }

    private void OnDisable()
    {
        UnbindListeners();
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }
    }

    private void HandleLanguageChanged(string _)
    {
        SyncFromSettings();
    }

    public void AutoResolveReferences()
    {
        if (sensitivitySlider == null)
        {
            var t = transform.Find("SensitivtySlider");
            if (t != null) sensitivitySlider = t.GetComponent<Slider>();
        }

        if (sensitivityLabel == null && sensitivitySlider != null)
        {
            var t = sensitivitySlider.transform.Find("SensitivityText");
            if (t != null) sensitivityLabel = t.GetComponent<TextMeshProUGUI>();
        }

        if (soundSlider == null)
        {
            var t = transform.Find("SoundSlider");
            if (t != null) soundSlider = t.GetComponent<Slider>();
        }

        if (soundLabel == null && soundSlider != null)
        {
            var t = soundSlider.transform.Find("SliderText");
            if (t != null) soundLabel = t.GetComponent<TextMeshProUGUI>();
        }

        if (fpsCounterToggle == null)
        {
            var t = transform.Find("FPSCounter_Toggle");
            if (t != null) fpsCounterToggle = t.GetComponent<Toggle>();
        }

        if (lockFPSToggle == null)
        {
            var t = transform.Find("LockFPS_Toggle");
            if (t != null) lockFPSToggle = t.GetComponent<Toggle>();
        }
    }

    private void EnsureRaycastTargets()
    {
        // Ensure backgrounds on sliders and toggles receive raycasts so clicking anywhere on them works
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 1f;
            sensitivitySlider.maxValue = 100f;
            sensitivitySlider.wholeNumbers = true;
            var bg = sensitivitySlider.transform.Find("Background")?.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;
        }

        if (soundSlider != null)
        {
            soundSlider.minValue = 0f;
            soundSlider.maxValue = 100f;
            soundSlider.wholeNumbers = true;
            var bg = soundSlider.transform.Find("Background")?.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;
        }

        if (fpsCounterToggle != null)
        {
            var bg = fpsCounterToggle.transform.Find("Background")?.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;
            var lbl = fpsCounterToggle.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl != null) lbl.raycastTarget = true;
        }

        if (lockFPSToggle != null)
        {
            var bg = lockFPSToggle.transform.Find("Background")?.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;
            var lbl = lockFPSToggle.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl != null) lbl.raycastTarget = true;
        }
    }

    public void SyncFromSettings()
    {
        if (GameSettingsManager.Instance == null) return;

        _isUpdatingUI = true;

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = GameSettingsManager.Instance.Sensitivity;
            UpdateSensitivityLabel(sensitivitySlider.value);
        }

        if (soundSlider != null)
        {
            soundSlider.value = GameSettingsManager.Instance.SoundVolume;
            UpdateSoundLabel(soundSlider.value);
        }

        if (lockFPSToggle != null)
        {
            lockFPSToggle.isOn = GameSettingsManager.Instance.Lock60FPS;
        }

        if (fpsCounterToggle != null)
        {
            fpsCounterToggle.isOn = GameSettingsManager.Instance.ShowFPSCounter;
        }

        _isUpdatingUI = false;
    }

    private void BindListeners()
    {
        UnbindListeners();

        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        if (soundSlider != null)
            soundSlider.onValueChanged.AddListener(OnSoundChanged);

        if (lockFPSToggle != null)
            lockFPSToggle.onValueChanged.AddListener(OnLockFPSChanged);

        if (fpsCounterToggle != null)
            fpsCounterToggle.onValueChanged.AddListener(OnFPSCounterChanged);
    }

    private void UnbindListeners()
    {
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);

        if (soundSlider != null)
            soundSlider.onValueChanged.RemoveListener(OnSoundChanged);

        if (lockFPSToggle != null)
            lockFPSToggle.onValueChanged.RemoveListener(OnLockFPSChanged);

        if (fpsCounterToggle != null)
            fpsCounterToggle.onValueChanged.RemoveListener(OnFPSCounterChanged);
    }

    private void OnSensitivityChanged(float value)
    {
        if (_isUpdatingUI) return;
        UpdateSensitivityLabel(value);
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.Sensitivity = value;
        }
    }

    private void OnSoundChanged(float value)
    {
        if (_isUpdatingUI) return;
        UpdateSoundLabel(value);
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.SoundVolume = value;
        }
    }

    private void OnLockFPSChanged(bool isOn)
    {
        if (_isUpdatingUI) return;
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.Lock60FPS = isOn;
        }
    }

    private void OnFPSCounterChanged(bool isOn)
    {
        if (_isUpdatingUI) return;
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.ShowFPSCounter = isOn;
        }
    }

    private void UpdateSensitivityLabel(float value)
    {
        if (sensitivityLabel != null)
        {
            string formatted = LocalizationManager.Format("ui_settings_sensitivity", "Sensitivity: {0}", Mathf.RoundToInt(value));
            LocalizationManager.ApplyText(sensitivityLabel, formatted, preserveAlignment: true);
        }
    }

    private void UpdateSoundLabel(float value)
    {
        if (soundLabel != null)
        {
            string formatted = LocalizationManager.Format("ui_settings_sound", "Sound: {0}%", Mathf.RoundToInt(value));
            LocalizationManager.ApplyText(soundLabel, formatted, preserveAlignment: true);
        }
    }
}
