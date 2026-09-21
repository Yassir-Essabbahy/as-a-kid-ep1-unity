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

    [Header("Current State")]
    [SerializeField] private RaftPiece currentHeldPiece = null;

    public RaftPiece CurrentHeldPiece => currentHeldPiece;
    public bool IsCarryingPiece => currentHeldPiece != null;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        ClearPrompt();
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
            // Look for existing CarryPoint or create one automatically
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
            var allTMPs = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
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
            var allPieces = FindObjectsByType<RaftPiece>(FindObjectsSortMode.None);
            float closestDist = float.MaxValue;
            foreach (var p in allPieces)
            {
                if (p != null && !p.IsPlaced && !p.IsBeingCarried && p.gameObject.activeInHierarchy)
                {
                    float d = Vector3.Distance(playerCamera.transform.position, p.transform.position);
                    if (d <= interactionDistance)
                    {
                        Vector3 toPiece = (p.transform.position - playerCamera.transform.position).normalized;
                        // Facing generally toward the piece
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
                SetPrompt("E to pick up");

                if (Input.GetKeyDown(interactKey))
                {
                    PickUpPiece(hitPiece);
                }
            }
            else if (hitPiece != currentHeldPiece)
            {
                SetPrompt("Drop item first");
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
                    SetPrompt("E to get on");

                    if (Input.GetKeyDown(interactKey))
                    {
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
                    SetPrompt("E to place");

                    if (Input.GetKeyDown(interactKey))
                    {
                        PlacePieceAtRaft(hitBuildArea);
                    }
                }
                else
                {
                    if (currentHeldPiece.pieceId.ToLower().Contains("mat"))
                    {
                        SetPrompt("Need wood first");
                    }
                    else
                    {
                        SetPrompt("Need cloth");
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
            SetPrompt("Q to drop");
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
        SetPrompt("Q to drop");
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

        objectiveTrackerText.text = $"Wood: {woodCount}/3\nCloth: {clothCount}/1";
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
            promptText.text = text;
            promptText.gameObject.SetActive(true);
        }

        if (promptBox != null)
        {
            promptBox.SetActive(true);
            if (promptBoxRect != null)
            {
                float width = 160f;
                if (text.Length > 12) width = 190f;
                else if (text.Length < 10) width = 130f;
                promptBoxRect.sizeDelta = new Vector2(width, 36f);
            }
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
    }
}
