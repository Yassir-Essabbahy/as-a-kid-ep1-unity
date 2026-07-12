using UnityEngine;

public class NPCFollow : MonoBehaviour
{
    public Transform player;
    public float speed = 3f;
    public float rotationSpeed = 5f;

    void Update()
    {
        if (player == null) return;

        // Follow without changing height
        Vector3 target = new Vector3(
            player.position.x,
            transform.position.y,
            player.position.z
        );

        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            speed * Time.deltaTime
        );

        // Face the player only on the Y axis
        Vector3 direction = target - transform.position;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
}