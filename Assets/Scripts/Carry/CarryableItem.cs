using UnityEngine;

public class CarryableItem : MonoBehaviour
{
    [Header("Carry Settings")]
    public Transform carryPoint;
    public Vector3 localOffset = new Vector3(0.3f, -0.2f, 0.5f);
    public Vector3 localRotationEuler = Vector3.zero;

    private bool isBeingCarried = false;
    private Rigidbody rb;
    private Collider col;

    // Stores the object's own rotation offset relative to its "neutral" orientation
    private Quaternion baseRotationFix;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void StartCarrying(Transform holdPoint)
    {
        carryPoint = holdPoint;
        isBeingCarried = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (col != null)
        {
            col.enabled = false;
        }

        // Snap immediately, no lerp, no physics
        SnapToCarryPosition();
    }

    public void StopCarrying()
    {
        isBeingCarried = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        if (col != null)
        {
            col.enabled = true;
        }
    }

    void SnapToCarryPosition()
    {
        if (carryPoint == null) return;

        transform.position = carryPoint.TransformPoint(localOffset);
        transform.rotation = carryPoint.rotation * Quaternion.Euler(localRotationEuler);
    }

    void LateUpdate()
    {
        if (!isBeingCarried || carryPoint == null) return;

        // Hard-locked, static relative to camera — no smoothing, no drift
        transform.position = carryPoint.TransformPoint(localOffset);
        transform.rotation = carryPoint.rotation * Quaternion.Euler(localRotationEuler);
    }

    public bool IsBeingCarried => isBeingCarried;
}