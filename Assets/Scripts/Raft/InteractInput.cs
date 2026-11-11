using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    [SerializeField] private GameObject mobileDropButton;
    [SerializeField] private TextMeshProUGUI mobileDropButtonLabel;
    [SerializeField] private bool forceShowInEditor = true;

    public event Action OnInteracted;

    public KeyCode InteractKey => interactKey;
    public void SetKey(KeyCode key) => interactKey = key;

    public bool WasPressed => wasPressedThisFrame;

    private bool wasPressedThisFrame;
    private bool isInteractionAvailable;
    private bool isDropAvailable;
    private Action onDropCallback;

    public void TriggerInteract()
    {
        wasPressedThisFrame = true;
        OnInteracted?.Invoke();
    }

    public void TriggerDrop()
    {
        onDropCallback?.Invoke();
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
            LocalizationManager.ApplyText(mobileButtonLabel, Story.MobileUIAdapter.AdaptPrompt(promptText), preserveAlignment: true);
        }
        UpdateMobileButtonVisibility();
    }

    public void SetDropAvailable(bool available, Action onDrop = null, string dropLabel = null)
    {
        isDropAvailable = available;
        onDropCallback = available ? onDrop : null;
        if (available && mobileDropButton == null)
        {
            EnsureMobileDropButton();
        }
        if (available && mobileDropButtonLabel != null)
        {
            string lbl = string.IsNullOrEmpty(dropLabel)
                ? LocalizationManager.Resolve("raft_prompt_drop_short", "Drop")
                : Story.MobileUIAdapter.AdaptPrompt(dropLabel);
            LocalizationManager.ApplyText(mobileDropButtonLabel, lbl, preserveAlignment: true);
        }
        UpdateMobileButtonVisibility();
    }

    public void SetMobileButton(GameObject button, TextMeshProUGUI label = null)
    {
        mobileButton = button;
        if (label != null) mobileButtonLabel = label;
        EnsureParentCanvasRaycaster();
        UpdateMobileButtonVisibility();
    }

    public void SetForceShowInEditor(bool force)
    {
        forceShowInEditor = force;
        UpdateMobileButtonVisibility();
    }

    private void Awake()
    {
        if (mobileButton == null)
        {
            var fwd = FindAnyObjectByType<MobileInteractButtonForwarder>(FindObjectsInactive.Include);
            if (fwd != null)
            {
                mobileButton = fwd.gameObject;
                if (mobileButtonLabel == null)
                    mobileButtonLabel = fwd.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
        EnsureParentCanvasRaycaster();
    }

    private void EnsureParentCanvasRaycaster()
    {
        if (mobileButton == null) return;
        var parentCanvas = mobileButton.GetComponentInParent<Canvas>(true);
        if (parentCanvas != null)
        {
            var gr = parentCanvas.GetComponent<GraphicRaycaster>();
            if (gr == null) gr = parentCanvas.gameObject.AddComponent<GraphicRaycaster>();
            gr.enabled = true;
        }
    }

    private void Update()
    {
        wasPressedThisFrame = false;

        if (Story.MobilePauseMenu.Instance != null && Story.MobilePauseMenu.Instance.isPaused)
            return;

        bool keyPress = Input.GetKeyDown(interactKey);
        bool mouseClick = (!Application.isMobilePlatform && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0));
        bool mobileInteract = ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame;

        if (keyPress || mouseClick || mobileInteract)
        {
            TriggerInteract();
        }

        UpdateMobileButtonVisibility();
    }

    private void EnsureMobileDropButton()
    {
        if (mobileDropButton != null || mobileButton == null) return;

        var clone = Instantiate(mobileButton, mobileButton.transform.parent);
        clone.name = "MobileDropButton";
        var fwd = clone.GetComponent<MobileInteractButtonForwarder>();
        if (fwd != null) DestroyImmediate(fwd);

        var rt = clone.GetComponent<RectTransform>();
        var srcRt = mobileButton.GetComponent<RectTransform>();
        if (rt != null && srcRt != null)
        {
            rt.anchorMin = new Vector2(Mathf.Clamp01(srcRt.anchorMin.x - 0.14f), srcRt.anchorMin.y);
            rt.anchorMax = new Vector2(Mathf.Clamp01(srcRt.anchorMax.x - 0.14f), srcRt.anchorMax.y);
            rt.pivot = srcRt.pivot;
            rt.sizeDelta = srcRt.sizeDelta;
            rt.anchoredPosition = srcRt.anchoredPosition;
        }

        var btn = clone.GetComponent<Button>();
        if (btn == null) btn = clone.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(TriggerDrop);

        mobileDropButtonLabel = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        mobileDropButton = clone;
        mobileDropButton.SetActive(false);
    }

    private void UpdateMobileButtonVisibility()
    {
        bool isMobile = Application.isMobilePlatform || forceShowInEditor || Story.MobileUIAdapter.IsMobile;
        if (mobileButton != null)
        {
            bool shouldBeActive = isMobile && isInteractionAvailable;
            if (mobileButton.activeSelf != shouldBeActive)
            {
                mobileButton.SetActive(shouldBeActive);
            }
        }
        if (mobileDropButton != null)
        {
            bool shouldDropActive = isMobile && isDropAvailable;
            if (mobileDropButton.activeSelf != shouldDropActive)
            {
                mobileDropButton.SetActive(shouldDropActive);
            }
        }
    }
}
