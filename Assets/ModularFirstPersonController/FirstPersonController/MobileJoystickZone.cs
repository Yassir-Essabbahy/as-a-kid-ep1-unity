using UnityEngine;
using UnityEngine.EventSystems;

namespace ModularFirstPersonController
{
    /// <summary>
    /// Forwards pointer events from the left touch zone to MobileTouchControls.
    /// </summary>
    public class MobileJoystickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public MobileTouchControls controls;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controls != null) controls.OnLeftPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (controls != null) controls.OnLeftDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (controls != null) controls.OnLeftPointerUp(eventData);
        }
    }
}
