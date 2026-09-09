using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
                    _instance.currentLanguage = string.IsNullOrEmpty(_instance.defaultLanguage) ? "en" : _instance.defaultLanguage;
                if (_instance.table == null || _instance.table.Count == 0)
                    _instance.Load();
            }

            return _instance;
        }
        private set => _instance = value;
    }

    [Tooltip("File name inside Resources/Localization, without extension")]
    [SerializeField] private string csvFileName = "dialogue";

    [Tooltip("Language code used if none is set yet, e.g. en / fr / ar")]
    [SerializeField] private string defaultLanguage = "en";

    private Dictionary<string, Dictionary<string, string>> table = new();
    private string currentLanguage;

    public string CurrentLanguage
    {
        get => currentLanguage;
        set => currentLanguage = value;
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

        currentLanguage = defaultLanguage;
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
            currentLanguage = string.IsNullOrEmpty(defaultLanguage) ? "en" : defaultLanguage;

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