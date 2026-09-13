using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Generic full-screen black fader using a CanvasGroup, decoupled from the
/// dialogue system. Use for teleports, scene handoffs, or any "cut to black
/// and back" moment that isn't tied to a dialogue sequence starting.
///
/// Attach to (or assign) the FadeOverlay GameObject that has a black Image
/// + CanvasGroup on it.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    /// <summary>
    /// Global flag to signal that the next loaded scene should start at 100% black
    /// and smoothly fade in.
    /// </summary>
    public static bool StartFromBlack = false;

    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private bool fadeAudio = true;
    [SerializeField] private bool autoFadeInOnStart = true;
    [SerializeField] private float defaultFadeDuration = 2.0f;

    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (overlay == null)
            overlay = GetComponent<CanvasGroup>();

        if (StartFromBlack)
        {
            if (overlay != null)
            {
                overlay.alpha = 1f;
                overlay.blocksRaycasts = true;
            }
            if (fadeAudio) AudioListener.volume = 0f;
        }
        else
        {
            if (overlay != null)
            {
                overlay.alpha = 0f;
                overlay.blocksRaycasts = false;
            }
            if (fadeAudio) AudioListener.volume = 1f;
        }
    }

    private void Start()
    {
        if (StartFromBlack)
        {
            StartFromBlack = false;
            FadeFromBlack(defaultFadeDuration);
        }
        else if (autoFadeInOnStart && overlay != null && overlay.alpha > 0.01f)
        {
            FadeFromBlack(defaultFadeDuration);
        }
    }

    /// <summary>
    /// Smoothly fades out to black, then loads the target scene and sets it to fade in.
    /// </summary>
    public static void TransitionToScene(string sceneName, float duration = 2.0f)
    {
        StartFromBlack = true;
        if (Instance != null)
        {
            Instance.FadeToBlack(duration, () => {
                SceneManager.LoadScene(sceneName);
            });
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public void FadeToBlack(float duration, Action onComplete = null)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        float currentAlpha = overlay != null ? overlay.alpha : 0f;
        _fadeCoroutine = StartCoroutine(Fade(currentAlpha, 1f, duration, onComplete, true));
    }

    public void FadeFromBlack(float duration, Action onComplete = null)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        float currentAlpha = overlay != null ? overlay.alpha : 1f;
        _fadeCoroutine = StartCoroutine(Fade(currentAlpha, 0f, duration, onComplete, false));
    }

    private IEnumerator Fade(float from, float to, float duration, Action onComplete, bool fadingToBlack)
    {
        float elapsed = 0f;
        if (overlay != null)
        {
            overlay.alpha = from;
            overlay.blocksRaycasts = true;
        }

        float startAudio = AudioListener.volume;
        float targetAudio = fadingToBlack ? 0f : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (overlay != null)
                overlay.alpha = Mathf.Lerp(from, to, t);

            if (fadeAudio)
                AudioListener.volume = Mathf.Lerp(startAudio, targetAudio, t);

            yield return null;
        }

        if (overlay != null)
        {
            overlay.alpha = to;
            overlay.blocksRaycasts = (to > 0.01f);
        }
        if (fadeAudio) AudioListener.volume = targetAudio;

        _fadeCoroutine = null;
        onComplete?.Invoke();
    }
}