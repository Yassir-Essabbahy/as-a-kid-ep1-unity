using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

/// <summary>
/// Wires the classroom scene using Cinemachine: teacher talks -> switch to
/// BookVCam (Brain blends smoothly) -> entity talks -> switch to TeacherVCam
/// (instant cut) -> teacher2 -> back to book -> player2 -> zoom into book
/// picture -> fade to black -> teleport player to metro station -> fade in.
/// </summary>
public class ClassroomGazeScene : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera teacherVCam;
    [SerializeField] private CinemachineCamera bookVCam;
    [SerializeField] private CinemachineCamera bookZoomVCam;
    [Tooltip("Optional - add a CinemachineImpulseSource to this object for a shake on the catch moment.")]
    [SerializeField] private CinemachineImpulseSource catchImpulse;

    [Header("Player handoff")]
    [Tooltip("The FPS rig, pre-placed in the scene but left INACTIVE until handoff.")]
    [SerializeField] private GameObject playerRig;
    [Tooltip("Where the player rig should stand once given control - position this at the metro station.")]
    [SerializeField] private Transform playerHandoffPoint;
    [Tooltip("Fade-to-black / fade-in duration for the teleport, in seconds.")]
    [SerializeField] private float teleportFadeDuration = 0.75f;

    [Header("Dialogue")]
    [SerializeField] private DialogueSequence teacherIntroLines;
    [SerializeField] private DialogueSequence entityLines;
    [SerializeField] private DialogueSequence teacher2Lines;
    [SerializeField] private DialogueSequence player2Lines;

    [Header("Book")]
    [SerializeField] private Transform bookTransform;
    [SerializeField] private float startAngleX = -269.45f;
    [SerializeField] private float endAngleX = -89f;
    [SerializeField] private float openDuration = 1.2f;

    [Header("Zoom transition")]
    [Tooltip("Position and rotation the zoom camera will snap to before blending in. Move this in the Scene view to frame the desired spot.")]
    [SerializeField] private Transform bookZoomPoint;
    [Tooltip("Fired after the teleport fade-in completes. Wire any extra on-arrival stuff here (ambient audio, UI prompt, etc.).")]
    [SerializeField] private UnityEvent onZoomComplete;

    private Coroutine _rotateCoroutine;

    // Prevents OnBookBlendFinished from re-triggering entityLines on later book camera switches.
    private bool _entityLinesStarted;

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
        bookZoomVCam.gameObject.SetActive(false);

        _entityLinesStarted = false;

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
                break;

            case EVENT_LOOK_AT_TEACHER:
                teacherVCam.gameObject.SetActive(true);
                bookVCam.gameObject.SetActive(false);
                if (catchImpulse != null) catchImpulse.GenerateImpulse();
                break;
        }
    }

    /// <summary>Wired in the Inspector to BookVCam's CinemachineCameraEvents -> Blend Finished Event.</summary>
    public void OnBookBlendFinished()
    {
        if (_entityLinesStarted) return;
        _entityLinesStarted = true;
        DialogueManager.Instance.PlaySequence(entityLines, showFade: false);
    }

    private void HandleSequenceComplete(string sequenceId)
    {
        if (sequenceId == entityLines.sequenceId)
        {
            DialogueManager.Instance.PlaySequence(teacher2Lines, showFade: false);
        }
        else if (sequenceId == teacher2Lines.sequenceId)
        {
            bookVCam.gameObject.SetActive(true);
            teacherVCam.gameObject.SetActive(false);
            DialogueManager.Instance.PlaySequence(player2Lines, showFade: false);
        }
        else if (sequenceId == player2Lines.sequenceId)
        {
            StartZoom();
        }
    }

    private void StartZoom()
    {
        if (bookZoomPoint != null)
            bookZoomVCam.transform.SetPositionAndRotation(bookZoomPoint.position, bookZoomPoint.rotation);

        bookVCam.gameObject.SetActive(false);
        bookZoomVCam.gameObject.SetActive(true);
    }

    /// <summary>Wired in the Inspector to BookZoomVCam's CinemachineCameraEvents -> Blend Finished Event.</summary>
    public void OnBookZoomBlendFinished()
    {
        ScreenFader.Instance.FadeToBlack(teleportFadeDuration, OnTeleportFadeOutComplete);
    }

    private void OnTeleportFadeOutComplete()
    {
        GiveControlToPlayer();
        ScreenFader.Instance.FadeFromBlack(teleportFadeDuration);
        onZoomComplete?.Invoke();
    }

    /// <summary>Swaps every VCam off, drops the player at the metro station, and hands over control. Only ever called while the screen is black.</summary>
    private void GiveControlToPlayer()
    {
        teacherVCam.gameObject.SetActive(false);
        bookVCam.gameObject.SetActive(false);
        bookZoomVCam.gameObject.SetActive(false);

        playerRig.transform.SetPositionAndRotation(playerHandoffPoint.position, playerHandoffPoint.rotation);
        playerRig.SetActive(true);
    }

    public void OnLookAtBook()
    {
        if (_rotateCoroutine != null) StopCoroutine(_rotateCoroutine);
        _rotateCoroutine = StartCoroutine(RotateBook());
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