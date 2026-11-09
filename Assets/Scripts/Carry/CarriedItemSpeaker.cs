using UnityEngine;

[RequireComponent(typeof(CarryableItem))]
public class CarriedItemSpeaker : MonoBehaviour
{
    [Header("Dialogue")]
    public NpcConversation conversation; // reuse your existing NpcConversation on this object
    public bool triggerByKeyPress = false;
    public KeyCode talkKey = KeyCode.None;

    [Header("Trigger Zone Option")]
    public bool triggerByZone = false;
    public string playerTag = "Player";

    private CarryableItem carryable;
    private bool hasTalkedThisTrigger = false;

    void Awake()
    {
        if (carryable == null)
            carryable = GetComponent<CarryableItem>();
        if (conversation == null)
            conversation = GetComponent<NpcConversation>();
    }

    void Update()
    {
        // Key-press dialogue trigger disabled so pressing F does not trigger or repeat Teddy dialogue.
    }

    // Only relevant if triggerByZone is true and this object (or a child)
    // has a Collider marked "Is Trigger", separate from the carry collider
    void OnTriggerEnter(Collider other)
    {
        if (!triggerByZone) return;
        if (!carryable.IsBeingCarried) return;
        if (hasTalkedThisTrigger) return;
        if (!other.CompareTag(playerTag)) return;
        if (conversation == null) return;

        hasTalkedThisTrigger = true;
        StartCoroutine(conversation.Play());
    }
}
