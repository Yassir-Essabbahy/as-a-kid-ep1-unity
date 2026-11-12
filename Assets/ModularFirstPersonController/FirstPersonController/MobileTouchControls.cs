using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
        public float maxJoystickRadius = 105f;
        [Tooltip("If true, the joystick centers where the thumb first touches on the left half.")]
        public bool dynamicJoystick = true;
        [Range(0f, 1f)] public float idleAlpha = 0.48f;
        [Range(0f, 1f)] public float activeAlpha = 0.88f;

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

        private struct ActivePointer
        {
            public int pointerId;
            public int touchId;
            public bool isMouse;

            public bool IsActive => pointerId != -999;

            public void Reset()
            {
                pointerId = -999;
                touchId = 0;
                isMouse = false;
            }

            public static ActivePointer FromEventData(PointerEventData eventData)
            {
                if (eventData is ExtendedPointerEventData ext)
                {
                    return new ActivePointer
                    {
                        pointerId = eventData.pointerId,
                        touchId = ext.touchId,
                        isMouse = ext.touchId == 0 && (ext.device is Mouse || ext.pointerType == UIPointerType.MouseOrPen)
                    };
                }

                return new ActivePointer
                {
                    pointerId = eventData.pointerId,
                    touchId = 0,
                    isMouse = eventData.pointerId < 0
                };
            }
        }

        // Internal State
        private Vector2 defaultBaseAnchoredPos;
        private Vector2 configuredBaseAnchoredPos;
        private bool hasCapturedBasePos = false;
        private ActivePointer movePointer = new ActivePointer { pointerId = -999 };
        private ActivePointer lookPointer = new ActivePointer { pointerId = -999 };
        private int movePointerId => movePointer.pointerId;
        private int lookPointerId => lookPointer.pointerId;
        private Vector2 lastLookScreenPos = Vector2.zero;
        private Vector2 currentMoveInput = Vector2.zero;
        private Vector2 pendingLookDelta = Vector2.zero;
        private Canvas rootCanvas;
        private bool wasControlBlocked = false;
        private Rect lastSafeArea = Rect.zero;
        private int lastScreenWidth = 0;
        private int lastScreenHeight = 0;

        public bool IsControlBlocked()
        {
            if (Story.MobilePauseMenu.Instance != null && Story.MobilePauseMenu.Instance.isPaused)
                return true;
            if (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning)
                return true;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying)
                return true;
            if (TimingMinigameUI.IsAnyMinigameActive)
                return true;
            if (MetroFuseBoxPuzzle.IsAnyInspecting)
                return true;
            if (AsAKid.Conversation.ConversationUI.IsAnyActive)
                return true;
            return false;
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<FirstPersonController>();
                if (controller == null) controller = FirstPersonController.ActiveInstance;
                if (controller == null) controller = FindAnyObjectByType<FirstPersonController>();
            }

            rootCanvas = GetComponentInParent<Canvas>();
            if (leftTouchArea == null)
                leftTouchArea = transform.Find("LeftTouchZone") as RectTransform;
            if (rightLookArea == null)
                rightLookArea = transform.Find("RightLookZone") as RectTransform;
            if (joystickBase == null && leftTouchArea != null)
                joystickBase = leftTouchArea.Find("JoystickBase") as RectTransform;
            if (joystickHandle == null && joystickBase != null)
                joystickHandle = joystickBase.Find("JoystickHandle") as RectTransform;
            if (joystickCanvasGroup == null && joystickBase != null)
                joystickCanvasGroup = joystickBase.GetComponent<CanvasGroup>();

            EnsureEventSystem();

            if (joystickBase != null && !hasCapturedBasePos)
            {
                configuredBaseAnchoredPos = joystickBase.anchoredPosition;
                hasCapturedBasePos = true;
                ApplySafeDefaultJoystickPos(true);
            }

            UpdateVisibility();
            SetJoystickAlpha(idleAlpha);
        }

        private void OnEnable()
        {
            ResetAllTouchPointers();
            ApplySafeDefaultJoystickPos(true);
            UpdateVisibility();
        }

        private void Start()
        {
            if (GameSettingsManager.Instance != null)
            {
                lookSensitivityX = GameSettingsManager.Instance.MobileLookSensitivityX;
                lookSensitivityY = GameSettingsManager.Instance.MobileLookSensitivityY;
            }
            GameSettingsManager.OnSensitivityChanged += HandleSensitivityChanged;
            ApplySafeDefaultJoystickPos(true);
            UpdateVisibility();
        }

        private void OnDestroy()
        {
            GameSettingsManager.OnSensitivityChanged -= HandleSensitivityChanged;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetAllTouchPointers();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ResetAllTouchPointers();
            }
        }

        private void HandleSensitivityChanged(float newSens)
        {
            if (GameSettingsManager.Instance != null)
            {
                lookSensitivityX = GameSettingsManager.Instance.MobileLookSensitivityX;
                lookSensitivityY = GameSettingsManager.Instance.MobileLookSensitivityY;
            }
        }

        private void Update()
        {
            if (controller == null)
            {
                controller = FirstPersonController.ActiveInstance != null
                    ? FirstPersonController.ActiveInstance
                    : FindAnyObjectByType<FirstPersonController>();
            }

            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                ApplySafeDefaultJoystickPos(false);
            }

            bool blocked = IsControlBlocked();
            if (blocked != wasControlBlocked)
            {
                wasControlBlocked = blocked;
                if (blocked)
                {
                    ResetAllTouchPointers();
                    currentMoveInput = Vector2.zero;
                    pendingLookDelta = Vector2.zero;
                }
                UpdateVisibility();
            }

            if (blocked)
            {
                if (controller != null)
                {
                    controller.SetMobileMovement(Vector2.zero);
                }
                pendingLookDelta = Vector2.zero;
                return;
            }

            // Verify active Input System / legacy pointers so lifted or cancelled touches never stick
            ValidateActivePointers();

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
            lookPointer.Reset();
            lastLookScreenPos = Vector2.zero;
            pendingLookDelta = Vector2.zero;
        }

        public void UpdateVisibility()
        {
            bool isMobile = Application.isMobilePlatform;
            bool shouldShowControls = (isMobile || forceShowInEditor) && !IsControlBlocked();

            if (leftTouchArea != null)
            {
                leftTouchArea.gameObject.SetActive(shouldShowControls);
            }
            if (rightLookArea != null)
            {
                rightLookArea.gameObject.SetActive(shouldShowControls);
            }
            if (interactButtonRect != null)
            {
                // If managed by InteractInput (MobileInteractButtonForwarder), only force-hide when blocked
                if (interactButtonRect.GetComponent<MobileInteractButtonForwarder>() != null)
                {
                    if (!shouldShowControls && interactButtonRect.gameObject.activeSelf)
                        interactButtonRect.gameObject.SetActive(false);
                }
                else
                {
                    interactButtonRect.gameObject.SetActive(shouldShowControls);
                }
            }

            // Keep the root CanvasGroup visible and interactable for modal dialogs (PauseMenu, etc.)
            var cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        private void SetJoystickAlpha(float alpha)
        {
            if (joystickCanvasGroup != null)
            {
                joystickCanvasGroup.alpha = alpha;
            }
        }

        private Vector2 GetSafeInsetBottomLeftCanvasUnits()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;
            Rect safe = Screen.safeArea;
            RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            float canvasWidth = canvasRect != null && canvasRect.rect.width > 1f ? canvasRect.rect.width : Screen.width;
            float canvasHeight = canvasRect != null && canvasRect.rect.height > 1f ? canvasRect.rect.height : Screen.height;

            float leftInset = Mathf.Max(0f, safe.xMin) * (canvasWidth / Screen.width);
            float bottomInset = Mathf.Max(0f, safe.yMin) * (canvasHeight / Screen.height);
            return new Vector2(leftInset, bottomInset);
        }

        private void ApplySafeDefaultJoystickPos(bool forceRecalculate)
        {
            if (joystickBase == null || !hasCapturedBasePos) return;
            if (!forceRecalculate &&
                Screen.safeArea == lastSafeArea &&
                Screen.width == lastScreenWidth &&
                Screen.height == lastScreenHeight)
            {
                return;
            }

            lastSafeArea = Screen.safeArea;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            Vector2 safeInset = GetSafeInsetBottomLeftCanvasUnits();
            Vector2 desired = configuredBaseAnchoredPos + safeInset;

            RectTransform parentRect = joystickBase.parent as RectTransform;
            if (parentRect != null && parentRect.rect.width > 1f && parentRect.rect.height > 1f)
            {
                Rect parentBounds = parentRect.rect;
                Vector2 anchorCenterRatio = (joystickBase.anchorMin + joystickBase.anchorMax) * 0.5f;
                Vector2 anchorOriginInParent = new Vector2(
                    Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, anchorCenterRatio.x),
                    Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, anchorCenterRatio.y)
                );
                Vector2 targetCenterInParent = anchorOriginInParent + desired;
                targetCenterInParent = ClampJoystickCenterInParent(parentBounds, targetCenterInParent, safeInset);
                Vector2 centerToPivot = new Vector2(
                    (joystickBase.pivot.x - 0.5f) * joystickBase.rect.width,
                    (joystickBase.pivot.y - 0.5f) * joystickBase.rect.height
                );
                desired = targetCenterInParent + centerToPivot - anchorOriginInParent;
            }

            defaultBaseAnchoredPos = desired;
            if (!movePointer.IsActive)
            {
                joystickBase.anchoredPosition = defaultBaseAnchoredPos;
            }
        }

        private Vector2 ClampJoystickCenterInParent(Rect parentBounds, Vector2 targetCenterInParent, Vector2 safeInset)
        {
            float halfBaseWidth = joystickBase != null ? joystickBase.rect.width * 0.5f : 130f;
            float halfBaseHeight = joystickBase != null ? joystickBase.rect.height * 0.5f : 130f;
            float marginX = Mathf.Max(maxJoystickRadius + 14f, halfBaseWidth + 8f);
            float marginY = Mathf.Max(maxJoystickRadius + 14f, halfBaseHeight + 8f);

            float minX = parentBounds.xMin + marginX + safeInset.x;
            float maxX = parentBounds.xMax - marginX;
            targetCenterInParent.x = minX <= maxX
                ? Mathf.Clamp(targetCenterInParent.x, minX, maxX)
                : parentBounds.center.x;

            float minY = parentBounds.yMin + marginY + safeInset.y;
            float maxY = parentBounds.yMax - marginY;
            targetCenterInParent.y = minY <= maxY
                ? Mathf.Clamp(targetCenterInParent.y, minY, maxY)
                : parentBounds.center.y;

            return targetCenterInParent;
        }

        #region Left Touch (Analog Stick) Handlers

        public void OnLeftPointerDown(PointerEventData eventData)
        {
            if (IsControlBlocked()) return;
            if (movePointer.IsActive) return; // Already tracking a movement pointer

            movePointer = ActivePointer.FromEventData(eventData);

            if (dynamicJoystick && joystickBase != null)
            {
                RectTransform parentRect = joystickBase.parent as RectTransform;
                if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                {
                    Rect parentBounds = parentRect.rect;
                    Vector2 clampedCenter = ClampJoystickCenterInParent(
                        parentBounds, localPoint, GetSafeInsetBottomLeftCanvasUnits());

                    Vector2 anchorCenterRatio = (joystickBase.anchorMin + joystickBase.anchorMax) * 0.5f;
                    Vector2 anchorOriginInParent = new Vector2(
                        Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, anchorCenterRatio.x),
                        Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, anchorCenterRatio.y)
                    );
                    Vector2 centerToPivot = new Vector2(
                        (joystickBase.pivot.x - 0.5f) * joystickBase.rect.width,
                        (joystickBase.pivot.y - 0.5f) * joystickBase.rect.height
                    );
                    joystickBase.anchoredPosition = clampedCenter + centerToPivot - anchorOriginInParent;
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
            if (!movePointer.IsActive || eventData.pointerId != movePointerId) return;

            UpdateJoystickMovement(eventData);
        }

        public void OnLeftPointerUp(PointerEventData eventData)
        {
            if (!movePointer.IsActive || eventData.pointerId != movePointerId) return;

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
            movePointer.Reset();
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
            if (IsControlBlocked()) return;
            if (lookPointer.IsActive) return; // Already tracking look pointer
            if (rightLookArea != null && eventData.pointerCurrentRaycast.gameObject != null &&
                eventData.pointerCurrentRaycast.gameObject != rightLookArea.gameObject)
            {
                return; // Do not steal touch from UI buttons
            }

            lookPointer = ActivePointer.FromEventData(eventData);
            lastLookScreenPos = eventData.position;
        }

        public void OnInteractPointerDown(PointerEventData eventData)
        {
            if (IsControlBlocked()) return;
            IsInteractTriggeredThisFrame = true;
            OnInteractTapped?.Invoke();
        }

        public void OnRightDrag(PointerEventData eventData)
        {
            if (!lookPointer.IsActive || eventData.pointerId != lookPointerId) return;

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
            if (!lookPointer.IsActive || eventData.pointerId != lookPointerId) return;

            lookPointer.Reset();
            lastLookScreenPos = Vector2.zero;
        }

        #endregion

        private void ValidateActivePointers()
        {
            if (movePointer.IsActive && !IsPointerStillActive(in movePointer))
            {
                ResetMovementStick();
            }

            if (lookPointer.IsActive && !IsPointerStillActive(in lookPointer))
            {
                lookPointer.Reset();
                lastLookScreenPos = Vector2.zero;
            }
        }

        private bool IsPointerStillActive(in ActivePointer pointer)
        {
            if (pointer.touchId > 0)
            {
                Touchscreen ts = Touchscreen.current;
                if (ts != null)
                {
                    var touches = ts.touches;
                    for (int i = 0; i < touches.Count; i++)
                    {
                        var touch = touches[i];
                        if (touch.touchId.ReadValue() == pointer.touchId && touch.isInProgress)
                        {
                            return true;
                        }
                    }
                    return false;
                }

                return Input.touchCount > 0;
            }

            if (pointer.isMouse)
            {
                if (Mouse.current != null)
                {
                    return Mouse.current.leftButton.isPressed;
                }
                return Input.GetMouseButton(0);
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                UnityEngine.Touch t = Input.GetTouch(i);
                if (t.fingerId == pointer.pointerId &&
                    t.phase != UnityEngine.TouchPhase.Ended &&
                    t.phase != UnityEngine.TouchPhase.Canceled)
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
                    var esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    DontDestroyOnLoad(esObj);
                }
            }
        }
    }
}
