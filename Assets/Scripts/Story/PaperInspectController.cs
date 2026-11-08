using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PaperInspectController : MonoBehaviour
{
    public static PaperInspectController Instance { get; private set; }

    public enum PaperMode
    {
        Dare_Math,
        Truth_Document
    }

    [Header("Current Mode")]
    public PaperMode currentMode = PaperMode.Dare_Math;

    [Header("Drawing Settings")]
    public int textureWidth = 620;
    public int textureHeight = 820;
    public Color pencilColor = new Color(0.12f, 0.16f, 0.22f, 0.95f);
    public int brushRadius = 3;

    [Header("State")]
    public bool isInspecting = false;
    public bool hasDrawn = false;

    // Runtime UI elements
    private Canvas paperCanvas;
    private GameObject paperRoot;
    private RectTransform paperPanel;
    private RawImage drawingImage;
    private Texture2D drawingTexture;
    private Color32[] textureBuffer;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI bodyText;
    private RectTransform footerRect;
    private Button doneButton;
    private Button clearButton;
    private CanvasGroup canvasGroup;

    private Vector2? lastDrawPos = null;
    private Action onFinishCallback;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Clean up or deactivate any serialized canvas instances
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("PaperInspect_Canvas"))
            {
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        EnsureUIBuilt();

        if (paperCanvas != null)
            paperCanvas.gameObject.SetActive(false);
        if (paperRoot != null)
            paperRoot.SetActive(false);
    }

    private void Start()
    {
        if (paperCanvas != null)
            paperCanvas.gameObject.SetActive(false);
        if (paperRoot != null)
            paperRoot.SetActive(false);

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("PaperInspect_Canvas"))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void EnsureUIBuilt()
    {
        if (paperCanvas != null) return;

        // Create UI Canvas for Paper Overlay
        var canvasGo = new GameObject("PaperInspect_Canvas");
        canvasGo.transform.SetParent(transform, false);

        paperCanvas = canvasGo.AddComponent<Canvas>();
        paperCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        paperCanvas.sortingOrder = 99; // Above world, below system modals

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Dark dim background overlay
        paperRoot = new GameObject("PaperRoot");
        paperRoot.transform.SetParent(canvasGo.transform, false);
        var rootRect = paperRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        var dimBg = paperRoot.AddComponent<Image>();
        dimBg.color = new Color(0f, 0f, 0f, 0.75f);

        canvasGroup = paperRoot.AddComponent<CanvasGroup>();

        // The Paper Sheet
        var sheetGo = new GameObject("PaperSheet");
        sheetGo.transform.SetParent(paperRoot.transform, false);
        paperPanel = sheetGo.AddComponent<RectTransform>();
        paperPanel.anchorMin = new Vector2(0.5f, 0.5f);
        paperPanel.anchorMax = new Vector2(0.5f, 0.5f);
        paperPanel.pivot = new Vector2(0.5f, 0.5f);
        paperPanel.sizeDelta = new Vector2(620f, 820f);

        var sheetImg = sheetGo.AddComponent<Image>();
        sheetImg.color = new Color(0.96f, 0.95f, 0.92f, 1f); // Off-white notebook page

        var sheetOutline = sheetGo.AddComponent<Outline>();
        sheetOutline.effectColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        sheetOutline.effectDistance = new Vector2(2f, -2f);

        // Header Text
        var headerGo = new GameObject("HeaderTitle");
        headerGo.transform.SetParent(sheetGo.transform, false);
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -24f);
        headerRect.sizeDelta = new Vector2(-40f, 60f);

        headerText = headerGo.AddComponent<TextMeshProUGUI>();
        headerText.fontSize = 24;
        headerText.color = new Color(0.12f, 0.12f, 0.15f, 1f);
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.TopLeft;
        headerText.raycastTarget = false;

        // Body Text
        var bodyGo = new GameObject("BodyText");
        bodyGo.transform.SetParent(sheetGo.transform, false);
        var bodyRect = bodyGo.AddComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -85f);
        bodyRect.sizeDelta = new Vector2(-50f, 320f);

        bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 18;
        bodyText.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        bodyText.lineSpacing = 1.35f;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.raycastTarget = false;

        // Drawing Area (RawImage with Texture2D) - covers the ENTIRE paper sheet!
        var drawGo = new GameObject("DrawingArea");
        drawGo.transform.SetParent(sheetGo.transform, false);
        var drawRect = drawGo.AddComponent<RectTransform>();
        drawRect.anchorMin = Vector2.zero;
        drawRect.anchorMax = Vector2.one;
        drawRect.pivot = new Vector2(0.5f, 0.5f);
        drawRect.anchoredPosition = Vector2.zero;
        drawRect.sizeDelta = Vector2.zero;

        drawingImage = drawGo.AddComponent<RawImage>();
        drawingImage.color = Color.white;
        drawingImage.raycastTarget = true;
        InitDrawingTexture();

        // Footer Buttons Container
        var footerGo = new GameObject("FooterButtons");
        footerGo.transform.SetParent(sheetGo.transform, false);
        footerRect = footerGo.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 20f);
        footerRect.sizeDelta = new Vector2(-40f, 50f);

        // Done / Put Down Button
        doneButton = CreateButton(footerGo.transform, "DoneButton", "DONE [E]", new Vector2(95f, 0f), new Vector2(180f, 38f));
        doneButton.onClick.AddListener(ClosePaper);

        // Clear Button (for drawing)
        clearButton = CreateButton(footerGo.transform, "ClearButton", "ERASE WORK", new Vector2(-95f, 0f), new Vector2(180f, 38f));
        clearButton.onClick.AddListener(ClearDrawing);

        // Deactivate both canvas and root overlay immediately upon construction
        if (paperRoot != null)
            paperRoot.SetActive(false);
        if (canvasGo != null)
            canvasGo.SetActive(false);
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);

        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 1f);

        var btn = btnGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        btn.colors = cb;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.color = Color.white;
        if (NpcDialogueManager.DefaultDialogueFont != null)
        {
            tmp.font = NpcDialogueManager.DefaultDialogueFont;
        }
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Normal;

        return btn;
    }

    private void InitDrawingTexture()
    {
        drawingTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        drawingTexture.filterMode = FilterMode.Bilinear;
        textureBuffer = new Color32[textureWidth * textureHeight];
        ClearDrawingBuffer();
        drawingImage.texture = drawingTexture;
    }

    private void ClearDrawingBuffer()
    {
        Color32 transparent = new Color32(0, 0, 0, 0);
        for (int i = 0; i < textureBuffer.Length; i++)
        {
            textureBuffer[i] = transparent;
        }
        if (drawingTexture != null)
        {
            drawingTexture.SetPixels32(textureBuffer);
            drawingTexture.Apply();
        }
        hasDrawn = false;
    }

    public void ClearDrawing()
    {
        ClearDrawingBuffer();
    }

    public void OpenPaper(PaperMode mode, Action onFinish = null)
    {
        EnsureUIBuilt();
        currentMode = mode;
        onFinishCallback = onFinish;
        isInspecting = true;
        hasDrawn = false;
        lastDrawPos = null;

        // Lock player controller
        var fps = FindAnyObjectByType<FirstPersonController>();
        if (fps != null) fps.SetControlLocked(true);

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ConfigureForMode(mode);

        if (paperCanvas != null)
        {
            paperCanvas.gameObject.SetActive(true);
        }

        if (paperRoot != null)
        {
            paperRoot.SetActive(true);
            StartCoroutine(FadeInRoutine());
        }
    }

    private void ConfigureForMode(PaperMode mode)
    {
        string lang = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : "en";
        bool isAr = string.Equals(lang, "ar", StringComparison.OrdinalIgnoreCase);
        bool isFr = string.Equals(lang, "fr", StringComparison.OrdinalIgnoreCase);

        if (headerText != null) headerText.alignment = isAr ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        if (bodyText != null) bodyText.alignment = isAr ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;

        var doneLabel = doneButton != null ? doneButton.GetComponentInChildren<TextMeshProUGUI>() : null;
        var clearLabel = clearButton != null ? clearButton.GetComponentInChildren<TextMeshProUGUI>() : null;

        if (mode == PaperMode.Dare_Math)
        {
            if (isAr)
            {
                if (headerText != null) headerText.text = LocalizationManager.FixRTL("<b>اختبار تقييم التركيز الإدراكي</b>\n<size=65%><color=#555555>قسم علم النفس السريري - مستشفى الأطفال</color></size>");
                if (bodyText != null) bodyText.text = LocalizationManager.FixRTL(
                    "<b>حل المسائل الحسابية التالية:</b>\n\n" +
                    "1)  <b>7 + 8 = </b>  (اكتب الناتج هنا)\n\n" +
                    "2)  <b>15 - 6 = </b>\n\n" +
                    "3)  <b>4 × 5 = </b>\n\n\n" +
                    "<size=80%><color=#666677><i>(استخدم مؤشر الفأرة للرسم والكتابة على الورقة)</i></color></size>"
                );
                if (doneLabel != null) doneLabel.text = LocalizationManager.FixRTL("تسليم الحل [E]");
                if (clearLabel != null) clearLabel.text = LocalizationManager.FixRTL("مسح");
            }
            else if (isFr)
            {
                if (headerText != null) headerText.text = "<b>ÉVALUATION DE CONCENTRATION COGNITIVE</b>\n<size=65%><color=#555555>Service de Psychiatrie Infantile</color></size>";
                if (bodyText != null) bodyText.text = "<b>Résolvez les exercices suivants :</b>\n\n" +
                                "1)  <b>7 + 8 = </b>  (Inscrivez la réponse)\n\n" +
                                "2)  <b>15 - 6 = </b>\n\n" +
                                "3)  <b>4 × 5 = </b>\n\n\n" +
                                "<size=80%><color=#666677><i>(Utilisez votre curseur pour écrire sur la feuille)</i></color></size>";
                if (doneLabel != null) doneLabel.text = "SOUMETTRE [E]";
                if (clearLabel != null) clearLabel.text = "EFFACER";
            }
            else
            {
                if (headerText != null) headerText.text = "<b>COGNITIVE CONCENTRATION EVALUATION</b>\n<size=65%><color=#555555>Department of Child Psychiatry</color></size>";
                if (bodyText != null) bodyText.text = "<b>Solve the following arithmetic problems:</b>\n\n" +
                                "1)  <b>7 + 8 = </b>  (Write your answer here)\n\n" +
                                "2)  <b>15 - 6 = </b>\n\n" +
                                "3)  <b>4 × 5 = </b>\n\n\n" +
                                "<size=80%><color=#666677><i>(Use your mouse cursor to write anywhere on this paper)</i></color></size>";
                if (doneLabel != null) doneLabel.text = "SUBMIT WORK [E]";
                if (clearLabel != null) clearLabel.text = "CLEAR";
            }

            drawingImage.gameObject.SetActive(true);
            clearButton.gameObject.SetActive(true);
            ClearDrawingBuffer();
        }
        else
        {
            if (isAr)
            {
                if (headerText != null) headerText.text = LocalizationManager.FixRTL("<b>تقرير الخروج الطبي - مستشفى الأطفال</b>\n<size=65%><color=#773333>سري للغاية - للملف الطبي فقط</color></size>");
                if (bodyText != null) bodyText.text = LocalizationManager.FixRTL(
                    "<b>التشخيص الأولي:</b> اضطراب تفارقي حاد ناتج عن رضّ نفسي بعد الحادث.\n\n" +
                    "<b>ملاحظة سريرية:</b> المريض يظهر نوبات إنكار شديدة وصعوبة في التواصل البصري. " +
                    "يتشبث بدمية محشوة تدعى 'دودو' ويتعامل معها كدرع نفسي لمقاومة الصدمة والهروب من الواقع.\n\n" +
                    "<b>التوصية:</b> يُوصى بالعزل المنزلي وتناول الأدوية المهدئة بانتظام والمتابعة النفسية المكثفة."
                );
                if (doneLabel != null) doneLabel.text = LocalizationManager.FixRTL("إنهاء القراءة [E]");
            }
            else if (isFr)
            {
                if (headerText != null) headerText.text = "<b>RAPPORT DE SORTIE MÉDICALE</b>\n<size=65%><color=#773333>CONFIDENTIEL - DOSSIER MÉDICAL</color></size>";
                if (bodyText != null) bodyText.text = "<b>Diagnostic :</b> Trouble dissociatif aigu résultant d'un traumatisme psychologique après l'accident.\n\n" +
                                "<b>Observation clinique :</b> Le jeune patient manifeste un déni sévère de la réalité. " +
                                "Il s'accroche constamment à une peluche nommée 'Doudou', l'utilisant comme substitut psychologique pour fuir son angoisse.\n\n" +
                                "<b>Recommandation :</b> Isolement à domicile, administration régulière de sédatifs et suivi psychiatrique urgent.";
                if (doneLabel != null) doneLabel.text = "POSER LE RAPPORT [E]";
            }
            else
            {
                if (headerText != null) headerText.text = "<b>HOSPITAL DISCHARGE MEDICAL REPORT</b>\n<size=65%><color=#773333>CONFIDENTIAL - MEDICAL RECORDS ONLY</color></size>";
                if (bodyText != null) bodyText.text = "<b>Primary Diagnosis:</b> Acute dissociative disorder resulting from psychological trauma after the accident.\n\n" +
                                "<b>Clinical Observation:</b> Patient exhibits severe avoidance behavior and detachment from surroundings. " +
                                "Clings persistently to a worn stuffed toy named 'Doudou', using it as a defensive psychological surrogate against reality.\n\n" +
                                "<b>Recommendation:</b> Recommended home isolation, regular sedative medication, and intensive psychiatric follow-up.";
                if (doneLabel != null) doneLabel.text = "PUT DOWN [E]";
            }

            drawingImage.gameObject.SetActive(false);
            clearButton.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeInRoutine()
    {
        if (canvasGroup == null) yield break;
        canvasGroup.alpha = 0f;
        float elapsed = 0f;
        float duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    public void ClosePaper()
    {
        if (!isInspecting) return;
        StartCoroutine(ClosePaperRoutine());
    }

    public void CloseInspect()
    {
        ClosePaper();
    }

    public void CloseInspectImmediate()
    {
        isInspecting = false;
        if (paperRoot != null) paperRoot.SetActive(false);
        if (paperCanvas != null) paperCanvas.gameObject.SetActive(false);

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("PaperInspect_Canvas"))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator ClosePaperRoutine()
    {
        isInspecting = false;

        if (canvasGroup != null)
        {
            float elapsed = 0f;
            float duration = 0.15f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
        }

        if (paperRoot != null) paperRoot.SetActive(false);
        if (paperCanvas != null) paperCanvas.gameObject.SetActive(false);

        // Relock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Unlock player controller
        var fps = FindAnyObjectByType<FirstPersonController>();
        if (fps != null) fps.SetControlLocked(false);

        var cb = onFinishCallback;
        onFinishCallback = null;
        cb?.Invoke();

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.OnPaperInspectionFinished(currentMode);
        }
    }

    private void Update()
    {
        if (!isInspecting) return;

        // Allow closing via 'E' or 'Escape'
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame)
        {
            ClosePaper();
            return;
        }

        // Active drawing handling in Math mode
        if (currentMode == PaperMode.Dare_Math && drawingImage != null && drawingImage.gameObject.activeInHierarchy)
        {
            HandleDrawingInput();
        }
    }

    private void HandleDrawingInput()
    {
        var drawRect = drawingImage.rectTransform;

        // Do not draw when clicking over the footer buttons
        if (footerRect != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(footerRect, Input.mousePosition, paperCanvas.worldCamera, out Vector2 fPoint))
            {
                if (footerRect.rect.Contains(fPoint))
                {
                    lastDrawPos = null;
                    return;
                }
            }
        }

        if (Input.GetMouseButton(0))
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(drawRect, Input.mousePosition, paperCanvas.worldCamera, out Vector2 localPoint))
            {
                // Check if inside bounds of the paper sheet
                Rect r = drawRect.rect;
                if (r.Contains(localPoint))
                {
                    // Normalize to [0, 1] UV coordinates
                    float u = (localPoint.x - r.x) / r.width;
                    float v = (localPoint.y - r.y) / r.height;

                    int px = Mathf.Clamp((int)(u * textureWidth), 0, textureWidth - 1);
                    int py = Mathf.Clamp((int)(v * textureHeight), 0, textureHeight - 1);
                    Vector2 currentPos = new Vector2(px, py);

                    if (lastDrawPos.HasValue)
                    {
                        DrawLineOnBuffer(lastDrawPos.Value, currentPos, brushRadius, pencilColor);
                    }
                    else
                    {
                        DrawCircleOnBuffer(px, py, brushRadius, pencilColor);
                    }

                    lastDrawPos = currentPos;
                    hasDrawn = true;
                    drawingTexture.SetPixels32(textureBuffer);
                    drawingTexture.Apply();
                }
                else
                {
                    lastDrawPos = null;
                }
            }
        }
        else
        {
            lastDrawPos = null;
        }
    }

    private void DrawCircleOnBuffer(int cx, int cy, int radius, Color color)
    {
        Color32 col32 = color;
        int rSq = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            int py = cy + y;
            if (py < 0 || py >= textureHeight) continue;
            for (int x = -radius; x <= radius; x++)
            {
                int px = cx + x;
                if (px < 0 || px >= textureWidth) continue;

                if (x * x + y * y <= rSq)
                {
                    textureBuffer[py * textureWidth + px] = col32;
                }
            }
        }
    }

    private void DrawLineOnBuffer(Vector2 start, Vector2 end, int radius, Color color)
    {
        float dist = Vector2.Distance(start, end);
        int steps = Mathf.Max(1, (int)(dist / 1.5f));
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 p = Vector2.Lerp(start, end, t);
            DrawCircleOnBuffer((int)p.x, (int)p.y, radius, color);
        }
    }
}
