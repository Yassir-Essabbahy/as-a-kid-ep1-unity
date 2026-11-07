using UnityEngine;
using System.Collections;

public class HouseMamaDialogue : MonoBehaviour, IInteractable
{
    [Header("Speaker Settings")]
    public string speakerName = "Mama";
    public string dialogueKey = "mama_house_envelope";

    [Header("Voice Audio")]
    public AudioSource audioSource;
    public AudioClip mamaVoiceClip;

    [Header("Repeat Dialogue Key")]
    public string repeatDialogueKey = "mama_house_reminder";

    [Header("Animation Settings")]
    [SerializeField] private bool returnToCookingAfterDialogue = true;
    [SerializeField] private string talkAnimBool = "IsTalking";
    [SerializeField] private string talkAnimTrigger = "TalkToPlayer";

    public string PromptMessage => "tap to talk";

    private bool hasSpoken = false;
    private NPCLookAt lookAt;
    private Animator animator;
    private Coroutine activeRoutine;

    private void Awake()
    {
        lookAt = GetComponent<NPCLookAt>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0.5f;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 10f;
        }

        if (lookAt != null)
        {
            lookAt.bodyWeight = 0.15f;
            lookAt.headWeight = 0.85f;
            lookAt.eyesWeight = 0.6f;
        }

#if UNITY_EDITOR
        if (mamaVoiceClip == null)
        {
            mamaVoiceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/MomVoice.wav");
        }
#endif
    }

    public void Interact()
    {
        if (activeRoutine != null) return;
        activeRoutine = StartCoroutine(DialogueRoutine());
    }

    private IEnumerator DialogueRoutine()
    {
        if (NpcDialogueManager.Instance == null) yield break;

        // 1. IK Look At & Turn Animation: Face player smoothly and play turn animation
        if (lookAt != null)
        {
            lookAt.StartLooking();
        }

        if (animator != null)
        {
            if (!string.IsNullOrEmpty(talkAnimBool)) animator.SetBool(talkAnimBool, true);
            if (!string.IsNullOrEmpty(talkAnimTrigger)) animator.SetTrigger(talkAnimTrigger);
        }

        // 2. Resolve dialogue text
        string keyToUse = hasSpoken ? repeatDialogueKey : dialogueKey;
        string defaultLine = hasSpoken
            ? "Don't lose that certificate. Head straight to school!"
            : "Take this. Hand it directly to the headmaster (Al-Mudir). Don't lose it... we had a hard time convincing the doctor you were ready to go back.";

        string line = defaultLine;
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get(keyToUse);
            if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) line = loc;
        }

        string formattedLine = $"{speakerName}: {line}";
        Color speakerColor = NpcDialogueManager.GetSpeakerColor(speakerName);
        var font = NpcDialogueManager.DefaultDialogueFont;

        AudioClip[] clips = (!hasSpoken && mamaVoiceClip != null) ? new AudioClip[] { mamaVoiceClip } : null;

        // 3. Show dialogue with typewriter typing effect and audio
        yield return NpcDialogueManager.Instance.ShowDialogue(
            new string[] { formattedLine },
            false,
            speakerColor,
            font,
            audioSource,
            clips
        );

        // 4. Return head from looking & resume cooking if configured
        if (lookAt != null)
        {
            lookAt.StopLooking();
        }

        if (animator != null && returnToCookingAfterDialogue)
        {
            if (!string.IsNullOrEmpty(talkAnimBool)) animator.SetBool(talkAnimBool, false);
        }

        hasSpoken = true;

        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnMamaTalked();
        }

        activeRoutine = null;
    }
}
