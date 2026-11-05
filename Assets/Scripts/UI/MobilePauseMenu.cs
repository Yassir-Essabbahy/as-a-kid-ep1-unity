using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ModularFirstPersonController;

namespace Story
{
    /// <summary>
    /// Touch-enabled and back-button-compatible Pause Menu for mobile and PC.
    /// Responds to on-screen pause button, Escape key, and Android hardware Back button.
    /// Uses project reference styling: 1920x1080 scaler, FriendlySans font, 48+ dp touch targets.
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
        }

        private void Start()
        {
            if (pausePanelObj != null)
            {
                pausePanelObj.SetActive(false);
            }
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
            if (Instance == this) Instance = null;
            if (isPaused)
            {
                Time.timeScale = 1f;
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

            if (pausePanelObj != null)
            {
                pausePanelObj.SetActive(true);
            }

            if (pauseButtonObj != null)
            {
                pauseButtonObj.SetActive(false);
            }

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

        public void EnsureUIHierarchy()
        {
            // If pause panel is not already created, build standard pause UI
            if (pausePanelObj != null && pauseButtonObj != null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var font = Resources.Load<TMP_FontAsset>("Fonts/FriendlySans-Regular SDF");
            var buttonSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Pause Button (Top-Right)
            if (pauseButtonObj == null)
            {
                var existingBtn = transform.Find("MobilePauseButton");
                if (existingBtn != null)
                {
                    pauseButtonObj = existingBtn.gameObject;
                }
                else
                {
                    pauseButtonObj = new GameObject("MobilePauseButton");
                    pauseButtonObj.transform.SetParent(transform, false);

                    var rt = pauseButtonObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-28f, -28f);
                    rt.sizeDelta = new Vector2(56f, 56f); // 56dp minimum touch target

                    var img = pauseButtonObj.AddComponent<Image>();
                    img.sprite = buttonSprite;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);
                    img.raycastTarget = true;

                    var btn = pauseButtonObj.AddComponent<Button>();
                    btn.targetGraphic = img;
                    var colors = btn.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                    colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                    btn.colors = colors;
                    btn.onClick.AddListener(TogglePause);

                    // Pause Icon Text "❚❚"
                    var iconObj = new GameObject("Icon", typeof(RectTransform));
                    iconObj.transform.SetParent(pauseButtonObj.transform, false);
                    var iconRt = iconObj.GetComponent<RectTransform>();
                    iconRt.anchorMin = Vector2.zero;
                    iconRt.anchorMax = Vector2.one;
                    iconRt.offsetMin = Vector2.zero;
                    iconRt.offsetMax = Vector2.zero;

                    var iconTmp = iconObj.AddComponent<TextMeshProUGUI>();
                    if (font != null) iconTmp.font = font;
                    iconTmp.text = "❚❚";
                    iconTmp.fontSize = 20;
                    iconTmp.alignment = TextAlignmentOptions.Center;
                    iconTmp.color = Color.white;
                    iconTmp.raycastTarget = false;
                }
            }

            // Pause Modal Panel
            if (pausePanelObj == null)
            {
                var existingPanel = transform.Find("PausePanel");
                if (existingPanel != null)
                {
                    pausePanelObj = existingPanel.gameObject;
                }
                else
                {
                    pausePanelObj = new GameObject("PausePanel");
                    pausePanelObj.transform.SetParent(transform, false);

                    var rt = pausePanelObj.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    var bg = pausePanelObj.AddComponent<Image>();
                    bg.color = new Color(0.04f, 0.05f, 0.07f, 0.92f);
                    bg.raycastTarget = true;

                    var panelCard = new GameObject("Card", typeof(RectTransform));
                    panelCard.transform.SetParent(pausePanelObj.transform, false);
                    var cardRt = panelCard.GetComponent<RectTransform>();
                    cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                    cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                    cardRt.pivot = new Vector2(0.5f, 0.5f);
                    cardRt.sizeDelta = new Vector2(400f, 440f);

                    // Title
                    var titleObj = new GameObject("TitleText", typeof(RectTransform));
                    titleObj.transform.SetParent(cardRt, false);
                    var titleRt = titleObj.GetComponent<RectTransform>();
                    titleRt.anchorMin = new Vector2(0.5f, 1f);
                    titleRt.anchorMax = new Vector2(0.5f, 1f);
                    titleRt.pivot = new Vector2(0.5f, 1f);
                    titleRt.anchoredPosition = new Vector2(0f, -20f);
                    titleRt.sizeDelta = new Vector2(360f, 60f);

                    var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
                    if (font != null) titleTmp.font = font;
                    titleTmp.text = "PAUSED";
                    titleTmp.fontSize = 46;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                    titleTmp.color = Color.white;
                    titleTmp.raycastTarget = false;

                    // Buttons Layout
                    resumeButton = CreatePauseMenuButton(cardRt, "ResumeButton", "RESUME", 0f, font, buttonSprite);
                    resumeButton.onClick.AddListener(ResumeGame);

                    restartButton = CreatePauseMenuButton(cardRt, "RestartButton", "RESTART", -76f, font, buttonSprite);
                    restartButton.onClick.AddListener(RestartCurrentScene);

                    mainMenuButton = CreatePauseMenuButton(cardRt, "MainMenuButton", "MAIN MENU", -152f, font, buttonSprite);
                    mainMenuButton.onClick.AddListener(LoadMainMenu);
                }
            }
        }

        private Button CreatePauseMenuButton(RectTransform parent, string name, string label, float yPos, TMP_FontAsset font, Sprite sprite)
        {
            var btnObj = new GameObject(name, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);

            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(280f, 56f); // 56dp height

            var img = btnObj.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.2f, 0.22f, 0.26f, 0.95f);
            img.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            btn.colors = cb;

            var textObj = new GameObject("Label", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return btn;
        }
    }
}
