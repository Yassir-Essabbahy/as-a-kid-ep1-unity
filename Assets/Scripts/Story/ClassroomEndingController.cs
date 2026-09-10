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
    public ClassroomClock classroomClock;
    public float hoursToAdvance = 2.0f;
    public float clockAdvanceDuration = 2.5f;

    [Header("Ending UI")]
    public GameObject endOfEpisodePanel;
    public Button restartButton;
    public ScreenFader screenFader;

    [Header("Timing")]
    public float initialClockFocusTime = 1.0f;
    public float postClockPause = 0.5f;

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
        if (classroomClock == null)
            classroomClock = FindAnyObjectByType<ClassroomClock>();

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

        // 2. Start focused on Clock
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

        // Fade in
        if (screenFader != null)
        {
            screenFader.FadeFromBlack(1.0f);
        }

        yield return new WaitForSeconds(initialClockFocusTime);

        // 3. Advance Clock
        bool clockDone = false;
        if (classroomClock != null)
        {
            classroomClock.AdvanceTime(hoursToAdvance, clockAdvanceDuration, () => clockDone = true);
            while (!clockDone) yield return null;
        }
        else
        {
            yield return new WaitForSeconds(clockAdvanceDuration);
        }

        yield return new WaitForSeconds(postClockPause);

        // 4. Cut or blend to Teacher
        if (cinemachineBrain != null)
        {
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.2f);
        }
        if (teacherVCam != null)
        {
            teacherVCam.Priority.Value = 150;
        }

        yield return new WaitForSeconds(1.2f);

        // 5. Final Teacher Dialogue
        string teacherName = MetroStorySequenceController.GetLoc("teacher_ending_01");
        string teacherQuestion = MetroStorySequenceController.GetLoc("teacher_ending_02");

        var diag = NpcDialogueManager.Instance ?? FindAnyObjectByType<NpcDialogueManager>();
        if (diag != null && Application.isPlaying)
        {
            yield return StartCoroutine(diag.ShowDialogue(
                new string[] { teacherName },
                hasChoice: false,
                dialogueColor: Color.white,
                dialogueFont: null
            ));

            yield return new WaitForSeconds(1.5f);

            yield return StartCoroutine(diag.ShowDialogue(
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

        yield return new WaitForSeconds(1.5f);

        // 6. Fade to Black
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

        // 7. Display END OF EPISODE
        if (endOfEpisodePanel != null)
        {
            endOfEpisodePanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Debug.Log("[ClassroomEnding] Ending complete. END OF EPISODE screen displayed.");
    }

    public void RestartGame()
    {
        Debug.Log("[ClassroomEnding] Restart clicked. Resetting state and returning to Gameplay3.");
        IsEndingActive = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("Gameplay3");
    }

    [ContextMenu("Debug: Force Trigger Ending")]
    public void DebugForceEnding()
    {
        IsEndingActive = true;
        StartCoroutine(PlayEndingSequence());
    }
}
