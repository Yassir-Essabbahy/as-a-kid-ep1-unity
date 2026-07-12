using System;

/// <summary>
/// The glue between dialogue and everything else - cameras, audio, doors,
/// jumpscares, whatever. The DialogueManager only ever calls Raise*.
/// Any script anywhere can subscribe without the dialogue system knowing
/// or caring who's listening. This is what keeps the dialogue system
/// generic enough to reuse for the whole game, not just this scene.
/// </summary>
public static class DialogueEvents
{
    /// Fires the instant a new sequence starts playing.
    public static event Action<string> OnSequenceStart;

    /// Fires every time a new line is displayed (index of the line passed).
    public static event Action OnLineShown;

    /// Fires the instant a line with a non-empty eventId is displayed.
    public static event Action<string> OnLineEvent;

    /// Fires once, when a full sequence finishes (player advances past the last line).
    public static event Action<string> OnSequenceComplete;

    public static void RaiseSequenceStart(string sequenceId)
    {
        OnSequenceStart?.Invoke(sequenceId);
    }

    public static void RaiseLineShown()
    {
        OnLineShown?.Invoke();
    }

    public static void RaiseLineEvent(string id)
    {
        if (!string.IsNullOrEmpty(id))
            OnLineEvent?.Invoke(id);
    }

    public static void RaiseSequenceComplete(string sequenceId)
    {
        OnSequenceComplete?.Invoke(sequenceId);
    }
}
