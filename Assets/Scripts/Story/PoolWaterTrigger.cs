using TMPro;
using UnityEngine;

public class PoolWaterTrigger : MonoBehaviour
{
    [Header("Prompt Settings")]
    public string promptText = "Press 'E' to Throw into Pool";
    public float interactionDistance = 5.0f;
    public Transform poolCenterPoint;

    private Transform playerTransform;
    private TextMeshProUGUI hudPromptText;

    private void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;

        var npcInteract = FindAnyObjectByType<NpcInteractionText>();
        if (npcInteract != null) hudPromptText = npcInteract.InteractText;

        if (poolCenterPoint == null) poolCenterPoint = transform;
    }

    private void Update()
    {
        if (PoolStoryManager.Instance == null || PoolStoryManager.Instance.CurrentlyCarriedItem == null)
        {
            return;
        }

        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player") ?? GameObject.Find("FirstPersonController");
            if (p != null) playerTransform = p.transform;
            if (playerTransform == null) return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactionDistance)
        {
            if (hudPromptText != null)
                hudPromptText.text = promptText;

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (hudPromptText != null) hudPromptText.text = "";
                PoolStoryManager.Instance.ThrowCurrentItemIntoPool(poolCenterPoint.position);
            }
        }
        else
        {
            if (hudPromptText != null && hudPromptText.text == promptText)
            {
                hudPromptText.text = "";
            }
        }
    }
}
