using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the raft building site on the shore.
/// Accepts raft pieces one-by-one in a defined sequence, activates the corresponding
/// raft parts, and fires an event when construction is finished.
/// </summary>
[DisallowMultipleComponent]
public class RaftBuildArea : MonoBehaviour
{
    [System.Serializable]
    public class BoatPartStage
    {
        [Tooltip("Readable name for this part (e.g. 'Foundation Logs', 'Main Decking', 'Mast & Sail')")]
        public string partName = "Boat Part";

        [Tooltip("Required piece ID ('Wood' for wooden stages, 'BuildMat' for the sail cloth)")]
        public string requiredPieceId = "Wood";

        [Tooltip("Visual GameObjects on the raft revealed when this stage is discovered")]
        public GameObject[] visualObjects;
    }

    [Header("Boat Progression (4 Stages)")]
    [Tooltip("The 4 progressive parts of the boat to discover")]
    public List<BoatPartStage> stages = new List<BoatPartStage>();

    [Header("Current Progress")]
    [SerializeField] private int woodPlacedCount = 0;
    [SerializeField] private bool isSailPlaced = false;
    [SerializeField] private int currentStageIndex = 0;
    [SerializeField] private bool isCompleted = false;

    [Header("Interaction & Events")]
    [Tooltip("Distance within which the player can interact with the build area")]
    public float interactionDistance = 4.5f;

    [Tooltip("Fired each time a piece is placed (passes stage number 1-4)")]
    public UnityEvent<int> OnStageCompleted;

    [Tooltip("Hook for story sequence when the entire raft is finished")]
    public UnityEvent OnRaftCompleted;

    public int WoodPlacedCount => woodPlacedCount;
    public int WoodRequiredCount => 3;
    public bool IsSailPlaced => isSailPlaced;
    public int CurrentStageIndex => currentStageIndex;
    public int TotalStages => stages.Count;
    public bool IsCompleted => isCompleted;

    private void Awake()
    {
        InitializeVisuals();
    }

    /// <summary>
    /// Deactivates all boat part visuals at startup so the boat starts empty.
    /// </summary>
    public void InitializeVisuals()
    {
        woodPlacedCount = 0;
        isSailPlaced = false;
        currentStageIndex = 0;
        isCompleted = false;
        for (int i = 0; i < stages.Count; i++)
        {
            bool shouldBeActive = (i < currentStageIndex);
            if (stages[i].visualObjects != null)
            {
                foreach (var obj in stages[i].visualObjects)
                {
                    if (obj != null) obj.SetActive(shouldBeActive);
                }
            }
        }
    }

    /// <summary>
    /// Gets the next stage waiting to be discovered, or null if complete.
    /// </summary>
    public BoatPartStage GetCurrentStage()
    {
        if (isCompleted || currentStageIndex >= stages.Count) return null;
        return stages[currentStageIndex];
    }

    /// <summary>
    /// Checks whether the supplied piece matches the requirement for the current stage.
    /// </summary>
    public bool CanPlacePiece(RaftPiece piece)
    {
        if (isCompleted || piece == null) return false;
        var stage = GetCurrentStage();
        if (stage == null) return false;

        if (!string.IsNullOrEmpty(stage.requiredPieceId))
        {
            return string.Equals(stage.requiredPieceId, piece.pieceId, StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }

    /// <summary>
    /// Places the held wood piece and discovers the next part of the boat.
    /// </summary>
    public bool TryPlacePiece(RaftPiece piece)
    {
        if (!CanPlacePiece(piece)) return false;

        var currentStage = GetCurrentStage();
        if (currentStage == null) return false;

        // 1. Consume the held wood piece
        piece.PlacePermanently();

        // 2. Reveal the next part of the boat
        if (currentStage.visualObjects != null)
        {
            foreach (var obj in currentStage.visualObjects)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        if (string.Equals(piece.pieceId, "BuildMat", StringComparison.OrdinalIgnoreCase))
        {
            isSailPlaced = true;
        }
        else
        {
            woodPlacedCount++;
        }

        currentStageIndex++;
        Debug.Log($"[RaftBuildArea] Placed '{piece.pieceName}' ({currentStageIndex}/{stages.Count}) - Discovered: {currentStage.partName}");

        // 3. Fire progress event
        OnStageCompleted?.Invoke(currentStageIndex);

        // 4. Check if fully finished (all 4 parts discovered)
        if (currentStageIndex >= stages.Count)
        {
            isCompleted = true;
            Debug.Log("[RaftBuildArea] *** BOAT COMPLETE (4/4)! *** Firing OnRaftCompleted event.");
            OnRaftCompleted?.Invoke();
        }

        return true;
    }
}
