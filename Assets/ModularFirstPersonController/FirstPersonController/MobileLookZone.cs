using UnityEngine;
using UnityEngine.EventSystems;

namespace ModularFirstPersonController
{
    /// <summary>
    /// Forwards pointer events from the right touch zone to MobileTouchControls.
    /// </summary>
    public class MobileLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public MobileTouchControls controls;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controls != null) controls.OnRightPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (controls != null) controls.OnRightDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (controls != null) controls.OnRightPointerUp(eventData);
        }
    }
}
