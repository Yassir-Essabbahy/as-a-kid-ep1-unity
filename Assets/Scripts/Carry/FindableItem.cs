using UnityEngine;

public class FindableItem : MonoBehaviour
{
    public TeddyFindQuest questGiver; // assign the object holding TeddyFindQuest
    public float interactDistance = 3f;
    public string promptText = "Press 'E' to Take Train Ticket";
    public bool isTrainTicket = true;
    public AudioClip pickupSound;
    private bool collected = false;

    public bool IsCollected => collected;

    private Transform playerTransform;

    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        if (collected) return;

        Transform targetT = playerTransform != null ? playerTransform : (Camera.main != null ? Camera.main.transform : null);
        if (targetT == null) return;

        float dist = Vector3.Distance(transform.position, targetT.position);
        if (dist <= interactDistance)
        {
            if (NpcDialogueManager.Instance == null || !NpcDialogueManager.Instance.IsDialogueRunning)
            {
                if (NpcInteractionText.Instance != null)
                {
                    NpcInteractionText.Instance.SetPrompt(promptText);
                }

                if (Input.GetKeyDown(KeyCode.E))
                {
                    Collect();
                }
            }
        }
        else
        {
            if (NpcInteractionText.Instance != null && NpcInteractionText.Instance.GetCurrentPrompt() == promptText)
            {
                NpcInteractionText.Instance.ClearPrompt();
            }
        }
    }

    public void Collect()
    {
        if (collected) return;
        collected = true;

        if (NpcInteractionText.Instance != null)
            NpcInteractionText.Instance.ClearPrompt();

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
        }

        gameObject.SetActive(false);

        if (questGiver != null)
        {
            questGiver.CompleteQuest();
        }

        if (isTrainTicket && MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.transitPassCollected = true;
            Debug.Log("[MetroStory] Train Ticket (ElementToFind) collected!");
            MetroStorySequenceController.Instance.CheckFloor1Progress();
        }
    }
}