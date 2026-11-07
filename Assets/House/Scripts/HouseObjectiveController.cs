using UnityEngine;
using TMPro;

public class HouseObjectiveController : MonoBehaviour
{
    public static HouseObjectiveController Instance { get; private set; }

    [Header("UI Reference")]
    public TextMeshProUGUI objectiveText;

    [Header("State")]
    public int phase = 0; // 0 = wash face in bathroom, 1 = get dressed & bag, 2 = talk to mom, 3 = exit to school
    public bool hasWashedFace = false;
    public bool hasWornClothes = false;
    public bool hasBackpack = false;
    public bool hasTalkedToMama = false;
    public bool hasTalkedToBaba = false;

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

    public void OnFaceWashed()
    {
        hasWashedFace = true;
        phase = 1;
        UpdateObjectiveUI();
    }

    public void OnClothesWorn()
    {
        hasWornClothes = true;
        CheckRoomPrepared();
    }

    public void OnBackpackTaken()
    {
        hasBackpack = true;
        CheckRoomPrepared();
    }

    private void CheckRoomPrepared()
    {
        if (hasWornClothes && hasBackpack)
        {
            phase = 2;
            UpdateObjectiveUI();
        }
    }

    public void UnlockBedroomDoor()
    {
        var door5 = GameObject.Find("Door5");
        if (door5 != null)
        {
            var dc = door5.GetComponentInChildren<DoorController>();
            if (dc != null) dc.isLocked = false;
        }
    }

    public void OnMamaTalked()
    {
        hasTalkedToMama = true;
        phase = 3;
        UpdateObjectiveUI();
    }

    public void OnMoneyPickedUp()
    {
        // Deprecated: money objective removed per story flow
    }

    public void OnBabaTalked()
    {
        hasTalkedToBaba = true;
    }

    public void UpdateObjectiveUI()
    {
        if (objectiveText == null) return;

        string key = "house_objective_wash_face";
        string fallback = "Go to the bathroom and wash your face";

        if (phase == 1)
        {
            key = "house_objective_get_dressed";
            fallback = "Put on clothes and take your backpack";
        }
        else if (phase == 2)
        {
            key = "house_objective_talk_mom";
            fallback = "Talk to your mom in the kitchen";
        }
        else if (phase >= 3)
        {
            key = "house_objective_go_school";
            fallback = "Head out to school";
        }

        string text = fallback;
        if (LocalizationManager.Instance != null)
        {
            string loc = LocalizationManager.Instance.Get(key);
            if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) text = loc;
        }

        objectiveText.text = text;
        objectiveText.gameObject.SetActive(true);
    }
}
