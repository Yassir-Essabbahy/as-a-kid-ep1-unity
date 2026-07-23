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

    [Header("Cameras")]
    public CinemachineVirtualCamera PlayerVcam;
    public CinemachineVirtualCamera TalkZoomVcam;

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
                    NpcConversation npcConv = hit.collider.GetComponent<NpcConversation>();
                    NpcLookAt npcLook = hit.collider.GetComponent<NpcLookAt>();

                    if (npcConv != null)
                        StartCoroutine(TalkSequence(npcConv, npcLook));
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

        if (look != null) look.IKActive = true;

        PlayerVcam.Priority = 0;
        TalkZoomVcam.Priority = 10;

        yield return new WaitForSeconds(1f);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return StartCoroutine(conv.Play());

        PlayerVcam.Priority = 10;
        TalkZoomVcam.Priority = 0;
        if (look != null) look.IKActive = false;
        Cursor.lockState = CursorLockMode.Locked;

        CanInteract = true;
    }
}