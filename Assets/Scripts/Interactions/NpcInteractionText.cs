using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Cinemachine;

public class NpcInteractionText : MonoBehaviour
{
    [Header("UI & Settings")]
    public TextMeshProUGUI InteractText;
    public float InteractionDistance = 5f;
    private bool CanInteract = true;

    [Header("Cameras & Cinemachine")]
    public Transform playerCamera;
    public CinemachineBrain cinemachineBrain;
    public CinemachineCamera playerVcam;
    public CinemachineCamera talkZoomVcam;

    [Header("Cinematic Dialogue Zoom")]
    public float dialogueZoomFOV = 40f;
    public float blendInDuration = 0.6f;
    public float blendOutDuration = 0.5f;
    public Vector3 aimOffset = new Vector3(0, 1.3f, 0);

    [Header("Player Control")]
    public MonoBehaviour playerController;

    private FirstPersonController _fpsController;
    private Rigidbody _rigidbody;

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (playerController == null)
            playerController = GetComponent<FirstPersonController>();

        _fpsController = playerController as FirstPersonController;
        if (_fpsController == null && playerController != null)
            _fpsController = playerController.GetComponent<FirstPersonController>();
        if (_fpsController == null)
            _fpsController = GetComponent<FirstPersonController>();

        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (playerCamera == null && _fpsController != null && _fpsController.playerCamera != null)
            playerCamera = _fpsController.playerCamera.transform;

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

    void Update()
    {
        if (!CanInteract) return;

        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, InteractionDistance))
        {
            if (hit.collider.CompareTag("InteractNPC"))
            {
                InteractText.text = "Press 'E' To Talk";

                if (Input.GetKeyDown(KeyCode.E))
                {
                    NpcConversation npcConv =
                        hit.collider.GetComponent<NpcConversation>();

                    NpcLookAt npcLook =
                        hit.collider.GetComponent<NpcLookAt>();

                    if (npcConv != null)
                    {
                        StartCoroutine(TalkSequence(npcConv, npcLook));
                    }
                }
            }
            else
            {
                InteractText.text = "";
            }
        }
        else
        {
            InteractText.text = "";
        }
    }

    IEnumerator TalkSequence(NpcConversation conv, NpcLookAt look)
    {
        CanInteract = false;
        InteractText.text = "";

        ResolveReferences();

        // 1. Immediately halt movement and lock player input
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

        if (look != null)
            look.IKActive = true;

        // 2. Find target look position (head/eye level of the NPC)
        Transform npc = conv.transform;
        Vector3 targetLookPos = npc.position + aimOffset;

        // Try to find a dedicated head/look transform if present
        Transform head = npc.Find("Head") ?? npc.Find("head") ?? npc.Find("BeggarHead") ?? npc.Find("LookTarget");
        if (head != null)
        {
            targetLookPos = head.position;
        }
        else
        {
            var anim = npc.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null) targetLookPos = headBone.position;
            }
        }

        // 3. Setup Cinemachine Virtual Cameras if available
        bool useCinemachine = (cinemachineBrain != null && playerVcam != null && talkZoomVcam != null && playerCamera != null);

        if (useCinemachine)
        {
            // Position PlayerVcam to mirror current playerCamera state
            Camera camComp = playerCamera.GetComponent<Camera>();
            float currentFov = camComp != null ? camComp.fieldOfView : 60f;

            playerVcam.transform.SetPositionAndRotation(playerCamera.position, playerCamera.rotation);
            playerVcam.Lens.FieldOfView = currentFov;
            playerVcam.Priority.Value = 20;

            // Position TalkZoomVcam: subtle forward dolly and aim at NPC face
            Vector3 toNpc = targetLookPos - playerCamera.position;
            float stepDistance = Mathf.Clamp(toNpc.magnitude * 0.15f, 0.2f, 0.45f);
            Vector3 zoomPos = playerCamera.position + toNpc.normalized * stepDistance;
            Quaternion zoomRot = Quaternion.LookRotation(targetLookPos - zoomPos);

            talkZoomVcam.transform.SetPositionAndRotation(zoomPos, zoomRot);
            talkZoomVcam.Lens.FieldOfView = dialogueZoomFOV;
            talkZoomVcam.Priority.Value = 10;

            // Configure brain blend and activate
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                blendInDuration
            );
            cinemachineBrain.enabled = true;

            // Trigger blend to talk zoom camera
            talkZoomVcam.Priority.Value = 30;

            // Unlock cursor for dialogue interactions
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            yield return new WaitForSeconds(blendInDuration);
        }
        else if (playerCamera != null)
        {
            // Fallback smooth rotation if Cinemachine is not wired
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Quaternion startRot = playerCamera.rotation;
            Quaternion targetRot = Quaternion.LookRotation(targetLookPos - playerCamera.position);
            float t = 0f;
            while (t < blendInDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / blendInDuration);
                playerCamera.rotation = Quaternion.Slerp(startRot, targetRot, s);
                yield return null;
            }
            playerCamera.rotation = targetRot;
        }

        // 4. Play conversation
        yield return StartCoroutine(conv.Play());

        // 5. Blend back out
        if (useCinemachine)
        {
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                blendOutDuration
            );
            // Lower TalkZoomVcam priority so Cinemachine smoothly returns to PlayerVcam
            talkZoomVcam.Priority.Value = 5;

            yield return new WaitForSeconds(blendOutDuration);

            // Disable CinemachineBrain so FirstPersonController has direct, non-interfering camera control
            cinemachineBrain.enabled = false;
        }
        else if (playerCamera != null)
        {
            // Fallback return
            Quaternion lookOutStart = playerCamera.rotation;
            Quaternion targetRot = transform.rotation;
            float t2 = 0f;
            while (t2 < blendOutDuration)
            {
                t2 += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t2 / blendOutDuration);
                playerCamera.rotation = Quaternion.Slerp(lookOutStart, targetRot, s);
                yield return null;
            }
        }

        // 6. Disable NPC look-at
        if (look != null)
            look.IKActive = false;

        // 7. Re-lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 8. Synchronize player view and re-enable controls smoothly
        if (_fpsController != null && playerCamera != null)
        {
            _fpsController.SnapViewRotation(playerCamera.rotation);
            _fpsController.SetControlLocked(false);
        }
        else if (playerController != null)
        {
            playerController.enabled = true;
        }

        CanInteract = true;
    }
}