using UnityEngine;
using System.Collections;

/// <summary>
/// Controls the bedroom wardrobe interaction:
/// 1. "Open wardrobe": Both doors swing open with smooth eased motion, playing wood creak and cloth rustle.
/// 2. "Put on clothes": Triggers a ~0.4s dip to black with fabric rustle SFX, marks clothes worn, and displays toast notification.
/// 3. Interaction is consumed: Doors stay open, no double-trigger.
/// </summary>
[DisallowMultipleComponent]
public class HouseWardrobeInteractable : MonoBehaviour, IInteractable
{
    public enum WardrobeState
    {
        Closed,
        OpenNotDressed,
        ClothesWorn
    }

    [Header("Door Transforms")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Door Swing Angles (Cabinet Up)")]
    public float openAngleLeft = 95f;   // swings forward into room
    public float openAngleRight = -95f; // swings forward into room
    public float swingDuration = 1.1f;
    public AnimationCurve swingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio SFX")]
    public AudioSource audioSource;
    public AudioClip creakSound;
    public AudioClip clothRustleSound;

    [Header("State")]
    public WardrobeState currentState = WardrobeState.Closed;

    public string PromptMessage
    {
        get
        {
            switch (currentState)
            {
                case WardrobeState.Closed:
                    return LocalizationManager.Resolve("prompt_open_wardrobe", "Open wardrobe");
                case WardrobeState.OpenNotDressed:
                    return LocalizationManager.Resolve("prompt_put_on_clothes", "Put on clothes");
                default:
                    return ""; // consumed
            }
        }
    }

    private Quaternion leftClosedRot;
    private Quaternion rightClosedRot;
    private Coroutine activeRoutine;

    private void Awake()
    {
        if (leftDoor == null) leftDoor = transform.Find("LeftDoor");
        if (rightDoor == null) rightDoor = transform.Find("RightDoor");

        // Ensure doors have upright orientation matching the wardrobe frame if uninitialized or broken
        if (leftDoor != null && Mathf.Abs(leftDoor.localEulerAngles.x - 270f) > 5f)
        {
            leftDoor.localEulerAngles = new Vector3(270f, 270f, 0f);
        }
        if (rightDoor != null && Mathf.Abs(rightDoor.localEulerAngles.x - 270f) > 5f)
        {
            rightDoor.localEulerAngles = new Vector3(270f, 270f, 0f);
        }

        if (leftDoor != null) leftClosedRot = leftDoor.rotation;
        if (rightDoor != null) rightClosedRot = rightDoor.rotation;

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

        // Auto-load audio clips if null
#if UNITY_EDITOR
        if (creakSound == null)
            creakSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/WardrobeCreak.wav");
        if (creakSound == null)
            creakSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/DoorOpen.wav");

        if (clothRustleSound == null)
            clothRustleSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/House/Audio/ClothRustle.wav");
#endif
    }

    public void Interact()
    {
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

        if (activeRoutine != null) return; // avoid double trigger while animating

        if (currentState == WardrobeState.Closed)
        {
            activeRoutine = StartCoroutine(OpenDoorsRoutine());
        }
        else if (currentState == WardrobeState.OpenNotDressed)
        {
            activeRoutine = StartCoroutine(PutOnClothesRoutine());
        }
    }

    private IEnumerator OpenDoorsRoutine()
    {
        if (audioSource != null)
        {
            if (creakSound != null) audioSource.PlayOneShot(creakSound, 0.9f);
            if (clothRustleSound != null) audioSource.PlayOneShot(clothRustleSound, 0.6f);
        }

        Vector3 axis = transform.up;
        float elapsed = 0f;
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / swingDuration);
            float t = swingCurve != null ? swingCurve.Evaluate(rawT) : rawT;

            if (leftDoor != null)
                leftDoor.rotation = Quaternion.AngleAxis(openAngleLeft * t, axis) * leftClosedRot;

            if (rightDoor != null)
                rightDoor.rotation = Quaternion.AngleAxis(openAngleRight * t, axis) * rightClosedRot;

            yield return null;
        }

        if (leftDoor != null)
            leftDoor.rotation = Quaternion.AngleAxis(openAngleLeft, axis) * leftClosedRot;

        if (rightDoor != null)
            rightDoor.rotation = Quaternion.AngleAxis(openAngleRight, axis) * rightClosedRot;

        currentState = WardrobeState.OpenNotDressed;
        activeRoutine = null;

        // Refresh UI prompt
        if (NpcInteractionText.Instance != null)
        {
            NpcInteractionText.Instance.SetPrompt(PromptMessage);
        }
    }

    private IEnumerator PutOnClothesRoutine()
    {
        // 1. Play cloth rustle SFX
        if (audioSource != null && clothRustleSound != null)
        {
            audioSource.PlayOneShot(clothRustleSound, 1.0f);
        }

        // 2. Short dip to black (~0.4s)
        if (EyelidBlink.Instance != null)
        {
            yield return EyelidBlink.Instance.DipToBlack(0.45f);
        }
        else
        {
            yield return new WaitForSeconds(0.45f);
        }

        // 3. Mark clothes worn
        currentState = WardrobeState.ClothesWorn;

        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnClothesWorn();
        }

        // 4. Clear prompt (interaction consumed, doors stay open)
        if (NpcInteractionText.Instance != null)
        {
            NpcInteractionText.Instance.ClearPrompt();
        }

        activeRoutine = null;
    }
}
