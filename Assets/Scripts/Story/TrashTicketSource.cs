using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Interactable trash bin / can in the metro station.
/// Acts as the ticket source: player walks up and interacts (keyboard, gamepad, or mobile button)
/// to start the timing search minigame.
/// 
/// Requirements:
/// 1. Only interactable and only shows search prompt / minigame AFTER the Fusebox puzzle is finished.
/// 2. Only ONE randomly chosen trash can in the scene contains the train ticket.
/// 3. Searching other trash cans gives empty trash feedback.
/// </summary>
[DisallowMultipleComponent]
public class TrashTicketSource : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactDistance = 2.8f;
    public KeyCode interactKey = KeyCode.E;
    public string playerTag = "Player";

    [Header("Quest Connection")]
    [Tooltip("Optional quest reference. If assigned, completing the trash search calls questGiver.CompleteQuest().")]
    public TeddyFindQuest questGiver;
    [Tooltip("If true, the bin can only be searched after the quest is active (after speaking with the NPC).")]
    public bool requireQuestActive = false;

    [Header("State & Persistence")]
    public bool hasTicket = false;
    public bool isSearched = false;
    [Tooltip("PlayerPrefs key to persist ticket collected state.")]
    public string playerPrefsKey = "Metro_TicketFound";

    [Header("Reward & Notification")]
    public Sprite ticketIcon;
    public int ticketAmount = 1;
    public string ticketLocalizationKey = "ticket_toast_label";
    public AudioClip ticketPickupSound;

    [Header("Localization Keys for Prompts")]
    public string promptSearchKey = "trash_search_prompt";
    public string promptSearchedKey = "trash_searched_prompt";

    [Header("References (Optional)")]
    public TimingMinigameUI minigameUI;

    public bool IsPlayerNear => _playerInRange;

    private Transform _playerTransform;
    private bool _playerInRange = false;
    private bool _isPromptButtonHooked = false;

    /// <summary>
    /// Checks whether the fusebox puzzle has been solved and elevator power is restored.
    /// </summary>
    public static bool IsFuseboxSolved
    {
        get
        {
            if (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.isPowerRestored)
                return true;
            if (MetroFuseBoxPuzzle.Instance != null && MetroFuseBoxPuzzle.Instance.isPowerRestored)
                return true;
            return false;
        }
    }

    private void Start()
    {
        ResolvePlayer();
        CheckExistingPersistence();

        if (minigameUI == null)
        {
            minigameUI = TimingMinigameUI.Instance;
        }

        // Try to load ticket sprite from Assets if none was manually assigned
        if (ticketIcon == null)
        {
            ticketIcon = Resources.Load<Sprite>("Textures/Train_Ticket");
        }

        // If fusebox is already solved on startup (e.g. loaded/resumed state),
        // ensure a random trash can holds the ticket.
        if (IsFuseboxSolved && !isSearched)
        {
            SetupRandomTicketLocation();
        }
    }

    private void ResolvePlayer()
    {
        var player = GameObject.FindWithTag(playerTag);
        if (player != null)
        {
            _playerTransform = player.transform;
            return;
        }

        var fps = FindAnyObjectByType<FirstPersonController>();
        if (fps != null)
        {
            _playerTransform = fps.transform;
        }
    }

    private void CheckExistingPersistence()
    {
        // Check PlayerPrefs
        bool prefsFound = PlayerPrefs.GetInt(playerPrefsKey, 0) == 1;

        // Check Story controller
        bool storyFound = MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.transitPassCollected;

        // Check Quest giver
        bool questFound = questGiver != null && questGiver.questCompleted;

        if (prefsFound || storyFound || questFound)
        {
            isSearched = true;
        }
    }

    private void OnDisable()
    {
        UnhookPromptButton();
        ClearPromptIfOurs();
    }

    private void Update()
    {
        if (minigameUI != null && minigameUI.IsActive)
        {
            UnhookPromptButton();
            ClearPromptIfOurs();
            return;
        }

        // REQUIREMENT 1: Neither the search prompt nor minigame canvas appear before the fusebox puzzle is completed
        if (!IsFuseboxSolved)
        {
            if (_playerInRange)
            {
                _playerInRange = false;
                UnhookPromptButton();
                ClearPromptIfOurs();
            }
            return;
        }

        if (_playerTransform == null)
        {
            ResolvePlayer();
            if (_playerTransform == null) return;
        }

        // Check distance to player
        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        bool inRange = dist <= interactDistance;

        if (inRange)
        {
            // Gate check: dialogue running
            if (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning)
            {
                UnhookPromptButton();
                ClearPromptIfOurs();
                return;
            }

            // Gate check: quest active requirement
            if (requireQuestActive && questGiver != null && !questGiver.questActive && !isSearched)
            {
                UnhookPromptButton();
                ClearPromptIfOurs();
                return;
            }

            _playerInRange = true;
            string promptText = GetPromptString();
            SetPrompt(promptText);

            bool alreadyDone = isSearched || (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.transitPassCollected);
            if (!alreadyDone)
            {
                HookPromptButton();

                // Handle Input (PC / Gamepad / Mobile)
                if (CheckInteractInput())
                {
                    Interact();
                }
            }
            else
            {
                UnhookPromptButton();
            }
        }
        else
        {
            if (_playerInRange)
            {
                _playerInRange = false;
                UnhookPromptButton();
                ClearPromptIfOurs();
            }
        }
    }

    private bool CheckInteractInput()
    {
        if (Input.GetKeyDown(interactKey) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.JoystickButton0))
        {
            return true;
        }

        return false;
    }

    private void HookPromptButton()
    {
        if (!_isPromptButtonHooked && NpcInteractionText.Instance != null && NpcInteractionText.Instance.promptButton != null)
        {
            NpcInteractionText.Instance.promptButton.onClick.AddListener(Interact);
            _isPromptButtonHooked = true;
        }
    }

    private void UnhookPromptButton()
    {
        if (_isPromptButtonHooked && NpcInteractionText.Instance != null && NpcInteractionText.Instance.promptButton != null)
        {
            NpcInteractionText.Instance.promptButton.onClick.RemoveListener(Interact);
            _isPromptButtonHooked = false;
        }
    }

    /// <summary>
    /// Starts interaction with the trash bin.
    /// Can be invoked by Update(), by mobile UI buttons, or from NpcInteractionText.
    /// </summary>
    public void Interact()
    {
        if (!IsFuseboxSolved)
        {
            return;
        }

        if (isSearched)
        {
            return;
        }

        if (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.transitPassCollected)
        {
            isSearched = true;
            return;
        }

        if (minigameUI == null)
        {
            minigameUI = TimingMinigameUI.Instance;
        }

        if (minigameUI != null)
        {
            UnhookPromptButton();
            ClearPromptIfOurs();
            minigameUI.StartMinigame(this);
        }
    }

    /// <summary>
    /// Invoked when the player successfully completes the minigame on the winning trash can.
    /// </summary>
    public void OnMinigameSuccessTicket()
    {
        isSearched = true;
        hasTicket = true;
        UnhookPromptButton();

        // 1. Persist to PlayerPrefs
        PlayerPrefs.SetInt(playerPrefsKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[TrashTicketSource] Ticket found and persisted to PlayerPrefs.");

        // 2. Update MetroStorySequenceController state (same flag the old pickup set)
        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.transitPassCollected = true;
            Debug.Log("[TrashTicketSource] transitPassCollected set on MetroStorySequenceController.");
            MetroStorySequenceController.Instance.CheckFloor1Progress();
        }

        // 3. Complete Quest if wired
        if (questGiver != null)
        {
            questGiver.CompleteQuest();
        }
        else
        {
            StartCoroutine(PlayTicketFoundDialogue());
        }

        // 4. Play Audio SFX
        if (ticketPickupSound != null)
        {
            AudioSource.PlayClipAtPoint(ticketPickupSound, transform.position, 1.0f);
        }

        // 5. Show RE-Style Item Pickup Toast
        string label = "Ticket";
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get(ticketLocalizationKey);
            if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("["))
            {
                label = loc;
            }
        }

        if (ItemPickupToast.Instance != null)
        {
            ItemPickupToast.Instance.Show(ticketIcon, label, ticketAmount);
        }

        // 6. Mark all other trash cans as searched so player does not need to search them
        var allCans = FindObjectsByType<TrashTicketSource>(FindObjectsSortMode.None);
        foreach (var can in allCans)
        {
            can.isSearched = true;
        }

        ClearPromptIfOurs();
    }

    /// <summary>
    /// Invoked when the player completes the minigame on an empty trash can.
    /// </summary>
    public void OnMinigameSuccessEmpty()
    {
        isSearched = true;
        hasTicket = false;
        UnhookPromptButton();
        ClearPromptIfOurs();

        Debug.Log($"[TrashTicketSource] Trash can at {transform.position} searched: Empty.");
        StartCoroutine(PlayEmptyCanTeddyComment());
    }

    /// <summary>
    /// Legacy entrypoint dispatched by TimingMinigameUI.
    /// </summary>
    public void OnMinigameSuccess()
    {
        if (hasTicket)
        {
            OnMinigameSuccessTicket();
        }
        else
        {
            OnMinigameSuccessEmpty();
        }
    }

    private IEnumerator PlayTicketFoundDialogue()
    {
        yield return new WaitForSeconds(0.35f);

        if (NpcDialogueManager.Instance != null)
        {
            string line1 = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("teddy_metro_05") : null;
            string line2 = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("teddy_metro_06") : null;

            if (string.IsNullOrEmpty(line1) || line1.StartsWith("["))
                line1 = $"{MetroStorySequenceController.TeddyName}: You found the ticket!";
            if (string.IsNullOrEmpty(line2) || line2.StartsWith("["))
                line2 = $"{MetroStorySequenceController.TeddyName}: Let's go, the elevator is ready for us!";

            yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                new string[] { line1, line2 },
                hasChoice: false,
                dialogueColor: NpcDialogueManager.GetSpeakerColor("teddy"),
                dialogueFont: NpcDialogueManager.DefaultDialogueFont
            ));
        }
    }

    private IEnumerator PlayEmptyCanTeddyComment()
    {
        yield return new WaitForSeconds(0.25f);

        if (NpcDialogueManager.Instance != null && !NpcDialogueManager.Instance.IsDialogueRunning)
        {
            string line = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("teddy_trash_empty_01") : null;
            if (string.IsNullOrEmpty(line) || line.StartsWith("["))
            {
                line = $"{MetroStorySequenceController.TeddyName}: Nothing in this one... let's check another trash can!";
            }

            yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                new string[] { line },
                hasChoice: false,
                dialogueColor: NpcDialogueManager.GetSpeakerColor("teddy"),
                dialogueFont: NpcDialogueManager.DefaultDialogueFont
            ));
        }
    }

    /// <summary>
    /// Places the train ticket randomly in exactly one unsearched trash can in the scene.
    /// </summary>
    public static void SetupRandomTicketLocation()
    {
        // Don't re-roll if ticket already found
        if (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.transitPassCollected)
            return;
        if (PlayerPrefs.GetInt("Metro_TicketFound", 0) == 1)
            return;

        var allCans = FindObjectsByType<TrashTicketSource>(FindObjectsSortMode.None);
        if (allCans == null || allCans.Length == 0)
        {
            Debug.LogWarning("[TrashTicketSource] No trash cans found in scene to place ticket.");
            return;
        }

        // Check if one can already has hasTicket == true and is unsearched
        int existingCount = 0;
        foreach (var c in allCans)
        {
            if (c.hasTicket && !c.isSearched) existingCount++;
        }
        if (existingCount == 1)
        {
            // Already set up and valid
            return;
        }

        // Reset all
        foreach (var c in allCans)
        {
            c.hasTicket = false;
        }

        // Filter to unsearched candidate cans
        var candidates = new List<TrashTicketSource>();
        foreach (var c in allCans)
        {
            if (!c.isSearched) candidates.Add(c);
        }

        if (candidates.Count == 0)
        {
            candidates.AddRange(allCans);
            foreach (var c in allCans) c.isSearched = false;
        }

        int chosenIndex = Random.Range(0, candidates.Count);
        candidates[chosenIndex].hasTicket = true;

        Debug.Log($"[TrashTicketSource] Ticket placed randomly in trash can '{candidates[chosenIndex].gameObject.name}' at {candidates[chosenIndex].transform.position} (1 of {candidates.Count} unsearched cans).");
    }

    private string GetPromptString()
    {
        if (isSearched || (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.transitPassCollected))
        {
            if (LocalizationManager.Instance != null)
            {
                string s = LocalizationManager.Instance.Get(promptSearchedKey);
                if (!string.IsNullOrEmpty(s) && !s.StartsWith("[")) return s;
            }
            return "Trash Can (Empty)";
        }
        else
        {
            if (LocalizationManager.Instance != null)
            {
                string s = LocalizationManager.Instance.Get(promptSearchKey);
                if (!string.IsNullOrEmpty(s) && !s.StartsWith("[")) return s;
            }
            return "Press 'E' to Search Trash";
        }
    }

    private void SetPrompt(string text)
    {
        if (NpcInteractionText.Instance != null)
        {
            NpcInteractionText.Instance.SetPrompt(text);
        }
    }

    private void ClearPromptIfOurs()
    {
        if (NpcInteractionText.Instance != null)
        {
            string cur = NpcInteractionText.Instance.GetCurrentPrompt();
            if (cur == GetPromptString() || cur.Contains("Search") || cur.Contains("Trash"))
            {
                NpcInteractionText.Instance.ClearPrompt();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = hasTicket ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
