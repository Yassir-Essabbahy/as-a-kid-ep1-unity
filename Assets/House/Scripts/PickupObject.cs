using UnityEngine;

public class PickupObject : MonoBehaviour, IInteractable
{
    [Header("Pickup Settings")]
    public float pickupDistance = 3f;
    public Transform holdPoint;
    public AudioClip pickupSound;
    public AudioSource audioSource;

    [Header("Player State")]
    public bool hasMoney = false;

    public string PromptMessage => CompareTag("Money") ? "Pick up Money" : "Pick up Object";

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void Interact()
    {
        ExecutePickup();
    }

    public void ExecutePickup()
    {
        if (CompareTag("Money"))
        {
            hasMoney = true;
            Debug.Log("[House] 200 DH picked up!");

            if (HouseObjectiveController.Instance != null)
            {
                HouseObjectiveController.Instance.OnMoneyPickedUp();
            }

            if (ItemPickupToast.Instance != null)
            {
                ItemPickupToast.Instance.Show(null, "200 DH");
            }

            if (pickupSound != null)
            {
                if (audioSource != null)
                    audioSource.PlayOneShot(pickupSound);
                else
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            Destroy(gameObject);
        }
        else if (CompareTag("Pickup"))
        {
            Transform targetHold = holdPoint;
            if (targetHold == null && Camera.main != null)
            {
                var hp = Camera.main.transform.Find("HoldPoint");
                targetHold = hp != null ? hp : Camera.main.transform;
            }

            if (targetHold != null)
            {
                transform.SetParent(targetHold);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }
    }
}
