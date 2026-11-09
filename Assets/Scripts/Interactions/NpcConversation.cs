using System;
using System.Collections;
using UnityEngine;

public class NpcConversation : MonoBehaviour
{
    [Header("Dialogue")]
    public string[] lineKeys;
    public bool needsChoiceAtEnd;
    public bool playOnce = true;

    [Header("Presentation")]
    public Color dialogueColor = Color.white;
    public TMPro.TMP_FontAsset dialogueFont;

    [Header("Voice")]
    public AudioSource voiceSource;
    public AudioClip[] voiceClips;

    [Header("Carry After Dialogue")]
    public bool becomesCarryableAfterDialogue = false;
    public Transform playerCarryPoint;

    public event Action OnConversationFinished;

    private bool isPlaying;
    private bool hasPlayed;

    public bool IsPlaying => isPlaying;
    public bool HasPlayed
    {
        get => hasPlayed;
        set => hasPlayed = value;
    }

    public bool CanPlay => !isPlaying && (!playOnce || !hasPlayed);

    public IEnumerator Play()
    {
        if (isPlaying || (playOnce && hasPlayed))
            yield break;

        isPlaying = true;
        hasPlayed = true;

        try
        {
            // ----------------------------------------------------
            // SAFETY CHECKS
            // ----------------------------------------------------

            if (LocalizationManager.Instance == null)
            {
                Debug.LogError(
                    $"[{name}] LocalizationManager.Instance is missing."
                );

                yield break;
            }

            if (NpcDialogueManager.Instance == null)
            {
                Debug.LogError(
                    $"[{name}] NpcDialogueManager.Instance is missing."
                );

                yield break;
            }


            // ----------------------------------------------------
            // RESOLVE TEXT
            // ----------------------------------------------------

            string[] resolvedLines =
                new string[lineKeys != null ? lineKeys.Length : 0];

            for (int i = 0; i < resolvedLines.Length; i++)
            {
                if (string.IsNullOrEmpty(lineKeys[i]))
                {
                    resolvedLines[i] = "";
                    continue;
                }

                resolvedLines[i] =
                    LocalizationManager.Instance.Get(lineKeys[i]);
            }


            // Prefer a source on this NPC when the Inspector reference was
            // lost during prefab/scene setup.
            if (voiceSource == null)
                voiceSource = GetComponent<AudioSource>();

            if (voiceSource == null)
                voiceSource = GetComponentInChildren<AudioSource>(true);

            // ----------------------------------------------------
            // VOICE VALIDATION
            // ----------------------------------------------------

            if (voiceClips != null &&
                voiceClips.Length > 0 &&
                voiceClips.Length != resolvedLines.Length)
            {
                Debug.LogWarning(
                    $"[{name}] Voice clips count ({voiceClips.Length}) " +
                    $"does not match dialogue lines ({resolvedLines.Length})."
                );
            }

            if (voiceSource == null && voiceClips != null)
            {
                bool hasVoice = false;

                for (int i = 0; i < voiceClips.Length; i++)
                {
                    if (voiceClips[i] != null)
                    {
                        hasVoice = true;
                        break;
                    }
                }

                if (hasVoice)
                {
                    Debug.LogWarning(
                        $"[{name}] Voice clips exist but Voice Source is not assigned."
                    );
                }
            }


            // ----------------------------------------------------
            // PLAY DIALOGUE
            // ----------------------------------------------------

            yield return StartCoroutine(
                NpcDialogueManager.Instance.ShowDialogue(
                    resolvedLines,
                    needsChoiceAtEnd,
                    dialogueColor,
                    dialogueFont,
                    voiceSource,
                    voiceClips
                )
            );


            // ----------------------------------------------------
            // CARRY AFTER DIALOGUE
            // ----------------------------------------------------

            if (becomesCarryableAfterDialogue)
            {
                CarryableItem carryable =
                    GetComponent<CarryableItem>();

                if (carryable != null &&
                    playerCarryPoint != null)
                {
                    carryable.StartCarrying(
                        playerCarryPoint
                    );
                }
            }


            // ----------------------------------------------------
            // FINISHED
            // ----------------------------------------------------

            OnConversationFinished?.Invoke();
        }
        finally
        {
            isPlaying = false;
        }
    }
}
