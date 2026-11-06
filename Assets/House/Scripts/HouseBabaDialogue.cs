using UnityEngine;
using System.Collections;

public class HouseBabaDialogue : MonoBehaviour, IInteractable
{
    public string speakerName = "Baba";
    public string dialogueKey = "baba_house_advice";

    public string PromptMessage => "Talk to Baba";

    public void Interact()
    {
        StartCoroutine(DialogueRoutine());
    }

    private IEnumerator DialogueRoutine()
    {
        if (NpcDialogueManager.Instance == null) yield break;

        string line = "Watch the road. Straight to school, straight back home. No wandering off.";
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

        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.OnBabaTalked();
        }
    }
}
