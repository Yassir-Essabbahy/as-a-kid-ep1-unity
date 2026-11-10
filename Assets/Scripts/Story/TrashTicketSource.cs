using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Interactable trash bin / can in the metro station.
/// Acts as the ticket source: player walks up and interacts (keyboard, gamepad, or mobile button)
/// to start the timing search minigame.
/// 
/// Guarantees:
/// 1. Only interactable and only shows search prompt / minigame AFTER the Fusebox puzzle is finished.
/// 2. Exactly ONE random trash can contains the train ticket.
/// 3. Guaranteed Pity System: If player searches empty decoy cans, the remaining unsearched cans retain the ticket,
///    and if only 1 unsearched can remains, it is 100% guaranteed to contain the ticket.
///    It is mathematically impossible for all trash cans to be empty!
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
            EnsureTicketExists();
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
        // 1. Check Story Controller (Single Source of Truth)
        if (MetroStorySequenceController.Instance != null)
        {
            if (MetroStorySequenceController.Instance.transitPassCollected)
            {
                isSearched = true;
                return;
            }
            else
            {
                // Story controller is active and transit pass is NOT collected.
                // Reset any dirty or stale flags so old runs NEVER brick a fresh run!
                isSearched = false;
                hasTicket = false;
                if (questGiver != null && questGiver.questCompleted)
                {
                    questGiver.questCompleted = false;
                }
                if (PlayerPrefs.GetInt(playerPrefsKey, 0) == 1)
                {
                    PlayerPrefs.DeleteKey(playerPrefsKey);
                    PlayerPrefs.Save();
                }
                return;
            }
        }

        // Standalone scene fallback (no MetroStorySequenceController)
        bool prefsFound = PlayerPrefs.GetInt(playerPrefsKey, 0) == 1;
        bool questFound = questGiver != null && questGiver.questCompleted;
        if (prefsFound || questFound)
        {
            isSearched = true;
        }
        else
        {
            isSearched = false;
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

        // Ticket Minigame ONLY available after Fusebox Puzzle is solved
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

        // Guarantee ticket exists among unsearched cans before launching minigame
        EnsureTicketExists();

        // Safety fallback: If no other unsearched can in the scene has the ticket, this can MUST have it!
        if (!hasTicket && !HasOtherUnsearchedCanWithTicket(this))
        {
            hasTicket = true;
            Debug.Log($"[TrashTicketSource] Guaranteed ticket on '{gameObject.name}' since no other unsearched can holds it.");
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

        // Guarantee that a ticket is still placed in one of the remaining unsearched cans
        EnsureTicketExists();

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
    /// Checks whether any other unsearched trash can in the scene currently holds the ticket.
    /// </summary>
    public static bool HasOtherUnsearchedCanWithTicket(TrashTicketSource current)
    {
        var allCans = FindObjectsByType<TrashTicketSource>(FindObjectsSortMode.None);
        if (allCans == null) return false;

        foreach (var c in allCans)
        {
            if (c != null && c != current && !c.isSearched && c.hasTicket)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Guarantees that exactly one unsearched trash can in the scene contains the ticket.
    /// If only one unsearched can remains, that can is 100% guaranteed to contain the ticket.
    /// </summary>
    public static void EnsureTicketExists()
    {
        // Don't place ticket if transit pass is already collected
        if (MetroStorySequenceController.Instance != null && MetroStorySequenceController.Instance.transitPassCollected)
            return;

        var allCans = FindObjectsByType<TrashTicketSource>(FindObjectsSortMode.None);
        if (allCans == null || allCans.Length == 0)
        {
            Debug.LogWarning("[TrashTicketSource] No trash cans found in scene to place ticket.");
            return;
        }

        // Gather all unsearched cans
        var unsearched = new List<TrashTicketSource>();
        TrashTicketSource existingWinningCan = null;

        foreach (var c in allCans)
        {
            if (!c.isSearched)
            {
                unsearched.Add(c);
                if (c.hasTicket)
                {
                    if (existingWinningCan == null)
                    {
                        existingWinningCan = c;
                    }
                    else
                    {
                        // Clean up multiple winning cans to keep exactly 1
                        c.hasTicket = false;
                    }
                }
            }
            else
            {
                c.hasTicket = false;
            }
        }

        // Failsafe: if all cans were searched but ticket was never collected, reset cans to prevent softlock
        if (unsearched.Count == 0)
        {
            Debug.LogWarning("[TrashTicketSource] All cans were searched but ticket was never found! Resetting cans to prevent softlock.");
            foreach (var c in allCans)
            {
                c.isSearched = false;
                c.hasTicket = false;
                unsearched.Add(c);
            }
        }

        // If an unsearched can already holds the ticket, we are good!
        if (existingWinningCan != null && unsearched.Contains(existingWinningCan))
        {
            return;
        }

        // If only 1 unsearched can remains, it MUST have the ticket!
        if (unsearched.Count == 1)
        {
            unsearched[0].hasTicket = true;
            Debug.Log($"[TrashTicketSource] Only 1 unsearched trash can left ('{unsearched[0].gameObject.name}'). Guaranteed ticket placed here.");
            return;
        }

        // Otherwise pick one randomly among the unsearched cans
        int chosenIndex = Random.Range(0, unsearched.Count);
        unsearched[chosenIndex].hasTicket = true;

        Debug.Log($"[TrashTicketSource] Ticket placed randomly in trash can '{unsearched[chosenIndex].gameObject.name}' at {unsearched[chosenIndex].transform.position} (1 of {unsearched.Count} unsearched cans).");
    }

    /// <summary>
    /// Legacy wrapper for backwards compatibility.
    /// </summary>
    public static void SetupRandomTicketLocation()
    {
        EnsureTicketExists();
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
            string mine = GetPromptString();
            string emptyPrompt = LocalizationManager.Resolve(promptSearchedKey, "Trash Can (Empty)");
            string searchPrompt = LocalizationManager.Resolve(promptSearchKey, "Tap to Search Trash");
            if (cur == mine || cur == emptyPrompt || cur == searchPrompt)
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
