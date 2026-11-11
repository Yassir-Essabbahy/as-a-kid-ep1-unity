using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class InteractivePlaceholderItem : MonoBehaviour
{
    public enum PlaceholderType
    {
        Floor1_TransitCard,
        Floor1_VendingMachine,
        Floor1_Intercom,
        Floor3_DepartureBoard,
        Floor3_LostTeddyPiece,
        Generic,
        Floor1_TicketBooth
    }

    [Header("Identity & Type")]
    public PlaceholderType itemType = PlaceholderType.Generic;
    public string promptText = "Press 'E' to Inspect";
    public string promptLocalizationKey = "";

    [Header("Dialogue Keys")]
    public string[] dialogueKeys;

    [Header("Audio & Effects")]
    public AudioSource audioSource;
    public AudioClip interactAudioClip;

    [Header("Behavior")]
    public bool isCollectible = false;
    public bool oneTimeOnly = true;
    public float interactionDistance = 3.0f;

    [Header("State")]
    public bool hasBeenInteracted = false;

    public event Action<InteractivePlaceholderItem> OnItemInteracted;

    private Transform playerTransform;
    private TextMeshProUGUI interactUI;

    private void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;

        var npcInteract = FindAnyObjectByType<NpcInteractionText>();
        if (npcInteract != null) interactUI = npcInteract.InteractText;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (string.IsNullOrEmpty(promptLocalizationKey))
        {
            promptLocalizationKey = GetDefaultPromptKey();
        }
    }

    private string GetDefaultPromptKey()
    {
        switch (itemType)
        {
            case PlaceholderType.Floor1_TransitCard: return "prompt_take_keycard";
            case PlaceholderType.Floor1_VendingMachine: return "prompt_inspect_vending";
            case PlaceholderType.Floor1_Intercom: return "prompt_listen_intercom";
            case PlaceholderType.Floor1_TicketBooth: return "ticket_booth_inspect_prompt";
            case PlaceholderType.Floor3_DepartureBoard: return "prompt_read_departure_board";
            case PlaceholderType.Floor3_LostTeddyPiece: return "prompt_take_teddy_ribbon";
            default: return "prompt_tap_interact";
        }
    }

    private string GetResolvedPrompt()
    {
        string k = !string.IsNullOrEmpty(promptLocalizationKey) ? promptLocalizationKey : GetDefaultPromptKey();
        return LocalizationManager.Resolve(k, promptText);
    }

    private void Update()
    {
        if (oneTimeOnly && hasBeenInteracted && isCollectible) return;
        if (playerTransform == null) return;

        string currentPrompt = GetResolvedPrompt();
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactionDistance)
        {
            if (NpcDialogueManager.Instance == null || !NpcDialogueManager.Instance.IsDialogueRunning)
            {
                if (NpcInteractionText.Instance != null && (!oneTimeOnly || !hasBeenInteracted))
                {
                    NpcInteractionText.Instance.SetPrompt(currentPrompt);
                }
                else if (interactUI != null && (!oneTimeOnly || !hasBeenInteracted))
                {
                    LocalizationManager.ApplyText(interactUI, currentPrompt, preserveAlignment: true);
                }

                if (Input.GetKeyDown(KeyCode.E) || ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame)
                {
                    TriggerInteraction();
                }
            }
        }
        else
        {
            if (NpcInteractionText.Instance != null &&
                (NpcInteractionText.Instance.GetCurrentPrompt() == currentPrompt || NpcInteractionText.Instance.GetCurrentPrompt() == promptText))
            {
                NpcInteractionText.Instance.ClearPrompt();
            }
            else if (interactUI != null && (interactUI.text == currentPrompt || interactUI.text == promptText))
            {
                interactUI.text = "";
            }
        }
    }

    public void TriggerInteraction()
    {
        if (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning) return;
        if (oneTimeOnly && hasBeenInteracted) return;
        hasBeenInteracted = true;

        if (NpcInteractionText.Instance != null)
            NpcInteractionText.Instance.ClearPrompt();

        if (interactUI != null)
            interactUI.text = "";

        if (audioSource != null && interactAudioClip != null)
        {
            audioSource.PlayOneShot(interactAudioClip);
        }

        OnItemInteracted?.Invoke(this);

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.OnPlaceholderInteracted(this);
        }

        if (dialogueKeys != null && dialogueKeys.Length > 0)
        {
            StartCoroutine(PlayDialogueRoutine());
        }
        else if (isCollectible)
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator PlayDialogueRoutine()
    {
        string[] lines = new string[dialogueKeys.Length];
        for (int i = 0; i < dialogueKeys.Length; i++)
        {
            lines[i] = MetroStorySequenceController.GetLoc(dialogueKeys[i]);
        }

        var diag = NpcDialogueManager.Instance;
        if (diag != null && Application.isPlaying)
        {
            yield return StartCoroutine(diag.ShowDialogue(
                lines,
                hasChoice: false,
                dialogueColor: new Color(0.9f, 0.95f, 1f),
                dialogueFont: null
            ));
        }

        if (isCollectible)
        {
            gameObject.SetActive(false);
        }
    }
}
