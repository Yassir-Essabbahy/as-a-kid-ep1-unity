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
        if (IsLocked) return;

        if (Input.GetKeyDown(advanceKey) && DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying)
        {
            DialogueManager.Instance.Advance();
        }
    }
}
