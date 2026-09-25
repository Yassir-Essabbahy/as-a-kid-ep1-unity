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

        EnsureUIBuilt();
    }

    private void Start()
    {
        if (paperRoot != null)
            paperRoot.SetActive(false);
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

        if (paperRoot != null)
        {
            paperRoot.SetActive(true);
            StartCoroutine(FadeInRoutine());
        }
    }

    private void ConfigureForMode(PaperMode mode)
    {
        string childName = MetroStorySequenceController.ChildName;

        if (mode == PaperMode.Dare_Math)
        {
            headerText.text = $"<b>MATHEMATICS - CLASSWORK</b>\n<size=65%><color=#555555>Student: {childName}   |   Grade 5   |   Room 204</color></size>";
            bodyText.text = "<b>Solve the exercises below:</b>\n\n" +
                            "1)  3x + 14 = 35   ➔   <b>x = </b>\n\n" +
                            "2)  (128 ÷ 4) + (15 × 3) = \n\n" +
                            "3)  Calculate area of triangle: <i>base = 8cm, height = 5cm</i>\n" +
                            "    <b>Area = </b>\n\n" +
                            "<size=80%><color=#666677><i>(Use your mouse cursor to write and draw anywhere on this paper)</i></color></size>";

            drawingImage.gameObject.SetActive(true);
            clearButton.gameObject.SetActive(true);
            ClearDrawingBuffer();

            var doneLabel = doneButton.GetComponentInChildren<TextMeshProUGUI>();
            if (doneLabel != null) doneLabel.text = "SUBMIT WORK [E]";
        }
        else
        {
            headerText.text = "<b>STUDENT HEALTH & INCIDENT DOSSIER</b>\n<size=65%><color=#773333>CONFIDENTIAL - FACULTY USE ONLY</color></size>";
            bodyText.text = $"<b>Patient / Student:</b> {childName}\n" +
                            "<b>Case Status:</b> Severe Acute Dissociation\n" +
                            "<b>Summary:</b> Following the tragedy at the coastal beach, the student has developed persistent avoidance behavior. " +
                            "Refuses to accept that his brother is gone. Carries a worn stuffed bear named 'Mistfox', using it as a psychological surrogate to externalize blame.\n\n" +
                            "<b>Recommendation:</b> Immediate psychiatric intervention. He must be confronted with reality before he completely detaches from the waking world.";

            drawingImage.gameObject.SetActive(false);
            clearButton.gameObject.SetActive(false);

            var doneLabel = doneButton.GetComponentInChildren<TextMeshProUGUI>();
            if (doneLabel != null) doneLabel.text = "PUT DOWN [E]";
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
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
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
