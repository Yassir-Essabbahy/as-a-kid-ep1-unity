using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal and simple dialogue & interaction UI matching the game's retro aesthetic:
/// - Minimal single-dot center reticle
/// - Black rectangle interaction box: "Press 'E' To Look"
/// - Cinematic letterbox bands (Top & Bottom)
/// - Single dialogue text inside bottom band: "<Speaker>: <Line>" in GeistPixel font
/// </summary>
public class ClassroomLifeIsStrangeUI : MonoBehaviour
{
    public static ClassroomLifeIsStrangeUI Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private Canvas canvas;

    [Header("Reticle")]
    [SerializeField] private RectTransform reticleDot;

    [Header("Interaction Prompt Box (Black Rectangle)")]
    [SerializeField] private GameObject promptBox;
    [SerializeField] private RectTransform promptBoxRect;
    [SerializeField] private TextMeshProUGUI promptText;

    [Header("Cinematic Letterbox Bands")]
    [SerializeField] private RectTransform topBand;
    [SerializeField] private RectTransform bottomBand;
    [SerializeField] private float topBandHeight = 140f;
    [SerializeField] private float bottomBandHeight = 220f;

    [Header("Dialogue Text (Inside Bottom Band)")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    private Coroutine _dialogueRoutine;
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
        HidePrompt();
        ClearDialogue();
    }

    /// <summary>
    /// Displays the minimal black interaction prompt box: "Press 'E' To <Action>"
    /// </summary>
    public void ShowPrompt(string key, string action, string itemName = "")
    {
        if (promptBox == null || promptText == null) return;

        string displayKey = string.IsNullOrEmpty(key) ? "E" : key.ToUpperInvariant();
        string displayAction = string.IsNullOrEmpty(action) ? "Look" : action;

        // Matches Image 2: Press 'E' To Talk / Press 'E' To Look
        string promptString = $"Press '{displayKey}' To {displayAction}";

        promptText.text = promptString;
        promptBox.SetActive(true);

        if (promptBoxRect != null)
        {
            float targetWidth = Mathf.Max(180f, promptString.Length * 11.5f + 36f);
            promptBoxRect.sizeDelta = new Vector2(targetWidth, 38f);
        }
    }

    public void HidePrompt()
    {
        if (promptBox != null)
        {
            promptBox.SetActive(false);
        }
    }

    /// <summary>
    /// Displays a dialogue line directly inside the bottom letterbox band:
    /// "<Speaker>: <Text>" matching Image 1.
    /// </summary>
    public void ShowDialogueLine(string speaker, string line, float duration = 3.5f)
    {
        if (string.IsNullOrEmpty(line)) return;

        if (_dialogueRoutine != null) StopCoroutine(_dialogueRoutine);
        _dialogueRoutine = StartCoroutine(DialogueRoutine(speaker, line, duration));
    }

    private IEnumerator DialogueRoutine(string speaker, string line, float duration)
    {
        if (dialogueText != null)
        {
            Color speakerColor = NpcDialogueManager.GetSpeakerColor(speaker);
            dialogueText.color = speakerColor;

            string formatted;
            if (!string.IsNullOrEmpty(speaker))
            {
                formatted = $"{speaker}: {line}";
            }
            else
            {
                formatted = line;
            }

            dialogueText.text = formatted;
        }

        yield return new WaitForSeconds(duration);

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
        _dialogueRoutine = null;
    }

    public void ClearDialogue()
    {
        if (_dialogueRoutine != null)
        {
            StopCoroutine(_dialogueRoutine);
            _dialogueRoutine = null;
        }

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
    }

    /// <summary>
    /// Ensures all minimal UI elements exist and are correctly structured on the Canvas.
    /// </summary>
    public void EnsureUIElementsCreated()
    {
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var cg = new GameObject("Classroom_Canvas");
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

        // Clean up any legacy bloated LIS elements if they exist
        var oldReticleRoot = canvas.transform.Find("LIS_ReticleRoot");
        if (oldReticleRoot != null) DestroyImmediate(oldReticleRoot.gameObject);
        var oldThoughtBox = canvas.transform.Find("LIS_ThoughtBox");
        if (oldThoughtBox != null) DestroyImmediate(oldThoughtBox.gameObject);
        var oldTeacherBox = canvas.transform.Find("LIS_TeacherBox");
        if (oldTeacherBox != null) DestroyImmediate(oldTeacherBox.gameObject);

        // 1. Center Reticle Dot (Minimal 4x4 white pixel dot)
        if (reticleDot == null)
        {
            var dotObj = canvas.transform.Find("Minimal_ReticleDot");
            if (dotObj != null) reticleDot = dotObj.GetComponent<RectTransform>();
            else
            {
                var go = new GameObject("Minimal_ReticleDot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                reticleDot = go.GetComponent<RectTransform>();
                reticleDot.anchorMin = new Vector2(0.5f, 0.5f);
                reticleDot.anchorMax = new Vector2(0.5f, 0.5f);
                reticleDot.pivot = new Vector2(0.5f, 0.5f);
                reticleDot.sizeDelta = new Vector2(4f, 4f);
                reticleDot.anchoredPosition = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.85f);
                img.raycastTarget = false;
            }
        }

        // 2. Interaction Prompt Box (Black rectangle with white text matching Image 2)
        if (promptBox == null)
        {
            var pbObj = canvas.transform.Find("Minimal_PromptBox");
            if (pbObj != null)
            {
                promptBox = pbObj.gameObject;
                promptBoxRect = pbObj.GetComponent<RectTransform>();
                promptText = pbObj.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                var go = new GameObject("Minimal_PromptBox", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                promptBox = go;
                promptBoxRect = go.GetComponent<RectTransform>();
                promptBoxRect.anchorMin = new Vector2(0.5f, 0.28f);
                promptBoxRect.anchorMax = new Vector2(0.5f, 0.28f);
                promptBoxRect.pivot = new Vector2(0.5f, 0.5f);
                promptBoxRect.sizeDelta = new Vector2(210f, 38f);
                promptBoxRect.anchoredPosition = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;

                // Child text
                var textGo = new GameObject("InteractText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;
                textRt.anchoredPosition = Vector2.zero;

                promptText = textGo.GetComponent<TextMeshProUGUI>();
                promptText.text = "Press 'E' To Look";
                promptText.fontSize = 20f;
                promptText.alignment = TextAlignmentOptions.Center;
                promptText.color = Color.white;
                promptText.raycastTarget = false;
                if (_pixelFont != null) promptText.font = _pixelFont;

                promptBox.SetActive(false);
            }
        }

        // 3. Cinematic Letterbox Bands (Solid Black Top & Bottom)
        if (topBand == null)
        {
            var tb = canvas.transform.Find("Cinematic_TopBand") ?? canvas.transform.Find("LIS_TopBand");
            if (tb != null)
            {
                tb.name = "Cinematic_TopBand";
                topBand = tb.GetComponent<RectTransform>();
                topBand.sizeDelta = new Vector2(0f, topBandHeight);
                topBand.anchoredPosition = Vector2.zero;
            }
            else
            {
                var go = new GameObject("Cinematic_TopBand", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                topBand = go.GetComponent<RectTransform>();
                topBand.anchorMin = new Vector2(0f, 1f);
                topBand.anchorMax = new Vector2(1f, 1f);
                topBand.pivot = new Vector2(0.5f, 1f);
                topBand.sizeDelta = new Vector2(0f, topBandHeight);
                topBand.anchoredPosition = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;
            }
        }

        if (bottomBand == null)
        {
            var bb = canvas.transform.Find("Cinematic_BottomBand") ?? canvas.transform.Find("LIS_BottomBand");
            if (bb != null)
            {
                bb.name = "Cinematic_BottomBand";
                bottomBand = bb.GetComponent<RectTransform>();
                bottomBand.sizeDelta = new Vector2(0f, bottomBandHeight);
                bottomBand.anchoredPosition = Vector2.zero;
            }
            else
            {
                var go = new GameObject("Cinematic_BottomBand", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                bottomBand = go.GetComponent<RectTransform>();
                bottomBand.anchorMin = new Vector2(0f, 0f);
                bottomBand.anchorMax = new Vector2(1f, 0f);
                bottomBand.pivot = new Vector2(0.5f, 0f);
                bottomBand.sizeDelta = new Vector2(0f, bottomBandHeight);
                bottomBand.anchoredPosition = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;
            }
        }

        // 4. Dialogue Text (Inside Bottom Band, left-aligned, matching Image 1)
        if (dialogueText == null && bottomBand != null)
        {
            var dtObj = bottomBand.Find("DialogueText");
            if (dtObj != null) dialogueText = dtObj.GetComponent<TextMeshProUGUI>();
            else
            {
                var go = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(bottomBand, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.08f, 0.22f);
                rt.anchorMax = new Vector2(0.92f, 0.85f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;

                dialogueText = go.GetComponent<TextMeshProUGUI>();
                dialogueText.fontSize = NpcDialogueManager.STANDARD_FONT_SIZE;
                dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
                dialogueText.color = Color.white;
                dialogueText.raycastTarget = false;
                dialogueText.text = "";
                if (_pixelFont != null) dialogueText.font = _pixelFont;
            }
        }
    }
}
