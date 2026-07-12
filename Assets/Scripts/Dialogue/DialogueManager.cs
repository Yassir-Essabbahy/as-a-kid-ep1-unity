using UnityEngine;
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

    [SerializeField] private GameObject dialogueBox;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private AudioSource voiceSource;

    private DialogueSequence currentSequence;
    private int currentIndex;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void PlaySequence(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines.Count == 0) return;

        currentSequence = sequence;
        currentIndex = -1;
        isPlaying = true;
        dialogueBox.SetActive(true);
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
        dialogueBox.SetActive(false);
        string finishedId = currentSequence.sequenceId;
        currentSequence = null;
        DialogueEvents.RaiseSequenceComplete(finishedId);
    }
}
