using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Life is Strange 1 style UI for the classroom scene:
/// - Minimalist center reticle dot + expanding focus ring
/// - Sleek LiS-style interaction pill badge ([E] Look • Pen)
/// - Child inner monologue thought subtitle display (soft italicized text)
/// - Teacher lecture subtitle display (warm speaker gold)
/// - Cinematic letterbox bands
/// </summary>
public class ClassroomLifeIsStrangeUI : MonoBehaviour
{
    public static ClassroomLifeIsStrangeUI Instance { get; private set; }

    [Header("Canvas & References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform reticleRoot;
    [SerializeField] private Image reticleDot;
    [SerializeField] private Image reticleRing;
    [SerializeField] private RectTransform promptBadge;
    [SerializeField] private CanvasGroup promptCanvasGroup;
    [SerializeField] private TextMeshProUGUI promptKeyText;
    [SerializeField] private TextMeshProUGUI promptActionText;
    [SerializeField] private TextMeshProUGUI promptItemNameText;

    [Header("Inner Monologue Thoughts")]
    [SerializeField] private RectTransform thoughtBox;
    [SerializeField] private CanvasGroup thoughtCanvasGroup;
    [SerializeField] private TextMeshProUGUI thoughtText;

    [Header("Teacher Subtitles")]
    [SerializeField] private RectTransform teacherBox;
    [SerializeField] private CanvasGroup teacherCanvasGroup;
    [SerializeField] private TextMeshProUGUI teacherSpeakerText;
    [SerializeField] private TextMeshProUGUI teacherBodyText;

    [Header("Letterbox Bands")]
    [SerializeField] private RectTransform topBand;
    [SerializeField] private RectTransform bottomBand;
    [SerializeField] private float letterboxHeight = 120f;
    [SerializeField] private float bandSlideDuration = 0.6f;

    [Header("Tuning")]
    [SerializeField] private float hoverFadeSpeed = 12f;
    [SerializeField] private float reticleNormalScale = 0.5f;
    [SerializeField] private float reticleHoverScale = 1.0f;

    private bool _isHovering = false;
    private float _currentHoverWeight = 0f;
    private Coroutine _thoughtCoroutine;
    private Coroutine _teacherCoroutine;
    private Coroutine _bandsCoroutine;
    private TMP_FontAsset _pixelFont;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureUIElementsCreated();
    }

    private void Start()
    {
        SetPromptVisible(false, immediate: true);
        HideThought(immediate: true);
        HideTeacherSubtitle(immediate: true);
    }

    private void Update()
    {
        float targetWeight = _isHovering ? 1f : 0f;
        _currentHoverWeight = Mathf.MoveTowards(_currentHoverWeight, targetWeight, Time.deltaTime * hoverFadeSpeed);

        if (promptCanvasGroup != null)
        {
            promptCanvasGroup.alpha = _currentHoverWeight;
        }

        if (reticleRing != null)
        {
            float scale = Mathf.Lerp(reticleNormalScale, reticleHoverScale, _currentHoverWeight);
            reticleRing.rectTransform.localScale = new Vector3(scale, scale, 1f);
            Color ringCol = reticleRing.color;
            ringCol.a = Mathf.Lerp(0.25f, 0.95f, _currentHoverWeight);
            reticleRing.color = ringCol;
        }

        if (reticleDot != null)
        {
            Color dotCol = reticleDot.color;
            dotCol.a = Mathf.Lerp(0.5f, 1f, _currentHoverWeight);
            reticleDot.color = dotCol;
        }
    }

    /// <summary>
    /// Updates the interaction prompt badge with the current hover target.
    /// </summary>
    public void ShowPrompt(string key, string action, string itemName)
    {
        _isHovering = true;

        if (promptKeyText != null) promptKeyText.text = key;
        if (promptActionText != null) promptActionText.text = action;
        if (promptItemNameText != null)
        {
            promptItemNameText.text = string.IsNullOrEmpty(itemName) ? "" : $"•  {itemName}";
        }
    }

    public void HidePrompt()
    {
        _isHovering = false;
    }

    public void SetPromptVisible(bool visible, bool immediate = false)
    {
        _isHovering = visible;
        if (immediate)
        {
            _currentHoverWeight = visible ? 1f : 0f;
            if (promptCanvasGroup != null) promptCanvasGroup.alpha = _currentHoverWeight;
        }
    }

    /// <summary>
    /// Displays a child inner thought line (monologue) with elegant styling and duration.
    /// </summary>
    public void ShowThought(string text, float duration = 3.5f)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (_thoughtCoroutine != null) StopCoroutine(_thoughtCoroutine);
        _thoughtCoroutine = StartCoroutine(ThoughtRoutine(text, duration));
    }

    private IEnumerator ThoughtRoutine(string text, float duration)
    {
        if (thoughtBox != null) thoughtBox.gameObject.SetActive(true);
        if (thoughtText != null)
        {
            // Life is Strange style: italicized inner thought
            thoughtText.text = $"<i>\"{text}\"</i>";
        }

        // Fade in
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            if (thoughtCanvasGroup != null) thoughtCanvasGroup.alpha = Mathf.Clamp01(t / 0.25f);
            yield return null;
        }

        if (thoughtCanvasGroup != null) thoughtCanvasGroup.alpha = 1f;

        // Hold
        float waitTime = Mathf.Max(duration, text.Length * 0.055f);
        yield return new WaitForSeconds(waitTime);

        // Fade out
        t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            if (thoughtCanvasGroup != null) thoughtCanvasGroup.alpha = Mathf.Clamp01(1f - (t / 0.35f));
            yield return null;
        }

        HideThought(immediate: true);
        _thoughtCoroutine = null;
    }

    public void HideThought(bool immediate = false)
    {
        if (_thoughtCoroutine != null)
        {
            StopCoroutine(_thoughtCoroutine);
            _thoughtCoroutine = null;
        }

        if (thoughtCanvasGroup != null) thoughtCanvasGroup.alpha = 0f;
        if (thoughtBox != null) thoughtBox.gameObject.SetActive(false);
    }

    /// <summary>
    /// Displays a teacher lecture subtitle at the bottom of the screen.
    /// </summary>
    public void ShowTeacherSubtitle(string speaker, string line, float duration = 4.0f)
    {
        if (_teacherCoroutine != null) StopCoroutine(_teacherCoroutine);
        _teacherCoroutine = StartCoroutine(TeacherSubtitleRoutine(speaker, line, duration));
    }

    private IEnumerator TeacherSubtitleRoutine(string speaker, string line, float duration)
    {
        if (teacherBox != null) teacherBox.gameObject.SetActive(true);
        if (teacherSpeakerText != null) teacherSpeakerText.text = speaker;
        if (teacherBodyText != null) teacherBodyText.text = line;

        // Fade in
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            if (teacherCanvasGroup != null) teacherCanvasGroup.alpha = Mathf.Clamp01(t / 0.2f);
            yield return null;
        }
        if (teacherCanvasGroup != null) teacherCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(duration);

        // Fade out
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            if (teacherCanvasGroup != null) teacherCanvasGroup.alpha = Mathf.Clamp01(1f - (t / 0.3f));
            yield return null;
        }

        HideTeacherSubtitle(immediate: true);
        _teacherCoroutine = null;
    }

    public void HideTeacherSubtitle(bool immediate = false)
    {
        if (_teacherCoroutine != null)
        {
            StopCoroutine(_teacherCoroutine);
            _teacherCoroutine = null;
        }

        if (teacherCanvasGroup != null) teacherCanvasGroup.alpha = 0f;
        if (teacherBox != null) teacherBox.gameObject.SetActive(false);
    }

    /// <summary>
    /// Slides the cinematic black letterbox bands in or off.
    /// </summary>
    public void SetLetterboxBands(bool slideIn)
    {
        if (_bandsCoroutine != null) StopCoroutine(_bandsCoroutine);
        _bandsCoroutine = StartCoroutine(LetterboxRoutine(slideIn));
    }

    private IEnumerator LetterboxRoutine(bool slideIn)
    {
        if (topBand == null || bottomBand == null) yield break;

        topBand.gameObject.SetActive(true);
        bottomBand.gameObject.SetActive(true);

        float elapsed = 0f;
        Vector2 topStart = topBand.anchoredPosition;
        Vector2 topEnd = slideIn ? new Vector2(0f, 0f) : new Vector2(0f, letterboxHeight);

        Vector2 bottomStart = bottomBand.anchoredPosition;
        Vector2 bottomEnd = slideIn ? new Vector2(0f, 0f) : new Vector2(0f, -letterboxHeight);

        while (elapsed < bandSlideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bandSlideDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            topBand.anchoredPosition = Vector2.Lerp(topStart, topEnd, smooth);
            bottomBand.anchoredPosition = Vector2.Lerp(bottomStart, bottomEnd, smooth);
            yield return null;
        }

        topBand.anchoredPosition = topEnd;
        bottomBand.anchoredPosition = bottomEnd;

        if (!slideIn)
        {
            topBand.gameObject.SetActive(false);
            bottomBand.gameObject.SetActive(false);
        }

        _bandsCoroutine = null;
    }

    /// <summary>
    /// Programmatically generates or connects the Life is Strange HUD elements.
    /// </summary>
    public void EnsureUIElementsCreated()
    {
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var cg = new GameObject("LifeIsStrange_Canvas");
                canvas = cg.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cg.AddComponent<CanvasScaler>();
                cg.AddComponent<GraphicRaycaster>();
            }
        }

        _pixelFont = Resources.Load<TMP_FontAsset>("Fonts/GeistPixel-Regular-VariableFont_ELSH SDF");
        if (_pixelFont == null)
        {
            _pixelFont = NpcDialogueManager.DefaultDialogueFont;
        }

        // 1. Center Reticle Root
        if (reticleRoot == null)
        {
            var rObj = canvas.transform.Find("LIS_ReticleRoot");
            if (rObj != null) reticleRoot = rObj.GetComponent<RectTransform>();
            else
            {
                var go = new GameObject("LIS_ReticleRoot", typeof(RectTransform));
                reticleRoot = go.GetComponent<RectTransform>();
                reticleRoot.SetParent(canvas.transform, false);
                reticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
                reticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
                reticleRoot.pivot = new Vector2(0.5f, 0.5f);
                reticleRoot.sizeDelta = new Vector2(40f, 40f);
                reticleRoot.anchoredPosition = Vector2.zero;
            }
        }

        // Reticle Ring
        if (reticleRing == null && reticleRoot != null)
        {
            var ringObj = reticleRoot.Find("ReticleRing");
            if (ringObj != null) reticleRing = ringObj.GetComponent<Image>();
            else
            {
                var go = new GameObject("ReticleRing", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(reticleRoot, false);
                reticleRing = go.GetComponent<Image>();
                reticleRing.rectTransform.sizeDelta = new Vector2(28f, 28f);
                reticleRing.color = new Color(1f, 1f, 1f, 0.35f);
                reticleRing.raycastTarget = false;
            }
        }

        // Reticle Dot
        if (reticleDot == null && reticleRoot != null)
        {
            var dotObj = reticleRoot.Find("ReticleDot");
            if (dotObj != null) reticleDot = dotObj.GetComponent<Image>();
            else
            {
                var go = new GameObject("ReticleDot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(reticleRoot, false);
                reticleDot = go.GetComponent<Image>();
                reticleDot.rectTransform.sizeDelta = new Vector2(5f, 5f);
                reticleDot.color = new Color(1f, 1f, 1f, 0.75f);
                reticleDot.raycastTarget = false;
            }
        }

        // 2. Prompt Badge (Pill)
        if (promptBadge == null && reticleRoot != null)
        {
            var pObj = reticleRoot.Find("PromptBadge");
            if (pObj != null)
            {
                promptBadge = pObj.GetComponent<RectTransform>();
                promptCanvasGroup = pObj.GetComponent<CanvasGroup>();
            }
            else
            {
                var go = new GameObject("PromptBadge", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
                go.transform.SetParent(reticleRoot, false);
                promptBadge = go.GetComponent<RectTransform>();
                promptBadge.pivot = new Vector2(0.5f, 1f);
                promptBadge.anchoredPosition = new Vector2(0f, -22f);

                promptCanvasGroup = go.GetComponent<CanvasGroup>();
                promptCanvasGroup.alpha = 0f;
                promptCanvasGroup.blocksRaycasts = false;
                promptCanvasGroup.interactable = false;

                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.08f, 0.09f, 0.12f, 0.88f);
                bg.raycastTarget = false;

                var hlg = go.GetComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(12, 14, 6, 6);
                hlg.spacing = 8f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                var csf = go.GetComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Key pill
                var keyGo = new GameObject("KeyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                keyGo.transform.SetParent(go.transform, false);
                promptKeyText = keyGo.GetComponent<TextMeshProUGUI>();
                promptKeyText.text = "[E]";
                promptKeyText.fontSize = 20f;
                promptKeyText.fontStyle = FontStyles.Bold;
                promptKeyText.color = new Color(1f, 0.85f, 0.3f);
                if (_pixelFont != null) promptKeyText.font = _pixelFont;

                // Action verb
                var actGo = new GameObject("ActionText", typeof(RectTransform), typeof(TextMeshProUGUI));
                actGo.transform.SetParent(go.transform, false);
                promptActionText = actGo.GetComponent<TextMeshProUGUI>();
                promptActionText.text = "Look";
                promptActionText.fontSize = 19f;
                promptActionText.fontStyle = FontStyles.Bold;
                promptActionText.color = Color.white;
                if (_pixelFont != null) promptActionText.font = _pixelFont;

                // Item Name
                var itemGo = new GameObject("ItemNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
                itemGo.transform.SetParent(go.transform, false);
                promptItemNameText = itemGo.GetComponent<TextMeshProUGUI>();
                promptItemNameText.text = "• Pen";
                promptItemNameText.fontSize = 18f;
                promptItemNameText.color = new Color(0.85f, 0.88f, 0.95f, 0.9f);
                if (_pixelFont != null) promptItemNameText.font = _pixelFont;
            }
        }

        // 3. Inner Thought Box
        if (thoughtBox == null)
        {
            var tObj = canvas.transform.Find("LIS_ThoughtBox");
            if (tObj != null)
            {
                thoughtBox = tObj.GetComponent<RectTransform>();
                thoughtCanvasGroup = tObj.GetComponent<CanvasGroup>();
                thoughtText = tObj.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                var go = new GameObject("LIS_ThoughtBox", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(ContentSizeFitter));
                go.transform.SetParent(canvas.transform, false);
                thoughtBox = go.GetComponent<RectTransform>();
                thoughtBox.anchorMin = new Vector2(0.5f, 0f);
                thoughtBox.anchorMax = new Vector2(0.5f, 0f);
                thoughtBox.pivot = new Vector2(0.5f, 0f);
                thoughtBox.sizeDelta = new Vector2(850f, 60f);
                thoughtBox.anchoredPosition = new Vector2(0f, 150f);

                thoughtCanvasGroup = go.GetComponent<CanvasGroup>();
                thoughtCanvasGroup.alpha = 0f;
                thoughtCanvasGroup.blocksRaycasts = false;
                thoughtCanvasGroup.interactable = false;

                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.04f, 0.05f, 0.08f, 0.75f);
                bg.raycastTarget = false;

                var csf = go.GetComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var textGo = new GameObject("ThoughtText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);
                var rt = textGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = new Vector2(-40f, -20f);
                rt.anchoredPosition = Vector2.zero;

                thoughtText = textGo.GetComponent<TextMeshProUGUI>();
                thoughtText.fontSize = 24f;
                thoughtText.fontStyle = FontStyles.Italic;
                thoughtText.alignment = TextAlignmentOptions.Center;
                thoughtText.color = new Color(0.92f, 0.96f, 1f, 1f);
                if (_pixelFont != null) thoughtText.font = _pixelFont;
            }
        }

        // 4. Teacher Subtitle Box
        if (teacherBox == null)
        {
            var tbObj = canvas.transform.Find("LIS_TeacherBox");
            if (tbObj != null)
            {
                teacherBox = tbObj.GetComponent<RectTransform>();
                teacherCanvasGroup = tbObj.GetComponent<CanvasGroup>();
            }
            else
            {
                var go = new GameObject("LIS_TeacherBox", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                teacherBox = go.GetComponent<RectTransform>();
                teacherBox.anchorMin = new Vector2(0.5f, 0f);
                teacherBox.anchorMax = new Vector2(0.5f, 0f);
                teacherBox.pivot = new Vector2(0.5f, 0f);
                teacherBox.sizeDelta = new Vector2(900f, 75f);
                teacherBox.anchoredPosition = new Vector2(0f, 40f);

                teacherCanvasGroup = go.GetComponent<CanvasGroup>();
                teacherCanvasGroup.alpha = 0f;
                teacherCanvasGroup.blocksRaycasts = false;
                teacherCanvasGroup.interactable = false;

                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.03f, 0.03f, 0.05f, 0.85f);
                bg.raycastTarget = false;

                var textGo = new GameObject("TeacherBody", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);
                var rt = textGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = new Vector2(-40f, -14f);
                rt.anchoredPosition = Vector2.zero;

                teacherBodyText = textGo.GetComponent<TextMeshProUGUI>();
                teacherBodyText.fontSize = 24f;
                teacherBodyText.alignment = TextAlignmentOptions.Center;
                teacherBodyText.color = NpcDialogueManager.GetSpeakerColor("Teacher");
                if (_pixelFont != null) teacherBodyText.font = _pixelFont;
            }
        }

        // 5. Letterbox Bands
        if (topBand == null)
        {
            var tb = canvas.transform.Find("LIS_TopBand");
            if (tb != null) topBand = tb.GetComponent<RectTransform>();
            else
            {
                var go = new GameObject("LIS_TopBand", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                topBand = go.GetComponent<RectTransform>();
                topBand.anchorMin = new Vector2(0f, 1f);
                topBand.anchorMax = new Vector2(1f, 1f);
                topBand.pivot = new Vector2(0.5f, 1f);
                topBand.sizeDelta = new Vector2(0f, letterboxHeight);
                topBand.anchoredPosition = new Vector2(0f, letterboxHeight);
                var img = go.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;
            }
        }

        if (bottomBand == null)
        {
            var bb = canvas.transform.Find("LIS_BottomBand");
            if (bb != null) bottomBand = bb.GetComponent<RectTransform>();
            else
            {
                var go = new GameObject("LIS_BottomBand", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                bottomBand = go.GetComponent<RectTransform>();
                bottomBand.anchorMin = new Vector2(0f, 0f);
                bottomBand.anchorMax = new Vector2(1f, 0f);
                bottomBand.pivot = new Vector2(0.5f, 0f);
                bottomBand.sizeDelta = new Vector2(0f, letterboxHeight);
                bottomBand.anchoredPosition = new Vector2(0f, -letterboxHeight);
                var img = go.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;
            }
        }
    }
}
