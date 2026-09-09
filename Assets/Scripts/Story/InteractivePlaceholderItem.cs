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
        Generic
    }

    [Header("Identity & Type")]
    public PlaceholderType itemType = PlaceholderType.Generic;
    public string promptText = "Press 'E' to Inspect";

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
    }

    private void Update()
    {
        if (oneTimeOnly && hasBeenInteracted && isCollectible) return;
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactionDistance)
        {
            if (interactUI != null && (!oneTimeOnly || !hasBeenInteracted))
            {
                interactUI.text = promptText;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                TriggerInteraction();
            }
        }
    }

    public void TriggerInteraction()
    {
        if (oneTimeOnly && hasBeenInteracted) return;
        hasBeenInteracted = true;

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
