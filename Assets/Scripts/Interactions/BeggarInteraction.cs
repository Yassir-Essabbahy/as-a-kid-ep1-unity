using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class BeggarInteraction : MonoBehaviour
{
    [Header("Trigger")]
    public string playerTag = "Player";
    public bool triggerOnce = true;

    [Header("Required Item")]
    public CarryableItem requiredCarriedItem; // must be carried for the beggar to talk

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip preSound;

    [Header("Timing")]
    public float delayBeforeLook = 0.4f;
    public float lookInDuration = 0.6f;
    public float lookOutDuration = 0.5f;
    public float delayBeforeDialogue = 0.2f;

    [Header("Refs")]
    public Transform playerCamera;
    public Transform lookTarget;
    public MonoBehaviour playerController;
    public NpcConversation conversation;

    [Header("Cinemachine")]
    public CinemachineBrain cinemachineBrain;
    public CinemachineCamera playerVcam;
    public CinemachineCamera talkZoomVcam;
    public float dialogueZoomFOV = 40f;

    private bool hasTriggered = false;
    private FirstPersonController _fpsController;
    private Rigidbody _rigidbody;

    void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (playerController == null)
            playerController = Object.FindFirstObjectByType<FirstPersonController>();

        _fpsController = playerController as FirstPersonController;
        if (_fpsController == null && playerController != null)
            _fpsController = playerController.GetComponent<FirstPersonController>();

        if (_fpsController != null)
        {
            if (_rigidbody == null)
                _rigidbody = _fpsController.GetComponent<Rigidbody>();
            if (playerCamera == null && _fpsController.playerCamera != null)
                playerCamera = _fpsController.playerCamera.transform;
        }

        if (cinemachineBrain == null && playerCamera != null)
            cinemachineBrain = playerCamera.GetComponent<CinemachineBrain>();

        if (playerVcam == null)
        {
            var pv = GameObject.Find("PlayerVcam");
            if (pv != null) playerVcam = pv.GetComponent<CinemachineCamera>();
        }

        if (talkZoomVcam == null)
        {
            var tv = GameObject.Find("TalkZoomVcam");
            if (tv != null) talkZoomVcam = tv.GetComponent<CinemachineCamera>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;
        if (conversation == null) return;

        // Gate: only proceed if the required item (teddy bear) is currently being carried
        if (requiredCarriedItem != null && !requiredCarriedItem.IsBeingCarried) return;

        hasTriggered = true;
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        if (audioSource != null && preSound != null)
            audioSource.PlayOneShot(preSound);

        yield return new WaitForSeconds(delayBeforeLook);

        ResolveReferences();

        // 1. Halt movement and lock input
        if (_fpsController != null)
        {
            _fpsController.SetControlLocked(true);
        }
        else if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        Vector3 targetLookPos = lookTarget != null ? lookTarget.position : (transform.position + Vector3.up * 1.3f);
        bool useCinemachine = (cinemachineBrain != null && playerVcam != null && talkZoomVcam != null && playerCamera != null);

        if (useCinemachine)
        {
            Camera camComp = playerCamera.GetComponent<Camera>();
            float currentFov = camComp != null ? camComp.fieldOfView : 60f;

            playerVcam.transform.SetPositionAndRotation(playerCamera.position, playerCamera.rotation);
            playerVcam.Lens.FieldOfView = currentFov;
            playerVcam.Priority.Value = 20;

            Vector3 toTarget = targetLookPos - playerCamera.position;
            float stepDistance = Mathf.Clamp(toTarget.magnitude * 0.15f, 0.2f, 0.45f);
            Vector3 zoomPos = playerCamera.position + toTarget.normalized * stepDistance;
            Quaternion zoomRot = Quaternion.LookRotation(targetLookPos - zoomPos);

            talkZoomVcam.transform.SetPositionAndRotation(zoomPos, zoomRot);
            talkZoomVcam.Lens.FieldOfView = dialogueZoomFOV;
            talkZoomVcam.Priority.Value = 10;

            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                lookInDuration
            );
            cinemachineBrain.enabled = true;

            talkZoomVcam.Priority.Value = 30;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            yield return new WaitForSeconds(lookInDuration);
        }
        else if (playerCamera != null)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Quaternion startRot = playerCamera.rotation;
            float t = 0f;
            while (t < lookInDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / lookInDuration);
                Quaternion currentTarget = Quaternion.LookRotation(targetLookPos - playerCamera.position);
                playerCamera.rotation = Quaternion.Slerp(startRot, currentTarget, s);
                yield return null;
            }
            playerCamera.rotation = Quaternion.LookRotation(targetLookPos - playerCamera.position);
        }

        yield return new WaitForSeconds(delayBeforeDialogue);

        yield return StartCoroutine(conversation.Play());

        if (useCinemachine)
        {
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                lookOutDuration
            );
            talkZoomVcam.Priority.Value = 5;

            yield return new WaitForSeconds(lookOutDuration);

            cinemachineBrain.enabled = false;
        }
        else if (playerCamera != null)
        {
            Quaternion lookOutStart = playerCamera.rotation;
            Quaternion lookOutEnd = _fpsController != null ? _fpsController.transform.rotation : Quaternion.identity;

            float t2 = 0f;
            while (t2 < lookOutDuration)
            {
                t2 += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t2 / lookOutDuration);
                playerCamera.rotation = Quaternion.Slerp(lookOutStart, lookOutEnd, s);
                yield return null;
            }
            playerCamera.rotation = lookOutEnd;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_fpsController != null && playerCamera != null)
        {
            _fpsController.SnapViewRotation(playerCamera.rotation);
            _fpsController.SetControlLocked(false);
        }
        else if (playerController != null)
        {
            playerController.enabled = true;
        }
    }
}