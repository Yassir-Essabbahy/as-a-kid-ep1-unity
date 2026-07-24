using System.Collections;
using TMPro;
using UnityEngine;

public class NpcConversation : MonoBehaviour
{

    [Header("Carry After Dialogue")]
public bool becomesCarryableAfterDialogue = false;
public Transform playerCarryPoint; // assign the player's hold point here

    public string[] lineKeys;
    public bool needsChoiceAtEnd;
    public Color dialogueColor = Color.white;
    public TMP_FontAsset dialogueFont;

    public IEnumerator Play()
    {
        string[] resolvedLines = new string[lineKeys.Length];
        for (int i = 0; i < lineKeys.Length; i++)
        {
            resolvedLines[i] = LocalizationManager.Instance.Get(lineKeys[i]);
        }

        yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
            resolvedLines,
            needsChoiceAtEnd,
            dialogueColor,
            dialogueFont));
            if (becomesCarryableAfterDialogue)
{
    CarryableItem carryable = GetComponent<CarryableItem>();
    if (carryable != null && playerCarryPoint != null)
    {
        carryable.StartCarrying(playerCarryPoint);
    }
}
    }
    
}