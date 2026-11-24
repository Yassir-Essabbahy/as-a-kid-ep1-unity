using System.Collections;
using System.Collections.Generic;
using ModularFirstPersonController;
using Story;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AsAKid.Conversation
{
    /// <summary>
    /// Prefab-based Ask Questions conversation UI.
    /// Shows an NPC's name, optional greeting, 3 question buttons (with already-asked
    /// questions visibly dimmed and labeled), a Leave button, and a typewriter reply area.
    /// Locks player movement/look and restores cursor &amp; mobile touch controls on any exit route.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConversationUI : MonoBehaviour
    {
        private enum ConversationState
        {
            Closed = 0,
            Greeting = 1,
            QuestionMenu = 2,
            Answering = 3
        }

        public static ConversationUI Instance { get; private set; }
        public static bool IsAnyActive => Instance != null && Instance._state != ConversationState.Closed;

        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text npcNameText;
        [SerializeField] private GameObject answerContainer;
        [SerializeField] private TMP_Text answerText;
        [SerializeField] private TMP_Text continueHintText;
        [SerializeField] private GameObject questionsContainer;
        [SerializeField] private Button[] questionButtons = new Button[NpcQuestionSet.QuestionCount];
        [SerializeField] private TMP_Text[] questionLabels = new TMP_Text[NpcQuestionSet.QuestionCount];
        [SerializeField] private Button leaveButton;
        [SerializeField] private TMP_Text leaveButtonLabel;

        [Header("Behavior & Styling")]
        [SerializeField] private bool playDoudouInnerVoice = false;
        [SerializeField] private float typewriterCharactersPerSecond = 25f;
        [SerializeField] private bool forceBoldFonts = false;
        [SerializeField] private bool forceUpperCase = false;
        [SerializeField, Range(0.25f, 1f)] private float characterTextAlpha = 1f;
        [SerializeField] private bool showTextBackdropStrip = false;
        [SerializeField, Range(0f, 1f)] private float backdropAlpha = 0.88f;
        [SerializeField, Range(0f, 0.5f)] private float backdropCharacterTint = 0f;
        [SerializeField] private Vector2 backdropPadding = new Vector2(14f, 4f);
        [SerializeField] private Color activeQuestionTextColor = Color.white;
        [SerializeField] private Color askedQuestionTextColor = new Color(0.62f, 0.62f, 0.62f, 0.55f);
        [SerializeField] private Color activeButtonBgColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Color askedButtonBgColor = new Color(0f, 0f, 0f, 0.55f);

        [Header("Cinematic Letterbox Bands")]
        [SerializeField] private bool showCinematicBands = true;
        [SerializeField] private RectTransform topBand;
        [SerializeField] private RectTransform bottomBand;
        [SerializeField] private float topBandHeight = 140f;
        [SerializeField] private float bottomBandHeight = 220f;
        [SerializeField] private float bandSlideDuration = 0.45f;

        private ConversationState _state = ConversationState.Closed;
        private NpcQuestionSet _currentNpc;
        private NpcLookAt _currentLookAt;
        private int _currentQuestionIndex = -1;
        private Coroutine _flowCoroutine;
        private Coroutine _bandSlideCoroutine;
        private string _currentSpeakerDisplayName = "";

        // Cached references so Update() performs zero allocations
        private FirstPersonController _lockedController;
        private MobileTouchControls _cachedTouchControls;
        private bool _hadControlLock;
        private bool _prevPlayerCanMove = true;
        private bool _prevCameraCanMove = true;
        private bool _prevLockCursor = true;
        private CursorLockMode _prevCursorLockMode = CursorLockMode.Locked;
        private bool _prevCursorVisible = false;
        private bool _wasTypewriterRunning = false;
        private int _questionMenuOpenedFrame = -1;
        private string _currentRawLine = "";
        private Color _currentSpeakerColor = Color.white;
        private Color _currentBackdropColor = new Color(0f, 0f, 0f, 0.88f);

        private struct LineBounds
        {
            public bool HasChars;
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;
        }

        private readonly List<Image> _answerLineBackdrops = new List<Image>(6);
        private readonly LineBounds[] _lineBoundsScratch = new LineBounds[16];
        private readonly Image[] _questionBackdrops = new Image[NpcQuestionSet.QuestionCount];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureCinematicBands();
            SnapBandsOffscreen(deactivate: true);
            BindButtonListeners();
            EnsureEventSystemExists();
            ApplyBoldStyling();
            EnsureTextBackdrops();

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
                LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }

            if (_state != ConversationState.Closed)
            {
                ForceCloseWithoutEvents(animateBandsOut: false);
            }
            else if (showCinematicBands)
            {
                SnapBandsOffscreen(deactivate: true);
            }
        }

        private void OnDestroy()
        {
            if (_state != ConversationState.Closed)
            {
                ForceCloseWithoutEvents(animateBandsOut: false);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (_state == ConversationState.Closed)
                return;

            // Safety: if the NPC disappeared, was destroyed, or became inactive, exit cleanly so player is never stuck.
            if (_currentNpc == null || !_currentNpc.gameObject.activeInHierarchy || !_currentNpc.enabled)
            {
                CloseConversation();
                return;
            }

            if (_state == ConversationState.QuestionMenu && Time.frameCount > _questionMenuOpenedFrame)
            {
                bool paused = MobilePauseMenu.Instance != null && MobilePauseMenu.Instance.isPaused;
                if (!paused)
                {
                    if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Tab))
                    {
                        CloseConversation();
                        return;
                    }

                    bool pointerTap = Input.GetMouseButtonDown(0) ||
                                      (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
                    if (pointerTap &&
                        !IsPointerOverAnyQuestionButton() &&
                        !DialogueTypewriterUtility.IsPointerOverPauseButton())
                    {
                        CloseConversation();
                    }
                }
            }
        }

        private bool IsPointerOverAnyQuestionButton()
        {
            if (questionButtons == null)
                return false;

            for (int i = 0; i < questionButtons.Length; i++)
            {
                if (DialogueTypewriterUtility.IsPointerOverButton(questionButtons[i]))
                    return true;
            }

            return false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_state != ConversationState.Closed)
            {
                ForceCloseWithoutEvents(animateBandsOut: false);
            }
            else if (showCinematicBands)
            {
                SnapBandsOffscreen(deactivate: true);
            }
        }

        private void BindButtonListeners()
        {
            if (questionButtons != null)
            {
                for (int i = 0; i < questionButtons.Length; i++)
                {
                    if (questionButtons[i] == null)
                        continue;

                    int capturedIndex = i;
                    questionButtons[i].onClick.RemoveAllListeners();
                    questionButtons[i].onClick.AddListener(() => SelectQuestion(capturedIndex));
                }
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveAllListeners();
                leaveButton.onClick.AddListener(CloseConversation);
            }
        }

        /// <summary>
        /// Opens the conversation UI for the given NPC question set.
        /// Returns false if a conversation is already active or the NPC is invalid/finished.
        /// </summary>
        public bool StartConversation(NpcQuestionSet npc, NpcLookAt lookAt = null)
        {
            if (npc == null || !npc.gameObject.activeInHierarchy)
                return false;

            if (_state != ConversationState.Closed)
                return false;

            if (MobilePauseMenu.Instance != null && MobilePauseMenu.Instance.isPaused)
                return false;

            if (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning)
                return false;

            if (npc.AreAllAsked)
            {
                if (!npc.HasDoneLine())
                    return false;
            }

            LocalizationManager.EnsureArabicFallbackFont();
            EnsureEventSystemExists();

            if (lookAt == null)
            {
                lookAt = npc.GetComponent<NpcLookAt>()
                         ?? npc.GetComponentInChildren<NpcLookAt>(true)
                         ?? npc.GetComponentInParent<NpcLookAt>();
            }

            _currentNpc = npc;
            _currentLookAt = lookAt;
            _currentQuestionIndex = -1;
            _currentRawLine = "";

            AcquirePlayerControlLock();
            if (_currentLookAt != null)
            {
                _currentLookAt.IKActive = true;
            }

            EnsureCinematicBands();
            if (showCinematicBands)
            {
                if ((topBand != null && !topBand.gameObject.activeSelf) ||
                    (bottomBand != null && !bottomBand.gameObject.activeSelf))
                {
                    SnapBandsOffscreen(deactivate: false);
                }
                SlideBands(true);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }

            RefreshHeaderAndLeaveButton();
            _currentNpc.NotifyConversationStarted();

            if (_flowCoroutine != null)
            {
                StopCoroutine(_flowCoroutine);
                _flowCoroutine = null;
            }

            if (npc.AreAllAsked)
            {
                _state = ConversationState.Answering;
                _flowCoroutine = StartCoroutine(PlayDoneOnlyRoutine());
            }
            else if (_currentNpc.HasGreeting() || (playDoudouInnerVoice && !_currentNpc.HasPlayedDoudouWhisper && _currentNpc.HasDoudouWhisper()))
            {
                _state = ConversationState.Greeting;
                _flowCoroutine = StartCoroutine(PlayGreetingRoutine());
            }
            else
            {
                ShowQuestionMenu();
            }

            return true;
        }

        /// <summary>
        /// Exits the conversation cleanly and restores player control.
        /// </summary>
        public void CloseConversation()
        {
            if (_state == ConversationState.Closed)
                return;

            NpcQuestionSet finishedNpc = _currentNpc;
            ForceCloseWithoutEvents(animateBandsOut: isActiveAndEnabled);

            if (finishedNpc != null)
            {
                finishedNpc.NotifyConversationEnded();
            }
        }

        private void ForceCloseWithoutEvents(bool animateBandsOut = false)
        {
            if (_flowCoroutine != null)
            {
                StopCoroutine(_flowCoroutine);
                _flowCoroutine = null;
            }

            _state = ConversationState.Closed;
            _wasTypewriterRunning = false;
            _currentQuestionIndex = -1;
            _currentRawLine = "";
            HideAnswerBackdrops();

            if (continueHintText != null)
            {
                continueHintText.gameObject.SetActive(false);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (_currentLookAt != null)
            {
                _currentLookAt.IKActive = false;
                _currentLookAt = null;
            }

            _currentNpc = null;
            ReleasePlayerControlLock();

            if (showCinematicBands)
            {
                if (animateBandsOut && isActiveAndEnabled)
                {
                    SlideBands(false);
                }
                else
                {
                    SnapBandsOffscreen(deactivate: true);
                }
            }
        }

        private void EnsureCinematicBands()
        {
            if (!showCinematicBands)
                return;

            topBand = EnsureBandRect(topBand, "TopBand", true, topBandHeight, 0);
            bottomBand = EnsureBandRect(bottomBand, "BottomBand", false, bottomBandHeight, 1);
        }

        private RectTransform EnsureBandRect(RectTransform existing, string bandName, bool isTop, float height, int siblingIndex)
        {
            if (existing == null)
            {
                Transform found = transform.Find(bandName);
                if (found != null)
                {
                    existing = found as RectTransform;
                }
                else
                {
                    var go = new GameObject(bandName, typeof(RectTransform), typeof(Image));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(transform, false);
                    existing = go.GetComponent<RectTransform>();
                }
            }

            if (existing != null)
            {
                if (isTop)
                {
                    existing.anchorMin = new Vector2(0f, 1f);
                    existing.anchorMax = new Vector2(1f, 1f);
                    existing.pivot = new Vector2(0.5f, 1f);
                }
                else
                {
                    existing.anchorMin = new Vector2(0f, 0f);
                    existing.anchorMax = new Vector2(1f, 0f);
                    existing.pivot = new Vector2(0.5f, 0f);
                }
                existing.sizeDelta = new Vector2(0f, height);
                existing.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, Mathf.Max(0, transform.childCount - 1)));

                var img = existing.GetComponent<Image>() ?? existing.gameObject.AddComponent<Image>();
                img.sprite = null;
                img.color = Color.black;
                img.raycastTarget = false;
            }

            return existing;
        }

        private void SnapBandsOffscreen(bool deactivate)
        {
            if (_bandSlideCoroutine != null)
            {
                StopCoroutine(_bandSlideCoroutine);
                _bandSlideCoroutine = null;
            }

            if (topBand != null)
            {
                topBand.sizeDelta = new Vector2(topBand.sizeDelta.x, topBandHeight);
                topBand.anchoredPosition = new Vector2(0f, topBandHeight);
                topBand.gameObject.SetActive(!deactivate && showCinematicBands);
            }

            if (bottomBand != null)
            {
                bottomBand.sizeDelta = new Vector2(bottomBand.sizeDelta.x, bottomBandHeight);
                bottomBand.anchoredPosition = new Vector2(0f, -bottomBandHeight);
                bottomBand.gameObject.SetActive(!deactivate && showCinematicBands);
            }
        }

        private void SlideBands(bool slideIn)
        {
            if (!showCinematicBands)
                return;

            EnsureCinematicBands();
            if (_bandSlideCoroutine != null)
            {
                StopCoroutine(_bandSlideCoroutine);
                _bandSlideCoroutine = null;
            }

            if (topBand == null || bottomBand == null)
                return;

            if (slideIn)
            {
                topBand.gameObject.SetActive(true);
                bottomBand.gameObject.SetActive(true);
            }

            if (!isActiveAndEnabled)
            {
                SnapBandsOffscreen(deactivate: !slideIn);
                return;
            }

            _bandSlideCoroutine = StartCoroutine(SlideBandsRoutine(slideIn));
        }

        private IEnumerator SlideBandsRoutine(bool slideIn)
        {
            Vector2 topStart = topBand.anchoredPosition;
            Vector2 topEnd = slideIn ? Vector2.zero : new Vector2(0f, topBandHeight);

            Vector2 botStart = bottomBand.anchoredPosition;
            Vector2 botEnd = slideIn ? Vector2.zero : new Vector2(0f, -bottomBandHeight);

            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, bandSlideDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                if (topBand != null) topBand.anchoredPosition = Vector2.Lerp(topStart, topEnd, smooth);
                if (bottomBand != null) bottomBand.anchoredPosition = Vector2.Lerp(botStart, botEnd, smooth);
                yield return null;
            }

            if (topBand != null)
            {
                topBand.anchoredPosition = topEnd;
                if (!slideIn) topBand.gameObject.SetActive(false);
            }
            if (bottomBand != null)
            {
                bottomBand.anchoredPosition = botEnd;
                if (!slideIn) bottomBand.gameObject.SetActive(false);
            }

            _bandSlideCoroutine = null;
        }

        private void ApplyPanelRectForMode(bool isQuestionMenu)
        {
            if (!showCinematicBands || panelRoot == null)
                return;

            RectTransform panelRt = panelRoot.GetComponent<RectTransform>();
            if (panelRt == null)
                return;

            if (isQuestionMenu)
            {
                panelRt.anchorMin = new Vector2(0.22f, 0.25f);
                panelRt.anchorMax = new Vector2(0.78f, 0.55f);
            }
            else
            {
                panelRt.anchorMin = new Vector2(0.08f, 0.045f);
                panelRt.anchorMax = new Vector2(0.86f, 0.185f);

                if (answerContainer != null)
                {
                    RectTransform ansContRt = answerContainer.GetComponent<RectTransform>();
                    if (ansContRt != null)
                    {
                        ansContRt.anchorMin = Vector2.zero;
                        ansContRt.anchorMax = Vector2.one;
                        ansContRt.offsetMin = Vector2.zero;
                        ansContRt.offsetMax = Vector2.zero;
                    }
                }
            }

            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
        }

        private IEnumerator PlayGreetingRoutine()
        {
            if (_currentNpc == null)
            {
                CloseConversation();
                yield break;
            }

            _state = ConversationState.Greeting;

            if (_currentNpc.HasGreeting())
            {
                RefreshSpeakerHeader(_currentNpc.GetDisplayName(), _currentNpc.NpcId);
                string greetingLine = _currentNpc.GetGreetingText();

                var storyCtrl = MetroStorySequenceController.Instance;
                if (string.Equals(_currentNpc.NpcId, "neighbor", System.StringComparison.OrdinalIgnoreCase) &&
                    storyCtrl != null &&
                    storyCtrl.fuseBoxInspected &&
                    storyCtrl.neighborGaveAdvice &&
                    !storyCtrl.isPowerRestored)
                {
                    string reminder = LocalizationManager.Resolve("neighbor_fuse_reminder_01", "");
                    if (!string.IsNullOrEmpty(reminder))
                    {
                        int colonIdx = reminder.IndexOf(':');
                        greetingLine = (colonIdx >= 0 && colonIdx + 1 < reminder.Length)
                            ? reminder.Substring(colonIdx + 1).TrimStart()
                            : reminder;
                    }
                }

                _currentRawLine = greetingLine;
                yield return RunLineDisplayRoutine(_currentRawLine);

                if (_state != ConversationState.Greeting || _currentNpc == null)
                    yield break;
            }

            if (playDoudouInnerVoice && !_currentNpc.HasPlayedDoudouWhisper && _currentNpc.HasDoudouWhisper())
            {
                _currentNpc.MarkDoudouWhisperPlayed();
                RefreshSpeakerHeader(MetroStorySequenceController.TeddyName, "doudou");
                _currentRawLine = _currentNpc.GetDoudouWhisperText();
                yield return RunLineDisplayRoutine(_currentRawLine);

                if (_state != ConversationState.Greeting || _currentNpc == null)
                    yield break;

                RefreshSpeakerHeader(_currentNpc.GetDisplayName(), _currentNpc.NpcId);
            }

            ShowQuestionMenu();
        }

        private IEnumerator PlayDoneOnlyRoutine()
        {
            if (_currentNpc == null)
            {
                CloseConversation();
                yield break;
            }

            _state = ConversationState.Answering;
            RefreshSpeakerHeader(_currentNpc.GetDisplayName(), _currentNpc.NpcId);
            _currentRawLine = _currentNpc.GetDoneText();
            if (!string.IsNullOrEmpty(_currentRawLine))
            {
                yield return RunLineDisplayRoutine(_currentRawLine);
            }

            CloseConversation();
        }

        private void ShowQuestionMenu()
        {
            if (_currentNpc == null)
            {
                CloseConversation();
                return;
            }

            _state = ConversationState.QuestionMenu;
            _questionMenuOpenedFrame = Time.frameCount;
            _currentQuestionIndex = -1;
            _wasTypewriterRunning = false;
            HideAnswerBackdrops();
            ApplyPanelRectForMode(isQuestionMenu: true);

            if (answerContainer != null)
            {
                answerContainer.SetActive(false);
            }

            if (continueHintText != null)
            {
                continueHintText.gameObject.SetActive(false);
            }

            if (questionsContainer != null)
            {
                questionsContainer.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }

            RefreshQuestionButtons();
        }

        public void SelectQuestion(int index)
        {
            if (_state != ConversationState.QuestionMenu || _currentNpc == null)
                return;

            if (index < 0 || index >= NpcQuestionSet.QuestionCount)
                return;

            if (_currentNpc.HasAsked(index))
                return;

            _currentQuestionIndex = index;
            _currentNpc.MarkQuestionAsked(index);

            if (_flowCoroutine != null)
            {
                StopCoroutine(_flowCoroutine);
            }

            _flowCoroutine = StartCoroutine(PlayAnswerRoutine(index));
        }

        private IEnumerator PlayAnswerRoutine(int index)
        {
            if (_currentNpc == null)
            {
                CloseConversation();
                yield break;
            }

            _state = ConversationState.Answering;
            RefreshSpeakerHeader(_currentNpc.GetDisplayName(), _currentNpc.NpcId);
            _currentRawLine = _currentNpc.GetAnswerText(index);

            yield return RunLineDisplayRoutine(_currentRawLine);

            if (_state != ConversationState.Answering || _currentNpc == null)
                yield break;

            if (_currentNpc.AreAllAsked)
            {
                string doneText = _currentNpc.GetDoneText();
                if (!string.IsNullOrEmpty(doneText))
                {
                    _currentRawLine = doneText;
                    yield return RunLineDisplayRoutine(_currentRawLine);

                    if (_state != ConversationState.Answering || _currentNpc == null)
                        yield break;
                }

                if (playDoudouInnerVoice && !_currentNpc.HasPlayedDoudouReaction && _currentNpc.HasDoudouReaction())
                {
                    _currentNpc.MarkDoudouReactionPlayed();
                    RefreshSpeakerHeader(MetroStorySequenceController.TeddyName, "doudou");
                    _currentRawLine = _currentNpc.GetDoudouReactionText();
                    yield return RunLineDisplayRoutine(_currentRawLine);

                    if (_state != ConversationState.Answering || _currentNpc == null)
                        yield break;
                }

                _currentNpc.FinalizeCompletionIfAllAsked();
                CloseConversation();
                yield break;
            }

            ShowQuestionMenu();
        }

        private string FormatLineWithSpeaker(string rawLine)
        {
            string line = rawLine ?? "";
            if (string.IsNullOrEmpty(_currentSpeakerDisplayName))
                return line;

            string trimmed = line.TrimStart();
            int colonIdx = trimmed.IndexOf(':');
            if (trimmed.StartsWith(_currentSpeakerDisplayName + ":", System.StringComparison.OrdinalIgnoreCase) ||
                (colonIdx > 0 && colonIdx < 25))
            {
                return line;
            }

            return $"{_currentSpeakerDisplayName}: {line}";
        }

        private IEnumerator RunLineDisplayRoutine(string rawLine)
        {
            ApplyPanelRectForMode(isQuestionMenu: false);

            if (questionsContainer != null)
            {
                questionsContainer.SetActive(false);
            }

            if (answerContainer != null)
            {
                answerContainer.SetActive(true);
            }

            if (continueHintText != null)
            {
                continueHintText.gameObject.SetActive(false);
            }

            if (answerText != null)
            {
                string displayLine = FormatLineWithSpeaker(rawLine);
                bool isArabic = LocalizationManager.ContainsArabic(displayLine);

                answerText.enableAutoSizing = false;
                answerText.fontSize = NpcDialogueManager.STANDARD_FONT_SIZE;
                answerText.characterSpacing = 0f;
                answerText.alignment = isArabic ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                answerText.isRightToLeftText = isArabic;
                answerText.textWrappingMode = TextWrappingModes.Normal;
                Canvas.ForceUpdateCanvases();
                HideAnswerBackdrops();

                _wasTypewriterRunning = true;
                yield return DialogueTypewriterUtility.TypeTextRoutine(
                    answerText,
                    displayLine,
                    typewriterCharactersPerSecond,
                    allowSkip: true,
                    externalSkipCheck: null,
                    ignorePointerOverButton: leaveButton,
                    preserveAlignment: true,
                    onVisibleCharactersChanged: showTextBackdropStrip
                        ? (visible, _) => SyncPerLineBackdrops(_answerLineBackdrops, answerText, visible, _currentBackdropColor)
                        : null);
                _wasTypewriterRunning = false;
            }

            // Wait one frame so a skip tap doesn't also dismiss the completed line on the exact same frame
            yield return null;

            if ((_state == ConversationState.Greeting || _state == ConversationState.Answering) && continueHintText != null)
            {
                RefreshContinueHint();
                continueHintText.gameObject.SetActive(true);
            }

            while (_state == ConversationState.Greeting || _state == ConversationState.Answering)
            {
                bool paused = MobilePauseMenu.Instance != null && MobilePauseMenu.Instance.isPaused;
                if (!paused)
                {
                    if (DialogueTypewriterUtility.IsAdvanceOrSkipInputPressed(leaveButton) ||
                        Input.GetKeyDown(KeyCode.Return) ||
                        Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (continueHintText != null)
            {
                continueHintText.gameObject.SetActive(false);
            }
        }

        private void HandleLanguageChanged(string newLanguage)
        {
            if (_state == ConversationState.Closed || _currentNpc == null)
                return;

            RefreshHeaderAndLeaveButton();
            RefreshContinueHint();

            if (_state == ConversationState.QuestionMenu)
            {
                RefreshQuestionButtons();
            }
            else if ((_state == ConversationState.Greeting || _state == ConversationState.Answering) && answerText != null)
            {
                _currentRawLine = _state == ConversationState.Greeting
                    ? _currentNpc.GetGreetingText()
                    : _currentNpc.GetAnswerText(_currentQuestionIndex);

                if (!_wasTypewriterRunning)
                {
                    string displayLine = FormatLineWithSpeaker(_currentRawLine);
                    bool isArabic = LocalizationManager.ContainsArabic(displayLine);

                    answerText.enableAutoSizing = false;
                    answerText.fontSize = NpcDialogueManager.STANDARD_FONT_SIZE;
                    answerText.characterSpacing = 0f;
                    answerText.alignment = isArabic ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                    answerText.isRightToLeftText = isArabic;
                    LocalizationManager.ApplyText(answerText, displayLine, preserveAlignment: true);
                    answerText.maxVisibleCharacters = int.MaxValue;
                    answerText.ForceMeshUpdate();
                    if (showTextBackdropStrip)
                    {
                        SyncPerLineBackdrops(_answerLineBackdrops, answerText, int.MaxValue, _currentBackdropColor);
                    }
                    else
                    {
                        HideAnswerBackdrops();
                    }
                }
            }
        }

        private void RefreshHeaderAndLeaveButton()
        {
            if (_currentNpc != null)
            {
                RefreshSpeakerHeader(_currentNpc.GetDisplayName(), _currentNpc.NpcId);
            }

            if (leaveButton != null && leaveButton.gameObject.activeSelf)
            {
                leaveButton.gameObject.SetActive(false);
            }
        }

        private void RefreshSpeakerHeader(string speakerDisplayName, string speakerIdFallback)
        {
            _currentSpeakerDisplayName = speakerDisplayName ?? "";

            Color c = NpcDialogueManager.GetSpeakerColor(speakerDisplayName);
            if (c == Color.white && !string.IsNullOrEmpty(speakerIdFallback))
            {
                c = NpcDialogueManager.GetSpeakerColor(speakerIdFallback);
            }

            _currentSpeakerColor = c;
            Color tinted = new Color(c.r, c.g, c.b, characterTextAlpha);
            _currentBackdropColor = new Color(
                c.r * backdropCharacterTint,
                c.g * backdropCharacterTint,
                c.b * backdropCharacterTint,
                backdropAlpha);

            if (npcNameText != null && npcNameText.gameObject.activeSelf)
            {
                npcNameText.gameObject.SetActive(false);
            }

            if (answerText != null)
            {
                answerText.color = tinted;
                EnsureBold(answerText);
            }

            if (continueHintText != null && continueHintText.gameObject.activeSelf)
            {
                continueHintText.gameObject.SetActive(false);
            }
        }

        private void EnsureTextBackdrops()
        {
            if (!showTextBackdropStrip)
                return;

            EnsureAnswerStripPoolSize(1);

            if (questionLabels != null)
            {
                for (int i = 0; i < questionLabels.Length && i < _questionBackdrops.Length; i++)
                {
                    _questionBackdrops[i] = CreateOrReuseBackdrop(questionLabels[i], $"Backdrop_Question_{i + 1}", _questionBackdrops[i]);
                }
            }
        }

        private void EnsureAnswerStripPoolSize(int count)
        {
            if (answerText == null || answerText.transform.parent == null)
                return;

            while (_answerLineBackdrops.Count < count)
            {
                int idx = _answerLineBackdrops.Count;
                Image img = CreateOrReuseBackdrop(answerText, $"Backdrop_Answer_Line_{idx + 1}", null);
                if (img == null)
                    break;
                img.enabled = false;
                _answerLineBackdrops.Add(img);
            }
        }

        private void HideAnswerBackdrops()
        {
            for (int i = 0; i < _answerLineBackdrops.Count; i++)
            {
                if (_answerLineBackdrops[i] != null)
                {
                    _answerLineBackdrops[i].enabled = false;
                }
            }
        }

        private static Image CreateOrReuseBackdrop(TMP_Text label, string name, Image existing)
        {
            if (existing != null)
                return existing;
            if (label == null || label.transform.parent == null)
                return null;

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(label.transform.parent, false);
            rt.SetSiblingIndex(label.transform.GetSiblingIndex());

            var img = go.GetComponent<Image>();
            img.sprite = null;
            img.raycastTarget = false;
            img.color = Color.clear;
            return img;
        }

        private void SyncBackdrop(Image backdrop, TMP_Text label, string measuredText, Color bgColor)
        {
            if (backdrop == null || label == null)
                return;

            if (!showTextBackdropStrip || string.IsNullOrWhiteSpace(measuredText))
            {
                backdrop.enabled = false;
                return;
            }

            RectTransform labelRt = label.rectTransform;
            float availW = labelRt.rect.width > 10f ? labelRt.rect.width : 1100f;
            Vector2 pref = label.GetPreferredValues(measuredText, availW, 0f);
            float w = Mathf.Min(availW, Mathf.Max(20f, pref.x)) + backdropPadding.x * 2f;
            float h = Mathf.Max(26f, pref.y) + backdropPadding.y * 2f;

            RectTransform rt = backdrop.rectTransform;
            Vector2 midAnchor = (labelRt.anchorMin + labelRt.anchorMax) * 0.5f;
            rt.anchorMin = midAnchor;
            rt.anchorMax = midAnchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (labelRt.offsetMin + labelRt.offsetMax) * 0.5f;
            rt.sizeDelta = new Vector2(w, h);

            backdrop.color = bgColor;
            backdrop.enabled = true;
        }

        private void SyncPerLineBackdrops(List<Image> stripPool, TMP_Text label, int visibleCount, Color bgColor)
        {
            if (label == null)
                return;

            if (!showTextBackdropStrip || visibleCount <= 0)
            {
                HideAnswerBackdrops();
                return;
            }

            TMP_TextInfo textInfo = label.textInfo;
            if (textInfo == null || textInfo.characterCount <= 0 || textInfo.characterInfo == null)
            {
                HideAnswerBackdrops();
                return;
            }

            int maxLines = Mathf.Clamp(textInfo.lineCount, 1, _lineBoundsScratch.Length);
            for (int l = 0; l < maxLines; l++)
            {
                _lineBoundsScratch[l] = new LineBounds
                {
                    HasChars = false,
                    MinX = float.PositiveInfinity,
                    MaxX = float.NegativeInfinity,
                    MinY = float.PositiveInfinity,
                    MaxY = float.NegativeInfinity
                };
            }

            int clampedVisible = Mathf.Min(visibleCount, textInfo.characterCount);
            for (int i = 0; i < clampedVisible; i++)
            {
                TMP_CharacterInfo ci = textInfo.characterInfo[i];
                if (char.IsWhiteSpace(ci.character))
                    continue;

                int lineIdx = Mathf.Clamp(ci.lineNumber, 0, maxLines - 1);
                LineBounds lb = _lineBoundsScratch[lineIdx];

                float left = Mathf.Min(ci.bottomLeft.x, ci.origin);
                float right = Mathf.Max(ci.topRight.x, ci.xAdvance);
                if (right < left)
                {
                    float tmp = left;
                    left = right;
                    right = tmp;
                }

                lb.MinX = Mathf.Min(lb.MinX, left);
                lb.MaxX = Mathf.Max(lb.MaxX, right);

                if (textInfo.lineInfo != null && lineIdx < textInfo.lineCount)
                {
                    TMP_LineInfo li = textInfo.lineInfo[lineIdx];
                    lb.MinY = Mathf.Min(lb.MinY, li.descender);
                    lb.MaxY = Mathf.Max(lb.MaxY, li.ascender);
                }
                else
                {
                    lb.MinY = Mathf.Min(lb.MinY, ci.descender);
                    lb.MaxY = Mathf.Max(lb.MaxY, ci.ascender);
                }

                lb.HasChars = true;
                _lineBoundsScratch[lineIdx] = lb;
            }

            int used = 0;
            for (int l = 0; l < maxLines; l++)
            {
                LineBounds lb = _lineBoundsScratch[l];
                if (!lb.HasChars)
                    continue;

                EnsureAnswerStripPoolSize(used + 1);
                if (used >= stripPool.Count)
                    break;

                Image strip = stripPool[used++];
                if (strip == null)
                    continue;

                RectTransform rt = strip.rectTransform;
                Transform parent = rt.parent;
                if (parent == null)
                    continue;

                Vector3 localCenter = new Vector3((lb.MinX + lb.MaxX) * 0.5f, (lb.MinY + lb.MaxY) * 0.5f, 0f);
                Vector3 parentCenter = parent.InverseTransformPoint(label.rectTransform.TransformPoint(localCenter));

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.localPosition = new Vector3(parentCenter.x, parentCenter.y, 0f);
                rt.sizeDelta = new Vector2(
                    Mathf.Max(6f, lb.MaxX - lb.MinX) + backdropPadding.x * 2f,
                    Mathf.Max(28f, lb.MaxY - lb.MinY) + backdropPadding.y * 2f);

                strip.color = bgColor;
                strip.enabled = true;
            }

            for (int i = used; i < stripPool.Count; i++)
            {
                if (stripPool[i] != null)
                {
                    stripPool[i].enabled = false;
                }
            }
        }

        private void ApplyBoldStyling()
        {
            TMP_FontAsset defaultFont = NpcDialogueManager.DefaultDialogueFont;

            EnsureBold(npcNameText);
            EnsureBold(answerText);
            EnsureBold(continueHintText);
            EnsureBold(leaveButtonLabel);

            if (answerText != null)
            {
                if (defaultFont != null)
                    answerText.font = defaultFont;
                answerText.enableAutoSizing = false;
                answerText.fontSize = NpcDialogueManager.STANDARD_FONT_SIZE;
                answerText.textWrappingMode = TextWrappingModes.Normal;
            }

            if (continueHintText != null)
            {
                if (defaultFont != null)
                    continueHintText.font = defaultFont;

                if (bottomBand != null)
                {
                    if (continueHintText.transform.parent != bottomBand)
                    {
                        continueHintText.transform.SetParent(bottomBand, false);
                    }

                    RectTransform hintRt = continueHintText.rectTransform;
                    hintRt.anchorMin = new Vector2(1f, 0f);
                    hintRt.anchorMax = new Vector2(1f, 0f);
                    hintRt.pivot = new Vector2(1f, 0f);
                    hintRt.anchoredPosition = new Vector2(-50f, 20f);
                    hintRt.sizeDelta = new Vector2(180f, 44f);
                }

                continueHintText.enableAutoSizing = false;
                continueHintText.fontSize = 20f;
                continueHintText.alignment = TextAlignmentOptions.MidlineRight;
                continueHintText.color = Color.white;
                continueHintText.raycastTarget = false;
                continueHintText.gameObject.SetActive(false);
            }

            if (questionLabels != null)
            {
                for (int i = 0; i < questionLabels.Length; i++)
                {
                    if (questionLabels[i] != null && defaultFont != null)
                    {
                        questionLabels[i].font = defaultFont;
                    }
                    EnsureBold(questionLabels[i]);
                }
            }
        }

        private void EnsureBold(TMP_Text label)
        {
            if (label == null)
                return;

            if (forceBoldFonts)
                label.fontStyle |= FontStyles.Bold;
            else
                label.fontStyle &= ~FontStyles.Bold;

            if (forceUpperCase)
                label.fontStyle |= FontStyles.UpperCase;
            else
                label.fontStyle &= ~FontStyles.UpperCase;
        }

        private void RefreshContinueHint()
        {
            if (continueHintText == null)
                return;

            string hint = LocalizationManager.Resolve("ui_continue", "CONTINUE >");
            EnsureBold(continueHintText);
            continueHintText.color = Color.white;
            LocalizationManager.ApplyText(continueHintText, hint, preserveAlignment: true);
        }

        private void RefreshQuestionButtons()
        {
            if (_currentNpc == null || questionButtons == null)
                return;

            TMP_FontAsset defaultFont = NpcDialogueManager.DefaultDialogueFont;
            string askedTag = LocalizationManager.Resolve("ui_conv_asked", "(Asked)");

            for (int i = 0; i < NpcQuestionSet.QuestionCount; i++)
            {
                Button btn = i < questionButtons.Length ? questionButtons[i] : null;
                TMP_Text label = (questionLabels != null && i < questionLabels.Length) ? questionLabels[i] : null;

                if (btn == null)
                    continue;

                bool asked = _currentNpc.HasAsked(i);
                btn.interactable = !asked;

                var outline = btn.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
                }

                var img = btn.GetComponent<Image>() ?? btn.targetGraphic as Image;
                if (img != null)
                {
                    img.sprite = null;
                    img.type = Image.Type.Simple;
                    img.color = asked ? askedButtonBgColor : activeButtonBgColor;
                    btn.targetGraphic = img;
                }

                btn.transition = Selectable.Transition.ColorTint;
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                cb.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
                cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                cb.colorMultiplier = 1f;
                cb.fadeDuration = 0.1f;
                btn.colors = cb;

                if (label != null)
                {
                    if (defaultFont != null)
                        label.font = defaultFont;

                    string questionLine = _currentNpc.GetQuestionText(i);
                    string formatted = asked ? $"{questionLine}  {askedTag}" : questionLine;
                    label.color = asked ? askedQuestionTextColor : activeQuestionTextColor;
                    label.fontSize = 22f;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 15f;
                    label.fontSizeMax = 22f;
                    label.characterSpacing = 0f;
                    EnsureBold(label);
                    label.alignment = TextAlignmentOptions.Center;
                    LocalizationManager.ApplyText(label, formatted, preserveAlignment: true);

                    if (i < _questionBackdrops.Length)
                    {
                        if (!showTextBackdropStrip)
                        {
                            if (_questionBackdrops[i] != null)
                                _questionBackdrops[i].enabled = false;
                        }
                        else
                        {
                            float bgAlpha = asked ? backdropAlpha * 0.45f : backdropAlpha;
                            SyncBackdrop(_questionBackdrops[i], label, label.text, new Color(0f, 0f, 0f, bgAlpha));
                        }
                    }
                }
            }
        }

        private void AcquirePlayerControlLock()
        {
            if (_hadControlLock)
                return;

            _lockedController = FirstPersonController.ActiveInstance ?? FindAnyObjectByType<FirstPersonController>();
            _prevCursorLockMode = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;

            if (_lockedController != null)
            {
                _prevPlayerCanMove = _lockedController.playerCanMove;
                _prevCameraCanMove = _lockedController.cameraCanMove;
                _prevLockCursor = _lockedController.lockCursor;

                _lockedController.lockCursor = false;
                _lockedController.SetControlLocked(true);
                _lockedController.SetCrosshairVisible(false);
            }

            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            _cachedTouchControls = _cachedTouchControls != null
                ? _cachedTouchControls
                : FindAnyObjectByType<MobileTouchControls>();

            if (_cachedTouchControls != null)
            {
                _cachedTouchControls.ResetAllTouchPointers();
                _cachedTouchControls.UpdateVisibility();
            }

            _hadControlLock = true;
        }

        private void ReleasePlayerControlLock()
        {
            if (!_hadControlLock)
                return;

            _hadControlLock = false;

            if (_lockedController != null)
            {
                _lockedController.lockCursor = _prevLockCursor;
                _lockedController.playerCanMove = _prevPlayerCanMove;
                _lockedController.cameraCanMove = _prevCameraCanMove;
                _lockedController.SetControlLocked(false);
                _lockedController.SetCrosshairVisible(true);
                _lockedController = null;
            }

            bool isPaused = MobilePauseMenu.Instance != null && MobilePauseMenu.Instance.isPaused;
            if (!Application.isMobilePlatform && !isPaused)
            {
                Cursor.lockState = _prevLockCursor ? CursorLockMode.Locked : _prevCursorLockMode;
                Cursor.visible = _prevLockCursor ? false : _prevCursorVisible;
            }

            if (_cachedTouchControls == null)
            {
                _cachedTouchControls = FindAnyObjectByType<MobileTouchControls>();
            }

            if (_cachedTouchControls != null)
            {
                _cachedTouchControls.ResetAllTouchPointers();
                _cachedTouchControls.UpdateVisibility();
            }
        }

        private static void EnsureEventSystemExists()
        {
            if (EventSystem.current != null)
                return;

            var existing = FindAnyObjectByType<EventSystem>();
            if (existing != null)
                return;

            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(esGo);
        }
    }
}
