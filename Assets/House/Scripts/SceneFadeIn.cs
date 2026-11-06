using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SceneFadeIn : MonoBehaviour
{
    public CanvasGroup fadeOverlay;
    public float fadeDuration = 1f;

    void Start()
    {
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        fadeOverlay.alpha = 1f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / fadeDuration;
            fadeOverlay.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }
        fadeOverlay.alpha = 0f;
    }
} 