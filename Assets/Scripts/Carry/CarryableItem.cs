using UnityEngine;

public class CarryableItem : MonoBehaviour
{
    [Header("Carry Settings")]
    public Transform carryPoint;      // assign player's hold point (e.g. under camera)
    public Vector3 localOffset = new Vector3(0.3f, -0.2f, 0.5f);
    public Vector3 localRotationEuler = Vector3.zero;
    public float followSpeed = 12f;

    private bool isBeingCarried = false;
    private Rigidbody rb;
    private Collider col;

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
        }
        if (col != null)
        {
            col.enabled = false; // stop it blocking raycasts / colliding with player
        }
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

    void LateUpdate()
    {
        if (!isBeingCarried || carryPoint == null) return;

        Vector3 targetPos = carryPoint.TransformPoint(localOffset);
        Quaternion targetRot = carryPoint.rotation * Quaternion.Euler(localRotationEuler);

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followSpeed);
    }

    public bool IsBeingCarried => isBeingCarried;
}