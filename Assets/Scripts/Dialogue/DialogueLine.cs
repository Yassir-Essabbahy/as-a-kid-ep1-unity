using UnityEngine;

/// <summary>
/// A single line of dialogue. eventId is optional - leave it empty for
/// ordinary lines. Set it only on the specific line that should trigger
/// something else in the scene (camera move, audio change, door unlock...).
/// </summary>
[System.Serializable]
public class DialogueLine
{
public string speakerName;

public string key; // localization key, e.g. "classroom_entity_01"

public AudioClip voiceClip;

[Tooltip("Leave empty for a normal line. Fill in to broadcast an event when this line is shown, e.g. 'look_at_book'.")]
public string eventId;
}
