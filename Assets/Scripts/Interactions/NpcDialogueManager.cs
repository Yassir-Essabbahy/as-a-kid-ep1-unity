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

    [Header("Typing")]
    [Min(0f)]
    public float charactersPerSecond = 25f;

    [Header("Choice")]
    public int lastChoiceIndex;

    private bool choiceMade;
    private bool dialogueRunning;

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

        if (talkPanel != null)
            talkPanel.SetActive(false);

        if (choicePack != null)
            choicePack.SetActive(false);
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

        if (talkPanel != null)
            talkPanel.SetActive(true);

        if (choicePack != null)
            choicePack.SetActive(false);

        choiceMade = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;


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

            yield return StartCoroutine(
                PlayLine(
                    lines[i],
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

        if (choicePack != null)
            choicePack.SetActive(false);

        if (talkPanel != null)
            talkPanel.SetActive(false);

        dialogueText.text = "";

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
        dialogueText.text = "";

        if (charactersPerSecond <= 0f)
        {
            dialogueText.text = line;
            yield break;
        }

        float delay = 1f / charactersPerSecond;

        foreach (char c in line)
        {
            dialogueText.text += c;

            yield return new WaitForSeconds(delay);
        }
    }


    // ============================================================
    // WAIT FOR NEXT LINE
    // ============================================================

    private IEnumerator WaitForAdvance(
        AudioSource voiceSource,
        bool voiceWasPlaying)
    {
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

            // Advance with left mouse click, Space, or E once voice has finished (or if text only).
            if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)) &&
                voiceFinished)
            {
                yield break;
            }

            yield return null;
        }
    }


    // ============================================================
    // CHOICES
    // ============================================================

    private IEnumerator HandleChoices()
    {
        if (choicePack == null)
            yield break;

        choiceMade = false;

        choicePack.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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