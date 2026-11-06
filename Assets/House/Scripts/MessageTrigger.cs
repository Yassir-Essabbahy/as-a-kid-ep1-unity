using UnityEngine;

public class MessageTrigger : MonoBehaviour, IInteractable
{
    [Header("Message Settings")]
    public string message = "Hello!";
    public float duration = 2f;

    public string PromptMessage => "Interact";

    public void Interact()
    {
        SendMessage();
    }

    public void SendMessage()
    {
        HouseUIBridge.ShowMessage(message, duration);
    }
}
