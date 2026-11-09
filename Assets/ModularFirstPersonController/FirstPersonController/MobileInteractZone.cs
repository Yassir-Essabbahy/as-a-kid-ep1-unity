using UnityEngine;
using UnityEngine.EventSystems;

namespace ModularFirstPersonController
{
    /// <summary>
    /// Forwards pointer events from the on-screen interact button to MobileTouchControls.
    /// </summary>
    public class MobileInteractZone : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public MobileTouchControls controls;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controls != null) controls.OnInteractPointerDown(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (controls != null) controls.OnInteractPointerDown(eventData);
        }
    }
}
