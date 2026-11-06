using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerName;

    [Header("Message UI")]
    public GameObject messagePanel;
    public TextMeshProUGUI messageText;

    [Header("Fade UI")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Choice Panel UI")]
    public GameObject choicePanel;
    public Button[] choiceButtons;
    public TextMeshProUGUI[] choiceButtonTexts;

    [Header("Player")]
    public MonoBehaviour playerLookScript;

    [Header("Typewriter")]
    public float typewriterSpeed = 0.04f;

    private Action<int> onChoiceSelected;
    private Coroutine typewriterCoroutine;
    private DialogueTrigger currentTrigger;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (dialoguePanel)
            dialoguePanel.SetActive(false);

        if (messagePanel)
            messagePanel.SetActive(false);

        if (choicePanel)
            choicePanel.SetActive(false);

        if (fadeCanvasGroup)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.gameObject.SetActive(true);
        }
    }

    // ═══════════════════════════════
    // DIALOGUE
    // ═══════════════════════════════

    public void ShowDialogue(string line, string speaker, DialogueTrigger trigger = null)
    {
        currentTrigger = trigger;

        dialoguePanel.SetActive(true);

        if (speakerName != null)
            speakerName.text = speaker;

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        typewriterCoroutine = StartCoroutine(TypewriterRoutine(line));
    }

    private IEnumerator TypewriterRoutine(string line)
    {
        dialogueText.text = "";

        currentTrigger?.StartVoice();

        foreach (char c in line)
        {
            dialogueText.text += c;

            yield return new WaitForSeconds(typewriterSpeed);
        }

        currentTrigger?.StopVoice();

        typewriterCoroutine = null;
    }

    public void HideDialogue()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        currentTrigger?.StopVoice();

        dialoguePanel.SetActive(false);
    }

    // ═══════════════════════════════
    // MESSAGE
    // ═══════════════════════════════

    public void ShowMessage(string message, float duration = 2f)
    {
        StopCoroutine(nameof(ShowMessageRoutine));

        StartCoroutine(ShowMessageRoutine(message, duration));
    }

    private IEnumerator ShowMessageRoutine(string message, float duration)
    {
        messagePanel.SetActive(true);

        messageText.text = message;

        yield return new WaitForSeconds(duration);

        messagePanel.SetActive(false);
    }

    // ═══════════════════════════════
    // FADE
    // ═══════════════════════════════

    public Coroutine FadeOut(float duration)
    {
        return StartCoroutine(FadeRoutine(0f, 1f, duration));
    }

    public Coroutine FadeIn(float duration)
    {
        return StartCoroutine(FadeRoutine(1f, 0f, duration));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;

        fadeCanvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            fadeCanvasGroup.alpha = Mathf.Lerp(
                from,
                to,
                elapsed / duration
            );

            yield return null;
        }

        fadeCanvasGroup.alpha = to;

        fadeCanvasGroup.blocksRaycasts = (to >= 1f);
    }

    // ═══════════════════════════════
    // CHOICE PANEL
    // ═══════════════════════════════

    public void ShowChoicePanel(string[] options, Action<int> callback)
    {
        onChoiceSelected = callback;

        choicePanel.SetActive(true);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (i < options.Length)
            {
                choiceButtons[i].gameObject.SetActive(true);

                choiceButtonTexts[i].text = options[i];

                int index = i;

                choiceButtons[i].onClick.RemoveAllListeners();

                choiceButtons[i].onClick.AddListener(() =>
                    OnChoiceButtonClicked(index)
                );
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerLookScript)
            playerLookScript.enabled = false;
    }

    public void HideChoicePanel()
    {
        choicePanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerLookScript)
            playerLookScript.enabled = true;
    }

    private void OnChoiceButtonClicked(int index)
    {
        onChoiceSelected?.Invoke(index);
    }
}