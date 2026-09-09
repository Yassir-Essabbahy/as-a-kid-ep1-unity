using UnityEngine;

[RequireComponent(typeof(CarryableItem))]
public class CarriedItemSpeaker : MonoBehaviour
{
    [Header("Dialogue")]
    public NpcConversation conversation; // reuse your existing NpcConversation on this object
    public bool triggerByKeyPress = true;
    public KeyCode talkKey = KeyCode.F;

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
        if (!triggerByKeyPress) return;

        if (Input.GetKeyDown(talkKey))
        {
            if (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.TryHandleTeddyInteraction())
            {
                return;
            }

            if (carryable != null && !carryable.IsBeingCarried) return;

            if (conversation == null)
                conversation = GetComponent<NpcConversation>();

            if (conversation != null)
            {
                StartCoroutine(conversation.Play());
            }
        }
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