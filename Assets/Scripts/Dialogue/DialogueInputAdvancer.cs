using UnityEngine;

public class DialogueInputAdvancer : MonoBehaviour
{
    [SerializeField] private KeyCode advanceKey = KeyCode.Space;

    /// <summary>How long (in seconds) after each line is shown before input is accepted.</summary>
    [SerializeField] private float inputLockDuration = 3f;

    private float _unlockTime = -1f;

    private bool IsLocked => Time.time < _unlockTime;

    private void OnEnable()
    {
        DialogueEvents.OnLineShown += HandleLineShown;
    }

    private void OnDisable()
    {
        DialogueEvents.OnLineShown -= HandleLineShown;
    }

    private void HandleLineShown()
    {
        _unlockTime = Time.time + inputLockDuration;
    }

    private void Update()
    {
        if (Story.MobilePauseMenu.Instance != null && Story.MobilePauseMenu.Instance.isPaused)
            return;

        if (IsLocked) return;

        if (DialogueManager.Instance == null || !DialogueManager.Instance.IsPlaying)
            return;

        bool pointerDown = Input.GetMouseButtonDown(0) ||
                           (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        bool advancePressed = Input.GetKeyDown(advanceKey) ||
                              (pointerDown &&
                               !DialogueTypewriterUtility.IsPointerOverButton(DialogueManager.Instance.continueButton) &&
                               !DialogueTypewriterUtility.IsPointerOverPauseButton());

        if (advancePressed)
        {
            DialogueManager.Instance.Advance();
        }
    }
}
