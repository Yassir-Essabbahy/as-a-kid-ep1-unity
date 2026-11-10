using System;
using System.Collections.Generic;
using System.IO;
using RTLTMPro;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LocalizationManager : MonoBehaviour
{
    public const string LANG_EN = "en";
    public const string LANG_AR = "ar";
    public const string LANG_FR = "fr";
    public const string LANG_DARIJA = "darija";

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
                    _instance.currentLanguage = !string.IsNullOrEmpty(saved)
                        ? saved
                        : (string.IsNullOrEmpty(_instance.defaultLanguage) ? LANG_EN : _instance.defaultLanguage);
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

    [Tooltip("Language code used if none is set yet, e.g. en / fr / ar / darija")]
    [SerializeField] private string defaultLanguage = LANG_EN;

    private Dictionary<string, Dictionary<string, string>> table = new();
    private string currentLanguage;
    private static bool _fallbackFontEnsured = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapOnLaunch()
    {
        var inst = Instance;
        if (inst != null)
        {
            inst.LocalizeStaticSceneUI();
        }
    }

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
                LocalizeStaticSceneUI();
            }
        }
    }

    public bool IsCurrentLanguageRTL => IsRightToLeftLanguage(CurrentLanguage);

    public static bool IsRightToLeftLanguage(string lang)
    {
        return string.Equals(lang, LANG_AR, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(lang, LANG_DARIJA, StringComparison.OrdinalIgnoreCase);
    }

    public event Action<string> OnLanguageChanged;

    public void SetLanguage(string lang)
    {
        CurrentLanguage = lang;
    }

    public void SetLanguageByIndex(int index)
    {
        switch (index)
        {
            case 0: CurrentLanguage = LANG_EN; break;
            case 1: CurrentLanguage = LANG_AR; break;
            case 2: CurrentLanguage = LANG_FR; break;
            case 3: CurrentLanguage = LANG_DARIJA; break;
            default: CurrentLanguage = LANG_EN; break;
        }
    }

    public int GetCurrentLanguageIndex()
    {
        if (string.Equals(CurrentLanguage, LANG_AR, StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(CurrentLanguage, LANG_FR, StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(CurrentLanguage, LANG_DARIJA, StringComparison.OrdinalIgnoreCase)) return 3;
        return 0; // en
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        currentLanguage = PlayerPrefs.GetString(PREF_KEY_LANGUAGE, defaultLanguage);
        EnsureArabicFallbackFont();
        Load();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LocalizeStaticSceneUI();
    }

    public static void EnsureArabicFallbackFont()
    {
        if (_fallbackFontEnsured) return;

        TMP_FontAsset arabicFont = Resources.Load<TMP_FontAsset>("Fonts/Arabic4");
        if (arabicFont == null) arabicFont = Resources.Load<TMP_FontAsset>("Fonts/Arabic 3");
        if (arabicFont == null) arabicFont = Resources.Load<TMP_FontAsset>("Fonts/Arabic2");
        if (arabicFont == null) return;

        var globalFallbacks = TMP_Settings.fallbackFontAssets;
        if (globalFallbacks != null && !globalFallbacks.Contains(arabicFont))
        {
            globalFallbacks.Add(arabicFont);
        }

        TMP_FontAsset friendlySans = Resources.Load<TMP_FontAsset>("Fonts/FriendlySans-Regular SDF");
        if (friendlySans != null)
        {
            if (friendlySans.fallbackFontAssetTable == null)
                friendlySans.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!friendlySans.fallbackFontAssetTable.Contains(arabicFont))
                friendlySans.fallbackFontAssetTable.Add(arabicFont);
        }

        _fallbackFontEnsured = true;
    }

    public void Reload()
    {
        table.Clear();
        Load();
        OnLanguageChanged?.Invoke(CurrentLanguage);
        LocalizeStaticSceneUI();
    }

    private void Load()
    {
        EnsureArabicFallbackFont();

        TextAsset csv = Resources.Load<TextAsset>($"Localization/{csvFileName}");
        if (csv == null)
        {
            Debug.LogError($"[Localization] Could not find Resources/Localization/{csvFileName}.csv");
            return;
        }

        table.Clear();

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
                langMap[lang] = fields[i].Trim().Replace("\\n", "\n");
            }

            table[key] = langMap;
        }

        Debug.Log($"[Localization] Loaded {table.Count} keys.");
    }

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";

        if (string.IsNullOrEmpty(currentLanguage))
            currentLanguage = PlayerPrefs.GetString(PREF_KEY_LANGUAGE, string.IsNullOrEmpty(defaultLanguage) ? LANG_EN : defaultLanguage);

        if (table == null || table.Count == 0)
            Load();

        if (!table.TryGetValue(key, out var langMap))
        {
            Debug.LogWarning($"[Localization] Missing key: '{key}'");
            return $"[{key}]";
        }

        if (langMap != null && langMap.TryGetValue(currentLanguage, out var text) && !string.IsNullOrEmpty(text))
            return text;

        if (string.Equals(currentLanguage, LANG_DARIJA, StringComparison.OrdinalIgnoreCase) &&
            langMap != null && langMap.TryGetValue(LANG_AR, out var arFallback) && !string.IsNullOrEmpty(arFallback))
        {
            return arFallback;
        }

        Debug.LogWarning($"[Localization] Key '{key}' missing language '{currentLanguage}'");
        return $"[{key}:{currentLanguage}]";
    }

    public static string Resolve(string key, string fallback = null)
    {
        string text = null;
        if (!string.IsNullOrEmpty(key) && Instance != null)
        {
            string loc = Instance.Get(key);
            if (!string.IsNullOrEmpty(loc) && !(loc.StartsWith("[") && loc.EndsWith("]")))
            {
                text = loc;
            }
        }

        if (string.IsNullOrEmpty(text))
        {
            text = fallback ?? key ?? "";
        }

        if (!string.IsNullOrEmpty(text))
        {
            string teddyName = MetroStorySequenceController.TeddyName;
            if (!string.IsNullOrEmpty(teddyName))
            {
                text = text.Replace("{TEDDY_NAME}", teddyName).Replace("[TEDDY NAME]", teddyName);
            }
            text = text.Replace("{CHILD_NAME}", MetroStorySequenceController.ChildName);
        }

        return text;
    }

    public static string Format(string key, string fallback, params object[] args)
    {
        string template = Resolve(key, fallback);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    public static void ApplyText(TMP_Text target, string rawText, bool preserveAlignment = true)
    {
        if (target == null) return;
        EnsureArabicFallbackFont();
        target.text = DialogueTypewriterUtility.PrepareDisplayText(target, rawText ?? "", preserveAlignment);
    }

    public void LocalizeStaticSceneUI()
    {
        EnsureArabicFallbackFont();

        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid()) return;

        // 1. MainMenu Canvas
        if (active.name == "MainMenu")
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform t = canvas.transform;
                SetChildText(t, "MainMenu/TitleText", "ui_game_title");
                SetChildText(t, "MainMenu/Buttons_Pack/PlayButton/Text (TMP)", "ui_menu_episodes");
                SetChildText(t, "MainMenu/Buttons_Pack/OptionsButton/Text (TMP)", "ui_menu_options");
                SetChildText(t, "MainMenu/Buttons_Pack/CreditsButton/Text (TMP)", "ui_menu_credits");
                SetChildText(t, "MainMenu/Buttons_Pack/ExitButton/Text (TMP)", "ui_menu_exit");

                SetChildText(t, "EpisodesMenu/TitleText", "ui_game_title");
                SetChildText(t, "EpisodesMenu/TitleText/SubTitle", "ui_episode_1_subtitle");
                SetChildText(t, "EpisodesMenu/PlayButton/Text (TMP)", "ui_menu_play");
                SetChildText(t, "EpisodesMenu/ExitButton/Text (TMP)", "ui_menu_back");

                SetChildText(t, "OptionsMenu/TitleText", "ui_game_title");
                SetChildText(t, "OptionsMenu/ExitButton/Text (TMP)", "ui_menu_back");
                SetChildText(t, "OptionsMenu/Settings/FPSCounter_Toggle/Label", "ui_settings_fps_counter");
                SetChildText(t, "OptionsMenu/Settings/LockFPS_Toggle/Label", "ui_settings_lock_fps");

                SetChildText(t, "CreditsMenu/TitleText", "ui_game_title");
                SetChildText(t, "CreditsMenu/Scroll View/Viewport/Content/CreditsText", "ui_credits_made_with_love");
                SetChildText(t, "CreditsMenu/ExitButton/Text (TMP)", "ui_menu_back");
            }
        }

        // 2. Dialogue Continue buttons across scenes (TalkPanel & DialogueBox)
        foreach (string path in new[]
        {
            "Canvas/TalkPanel/BottomBand/ContinueButton/Text",
            "Canvas/TalkPanel/BottomBand/ContinueButton/Label",
            "DialogueCanvas/TalkPanel/BottomBand/ContinueButton/Label",
            "DialogueCanvas/TalkPanel/BottomBand/ContinueButton/Text",
            "Canvas/DialogueBox/BottomBand/ContinueButton/Text",
            "Canvas/DialogueBox/BottomBand/ContinueButton/Label"
        })
        {
            SetObjectPathText(path, "ui_continue");
        }

        // 3. FuseBox close button & DoorSign in Story
        SetObjectPathText("Canvas/MobileFuseBoxUI/CloseButton/Label", "ui_close");
        SetObjectPathText("Floor3/Door_Behind/DoorSign", "door_sign_staff_only");

        // 4. End Panels (if any)
        SetObjectPathText("Canvas/EndOfEpisodePanel/TitleText", "ui_end_of_episode");
        SetObjectPathText("Canvas/EndOfEpisodePanel/SubtitleText", "ui_end_episode_subtitle");
        SetObjectPathText("Canvas/EndOfEpisodePanel/RestartButton/BtnText", "ui_restart");
        SetObjectPathText("Canvas/EndPrototypePanel/TitleText", "ui_end_prototype_title");
        SetObjectPathText("Canvas/EndPrototypePanel/SubtitleText", "ui_end_prototype_subtitle");
        SetObjectPathText("Canvas/EndPrototypePanel/RestartButton/BtnText", "ui_restart_sequence");
    }

    private static void SetChildText(Transform root, string relativePath, string localizationKey)
    {
        if (root == null) return;
        Transform target = root.Find(relativePath);
        if (target == null) return;
        TMP_Text tmp = target.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            ApplyText(tmp, Resolve(localizationKey), preserveAlignment: true);
        }
    }

    private static void SetObjectPathText(string hierarchyPath, string localizationKey)
    {
        int slashIdx = hierarchyPath.IndexOf('/');
        if (slashIdx <= 0)
        {
            GameObject go = GameObject.Find(hierarchyPath);
            if (go != null)
            {
                var tmp = go.GetComponent<TMP_Text>();
                if (tmp != null) ApplyText(tmp, Resolve(localizationKey), preserveAlignment: true);
            }
            return;
        }

        string rootName = hierarchyPath.Substring(0, slashIdx);
        string subPath = hierarchyPath.Substring(slashIdx + 1);
        GameObject rootGo = GameObject.Find(rootName);
        if (rootGo == null)
        {
            // Also search inactive scene roots if needed
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid())
            {
                foreach (var root in active.GetRootGameObjects())
                {
                    if (root.name == rootName)
                    {
                        rootGo = root;
                        break;
                    }
                }
            }
        }

        if (rootGo != null)
        {
            SetChildText(rootGo.transform, subPath, localizationKey);
        }
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
        if (line.EndsWith(","))
        {
            fields.Add("");
        }
        return fields.ToArray();
    }
}
