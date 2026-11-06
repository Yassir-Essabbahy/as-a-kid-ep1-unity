using UnityEngine;
using System.Collections;

public class HouseMamaDialogue : MonoBehaviour, IInteractable
{
    public string speakerName = "Mama";
    public string dialogueKey = "mama_house_envelope";

    public string PromptMessage => "tap to talk";

    private bool hasSpoken = false;

    public void Interact()
    {
        if (hasSpoken)
        {
            HouseUIBridge.ShowMessage(dialogueKey, 4.0f);
            return;
        }

        StartCoroutine(DialogueRoutine());
    }

    private IEnumerator DialogueRoutine()
    {
        if (NpcDialogueManager.Instance == null) yield break;

        string line = "Take this. Hand it directly to the headmaster (Al-Mudir). Don't lose it... we had a hard time convincing the doctor you were ready to go back.";
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get(dialogueKey);
            if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) line = loc;
        }

        Color speakerColor = NpcDialogueManager.GetSpeakerColor(speakerName);
        var font = NpcDialogueManager.DefaultDialogueFont;

        yield return NpcDialogueManager.Instance.ShowDialogue(
            new string[] { line },
            false,
            speakerColor,
            font
        );

        hasSpoken = true;

        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnMamaTalked();
        }

        string certLabel = "Doctor's Certificate";
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get("item_doctor_certificate");
            if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) certLabel = loc;
        }

        if (ItemPickupToast.Instance != null)
        {
            ItemPickupToast.Instance.Show(null, certLabel, 1);
        }
    }
}
