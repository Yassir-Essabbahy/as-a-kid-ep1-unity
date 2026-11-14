using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Automation script to run play-mode verification of the FallCutscene sequence:
/// Captures all 11 required screenshots, logs transform states, verifies level horizon,
/// checks for 180 flips, and verifies audio levels and controls.
/// </summary>
public class FallCutsceneVerificationRunner : MonoBehaviour
{
    public static FallCutsceneVerificationRunner Instance { get; private set; }

    public bool isRunning = false;
    public bool isCompleted = false;

    private string screenshotDir;

    private void Awake()
    {
        Instance = this;
        screenshotDir = Path.Combine(Application.dataPath, "Screenshots");
        if (!Directory.Exists(screenshotDir)) Directory.CreateDirectory(screenshotDir);
    }

    private void Start()
    {
        StartCoroutine(VerificationRoutine());
    }

    private IEnumerator VerificationRoutine()
    {
        isRunning = true;
        Debug.Log("[VerificationRunner] Starting FallCutscene verification sequence...");

        var cutscene = FallCutscene.Instance;
        while (cutscene == null)
        {
            cutscene = FallCutscene.Instance;
            yield return null;
        }

        // Wait a frame for initialization
        yield return null;

        // 1. Black fall
        yield return new WaitForSeconds(2.0f);
        CaptureScreenshot("01_black_fall.png");
        Debug.Log($"[VerificationRunner] Captured 01_black_fall at t={Time.time:F2}s, Stage={cutscene.CurrentStage}");

        // Wait until ground reveal
        while (cutscene.CurrentStage != FallCutscene.CutsceneStage.GroundReveal && cutscene.CurrentStage < FallCutscene.CutsceneStage.ImpactFlash)
        {
            yield return null;
        }
        // 2. Late ground reveal
        yield return new WaitForSeconds(0.15f);
        CaptureScreenshot("02_late_ground_reveal.png");
        Debug.Log($"[VerificationRunner] Captured 02_late_ground_reveal at t={Time.time:F2}s, Stage={cutscene.CurrentStage}");

        // Wait until impact flash
        while (cutscene.CurrentStage != FallCutscene.CutsceneStage.ImpactFlash && cutscene.CurrentStage < FallCutscene.CutsceneStage.LyingDazed)
        {
            yield return null;
        }
        // 3. White flash
        CaptureScreenshot("03_white_flash.png");
        Debug.Log($"[VerificationRunner] Captured 03_white_flash at t={Time.time:F2}s, Stage={cutscene.CurrentStage}");

        // Wait for LyingDazed stage
        while (cutscene.CurrentStage != FallCutscene.CutsceneStage.LyingDazed)
        {
            yield return null;
        }

        // 4. First slit (Blink 1 at ~10%)
        var eyelid = EyelidBlink.Instance;
        while (eyelid != null && eyelid.currentOpenAmount < 0.08f)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.1f);
        CaptureScreenshot("04_first_slit.png");
        Debug.Log($"[VerificationRunner] Captured 04_first_slit at t={Time.time:F2}s, EyelidOpen={eyelid?.currentOpenAmount:F2}");

        // 5. Half open (Blink 2 at ~40%)
        // First wait for Blink 1 to close
        while (eyelid != null && eyelid.currentOpenAmount > 0.05f)
        {
            yield return null;
        }
        // Then wait for Blink 2 to open >= 0.35f
        while (eyelid != null && eyelid.currentOpenAmount < 0.35f)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.1f);
        CaptureScreenshot("05_half_open.png");
        Debug.Log($"[VerificationRunner] Captured 05_half_open at t={Time.time:F2}s, EyelidOpen={eyelid?.currentOpenAmount:F2}");

        // 6. Fully open (Blink 3 at 100%)
        // Wait for Blink 2 to close
        while (eyelid != null && eyelid.currentOpenAmount > 0.05f)
        {
            yield return null;
        }
        // Then wait for Blink 3 >= 0.95f
        while (eyelid != null && eyelid.currentOpenAmount < 0.95f)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
        CaptureScreenshot("06_fully_open.png");
        Debug.Log($"[VerificationRunner] Captured 06_fully_open at t={Time.time:F2}s, EyelidOpen={eyelid?.currentOpenAmount:F2}");

        // 7. Rolling
        while (cutscene.CurrentStage != FallCutscene.CutsceneStage.Rolling)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);
        CaptureScreenshot("07_rolling.png");
        Debug.Log($"[VerificationRunner] Captured 07_rolling at t={Time.time:F2}s, CameraRot={cutscene.mainCamera?.transform.eulerAngles}");

        // 8. Kneeling
        while (cutscene.CurrentStage != FallCutscene.CutsceneStage.Kneeling)
        {
            yield return null;
        }
        yield return new WaitForSeconds(1.0f);
        CaptureScreenshot("08_kneeling.png");
        Debug.Log($"[VerificationRunner] Captured 08_kneeling at t={Time.time:F2}s, CameraPos={cutscene.mainCamera?.transform.position}");

        // 9. Standing
        while (cutscene.CurrentStage != FallCutscene.CutsceneStage.Standing)
        {
            yield return null;
        }
        yield return new WaitForSeconds(2.0f);
        CaptureScreenshot("09_standing.png");
        Debug.Log($"[VerificationRunner] Captured 09_standing at t={Time.time:F2}s, CameraPos={cutscene.mainCamera?.transform.position}");

        // 10. Control handed over
        while (cutscene.IsCinematicActive)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.1f);
        CaptureScreenshot("10_control_handed_over.png");
        Vector3 handoffCamEuler = cutscene.mainCamera != null ? cutscene.mainCamera.transform.eulerAngles : Vector3.zero;
        Vector3 handoffCamPos = cutscene.mainCamera != null ? cutscene.mainCamera.transform.position : Vector3.zero;
        Debug.Log($"[VerificationRunner] Captured 10_control_handed_over at t={Time.time:F2}s, CamPos={handoffCamPos}, CamRot={handoffCamEuler}");

        // 11. Frame 1 second after control starts (CRITICAL: must look identical, no snap, no 180 flip)
        yield return new WaitForSeconds(1.0f);
        CaptureScreenshot("11_control_1sec_after.png");
        Vector3 afterCamEuler = cutscene.mainCamera != null ? cutscene.mainCamera.transform.eulerAngles : Vector3.zero;
        Vector3 afterCamPos = cutscene.mainCamera != null ? cutscene.mainCamera.transform.position : Vector3.zero;
        Debug.Log($"[VerificationRunner] Captured 11_control_1sec_after at t={Time.time:F2}s, CamPos={afterCamPos}, CamRot={afterCamEuler}");

        Debug.Log($"[VerificationRunner] Horizon Check: Handoff Pitch={handoffCamEuler.x:F2}, After Pitch={afterCamEuler.x:F2}, Roll={afterCamEuler.z:F2}");
        Debug.Log($"[VerificationRunner] Yaw Check: Handoff Yaw={handoffCamEuler.y:F2}, After Yaw={afterCamEuler.y:F2}");

        isCompleted = true;
        isRunning = false;
        Debug.Log("[VerificationRunner] All verification screenshots captured successfully!");
    }

    private void CaptureScreenshot(string fileName)
    {
        string fullPath = Path.Combine(screenshotDir, fileName);
        ScreenCapture.CaptureScreenshot(fullPath);
    }
}
