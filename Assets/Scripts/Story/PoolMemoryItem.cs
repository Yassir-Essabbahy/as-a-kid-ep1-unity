using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class PoolMemoryItem : MonoBehaviour
{
    public enum MemoryType
    {
        Pants,
        Bag,
        Shoes,
        Towel,
        Phone
    }

    [Header("Item Config")]
    public MemoryType memoryType;
    public string displayName = "Brother's Item";
    public string discoveryDialogueKey;
    public bool isFinalItem = false;
    public float interactionDistance = 3.0f;

    [Header("State")]
    public bool isCollected = false;
    public bool isThrownIntoPool = false;

    [Header("Components")]
    public CarryableItem carryable;
    public Rigidbody rb;
    public Collider col;
    public TextMeshPro labelText;

    private Transform playerTransform;
    private TextMeshProUGUI hudPromptText;

    private void Awake()
    {
        if (carryable == null) carryable = GetComponent<CarryableItem>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<Collider>();
        if (labelText == null) labelText = GetComponentInChildren<TextMeshPro>();
    }

    private void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;

        var npcInteract = FindAnyObjectByType<NpcInteractionText>();
        if (npcInteract != null) hudPromptText = npcInteract.InteractText;

        if (labelText != null)
        {
            labelText.text = displayName;
        }
    }

    private void Update()
    {
        if (isCollected || isThrownIntoPool) return;
        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player") ?? GameObject.Find("FirstPersonController");
            if (p != null) playerTransform = p.transform;
            if (playerTransform == null) return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactionDistance)
        {
            Ray ray = new Ray(Camera.main != null ? Camera.main.transform.position : playerTransform.position + Vector3.up * 1.5f,
                              Camera.main != null ? Camera.main.transform.forward : playerTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance + 1f))
            {
                if (hit.collider == col || hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    if (hudPromptText != null)
                        hudPromptText.text = $"Press 'E' to Pick Up {displayName}";

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        TryPickUp();
                    }
                }
            }
        }
    }

    public void TryPickUp()
    {
        if (isCollected || isThrownIntoPool) return;

        if (PoolStoryManager.Instance != null && PoolStoryManager.Instance.CurrentlyCarriedItem != null)
        {
            if (hudPromptText != null)
                hudPromptText.text = "You are already carrying an item. Throw it in the pool first.";
            return;
        }

        PickUp();
    }

    public void PickUp()
    {
        isCollected = true;
        if (hudPromptText != null) hudPromptText.text = "";

        Transform carryPoint = null;
        var fps = FindAnyObjectByType<FirstPersonController>();
        if (fps != null)
        {
            carryPoint = fps.GetComponentInChildren<Camera>()?.transform.Find("CarryPoint")
                         ?? fps.transform.Find("CarryPoint");
        }
        if (carryPoint == null)
        {
            var cp = GameObject.Find("CarryPoint");
            if (cp != null) carryPoint = cp.transform;
        }

        if (carryable != null && carryPoint != null)
        {
            carryable.StartCarrying(carryPoint);
        }

        if (PoolStoryManager.Instance != null)
        {
            PoolStoryManager.Instance.OnItemPickedUp(this);
        }

        if (!string.IsNullOrEmpty(discoveryDialogueKey))
        {
            StartCoroutine(PlayDiscoveryDialogue());
        }
    }

    private IEnumerator PlayDiscoveryDialogue()
    {
        if (isFinalItem)
        {
            yield return new WaitForSeconds(1.5f);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }

        string line = MetroStorySequenceController.GetLoc(discoveryDialogueKey);
        var diag = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();
        if (diag != null && Application.isPlaying)
        {
            yield return StartCoroutine(diag.ShowDialogue(
                new string[] { line },
                hasChoice: false,
                dialogueColor: new Color(0.9f, 0.95f, 1f),
                dialogueFont: null
            ));
        }
    }

    public void ThrowIntoPool(Vector3 poolCenter)
    {
        if (isThrownIntoPool) return;
        isThrownIntoPool = true;

        if (carryable != null)
        {
            carryable.StopCarrying();
        }

        StartCoroutine(SinkRoutine(poolCenter));
    }

    private IEnumerator SinkRoutine(Vector3 poolCenter)
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            Vector3 throwDir = (poolCenter - transform.position).normalized + Vector3.up * 0.3f;
            rb.linearVelocity = throwDir * 4.0f;
        }

        yield return new WaitForSeconds(2.0f);

        Vector3 origScale = transform.localScale;
        float elapsed = 0f;
        float duration = 1.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(origScale, Vector3.zero, elapsed / duration);
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
