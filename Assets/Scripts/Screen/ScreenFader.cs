using System;
using System.Collections;
using UnityEngine;

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

    [SerializeField] private CanvasGroup overlay;

    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        overlay.alpha = 0f;
    }

    public void FadeToBlack(float duration, Action onComplete = null)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(Fade(0f, 1f, duration, onComplete));
    }

    public void FadeFromBlack(float duration, Action onComplete = null)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(Fade(1f, 0f, duration, onComplete));
    }

    private IEnumerator Fade(float from, float to, float duration, Action onComplete)
    {
        float elapsed = 0f;
        overlay.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            overlay.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        overlay.alpha = to;
        _fadeCoroutine = null;
        onComplete?.Invoke();
    }
}