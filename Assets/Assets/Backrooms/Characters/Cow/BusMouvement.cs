using UnityEngine;

public class BusMovement : MonoBehaviour
{
    public Transform[] points;
    public float speed = 5f;
    public float stopDistance = 0.1f;

    private int currentPoint = 0;
    private bool waiting = false;

    void Update()
    {
        // Move toward current point
        if (!waiting && currentPoint < points.Length)
        {
            Vector3 direction = points[currentPoint].position - transform.position;
            direction.y = 0f;

            // Move
            transform.position = Vector3.MoveTowards(
                transform.position,
                points[currentPoint].position,
                speed * Time.deltaTime
            );

            // Rotate toward point
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    5f * Time.deltaTime
                );
            }

            // Arrived
            if (Vector3.Distance(transform.position, points[currentPoint].position) <= stopDistance)
            {
                waiting = true;
            }
        }

        // Press F to continue
        if (waiting && Input.GetKeyDown(KeyCode.F))
        {
            currentPoint++;
            waiting = false;
        }
    }
}