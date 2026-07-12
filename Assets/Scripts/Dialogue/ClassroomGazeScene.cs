using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Wires the classroom scene using Cinemachine: teacher talks -> switch to
/// BookVCam (Brain blends smoothly, per the Default Blend on the Brain) ->
/// entity talks -> switch to TeacherVCam (a zero-length Custom Blend = an
/// instant cut) -> caught -> player rig handed control.
///
/// The book switch is NOT instant, so entity dialogue can't just start
/// right after SetActive - it needs to wait until the blend has actually
/// finished. That's wired through the Inspector, not code:
///   1. Add a "Cinemachine Camera Events" component to BookVCam.
///   2. Drag this script into its "Blend Finished Event" slot.
///   3. Pick OnBookBlendFinished() from the dropdown.
///
/// The teacher-catch switch uses a zero-length blend, so BlendFinishedEvent
/// won't even fire for it (Cinemachine skips that event for zero-length
/// blends) - which is fine, since a cut has nothing to wait for. Dialogue
/// fires immediately in that branch instead.
/// </summary>
public class ClassroomGazeScene : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera teacherVCam;
    [SerializeField] private CinemachineCamera bookVCam;
    [Tooltip("Optional - add a CinemachineImpulseSource to this object for a shake on the catch moment.")]
    [SerializeField] private CinemachineImpulseSource catchImpulse;

    [Header("Player handoff")]
    [Tooltip("The FPS rig, pre-placed in the scene but left INACTIVE until handoff.")]
    [SerializeField] private GameObject playerRig;
    [Tooltip("Where the player rig should stand once given control.")]
    [SerializeField] private Transform playerHandoffPoint;

    [Header("Dialogue")]
    [SerializeField] private DialogueSequence teacherIntroLines;
    [SerializeField] private DialogueSequence entityLines;
    [SerializeField] private DialogueSequence teacherCatchesHimLines;

    [Header("Book")]
[SerializeField] private Transform bookTransform;
[SerializeField] private float startAngleX = -269.45f;
[SerializeField] private float endAngleX = -89f;
[SerializeField] private float openDuration = 1.2f;
private Coroutine rotateCoroutine;




    // Set these exact strings as the eventId on the relevant DialogueLine in the Inspector.
    private const string EVENT_LOOK_AT_BOOK = "look_at_book";
    private const string EVENT_LOOK_AT_TEACHER = "look_at_teacher";

    private void OnEnable()
    {
        DialogueEvents.OnLineEvent += HandleLineEvent;
        DialogueEvents.OnSequenceComplete += HandleSequenceComplete;
    }

    private void OnDisable()
    {
        DialogueEvents.OnLineEvent -= HandleLineEvent;
        DialogueEvents.OnSequenceComplete -= HandleSequenceComplete;
    }

    private void Start()
    {
        playerRig.SetActive(false);

        teacherVCam.gameObject.SetActive(true);
        bookVCam.gameObject.SetActive(false);

        DialogueManager.Instance.PlaySequence(teacherIntroLines);
    }

    private void HandleLineEvent(string eventId)
    {
        switch (eventId)
        {
            case EVENT_LOOK_AT_BOOK:
                bookVCam.gameObject.SetActive(true);
                teacherVCam.gameObject.SetActive(false);
                OnLookAtBook();
                // Entity dialogue starts from OnBookBlendFinished(), not here.
                break;

            case EVENT_LOOK_AT_TEACHER:
                // Camera cut + shake happen immediately - that's the jolt.
                teacherVCam.gameObject.SetActive(true);
                bookVCam.gameObject.SetActive(false);
                if (catchImpulse != null) catchImpulse.GenerateImpulse();
                // But do NOT start the next sequence here - this line is still
                // being shown to the player. Starting a new sequence now would
                // overwrite it before they ever get to read it or press Space.
                // teacherCatchesHimLines starts from HandleSequenceComplete
                // instead, once entityLines has actually been advanced past.
                break;
        }
    }

    /// Wired in the Inspector to BookVCam's Cinemachine Camera Events -> Blend Finished Event.
    public void OnBookBlendFinished()
    {
        DialogueManager.Instance.PlaySequence(entityLines);
    }

    private void HandleSequenceComplete(string sequenceId)
    {
        if (sequenceId == teacherCatchesHimLines.sequenceId)
        {
            GiveControlToPlayer();
        }
    }

    private void GiveControlToPlayer()
    {
        playerRig.transform.SetPositionAndRotation(playerHandoffPoint.position, playerHandoffPoint.rotation);
        playerRig.SetActive(true); // turns its own Camera + AudioListener on

        teacherVCam.gameObject.SetActive(false);
        bookVCam.gameObject.SetActive(false);
    }


public void OnLookAtBook()
{
    if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
    rotateCoroutine = StartCoroutine(RotateBook());
}

private System.Collections.IEnumerator RotateBook()
{
    Quaternion start = Quaternion.Euler(startAngleX, 0f, 0f);
    Quaternion end = Quaternion.Euler(endAngleX, 0f, 0f);
    float t = 0f;

    while (t < openDuration)
    {
        t += Time.deltaTime;
        bookTransform.localRotation = Quaternion.Slerp(start, end, t / openDuration);
        yield return null;
    }
    bookTransform.localRotation = end;
}
}