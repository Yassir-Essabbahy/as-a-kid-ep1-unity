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

        return text;
    }

    private IEnumerator SafeShowDialogue(string[] lines, bool hasChoice = false, Color? dialogueColor = null, TMP_FontAsset dialogueFont = null)
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
                dialogueFont: dialogueFont
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
        EndOfPrototype          // 14: Fade to black, End UI, Restart
    }

    [Header("Teddy Identity (Single Source of Truth)")]
    [Tooltip("The single variable storing the teddy bear's name.")]
    public string teddyName = "Mistfox";
    public static string TeddyName => Instance != null ? Instance.teddyName : "Mistfox";

    [Header("Current Phase")]
    public StoryPhase currentPhase = StoryPhase.Floor1_Exploration;

    [Header("Floor 1 Exploration Tasks")]
    public bool transitPassCollected = false;
    public bool vendingMachineInspected = false;
    public bool intercomInspected = false;
    public bool IsElevatorUnlocked => transitPassCollected || (neighborSpoken && shopOwnerSpoken);

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
        EnsureTeddyCarried();

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
            objectiveText.text = "Explore the platform. Speak with the people waiting.";
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
            neighborConversation.OnConversationFinished += () => OnNpcSpoken(0);

        if (shopOwnerConversation != null)
            shopOwnerConversation.OnConversationFinished += () => OnNpcSpoken(1);

        if (bullyConversation != null)
            bullyConversation.OnConversationFinished += () => OnNpcSpoken(2);
    }

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

    public void CheckFloor1Progress()
    {
        if (currentPhase == StoryPhase.Floor1_Exploration && IsElevatorUnlocked)
        {
            currentPhase = StoryPhase.Floor1_ElevatorReady;
            Debug.Log("[MetroStory] Floor 1 requirements met. Elevator is unlocked!");
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
                objectiveText.text = "Walk up the stairs and take the elevator to the upper platform.";
            }
        }
    }

    public void CheckFloor3Progress()
    {
        if (bullySpoken)
        {
            currentPhase = StoryPhase.Floor3_TruthOrDare;
            Debug.Log("[MetroStory] Bully spoken on Floor 3. Truth or Dare is primed!");
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
                objectiveText.text = "Press 'F' to talk to your Teddy Bear...";
            }
        }
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
        EnsureTeddyCarried();
        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "Explore Floor 3. Find out what this place is.";
        }
        Debug.Log($"[MetroStory] Arrived on Floor 3 (Index {floorIndex}).");
    }

    public void ActivateFloor3StoryElements()
    {
        if (floor3BullyObject != null)
            floor3BullyObject.SetActive(true);

        if (doorObject != null)
            doorObject.SetActive(true);

        var f3 = GameObject.Find("Floor3");
        if (f3 != null)
        {
            var doorInF3 = f3.transform.Find("Door_Behind");
            if (doorInF3 != null) doorInF3.gameObject.SetActive(true);
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

        if (sequenceBusy)
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
                StartCoroutine(TruthOrDareSequence());
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

        if (platformEndTrigger != null)
            platformEndTrigger.SetActive(true);

        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(true);
            objectiveText.text = "Dare: Walk to the end of the platform.";
        }

        if (fpsController != null) fpsController.SetControlLocked(false);
        sequenceBusy = false;
    }

    public void OnPlatformEndReached()
    {
        if (currentPhase != StoryPhase.Floor3_DareObjective || dareObjectiveCompleted) return;
        dareObjectiveCompleted = true;

        if (platformEndTrigger != null)
            platformEndTrigger.SetActive(false);

        if (objectiveText != null)
            objectiveText.gameObject.SetActive(false);

        StartCoroutine(CompleteDareRoutine());
    }

    private IEnumerator CompleteDareRoutine()
    {
        sequenceBusy = true;
        if (fpsController != null) fpsController.SetControlLocked(true);

        string[] lines = new string[] {
            GetLoc("dare_complete_01")
        };

        yield return StartCoroutine(SafeShowDialogue(
            lines,
            hasChoice: false,
            dialogueColor: Color.white,
            dialogueFont: null
        ));

        yield return new WaitForSeconds(0.8f);

        yield return StartCoroutine(TeddyTurnRoutine());
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
            objectiveText.text = "Investigate the sounds coming from the door...";
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

        if (cinemachineBrain != null && behindDoorVcam != null)
        {
            behindDoorVcam.Priority.Value = 100;
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                1.0f
            );
            cinemachineBrain.enabled = true;
            yield return new WaitForSeconds(1.0f);
        }

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
            dialogueFont: null
        ));

        yield return StartCoroutine(ChildEpisodeRoutine());
    }

    private IEnumerator ChildEpisodeRoutine()
    {
        currentPhase = StoryPhase.ChildEpisode;

        Transform shakeTarget = behindDoorVcam != null ? behindDoorVcam.transform : playerCamera;
        Coroutine shakeRoutine = StartCoroutine(CameraShakeRoutine(shakeTarget, 5.0f, 0.35f));

        string[] episodeLines = new string[] {
            GetLoc("episode_01"),
            GetLoc("episode_02"),
            GetLoc("episode_03"),
            GetLoc("episode_04")
        };

        yield return StartCoroutine(SafeShowDialogue(
            episodeLines,
            hasChoice: false,
            dialogueColor: new Color(1f, 0.4f, 0.4f),
            dialogueFont: null
        ));

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);

        yield return StartCoroutine(FinalTeddyMomentRoutine());
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

        if (behindDoorVcam != null)
            behindDoorVcam.Priority.Value = 0;

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

        if (screenFader != null)
        {
            bool fadeDone = false;
            screenFader.FadeToBlack(2.0f, () => fadeDone = true);
            while (!fadeDone) yield return null;
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        currentPhase = StoryPhase.EndOfPrototype;
        if (endPrototypePanel != null)
        {
            endPrototypePanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Debug.Log("[MetroStory] Sequence complete. END OF PROTOTYPE displayed.");
    }

    public void RestartSequence()
    {
        Debug.Log("[MetroStory] Restarting sequence / reloading scene.");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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