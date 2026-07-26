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

    void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;
        if (conversation == null) return;

        // Safety: only fire if the required item is currently being carried
        if (requiredCarriedItem != null && !requiredCarriedItem.IsBeingCarried) return;

        hasTriggered = true;
        StartCoroutine(conversation.Play());
    }
}