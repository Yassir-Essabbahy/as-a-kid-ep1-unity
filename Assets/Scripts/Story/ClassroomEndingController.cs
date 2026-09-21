using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClassroomEndingController : MonoBehaviour
{
    public static bool IsEndingActive = false;

    [Header("Cameras")]
    public CinemachineCamera clockVCam;
    public CinemachineCamera teacherVCam;
    public CinemachineBrain cinemachineBrain;

    [Header("Clock")]
    public Transform wallClock;
    public ClassroomClock classroomClock;
    public float initialClockFocusTime = 2.5f;

    [Header("Teacher & IK")]
    public NpcLookAt teacherLookAt;

    [Header("Cinematic & Dialogue")]
    public DialogueManager dialogueManager;

    [Header("Ending UI")]
    public GameObject endOfEpisodePanel;
    public TextMeshProUGUI titleText;
    public TMP_FontAsset titleCardFont;
    public string titleCardString = "To be continued...";
    public Button restartButton;
    public ScreenFader screenFader;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (!IsEndingActive)
        {
            Debug.Log("[ClassroomEnding] Not in ending mode. Normal classroom intro will proceed.");
            return;
        }

        Debug.Log("[ClassroomEnding] Ending mode ACTIVE! Taking over classroom sequence.");
        StartCoroutine(PlayEndingSequence());
    }

    public void ResolveReferences()
    {
        if (wallClock == null)
        {
            var wc = GameObject.Find("WallClock");
            if (wc != null) wallClock = wc.transform;
        }

        if (teacherLookAt == null)
        {
            var teacher = GameObject.Find("Teacher");
            if (teacher != null)
            {
                teacherLookAt = teacher.GetComponent<NpcLookAt>();
                if (teacherLookAt == null)
                    teacherLookAt = teacher.AddComponent<NpcLookAt>();
            }
        }

        if (teacherLookAt != null)
        {
            if (teacherLookAt.animator == null)
            {
                var teacher = GameObject.Find("Teacher");
                if (teacher != null) teacherLookAt.animator = teacher.GetComponent<Animator>();
            }
            if (teacherLookAt.LookAtObj == null && teacherVCam != null)
            {
                teacherLookAt.LookAtObj = teacherVCam.transform;
            }
        }

        if (dialogueManager == null)
            dialogueManager = DialogueManager.Instance ?? FindAnyObjectByType<DialogueManager>();

        if (clockVCam == null)
        {
            var cv = GameObject.Find("ClockVCam");
            if (cv != null) clockVCam = cv.GetComponent<CinemachineCamera>();
        }

        if (teacherVCam == null)
        {
            var tv = GameObject.Find("TeacherVCam");
            if (tv != null) teacherVCam = tv.GetComponent<CinemachineCamera>();
        }

        if (screenFader == null)
            screenFader = FindAnyObjectByType<ScreenFader>();

        if (cinemachineBrain == null)
        {
            var cam = Camera.main;
            if (cam != null) cinemachineBrain = cam.GetComponent<CinemachineBrain>();
        }

        if (endOfEpisodePanel == null)
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                var panel = canvas.transform.Find("EndOfEpisodePanel");
                if (panel != null) endOfEpisodePanel = panel.gameObject;
            }
        }

        if (restartButton == null && endOfEpisodePanel != null)
        {
            restartButton = endOfEpisodePanel.GetComponentInChildren<Button>();
        }

        if (titleText == null && endOfEpisodePanel != null)
        {
            var t = endOfEpisodePanel.transform.Find("TitleText");
            if (t != null) titleText = t.GetComponent<TextMeshProUGUI>();
            if (titleText == null) titleText = endOfEpisodePanel.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (titleCardFont == null)
        {
            titleCardFont = Resources.Load<TMP_FontAsset>("Fonts/GeistPixel-Regular-VariableFont_ELSH SDF");
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    public IEnumerator PlayEndingSequence()
    {
        ResolveReferences();

        // 1. Suppress ClassroomGazeScene
        var gazeScene = FindAnyObjectByType<ClassroomGazeScene>();
        if (gazeScene != null)
        {
            gazeScene.enabled = false;
        }

        // Disable player controller
        var fps = FindAnyObjectByType<FirstPersonController>();
        if (fps != null) fps.SetControlLocked(true);

        // 2. Start focused on WallClock
        if (cinemachineBrain != null)
        {
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            cinemachineBrain.enabled = true;
        }

        if (teacherVCam != null) teacherVCam.Priority.Value = 10;
        if (clockVCam != null)
        {
            clockVCam.Priority.Value = 100;
        }

        // Initialize Teacher IK targeting teacherVCam/camera
        if (teacherLookAt != null)
        {
            teacherLookAt.IKActive = false;
            teacherLookAt.LookAtObj = (teacherVCam != null) ? teacherVCam.transform : Camera.main?.transform;
        }

        // 3. Cinematic Bands immediately slide in / appear for widescreen letterboxing
        var s1Diag = dialogueManager ?? DialogueManager.Instance ?? FindAnyObjectByType<DialogueManager>();
        if (s1Diag != null && Application.isPlaying)
        {
            if (s1Diag.dialogueBox != null)
            {
                s1Diag.dialogueBox.SetActive(true);
                s1Diag.ResetBandsOffscreen();
                if (s1Diag.speakerText != null) s1Diag.speakerText.text = "";
                if (s1Diag.bodyText != null) s1Diag.bodyText.text = "";
                if (s1Diag.continueButton != null) s1Diag.continueButton.gameObject.SetActive(false);
                StartCoroutine(s1Diag.SlideBandsRoutine(true));
            }
        }

        // Fade in from black
        if (screenFader != null)
        {
            screenFader.FadeFromBlack(1.0f);
        }

        // 4. Cinematic Shot on WallClock (hold shot for ~2.5s)
        yield return new WaitForSeconds(initialClockFocusTime);

        // 5. Smoothly blend down and across room to Teacher
        if (cinemachineBrain != null)
        {
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.2f);
        }
        if (teacherVCam != null)
        {
            teacherVCam.Priority.Value = 150;
        }

        // Teacher locks eyes/gaze onto the player
        if (teacherLookAt != null)
        {
            teacherLookAt.IKActive = true;
            teacherLookAt.LookAtObj = (teacherVCam != null) ? teacherVCam.transform : Camera.main?.transform;
        }

        yield return new WaitForSeconds(1.2f);

        // 6. Teacher Dialogue in bottom cinematic band
        string teacherName = MetroStorySequenceController.GetLoc("teacher_ending_01");
        string teacherQuestion = MetroStorySequenceController.GetLoc("teacher_ending_02");

        var npcDiag = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();

        if (s1Diag != null && Application.isPlaying && s1Diag.dialogueBox != null)
        {
            // Line 1: Teacher: {CHILD_NAME}?
            if (s1Diag.speakerText != null) s1Diag.speakerText.text = "Teacher";
            if (s1Diag.bodyText != null)
            {
                string line1 = teacherName;
                if (line1.StartsWith("Teacher: ")) line1 = line1.Substring(9);
                s1Diag.bodyText.text = line1;
            }

            yield return new WaitForSeconds(2.8f);

            // Line 2: Teacher: ... Are you with us?
            if (s1Diag.speakerText != null) s1Diag.speakerText.text = "Teacher";
            if (s1Diag.bodyText != null)
            {
                string line2 = teacherQuestion;
                if (line2.StartsWith("Teacher: ")) line2 = line2.Substring(9);
                s1Diag.bodyText.text = line2;
            }

            yield return new WaitForSeconds(3.2f);
        }
        else if (npcDiag != null && Application.isPlaying)
        {
            yield return StartCoroutine(npcDiag.ShowDialogue(
                new string[] { teacherName },
                hasChoice: false,
                dialogueColor: Color.white,
                dialogueFont: null
            ));

            yield return new WaitForSeconds(1.5f);

            yield return StartCoroutine(npcDiag.ShowDialogue(
                new string[] { teacherQuestion },
                hasChoice: false,
                dialogueColor: Color.white,
                dialogueFont: null
            ));
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }

        yield return new WaitForSeconds(0.8f);

        // 7. Fade to Black (cinematic bands stay present during fade)
        if (screenFader != null)
        {
            bool fadeDone = false;
            screenFader.FadeToBlack(2.0f, () => fadeDone = true);
            while (!fadeDone) yield return null;
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }

        // Hide dialogue box once screen is black
        if (s1Diag != null && s1Diag.dialogueBox != null)
        {
            s1Diag.dialogueBox.SetActive(false);
        }

        // 8. Display retro title card: "To be continued..."
        if (endOfEpisodePanel != null)
        {
            if (titleText != null)
            {
                titleText.text = titleCardString;
                if (titleCardFont != null) titleText.font = titleCardFont;
            }

            // Remove/hide restart button completely
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(false);
            }
            var allButtons = endOfEpisodePanel.GetComponentsInChildren<Button>(true);
            foreach (var b in allButtons)
            {
                b.gameObject.SetActive(false);
            }

            // Hide subtitle to keep title card clean and minimal
            var sub = endOfEpisodePanel.transform.Find("SubtitleText");
            if (sub != null) sub.gameObject.SetActive(false);

            endOfEpisodePanel.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Debug.Log("[ClassroomEnding] Ending complete. Retro title card displayed.");
    }

    public void RestartGame()
    {
        Debug.Log("[ClassroomEnding] Restart clicked - transitioning to MoroccanBeach.");
        IsEndingActive = false;
        Time.timeScale = 1f;
        ScreenFader.TransitionToScene("MoroccanBeach", 2.0f);
    }

    [ContextMenu("Debug: Force Trigger Ending")]
    public void DebugForceEnding()
    {
        IsEndingActive = true;
        StartCoroutine(PlayEndingSequence());
    }
}
