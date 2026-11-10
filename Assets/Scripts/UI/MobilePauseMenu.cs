using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ModularFirstPersonController;

namespace Story
{
    /// <summary>
    /// Touch-enabled, back-button-compatible Pause Menu for mobile and PC.
    /// Matched 1:1 to the "As A Kid" Main Menu theme:
    /// - Bold red "As A Kid" title on the left side
    /// - Clean left-aligned menu items (Resume, Restart, Main Menu) with FriendlySans font
    /// - Signature white cube selection indicator (◼) on active/tapped item
    /// - Atmospheric dark cinematic overlay dimming the 3D scene
    /// - Ergonomic >=48dp touch targets and safe area margin protection
    /// </summary>
    [DisallowMultipleComponent]
    public class MobilePauseMenu : MonoBehaviour
    {
        public static MobilePauseMenu Instance { get; private set; }

        [Header("State")]
        public bool isPaused = false;
        public bool canPause = true;

        [Header("UI References")]
        public GameObject pauseButtonObj;
        public GameObject pausePanelObj;
        public Button resumeButton;
        public Button restartButton;
        public Button mainMenuButton;

        private float _previousTimeScale = 1f;
        private List<GameObject> _indicators = new List<GameObject>();
        private TextMeshProUGUI _titleTmp;
        private TextMeshProUGUI _subtitleTmp;
        private readonly List<(TextMeshProUGUI tmp, string locKey, string fallback, RectTransform cubeRt)> _localizedButtons = new List<(TextMeshProUGUI, string, string, RectTransform)>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureUIHierarchy();
            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
                LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
            }
        }

        private void Start()
        {
            if (pausePanelObj != null)
            {
                pausePanelObj.SetActive(false);
            }
            RefreshLocalizedLabels();
            UpdatePauseButtonVisibility();
        }

        private void Update()
        {
            // Android Back Button and PC Escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (isPaused)
                {
                    ResumeGame();
                }
                else if (canPause)
                {
                    PauseGame();
                }
            }
        }

        private void OnDestroy()
        {
            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
            if (Instance == this) Instance = null;
            if (isPaused)
            {
                Time.timeScale = 1f;
            }
        }

        private void HandleLanguageChanged(string _)
        {
            RefreshLocalizedLabels();
        }

        public void RefreshLocalizedLabels()
        {
            if (_titleTmp != null)
            {
                LocalizationManager.ApplyText(_titleTmp, LocalizationManager.Resolve("ui_game_title", "As A Kid"), preserveAlignment: true);
            }
            if (_subtitleTmp != null)
            {
                string sub = LocalizationManager.Resolve("ui_pause_title", "PAUSED");
                LocalizationManager.ApplyText(_subtitleTmp, sub, preserveAlignment: true);
                _subtitleTmp.characterSpacing = LocalizationManager.ContainsArabic(sub) ? 0f : 8f;
            }
            foreach (var item in _localizedButtons)
            {
                if (item.tmp != null)
                {
                    LocalizationManager.ApplyText(item.tmp, LocalizationManager.Resolve(item.locKey, item.fallback), preserveAlignment: true);
                    item.tmp.ForceMeshUpdate();
                    if (item.cubeRt != null)
                    {
                        float textWidth = item.tmp.preferredWidth > 10f ? item.tmp.preferredWidth : 220f;
                        item.cubeRt.anchoredPosition = new Vector2(textWidth + 20f, -4f);
                    }
                }
            }
        }

        public void TogglePause()
        {
            if (isPaused) ResumeGame();
            else if (canPause) PauseGame();
        }

        public void PauseGame()
        {
            if (isPaused) return;

            isPaused = true;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            RefreshLocalizedLabels();

            if (pausePanelObj != null)
            {
                pausePanelObj.SetActive(true);
            }

            if (pauseButtonObj != null)
            {
                pauseButtonObj.SetActive(false);
            }

            // Set first item (Resume) active indicator by default, matching Main Menu
            HighlightIndicator(0);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var touchControls = FindAnyObjectByType<MobileTouchControls>();
            if (touchControls != null)
            {
                touchControls.ResetAllTouchPointers();
            }

            var fpc = FirstPersonController.ActiveInstance ?? FindAnyObjectByType<FirstPersonController>();
            if (fpc != null)
            {
                fpc.playerCanMove = false;
                fpc.cameraCanMove = false;
            }
        }

        public void ResumeGame()
        {
            if (!isPaused) return;

            isPaused = false;
            Time.timeScale = _previousTimeScale > 0.01f ? _previousTimeScale : 1f;

            if (pausePanelObj != null)
            {
                pausePanelObj.SetActive(false);
            }

            UpdatePauseButtonVisibility();

            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            var touchControls = FindAnyObjectByType<MobileTouchControls>();
            if (touchControls != null)
            {
                touchControls.ResetAllTouchPointers();
            }

            var fpc = FirstPersonController.ActiveInstance ?? FindAnyObjectByType<FirstPersonController>();
            if (fpc != null)
            {
                fpc.playerCanMove = true;
                fpc.cameraCanMove = true;
            }
        }

        public void RestartCurrentScene()
        {
            Time.timeScale = 1f;
            isPaused = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            isPaused = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu");
        }

        public void UpdatePauseButtonVisibility()
        {
            if (pauseButtonObj != null)
            {
                bool shouldShow = (Application.isMobilePlatform || MobileUIAdapter.ForceMobileInEditor) && !isPaused;
                pauseButtonObj.SetActive(shouldShow);
            }
        }

        public void HighlightIndicator(int index)
        {
            for (int i = 0; i < _indicators.Count; i++)
            {
                if (_indicators[i] != null)
                {
                    _indicators[i].SetActive(i == index);
                }
            }
        }

        public void EnsureUIHierarchy()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var font = Resources.Load<TMP_FontAsset>("Fonts/FriendlySans-Regular SDF");
            Sprite buttonSprite = null;
#if UNITY_EDITOR
            buttonSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
#endif

            // 1. Mobile Pause Button (Top-Right during gameplay)
            if (pauseButtonObj == null)
            {
                var existingBtn = transform.Find("MobilePauseButton");
                if (existingBtn != null)
                {
                    pauseButtonObj = existingBtn.gameObject;
                }
                else
                {
                    pauseButtonObj = new GameObject("MobilePauseButton", typeof(RectTransform));
                    pauseButtonObj.transform.SetParent(transform, false);

                    var rt = pauseButtonObj.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-36f, -36f);
                    rt.sizeDelta = new Vector2(56f, 56f); // 56dp touch target

                    var img = pauseButtonObj.AddComponent<Image>();
                    img.sprite = buttonSprite;
                    img.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                    img.color = new Color(0.10f, 0.10f, 0.12f, 0.72f);
                    img.raycastTarget = true;

                    var btn = pauseButtonObj.AddComponent<Button>();
                    btn.targetGraphic = img;
                    var colors = btn.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                    colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                    btn.colors = colors;
                    btn.onClick.AddListener(TogglePause);

                    // Crisp Vector Pause Icon (two vertical bars, independent of font glyphs)
                    var bar1 = new GameObject("Bar1", typeof(RectTransform), typeof(Image));
                    bar1.transform.SetParent(pauseButtonObj.transform, false);
                    var b1Rt = bar1.GetComponent<RectTransform>();
                    b1Rt.anchorMin = new Vector2(0.5f, 0.5f);
                    b1Rt.anchorMax = new Vector2(0.5f, 0.5f);
                    b1Rt.pivot = new Vector2(0.5f, 0.5f);
                    b1Rt.anchoredPosition = new Vector2(-5f, 0f);
                    b1Rt.sizeDelta = new Vector2(5f, 22f);
                    var b1Img = bar1.GetComponent<Image>();
                    b1Img.color = Color.white;
                    b1Img.raycastTarget = false;

                    var bar2 = new GameObject("Bar2", typeof(RectTransform), typeof(Image));
                    bar2.transform.SetParent(pauseButtonObj.transform, false);
                    var b2Rt = bar2.GetComponent<RectTransform>();
                    b2Rt.anchorMin = new Vector2(0.5f, 0.5f);
                    b2Rt.anchorMax = new Vector2(0.5f, 0.5f);
                    b2Rt.pivot = new Vector2(0.5f, 0.5f);
                    b2Rt.anchoredPosition = new Vector2(5f, 0f);
                    b2Rt.sizeDelta = new Vector2(5f, 22f);
                    var b2Img = bar2.GetComponent<Image>();
                    b2Img.color = Color.white;
                    b2Img.raycastTarget = false;
                }
            }

            // 2. Pause Menu Modal Overlay (As A Kid Main Menu Theme)
            var oldPanel = transform.Find("PausePanel");
            if (oldPanel != null)
            {
                DestroyImmediate(oldPanel.gameObject);
                pausePanelObj = null;
            }

            _indicators.Clear();
            _localizedButtons.Clear();

            pausePanelObj = new GameObject("PausePanel", typeof(RectTransform));
            pausePanelObj.transform.SetParent(transform, false);

            var panelRt = pausePanelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            // Atmospheric dark overlay (dimming paused 3D scene)
            var bgImg = pausePanelObj.AddComponent<Image>();
            bgImg.color = new Color(0.01f, 0.01f, 0.02f, 0.82f);
            bgImg.raycastTarget = true;

            // Safe Area Wrapper
            var contentRoot = new GameObject("ContentRoot", typeof(RectTransform));
            contentRoot.transform.SetParent(pausePanelObj.transform, false);
            var contentRt = contentRoot.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            contentRoot.AddComponent<SafeAreaHandler>();

            // Left-aligned Main Content Container (matches MainMenu.unity)
            var menuContainer = new GameObject("MainMenu_Style_Container", typeof(RectTransform));
            menuContainer.transform.SetParent(contentRoot.transform, false);
            var menuRt = menuContainer.GetComponent<RectTransform>();
            menuRt.anchorMin = Vector2.zero;
            menuRt.anchorMax = Vector2.one;
            menuRt.offsetMin = Vector2.zero;
            menuRt.offsetMax = Vector2.zero;

            // Title: "As A Kid" (Bold Red, size 105, left-aligned)
            var titleObj = new GameObject("TitleText", typeof(RectTransform));
            titleObj.transform.SetParent(menuRt, false);
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(133f, 210f); // Exact MainMenu X & Y alignment
            titleRt.sizeDelta = new Vector2(650f, 150f);

            _titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) _titleTmp.font = font;
            _titleTmp.fontSize = 105f;
            _titleTmp.fontStyle = FontStyles.Bold;
            _titleTmp.color = new Color(1.0f, 0.0f, 0.0f, 1.0f); // Pure Red (#FF0000)
            _titleTmp.alignment = TextAlignmentOptions.Left;
            _titleTmp.raycastTarget = false;
            LocalizationManager.ApplyText(_titleTmp, LocalizationManager.Resolve("ui_game_title", "As A Kid"), preserveAlignment: true);

            // Subtitle: "PAUSED"
            var subTitleObj = new GameObject("SubtitleText", typeof(RectTransform));
            subTitleObj.transform.SetParent(menuRt, false);
            var subTitleRt = subTitleObj.GetComponent<RectTransform>();
            subTitleRt.anchorMin = new Vector2(0f, 0.5f);
            subTitleRt.anchorMax = new Vector2(0f, 0.5f);
            subTitleRt.pivot = new Vector2(0f, 0.5f);
            subTitleRt.anchoredPosition = new Vector2(135f, 120f);
            subTitleRt.sizeDelta = new Vector2(450f, 50f);

            _subtitleTmp = subTitleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) _subtitleTmp.font = font;
            _subtitleTmp.fontSize = 36f;
            _subtitleTmp.fontStyle = FontStyles.Bold;
            _subtitleTmp.color = new Color(1.0f, 0.25f, 0.25f, 0.9f); // Subtle horror red accent
            _subtitleTmp.alignment = TextAlignmentOptions.Left;
            string pauseSub = LocalizationManager.Resolve("ui_pause_title", "PAUSED");
            _subtitleTmp.characterSpacing = LocalizationManager.ContainsArabic(pauseSub) ? 0f : 8f;
            _subtitleTmp.raycastTarget = false;
            LocalizationManager.ApplyText(_subtitleTmp, pauseSub, preserveAlignment: true);

            // Buttons Vertical Pack (matches MainMenu pos.x = 133, pivot=(0, 0.5), anchor=(0, 0.5))
            var buttonsPack = new GameObject("Buttons_Pack", typeof(RectTransform));
            buttonsPack.transform.SetParent(menuRt, false);
            var packRt = buttonsPack.GetComponent<RectTransform>();
            packRt.anchorMin = new Vector2(0f, 0.5f);
            packRt.anchorMax = new Vector2(0f, 0.5f);
            packRt.pivot = new Vector2(0f, 0.5f);
            packRt.anchoredPosition = new Vector2(133f, 0f);
            packRt.sizeDelta = new Vector2(450f, 400f);

            // 1. Resume Button (pos.y = 15f)
            resumeButton = CreateThemedMenuButton(buttonsPack.transform, "ResumeButton", "ui_pause_resume", "Resume", 15f, 0, font);
            resumeButton.onClick.AddListener(ResumeGame);

            // 2. Restart Button (pos.y = -100f)
            restartButton = CreateThemedMenuButton(buttonsPack.transform, "RestartButton", "ui_pause_restart", "Restart", -100f, 1, font);
            restartButton.onClick.AddListener(RestartCurrentScene);

            // 3. Main Menu Button (pos.y = -215f)
            mainMenuButton = CreateThemedMenuButton(buttonsPack.transform, "MainMenuButton", "ui_pause_main_menu", "Main Menu", -215f, 2, font);
            mainMenuButton.onClick.AddListener(LoadMainMenu);

            // Initially highlight Resume
            HighlightIndicator(0);
        }

        private Button CreateThemedMenuButton(Transform parent, string name, string locKey, string fallbackLabel, float yPos, int index, TMP_FontAsset font)
        {
            var btnObj = new GameObject(name, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);

            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(420f, 75f); // Generous touch target (75dp)

            // Transparent image background for touch detection (identical to MainMenu buttons)
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.001f); // Transparent raycast catch
            img.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.85f, 0.85f, 1f);
            colors.pressedColor = new Color(1f, 0.4f, 0.4f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;

            // Text Label
            var textObj = new GameObject("Text (TMP)", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            string resolvedLabel = LocalizationManager.Resolve(locKey, fallbackLabel);
            tmp.fontSize = 65.7f; // Exact MainMenu font size
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 30f;
            tmp.fontSizeMax = 65.7f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            LocalizationManager.ApplyText(tmp, resolvedLabel, preserveAlignment: true);

            // Force mesh generation to calculate preferredWidth accurately
            tmp.ForceMeshUpdate();
            float textWidth = tmp.preferredWidth > 10f ? tmp.preferredWidth : (fallbackLabel.Length * 36f);

            // Signature Selection Cube Image (◼)
            var cubeObj = new GameObject("CubeImage", typeof(RectTransform));
            cubeObj.transform.SetParent(btnObj.transform, false);
            var cubeRt = cubeObj.GetComponent<RectTransform>();
            cubeRt.anchorMin = new Vector2(0f, 0.5f);
            cubeRt.anchorMax = new Vector2(0f, 0.5f);
            cubeRt.pivot = new Vector2(0f, 0.5f);
            cubeRt.anchoredPosition = new Vector2(textWidth + 20f, -4f);
            cubeRt.sizeDelta = new Vector2(36.13f, 33.63f); // Exact MainMenu CubeImage dimensions

            var cubeImg = cubeObj.AddComponent<Image>();
            cubeImg.color = Color.white;
            cubeImg.raycastTarget = false;
            cubeObj.SetActive(index == 0);

            _indicators.Add(cubeObj);
            _localizedButtons.Add((tmp, locKey, fallbackLabel, cubeRt));

            // Wire selection trigger to illuminate indicator on touch / hover / select
            var trigger = btnObj.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => HighlightIndicator(index));
            trigger.triggers.Add(enterEntry);

            var selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selectEntry.callback.AddListener((_) => HighlightIndicator(index));
            trigger.triggers.Add(selectEntry);

            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener((_) => HighlightIndicator(index));
            trigger.triggers.Add(downEntry);

            return btn;
        }
    }
}
