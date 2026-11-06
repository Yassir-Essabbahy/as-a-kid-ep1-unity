using UnityEngine;
using TMPro;

public class ObjectiveUI : MonoBehaviour 
{
    public TextMeshProUGUI objectiveText;

    public void SetObjective(string text)
    {
        objectiveText.text = "← " + text;
    }

    public void ClearObjective()
    {
        objectiveText.text = "";
    }
}
