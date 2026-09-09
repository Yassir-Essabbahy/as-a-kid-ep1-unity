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
    private Coroutine slideCoroutine;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

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

    public System.Collections.IEnumerator SlideBandsRoutine(bool slideIn)
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

        currentIndex++;
        if (currentIndex >= currentSequence.lines.Count)
        {
            EndSequence();
            return;
        }

        DialogueLine line = currentSequence.lines[currentIndex];

        speakerText.text = line.speakerName;
bodyText.text = LocalizationManager.Instance.Get(line.key);

        if (line.voiceClip != null)
        {
            voiceSource.clip = line.voiceClip;
            voiceSource.Play();
        }

        // This is the whole trick: broadcast the id, don't know or care who's listening.
        Debug.Log($"[Dialogue] Line shown, eventId = '{line.eventId}'");
        DialogueEvents.RaiseLineShown();
        DialogueEvents.RaiseLineEvent(line.eventId);
        
    }

    private void EndSequence()
    {
        isPlaying = false;
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
