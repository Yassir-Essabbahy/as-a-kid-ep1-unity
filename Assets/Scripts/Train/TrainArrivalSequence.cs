using UnityEngine;

public class TrainArrivalSequence : MonoBehaviour
{
    [Header("Points")]
    public Transform pointA;
    public Transform pointB;

    [Header("Timing")]
    public float delayBeforeMove = 3f;
    public float speed = 5f; // adjustable live in Inspector

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip arrivalSound;   // plays immediately at t=0
    public AudioClip moveSound;      // plays once movement starts, loops

    private bool isMoving = false;
    private bool hasArrived = false;

    void Start()
    {
        if (pointA != null)
            transform.position = pointA.position;

        if (audioSource != null && arrivalSound != null)
        {
            audioSource.clip = arrivalSound;
            audioSource.loop = false;
            audioSource.Play();
        }

        Invoke(nameof(BeginMove), delayBeforeMove);
    }

    void BeginMove()
    {
        isMoving = true;

        if (audioSource != null && moveSound != null)
        {
            audioSource.clip = moveSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    void Update()
    {
        if (!isMoving || hasArrived || pointA == null || pointB == null) return;

        transform.position = Vector3.MoveTowards(transform.position, pointB.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, pointB.position) < 0.01f)
        {
            hasArrived = true;
            isMoving = false;

            if (audioSource != null)
                audioSource.Stop();
        }
    }
}