using UnityEngine;
using System.Collections;

public static class HouseUIBridge
{
    public static void ShowMessage(string message, float duration = 3.0f)
    {
        if (string.IsNullOrEmpty(message)) return;

        string resolvedText = ResolveLocalizedMessage(message);

        if (NpcDialogueManager.Instance != null)
        {
            NpcDialogueManager.Instance.ShowInteractableMessage(resolvedText, duration);
        }
        else if (NpcInteractionText.Instance != null)
        {
            NpcInteractionText.Instance.SetPrompt(resolvedText);
        }
        else if (UIManager.instance != null)
        {
            UIManager.instance.ShowMessage(resolvedText, duration);
        }
    }

    private static string ResolveLocalizedMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;

        string key = null;
        string lower = message.Trim().ToLowerInvariant();

        if (lower == "baba makay5linich nd5l hna" || lower == "door_locked" || lower.Contains("dad doesn't let me"))
            key = "door_locked";
        else if (lower == "dima telaja khawya" || lower == "fridge_comment" || lower.Contains("fridge is always empty"))
            key = "fridge_comment";
        else if (lower == "n9dr nt5ba hna 3la mama" || lower == "cabinet_comment" || lower.Contains("hide here from mom"))
            key = "cabinet_comment";
        else if (lower == "الدرج مسدود" || lower == "المجر مقفول" || lower == "drawer_locked" || lower.Contains("drawer is locked"))
            key = "drawer_locked";
        else if (lower == "النافذة مسدودة" || lower == "النافذة مقفولة" || lower == "window_locked" || lower.Contains("window is locked"))
            key = "window_locked";
        else if (lower == "الخزانة مسدودة" || lower == "الخزانة مقفولة" || lower == "cupboard_locked" || lower.Contains("cabinet is locked"))
            key = "cupboard_locked";
        else if (lower == "house_need_wash_face" || lower.Contains("wash my face") || lower.Contains("laver le visage") || lower.Contains("nghsel wjhi"))
            key = "house_need_wash_face";
        else if (lower == "house_face_washed" || lower.Contains("face washed") || lower.Contains("visage lav") || lower.Contains("غسلت"))
            key = "house_face_washed";
        else if (lower == "house_need_clothes_and_bag" || lower.Contains("need to put on my clothes") || lower.Contains("mettre mes vêtements") || lower.Contains("nlbes hwayji"))
            key = "house_need_clothes_and_bag";
        else if (lower == "house_need_talk_mom" || lower == "door_cant_leave_no_papers" || lower.Contains("talk to mom") || lower.Contains("talk to mama") || lower.Contains("parler à maman") || lower.Contains("nhdr m3a mama"))
            key = "house_need_talk_mom";
        else if (lower == "door_cant_leave_no_money" || lower.Contains("money from the drawer") || lower.Contains("l'argent dans le tiroir") || lower.Contains("lflouss mn lmjer"))
            key = "door_cant_leave_no_money";

        if (key != null && LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get(key);
            if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) return loc;
        }

        if (LocalizationManager.Instance != null)
        {
            string directLoc = LocalizationManager.Instance.Get(message);
            if (!string.IsNullOrEmpty(directLoc) && !directLoc.StartsWith("["))
                return directLoc;
        }

        return message;
    }
}
