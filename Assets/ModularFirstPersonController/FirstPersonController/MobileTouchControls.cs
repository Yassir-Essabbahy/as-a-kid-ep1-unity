using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModularFirstPersonController
{
    /// <summary>
    /// Dual touch mobile controller:
    /// - Left half: semi-transparent virtual analog stick for movement (dynamic or fixed).
    /// - Right half: completely hidden touch zone for camera look (pitch & yaw).
    /// Fully multi-touch compatible and safe for mobile and editor testing.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileTouchControls : MonoBehaviour
    {
        [Header("Target Controller")]
        [Tooltip("The FirstPersonController to drive. Automatically found if null.")]
        public FirstPersonController controller;

        [Header("Platform Visibility")]
        [Tooltip("If true, shows controls in Editor for mouse-based testing.")]
        public bool forceShowInEditor = true;

        [Header("Analog Stick (Left Screen)")]
        public RectTransform leftTouchArea;
        public RectTransform joystickBase;
        public RectTransform joystickHandle;
        public CanvasGroup joystickCanvasGroup;
        [Tooltip("Maximum pixel distance the handle can move from base center.")]
        public float maxJoystickRadius = 75f;
        [Tooltip("If true, the joystick centers where the thumb first touches on the left half.")]
        public bool dynamicJoystick = true;
        [Range(0f, 1f)] public float idleAlpha = 0.28f;
        [Range(0f, 1f)] public float activeAlpha = 0.70f;

        [Header("Camera Look (Right Screen - Hidden)")]
        public RectTransform rightLookArea;
        [Tooltip("Horizontal look sensitivity (degrees per pixel).")]
        public float lookSensitivityX = 0.18f;
        [Tooltip("Vertical look sensitivity (degrees per pixel).")]
        public float lookSensitivityY = 0.14f;

        [Header("On-Screen Interact Button (Mobile)")]
        public RectTransform interactButtonRect;

        public static event System.Action OnInteractTapped;
        public static bool IsInteractTriggeredThisFrame { get; private set; }

        // Internal State
        private Vector2 defaultBaseAnchoredPos;
        private int movePointerId = -999;
        private int lookPointerId = -999;
        private Vector2 lastLookScreenPos = Vector2.zero;
        private Vector2 currentMoveInput = Vector2.zero;
        private Vector2 pendingLookDelta = Vector2.zero;
        private Canvas rootCanvas;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<FirstPersonController>();
                if (controller == null) controller = FirstPersonController.ActiveInstance;
                if (controller == null) controller = FindAnyObjectByType<FirstPersonController>();
            }

            rootCanvas = GetComponentInParent<Canvas>();
            EnsureEventSystem();

            if (joystickBase != null)
            {
                defaultBaseAnchoredPos = joystickBase.anchoredPosition;
            }

            UpdateVisibility();
            SetJoystickAlpha(idleAlpha);
        }

        private void Start()
        {
            UpdateVisibility();
        }

        private void Update()
        {
            if (controller == null)
            {
                controller = FirstPersonController.ActiveInstance != null
                    ? FirstPersonController.ActiveInstance
                    : FindAnyObjectByType<FirstPersonController>();
            }

            // Verify active touches on real mobile devices to prevent stuck pointers
            ValidatePointersOnMobile();

            // Feed movement to controller
            if (controller != null)
            {
                if (controller.playerCanMove)
                {
                    controller.SetMobileMovement(currentMoveInput);
                }
                else
                {
                    controller.SetMobileMovement(Vector2.zero);
                }

                // Feed camera look to controller
                if (controller.cameraCanMove && pendingLookDelta.sqrMagnitude > 0.0001f)
                {
                    controller.AddMobileLook(pendingLookDelta);
                }
            }

            pendingLookDelta = Vector2.zero;
        }

        private void LateUpdate()
        {
            IsInteractTriggeredThisFrame = false;
        }

        public void ResetAllTouchPointers()
        {
            ResetMovementStick();
            lookPointerId = -999;
            lastLookScreenPos = Vector2.zero;
            pendingLookDelta = Vector2.zero;
        }

        public void UpdateVisibility()
        {
            bool isMobile = Application.isMobilePlatform;
            bool shouldShow = isMobile || forceShowInEditor;

            var cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = shouldShow ? 1f : 0f;
                cg.interactable = shouldShow;
                cg.blocksRaycasts = shouldShow;
            }
            else
            {
                gameObject.SetActive(shouldShow);
            }
        }

        private void SetJoystickAlpha(float alpha)
        {
            if (joystickCanvasGroup != null)
            {
                joystickCanvasGroup.alpha = alpha;
            }
        }

        #region Left Touch (Analog Stick) Handlers

        public void OnLeftPointerDown(PointerEventData eventData)
        {
            if (movePointerId != -999) return; // Already tracking a movement pointer

            movePointerId = eventData.pointerId;

            if (dynamicJoystick && joystickBase != null)
            {
                RectTransform parentRect = joystickBase.parent as RectTransform;
                if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                {
                    joystickBase.anchoredPosition = localPoint;
                }
            }

            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }

            UpdateJoystickMovement(eventData);
            SetJoystickAlpha(activeAlpha);
        }

        public void OnLeftDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != movePointerId) return;

            UpdateJoystickMovement(eventData);
        }

        public void OnLeftPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != movePointerId) return;

            ResetMovementStick();
        }

        private void UpdateJoystickMovement(PointerEventData eventData)
        {
            if (joystickBase == null || joystickHandle == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBase, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                Vector2 clamped = Vector2.ClampMagnitude(localPoint, maxJoystickRadius);
                joystickHandle.anchoredPosition = clamped;

                // Normalize input: x = Horizontal (strafe), y = Vertical (forward/back)
                float radius = Mathf.Max(maxJoystickRadius, 1f);
                currentMoveInput = clamped / radius;
            }
        }

        private void ResetMovementStick()
        {
            movePointerId = -999;
            currentMoveInput = Vector2.zero;

            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }

            if (dynamicJoystick && joystickBase != null)
            {
                joystickBase.anchoredPosition = defaultBaseAnchoredPos;
            }

            SetJoystickAlpha(idleAlpha);
        }

        #endregion

        #region Right Touch (Camera Look - Hidden) Handlers

        public void OnRightPointerDown(PointerEventData eventData)
        {
            if (lookPointerId != -999) return; // Already tracking look pointer
            if (rightLookArea != null && eventData.pointerCurrentRaycast.gameObject != null &&
                eventData.pointerCurrentRaycast.gameObject != rightLookArea.gameObject)
            {
                return; // Do not steal touch from UI buttons
            }

            lookPointerId = eventData.pointerId;
            lastLookScreenPos = eventData.position;
        }

        public void OnInteractPointerDown(PointerEventData eventData)
        {
            IsInteractTriggeredThisFrame = true;
            OnInteractTapped?.Invoke();
        }

        public void OnRightDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != lookPointerId) return;

            Vector2 currentPos = eventData.position;
            Vector2 delta = currentPos - lastLookScreenPos;
            lastLookScreenPos = currentPos;

            // Apply sensitivity
            float lookX = delta.x * lookSensitivityX;
            float lookY = delta.y * lookSensitivityY;

            pendingLookDelta += new Vector2(lookX, lookY);
        }

        public void OnRightPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != lookPointerId) return;

            lookPointerId = -999;
            lastLookScreenPos = Vector2.zero;
        }

        #endregion

        private void ValidatePointersOnMobile()
        {
#if !UNITY_EDITOR
            if (Application.isMobilePlatform)
            {
                if (movePointerId != -999 && !IsTouchFingerActive(movePointerId))
                {
                    ResetMovementStick();
                }

                if (lookPointerId != -999 && !IsTouchFingerActive(lookPointerId))
                {
                    lookPointerId = -999;
                    lastLookScreenPos = Vector2.zero;
                }
            }
#endif
        }

        private bool IsTouchFingerActive(int fingerId)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId == fingerId && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                {
                    return true;
                }
            }
            return false;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var existing = FindAnyObjectByType<EventSystem>();
                if (existing == null)
                {
                    var esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                    DontDestroyOnLoad(esObj);
                }
            }
        }
    }

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
