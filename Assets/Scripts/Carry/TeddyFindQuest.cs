using UnityEngine;

public class TeddyFindQuest : MonoBehaviour
{
    public NpcConversation askConversation;   // the existing "go find X" NpcConversation
    public NpcConversation foundConversation; // the existing "you found it" NpcConversation
    public bool questActive = false;
    public bool questCompleted = false;

    void OnEnable()
    {
        askConversation.OnConversationFinished += HandleAskFinished;
    }

    void OnDisable()
    {
        askConversation.OnConversationFinished -= HandleAskFinished;
    }

    void HandleAskFinished()
    {
        questActive = true;
    }

    public void CompleteQuest()
    {
        if (!questActive || questCompleted) return;
        questActive = false;
        questCompleted = true;
        StartCoroutine(foundConversation.Play());
    }
}