using System.Collections;
using UnityEngine;

public class NpcConversation : MonoBehaviour
{
    public string[] lineKeys;
    public bool needsChoiceAtEnd;

    public IEnumerator Play()
    {
        string[] resolvedLines = new string[lineKeys.Length];
        for (int i = 0; i < lineKeys.Length; i++)
        {
            resolvedLines[i] = LocalizationManager.Instance.Get(lineKeys[i]);
        }

        yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(resolvedLines, needsChoiceAtEnd));
    }
}