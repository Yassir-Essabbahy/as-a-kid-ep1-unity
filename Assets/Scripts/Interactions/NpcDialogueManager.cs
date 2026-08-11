using System.Collections;
using TMPro;
using UnityEngine;

public class NpcDialogueManager : MonoBehaviour
{
    public static NpcDialogueManager Instance;

    [Header("UI")]
    public GameObject talkPanel;
    public GameObject choicePack;
    public TextMeshProUGUI dialogueText;

    [Header("Choice")]
    public int lastChoiceIndex;

    private bool choiceMade = false;

    void Awake()
    {
        Instance = this;

        talkPanel.SetActive(false);
        choicePack.SetActive(false);
    }

    public IEnumerator ShowDialogue(
        string[] lines,
        bool hasChoice,
        Color dialogueColor,
        TMP_FontAsset dialogueFont,
        AudioSource voiceSource = null,
        AudioClip[] voiceClips = null)
    {
        dialogueText.color = dialogueColor;

        if (dialogueFont != null)
        {
            dialogueText.font = dialogueFont;
        }

        // Hide cursor during dialogue
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        talkPanel.SetActive(true);
        choicePack.SetActive(false);

        for (int i = 0; i < lines.Length; i++)
        {
            AudioClip clip =
                (voiceClips != null && i < voiceClips.Length)
                ? voiceClips[i]
                : null;

            // Type text + play voice
            yield return StartCoroutine(
                TypeLine(lines[i], voiceSource, clip)
            );

            // Wait until voice finishes AND player clicks
            yield return StartCoroutine(
                WaitForInput(voiceSource)
            );
        }

        // Stop voice just in case
        StopVoice(voiceSource);

        // Handle choice
        if (hasChoice)
        {
            yield return StartCoroutine(
                HandleChoices()
            );
        }

        choicePack.SetActive(false);
        talkPanel.SetActive(false);

        // Keep cursor hidden
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

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

    IEnumerator TypeLine(
        string line,
        AudioSource voiceSource,
        AudioClip clip)
    {
        dialogueText.text = "";

        // Stop previous voice
        StopVoice(voiceSource);

        // Start voice
        if (voiceSource != null && clip != null)
        {
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        // Type text
        foreach (char c in line)
        {
            dialogueText.text += c;

            yield return new WaitForSeconds(0.04f);
        }
    }

    IEnumerator WaitForInput(AudioSource voiceSource)
    {
        while (true)
        {
            // Check whether voice has finished
            bool voiceFinished =
                voiceSource == null ||
                !voiceSource.isPlaying;

            // Only accept a click once the voice has finished.
            // This prevents an early click (made while the voice
            // is still playing) from being "banked" and instantly
            // advancing the dialogue the moment the voice ends.
            if (voiceFinished && Input.GetMouseButtonDown(0))
            {
                yield break;
            }

            yield return null;
        }
    }

    void StopVoice(AudioSource voiceSource)
    {
        if (voiceSource != null && voiceSource.isPlaying)
        {
            voiceSource.Stop();
        }
    }

    IEnumerator HandleChoices()
    {
        // Keep cursor hidden
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        choicePack.SetActive(true);
        choiceMade = false;

        while (!choiceMade)
        {
            yield return null;
        }

        choicePack.SetActive(false);
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
    }
}