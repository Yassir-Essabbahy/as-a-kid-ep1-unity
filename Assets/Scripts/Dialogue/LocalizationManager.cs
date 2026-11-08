using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RTLTMPro;

public class LocalizationManager : MonoBehaviour
{
    private static LocalizationManager _instance;
    public static LocalizationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<LocalizationManager>();
                if (_instance == null)
                {
                    var go = new GameObject("LocalizationManager_Auto");
                    _instance = go.AddComponent<LocalizationManager>();
                }
            }

            if (_instance != null)
            {
                if (string.IsNullOrEmpty(_instance.currentLanguage))
                {
                    string saved = PlayerPrefs.GetString(PREF_KEY_LANGUAGE, "");
                    _instance.currentLanguage = !string.IsNullOrEmpty(saved) ? saved : (string.IsNullOrEmpty(_instance.defaultLanguage) ? "en" : _instance.defaultLanguage);
                }
                if (_instance.table == null || _instance.table.Count == 0)
                    _instance.Load();
            }

            return _instance;
        }
        private set => _instance = value;
    }

    private const string PREF_KEY_LANGUAGE = "SelectedLanguage";

    [Tooltip("File name inside Resources/Localization, without extension")]
    [SerializeField] private string csvFileName = "dialogue";

    [Tooltip("Language code used if none is set yet, e.g. en / fr / ar")]
    [SerializeField] private string defaultLanguage = "en";

    private Dictionary<string, Dictionary<string, string>> table = new();
    private string currentLanguage;

    public string CurrentLanguage
    {
        get
        {
            if (string.IsNullOrEmpty(currentLanguage))
            {
                currentLanguage = PlayerPrefs.GetString(PREF_KEY_LANGUAGE, defaultLanguage);
            }
            return currentLanguage;
        }
        set
        {
            if (currentLanguage != value)
            {
                currentLanguage = value;
                PlayerPrefs.SetString(PREF_KEY_LANGUAGE, currentLanguage);
                PlayerPrefs.Save();
                OnLanguageChanged?.Invoke(currentLanguage);
            }
        }
    }

    public event System.Action<string> OnLanguageChanged;

    public void SetLanguage(string lang)
    {
        CurrentLanguage = lang;
    }

    public void SetLanguageByIndex(int index)
    {
        switch (index)
        {
            case 0: CurrentLanguage = "en"; break;
            case 1: CurrentLanguage = "ar"; break;
            case 2: CurrentLanguage = "fr"; break;
            default: CurrentLanguage = "en"; break;
        }
    }

    public int GetCurrentLanguageIndex()
    {
        if (string.Equals(CurrentLanguage, "ar", System.StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(CurrentLanguage, "fr", System.StringComparison.OrdinalIgnoreCase)) return 2;
        return 0; // en
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentLanguage = PlayerPrefs.GetString(PREF_KEY_LANGUAGE, defaultLanguage);
        Load();
    }

    private void Load()
    {
        TextAsset csv = Resources.Load<TextAsset>($"Localization/{csvFileName}");
        if (csv == null)
        {
            Debug.LogError($"[Localization] Could not find Resources/Localization/{csvFileName}.csv");
            return;
        }

        using StringReader reader = new StringReader(csv.text);
        string headerLine = reader.ReadLine();
        if (headerLine == null) return;

        string[] headers = ParseLine(headerLine);
        // headers[0] = "key", headers[1..] = language codes

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] fields = ParseLine(line);
            if (fields.Length == 0) continue;

            string key = fields[0].Trim();
            var langMap = new Dictionary<string, string>();

            for (int i = 1; i < headers.Length && i < fields.Length; i++)
            {
                string lang = headers[i].Trim();
                langMap[lang] = fields[i].Trim();
            }

            table[key] = langMap;
        }

        Debug.Log($"[Localization] Loaded {table.Count} keys.");
    }

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";

        if (string.IsNullOrEmpty(currentLanguage))
            currentLanguage = PlayerPrefs.GetString(PREF_KEY_LANGUAGE, string.IsNullOrEmpty(defaultLanguage) ? "en" : defaultLanguage);

        if (table == null || table.Count == 0)
            Load();

        if (!table.TryGetValue(key, out var langMap))
        {
            Debug.LogWarning($"[Localization] Missing key: '{key}'");
            return $"[{key}]";
        }

        if (langMap != null && langMap.TryGetValue(currentLanguage, out var text))
            return text;

        Debug.LogWarning($"[Localization] Key '{key}' missing language '{currentLanguage}'");
        return $"[{key}:{currentLanguage}]";
    }

    // ============================================================
    // ARABIC RTL & SHAPING HELPER
    // ============================================================
    private static FastStringBuilder _rtlBuffer = new FastStringBuilder(2048);

    public static string FixRTL(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (!ContainsArabic(text)) return text;

        lock (_rtlBuffer)
        {
            _rtlBuffer.Clear();
            RTLSupport.FixRTL(text, _rtlBuffer, farsi: false, fixTextTags: true, preserveNumbers: true);
            _rtlBuffer.Reverse();
            return _rtlBuffer.ToString();
        }
    }

    public static bool ContainsArabic(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= 0x0600 && c <= 0x06FF) return true;
            if (c >= 0x0750 && c <= 0x077F) return true;
            if (c >= 0xFB50 && c <= 0xFDFF) return true;
            if (c >= 0xFE70 && c <= 0xFEFF) return true;
        }
        return false;
    }

    // Minimal CSV parser that handles quoted fields containing commas
    private string[] ParseLine(string line)
    {
        var fields = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            string field;
            if (line[i] == '"')
            {
                i++;
                int start = i;
                var sb = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                        }
                        else
                        {
                            i++;
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                field = sb.ToString();
                if (i < line.Length && line[i] == ',') i++;
            }
            else
            {
                int start = i;
                while (i < line.Length && line[i] != ',') i++;
                field = line.Substring(start, i - start);
                if (i < line.Length && line[i] == ',') i++;
            }
            fields.Add(field);
        }
        return fields.ToArray();
    }
}
