using TMPro;
using UnityEngine;

/// <summary>
/// Attached to the player or camera to handle looking at raft pieces,
/// picking them up, carrying them, and placing them onto the RaftBuildArea.
/// Enforces the rule that the player can only carry one piece at a time.
/// </summary>
[DisallowMultipleComponent]
public class RaftPlayerInteraction : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player's camera used for interaction raycasting")]
    public Camera playerCamera;

    [Tooltip("Transform in front of the camera where carried pieces rest")]
    public Transform carryPoint;

    [Tooltip("HUD text to display interaction instructions")]
    public TextMeshProUGUI promptText;

    [Tooltip("Container box with black border")]
    public GameObject promptBox;
    public RectTransform promptBoxRect;

    [Tooltip("Top-left HUD text to display objectives (e.g. Wood 0/3, Cloth 0/1)")]
    public TextMeshProUGUI objectiveTrackerText;

    [Header("Interaction Settings")]
    [Tooltip("Maximum distance for interacting with pieces and the build area")]
    public float interactionDistance = 4.0f;

    [Tooltip("SphereCast radius for effortless grabbing of ground items (mats, wood)")]
    public float grabRadius = 0.45f;

    [Tooltip("Key to pick up or place a piece")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Key to drop the currently held piece")]
    public KeyCode dropKey = KeyCode.Q;

    [Tooltip("Shared interaction input; assign the mobile button component when available")]
    public InteractInput interactInput;

    [Header("Current State")]
    [SerializeField] private RaftPiece currentHeldPiece = null;

    public RaftPiece CurrentHeldPiece => currentHeldPiece;
    public bool IsCarryingPiece => currentHeldPiece != null;

    private void Awake()
    {
        ResolveReferences();
        if (interactInput == null) interactInput = GetComponent<InteractInput>();
        if (interactInput == null) interactInput = gameObject.AddComponent<InteractInput>();
        interactInput.SetKey(interactKey);
    }

    private void Start()
    {
        ResolveReferences();
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }
        ClearPrompt();
        UpdateObjectiveTracker();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }
    }

    private void HandleLanguageChanged(string _)
    {
        UpdateObjectiveTracker();
    }

    public void ResolveReferences()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;
        }

        if (playerCamera != null && carryPoint == null)
        {
            var existingPoint = playerCamera.transform.Find("CarryPoint");
            if (existingPoint != null)
            {
                carryPoint = existingPoint;
            }
            else
            {
                GameObject cpObj = new GameObject("CarryPoint");
                cpObj.transform.SetParent(playerCamera.transform, false);
                cpObj.transform.localPosition = new Vector3(0.35f, -0.28f, 0.75f);
                cpObj.transform.localRotation = Quaternion.identity;
                carryPoint = cpObj.transform;
            }
        }

        if (promptBox == null)
        {
            var pBox = GameObject.Find("PromptBox");
            if (pBox != null)
            {
                promptBox = pBox;
                promptBoxRect = pBox.GetComponent<RectTransform>();
            }
        }

        if (promptText == null || objectiveTrackerText == null)
        {
            var allTMPs = FindObjectsByType<TextMeshProUGUI>();
            foreach (var tmp in allTMPs)
            {
                string n = tmp.gameObject.name.ToLower();
                if (promptText == null && (n.Contains("interact") || n.Contains("prompt")))
                {
                    promptText = tmp;
                }
                if (objectiveTrackerText == null && (n.Contains("tracker") || n.Contains("objective")))
                {
                    objectiveTrackerText = tmp;
                }
            }
        }
    }

    private void Update()
    {
        // 1. Handle Drop Input
        if (currentHeldPiece != null && Input.GetKeyDown(dropKey))
        {
            DropCurrentPiece();
            return;
        }

        // 2. Raycast & SphereCast to detect interactive objects
        PerformInteractionCheck();
    }

    private void PerformInteractionCheck()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, interactionDistance);

        RaftPiece hitPiece = null;
        RaftBuildArea hitBuildArea = null;

        if (hitSomething)
        {
            hitPiece = hit.collider.GetComponentInParent<RaftPiece>();
            hitBuildArea = hit.collider.GetComponentInParent<RaftBuildArea>();
        }

        // Generous SphereCast fallback for effortless grabbing of pieces like the mat or wood
        if (hitPiece == null && hitBuildArea == null)
        {
            if (Physics.SphereCast(ray, grabRadius, out RaycastHit sphereHit, interactionDistance))
            {
                var spPiece = sphereHit.collider.GetComponentInParent<RaftPiece>();
                var spArea = sphereHit.collider.GetComponentInParent<RaftBuildArea>();
                if (spPiece != null || spArea != null)
                {
                    hit = sphereHit;
                    hitPiece = spPiece;
                    hitBuildArea = spArea;
                    hitSomething = true;
                }
            }
        }

        // Direct proximity fallback: look around within interaction range for any unplaced piece
        if (hitPiece == null && currentHeldPiece == null)
        {
            var allPieces = FindObjectsByType<RaftPiece>();
            float closestDist = float.MaxValue;
            foreach (var p in allPieces)
            {
                if (p != null && !p.IsPlaced && !p.IsBeingCarried && p.gameObject.activeInHierarchy)
                {
                    float d = Vector3.Distance(playerCamera.transform.position, p.transform.position);
                    if (d <= interactionDistance)
                    {
                        Vector3 toPiece = (p.transform.position - playerCamera.transform.position).normalized;
                        if (Vector3.Dot(playerCamera.transform.forward, toPiece) > 0.55f && d < closestDist)
                        {
                            closestDist = d;
                            hitPiece = p;
                        }
                    }
                }
            }
        }

        // Proximity fallback for RaftBuildArea if looking close by
        if (hitBuildArea == null)
        {
            var area = FindAnyObjectByType<RaftBuildArea>();
            if (area != null && Vector3.Distance(transform.position, area.transform.position) <= area.interactionDistance)
            {
                Vector3 toArea = (area.transform.position - playerCamera.transform.position).normalized;
                if (Vector3.Dot(playerCamera.transform.forward, toArea) > 0.4f)
                {
                    hitBuildArea = area;
                }
            }
        }

        // Case A: Looking at a RaftPiece
        if (hitPiece != null && !hitPiece.IsPlaced)
        {
            if (currentHeldPiece == null)
            {
                SetPrompt(LocalizationManager.Resolve("raft_prompt_pickup", $"{interactKey} to pick up"));

                if (interactInput != null && interactInput.WasPressed)
                {
                    interactInput.ConsumePress();
                    PickUpPiece(hitPiece);
                }
            }
            else if (hitPiece != currentHeldPiece)
            {
                SetPrompt(LocalizationManager.Resolve("raft_prompt_drop", $"{dropKey} to drop held item"));
            }
            return;
        }

        // Case B: Looking at or standing by RaftBuildArea
        if (hitBuildArea != null)
        {
            if (hitBuildArea.IsCompleted)
            {
                if (BeachEndingController.Instance != null && BeachEndingController.Instance.CanBoardRaft)
                {
                    SetPrompt(LocalizationManager.Resolve("raft_prompt_board", $"{interactKey} to get on"));

                    if (interactInput != null && interactInput.WasPressed)
                    {
                        interactInput.ConsumePress();
                        ClearPrompt();
                        BeachEndingController.Instance.StartRaftDeparture();
                    }
                }
                else
                {
                    ClearPrompt();
                }
                return;
            }
            else if (currentHeldPiece != null)
            {
                if (hitBuildArea.CanPlacePiece(currentHeldPiece))
                {
                    string placeStr = LocalizationManager.Resolve("raft_prompt_place", $"{interactKey} to place");
                    string dropStr = LocalizationManager.Resolve("raft_prompt_drop_short", $"{dropKey} to drop");
                    SetPrompt($"{placeStr} / {dropStr}");

                    if (interactInput != null && interactInput.WasPressed)
                    {
                        interactInput.ConsumePress();
                        PlacePieceAtRaft(hitBuildArea);
                    }
                }
                else
                {
                    string dropStr = LocalizationManager.Resolve("raft_prompt_drop_short", $"{dropKey} to drop");
                    if (currentHeldPiece.pieceId.ToLower().Contains("mat"))
                    {
                        string needWood = LocalizationManager.Resolve("raft_prompt_need_wood", "Need wood first");
                        SetPrompt($"{needWood} / {dropStr}");
                    }
                    else
                    {
                        string needCloth = LocalizationManager.Resolve("raft_prompt_need_cloth", "Need cloth");
                        SetPrompt($"{needCloth} / {dropStr}");
                    }
                }
            }
            else
            {
                ClearPrompt();
            }
            return;
        }

        // Case C: No interactive target in view
        if (currentHeldPiece != null)
        {
            SetPrompt(LocalizationManager.Resolve("raft_prompt_drop_short", $"{dropKey} to drop"));
        }
        else
        {
            ClearPrompt();
        }
    }

    private void PickUpPiece(RaftPiece piece)
    {
        if (piece == null || currentHeldPiece != null) return;

        currentHeldPiece = piece;
        currentHeldPiece.StartCarrying(carryPoint);
        SetPrompt(LocalizationManager.Resolve("raft_prompt_drop_short", $"{dropKey} to drop"));
    }

    private void DropCurrentPiece()
    {
        if (currentHeldPiece == null) return;

        Vector3 dropPos = playerCamera.transform.position + playerCamera.transform.forward * 0.9f;
        currentHeldPiece.Drop(dropPos, playerCamera.transform.forward);
        currentHeldPiece = null;
        ClearPrompt();
    }

    private void PlacePieceAtRaft(RaftBuildArea buildArea)
    {
        if (currentHeldPiece == null || buildArea == null) return;

        RaftPiece pieceToPlace = currentHeldPiece;
        if (buildArea.TryPlacePiece(pieceToPlace))
        {
            currentHeldPiece = null;
            ClearPrompt();
            UpdateObjectiveTracker();
        }
    }

    public void UpdateObjectiveTracker()
    {
        if (objectiveTrackerText == null) return;

        var buildArea = FindAnyObjectByType<RaftBuildArea>();
        int woodCount = buildArea != null ? buildArea.WoodPlacedCount : 0;
        int clothCount = (buildArea != null && buildArea.IsSailPlaced) ? 1 : 0;

        string trackerStr = LocalizationManager.Format("raft_tracker_progress", "Wood: {0}/3\nCloth: {1}/1", woodCount, clothCount);
        LocalizationManager.ApplyText(objectiveTrackerText, trackerStr, preserveAlignment: true);
    }

    private void SetPrompt(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            ClearPrompt();
            return;
        }

        if (promptText != null)
        {
            LocalizationManager.ApplyText(promptText, text, preserveAlignment: true);
            promptText.gameObject.SetActive(true);
        }

        if (promptBox != null)
        {
            promptBox.SetActive(true);
            if (promptBoxRect != null)
            {
                float width = Mathf.Max(130f, promptText != null ? promptText.GetPreferredValues(promptText.text).x + 36f : 190f);
                promptBoxRect.sizeDelta = new Vector2(width, 36f);
            }
        }

        if (interactInput != null)
        {
            interactInput.SetInteractionAvailable(true, text);
        }
    }

    private void ClearPrompt()
    {
        if (promptText != null)
        {
            promptText.text = "";
        }
        if (promptBox != null)
        {
            promptBox.SetActive(false);
        }
        if (interactInput != null)
        {
            interactInput.SetInteractionAvailable(false);
        }
    }
}
