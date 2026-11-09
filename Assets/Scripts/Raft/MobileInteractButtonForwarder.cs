using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to the mobile on-screen interact button to forward taps.
/// </summary>
public class MobileInteractButtonForwarder : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
{
    public InteractInput targetInput;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetInput != null)
        {
            targetInput.TriggerInteract();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetInput != null)
        {
            targetInput.TriggerInteract();
        }
    }
}
