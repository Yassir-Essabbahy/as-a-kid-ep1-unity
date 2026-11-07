using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

/// <summary>
/// Reusable organic eyelid blink and drowsy eye animation system.
/// Controls upper and lower feathered eyelid panels with customizable easing curves,
/// full screen scaling (16:9, 20:9, 21:9) without safe-area clipping,
/// and optional post-processing heavy-eyes wash.
/// </summary>
[DisallowMultipleComponent]
public class EyelidBlink : MonoBehaviour
{
    private static EyelidBlink _instance; public static EyelidBlink Instance { get { if (_instance == null) _instance = Object.FindAnyObjectByType<EyelidBlink>(); return _instance; } private set => _instance = value; }

    [Header("Eyelid UI RectTransforms")]
    public RectTransform upperLid;
    public RectTransform lowerLid;
    public Image upperLidImage;
    public Image lowerLidImage;

    [Header("Sprites")]
    public Sprite upperLidSprite;
    public Sprite lowerLidSprite;

    [Header("Canvas & Sorting")]
    public Canvas overlayCanvas;
    public CanvasGroup canvasGroup;

    [Header("Easing Curves")]
    public AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Heavy Eyes Post Processing (Optional)")]
    public Volume heavyEyesVolume;
    [Tooltip("If true, post processing effects are scaled down or skipped on low-end mobile.")]
    public bool mobileSafePostProcessing = true;

    [Header("Current State")]
    [Range(0f, 1f)] public float currentOpenAmount = 0f; // 0 = closed, 1 = fully open
    public bool IsAnimating => activeCoroutine != null;

    private Coroutine activeCoroutine;
    private float screenHeight = 1080f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureUIBuilt();
        SetOpenImmediate(currentOpenAmount);
    }

    /// <summary>
    /// Builds or binds the top-sorted screen-space overlay canvas and eyelid panels.
    /// </summary>
    public void EnsureUIBuilt()
    {
        if (overlayCanvas == null)
        {
            var canvasGo = GameObject.Find("EyelidCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("EyelidCanvas");
                canvasGo.transform.SetParent(null);
            }

            overlayCanvas = canvasGo.GetComponent<Canvas>();
            if (overlayCanvas == null) overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasGo.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
        }

        if (upperLidSprite == null)
            upperLidSprite = Resources.Load<Sprite>("Textures/EyelidUpper");
#if UNITY_EDITOR
        if (upperLidSprite == null)
            upperLidSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/House/Textures/EyelidUpper.png");
#endif

        if (lowerLidSprite == null)
            lowerLidSprite = Resources.Load<Sprite>("Textures/EyelidLower");
#if UNITY_EDITOR
        if (lowerLidSprite == null)
            lowerLidSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/House/Textures/EyelidLower.png");
#endif

        // Build Upper Eyelid panel if needed (solid black full-screen bar, no blur sprite)
        if (upperLid == null && overlayCanvas != null)
        {
            var upperGo = new GameObject("UpperEyelid", typeof(RectTransform), typeof(Image));
            upperGo.transform.SetParent(overlayCanvas.transform, false);
            upperLid = upperGo.GetComponent<RectTransform>();
            upperLidImage = upperGo.GetComponent<Image>();
            upperLidImage.sprite = null;
            upperLidImage.color = Color.black;
            upperLidImage.raycastTarget = false;
        }

        // Build Lower Eyelid panel if needed (solid black full-screen bar, no blur sprite)
        if (lowerLid == null && overlayCanvas != null)
        {
            var lowerGo = new GameObject("LowerEyelid", typeof(RectTransform), typeof(Image));
            lowerGo.transform.SetParent(overlayCanvas.transform, false);
            lowerLid = lowerGo.GetComponent<RectTransform>();
            lowerLidImage = lowerGo.GetComponent<Image>();
            lowerLidImage.sprite = null;
            lowerLidImage.color = Color.black;
            lowerLidImage.raycastTarget = false;
        }

        ConfigureEyelidAnchors();
    }

    private void ConfigureEyelidAnchors()
    {
        // Reference height extra padding so wide aspect ratios (20:9, 21:9) never reveal edges
        screenHeight = 1080f;
        float halfHeight = screenHeight * 0.5f + 300f;

        if (upperLid != null)
        {
            upperLid.anchorMin = new Vector2(0f, 0.5f);
            upperLid.anchorMax = new Vector2(1f, 1f);
            upperLid.pivot = new Vector2(0.5f, 0f); // pivot at center line (bottom of upper half)
            upperLid.sizeDelta = new Vector2(0f, halfHeight);
            if (upperLidImage != null)
            {
                upperLidImage.sprite = null;
                upperLidImage.color = Color.black;
            }
        }

        if (lowerLid != null)
        {
            lowerLid.anchorMin = new Vector2(0f, 0f);
            lowerLid.anchorMax = new Vector2(1f, 0.5f);
            lowerLid.pivot = new Vector2(0.5f, 1f); // pivot at center line (top of lower half)
            lowerLid.sizeDelta = new Vector2(0f, halfHeight);
            if (lowerLidImage != null)
            {
                lowerLidImage.sprite = null;
                lowerLidImage.color = Color.black;
            }
        }
    }

    /// <summary>
    /// Instantly sets the eyelid aperture (0 = closed, 1 = fully open).
    /// </summary>
    public void SetOpenImmediate(float openAmount)
    {
        EnsureUIBuilt();
        currentOpenAmount = Mathf.Clamp01(openAmount);
        ApplyEyelidPositions(currentOpenAmount);
        ApplyHeavyEyesWeight(currentOpenAmount);
    }

    private void ApplyEyelidPositions(float openAmount)
    {
        float maxOffset = (screenHeight * 0.5f) + 120f;
        float offset = openAmount * maxOffset;

        if (upperLid != null)
        {
            upperLid.anchoredPosition = new Vector2(0f, offset);
        }

        if (lowerLid != null)
        {
            lowerLid.anchoredPosition = new Vector2(0f, -offset);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = openAmount >= 0.999f ? 0f : 1f;
        }
    }

    private void ApplyHeavyEyesWeight(float openAmount)
    {
        // Zero blur requested ("no blur blink")
        if (heavyEyesVolume != null)
        {
            heavyEyesVolume.weight = 0f;
        }
    }

    public Coroutine OpenSlow(float targetOpen, float duration)
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(AnimateOpenRoutine(targetOpen, duration, openCurve));
        return activeCoroutine;
    }

    public Coroutine Close(float duration)
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(AnimateOpenRoutine(0f, duration, closeCurve));
        return activeCoroutine;
    }

    public Coroutine Blink(float duration)
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(BlinkRoutine(duration));
        return activeCoroutine;
    }

    public Coroutine DipToBlack(float duration = 0.4f)
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(DipToBlackRoutine(duration));
        return activeCoroutine;
    }

    public IEnumerator WaitForAnimation()
    {
        while (activeCoroutine != null)
        {
            yield return null;
        }
    }

    private IEnumerator AnimateOpenRoutine(float targetOpen, float duration, AnimationCurve curve)
    {
        float start = currentOpenAmount;
        float elapsed = 0f;

        if (duration <= 0.001f)
        {
            SetOpenImmediate(targetOpen);
            activeCoroutine = null;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float curveT = curve != null ? curve.Evaluate(rawT) : rawT;
            float val = Mathf.Lerp(start, targetOpen, curveT);
            currentOpenAmount = val;
            ApplyEyelidPositions(val);
            ApplyHeavyEyesWeight(val);
            yield return null;
        }

        currentOpenAmount = targetOpen;
        ApplyEyelidPositions(targetOpen);
        ApplyHeavyEyesWeight(targetOpen);
        activeCoroutine = null;
    }

    private IEnumerator BlinkRoutine(float duration)
    {
        float half = duration * 0.5f;
        float initialOpen = currentOpenAmount;

        // Close quickly
        yield return StartCoroutine(AnimateOpenRoutine(0f, half, closeCurve));
        // Open back
        yield return StartCoroutine(AnimateOpenRoutine(initialOpen, half, openCurve));

        activeCoroutine = null;
    }

    private IEnumerator DipToBlackRoutine(float duration)
    {
        float half = duration * 0.5f;
        // Snap/close to black
        yield return StartCoroutine(AnimateOpenRoutine(0f, half, closeCurve));
        // Return open
        yield return StartCoroutine(AnimateOpenRoutine(1f, half, openCurve));
        activeCoroutine = null;
    }
}


