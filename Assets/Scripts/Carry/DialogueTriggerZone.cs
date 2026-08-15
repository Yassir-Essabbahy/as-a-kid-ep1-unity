using System.Collections;
using UnityEngine;

public class DialogueTriggerZone : MonoBehaviour
{
    [Header("Dialogue Source")]
    public NpcConversation conversation;

    [Header("Safety Check")]
    public CarryableItem requiredCarriedItem; // e.g. the teddy bear's CarryableItem

    [Header("Trigger Settings")]
    public string playerTag = "Player";
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        StartCoroutine(TriggerIfReady());
    }

    /// <summary>
    /// Allows a scripted sequence to fire this exact trigger without moving
    /// the player through its collider.
    /// </summary>
    public IEnumerator TriggerIfReady()
    {
        if (triggerOnce && hasTriggered)
            yield break;

        if (conversation == null)
            yield break;

        if (requiredCarriedItem != null && !requiredCarriedItem.IsBeingCarried)
            yield break;

        hasTriggered = true;
        yield return StartCoroutine(conversation.Play());
    }
}