using UnityEngine;

public class MessageTrigger : MonoBehaviour, IInteractable
{
    [Header("Message Settings")]
    public string message = "Hello!";
    public float duration = 2f;

    public string PromptMessage => LocalizationManager.Resolve("prompt_tap_interact", "Tap to interact");

    public void Interact()
    {
        SendMessage();
    }

    public void SendMessage()
    {
        HouseUIBridge.ShowMessage(message, duration);
    }
}
