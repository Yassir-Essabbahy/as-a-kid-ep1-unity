using UnityEngine;
using System.Collections;

public class HouseBackpackInteractable : MonoBehaviour, IInteractable
{
    [Header("Audio SFX")]
    public AudioSource audioSource;
    public AudioClip clothRustleSound;
    public AudioClip pickupSound;

    [Header("Transition Settings")]
    public float dipDuration = 0.45f;

    public string PromptMessage => collected ? "" : LocalizationManager.Resolve("prompt_take_backpack", "Take Backpack");

    private bool collected = false;
    private Coroutine activeRoutine;

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        if (!col.enabled || col.size.sqrMagnitude < 0.000001f)
        {
            col.enabled = true;
            col.isTrigger = true;
            col.center = Vector3.zero;
            col.size = new Vector3(0.0055f, 0.0050f, 0.0055f);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 10f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            }
        }

#if UNITY_EDITOR
        if (clothRustleSound == null)
        {
            clothRustleSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/ClothRustle.wav");
        }
#endif
    }

    public void Interact()
    {
        if (collected || activeRoutine != null) return;

        if (HouseObjectiveController.Instance != null && !HouseObjectiveController.Instance.hasWashedFace)
        {
            string msg = "I should go to the bathroom and wash my face first.";
            if (LocalizationManager.Instance != null)
            {
                string loc = LocalizationManager.Instance.Get("house_need_wash_face");
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) msg = loc;
            }
            HouseUIBridge.ShowMessage(msg);
            return;
        }

        activeRoutine = StartCoroutine(TakeBackpackRoutine());
    }

    private IEnumerator TakeBackpackRoutine()
    {
        collected = true;

        // 1. Immediately clear prompt and disable colliders to prevent double-trigger
        if (NpcInteractionText.Instance != null)
        {
            NpcInteractionText.Instance.ClearPrompt();
        }

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // 2. Play cloth rustle SFX (same sound effect as taking clothes)
        AudioClip clipToPlay = clothRustleSound != null ? clothRustleSound : pickupSound;
        if (clipToPlay != null)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, transform.position, 1.0f);
        }

        // 3. Short dip to black (~0.45s) identical to putting on clothes
        if (EyelidBlink.Instance != null)
        {
            float half = dipDuration * 0.5f;
            // Eyes close
            yield return EyelidBlink.Instance.Close(half);

            // Hide backpack visual while screen is pitch black
            SetRenderersVisible(false);

            // Eyes open back up
            yield return EyelidBlink.Instance.OpenSlow(1f, half);
        }
        else
        {
            float half = dipDuration * 0.5f;
            yield return new WaitForSeconds(half);
            SetRenderersVisible(false);
            yield return new WaitForSeconds(half);
        }

        // 4. Mark backpack collected in objective system
        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnBackpackTaken();
        }

        // 5. Deactivate backpack GameObject
        gameObject.SetActive(false);
        activeRoutine = null;
    }

    private void SetRenderersVisible(bool visible)
    {
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }
    }
}
