using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A reusable dialogue asset. Right-click in Project window ->
/// Create -> Dialogue -> Sequence to make one for any conversation
/// in the game (teacher, entity, household characters, anything).
/// </summary>
[CreateAssetMenu(fileName = "New Dialogue Sequence", menuName = "Dialogue/Sequence")]
public class DialogueSequence : ScriptableObject
{
    [Tooltip("Unique name for this conversation, e.g. 'teacher_intro', 'entity_first_talk'.")]
    public string sequenceId;

    public List<DialogueLine> lines;
}
