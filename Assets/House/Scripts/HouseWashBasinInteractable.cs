using UnityEngine;
using System.Collections;

/// <summary>
/// Controls the bathroom washbasin / sink interaction:
/// 1. Prompts "Wash Face" when the player approaches the bathroom sink before grabbing clothes & bag.
/// 2. When interacted:
///    - Locks player control temporarily (~2.5s).
///    - Audio: Plays tap water running and splashing on face (WaterSplashFace.wav).
///    - Camera: Smoothly dips down toward the sink basin as hands cup water.
///    - Visual: Eyelids blink/dip to black as cool water hits the eyes/face.
///    - Plays refreshed morning sigh (MorningBreath.wav).
///    - Displays toast: "Face washed. Feeling awake!"
///    - Signals HouseObjectiveController.Instance.OnFaceWashed(), advancing to phase 1 (grab clothes & backpack).
///    - Unlocks player control and marks interaction consumed.
/// </summary>
[DisallowMultipleComponent]
public class HouseWashBasinInteractable : MonoBehaviour, IInteractable
{
    [Header("Audio SFX")]
    public AudioSource audioSource;
    public AudioClip waterSplashSound;
    public AudioClip breathSound;

    [Header("Animation & Camera Settings")]
    public float interactionDuration = 2.5f;
    public float cameraDipAngle = 20f; // tilts down towards basin

    [Header("Prompt Settings")]
    public string promptMessage = "Wash Face";
    public string localizationKey = "prompt_wash_face";

    public string PromptMessage
    {
        get
        {
            if (hasWashed) return "";
            if (HouseObjectiveController.Instance != null && HouseObjectiveController.Instance.hasWashedFace) return "";

            if (!string.IsNullOrEmpty(localizationKey) && LocalizationManager.Instance != null)
            {
                string loc = LocalizationManager.Instance.Get(localizationKey);
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) return loc;
            }

            return promptMessage;
        }
    }

    private bool hasWashed = false;
    private Coroutine activeRoutine;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.8f;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 8f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            }
        }

#if UNITY_EDITOR
        if (waterSplashSound == null)
        {
            waterSplashSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/WaterSplashFace.wav");
        }
        if (breathSound == null)
        {
            breathSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/MorningBreath.wav");
        }
#endif
    }

    public void Interact()
    {
        if (hasWashed || activeRoutine != null) return;
        if (HouseObjectiveController.Instance != null && HouseObjectiveController.Instance.hasWashedFace) return;

        activeRoutine = StartCoroutine(WashFaceRoutine());
    }

    private IEnumerator WashFaceRoutine()
    {
        hasWashed = true;

        // 1. Immediately clear interaction prompt
        if (NpcInteractionText.Instance != null)
        {
            NpcInteractionText.Instance.ClearPrompt();
        }

        // 2. Lock player movement & look
        var fpc = Object.FindAnyObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            fpc.SetControlLocked(true);
        }

        // 3. Play water splash & faucet sound
        if (audioSource != null && waterSplashSound != null)
        {
            audioSource.PlayOneShot(waterSplashSound, 1.0f);
        }

        // 4. Smooth camera lean / dip down toward the sink basin
        Camera cam = Camera.main;
        float startPitch = fpc != null ? fpc.Pitch : (cam != null ? cam.transform.localEulerAngles.x : 0f);
        if (startPitch > 180f) startPitch -= 360f;
        float maxAngle = fpc != null ? fpc.maxLookAngle : 50f;
        float targetPitch = Mathf.Clamp(startPitch + cameraDipAngle, -maxAngle, maxAngle);

        // Tilt down toward water
        float dipTime = 0.6f;
        float elapsed = 0f;
        while (elapsed < dipTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dipTime);
            if (fpc != null)
            {
                fpc.SetViewAngles(fpc.transform.eulerAngles.y, Mathf.Lerp(startPitch, targetPitch, t));
            }
            yield return null;
        }

        // 5. Water splashes on face: Eyelids blink/dip to black
        if (EyelidBlink.Instance != null)
        {
            EyelidBlink.Instance.DipToBlack(0.55f);
        }
        yield return new WaitForSeconds(0.55f);

        // 6. Camera tilts back up to original level
        float returnTime = 0.5f;
        elapsed = 0f;
        while (elapsed < returnTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / returnTime);
            if (fpc != null)
            {
                fpc.SetViewAngles(fpc.transform.eulerAngles.y, Mathf.Lerp(targetPitch, startPitch, t));
            }
            yield return null;
        }

        // 7. Refreshing sigh / breath sound
        if (audioSource != null && breathSound != null)
        {
            audioSource.PlayOneShot(breathSound, 0.85f);
        }

        yield return new WaitForSeconds(0.3f);

        // 8. Update objective system to phase 1 (grab clothes & backpack)
        Debug.Log("[WashBasin] Updating objective to phase 1...");
        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnFaceWashed();
            Debug.Log("[WashBasin] OnFaceWashed called! New phase=" + HouseObjectiveController.Instance.phase);
        }
        else
        {
            Debug.LogError("[WashBasin] HouseObjectiveController.Instance is null!");
        }

        // 9. Show refreshed feedback toast
        HouseUIBridge.ShowMessage("house_face_washed", 3.0f);

        // 10. Unlock player movement & look
        if (fpc != null)
        {
            fpc.SetControlLocked(false);
        }

        activeRoutine = null;
    }
}
