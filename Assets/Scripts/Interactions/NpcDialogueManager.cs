using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcDialogueManager : MonoBehaviour
{
    public static NpcDialogueManager Instance;

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

    [Header("Choice")]
    public int lastChoiceIndex;

    private bool choiceMade;
    private bool dialogueRunning;
    private Coroutine slideCoroutine;

    private bool continueClicked;
    private bool skipTypingRequested;
    private bool isTyping;

    public bool IsDialogueRunning => dialogueRunning;


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
        AudioClip[] voiceClips = null)
    {
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

        // --------------------------------------------------------
        // UI SETUP
        // --------------------------------------------------------

        dialogueText.color = dialogueColor;

        if (dialogueFont != null)
            dialogueText.font = dialogueFont;

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
        dialogueText.text = "";

        if (charactersPerSecond <= 0f)
        {
            dialogueText.text = line;
            isTyping = false;
            yield break;
        }

        float delay = 1f / charactersPerSecond;

        for (int i = 0; i < line.Length; i++)
        {
            // Allow skipping typewriter text on continue button, Space, E, Left Click, or Touch tap
            if (skipTypingRequested ||
                Input.GetMouseButtonDown(0) || 
                Input.GetKeyDown(KeyCode.Space) || 
                Input.GetKeyDown(KeyCode.E) ||
                (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                dialogueText.text = line;
                skipTypingRequested = false;
                isTyping = false;
                yield break;
            }

            dialogueText.text += line[i];
            yield return new WaitForSeconds(delay);
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

            // Advance with continue button, left mouse click, Space, E, or Touch once voice has finished
            if (advanceInput && voiceFinished)
            {
                continueClicked = false;
                yield break;
            }

            yield return null;
        }
    }


    // ============================================================
    // CHOICES
    // ============================================================

    public void SetChoiceLabels(string optionA, string optionB)
    {
        if (choicePack == null) return;
        var yesLabel = choicePack.transform.Find("YesButton/Label")?.GetComponent<TextMeshProUGUI>();
        if (yesLabel != null) yesLabel.text = optionA;

        var noLabel = choicePack.transform.Find("NoButton/Label")?.GetComponent<TextMeshProUGUI>();
        if (noLabel != null) noLabel.text = optionB;
    }

    private IEnumerator HandleChoices()
    {
        if (choicePack == null)
            yield break;

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

        choicePack.SetActive(true);

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

        choicePack.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        if (voiceSource == null)
            return;

        if (voiceSource.isPlaying)
            voiceSource.Stop();

        voiceSource.clip = null;
    }
}