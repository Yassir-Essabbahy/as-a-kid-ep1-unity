using UnityEngine;
using System.Collections;

public static class HouseUIBridge
{
    public static void ShowMessage(string message, float duration = 2.5f)
    {
        if (NpcInteractionText.Instance != null)
        {
            NpcInteractionText.Instance.SetPrompt(message);
        }
        else if (UIManager.instance != null)
        {
            UIManager.instance.ShowMessage(message, duration);
        }
    }
}
