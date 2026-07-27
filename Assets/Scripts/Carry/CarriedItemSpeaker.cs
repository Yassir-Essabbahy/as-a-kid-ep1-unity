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
        carryable = GetComponent<CarryableItem>();
    }

    void Update()
    {
        if (!triggerByKeyPress) return;
        if (!carryable.IsBeingCarried) return;
        if (conversation == null) return;

        if (Input.GetKeyDown(talkKey))
        {
            StartCoroutine(conversation.Play());
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