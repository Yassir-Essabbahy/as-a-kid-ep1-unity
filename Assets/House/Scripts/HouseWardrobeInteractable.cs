using UnityEngine;
using System.Collections;

public class HouseWardrobeInteractable : MonoBehaviour, IInteractable
{
    [Header("Door Transforms")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Open Angles (Local Y)")]
    public float openAngleLeft = -80f;
    public float openAngleRight = 80f;
    public float animationDuration = 0.8f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip clothesSound;

    public string PromptMessage
    {
        get
        {
            if (HouseObjectiveController.Instance != null && HouseObjectiveController.Instance.hasWornClothes)
            {
                return isOpen ? "Close Wardrobe" : "Open Wardrobe";
            }
            return "Wear Clothes";
        }
    }

    private bool isOpen = false;
    private Quaternion leftClosedRot;
    private Quaternion rightClosedRot;
    private Quaternion leftOpenRot;
    private Quaternion rightOpenRot;
    private Coroutine doorCoroutine;

    private void Awake()
    {
        if (leftDoor == null) leftDoor = transform.Find("LeftDoor");
        if (rightDoor == null) rightDoor = transform.Find("RightDoor");

        if (leftDoor != null)
        {
            leftClosedRot = leftDoor.localRotation;
            leftOpenRot = leftClosedRot * Quaternion.Euler(0f, openAngleLeft, 0f);
        }

        if (rightDoor != null)
        {
            rightClosedRot = rightDoor.localRotation;
            rightOpenRot = rightClosedRot * Quaternion.Euler(0f, openAngleRight, 0f);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }
        }
    }

    public void Interact()
    {
        bool wasDressed = HouseObjectiveController.Instance != null && HouseObjectiveController.Instance.hasWornClothes;

        isOpen = !isOpen;

        if (doorCoroutine != null) StopCoroutine(doorCoroutine);
        doorCoroutine = StartCoroutine(AnimateDoorsRoutine(isOpen));

        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        if (!wasDressed)
        {
            if (audioSource != null && clothesSound != null)
            {
                audioSource.PlayOneShot(clothesSound);
            }

            if (HouseObjectiveController.Instance != null)
            {
                HouseObjectiveController.Instance.OnClothesWorn();
            }

            string label = "School Clothes Worn";
            if (LocalizationManager.Instance != null)
            {
                string loc = LocalizationManager.Instance.Get("house_clothes_worn");
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) label = loc;
            }

            if (ItemPickupToast.Instance != null)
            {
                ItemPickupToast.Instance.Show(null, label, 1);
            }

            HouseUIBridge.ShowMessage("house_clothes_worn", 2.0f);
        }
    }

    private IEnumerator AnimateDoorsRoutine(bool open)
    {
        if (leftDoor == null || rightDoor == null) yield break;

        float elapsed = 0f;
        Quaternion startLeft = leftDoor.localRotation;
        Quaternion targetLeft = open ? leftOpenRot : leftClosedRot;

        Quaternion startRight = rightDoor.localRotation;
        Quaternion targetRight = open ? rightOpenRot : rightClosedRot;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            leftDoor.localRotation = Quaternion.Slerp(startLeft, targetLeft, t);
            rightDoor.localRotation = Quaternion.Slerp(startRight, targetRight, t);
            yield return null;
        }

        leftDoor.localRotation = targetLeft;
        rightDoor.localRotation = targetRight;
        doorCoroutine = null;
    }
}
