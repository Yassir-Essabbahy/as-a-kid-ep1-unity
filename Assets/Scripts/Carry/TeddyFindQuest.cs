using UnityEngine;

public class TeddyFindQuest : MonoBehaviour
{
    [Header("Quest Conversations")]
    public NpcConversation askConversation;   // the existing "go find X" NpcConversation
    public NpcConversation foundConversation; // the existing "you found it" NpcConversation

    [Header("Quest Item")]
    public GameObject itemToFind;             // Item to spawn/activate when quest starts
    public bool questActive = false;
    public bool questCompleted = false;

    [Header("Trash Minigame Transition")]
    [Tooltip("If true, the world pickup is suppressed in favor of the trash can minigame.")]
    public bool useTrashMinigame = true;

    void Awake()
    {
        if (itemToFind != null && !questActive && !questCompleted)
        {
            itemToFind.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (askConversation != null)
            askConversation.OnConversationFinished += HandleAskFinished;
    }

    void OnDisable()
    {
        if (askConversation != null)
            askConversation.OnConversationFinished -= HandleAskFinished;
    }

    void HandleAskFinished()
    {
        questActive = true;
        if (!useTrashMinigame && itemToFind != null)
        {
            itemToFind.SetActive(true);
        }
    }

    public void CompleteQuest()
    {
        if (questCompleted) return;
        questActive = false;
        questCompleted = true;
        if (foundConversation != null)
        {
            StartCoroutine(foundConversation.Play());
        }
    }
}
