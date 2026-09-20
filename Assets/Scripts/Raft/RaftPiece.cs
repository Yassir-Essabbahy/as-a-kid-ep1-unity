using UnityEngine;

/// <summary>
/// Represents a collectible raft building piece in the world.
/// Can be picked up, carried smoothly in front of the camera with physics disabled,
/// dropped, or placed at the RaftBuildArea.
/// </summary>
[DisallowMultipleComponent]
public class RaftPiece : MonoBehaviour
{
    [Header("Piece Identification")]
    [Tooltip("Unique ID matching the requiredPieceId in RaftBuildArea stages (e.g. Piece_01, Piece_02)")]
    public string pieceId = "Piece_01";

    [Tooltip("User-friendly display name shown in HUD prompts")]
    public string pieceName = "Raft Piece";

    [Header("Carry Position Settings")]
    [Tooltip("Offset relative to the camera's carry point while being held")]
    public Vector3 carryOffset = new Vector3(0.2f, -0.2f, 0.6f);

    [Tooltip("Euler rotation relative to the camera while being held")]
    public Vector3 carryRotation = Vector3.zero;

    [Header("Carry Scale Settings")]
    [Tooltip("Scale multiplier while being carried (useful for large items like beach mats/towels)")]
    public float carryScaleMultiplier = 1.0f;

    [Header("State")]
    [SerializeField] private bool isBeingCarried = false;
    [SerializeField] private bool isPlaced = false;

    // Component references
    private Rigidbody rb;
    private Collider[] colliders;
    private Transform carryPoint;
    private Vector3 originalScale;

    public bool IsBeingCarried => isBeingCarried;
    public bool IsPlaced => isPlaced;

    private void Awake()
    {
        isPlaced = false;
        isBeingCarried = false;
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>(true);
        originalScale = transform.localScale;
    }

    public void ResetPlaced()
    {
        isPlaced = false;
        isBeingCarried = false;
    }

    /// <summary>
    /// Pick up this piece and attach it smoothly to the camera carry point.
    /// Disables physics and collision so it won't push the player or get stuck.
    /// </summary>
    public void StartCarrying(Transform holdPoint)
    {
        if (isPlaced) return;

        carryPoint = holdPoint;
        isBeingCarried = true;

        // Disable physics simulation while carrying
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Disable colliders so it doesn't collide with the player or environment
        if (colliders != null)
        {
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        // Scale for carrying (e.g. folded mat)
        if (originalScale == Vector3.zero) originalScale = transform.localScale;
        transform.localScale = originalScale * carryScaleMultiplier;

        UpdateCarryTransform();
    }

    /// <summary>
    /// Drop this piece in the world if the player chooses to release it.
    /// </summary>
    public void Drop(Vector3 dropPosition, Vector3 dropDirection)
    {
        if (!isBeingCarried) return;

        isBeingCarried = false;
        carryPoint = null;

        transform.position = dropPosition;
        if (originalScale != Vector3.zero) transform.localScale = originalScale;

        // Re-enable colliders
        if (colliders != null)
        {
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = true;
            }
        }

        // Re-enable physics simulation
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = dropDirection * 2f;
            rb.angularVelocity = Random.insideUnitSphere * 1f;
        }
    }

    /// <summary>
    /// Called when successfully placed at the build area.
    /// Freezes the piece permanently or deactivates the loose item.
    /// </summary>
    public void PlacePermanently()
    {
        isBeingCarried = false;
        isPlaced = true;
        carryPoint = null;
        if (originalScale != Vector3.zero) transform.localScale = originalScale;

        // Deactivate the loose collectible item since the staged visual on the raft will take over
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (isBeingCarried && carryPoint != null)
        {
            UpdateCarryTransform();
        }
    }

    private void UpdateCarryTransform()
    {
        if (carryPoint == null) return;

        // Lock position and rotation relative to the camera carry point
        transform.position = carryPoint.TransformPoint(carryOffset);
        transform.rotation = carryPoint.rotation * Quaternion.Euler(carryRotation);
    }
}
