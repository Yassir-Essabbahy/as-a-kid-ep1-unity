using UnityEngine;
using System.Collections;

public class HouseBabaDialogue : MonoBehaviour, IInteractable
{
    [Header("Speaker Settings")]
    public string speakerName = "Baba";
    public string dialogueKey = "baba_house_advice";

    [Header("Voice Audio")]
    public AudioSource audioSource;
    public AudioClip dadVoiceClip;

    [Header("Repeat Dialogue Key")]
    public string repeatDialogueKey = "baba_house_reminder";

    public string PromptMessage => "tap to talk";

    private bool hasSpoken = false;
    private NPCLookAt lookAt;
    private Coroutine activeRoutine;

    private void Awake()
    {
        lookAt = GetComponent<NPCLookAt>();
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
            // Baba is sitting on the couch; keep body planted and rotate head
            lookAt.bodyWeight = 0.02f;
            lookAt.headWeight = 0.95f;
            lookAt.eyesWeight = 0.6f;
        }

#if UNITY_EDITOR
        if (dadVoiceClip == null)
        {
            dadVoiceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/DadVoice.wav");
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

        // 1. IK Look At: Turn head & face smoothly towards player (keeps existing seated body animation playing)
        if (lookAt != null)
        {
            lookAt.StartLooking();
        }

        // 2. Resolve dialogue text
        string keyToUse = hasSpoken ? repeatDialogueKey : dialogueKey;
        string defaultLine = hasSpoken
            ? "Straight to school, straight back home. Go on."
            : "Watch the road. Straight to school, straight back home. No wandering off.";

        string line = defaultLine;
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get(keyToUse);
            if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) line = loc;
        }

        string formattedLine = $"{speakerName}: {line}";
        Color speakerColor = NpcDialogueManager.GetSpeakerColor(speakerName);
        var font = NpcDialogueManager.DefaultDialogueFont;

        AudioClip[] clips = (!hasSpoken && dadVoiceClip != null) ? new AudioClip[] { dadVoiceClip } : null;

        // 3. Show dialogue with typewriter typing effect and audio
        yield return NpcDialogueManager.Instance.ShowDialogue(
            new string[] { formattedLine },
            false,
            speakerColor,
            font,
            audioSource,
            clips
        );

        // 4. Return head from looking
        if (lookAt != null)
        {
            lookAt.StopLooking();
        }

        hasSpoken = true;

        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnBabaTalked();
        }

        activeRoutine = null;
    }
}
