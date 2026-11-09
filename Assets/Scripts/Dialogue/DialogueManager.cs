using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One instance for the whole game (put it in a persistent scene / DontDestroyOnLoad,
/// or re-hook it per scene if you prefer - either works with this design).
/// Call PlaySequence() to start any conversation. Call Advance() from your
/// existing "press to continue" input - same button you already use everywhere.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI")]
    public GameObject dialogueBox;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public AudioSource voiceSource;

    [Header("Typing Effect")]
    [Min(0f)]
    public float charactersPerSecond = 25f;
    public AudioClip typingSound;
    public AudioSource typingSource;

    [Header("Cinematic Letterbox Bands")]
    public RectTransform topBand;
    public RectTransform bottomBand;
    public float topBandHeight = 140f;
    public float bottomBandHeight = 220f;
    public float slideDuration = 0.45f;

    [Header("Buttons")]
    public Button continueButton;

    private DialogueSequence currentSequence;
    private int currentIndex;
    private bool isPlaying;
    private bool isTyping;
    private bool skipTypingRequested;
    private Coroutine slideCoroutine;
    private Coroutine typeCoroutine;

    public bool IsPlaying => isPlaying;
    public bool IsTyping => isTyping;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureTypingAudio();

        if (dialogueBox != null)
        {
            if (topBand == null)
            {
                var t = dialogueBox.transform.Find("TopBand");
                if (t != null) topBand = t.GetComponent<RectTransform>();
            }
            if (bottomBand == null)
            {
                var b = dialogueBox.transform.Find("BottomBand");
                if (b != null) bottomBand = b.GetComponent<RectTransform>();
            }
            if (continueButton == null)
            {
                var btn = dialogueBox.transform.Find("BottomBand/ContinueButton");
                if (btn == null) btn = dialogueBox.transform.Find("ContinueButton");
                if (btn != null) continueButton = btn.GetComponent<Button>();
            }
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Advance);
            continueButton.onClick.AddListener(Advance);
        }

        ResetBandsOffscreen();
    }

    private void EnsureTypingAudio()
    {
        if (typingSource == null)
        {
            var sources = GetComponents<AudioSource>();
            foreach (var s in sources)
            {
                if (s != voiceSource)
                {
                    typingSource = s;
                    break;
                }
            }
            if (typingSource == null)
            {
                typingSource = gameObject.AddComponent<AudioSource>();
            }
            typingSource.playOnAwake = false;
            typingSource.loop = true;
            typingSource.spatialBlend = 0f;
            typingSource.volume = 0.35f;
        }

        if (typingSound == null)
        {
            typingSound = NpcDialogueManager.DefaultTypingSound;
        }
    }

    public void ResetBandsOffscreen()
    {
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
        if (topBand == null || bottomBand == null)
        {
            if (!slideIn && dialogueBox != null) dialogueBox.SetActive(false);
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
            if (dialogueBox != null) dialogueBox.SetActive(false);
        }
        slideCoroutine = null;
    }

    public void PlaySequence(DialogueSequence sequence, bool showFade = true)
    {
        if (sequence == null || sequence.lines.Count == 0) return;

        currentSequence = sequence;
        currentIndex = -1;
        isPlaying = true;
        dialogueBox.SetActive(true);
        if (continueButton != null) continueButton.gameObject.SetActive(true);
        ResetBandsOffscreen();
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideBandsRoutine(slideIn: true));

        if (showFade)
            DialogueEvents.RaiseSequenceStart(sequence.sequenceId);
        Advance();
    }

    /// Call this from your existing dialogue-advance input.
    public void Advance()
    {
        if (!isPlaying) return;

        if (isTyping)
        {
            skipTypingRequested = true;
            return;
        }

        currentIndex++;
        if (currentIndex >= currentSequence.lines.Count)
        {
            EndSequence();
            return;
        }

        DialogueLine line = currentSequence.lines[currentIndex];

        Color speakerColor = NpcDialogueManager.GetSpeakerColor(line.speakerName);
        var font = NpcDialogueManager.DefaultDialogueFont;

        if (speakerText != null)
        {
            speakerText.text = line.speakerName;
            speakerText.color = speakerColor;
            speakerText.fontSize = NpcDialogueManager.STANDARD_FONT_SIZE;
            if (font != null && (speakerText.font == null || !speakerText.font.name.Contains("GeistPixel")))
            {
                speakerText.font = font;
            }
        }

        if (bodyText != null)
        {
            bodyText.color = speakerColor;
            bodyText.fontSize = NpcDialogueManager.STANDARD_FONT_SIZE;
            if (font != null && (bodyText.font == null || !bodyText.font.name.Contains("GeistPixel")))
            {
                bodyText.font = font;
            }
            string rawLine = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(line.key) : line.key;
            if (typeCoroutine != null) StopCoroutine(typeCoroutine);
            typeCoroutine = StartCoroutine(TypeBodyTextRoutine(rawLine));
        }

        if (voiceSource != null)
        {
            voiceSource.Stop();
            voiceSource.clip = line.voiceClip;
            if (line.voiceClip != null) voiceSource.Play();
        }

        // This is the whole trick: broadcast the id, don't know or care who's listening.
        Debug.Log($"[Dialogue] Line shown, eventId = '{line.eventId}'");
        DialogueEvents.RaiseLineShown();
        DialogueEvents.RaiseLineEvent(line.eventId);
    }

    public Coroutine TypeCustomLine(string text, Color? color = null)
    {
        if (bodyText == null) return null;

        if (color.HasValue)
            bodyText.color = color.Value;

        var font = NpcDialogueManager.DefaultDialogueFont;
        bodyText.fontSize = NpcDialogueManager.STANDARD_FONT_SIZE;
        if (font != null && (bodyText.font == null || !bodyText.font.name.Contains("GeistPixel")))
        {
            bodyText.font = font;
        }

        if (typeCoroutine != null) StopCoroutine(typeCoroutine);
        typeCoroutine = StartCoroutine(TypeBodyTextRoutine(text));
        return typeCoroutine;
    }

    private IEnumerator TypeBodyTextRoutine(string line)
    {
        EnsureTypingAudio();
        isTyping = true;
        skipTypingRequested = false;

        if (string.IsNullOrEmpty(line)) line = "";

        bool isArabic = LocalizationManager.ContainsArabic(line);
        string displayLine = isArabic ? LocalizationManager.FixRTL(line) : line;

        if (bodyText != null)
        {
            bodyText.alignment = isArabic ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        }

        if (charactersPerSecond <= 0f || !Application.isPlaying)
        {
            if (bodyText != null)
            {
                bodyText.text = displayLine;
                bodyText.maxVisibleCharacters = int.MaxValue;
            }
            isTyping = false;
            typeCoroutine = null;
            yield break;
        }

        if (bodyText != null)
        {
            bodyText.text = displayLine;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
        }

        int totalChars = (bodyText != null && bodyText.textInfo != null && bodyText.textInfo.characterCount > 0)
            ? bodyText.textInfo.characterCount
            : displayLine.Length;

        if (typingSource != null && typingSound != null)
        {
            typingSource.clip = typingSound;
            typingSource.loop = true;
            typingSource.volume = 0.35f;
            typingSource.Play();
        }

        float delay = 1f / charactersPerSecond;
        int startFrame = Time.frameCount;

        for (int i = 1; i <= totalChars; i++)
        {
            if (Time.frameCount > startFrame &&
                (skipTypingRequested ||
                 Input.GetMouseButtonDown(0) ||
                 Input.GetKeyDown(KeyCode.Space) ||
                 Input.GetKeyDown(KeyCode.E) ||
                 (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
            {
                if (bodyText != null) bodyText.maxVisibleCharacters = int.MaxValue;
                skipTypingRequested = false;
                break;
            }

            if (bodyText != null) bodyText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(delay);
        }

        if (bodyText != null) bodyText.maxVisibleCharacters = int.MaxValue;

        if (typingSource != null && typingSource.isPlaying)
        {
            typingSource.Stop();
        }

        isTyping = false;
        typeCoroutine = null;
    }

    private void EndSequence()
    {
        isPlaying = false;
        isTyping = false;
        if (typeCoroutine != null)
        {
            StopCoroutine(typeCoroutine);
            typeCoroutine = null;
        }
        if (typingSource != null && typingSource.isPlaying)
        {
            typingSource.Stop();
        }
        if (voiceSource != null) voiceSource.Stop();
        if (continueButton != null) continueButton.gameObject.SetActive(false);
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        if (topBand != null && bottomBand != null)
        {
            StartCoroutine(SlideBandsRoutine(slideIn: false));
        }
        else
        {
            if (dialogueBox != null) dialogueBox.SetActive(false);
        }

        string finishedId = currentSequence.sequenceId;
        currentSequence = null;
        DialogueEvents.RaiseSequenceComplete(finishedId);
    }
}
