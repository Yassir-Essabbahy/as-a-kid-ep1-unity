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
            var placeholder = hit.collider.GetComponent<InteractivePlaceholderItem>() ?? hit.collider.GetComponentInParent<InteractivePlaceholderItem>();
            if (placeholder != null && (!placeholder.oneTimeOnly || !placeholder.hasBeenInteracted))
            {
                InteractText.text = placeholder.promptText;
                if (Input.GetKeyDown(KeyCode.E))
                {
                    placeholder.TriggerInteraction();
                }
                return;
            }

            if (hit.collider.CompareTag("InteractNPC"))
            {
                InteractText.text = "Press 'E' To Talk";

                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (hit.collider.gameObject.name.Contains("Teddy") || (MetroStorySequenceController.Instance != null && hit.collider.gameObject == MetroStorySequenceController.Instance.teddyBearObject))
                    {
                        if (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.TryHandleTeddyInteraction())
                        {
                            return;
                        }
                    }

                    NpcConversation npcConv =
                        hit.collider.GetComponent<NpcConversation>();

                    NpcLookAt npcLook =
                        hit.collider.GetComponent<NpcLookAt>();

                    if (npcConv != null)
                    {
                        StartCoroutine(TalkSequence(npcConv, npcLook, hit.collider, hit.point));
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

    private Vector3 GetTargetLookPosition(NpcConversation conv, Collider hitCol, Vector3 hitPoint)
    {
        Transform npc = conv.transform;

        // 1. Check for dedicated look target children (e.g. BeggarHead, LookTarget, Head)
        Transform lookTarget = npc.Find("LookTarget") ?? npc.Find("lookTarget") ?? 
                               npc.Find("BeggarHead") ?? npc.Find("Head") ?? npc.Find("head");
        if (lookTarget != null)
            return lookTarget.position;

        // 2. Check for humanoid animator head bone
        var anim = npc.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
                return headBone.position;
        }

        // 3. If there is an animator with named head/face bones in generic rig
        if (anim != null)
        {
            var transforms = npc.GetComponentsInChildren<Transform>();
            foreach (var t in transforms)
            {
                string lower = t.name.ToLower();
                if (lower.Contains("head") || lower.Contains("face"))
                    return t.position;
            }

            var r = npc.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Bounds b = r.bounds;
                return new Vector3(b.center.x, b.min.y + b.size.y * 0.85f, b.center.z);
            }
        }

        // 4. For props, items, dolls, or objects (NO Animator, like Teddy Bear):
        // Prefer the exact visual MeshRenderer bounds center
        var rend = hitCol != null ? hitCol.GetComponent<Renderer>() : null;
        if (rend == null) rend = npc.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            return rend.bounds.center;
        }

        // 5. Use raycast hit point if available
        if (hitPoint != Vector3.zero)
            return hitPoint;

        // 6. Fallback to collider bounds
        Collider col = hitCol != null ? hitCol : npc.GetComponent<Collider>();
        if (col == null) col = npc.GetComponentInChildren<Collider>();
        if (col != null)
        {
            return col.bounds.center;
        }

        return npc.position;
    }

    IEnumerator TalkSequence(NpcConversation conv, NpcLookAt look, Collider hitCollider = null, Vector3 hitPoint = default)
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

        // 2. Find accurate target look position (face height or object center)
        Vector3 targetLookPos = GetTargetLookPosition(conv, hitCollider, hitPoint);

        // Keep cursor locked and invisible throughout dialogue
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

            // Position TalkZoomVcam at player's eye level (optical zoom, no positional displacement)
            Vector3 camPos = playerCamera.position;
            Vector3 lookDirection = targetLookPos - camPos;
            if (lookDirection.sqrMagnitude < 0.001f)
                lookDirection = playerCamera.forward;

            Quaternion zoomRot = Quaternion.LookRotation(lookDirection);

            talkZoomVcam.transform.SetPositionAndRotation(camPos, zoomRot);
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

            yield return new WaitForSeconds(blendInDuration);
        }
        else if (playerCamera != null)
        {
            // Fallback smooth rotation if Cinemachine is not wired
            Quaternion startRot = playerCamera.rotation;
            Vector3 lookDirection = targetLookPos - playerCamera.position;
            Quaternion targetRot = lookDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDirection) : playerCamera.rotation;
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

        // 7. Ensure cursor stays locked
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