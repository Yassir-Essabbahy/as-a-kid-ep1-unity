using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TMPro;

public class DialogueTrigger : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    public string[] lines;
    public string speakerName;
    public bool isMama = false;

    [Header("Choice")]
    public bool hasChoice = false;
    public int choiceLineIndex = 2;
    public string choiceA = "Ah";
    public string choiceB = "La";

    [Header("Typing Sound (Disabled - Visual Only)")]
    [HideInInspector] public AudioClip typingTrack;
    public AudioSource audioSource;
    [Range(0f, 1f)] public float typingVolume = 0.8f;

    [Header("Events")]
    public UnityEvent onDialogueCompleted;

    public string PromptMessage => "Talk to " + (string.IsNullOrEmpty(speakerName) ? gameObject.name : speakerName);

    public static bool isDialogueActive = false;
    private bool hasSpoken = false;
    private NPCLookAt lookAt;

    void Start()
    {
        lookAt = GetComponent<NPCLookAt>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void Interact()
    {
        Talk();
    }

    public void Talk()
    {
        if (isDialogueActive || hasSpoken)
            return;

        StartCoroutine(DialogueRoutine());
    }

    public void StartVoice()
    {
        // Typing sound intentionally disabled (visual typewriter only).
    }

    public void StopVoice()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    private IEnumerator DialogueRoutine()
    {
        hasSpoken = true;
        isDialogueActive = true;

        if (lookAt != null)
            lookAt.StartLooking();

        Color speakerColor = NpcDialogueManager.GetSpeakerColor(speakerName);
        TMP_FontAsset font = NpcDialogueManager.DefaultDialogueFont;

        AudioClip[] clips = null;

        if (NpcDialogueManager.Instance != null)
        {
            if (hasChoice)
            {
                NpcDialogueManager.Instance.SetChoiceLabels(choiceA, choiceB);
            }

            string[] resolvedLines = lines;
            if (lines != null && LocalizationManager.Instance != null)
            {
                resolvedLines = new string[lines.Length];
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i];
                    string loc = LocalizationManager.Instance.Get(l);
                    if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("["))
                    {
                        l = loc;
                    }
                    resolvedLines[i] = l;
                }
            }

            yield return NpcDialogueManager.Instance.ShowDialogue(
                resolvedLines,
                hasChoice,
                speakerColor,
                font,
                audioSource,
                clips
            );
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }

        if (lookAt != null)
            lookAt.StopLooking();

        isDialogueActive = false;

        if (isMama)
        {
            HouseObjectiveController.Instance?.OnMamaTalked();
        }

        onDialogueCompleted?.Invoke();
    }
}
