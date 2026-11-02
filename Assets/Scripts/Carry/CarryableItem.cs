using UnityEngine;

public class CarryableItem : MonoBehaviour
{
    [Header("Carry Settings")]
    public Transform carryPoint;
    public Vector3 localOffset = new Vector3(0.3f, -0.2f, 0.5f);
    public Vector3 localRotationEuler = Vector3.zero;

    [Header("Safety Reset")]
    public Vector3 safeResetPosition = Vector3.zero;
    public float minYThreshold = -2f;

    private bool isBeingCarried = false;
    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        if (safeResetPosition == Vector3.zero)
        {
            safeResetPosition = transform.position;
        }
    }

    public void SetSafeResetPosition(Vector3 pos)
    {
        safeResetPosition = pos;
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

        SnapToCarryPosition();
    }

    public void StopCarrying()
    {
        isBeingCarried = false;
        carryPoint = null;

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

    public void Drop(Vector3 dropPosition, Vector3 dropDirection)
    {
        if (!isBeingCarried) return;
        isBeingCarried = false;
        carryPoint = null;

        transform.position = dropPosition;

        if (col != null) col.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = dropDirection * 2f;
            rb.angularVelocity = Random.insideUnitSphere;
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
        if (isBeingCarried && carryPoint != null)
        {
            transform.position = carryPoint.TransformPoint(localOffset);
            transform.rotation = carryPoint.rotation * Quaternion.Euler(localRotationEuler);
        }
        else if (!isBeingCarried && transform.position.y < minYThreshold)
        {
            RecoverToSafeSpot();
        }
    }

    public void RecoverToSafeSpot()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        transform.position = safeResetPosition;
    }

    public bool IsBeingCarried => isBeingCarried;
}
