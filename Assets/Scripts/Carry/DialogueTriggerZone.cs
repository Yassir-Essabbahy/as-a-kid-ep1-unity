using UnityEngine;

public class DialogueTriggerZone : MonoBehaviour
{
    [Header("Dialogue Source")]
    public NpcConversation conversation; // the bear's NpcConversation, or any NPC's

    [Header("Trigger Settings")]
    public string playerTag = "Player";
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;
        if (conversation == null) return;

        hasTriggered = true;
        StartCoroutine(conversation.Play());
    }
}