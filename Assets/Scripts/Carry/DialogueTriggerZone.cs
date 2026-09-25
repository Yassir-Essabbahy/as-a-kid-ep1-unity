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

    [Header("Requirements & Spawning")]
    public bool requirePowerRestored = false;
    public GameObject itemToSpawn;

    private bool hasTriggered = false;
    private bool isExecuting = false;

    public bool HasTriggered => hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;

        StartCoroutine(TriggerIfReady());
    }

    private void OnTriggerStay(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if (isExecuting) return;
        if (!other.CompareTag(playerTag)) return;

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

        if (isExecuting)
            yield break;

        if (requirePowerRestored)
        {
            bool powerReady = false;
            if (MetroStorySequenceController.Instance != null)
            {
                powerReady = MetroStorySequenceController.Instance.isPowerRestored;
            }
            else
            {
                var eb = FindAnyObjectByType<ElevatorButton>();
                if (eb != null) powerReady = eb.isPowered;
            }

            if (!powerReady)
                yield break;
        }

        if (conversation == null)
            yield break;

        if (requiredCarriedItem != null && !requiredCarriedItem.IsBeingCarried)
            yield break;

        if (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning)
            yield break;

        isExecuting = true;
        hasTriggered = true;

        if (itemToSpawn != null)
        {
            itemToSpawn.SetActive(true);
            Debug.Log($"[DialogueTriggerZone] Spawned item: {itemToSpawn.name}");
        }

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.OnElevatorTicketTriggered();
        }

        yield return StartCoroutine(conversation.Play());
        isExecuting = false;
    }
}