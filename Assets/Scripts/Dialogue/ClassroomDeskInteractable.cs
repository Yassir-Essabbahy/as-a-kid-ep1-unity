using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Modular interactable object placed on or around the student's desk (Life is Strange 1 style).
/// Any object with a Collider and this component will be detected by the seated interaction pointer.
/// If marked as isRequiredForProgression, all such items must be inspected before the teacher
/// tells the class to open page 54 of the book.
/// </summary>
public class ClassroomDeskInteractable : MonoBehaviour
{
    [Header("Identity & Prompt")]
    [Tooltip("The readable name of the item displayed on the reticle prompt (e.g. 'Pen', 'Drawing', 'Desk Carving')")]
    public string itemName = "Object";

    [Tooltip("Action verb displayed on the prompt badge (e.g. 'Look', 'Examine', 'Open Book')")]
    public string actionPrompt = "Look";

    [Tooltip("Key binding display (default 'E')")]
    public string keyPrompt = "E";

    [Header("Progression Flow")]
    [Tooltip("If true, this item must be inspected before the teacher cues opening the book")]
    public bool isRequiredForProgression = true;

    [Tooltip("Is the item currently interactable? (For example, Book is locked until required items are inspected)")]
    public bool isInteractable = true;

    [Tooltip("Message or thought key if player tries to interact when locked")]
    public string lockedThoughtKey = "desk_book_locked_01";

    [Header("Inner Monologue / Thoughts")]
    [Tooltip("Localization keys in dialogue.csv triggered when inspecting this object")]
    public string[] thoughtDialogueKeys;

    [Tooltip("Fallback text if localization keys are empty or missing")]
    [TextArea(2, 4)]
    public string[] fallbackThoughts;

    [Header("Audio & Feel")]
    public AudioClip inspectAudioClip;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    [Header("State & Events")]
    public bool hasBeenInspected = false;
    public UnityEvent onInspected;

    public event Action<ClassroomDeskInteractable> OnItemInspected;

    private int _thoughtIndex = 0;

    /// <summary>
    /// Returns the localized or fallback thought string for the current inspection.
    /// </summary>
    public string GetNextThought()
    {
        if (thoughtDialogueKeys != null && thoughtDialogueKeys.Length > 0)
        {
            string key = thoughtDialogueKeys[_thoughtIndex % thoughtDialogueKeys.Length];
            _thoughtIndex++;

            if (LocalizationManager.Instance != null)
            {
                string loc = LocalizationManager.Instance.Get(key);
                if (!string.IsNullOrEmpty(loc) && loc != key) return loc;
            }
        }

        if (fallbackThoughts != null && fallbackThoughts.Length > 0)
        {
            string fb = fallbackThoughts[_thoughtIndex % fallbackThoughts.Length];
            _thoughtIndex++;
            return fb;
        }

        return string.Empty;
    }

    /// <summary>
    /// Triggered when the player presses the interaction key while hovering over this object.
    /// </summary>
    public void Interact()
    {
        if (!isInteractable)
        {
            Debug.Log($"[ClassroomDeskInteractable] {itemName} is currently locked.");
            return;
        }

        hasBeenInspected = true;
        onInspected?.Invoke();
        OnItemInspected?.Invoke(this);

        if (inspectAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(inspectAudioClip, transform.position, soundVolume);
        }
    }
}
