using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Handles language selection via TMP_Dropdown in the Main Menu.
/// Syncs initial value from PlayerPrefs / LocalizationManager.Instance,
/// saves selection, and updates LocalizationManager + dropdown RTL labels.
/// Options:
/// [0] English ("en")
/// [1] Arabic  ("ar")
/// [2] French  ("fr")
/// [3] Darija  ("darija")
/// </summary>
public class LanguageDropdownHandler : MonoBehaviour
{
    private TMP_Dropdown _dropdown;
    private readonly string[] _rawOptionLabels = new string[4];
    private Transform _lastStyledListTransform;

    private static readonly string[] OptionKeys = new string[]
    {
        "lang_en",
        "lang_ar",
        "lang_fr",
        "lang_darija"
    };

    private static readonly string[] DefaultLabels = new string[]
    {
        "English",
        "Arabic",
        "French",
        "Darija"
    };

    private void Awake()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
        var _ = LocalizationManager.Instance;
    }

    private void Start()
    {
        InitializeDropdown();
    }

    private void OnEnable()
    {
        InitializeDropdown();
    }

    private void LateUpdate()
    {
        if (_dropdown == null) return;

        Transform dropdownList = _dropdown.transform.Find("Dropdown List");
        if (dropdownList != null && dropdownList.gameObject.activeInHierarchy)
        {
            if (_lastStyledListTransform != dropdownList)
            {
                _lastStyledListTransform = dropdownList;
                StyleOpenDropdownItems(dropdownList);
            }
        }
        else
        {
            _lastStyledListTransform = null;
        }
    }

    private void InitializeDropdown()
    {
        if (_dropdown == null) _dropdown = GetComponent<TMP_Dropdown>();
        if (_dropdown == null) return;

        var loc = LocalizationManager.Instance;
        RefreshOptionLabels();

        string current = loc != null
            ? loc.CurrentLanguage
            : PlayerPrefs.GetString("SelectedLanguage", LocalizationManager.LANG_EN);

        int index = 0; // en
        if (string.Equals(current, LocalizationManager.LANG_AR, System.StringComparison.OrdinalIgnoreCase))
            index = 1;
        else if (string.Equals(current, LocalizationManager.LANG_FR, System.StringComparison.OrdinalIgnoreCase))
            index = 2;
        else if (string.Equals(current, LocalizationManager.LANG_DARIJA, System.StringComparison.OrdinalIgnoreCase))
            index = 3;

        _dropdown.SetValueWithoutNotify(index);
        _dropdown.RefreshShownValue();
        LocalizationManager.ApplyText(_dropdown.captionText, _rawOptionLabels[index], preserveAlignment: true);

        _dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        _dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    private void RefreshOptionLabels()
    {
        if (_dropdown == null) return;

        if (_dropdown.options == null)
            _dropdown.options = new List<TMP_Dropdown.OptionData>();

        while (_dropdown.options.Count < 4)
            _dropdown.options.Add(new TMP_Dropdown.OptionData());
        while (_dropdown.options.Count > 4)
            _dropdown.options.RemoveAt(_dropdown.options.Count - 1);

        for (int i = 0; i < 4; i++)
        {
            string raw = LocalizationManager.Resolve(OptionKeys[i], DefaultLabels[i]);
            _rawOptionLabels[i] = raw;
            _dropdown.options[i].text = LocalizationManager.ContainsArabic(raw) ? LocalizationManager.FixRTL(raw) : raw;
        }
    }

    private void StyleOpenDropdownItems(Transform dropdownList)
    {
        var toggles = dropdownList.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            // Template item index 0..3 matches option order (Item 0: English, etc.)
            int optionIdx = i;
            if (optionIdx >= 0 && optionIdx < _rawOptionLabels.Length)
            {
                Transform labelT = toggles[i].transform.Find("Item Label");
                TMP_Text labelTmp = labelT != null ? labelT.GetComponent<TMP_Text>() : toggles[i].GetComponentInChildren<TMP_Text>(true);
                if (labelTmp != null)
                {
                    LocalizationManager.ApplyText(labelTmp, _rawOptionLabels[optionIdx], preserveAlignment: true);
                }
            }
        }
    }

    private void OnDropdownValueChanged(int index)
    {
        string langCode = LocalizationManager.LANG_EN;
        switch (index)
        {
            case 0:
                langCode = LocalizationManager.LANG_EN;
                break;
            case 1:
                langCode = LocalizationManager.LANG_AR;
                break;
            case 2:
                langCode = LocalizationManager.LANG_FR;
                break;
            case 3:
                langCode = LocalizationManager.LANG_DARIJA;
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

        RefreshOptionLabels();
        if (_dropdown != null)
        {
            _dropdown.SetValueWithoutNotify(index);
            _dropdown.RefreshShownValue();
            LocalizationManager.ApplyText(_dropdown.captionText, _rawOptionLabels[index], preserveAlignment: true);
        }
    }
}
