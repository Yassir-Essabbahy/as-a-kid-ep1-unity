using UnityEngine;
using System.Collections;

public class HouseWakingCinematic : MonoBehaviour
{
    [Header("Player & Camera References")]
    public GameObject player;
    public Transform cameraTransform;
    public Canvas mobileControlsCanvas;

    [Header("Alarm Clock & Audio")]
    public AudioSource alarmAudioSource;
    public AudioClip alarmClip;
    public AudioClip clockOffSound;

    [Header("Cinematic Staging (Kid's Bedroom)")]
    public Vector3 pillowEyePos = new Vector3(104.70f, 7.05f, 119.10f);
    public Vector3 pillowEyeEuler = new Vector3(-25f, 45f, 12f);

    public Vector3 sitEyePos = new Vector3(104.60f, 7.55f, 119.45f);
    public Vector3 sitEyeEuler = new Vector3(0f, 35f, 0f);

    public Vector3 standFloorPos = new Vector3(104.20f, 7.20f, 120.20f);
    public Vector3 standFloorEuler = new Vector3(0f, 220f, 0f);

    [Header("Timings")]
    public float ringBeforeWakeDuration = 1.0f;
    public float sitUpDuration = 1.8f;
    public float lookAlarmDuration = 0.8f;
    public float standUpDuration = 1.6f;

    [Header("Canvas Fader")]
    public CanvasGroup eyeBlinkOverlay;

    private FirstPersonController fpc;

    private void Start()
    {
        FindReferences();
        StartCoroutine(WakingSequenceRoutine());
    }

    private void FindReferences()
    {
        if (player == null)
            player = GameObject.Find("FirstPersonController");

        if (player != null)
        {
            fpc = player.GetComponent<FirstPersonController>();
            if (cameraTransform == null)
            {
                var cam = player.GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }

            if (mobileControlsCanvas == null)
            {
                var m = player.transform.Find("MobileControlsCanvas");
                if (m != null) mobileControlsCanvas = m.GetComponent<Canvas>();
            }
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (alarmAudioSource == null)
        {
            var clock = GameObject.Find("AlarmClock");
            if (clock != null) alarmAudioSource = clock.GetComponent<AudioSource>();
        }

        if (eyeBlinkOverlay == null)
        {
            var fader = FindAnyObjectByType<ScreenFader>();
            if (fader != null)
            {
                eyeBlinkOverlay = fader.GetComponent<CanvasGroup>();
            }
            if (eyeBlinkOverlay == null)
            {
                CreateFallbackOverlay();
            }
        }
    }

    private void CreateFallbackOverlay()
    {
        var canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null) return;

        var blinkGo = new GameObject("EyeBlinkOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
        blinkGo.transform.SetParent(canvasGo.transform, false);

        var rt = blinkGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var img = blinkGo.GetComponent<UnityEngine.UI.Image>();
        img.color = Color.black;

        eyeBlinkOverlay = blinkGo.GetComponent<CanvasGroup>();
        eyeBlinkOverlay.alpha = 1f;
        eyeBlinkOverlay.blocksRaycasts = true;
    }

    private IEnumerator WakingSequenceRoutine()
    {
        // 1. Lock controls and hide mobile HUD
        if (fpc != null)
        {
            fpc.playerCanMove = false;
            fpc.cameraCanMove = false;
            fpc.enabled = false;
        }

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(false);

        if (player != null)
        {
            player.transform.position = standFloorPos;
            player.transform.eulerAngles = standFloorEuler;
        }

        // Snap camera to pillow position (lying in bed)
        if (cameraTransform != null)
        {
            cameraTransform.position = pillowEyePos;
            cameraTransform.rotation = Quaternion.Euler(pillowEyeEuler);
        }

        if (eyeBlinkOverlay != null)
        {
            eyeBlinkOverlay.alpha = 1f;
        }

        yield return new WaitForSeconds(0.2f);

        // 2. Alarm clock starts ringing!
        if (alarmAudioSource != null)
        {
            if (alarmClip != null) alarmAudioSource.clip = alarmClip;
            alarmAudioSource.loop = true;
            alarmAudioSource.Play();
        }

        yield return new WaitForSeconds(ringBeforeWakeDuration);

        // 3. Eyelids blink groggily open
        yield return StartCoroutine(EyelidBlinkRoutine());

        // 4. Sitting up from pillow
        float elapsed = 0f;
        Vector3 startPos = cameraTransform != null ? cameraTransform.position : pillowEyePos;
        Quaternion startRot = cameraTransform != null ? cameraTransform.rotation : Quaternion.Euler(pillowEyeEuler);
        Quaternion sitRot = Quaternion.Euler(sitEyeEuler);

        while (elapsed < sitUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / sitUpDuration);
            if (cameraTransform != null)
            {
                cameraTransform.position = Vector3.Lerp(startPos, sitEyePos, t);
                cameraTransform.rotation = Quaternion.Slerp(startRot, sitRot, t);
            }
            yield return null;
        }

        // 5. Look toward bedside table & stop the alarm
        yield return new WaitForSeconds(lookAlarmDuration);

        if (alarmAudioSource != null && alarmAudioSource.isPlaying)
        {
            alarmAudioSource.Stop();
            if (clockOffSound != null)
            {
                alarmAudioSource.PlayOneShot(clockOffSound);
            }
        }

        yield return new WaitForSeconds(0.4f);

        // 6. Stand up from bed onto the bedroom floor
        elapsed = 0f;
        startPos = cameraTransform != null ? cameraTransform.position : sitEyePos;
        startRot = cameraTransform != null ? cameraTransform.rotation : sitRot;
        Vector3 standCamPos = standFloorPos + Vector3.up * 0.85f;
        Quaternion standRot = Quaternion.Euler(standFloorEuler);

        while (elapsed < standUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / standUpDuration);
            if (cameraTransform != null)
            {
                cameraTransform.position = Vector3.Lerp(startPos, standCamPos, t);
                cameraTransform.rotation = Quaternion.Slerp(startRot, standRot, t);
            }
            yield return null;
        }

        // 7. Complete cutscene & enable player controls
        if (player != null)
        {
            player.transform.position = standFloorPos;
            player.transform.eulerAngles = standFloorEuler;
        }

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = new Vector3(0f, 0.85f, 0f);
            cameraTransform.localRotation = Quaternion.identity;
        }

        if (fpc != null)
        {
            fpc.enabled = true;
            fpc.playerCanMove = true;
            fpc.cameraCanMove = true;
        }

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.gameObject.SetActive(true);

        // 8. Activate objectives
        if (HouseObjectiveController.Instance != null)
        {
            HouseObjectiveController.Instance.phase = 0;
            HouseObjectiveController.Instance.UpdateObjectiveUI();
        }
    }

    private IEnumerator EyelidBlinkRoutine()
    {
        if (eyeBlinkOverlay == null) yield break;

        // Blink 1: partial open
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            eyeBlinkOverlay.alpha = Mathf.Lerp(1f, 0.35f, t / 0.6f);
            yield return null;
        }

        // Blink 1 close
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            eyeBlinkOverlay.alpha = Mathf.Lerp(0.35f, 0.85f, t / 0.3f);
            yield return null;
        }

        // Blink 2: fully open into room
        t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            eyeBlinkOverlay.alpha = Mathf.Lerp(0.85f, 0f, t / 0.7f);
            yield return null;
        }

        eyeBlinkOverlay.alpha = 0f;
        eyeBlinkOverlay.blocksRaycasts = false;
    }
}
