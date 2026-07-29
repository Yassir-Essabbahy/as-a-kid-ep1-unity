using UnityEngine;
using System.Collections;

public class BeggarInteraction : MonoBehaviour
{
    [Header("Trigger")]
    public string playerTag = "Player";
    public bool triggerOnce = true;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip preSound;

    [Header("Timing")]
    public float delayBeforeLook = 0.4f;
    public float lookInDuration = 1.0f;
    public float lookOutDuration = 0.6f;
    public float delayBeforeDialogue = 0.2f;

    [Header("Refs")]
    public Transform playerCamera;
    public Transform lookTarget;
    public MonoBehaviour playerController;
    public NpcConversation conversation;

    private bool hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;
        if (conversation == null) return;

        hasTriggered = true;
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        if (audioSource != null && preSound != null)
            audioSource.PlayOneShot(preSound);

        yield return new WaitForSeconds(delayBeforeLook);

        if (playerController != null) playerController.enabled = false;

        Quaternion startRot = playerCamera.rotation;

        // Lerp in, re-aiming at the target every frame so it's exact
        // even if the target or camera moved slightly during the blend
        float t = 0f;
        while (t < lookInDuration)
        {
            t += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, t / lookInDuration);
            Quaternion currentTarget = Quaternion.LookRotation(lookTarget.position - playerCamera.position);
            playerCamera.rotation = Quaternion.Slerp(startRot, currentTarget, s);
            yield return null;
        }

        // Snap exactly onto target at the end of the lerp-in only
        // (this is the one instant snap you want — everything else is smooth)
        playerCamera.rotation = Quaternion.LookRotation(lookTarget.position - playerCamera.position);

        yield return new WaitForSeconds(delayBeforeDialogue);

        yield return StartCoroutine(conversation.Play());

        // Lerp back out to wherever the player's controller would naturally
        // be facing, so control doesn't resume with a snap
        Quaternion lookOutStart = playerCamera.rotation;
        Quaternion lookOutEnd = startRot; // rotation from before the beat began

        float t2 = 0f;
        while (t2 < lookOutDuration)
        {
            t2 += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, t2 / lookOutDuration);
            playerCamera.rotation = Quaternion.Slerp(lookOutStart, lookOutEnd, s);
            yield return null;
        }
        playerCamera.rotation = lookOutEnd;

        if (playerController != null) playerController.enabled = true;
    }
}