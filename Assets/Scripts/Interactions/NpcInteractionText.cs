using UnityEngine;
using TMPro;
using System.Collections;

public class NpcInteractionText : MonoBehaviour
{
    [Header("UI & Settings")]
    public TextMeshProUGUI InteractText;
    public float InteractionDistance = 5f;
    private bool CanInteract = true;

    [Header("Camera")]
    public Transform playerCamera;

    [Header("Look At")]
    public float lookInDuration = 0.6f;
    public float lookOutDuration = 0.4f;

    [Header("Player Control")]
    public MonoBehaviour playerController;

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

        // Disable player movement/look
        if (playerController != null)
            playerController.enabled = false;

        if (look != null)
            look.IKActive = true;

        // Save the camera's original rotation
        Quaternion startRot = playerCamera.rotation;

        // NPC position
        Transform npc = conv.transform;

        // Calculate rotation toward NPC
        Quaternion targetRot = Quaternion.LookRotation(
            npc.position - playerCamera.position
        );

        // Unlock cursor right as the camera starts switching to the NPC
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Smoothly look at NPC
        float t = 0f;

        while (t < lookInDuration)
        {
            t += Time.deltaTime;

            float s = Mathf.SmoothStep(
                0f,
                1f,
                t / lookInDuration
            );

            playerCamera.rotation = Quaternion.Slerp(
                startRot,
                targetRot,
                s
            );

            yield return null;
        }

        playerCamera.rotation = targetRot;

        // Play dialogue
        yield return StartCoroutine(conv.Play());

        // Look back to original direction
        Quaternion lookOutStart = playerCamera.rotation;

        float t2 = 0f;

        while (t2 < lookOutDuration)
        {
            t2 += Time.deltaTime;

            float s = Mathf.SmoothStep(
                0f,
                1f,
                t2 / lookOutDuration
            );

            playerCamera.rotation = Quaternion.Slerp(
                lookOutStart,
                startRot,
                s
            );

            yield return null;
        }

        playerCamera.rotation = startRot;

        // Disable NPC look-at
        if (look != null)
            look.IKActive = false;

        // Lock cursor again
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Enable player control
        if (playerController != null)
            playerController.enabled = true;

        CanInteract = true;
    }
}