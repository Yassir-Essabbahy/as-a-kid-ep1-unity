using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class InteractHoldInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField, Min(0.01f)] private float holdDuration = 1.5f;
    [SerializeField] private GameObject mobileButton;

    public event Action OnHoldStarted;
    public event Action OnHoldCompleted;
    public event Action OnHoldCancelled;
    public bool IsHeld => Input.GetKey(interactKey) || pointerHeld;
    public bool WasPressed => Input.GetKeyDown(interactKey) || pointerPressedFrame == Time.frameCount;
    public float HoldProgress { get; private set; }
    private bool pointerHeld, wasHeld, completed;
    private int pointerPressedFrame = -1;

    public void SetKey(KeyCode key) => interactKey = key;
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Application.isMobilePlatform) return;
        pointerHeld = true;
        pointerPressedFrame = Time.frameCount;
    }
    public void OnPointerUp(PointerEventData eventData) => pointerHeld = false;

    private void Start()
    {
        if (mobileButton != null) mobileButton.SetActive(Application.isMobilePlatform);
    }
    private void Update()
    {
        bool held = IsHeld;
        if (held && !wasHeld)
        {
            HoldProgress = 0f;
            completed = false;
            OnHoldStarted?.Invoke();
        }
        if (held && !completed)
        {
            HoldProgress = Mathf.Clamp01(HoldProgress + Time.unscaledDeltaTime / Mathf.Max(0.01f, holdDuration));
            if (HoldProgress >= 1f)
            {
                completed = true;
                OnHoldCompleted?.Invoke();
            }
        }
        if (!held && wasHeld)
        {
            if (!completed) OnHoldCancelled?.Invoke();
            HoldProgress = 0f;
        }
        wasHeld = held;
    }
    private void OnDisable()
    {
        if (wasHeld && !completed) OnHoldCancelled?.Invoke();
        pointerHeld = wasHeld = completed = false;
        HoldProgress = 0f;
    }
}
