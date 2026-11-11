using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to the mobile on-screen interact button to forward taps.
/// </summary>
public class MobileInteractButtonForwarder : MonoBehaviour, IPointerDownHandler
{
    public InteractInput targetInput;

    private void Awake()
    {
        if (targetInput == null)
            targetInput = FindAnyObjectByType<InteractInput>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetInput == null)
            targetInput = FindAnyObjectByType<InteractInput>();

        if (targetInput != null)
        {
            targetInput.TriggerInteract();
        }
    }
}
