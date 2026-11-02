using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles both tap and hold interaction inputs for PC (keyboard 'E' + mouse click)
/// and Android mobile (on-screen button with touch support).
/// </summary>
[DisallowMultipleComponent]
public class InteractHoldInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Input Configuration")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField, Min(0.01f)] private float holdDuration = 1.5f;

    [Header("Mobile UI")]
    [SerializeField] private GameObject mobileButton;
    [SerializeField] private bool forceShowInEditor = false;

    public event Action OnHoldStarted;
    public event Action OnHoldCompleted;
    public event Action OnHoldCancelled;

    public float HoldDuration
    {
        get => holdDuration;
        set => holdDuration = Mathf.Max(0.01f, value);
    }

    public bool IsHeld
    {
        get
        {
            bool keyHeld = Input.GetKey(interactKey);
            bool mouseHeld = (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButton(0));
            return keyHeld || mouseHeld || pointerHeld;
        }
    }

    public bool WasPressed
    {
        get
        {
            bool keyPressed = Input.GetKeyDown(interactKey);
            bool mousePressed = (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0));
            return keyPressed || mousePressed || (pointerPressedFrame == Time.frameCount);
        }
    }

    public float HoldProgress { get; private set; }

    private bool pointerHeld;
    private bool wasHeld;
    private bool completed;
    private bool isInteractionAvailable;
    private int pointerPressedFrame = -1;

    public void SetKey(KeyCode key) => interactKey = key;

    public void SetInteractionAvailable(bool available)
    {
        isInteractionAvailable = available;
        UpdateMobileButtonVisibility();
    }

    public void SetMobileButton(GameObject button)
    {
        mobileButton = button;
        UpdateMobileButtonVisibility();
    }

    public void SetForceShowInEditor(bool force)
    {
        forceShowInEditor = force;
        UpdateMobileButtonVisibility();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerHeld = true;
        pointerPressedFrame = Time.frameCount;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerHeld = false;
    }

    public void ResetHold()
    {
        if (wasHeld && !completed)
        {
            OnHoldCancelled?.Invoke();
        }
        HoldProgress = 0f;
        wasHeld = false;
        completed = false;
        pointerHeld = false;
    }

    private void Start()
    {
        UpdateMobileButtonVisibility();
    }

    private void Update()
    {
        UpdateMobileButtonVisibility();

        bool held = IsHeld;
        if (held && !wasHeld)
        {
            HoldProgress = 0f;
            completed = false;
            OnHoldStarted?.Invoke();
        }

        if (held && !completed)
        {
            HoldProgress = Mathf.Clamp01(HoldProgress + Time.deltaTime / Mathf.Max(0.01f, holdDuration));
            if (HoldProgress >= 1f)
            {
                completed = true;
                OnHoldCompleted?.Invoke();
            }
        }

        if (!held && wasHeld)
        {
            if (!completed)
            {
                OnHoldCancelled?.Invoke();
            }
            HoldProgress = 0f;
        }

        wasHeld = held;
    }

    private void UpdateMobileButtonVisibility()
    {
        if (mobileButton != null)
        {
            bool isMobile = Application.isMobilePlatform || forceShowInEditor;
            bool shouldBeActive = isMobile && isInteractionAvailable;
            if (mobileButton.activeSelf != shouldBeActive)
            {
                mobileButton.SetActive(shouldBeActive);
            }
        }
    }

    private void OnDisable()
    {
        ResetHold();
    }
}

/// <summary>
/// Lightweight forwarder placed on the mobile UI button to deliver touch events to InteractHoldInput.
/// </summary>
public class MobileHoldButtonForwarder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public InteractHoldInput targetInput;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetInput != null)
        {
            targetInput.OnPointerDown(eventData);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetInput != null)
        {
            targetInput.OnPointerUp(eventData);
        }
    }
}
