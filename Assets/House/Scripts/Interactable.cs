using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour, IInteractable
{
    public string promptMessage = "اضغط E";
    public string PromptMessage => promptMessage;

    // drag any function here in Inspector
    public UnityEvent onInteract;

    public void Interact()
    {
        onInteract?.Invoke();
    }
}
