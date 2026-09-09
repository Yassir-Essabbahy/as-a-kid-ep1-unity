using UnityEngine;

public class CowMove : MonoBehaviour
{
    public Transform pointB;
    public float speed = 1.5f;
    public float rotationSpeed = 5f;

    void Update()
    {
        Vector3 direction = pointB.position - transform.position;
        direction.y = 0f;

        if (direction.magnitude > 0.05f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            transform.position = Vector3.MoveTowards(
                transform.position,
                pointB.position,
                speed * Time.deltaTime
            );
        }
    }
} 