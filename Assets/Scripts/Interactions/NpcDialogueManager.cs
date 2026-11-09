using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcDialogueManager : MonoBehaviour
{
    private static NpcDialogueManager _instance;
    public static NpcDialogueManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<NpcDialogueManager>();
                if (_instance != null)
                {
                    _instance.EnsureBandsBound();
                }
            }
            return _instance;
        }
        set => _instance = value;
    }

    [Header("UI")]
    public GameObject talkPanel;
    public GameObject choicePack;
    public TextMeshProUGUI dialogueText;

    [Header("Cinematic Letterbox Bands")]
    public RectTransform topBand;
    public RectTransform bottomBand;
    public float topBandHeight = 140f;
    public float bottomBandHeight = 220f;
    public float slideDuration = 0.45f;

    [Header("Buttons")]
    public Button continueButton;

    [Header("Typing")]
    [Min(0f)]
    public float charactersPerSecond = 25f;
    public AudioClip typingSound;
    public AudioSource typingSource;

    [Header("Choice")]
    public int lastChoiceIndex;

    public event System.Action<int, string> OnDialogueLineStarted;
    public static event System.Action<int, string> OnAnyDialogueLineStarted;

    private bool choiceMade;
    private bool dialogueRunning;
    private Coroutine slideCoroutine;

    private bool continueClicked;
    private bool skipTypingRequested;
    private bool isTyping;

    public const float STANDARD_FONT_SIZE = 28f;

    private static TMP_FontAsset _defaultFont;
    public static TMP_FontAsset DefaultDialogueFont
    {
        get
        {
            if (_defaultFont == null)
            {
                _defaultFont = Resources.Load<TMP_FontAsset>("Fonts/GeistPixel-Regular-VariableFont_ELSH SDF");
            }
            return _defaultFont;
        }
    }

    private Color currentDialogueColor = Color.white;

    /// <summary>
    /// Centralized speaker color palette for all NPCs and characters.
    /// Teddy = Amber/Orange, Teacher = Warm Yellow, Neighbor = Mint Green,
    /// Shop Owner = Warm Coral, Bully = Crimson Red, Brother = Soft Cyan,
    /// Mother = Soft Lilac, Father = Slate Blue, Intercom = Terminal Aqua,
    /// Player/Child/Default = Pure Crisp White.
    /// </summary>
    public static Color GetSpeakerColor(string speakerOrNpcName)
    {
        if (string.IsNullOrEmpty(speakerOrNpcName)) return Color.white;
        string s = speakerOrNpcName.Trim().ToLowerInvariant();

        // Teddy / Mistfox
        if (s.Contains("teddy") || s.Contains("doudou") || s.Contains("mistfox") || s.Contains("nounours") || s.Contains("الدب") || s.Contains("دودو"))
            return new Color(1.0f, 0.65f, 0.30f, 1.0f); // #FFA64D Soft Amber / Orange

        // Teacher
        if (s.Contains("teacher") || s.Contains("professeur") || s.Contains("prof") || s.Contains("المعلم"))
            return new Color(1.0f, 0.94f, 0.63f, 1.0f); // #FFF0A0 Warm Pale Yellow / Cream

        // Neighbor
        if (s.Contains("neighbor") || s.Contains("neigbhor") || s.Contains("voisin") || s.Contains("الجار"))
            return new Color(0.48f, 0.89f, 0.58f, 1.0f); // #7BE495 Soft Mint / Lime Green

        // Shop Owner / Merchant
        if (s.Contains("shop") || s.Contains("owner") || s.Contains("commercant") || s.Contains("merchant") || s.Contains("المتجر"))
            return new Color(1.0f, 0.55f, 0.40f, 1.0f); // #FF8C66 Warm Coral / Peach

        // Bully
        if (s.Contains("bully") || s.Contains("harceleur") || s.Contains("المتنمر"))
            return new Color(1.0f, 0.42f, 0.42f, 1.0f); // #FF6B6B Muted Crimson / Red

        // Brother
        if (s.Contains("brother") || s.Contains("frere") || s.Contains("الأخ") || s.Contains("اخ"))
            return new Color(0.36f, 0.88f, 0.90f, 1.0f); // #5CE1E6 Soft Sky Blue / Cyan

        // Mother
        if (s.Contains("mother") || s.Contains("mama") || s.Contains("mere") || s.Contains("الأم") || s.Contains("ام"))
            return new Color(0.87f, 0.63f, 0.87f, 1.0f); // #DDA0DD Soft Lilac / Lavender

        // Father
        if (s.Contains("father") || s.Contains("baba") || s.Contains("dad") || s.Contains("pere") || s.Contains("الأب") || s.Contains("اب"))
            return new Color(0.56f, 0.80f, 0.96f, 1.0f); // #90CDF4 Slate / Steel Blue

        // Intercom / System / Announcement
        if (s.Contains("intercom") || s.Contains("interphone") || s.Contains("system") || s.Contains("مكبر"))
            return new Color(0.50f, 1.0f, 0.83f, 1.0f); // #80FFD4 Retro Terminal Aqua

        // Old Traveler / Beggar
        if (s.Contains("traveler") || s.Contains("beggar") || s.Contains("voyageur") || s.Contains("mendiant") || s.Contains("مسافر") || s.Contains("متسول") || s.Contains("عجوز"))
            return new Color(0.92f, 0.80f, 0.60f, 1.0f); // #EBCCA0 Weathered Muted Amber

        // Child / Player / Protagonist / Default
        return Color.white;
    }

    /// <summary>
    /// Dynamically resolves line color based on speaker prefix if present,
    /// or falls back to the default conversation color.
    /// </summary>
    public static Color ResolveColorForLine(string line, Color defaultColor)
    {
        if (!string.IsNullOrEmpty(line))
        {
            int colonIdx = line.IndexOf(':');
            if (colonIdx > 0 && colonIdx < 30)
            {
                string speaker = line.Substring(0, colonIdx).Trim();
                if (speaker.StartsWith("{") && speaker.EndsWith("}"))
                {
                    return GetSpeakerColor("teddy");
                }
                return GetSpeakerColor(speaker);
            }
        }
        if (defaultColor != Color.white && defaultColor.a > 0.01f)
            return defaultColor;
        return Color.white;
    }

    public int LastDialogueEndFrame { get; private set; } = -100;
    public bool IsDialogueRunning => dialogueRunning || (Application.isPlaying && Time.frameCount <= LastDialogueEndFrame + 1);


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"Duplicate NpcDialogueManager found on {gameObject.name}. " +
                "Destroying duplicate."
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (typingSource == null)
        {
            typingSource = GetComponent<AudioSource>();
            if (typingSource == null)
            {
                typingSource = gameObject.AddComponent<AudioSource>();
            }
            typingSource.playOnAwake = false;
            typingSource.loop = true;
            typingSource.spatialBlend = 0f;
            typingSource.volume = 0.35f;
        }

#if UNITY_EDITOR
        if (typingSound == null)
        {
            typingSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/TypingSound.wav");
        }
#endif

        EnsureBandsBound();
        ResetBandsOffscreen();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (talkPanel != null)
            talkPanel.SetActive(false);

        if (choicePack != null)
            choicePack.SetActive(false);
    }

    public void OnContinueClicked()
    {
        if (isTyping)
        {
            skipTypingRequested = true;
            return;
        }

        continueClicked = true;
    }

    public void EnsureBandsBound()
    {
        if (talkPanel == null) return;

        if (topBand == null)
        {
            var t = talkPanel.transform.Find("TopBand");
            if (t != null) topBand = t.GetComponent<RectTransform>();
        }
        if (bottomBand == null)
        {
            var b = talkPanel.transform.Find("BottomBand");
            if (b != null) bottomBand = b.GetComponent<RectTransform>();
        }
        if (continueButton == null && bottomBand != null)
        {
            var btn = bottomBand.transform.Find("ContinueButton");
            if (btn != null)
            {
                continueButton = btn.GetComponent<Button>();
                if (continueButton != null)
                {
                    continueButton.onClick.RemoveListener(OnContinueClicked);
                    continueButton.onClick.AddListener(OnContinueClicked);
                }
            }
        }

        if (continueButton != null)
        {
            var btnText = continueButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (btnText != null && btnText.text != null && btnText.text.Contains("\u25B6"))
            {
                btnText.text = btnText.text.Replace("\u25B6", ">");
            }
        }
    }

    public void ResetBandsOffscreen()
    {
        EnsureBandsBound();

        if (topBand != null)
        {
            topBand.sizeDelta = new Vector2(topBand.sizeDelta.x, topBandHeight);
            topBand.anchoredPosition = new Vector2(0f, topBandHeight);
        }
        if (bottomBand != null)
        {
            bottomBand.sizeDelta = new Vector2(bottomBand.sizeDelta.x, bottomBandHeight);
            bottomBand.anchoredPosition = new Vector2(0f, -bottomBandHeight);
        }
    }

    public IEnumerator SlideBandsRoutine(bool slideIn)
    {
        EnsureBandsBound();

        if (topBand == null || bottomBand == null)
        {
            if (!slideIn && talkPanel != null) talkPanel.SetActive(false);
            yield break;
        }

        float elapsed = 0f;
        Vector2 topStart = topBand.anchoredPosition;
        Vector2 topEnd = slideIn ? new Vector2(0f, 0f) : new Vector2(0f, topBandHeight);

        Vector2 bottomStart = bottomBand.anchoredPosition;
        Vector2 bottomEnd = slideIn ? new Vector2(0f, 0f) : new Vector2(0f, -bottomBandHeight);

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            topBand.anchoredPosition = Vector2.Lerp(topStart, topEnd, smoothT);
            bottomBand.anchoredPosition = Vector2.Lerp(bottomStart, bottomEnd, smoothT);
            yield return null;
        }

        topBand.anchoredPosition = topEnd;
        bottomBand.anchoredPosition = bottomEnd;

        if (!slideIn)
        {
            if (talkPanel != null) talkPanel.SetActive(false);
        }
        slideCoroutine = null;
    }


    // ============================================================
    // MAIN DIALOGUE
    // ============================================================

    public IEnumerator ShowDialogue(
        string[] lines,
        bool hasChoice,
        Color dialogueColor,
        TMP_FontAsset dialogueFont,
        AudioSource voiceSource = null,
        AudioClip[] voiceClips = null,
        System.Action<int, string> onLineStarted = null)
    {
        if (activeInteractableRoutine != null)
        {
            StopCoroutine(activeInteractableRoutine);
            activeInteractableRoutine = null;
            dialogueRunning = false;
        }

        if (dialogueRunning)
        {
            Debug.LogWarning(
                "NpcDialogueManager: A dialogue is already running."
            );

            yield break;
        }

        dialogueRunning = true;

        // --------------------------------------------------------
        // SAFETY
        // --------------------------------------------------------

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning(
                "NpcDialogueManager: Dialogue contains no lines."
            );

            dialogueRunning = false;
            yield break;
        }

        if (dialogueText == null)
        {
            Debug.LogError(
                "NpcDialogueManager: Dialogue Text is not assigned."
            );

            dialogueRunning = false;
            yield break;
        }

        currentDialogueColor = dialogueColor;

        if (dialogueFont != null)
            dialogueText.font = dialogueFont;
        else if (dialogueText.font == null || dialogueText.font.name.Contains("Liberation"))
        {
            var f = DefaultDialogueFont;
            if (f != null) dialogueText.font = f;
        }

        dialogueText.fontSize = STANDARD_FONT_SIZE;
        dialogueText.color = ResolveColorForLine(lines[0], dialogueColor);

        EnsureBandsBound();
        ResetBandsOffscreen();

        if (talkPanel != null)
            talkPanel.SetActive(true);

        if (choicePack != null)
            choicePack.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        choiceMade = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Slide the bands in: top slides down, bottom slides up
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideBandsRoutine(slideIn: true));


        // --------------------------------------------------------
        // PLAY EACH LINE
        // --------------------------------------------------------

        for (int i = 0; i < lines.Length; i++)
        {
            AudioClip clip = null;

            if (voiceClips != null &&
                i < voiceClips.Length)
            {
                clip = voiceClips[i];
            }

            string processedLine = lines[i];
            if (!string.IsNullOrEmpty(processedLine))
            {
                string tName = MetroStorySequenceController.TeddyName;
                if (!string.IsNullOrEmpty(tName))
                {
                    processedLine = processedLine.Replace("{TEDDY_NAME}", tName).Replace("[TEDDY NAME]", tName);
                }
            }

            onLineStarted?.Invoke(i, processedLine);
            OnDialogueLineStarted?.Invoke(i, processedLine);
            OnAnyDialogueLineStarted?.Invoke(i, processedLine);

            yield return StartCoroutine(
                PlayLine(
                    processedLine,
                    voiceSource,
                    clip
                )
            );
        }


        // --------------------------------------------------------
        // STOP VOICE
        // --------------------------------------------------------

        StopVoice(voiceSource);


        // --------------------------------------------------------
        // CHOICE
        // --------------------------------------------------------

        if (hasChoice)
        {
            yield return StartCoroutine(
                HandleChoices()
            );
        }


        // --------------------------------------------------------
        // CLOSE
        // --------------------------------------------------------

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (choicePack != null)
            choicePack.SetActive(false);

        dialogueText.text = "";

        // Slide the bands out: top slides up, bottom slides down
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        yield return StartCoroutine(SlideBandsRoutine(slideIn: false));

        if (talkPanel != null)
            talkPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (Application.isPlaying) LastDialogueEndFrame = Time.frameCount;
        dialogueRunning = false;
    }


    // ============================================================
    // BACKWARD COMPATIBILITY
    // ============================================================

    public IEnumerator ShowDialogue(
        string[] lines,
        bool hasChoice)
    {
        yield return StartCoroutine(
            ShowDialogue(
                lines,
                hasChoice,
                Color.white,
                null,
                null,
                null
            )
        );
    }


    // ============================================================
    // INTERACTABLE INSPECTION MESSAGES (CINEMATIC BORDERS)
    // ============================================================

    private Coroutine activeInteractableRoutine;

    /// <summary>
    /// Displays an interactable inspection message / monologue with cinematic letterbox borders.
    /// Dismisses automatically after autoDismissDuration seconds or immediately when the player taps / clicks / presses continue.
    /// </summary>
    public Coroutine ShowInteractableMessage(string message, float autoDismissDuration = 2.5f, Color? textColor = null)
    {
        if (string.IsNullOrEmpty(message)) return null;

        if (activeInteractableRoutine != null)
        {
            StopCoroutine(activeInteractableRoutine);
            activeInteractableRoutine = null;
        }
        else if (dialogueRunning)
        {
            return null;
        }

        activeInteractableRoutine = StartCoroutine(ShowInteractableMessageRoutine(message, autoDismissDuration, textColor ?? Color.white));
        return activeInteractableRoutine;
    }

    public void ForceEndDialogue()
    {
        StopAllCoroutines();
        activeInteractableRoutine = null;
        slideCoroutine = null;
        dialogueRunning = false;
        ResetBandsOffscreen();
        if (talkPanel != null) talkPanel.SetActive(false);
    }

    public IEnumerator ShowInteractableMessageRoutine(string message, float autoDismissDuration, Color textColor)
    {
        dialogueRunning = true;

        EnsureBandsBound();
        if (talkPanel == null || !talkPanel.activeSelf)
        {
            ResetBandsOffscreen();
        }

        if (talkPanel != null)
            talkPanel.SetActive(true);

        if (choicePack != null)
            choicePack.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        choiceMade = false;

        if (dialogueText != null)
        {
            dialogueText.fontSize = STANDARD_FONT_SIZE;
            dialogueText.color = textColor;
            if (dialogueText.font == null || dialogueText.font.name.Contains("Liberation"))
            {
                var f = DefaultDialogueFont;
                if (f != null) dialogueText.font = f;
            }
        }

        // Slide the cinematic bands in: top slides down, bottom slides up
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        yield return StartCoroutine(SlideBandsRoutine(slideIn: true));

        // Type out the message line
        yield return StartCoroutine(TypeText(message));

        // Wait for player tap / click / press E / continue button, OR auto-dismiss after autoDismissDuration
        float timer = 0f;
        continueClicked = false;
        while (timer < autoDismissDuration)
        {
            timer += Time.unscaledDeltaTime;

            bool advanceInput = continueClicked ||
                                Input.GetMouseButtonDown(0) ||
                                Input.GetKeyDown(KeyCode.Space) ||
                                Input.GetKeyDown(KeyCode.E) ||
                                (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) ||
                                ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame;

            // Give a 0.25s grace period so the triggering keypress/tap doesn't immediately dismiss
            if (timer > 0.25f && advanceInput)
            {
                continueClicked = false;
                break;
            }

            yield return null;
        }

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (dialogueText != null)
            dialogueText.text = "";

        // Slide the cinematic bands out: top slides up, bottom slides down
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        yield return StartCoroutine(SlideBandsRoutine(slideIn: false));

        if (talkPanel != null)
            talkPanel.SetActive(false);

        if (Application.isPlaying) LastDialogueEndFrame = Time.frameCount;
        dialogueRunning = false;
        activeInteractableRoutine = null;
    }


    // ============================================================
    // PLAY ONE LINE
    // ============================================================

    private IEnumerator PlayLine(
        string line,
        AudioSource voiceSource,
        AudioClip clip)
    {
        if (string.IsNullOrEmpty(line))
            line = "";


        // --------------------------------------------------------
        // STOP PREVIOUS AUDIO
        // --------------------------------------------------------

        StopVoice(voiceSource);


        // --------------------------------------------------------
        // PLAY VOICE
        // --------------------------------------------------------

        bool voicePlaying = false;

        if (voiceSource != null &&
            clip != null)
        {
            voiceSource.clip = clip;

            // Make sure volume isn't accidentally zero.
            if (voiceSource.volume <= 0f)
                voiceSource.volume = 1f;

            voiceSource.Play();

            // Unity updates isPlaying after Play().
            yield return null;

            voicePlaying = voiceSource.isPlaying;

            if (!voicePlaying)
            {
                Debug.LogWarning(
                    $"NpcDialogueManager: AudioSource failed to play " +
                    $"clip '{clip.name}'. Dialogue will continue normally."
                );
            }
        }


        // --------------------------------------------------------
        // COLOR & FONT SETUP FOR THIS LINE
        // --------------------------------------------------------

        if (dialogueText != null)
        {
            dialogueText.fontSize = STANDARD_FONT_SIZE;
            dialogueText.color = ResolveColorForLine(line, currentDialogueColor);
            if (dialogueText.font == null || dialogueText.font.name.Contains("Liberation"))
            {
                var f = DefaultDialogueFont;
                if (f != null) dialogueText.font = f;
            }
        }

        // --------------------------------------------------------
        // TYPE TEXT
        // --------------------------------------------------------

        yield return StartCoroutine(
            TypeText(line)
        );


        // --------------------------------------------------------
        // WAIT
        // --------------------------------------------------------

        yield return StartCoroutine(
            WaitForAdvance(
                voiceSource,
                voicePlaying
            )
        );


        // --------------------------------------------------------
        // CLEANUP
        // --------------------------------------------------------

        StopVoice(voiceSource);
    }


    // ============================================================
    // TYPE TEXT
    // ============================================================

    private IEnumerator TypeText(string line)
    {
        isTyping = true;
        skipTypingRequested = false;

        bool isArabic = LocalizationManager.ContainsArabic(line);
        string displayLine = isArabic ? LocalizationManager.FixRTL(line) : line;

        if (dialogueText != null)
        {
            dialogueText.alignment = isArabic ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        }

        if (charactersPerSecond <= 0f)
        {
            if (dialogueText != null)
            {
                dialogueText.text = displayLine;
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }
            isTyping = false;
            yield break;
        }

        if (dialogueText != null)
        {
            dialogueText.text = displayLine;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();
        }

        int totalChars = (dialogueText != null && dialogueText.textInfo != null) ? dialogueText.textInfo.characterCount : displayLine.Length;

        // Play typing audio
        if (typingSource != null && typingSound != null)
        {
            typingSource.clip = typingSound;
            typingSource.loop = true;
            typingSource.volume = 0.35f;
            typingSource.Play();
        }

        float delay = 1f / charactersPerSecond;

        for (int i = 1; i <= totalChars; i++)
        {
            // Allow skipping typewriter text on continue button, Space, E, Left Click, or Touch tap
            if (skipTypingRequested ||
                Input.GetMouseButtonDown(0) || 
                Input.GetKeyDown(KeyCode.Space) || 
                Input.GetKeyDown(KeyCode.E) ||
                (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                if (dialogueText != null) dialogueText.maxVisibleCharacters = int.MaxValue;
                skipTypingRequested = false;
                break;
            }

            if (dialogueText != null) dialogueText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(delay);
        }

        if (dialogueText != null) dialogueText.maxVisibleCharacters = int.MaxValue;

        // Stop typing audio when finished typing
        if (typingSource != null && typingSource.isPlaying)
        {
            typingSource.Stop();
        }

        isTyping = false;
    }


    // ============================================================
    // WAIT FOR NEXT LINE
    // ============================================================

    private IEnumerator WaitForAdvance(
        AudioSource voiceSource,
        bool voiceWasPlaying)
    {
        continueClicked = false;
        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        bool voiceFinished =
            !voiceWasPlaying ||
            voiceSource == null;

        while (true)
        {
            // If voice was playing, wait until it finishes.
            if (voiceWasPlaying &&
                voiceSource != null &&
                !voiceSource.isPlaying)
            {
                voiceFinished = true;
            }

            bool advanceInput = continueClicked ||
                                Input.GetMouseButtonDown(0) || 
                                Input.GetKeyDown(KeyCode.Space) || 
                                Input.GetKeyDown(KeyCode.E) ||
                                (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

            // Advance with continue button, left mouse click, Space, E, or Touch
            if (advanceInput)
            {
                continueClicked = false;
                StopVoice(voiceSource);
                yield break;
            }

            yield return null;
        }
    }


    // ============================================================
    // CHOICES
    // ============================================================

    public void ApplyMonochromeChoiceTheme()
    {
        if (choicePack == null) return;

        var packRect = choicePack.GetComponent<RectTransform>();
        if (packRect != null)
        {
            // Position above the 220px bottom cinematic letterbox band
            packRect.anchorMin = new Vector2(0.5f, 0f);
            packRect.anchorMax = new Vector2(0.5f, 0f);
            packRect.pivot = new Vector2(0.5f, 0.5f);
            packRect.anchoredPosition = new Vector2(0f, 275f);
            packRect.sizeDelta = new Vector2(560f, 56f);
        }

        var yesBtnTransform = choicePack.transform.Find("YesButton");
        var noBtnTransform = choicePack.transform.Find("NoButton");

        StyleChoiceButton(yesBtnTransform, -140f);
        StyleChoiceButton(noBtnTransform, 140f);
    }

    private void StyleChoiceButton(Transform btnTransform, float posX)
    {
        if (btnTransform == null) return;

        var btnRect = btnTransform.GetComponent<RectTransform>();
        if (btnRect != null)
        {
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(posX, 0f);
            btnRect.sizeDelta = new Vector2(240f, 48f);
        }

        var img = btnTransform.GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            img.color = new Color(0f, 0f, 0f, 1f);
        }

        var outline = btnTransform.GetComponent<UnityEngine.UI.Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        var btn = btnTransform.GetComponent<Button>();
        if (btn != null)
        {
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            cb.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
        }

        var label = btnTransform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            if (DefaultDialogueFont != null)
            {
                label.font = DefaultDialogueFont;
            }
            label.color = Color.white;
            label.fontSize = 22f;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    public void SetChoiceLabels(string optionA, string optionB)
    {
        if (choicePack == null) return;
        ApplyMonochromeChoiceTheme();

        var yesLabel = choicePack.transform.Find("YesButton/Label")?.GetComponent<TextMeshProUGUI>();
        if (yesLabel != null)
        {
            string cleanA = optionA.Replace("<size=70%><color=#8E9EAB>[1]</color></size>  ", "").Replace("[1]", "").Trim();
            string textA = Story.MobileUIAdapter.IsMobile ? cleanA.ToUpper() : $"[1]  {cleanA.ToUpper()}";
            yesLabel.text = LocalizationManager.FixRTL(textA);
            yesLabel.color = Color.white;
        }

        var noLabel = choicePack.transform.Find("NoButton/Label")?.GetComponent<TextMeshProUGUI>();
        if (noLabel != null)
        {
            string cleanB = optionB.Replace("<size=70%><color=#8E9EAB>[2]</color></size>  ", "").Replace("[2]", "").Trim();
            string textB = Story.MobileUIAdapter.IsMobile ? cleanB.ToUpper() : $"[2]  {cleanB.ToUpper()}";
            noLabel.text = LocalizationManager.FixRTL(textB);
            noLabel.color = Color.white;
        }
    }

    private IEnumerator HandleChoices()
    {
        if (choicePack == null)
            yield break;

        ApplyMonochromeChoiceTheme();

        choiceMade = false;

        // Hide continue button during choices
        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        // Ensure button click listeners are bound to respective indices
        var yesBtn = choicePack.transform.Find("YesButton")?.GetComponent<Button>();
        var noBtn = choicePack.transform.Find("NoButton")?.GetComponent<Button>();
        if (yesBtn != null)
        {
            yesBtn.onClick.RemoveAllListeners();
            yesBtn.onClick.AddListener(() => MakeChoice(0));
        }
        if (noBtn != null)
        {
            noBtn.onClick.RemoveAllListeners();
            noBtn.onClick.AddListener(() => MakeChoice(1));
        }

        var cg = choicePack.GetComponent<CanvasGroup>();
        if (cg == null) cg = choicePack.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        choicePack.SetActive(true);
        StartCoroutine(AnimateChoiceEntry(choicePack.transform, cg));

        // Unlock mouse cursor so the player can click choice buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        while (!choiceMade)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                MakeChoice(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                MakeChoice(1);

            yield return null;
        }

        if (cg != null)
        {
            float fadeOut = 0.15f;
            float elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(elapsed / fadeOut);
                yield return null;
            }
        }

        choicePack.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator AnimateChoiceEntry(Transform packTransform, CanvasGroup cg)
    {
        float duration = 0.22f;
        float elapsed = 0f;
        Vector3 startScale = new Vector3(0.96f, 0.96f, 1f);
        Vector3 endScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            if (cg != null) cg.alpha = smoothT;
            if (packTransform != null) packTransform.localScale = Vector3.Lerp(startScale, endScale, smoothT);
            yield return null;
        }

        if (cg != null) cg.alpha = 1f;
        if (packTransform != null) packTransform.localScale = endScale;
    }


    public void MakeChoice()
    {
        choiceMade = true;

        Debug.Log("Choice Selected!");
    }


    public void MakeChoice(int index)
    {
        lastChoiceIndex = index;
        choiceMade = true;

        Debug.Log(
            $"Choice Selected: {index}"
        );
    }


    // ============================================================
    // AUDIO
    // ============================================================

    private void StopVoice(AudioSource voiceSource)
    {
        if (typingSource != null && typingSource.isPlaying)
        {
            typingSource.Stop();
        }

        if (voiceSource == null)
            return;

        if (voiceSource.isPlaying)
            voiceSource.Stop();

        voiceSource.clip = null;
    }
}