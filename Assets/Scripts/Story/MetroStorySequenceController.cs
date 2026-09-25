using System;
using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MetroStorySequenceController : MonoBehaviour
{
    private static MetroStorySequenceController _instance;
    public static MetroStorySequenceController Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<MetroStorySequenceController>();
            return _instance;
        }
        private set => _instance = value;
    }

    public static string GetLoc(string key)
    {
        string text = key;
        try
        {
            if (LocalizationManager.Instance != null)
                text = LocalizationManager.Instance.Get(key);
            else
            {
                var loc = FindAnyObjectByType<LocalizationManager>();
                if (loc != null)
                    text = loc.Get(key);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MetroStory] GetLoc error for '{key}': {e.Message}");
        }

        if (!string.IsNullOrEmpty(text) && text.Contains("{TEDDY_NAME}"))
        {
            text = text.Replace("{TEDDY_NAME}", TeddyName);
        }
        if (!string.IsNullOrEmpty(text) && text.Contains("{CHILD_NAME}"))
        {
            text = text.Replace("{CHILD_NAME}", ChildName);
        }

        return text;
    }

    private IEnumerator SafeShowDialogue(
        string[] lines,
        bool hasChoice = false,
        Color? dialogueColor = null,
        TMP_FontAsset dialogueFont = null,
        Action<int, string> onLineStarted = null)
    {
        var diag = NpcDialogueManager.Instance;
        if (diag == null)
            diag = FindAnyObjectByType<NpcDialogueManager>();

        if (diag != null && Application.isPlaying)
        {
            yield return StartCoroutine(diag.ShowDialogue(
                lines,
                hasChoice: hasChoice,
                dialogueColor: dialogueColor ?? Color.white,
                dialogueFont: dialogueFont,
                voiceSource: null,
                voiceClips: null,
                onLineStarted: onLineStarted
            ));
        }
        else
        {
            if (diag != null && !Application.isPlaying)
            {
                Debug.Log($"[MetroStory EditMode] Simulating dialogue ({lines.Length} lines)");
            }
            else
            {
                Debug.LogWarning("[MetroStory] NpcDialogueManager not available. Skipping dialogue wait.");
            }
            yield return null;
        }
    }

    public enum StoryPhase
    {
        Floor1_Exploration,     // 0: Floor 1 - talk to NPCs (Neighbor, Shop Owner), search for Transit Pass
        Floor1_ElevatorReady,   // 1: Floor 1 - Elevator unlocked, objective points up stairs
        Elevator_Travel,        // 2: Inside elevator - doors close, travelling
        Elevator_TrainFalseStop,// 3: False stop - doors open onto void track, train speeds past
        Floor3_Arrival,         // 4: Arrived at Floor 3 - doors open, explore Floor 3
        Floor3_NpcEncounter,    // 5: Floor 3 - Meet Bully and inspect Floor 3 clues
        Floor3_TruthOrDare,     // 6: Truth or Dare choice on Floor 3
        Floor3_TruthResolution, // 7: Truth branch dialogue
        Floor3_DareObjective,   // 8: Dare branch - walk to end of Floor 3 platform
        TeddyTurn,              // 9: Teddy says "Wake up" -> door knocking starts
        DoorWaiting,            // 10: Player walks to Door on Floor 3
        BehindDoorCinematic,    // 11: Teacher, Mother, Father cinematic
        ChildEpisode,           // 12: Child seizure / panic episode
        FinalTeddyMoment,       // 13: Final calm teddy view on Floor 3
        EndOfPrototype,         // 14: Fade to black, End UI, Restart
        DarkRoom_Reveal,        // 15: Teddy explains brother is gone and reveals he is imagination
        MoroccanBeach_Transition, // 16: Transition directly to MoroccanBeach scene
        Classroom_Return,       // 17: Return to S1 classroom scene
        Classroom_ClockShot,    // 18: Camera focused on clock moving forward
        Classroom_TeacherEnding,// 19: Teacher asks "Are you with us?"
        EndOfEpisode            // 20: Fade to black, END OF EPISODE screen, restart
    }

    [Header("Teddy Identity (Single Source of Truth)")]
    [Tooltip("The single variable storing the teddy bear's name.")]
    public string teddyName = "Mistfox";
    public static string TeddyName => Instance != null ? Instance.teddyName : "Mistfox";

    [Header("Child Identity")]
    public string childName = "Yassir";
    public static string ChildName => Instance != null ? Instance.childName : "Yassir";

    [Header("Current Phase")]
    public StoryPhase currentPhase = StoryPhase.Floor1_Exploration;

    [Header("Floor 1 Exploration Tasks")]
    public bool transitPassCollected = false;
    public bool vendingMachineInspected = false;
    public bool intercomInspected = false;
    public bool isPowerRestored = false;
    public bool fuseBoxInspected = false;
    public bool neighborGaveAdvice = false;
    public bool IsElevatorUnlocked => transitPassCollected;

    [Header("Floor 3 Exploration Tasks")]
    public bool departureBoardInspected = false;
    public bool teddyLostPieceCollected = false;
    public GameObject floor3BullyObject;

    [Header("NPC References")]
    public NpcConversation neighborConversation;
    public NpcConversation shopOwnerConversation;
    public NpcConversation bullyConversation;

    [Header("NPC Spoken Status")]
    public bool neighborSpoken = false;
    public bool shopOwnerSpoken = false;
    public bool bullySpoken = false;

    public bool AllFloor1NpcsSpoken => neighborSpoken && shopOwnerSpoken;
    public bool AllNpcsSpoken => neighborSpoken && shopOwnerSpoken && bullySpoken;

    [Header("Teddy References")]
    public GameObject teddyBearObject;
    public NpcConversation teddyConversation;
    public CarryableItem teddyCarryable;

    [Header("Dare Objective")]
    public GameObject platformEndTrigger;
    public bool dareObjectiveCompleted = false;

    [Header("Door & Cinematic")]
    public GameObject doorObject;
    public AudioSource doorAudioSource;
    public BoxCollider doorTrigger;
    public CinemachineCamera behindDoorVcam;
    public CinemachineCamera finalTeddyVcam;
    public Transform teacherTransform;
    public Transform motherTransform;
    public Transform fatherTransform;

    [Header("Interrogation Cinematic Cameras")]
    public CinemachineCamera interrogationWideVCam;
    public CinemachineCamera interrogationTeacherVCam;
    public CinemachineCamera interrogationMotherVCam;
    public CinemachineCamera interrogationFatherVCam;
    public CinemachineCamera interrogationDramaticVCam;

    [Header("Dark Room Staging")]
    public GameObject darkRoomObject;
    public CinemachineCamera darkRoomVcam;

    [Header("Player References")]
    public FirstPersonController fpsController;
    public Transform playerCamera;
    public CinemachineBrain cinemachineBrain;

    [Header("UI References")]
    public GameObject endPrototypePanel;
    public Button restartButton;
    public ScreenFader screenFader;
    public TextMeshProUGUI objectiveText;

    private bool sequenceBusy = false;

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
        ResolveReferences();
        HookConversations();

        if (platformEndTrigger != null)
            platformEndTrigger.SetActive(false);

        if (doorTrigger != null)
            doorTrigger.enabled = false;

        if (endPrototypePanel != null)
            endPrototypePanel.SetActive(false);

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartSequence);
            restartButton.onClick.AddListener(RestartSequence);
        }

        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "Find Teddy...";
        }

        ResetCinematicCameras();
        if (cinemachineBrain != null)
            cinemachineBrain.enabled = false;

        if (fpsController != null)
        {
            fpsController.SetControlLocked(false);
            if (fpsController.playerCamera != null)
            {
                fpsController.playerCamera.transform.localPosition = Vector3.zero;
                fpsController.playerCamera.transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.T))
        {
            TryHandleTeddyInteraction();
        }

        if (currentPhase == StoryPhase.EndOfPrototype)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartSequence();
            }
        }
    }

    public void ResolveReferences()
    {
        if (fpsController == null)
            fpsController = FindAnyObjectByType<FirstPersonController>();

        if (fpsController != null && playerCamera == null && fpsController.playerCamera != null)
            playerCamera = fpsController.playerCamera.transform;

        if (cinemachineBrain == null && playerCamera != null)
            cinemachineBrain = playerCamera.GetComponent<CinemachineBrain>();

        if (screenFader == null)
            screenFader = FindAnyObjectByType<ScreenFader>();

        if (teddyBearObject == null)
            teddyBearObject = GameObject.Find("Teddy_Bear_Box");

        if (teddyBearObject != null)
        {
            if (teddyConversation == null)
                teddyConversation = teddyBearObject.GetComponent<NpcConversation>();
            if (teddyCarryable == null)
                teddyCarryable = teddyBearObject.GetComponent<CarryableItem>();
        }

        if (darkRoomObject == null)
            darkRoomObject = GameObject.Find("DarkRoom_Environment");
        if (darkRoomVcam == null)
        {
            var dv = GameObject.Find("DarkRoomVcam");
            if (dv != null) darkRoomVcam = dv.GetComponent<CinemachineCamera>();
        }

        if (interrogationWideVCam == null)
            interrogationWideVCam = GameObject.Find("Interrogation_WideVCam")?.GetComponent<CinemachineCamera>();
        if (interrogationTeacherVCam == null)
            interrogationTeacherVCam = GameObject.Find("Interrogation_TeacherVCam")?.GetComponent<CinemachineCamera>();
        if (interrogationMotherVCam == null)
            interrogationMotherVCam = GameObject.Find("Interrogation_MotherVCam")?.GetComponent<CinemachineCamera>();
        if (interrogationFatherVCam == null)
            interrogationFatherVCam = GameObject.Find("Interrogation_FatherVCam")?.GetComponent<CinemachineCamera>();
        if (interrogationDramaticVCam == null)
            interrogationDramaticVCam = GameObject.Find("Interrogation_DramaticVCam")?.GetComponent<CinemachineCamera>();

        if (behindDoorVcam == null)
            behindDoorVcam = interrogationWideVCam ?? GameObject.Find("BehindDoorVcam")?.GetComponent<CinemachineCamera>();

        if (teacherTransform == null)
            teacherTransform = GameObject.Find("Teacher")?.transform;
        if (motherTransform == null)
            motherTransform = GameObject.Find("Mother")?.transform;
        if (fatherTransform == null)
            fatherTransform = GameObject.Find("Father")?.transform;

        if (neighborConversation == null)
        {
            var neighbor = GameObject.Find("Floor1/Neigbhor_NPC") ?? GameObject.Find("Neigbhor_NPC");
            if (neighbor != null) neighborConversation = neighbor.GetComponent<NpcConversation>();
        }

        if (shopOwnerConversation == null)
        {
            var shopOwner = GameObject.Find("Floor1/ShopOwner_NPC") ?? GameObject.Find("ShopOwner_NPC");
            if (shopOwner != null) shopOwnerConversation = shopOwner.GetComponent<NpcConversation>();
        }

        if (bullyConversation == null)
        {
            var allBullys = Resources.FindObjectsOfTypeAll<NpcConversation>();
            foreach (var c in allBullys)
            {
                if (c.gameObject.scene.name == "Gameplay3" && c.gameObject.name == "Bully_NPC")
                {
                    bullyConversation = c;
                    break;
                }
            }
        }

        var elevBtn = FindAnyObjectByType<ElevatorButton>();
        if (elevBtn != null)
        {
            elevBtn.OnElevatorRideStarted -= OnElevatorStarted;
            elevBtn.OnElevatorRideStarted += OnElevatorStarted;
            elevBtn.OnFalseStopTriggered -= OnElevatorFalseStop;
            elevBtn.OnFalseStopTriggered += OnElevatorFalseStop;
            elevBtn.OnArrivalOnNewFloor -= () => OnFloorArrived(1);
            elevBtn.OnArrivalOnNewFloor += () => OnFloorArrived(1);
        }
    }

    private void HookConversations()
    {
        if (neighborConversation != null)
        {
            neighborConversation.OnConversationFinished -= HandleNeighborFinished;
            neighborConversation.OnConversationFinished += HandleNeighborFinished;
        }

        if (shopOwnerConversation != null)
        {
            shopOwnerConversation.OnConversationFinished -= HandleShopOwnerFinished;
            shopOwnerConversation.OnConversationFinished += HandleShopOwnerFinished;
        }

        if (bullyConversation != null)
        {
            bullyConversation.OnConversationFinished -= HandleBullyFinished;
            bullyConversation.OnConversationFinished += HandleBullyFinished;
        }
    }

    private void HandleNeighborFinished()
    {
        if (fuseBoxInspected && !neighborGaveAdvice)
        {
            neighborGaveAdvice = true;
            Debug.Log("[MetroStory] Neighbor gave electrical advice!");
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
                objectiveText.text = "Restore the circuit...";
            }

            if (neighborConversation != null)
            {
                neighborConversation.lineKeys = new string[] {
                    "neighbor_fuse_reminder_01"
                };
            }
        }

        OnNpcSpoken(0);
    }

    public void OnFuseBoxInspected()
    {
        if (fuseBoxInspected) return;
        fuseBoxInspected = true;
        Debug.Log("[MetroStory] Fusebox inspected! Neighbor advice quest activated.");

        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "The neighbor might know...";
        }

        if (neighborConversation != null)
        {
            neighborConversation.lineKeys = new string[] {
                "neighbor_fuse_advice_01",
                "neighbor_fuse_advice_02",
                "neighbor_fuse_advice_03",
                "neighbor_fuse_advice_04",
                "neighbor_fuse_advice_05"
            };
        }
    }
    private void HandleShopOwnerFinished() => OnNpcSpoken(1);
    private void HandleBullyFinished() => OnNpcSpoken(2);

    public void OnNpcSpoken(int npcIndex)
    {
        switch (npcIndex)
        {
            case 0:
                neighborSpoken = true;
                Debug.Log("[MetroStory] Neighbor conversation finished.");
                break;
            case 1:
                shopOwnerSpoken = true;
                Debug.Log("[MetroStory] Shop Owner conversation finished.");
                break;
            case 2:
                bullySpoken = true;
                Debug.Log("[MetroStory] Bully conversation finished.");
                break;
        }

        if (npcIndex == 0 || npcIndex == 1)
        {
            CheckFloor1Progress();
        }
        else if (npcIndex == 2)
        {
            CheckFloor3Progress();
        }
    }

    public void OnPlaceholderInteracted(InteractivePlaceholderItem item)
    {
        if (item == null) return;
        switch (item.itemType)
        {
            case InteractivePlaceholderItem.PlaceholderType.Floor1_TransitCard:
                transitPassCollected = true;
                Debug.Log("[MetroStory] Transit Keycard collected!");
                CheckFloor1Progress();
                break;
            case InteractivePlaceholderItem.PlaceholderType.Floor1_VendingMachine:
                vendingMachineInspected = true;
                Debug.Log("[MetroStory] Vending machine inspected.");
                CheckFloor1Progress();
                break;
            case InteractivePlaceholderItem.PlaceholderType.Floor1_Intercom:
                intercomInspected = true;
                Debug.Log("[MetroStory] Intercom inspected.");
                CheckFloor1Progress();
                break;
            case InteractivePlaceholderItem.PlaceholderType.Floor3_DepartureBoard:
                departureBoardInspected = true;
                Debug.Log("[MetroStory] Departure board inspected.");
                CheckFloor3Progress();
                break;
            case InteractivePlaceholderItem.PlaceholderType.Floor3_LostTeddyPiece:
                teddyLostPieceCollected = true;
                Debug.Log("[MetroStory] Lost teddy ribbon collected.");
                CheckFloor3Progress();
                break;
        }
    }

    public void OnPowerRestored()
    {
        isPowerRestored = true;
        Debug.Log("[MetroStory] Elevator power restored via fuse box puzzle!");

        if (IsElevatorUnlocked)
        {
            currentPhase = StoryPhase.Floor1_ElevatorReady;
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
                objectiveText.text = "Head upstairs...";
            }
        }
        else
        {
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
                objectiveText.text = "Check the elevator...";
            }
        }
    }

    public void OnElevatorTicketTriggered()
    {
        if (!IsElevatorUnlocked)
        {
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
                objectiveText.text = "Need a ticket...";
            }
        }
    }

    public void CheckFloor1Progress()
    {
        if (currentPhase == StoryPhase.Floor1_Exploration && IsElevatorUnlocked)
        {
            if (isPowerRestored)
            {
                currentPhase = StoryPhase.Floor1_ElevatorReady;
                Debug.Log("[MetroStory] Floor 1 requirements met & power restored. Elevator is unlocked!");
                if (objectiveText != null)
                {
                    objectiveText.gameObject.SetActive(true);
                    objectiveText.text = "Head upstairs...";
                }
            }
            else
            {
                Debug.Log("[MetroStory] Floor 1 people spoken/pass collected, but elevator has no power!");
                if (objectiveText != null)
                {
                    objectiveText.gameObject.SetActive(true);
                    objectiveText.text = "The grid is dead...";
                }
            }
        }
    }

    private bool isTruthOrDareStarted = false;

    public void CheckFloor3Progress()
    {
        if (bullySpoken && !isTruthOrDareStarted)
        {
            currentPhase = StoryPhase.Floor3_TruthOrDare;
            Debug.Log("[MetroStory] Bully spoken on Floor 3. Waiting 1.5 seconds then auto-firing Teddy dialogue...");
            StartCoroutine(AutoTriggerTeddyRoutine());
        }
    }

    private bool isAutoTriggeringTeddy = false;

    private IEnumerator AutoTriggerTeddyRoutine()
    {
        isAutoTriggeringTeddy = true;
        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "...";
        }

        if (Application.isPlaying)
        {
            yield return new WaitForSeconds(1.5f);
        }
        else
        {
            yield return null;
        }

        isAutoTriggeringTeddy = false;
        yield return StartCoroutine(TruthOrDareSequence());
    }

    public void OnElevatorStarted()
    {
        currentPhase = StoryPhase.Elevator_Travel;
        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "...";
        }
        Debug.Log("[MetroStory] Elevator travel started.");
    }

    public void OnElevatorFalseStop()
    {
        currentPhase = StoryPhase.Elevator_TrainFalseStop;
        Debug.Log("[MetroStory] Elevator false stop triggered on void track.");
    }

    public void OnFloorArrived(int floorIndex)
    {
        currentPhase = StoryPhase.Floor3_Arrival;
        ActivateFloor3StoryElements();
        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "Where are we...?";
        }
        Debug.Log($"[MetroStory] Arrived on Floor 3 (Index {floorIndex}).");
    }

    public void ActivateFloor3StoryElements()
    {
        if (floor3BullyObject != null)
            floor3BullyObject.SetActive(true);

        if (bullyConversation == null)
        {
            var allBullys = Resources.FindObjectsOfTypeAll<NpcConversation>();
            foreach (var c in allBullys)
            {
                if (c.gameObject.scene.name == "Gameplay3" && c.gameObject.name == "Bully_NPC")
                {
                    bullyConversation = c;
                    break;
                }
            }
        }

        if (bullyConversation != null)
        {
            bullyConversation.OnConversationFinished -= HandleBullyFinished;
            bullyConversation.OnConversationFinished += HandleBullyFinished;
        }

        if (doorObject != null)
            doorObject.SetActive(true);

        var f3 = GameObject.Find("Floor3");
        if (f3 != null)
        {
            var doorInF3 = f3.transform.Find("Door_Behind");
            if (doorInF3 != null) doorInF3.gameObject.SetActive(true);
        }
    }

    public bool IsTeddyCarried
    {
        get
        {
            if (teddyCarryable != null) return teddyCarryable.IsBeingCarried;
            var tb = GameObject.Find("Teddy_Bear_Box");
            var c = tb != null ? tb.GetComponent<CarryableItem>() : null;
            return c != null && c.IsBeingCarried;
        }
    }

    public void EnsureTeddyCarried()
    {
        if (teddyCarryable == null)
        {
            if (teddyBearObject != null)
                teddyCarryable = teddyBearObject.GetComponent<CarryableItem>();
            else
            {
                var tb = GameObject.Find("Teddy_Bear_Box");
                if (tb != null)
                {
                    teddyBearObject = tb;
                    teddyCarryable = tb.GetComponent<CarryableItem>();
                }
            }
        }

        if (teddyCarryable != null && !teddyCarryable.IsBeingCarried)
        {
            Transform cp = null;
            if (fpsController != null)
            {
                cp = fpsController.GetComponentInChildren<Camera>()?.transform.Find("CarryPoint") 
                     ?? fpsController.transform.Find("CarryPoint");
            }
            if (cp == null && playerCamera != null)
            {
                cp = playerCamera.Find("CarryPoint");
            }
            if (cp == null)
            {
                var foundCp = GameObject.Find("CarryPoint");
                if (foundCp != null) cp = foundCp.transform;
            }

            if (cp != null)
            {
                teddyCarryable.StartCarrying(cp);
                Debug.Log("[MetroStory] Teddy snapped to player's CarryPoint.");
            }
        }
    }

    public bool TryHandleTeddyInteraction()
    {
        var dm = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();
        if (dm != null && !dm.IsDialogueRunning)
        {
            sequenceBusy = false;
        }

        if (sequenceBusy || isAutoTriggeringTeddy)
        {
            Debug.Log("[MetroStory] Sequence currently busy, ignoring Teddy interaction.");
            return true;
        }

        EnsureTeddyCarried();

        if (currentPhase == StoryPhase.Floor1_Exploration || currentPhase == StoryPhase.Floor1_ElevatorReady)
        {
            if (!IsElevatorUnlocked)
            {
                StartCoroutine(PlaySimpleTeddyLines(new string[] {
                    "Teddy: This place feels strange.",
                    "Teddy: Search the platform and see what those people waiting want."
                }));
                return true;
            }
            else
            {
                StartCoroutine(PlaySimpleTeddyLines(new string[] {
                    "Teddy: Look! The elevator stairs are open.",
                    "Teddy: Let's go up before another train comes."
                }));
                return true;
            }
        }
        else if (currentPhase == StoryPhase.Floor3_Arrival || currentPhase == StoryPhase.Floor3_NpcEncounter || currentPhase == StoryPhase.Floor3_TruthOrDare || bullySpoken)
        {
            if (!bullySpoken)
            {
                StartCoroutine(PlaySimpleTeddyLines(new string[] {
                    "Teddy: Someone is standing down this platform...",
                    "Teddy: Go see who it is."
                }));
                return true;
            }
            else
            {
                if (!isTruthOrDareStarted)
                {
                    StartCoroutine(TruthOrDareSequence());
                }
                return true;
            }
        }

        return false;
    }

    private IEnumerator PlaySimpleTeddyLines(string[] rawLines)
    {
        sequenceBusy = true;
        if (fpsController != null) fpsController.SetControlLocked(true);

        yield return StartCoroutine(SafeShowDialogue(
            rawLines,
            hasChoice: false,
            dialogueColor: Color.white,
            dialogueFont: null
        ));

        // If teddy is not carried yet, pick it up into hands
        if (teddyCarryable != null && !teddyCarryable.IsBeingCarried && fpsController != null)
        {
            var cp = fpsController.GetComponentInChildren<Camera>()?.transform.Find("CarryPoint") ?? fpsController.transform.Find("CarryPoint");
            if (cp != null)
            {
                teddyCarryable.StartCarrying(cp);
            }
        }

        if (fpsController != null) fpsController.SetControlLocked(false);
        sequenceBusy = false;
    }

    public IEnumerator TruthOrDareSequence()
    {
        sequenceBusy = true;
        currentPhase = StoryPhase.Floor3_TruthOrDare;

        EnsureTeddyCarried();

        if (objectiveText != null) objectiveText.gameObject.SetActive(false);
        if (fpsController != null) fpsController.SetControlLocked(true);

        string[] introLines = new string[] {
            GetLoc("teddy_met_people_01"),
            GetLoc("teddy_met_people_02")
        };

        if (NpcDialogueManager.Instance != null)
        {
            NpcDialogueManager.Instance.SetChoiceLabels("TRUTH", "DARE");
        }

        yield return StartCoroutine(SafeShowDialogue(
            introLines,
            hasChoice: true,
            dialogueColor: Color.white,
            dialogueFont: null
        ));

        int choice = NpcDialogueManager.Instance != null ? NpcDialogueManager.Instance.lastChoiceIndex : 0;
        Debug.Log($"[MetroStory] Player chose Truth or Dare: {choice} (0=Truth, 1=Dare)");

        if (choice == 0)
        {
            yield return StartCoroutine(TruthBranchRoutine());
        }
        else
        {
            yield return StartCoroutine(DareBranchRoutine());
        }
    }

    private IEnumerator TruthBranchRoutine()
    {
        currentPhase = StoryPhase.Floor3_TruthResolution;

        string[] truthLines = new string[] {
            GetLoc("truth_01"),
            GetLoc("truth_02"),
            GetLoc("truth_03"),
            GetLoc("truth_04")
        };

        yield return StartCoroutine(SafeShowDialogue(
            truthLines,
            hasChoice: false,
            dialogueColor: Color.white,
            dialogueFont: null
        ));

        yield return new WaitForSeconds(0.8f);

        yield return StartCoroutine(TeddyTurnRoutine());
    }

    private IEnumerator DareBranchRoutine()
    {
        currentPhase = StoryPhase.Floor3_DareObjective;

        if (platformEndTrigger != null)
            platformEndTrigger.SetActive(false);

        string[] dareLines = new string[] {
            GetLoc("dare_01"),
            GetLoc("dare_02")
        };

        yield return StartCoroutine(SafeShowDialogue(
            dareLines,
            hasChoice: false,
            dialogueColor: Color.white,
            dialogueFont: null
        ));

        // The psychological Dare: Close eyes, darkness envelops screen
        if (screenFader != null)
        {
            bool fadeDone = false;
            screenFader.FadeToBlack(1.0f, () => fadeDone = true);
            while (!fadeDone) yield return null;
        }

        // Camera subtly trembles in the darkness with ambient echo
        if (playerCamera != null)
        {
            StartCoroutine(CameraShakeRoutine(playerCamera, 1.5f, 0.02f));
        }
        yield return new WaitForSeconds(1.8f);

        // Open eyes: Fade back in from black
        if (screenFader != null)
        {
            screenFader.FadeFromBlack(1.0f);
        }
        yield return new WaitForSeconds(0.6f);

        dareObjectiveCompleted = true;

        string[] dareCompleteLines = new string[] {
            GetLoc("dare_complete_01")
        };

        yield return StartCoroutine(SafeShowDialogue(
            dareCompleteLines,
            hasChoice: false,
            dialogueColor: Color.white,
            dialogueFont: null
        ));

        yield return new WaitForSeconds(0.8f);

        yield return StartCoroutine(TeddyTurnRoutine());
    }

    public void OnPlatformEndReached()
    {
        if (platformEndTrigger != null)
            platformEndTrigger.SetActive(false);
    }

    private IEnumerator TeddyTurnRoutine()
    {
        currentPhase = StoryPhase.TeddyTurn;
        sequenceBusy = true;
        if (fpsController != null) fpsController.SetControlLocked(true);

        string[] turnLines = new string[] {
            GetLoc("turn_01"),
            GetLoc("turn_02"),
            GetLoc("turn_03"),
            GetLoc("turn_04"),
            GetLoc("turn_05"),
            GetLoc("turn_06")
        };

        yield return StartCoroutine(SafeShowDialogue(
            turnLines,
            hasChoice: false,
            dialogueColor: Color.white,
            dialogueFont: null
        ));

        if (doorAudioSource != null && !doorAudioSource.isPlaying)
        {
            doorAudioSource.Play();
        }

        if (doorTrigger != null)
            doorTrigger.enabled = true;

        currentPhase = StoryPhase.DoorWaiting;

        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "Behind the door...";
        }

        if (fpsController != null) fpsController.SetControlLocked(false);
        sequenceBusy = false;
        Debug.Log("[MetroStory] Teddy's turn finished. Door trigger active!");
    }

    public void OnDoorInteracted()
    {
        if (currentPhase != StoryPhase.DoorWaiting || sequenceBusy) return;
        StartCoroutine(BehindDoorCinematicRoutine());
    }

    public IEnumerator BehindDoorCinematicRoutine()
    {
        sequenceBusy = true;
        currentPhase = StoryPhase.BehindDoorCinematic;

        if (doorTrigger != null) doorTrigger.enabled = false;
        if (objectiveText != null) objectiveText.gameObject.SetActive(false);

        if (fpsController != null)
        {
            fpsController.SetControlLocked(true);
        }

        // Establish initial interrogation camera (Teacher or Wide)
        SwitchDialogueCamera(interrogationTeacherVCam ?? behindDoorVcam);

        string[] cinematicLines = new string[] {
            GetLoc("cinematic_01"),
            GetLoc("cinematic_02"),
            GetLoc("cinematic_03"),
            GetLoc("cinematic_04"),
            GetLoc("cinematic_05"),
            GetLoc("cinematic_06"),
            GetLoc("cinematic_07"),
            GetLoc("cinematic_08"),
            GetLoc("cinematic_09"),
            GetLoc("scream_01"),
            GetLoc("scream_02")
        };

        yield return StartCoroutine(SafeShowDialogue(
            cinematicLines,
            hasChoice: false,
            dialogueColor: new Color(1f, 0.85f, 0.85f),
            dialogueFont: null,
            onLineStarted: OnBehindDoorCinematicLineStarted
        ));

        yield return StartCoroutine(ChildEpisodeRoutine());
    }

    public void SwitchDialogueCamera(CinemachineCamera targetVcam)
    {
        if (targetVcam == null)
            targetVcam = interrogationWideVCam ?? behindDoorVcam;

        if (targetVcam == null) return;

        ResetCinematicCameras();
        targetVcam.Priority.Value = 200;

        if (playerCamera != null)
        {
            playerCamera.position = targetVcam.transform.position;
            playerCamera.rotation = targetVcam.transform.rotation;
        }

        if (cinemachineBrain != null)
        {
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut,
                0f
            );
            cinemachineBrain.enabled = true;
        }
    }

    public void ResetCinematicCameras()
    {
        if (behindDoorVcam != null) behindDoorVcam.Priority.Value = 0;
        if (interrogationWideVCam != null) interrogationWideVCam.Priority.Value = 0;
        if (interrogationTeacherVCam != null) interrogationTeacherVCam.Priority.Value = 0;
        if (interrogationMotherVCam != null) interrogationMotherVCam.Priority.Value = 0;
        if (interrogationFatherVCam != null) interrogationFatherVCam.Priority.Value = 0;
        if (interrogationDramaticVCam != null) interrogationDramaticVCam.Priority.Value = 0;

        var pv = GameObject.Find("PlayerVcam")?.GetComponent<CinemachineCamera>();
        if (pv != null) pv.Priority.Value = 0;

        var tz = GameObject.Find("TalkZoomVcam")?.GetComponent<CinemachineCamera>();
        if (tz != null) tz.Priority.Value = 0;
    }

    private void OnBehindDoorCinematicLineStarted(int index, string line)
    {
        switch (index)
        {
            case 0: // Teacher: "It's getting worse."
                SwitchDialogueCamera(interrogationTeacherVCam);
                break;
            case 1: // Mother: "What do we do?"
                SwitchDialogueCamera(interrogationMotherVCam);
                break;
            case 2: // Teacher: "He keeps talking to someone."
                SwitchDialogueCamera(interrogationTeacherVCam);
                break;
            case 3: // Father: "Who?"
                SwitchDialogueCamera(interrogationFatherVCam);
                break;
            case 4: // Teacher: "He calls him by a name."
            case 5: // Teacher: "But there is nobody there."
                SwitchDialogueCamera(interrogationTeacherVCam);
                break;
            case 6: // Mother: "No... please..."
                SwitchDialogueCamera(interrogationMotherVCam);
                break;
            case 7: // Teacher: "He keeps saying that someone is waiting for him."
                SwitchDialogueCamera(interrogationTeacherVCam);
                break;
            case 8: // Teacher: "He keeps screaming that name." -> Wide establishing shot
                SwitchDialogueCamera(interrogationWideVCam);
                break;
            case 9: // Child scream 1
            case 10: // Child scream 2
                SwitchDialogueCamera(interrogationDramaticVCam);
                break;
            default:
                SwitchDialogueCamera(interrogationWideVCam);
                break;
        }
    }

    private void OnChildEpisodeLineStarted(int index, string line)
    {
        switch (index)
        {
            case 0: // Teacher: "It's happening again!"
                SwitchDialogueCamera(interrogationTeacherVCam);
                break;
            case 1: // Mother: "My son!"
                SwitchDialogueCamera(interrogationMotherVCam);
                break;
            case 2: // Father: "Hold him!"
                SwitchDialogueCamera(interrogationFatherVCam);
                break;
            case 3: // Child: "{TEDDY_NAME}!"
                SwitchDialogueCamera(interrogationDramaticVCam);
                break;
            default:
                SwitchDialogueCamera(interrogationWideVCam);
                break;
        }
    }

    private IEnumerator ChildEpisodeRoutine()
    {
        currentPhase = StoryPhase.ChildEpisode;

        string[] episodeLines = new string[] {
            GetLoc("episode_01"),
            GetLoc("episode_02"),
            GetLoc("episode_03"),
            GetLoc("episode_04")
        };

        yield return StartCoroutine(SafeShowDialogue(
            episodeLines,
            hasChoice: false,
            dialogueColor: new Color(1f, 0.85f, 0.85f),
            dialogueFont: null,
            onLineStarted: OnChildEpisodeLineStarted
        ));

        // After seizure episode, continue directly into the Dark Room Teddy reveal
        yield return StartCoroutine(DarkRoomRoutine());
    }

    public IEnumerator DarkRoomRoutine()
    {
        currentPhase = StoryPhase.DarkRoom_Reveal;
        sequenceBusy = true;

        if (fpsController != null) fpsController.SetControlLocked(true);

        if (screenFader != null)
        {
            bool fadeOutDone = false;
            screenFader.FadeToBlack(1.2f, () => fadeOutDone = true);
            while (!fadeOutDone) yield return null;
        }

        ResetCinematicCameras();
        if (darkRoomObject != null) darkRoomObject.SetActive(true);

        if (darkRoomVcam != null)
        {
            darkRoomVcam.Priority.Value = 300;
            if (playerCamera != null)
            {
                playerCamera.position = darkRoomVcam.transform.position;
                playerCamera.rotation = darkRoomVcam.transform.rotation;
            }
        }

        if (screenFader != null)
        {
            screenFader.FadeFromBlack(1.0f);
        }

        yield return new WaitForSeconds(1.0f);

        string[] darkRoomLines = new string[] {
            GetLoc("darkroom_01"),
            GetLoc("darkroom_02"),
            GetLoc("darkroom_03"),
            GetLoc("darkroom_04"),
            GetLoc("darkroom_05"),
            GetLoc("darkroom_06"),
            GetLoc("darkroom_07"),
            GetLoc("darkroom_08")
        };

        yield return StartCoroutine(SafeShowDialogue(
            darkRoomLines,
            hasChoice: false,
            dialogueColor: new Color(0.9f, 0.95f, 1f),
            dialogueFont: null
        ));

        yield return new WaitForSeconds(1.0f);

        currentPhase = StoryPhase.MoroccanBeach_Transition;
        Debug.Log("[MetroStory] Dark room dialogue finished. Teddy takes the kid to the Moroccan Beach. Transitioning to MoroccanBeach...");

        if (screenFader != null)
        {
            ScreenFader.TransitionToScene("MoroccanBeach", 2.0f, shouldFadeAudio: true);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MoroccanBeach");
        }
    }

    public IEnumerator TransitionToMoroccanBeachRoutine()
    {
        currentPhase = StoryPhase.MoroccanBeach_Transition;
        if (screenFader != null)
        {
            ScreenFader.TransitionToScene("MoroccanBeach", 2.0f, shouldFadeAudio: true);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MoroccanBeach");
        }
        yield break;
    }

    private IEnumerator CameraShakeRoutine(Transform targetTransform, float duration, float magnitude)
    {
        if (targetTransform == null) yield break;
        Vector3 originalPos = targetTransform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            targetTransform.localPosition = originalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        targetTransform.localPosition = originalPos;
    }

    private IEnumerator FinalTeddyMomentRoutine()
    {
        currentPhase = StoryPhase.FinalTeddyMoment;

        ResetCinematicCameras();

        if (finalTeddyVcam != null)
        {
            finalTeddyVcam.Priority.Value = 120;
        }
        else
        {
            var talkVcam = GameObject.Find("TalkZoomVcam")?.GetComponent<CinemachineCamera>();
            if (talkVcam != null && teddyBearObject != null)
            {
                talkVcam.transform.position = teddyBearObject.transform.position + new Vector3(0, 0.8f, 1.8f);
                talkVcam.transform.rotation = Quaternion.LookRotation(teddyBearObject.transform.position - talkVcam.transform.position);
                talkVcam.Priority.Value = 120;
            }
        }

        yield return new WaitForSeconds(0.6f);

        string[] finalLines = new string[] {
            GetLoc("final_teddy_01"),
            GetLoc("final_teddy_02")
        };

        yield return StartCoroutine(SafeShowDialogue(
            finalLines,
            hasChoice: false,
            dialogueColor: new Color(0.9f, 0.95f, 1f),
            dialogueFont: null
        ));

        currentPhase = StoryPhase.EndOfPrototype;
        Debug.Log("[MetroStory] Sequence complete. Smoothly transitioning to MoroccanBeach...");
        ScreenFader.TransitionToScene("MoroccanBeach", 2.0f, shouldFadeAudio: true);
    }

    public void RestartSequence()
    {
        Debug.Log("[MetroStory] Transitioning to MoroccanBeach.");
        ScreenFader.TransitionToScene("MoroccanBeach", 2.0f);
    }

    [ContextMenu("Debug: Trigger Transition to Moroccan Beach")]
    public void DebugTriggerBeachTransition()
    {
        ScreenFader.TransitionToScene("MoroccanBeach", 2.0f);
    }

    [ContextMenu("Debug: Mark All NPCs Spoken")]
    public void DebugMarkAllNpcsSpoken()
    {
        neighborSpoken = true;
        shopOwnerSpoken = true;
        bullySpoken = true;
        Debug.Log("[MetroStory DEBUG] All NPCs marked spoken.");
    }

    [ContextMenu("Debug: Trigger Truth Branch")]
    public void DebugTriggerTruthBranch()
    {
        DebugMarkAllNpcsSpoken();
        StartCoroutine(TruthBranchRoutine());
    }

    [ContextMenu("Debug: Trigger Dare Branch")]
    public void DebugTriggerDareBranch()
    {
        DebugMarkAllNpcsSpoken();
        StartCoroutine(DareBranchRoutine());
    }

    [ContextMenu("Debug: Trigger Behind Door Cinematic")]
    public void DebugTriggerCinematic()
    {
        StartCoroutine(BehindDoorCinematicRoutine());
    }

    [ContextMenu("Debug: Trigger Episode")]
    public void DebugTriggerEpisode()
    {
        StartCoroutine(ChildEpisodeRoutine());
    }

    [ContextMenu("Debug: Trigger Final Teddy Moment")]
    public void DebugTriggerFinalTeddy()
    {
        StartCoroutine(FinalTeddyMomentRoutine());
    }
}