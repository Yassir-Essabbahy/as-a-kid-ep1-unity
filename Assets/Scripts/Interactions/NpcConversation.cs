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

    [Header("Voice")]
    public AudioSource voiceSource;   // assign a shared dialogue AudioSource, or one per NPC
    public AudioClip[] voiceClips;    // same length/order as lineKeys, leave entries null for silent lines

    [Header("Carry After Dialogue")]
    public bool becomesCarryableAfterDialogue = false;
    public Transform playerCarryPoint;

    public event Action OnConversationFinished;

    public IEnumerator Play()
    {
        string[] resolvedLines = new string[lineKeys.Length];
        for (int i = 0; i < lineKeys.Length; i++)
        {
            resolvedLines[i] = LocalizationManager.Instance.Get(lineKeys[i]);
        }

        yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
            resolvedLines, needsChoiceAtEnd, dialogueColor, dialogueFont, voiceSource, voiceClips));

        if (becomesCarryableAfterDialogue)
        {
            CarryableItem carryable = GetComponent<CarryableItem>();
            if (carryable != null && playerCarryPoint != null)
            {
                carryable.StartCarrying(playerCarryPoint);
            }
        }

        OnConversationFinished?.Invoke();
    }
}