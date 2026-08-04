using System.Collections;
using UnityEngine;

public class ElevatorButton : MonoBehaviour
{
    [Header("Interact")]
    public float interactDistance = 2f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Quest Gate")]
    public TeddyFindQuest currentFloorQuest;

    [Header("Doors")]
    public Animator doorAnimator;
    public string closeTrigger = "Close";
    public string openTrigger = "Open";

    [Header("Timing")]
    public float doorCloseTime = 1.2f;
    public float rideTime = 3f;
    public float doorOpenTime = 1.2f;

    [Header("Shake")]
    public Transform shakeTarget; // usually PlayerCamera or the elevator cabin itself
    public float shakeIntensity = 0.02f;
    public float shakeSpeed = 25f;

    [Header("Player")]
    public Transform playerTransform;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip doorCloseSound;
    public AudioClip elevatorSong;
    public AudioClip doorOpenSound;

    private bool isRunning = false;

    void Update()
    {
        if (isRunning) return;
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactDistance && Input.GetKeyDown(interactKey))
        {
            if (currentFloorQuest != null && !currentFloorQuest.questCompleted) return;
            if (!FloorManager.Instance.HasNextFloor()) return;

            StartCoroutine(RunElevator());
        }
    }

    IEnumerator RunElevator()
    {
        isRunning = true;

        if (doorAnimator != null) doorAnimator.SetTrigger(closeTrigger);
        if (audioSource != null && doorCloseSound != null) audioSource.PlayOneShot(doorCloseSound);
        yield return new WaitForSeconds(doorCloseTime);

        if (audioSource != null && elevatorSong != null) audioSource.PlayOneShot(elevatorSong);

        int nextFloor = FloorManager.Instance.currentFloorIndex + 1;
        FloorManager.Instance.ActivateFloor(nextFloor);

        // Shake during the ride, player can still walk around freely
        yield return StartCoroutine(ShakeDuringRide(rideTime));

        if (doorAnimator != null) doorAnimator.SetTrigger(openTrigger);
        if (audioSource != null && doorOpenSound != null) audioSource.PlayOneShot(doorOpenSound);
        yield return new WaitForSeconds(doorOpenTime);

        isRunning = false;
    }

    IEnumerator ShakeDuringRide(float duration)
    {
        if (shakeTarget == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector3 originalLocalPos = shakeTarget.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float offsetX = (Mathf.PerlinNoise(Time.time * shakeSpeed, 0f) - 0.5f) * shakeIntensity;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * shakeSpeed) - 0.5f) * shakeIntensity;

            shakeTarget.localPosition = originalLocalPos + new Vector3(offsetX, offsetY, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        shakeTarget.localPosition = originalLocalPos;
    }
}