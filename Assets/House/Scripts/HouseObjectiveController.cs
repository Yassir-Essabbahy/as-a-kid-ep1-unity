using UnityEngine;
using TMPro;

public class HouseObjectiveController : MonoBehaviour
{
    public static HouseObjectiveController Instance { get; private set; }

    [Header("UI Reference")]
    public TextMeshProUGUI objectiveText;

    [Header("State")]
    public int phase = 0;
    public bool hasTalkedToMama = false;
    public bool hasMoney = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (objectiveText == null)
        {
            var go = GameObject.Find("StoryObjectiveText");
            if (go != null) objectiveText = go.GetComponent<TextMeshProUGUI>();
        }
        UpdateObjectiveUI();
    }

    public void OnMamaTalked()
    {
        hasTalkedToMama = true;
        phase = 1;
        UpdateObjectiveUI();
    }

    public void OnMoneyPickedUp()
    {
        hasMoney = true;
        phase = 2;
        UpdateObjectiveUI();
    }

    private void UpdateObjectiveUI()
    {
        if (objectiveText == null) return;

        string key = "house_objective_talk_parents";
        string fallback = "Talk to your parents";

        if (phase == 1)
        {
            key = "house_objective_get_money";
            fallback = "Find money in the kitchen drawer";
        }
        else if (phase >= 2)
        {
            key = "house_objective_go_school";
            fallback = "Head out to school";
        }

        string text = fallback;
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get(key);
            if (!string.IsNullOrEmpty(loc)) text = loc;
        }

        objectiveText.text = text;
        objectiveText.gameObject.SetActive(true);
    }
}
