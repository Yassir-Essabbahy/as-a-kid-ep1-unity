using System.Collections;
using TMPro;
using UnityEngine;

public class NpcDialogueManager : MonoBehaviour
{
    public static NpcDialogueManager Instance;

    public GameObject talkPanel;   // The main dialogue box
    public GameObject choicePack;  // The container for buttons
    public TextMeshProUGUI dialogueText;
    public int lastChoiceIndex;

    private bool choiceMade = false;

    void Awake()
    {
        Instance = this;
        talkPanel.SetActive(false);
        choicePack.SetActive(false);
    }

    public IEnumerator ShowDialogue(string[] lines, bool hasChoice, Color dialogueColor, TMP_FontAsset dialogueFont,
        AudioSource voiceSource = null, AudioClip[] voiceClips = null)
    {
        dialogueText.color = dialogueColor;
        if (dialogueFont != null)
        {
            dialogueText.font = dialogueFont;
        }

        talkPanel.SetActive(true);
        choicePack.SetActive(false);

        for (int i = 0; i < lines.Length; i++)
        {
            AudioClip clip = (voiceClips != null && i < voiceClips.Length) ? voiceClips[i] : null;

            yield return StartCoroutine(TypeLine(lines[i], voiceSource, clip));
            yield return WaitForInput(voiceSource);
        }

        if (hasChoice)
        {
            yield return StartCoroutine(HandleChoices());
        }

        choicePack.SetActive(false);
        talkPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public IEnumerator ShowDialogue(string[] lines, bool hasChoice)
    {
        yield return StartCoroutine(ShowDialogue(lines, hasChoice, Color.white, null, null, null));
    }

    IEnumerator TypeLine(string line, AudioSource voiceSource, AudioClip clip)
    {
        dialogueText.text = "";

        if (voiceSource != null && clip != null)
        {
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(0.04f);
        }
    }

    // Waits for BOTH the player click AND the voice clip to finish before advancing
    IEnumerator WaitForInput(AudioSource voiceSource)
    {
        bool clicked = false;

        while (true)
        {
            if (Input.GetMouseButtonDown(0))
            {
                clicked = true;
            }

            bool voiceFinished = voiceSource == null || !voiceSource.isPlaying;

            if (clicked && voiceFinished)
            {
                break;
            }

            yield return null;
        }
    }

    IEnumerator HandleChoices()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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