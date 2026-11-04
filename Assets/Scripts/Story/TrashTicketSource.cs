using TMPro;
using UnityEngine;

/// <summary>
/// Interactable trash bin / can in the metro station.
/// Acts as the ticket source: player walks up and interacts (keyboard, gamepad, or mobile button)
/// to start the timing search minigame.
/// On success: grants the transit pass, persists to PlayerPrefs, triggers ItemPickupToast,
/// and disables further searching.
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
            HookPromptButton();

            // Handle Input (PC / Gamepad / Mobile)
            if (CheckInteractInput())
            {
                Interact();
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
        if (isSearched)
        {
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
    /// Invoked by TimingMinigameUI when the player successfully stops the marker in the green zone.
    /// </summary>
    public void OnMinigameSuccess()
    {
        isSearched = true;
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

        ClearPromptIfOurs();
    }

    private string GetPromptString()
    {
        if (isSearched)
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
