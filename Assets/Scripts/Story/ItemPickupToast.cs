using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Resident Evil / PSX style item pickup toast notification.
/// Styled with a pure black background and crisp border to match PromptBox and TalkPanel.
/// Slides in from the top-right, displays an icon in a dedicated framed slot and localized item count,
/// stays for a configurable duration (using unscaled time), and slides back out.
/// Supports queueing if multiple pickups occur simultaneously.
/// </summary>
[DisallowMultipleComponent]
public class ItemPickupToast : MonoBehaviour
{
    private static ItemPickupToast _instance;
    public static ItemPickupToast Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ItemPickupToast>();
                if (_instance == null)
                {
                    Canvas canvas = FindAnyObjectByType<Canvas>();
                    GameObject toastHost = new GameObject("ItemPickupToast");
                    if (canvas != null)
                    {
                        toastHost.transform.SetParent(canvas.transform, false);
                    }
                    _instance = toastHost.AddComponent<ItemPickupToast>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("UI References (Optional - Auto-Built if Null)")]
    public RectTransform panelRect;
    public Image iconImage;
    public TextMeshProUGUI labelText;
    public CanvasGroup canvasGroup;

    [Header("Audio SFX")]
    public AudioSource audioSource;
    public AudioClip toastSound;

    [Header("Animation Settings")]
    public float slideInDuration = 0.35f;
    public float displayDuration = 2.5f;
    public float slideOutDuration = 0.35f;

    [Header("Positions (Anchored Top-Right)")]
    [Tooltip("Anchored position off-screen to the right.")]
    public Vector2 offscreenPos = new Vector2(380f, -40f);
    [Tooltip("Anchored position on-screen in view.")]
    public Vector2 onscreenPos = new Vector2(-28f, -40f);

    private class ToastData
    {
        public Sprite icon;
        public string label;
        public int amount;
    }

    private readonly Queue<ToastData> _queue = new Queue<ToastData>();
    private Coroutine _activeRoutine;
    private AudioClip _proceduralChime;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (panelRect == null)
        {
            BuildDefaultToastUI();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D UI sound
            }
        }

        if (toastSound == null)
        {
            _proceduralChime = CreateProceduralChime();
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = offscreenPos;
            panelRect.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Displays an item pickup notification toast.
    /// </summary>
    /// <param name="icon">Item icon sprite.</param>
    /// <param name="localizedLabel">Localized item name or string.</param>
    /// <param name="amount">Item count (e.g. 1 creates "x1 [Label]").</param>
    public void Show(Sprite icon, string localizedLabel, int amount = 1)
    {
        if (panelRect == null)
        {
            BuildDefaultToastUI();
        }

        _queue.Enqueue(new ToastData
        {
            icon = icon,
            label = localizedLabel,
            amount = amount
        });

        if (_activeRoutine == null)
        {
            _activeRoutine = StartCoroutine(ProcessQueueRoutine());
        }
    }

    private IEnumerator ProcessQueueRoutine()
    {
        while (_queue.Count > 0)
        {
            ToastData item = _queue.Dequeue();

            // Setup Icon
            if (iconImage != null)
            {
                if (item.icon != null)
                {
                    iconImage.sprite = item.icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            // Setup Label Text
            if (labelText != null)
            {
                if (item.amount > 0)
                {
                    labelText.text = $"x{item.amount} {item.label}";
                }
                else
                {
                    labelText.text = item.label ?? "";
                }
            }

            // Play Chime
            PlayChime();

            // Activate Panel at offscreen start
            if (panelRect != null)
            {
                panelRect.gameObject.SetActive(true);
                panelRect.anchoredPosition = offscreenPos;
            }

            // Slide In (Ease-Out Cubic, Unscaled Time)
            float t = 0f;
            while (t < slideInDuration)
            {
                t += Time.unscaledDeltaTime;
                float norm = Mathf.Clamp01(t / Mathf.Max(0.001f, slideInDuration));
                float ease = 1f - Mathf.Pow(1f - norm, 3f);
                if (panelRect != null)
                {
                    panelRect.anchoredPosition = Vector2.LerpUnclamped(offscreenPos, onscreenPos, ease);
                }
                yield return null;
            }

            if (panelRect != null)
                panelRect.anchoredPosition = onscreenPos;

            // Rest / Display Duration (Unscaled Time)
            float waitTimer = 0f;
            while (waitTimer < displayDuration)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // Slide Out (Ease-In Cubic, Unscaled Time)
            t = 0f;
            while (t < slideOutDuration)
            {
                t += Time.unscaledDeltaTime;
                float norm = Mathf.Clamp01(t / Mathf.Max(0.001f, slideOutDuration));
                float ease = Mathf.Pow(norm, 3f);
                if (panelRect != null)
                {
                    panelRect.anchoredPosition = Vector2.LerpUnclamped(onscreenPos, offscreenPos, ease);
                }
                yield return null;
            }

            if (panelRect != null)
            {
                panelRect.anchoredPosition = offscreenPos;
            }

            // Small gap between multiple queued toasts
            if (_queue.Count > 0)
            {
                yield return new WaitForSecondsRealtime(0.12f);
            }
        }

        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(false);
        }

        _activeRoutine = null;
    }

    private void PlayChime()
    {
        AudioClip clipToPlay = toastSound != null ? toastSound : _proceduralChime;
        if (clipToPlay != null && audioSource != null)
        {
            audioSource.PlayOneShot(clipToPlay, 0.85f);
        }
    }

    /// <summary>
    /// Auto-constructs a PSX-styled toast panel if none was pre-wired in Inspector.
    /// Matches the pure black theme, subtle white border, and pixel font of PromptBox & TalkPanel.
    /// </summary>
    public void BuildDefaultToastUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                transform.SetParent(canvas.transform, false);
            }
        }

        // Panel Container
        GameObject panelObj = new GameObject("ToastPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panelObj.transform.SetParent(transform, false);
        panelRect = panelObj.GetComponent<RectTransform>();

        // Anchor Top-Right
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = new Vector2(265f, 54f);
        panelRect.anchoredPosition = offscreenPos;

        // Background Style: Pure Black matching canvas PromptBox/TalkPanel
        Image bgImg = panelObj.GetComponent<Image>();
        bgImg.color = new Color(0.0f, 0.0f, 0.0f, 0.92f);

        // Thin Border matching PromptBox / TalkPanel buttons
        Outline border = panelObj.GetComponent<Outline>();
        border.effectColor = new Color(1.0f, 1.0f, 1.0f, 0.45f);
        border.effectDistance = new Vector2(1.2f, -1.2f);

        // Horizontal Layout
        HorizontalLayoutGroup layout = panelObj.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 12, 6, 6);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Dedicated Recessed Frame for Ticket Picture
        GameObject frameObj = new GameObject("IconFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
        frameObj.transform.SetParent(panelObj.transform, false);
        RectTransform frameRect = frameObj.GetComponent<RectTransform>();
        frameRect.sizeDelta = new Vector2(58f, 38f);
        Image frameBg = frameObj.GetComponent<Image>();
        frameBg.color = new Color(0.06f, 0.06f, 0.06f, 1.0f);
        Outline frameBorder = frameObj.GetComponent<Outline>();
        frameBorder.effectColor = new Color(1.0f, 1.0f, 1.0f, 0.35f);
        frameBorder.effectDistance = new Vector2(1f, -1f);

        // Icon Image inside Frame
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(frameObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(54f, 34f);
        iconImage = iconObj.GetComponent<Image>();
        iconImage.preserveAspect = true;

        // Label Text
        GameObject textObj = new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(panelObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(170f, 38f);

        labelText = textObj.GetComponent<TextMeshProUGUI>();
        labelText.fontSize = 20f;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.color = Color.white;

        // Apply project font: GeistPixel-Regular-VariableFont_ELSH SDF
        TMP_FontAsset pixelFont = Resources.Load<TMP_FontAsset>("Fonts/GeistPixel-Regular-VariableFont_ELSH SDF");
        if (pixelFont == null)
        {
            var existingTmps = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var t in existingTmps)
            {
                if (t.font != null && t.font.name.Contains("GeistPixel"))
                {
                    pixelFont = t.font;
                    break;
                }
            }
        }

        if (pixelFont != null)
        {
            labelText.font = pixelFont;
        }

        panelObj.SetActive(false);
    }

    /// <summary>
    /// Generates a nostalgic 2-tone chime for PSX item pickups if no clip is wired.
    /// </summary>
    private AudioClip CreateProceduralChime()
    {
        int sampleRate = 44100;
        float duration = 0.45f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = t < 0.12f ? 523.25f : 659.25f; // C5 to E5
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f;
            float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.25f;
            float env = Mathf.Exp(-t * 7.5f);
            data[i] = (tone + harmonic) * env * 0.5f;
        }

        var clip = AudioClip.Create("PSX_ItemPickupChime", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
