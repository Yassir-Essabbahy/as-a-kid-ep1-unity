using System;
using System.Collections;
using UnityEngine;

public class NpcConversation : MonoBehaviour
{
    public string[] lineKeys;
    public bool needsChoiceAtEnd;

    [Header("Presentation")]
    public Color dialogueColor = Color.white;
    public TMPro.TMP_FontAsset dialogueFont;

    [Header("Sound")]
    public AudioSource dialogueAudioSource;
    public AudioClip typingSound;
    public AudioClip finishSound;

    [Header("Carry After Dialogue")]
    public bool becomesCarryableAfterDialogue = false;
    public Transform playerCarryPoint;

    // NEW — optional, only used where you actually need it
    public event Action OnConversationFinished;

    public IEnumerator Play()
    {
        string[] resolvedLines = new string[lineKeys.Length];
        for (int i = 0; i < lineKeys.Length; i++)
        {
            resolvedLines[i] = LocalizationManager.Instance.Get(lineKeys[i]);
        }

        yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
            resolvedLines, needsChoiceAtEnd, dialogueColor, dialogueFont,
            dialogueAudioSource, typingSound));

        if (becomesCarryableAfterDialogue)
        {
            CarryableItem carryable = GetComponent<CarryableItem>();
            if (carryable != null && playerCarryPoint != null)
            {
                carryable.StartCarrying(playerCarryPoint);
            }
        }

        if (dialogueAudioSource != null && finishSound != null)
            dialogueAudioSource.PlayOneShot(finishSound);

        OnConversationFinished?.Invoke();
    }
}