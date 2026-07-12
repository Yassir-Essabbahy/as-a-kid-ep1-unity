using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen black overlay that fades out when a dialogue sequence starts.
/// Attach to the FadeOverlay Image child of the dialogue Canvas.
/// </summary>
[RequireComponent(typeof(Image))]
public class DialogueFadeScreen : MonoBehaviour
{
    /// <summary>Duration of the fade-out in seconds.</summary>
    [SerializeField] private float fadeDuration = 1f;

    private Image _overlay;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _overlay = GetComponent<Image>();
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        DialogueEvents.OnSequenceStart += HandleSequenceStart;
    }

    private void OnDisable()
    {
        DialogueEvents.OnSequenceStart -= HandleSequenceStart;
    }

    private void HandleSequenceStart(string _)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        SetAlpha(1f);
        _fadeCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        SetAlpha(0f);
        _fadeCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        Color c = _overlay.color;
        c.a = alpha;
        _overlay.color = c;
    }
}
