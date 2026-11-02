using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Small shared interact input component: a simple "pressed this frame" event,
/// with no hold logic needed.
/// - PC: the key the raft already uses (E) and left click while cursor is locked.
/// - Android: an on-screen interact button, visible only on mobile (or forced in editor)
///   and only while an interaction is available. It fires the same event on tap.
/// </summary>
[DisallowMultipleComponent]
public class InteractInput : MonoBehaviour
{
    [Header("Input Configuration")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Mobile UI")]
    [SerializeField] private GameObject mobileButton;
    [SerializeField] private TextMeshProUGUI mobileButtonLabel;
    [SerializeField] private bool forceShowInEditor = false;

    public event Action OnInteracted;

    public KeyCode InteractKey => interactKey;
    public void SetKey(KeyCode key) => interactKey = key;

    public bool WasPressed => wasPressedThisFrame;

    private bool wasPressedThisFrame;
    private bool isInteractionAvailable;

    public void TriggerInteract()
    {
        wasPressedThisFrame = true;
        OnInteracted?.Invoke();
    }

    public void ConsumePress()
    {
        wasPressedThisFrame = false;
    }

    public void SetInteractionAvailable(bool available, string promptText = null)
    {
        isInteractionAvailable = available;
        if (!string.IsNullOrEmpty(promptText) && mobileButtonLabel != null)
        {
            mobileButtonLabel.text = promptText;
        }
        UpdateMobileButtonVisibility();
    }

    public void SetMobileButton(GameObject button, TextMeshProUGUI label = null)
    {
        mobileButton = button;
        if (label != null) mobileButtonLabel = label;
        UpdateMobileButtonVisibility();
    }

    public void SetForceShowInEditor(bool force)
    {
        forceShowInEditor = force;
        UpdateMobileButtonVisibility();
    }

    private void Update()
    {
        wasPressedThisFrame = false;

        bool keyPress = Input.GetKeyDown(interactKey);
        bool mouseClick = (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0));

        if (keyPress || mouseClick)
        {
            TriggerInteract();
        }

        UpdateMobileButtonVisibility();
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
}

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
