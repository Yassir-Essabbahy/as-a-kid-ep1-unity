using UnityEngine;

public class Floor3PaperInteractable : MonoBehaviour
{
    public float interactionDistance = 3.5f;
    public KeyCode interactKey = KeyCode.E;
    public string promptText = "Press 'E' to Inspect Paper";

    private bool playerInRange = false;

    private void Update()
    {
        var metro = MetroStorySequenceController.Instance;
        if (metro == null) return;

        bool isTruthOrDareActive = 
            metro.currentPhase == MetroStorySequenceController.StoryPhase.Floor3_DareObjective ||
            metro.currentPhase == MetroStorySequenceController.StoryPhase.Floor3_TruthResolution;

        if (!isTruthOrDareActive)
        {
            if (playerInRange)
            {
                playerInRange = false;
                if (NpcInteractionText.Instance != null) NpcInteractionText.Instance.ClearPrompt();
            }
            return;
        }

        if (PaperInspectController.Instance != null && PaperInspectController.Instance.isInspecting)
        {
            if (playerInRange)
            {
                playerInRange = false;
                if (NpcInteractionText.Instance != null) NpcInteractionText.Instance.ClearPrompt();
            }
            return;
        }

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;

        float dist = Vector3.Distance(transform.position, cam.position);
        if (dist <= interactionDistance)
        {
            Ray ray = new Ray(cam.position, cam.forward);
            bool isLooking = false;
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    isLooking = true;
                }
            }
            if (!isLooking && Vector3.Dot(cam.forward, (transform.position - cam.position).normalized) > 0.65f)
            {
                isLooking = true;
            }

            if (isLooking)
            {
                playerInRange = true;
                if (NpcInteractionText.Instance != null)
                {
                    NpcInteractionText.Instance.SetPrompt(promptText);
                }

                if (Input.GetKeyDown(interactKey))
                {
                    if (NpcInteractionText.Instance != null)
                    {
                        NpcInteractionText.Instance.ClearPrompt();
                    }

                    PaperInspectController.PaperMode mode = 
                        (metro.currentPhase == MetroStorySequenceController.StoryPhase.Floor3_DareObjective)
                        ? PaperInspectController.PaperMode.Dare_Math
                        : PaperInspectController.PaperMode.Truth_Document;

                    if (PaperInspectController.Instance != null)
                    {
                        PaperInspectController.Instance.OpenPaper(mode);
                    }
                }
                return;
            }
        }

        if (playerInRange)
        {
            playerInRange = false;
            if (NpcInteractionText.Instance != null)
            {
                NpcInteractionText.Instance.ClearPrompt();
            }
        }
    }
}
