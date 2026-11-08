using TMPro;
using UnityEngine;

/// <summary>
/// Handles language selection via TMP_Dropdown in the Main Menu.
/// Syncs initial value from PlayerPrefs / LocalizationManager.Instance,
/// saves selection, and updates LocalizationManager.
/// Options:
/// [0] English ("en")
/// [1] Arabic  ("ar")
/// [2] French  ("fr")
/// </summary>
public class LanguageDropdownHandler : MonoBehaviour
{
    private TMP_Dropdown _dropdown;

    private void Awake()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
    }

    private void Start()
    {
        InitializeDropdown();
    }

    private void OnEnable()
    {
        InitializeDropdown();
    }

    private void InitializeDropdown()
    {
        if (_dropdown == null) _dropdown = GetComponent<TMP_Dropdown>();
        if (_dropdown == null) return;

        // Get saved language code
        string current = LocalizationManager.Instance != null 
            ? LocalizationManager.Instance.CurrentLanguage 
            : PlayerPrefs.GetString("SelectedLanguage", "en");

        int index = 0; // en
        if (string.Equals(current, "ar", System.StringComparison.OrdinalIgnoreCase))
            index = 1;
        else if (string.Equals(current, "fr", System.StringComparison.OrdinalIgnoreCase))
            index = 2;

        _dropdown.SetValueWithoutNotify(index);

        _dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        _dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    private void OnDropdownValueChanged(int index)
    {
        string langCode = "en";
        switch (index)
        {
            case 0:
                langCode = "en";
                break;
            case 1:
                langCode = "ar";
                break;
            case 2:
                langCode = "fr";
                break;
        }

        Debug.Log($"[LanguageDropdownHandler] Language changed to: {langCode} (index {index})");

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.CurrentLanguage = langCode;
        }
        else
        {
            PlayerPrefs.SetString("SelectedLanguage", langCode);
            PlayerPrefs.Save();
        }
    }
}
