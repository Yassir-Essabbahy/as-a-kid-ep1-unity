using System.IO;
using AsAKid.Conversation;
using Story;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AsAKid.Conversation.Editor
{
    /// <summary>
    /// Editor utility to rebuild or regenerate Assets/Prefabs/UI/ConversationUI.prefab cleanly inside Unity.
    /// </summary>
    public static class ConversationUIPrefabBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/ConversationUI.prefab";

        [MenuItem("Tools/Rebuild Conversation UI Prefab", false, 50)]
        public static void RebuildPrefab()
        {
            Directory.CreateDirectory("Assets/Prefabs/UI");

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/GeistPixel-Regular-VariableFont_ELSH SDF");
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }

            GameObject root = new GameObject("ConversationUI");
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 25;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                root.AddComponent<GraphicRaycaster>();
                ConversationUI controller = root.AddComponent<ConversationUI>();

                // SafeAreaRoot
                GameObject safeRoot = CreateUIObject("SafeAreaRoot", root.transform);
                StretchFull(safeRoot.GetComponent<RectTransform>());
                SafeAreaHandler safeHandler = safeRoot.AddComponent<SafeAreaHandler>();
                safeHandler.applyHorizontal = true;
                safeHandler.applyVertical = true;

                // ConversationPanel
                GameObject panel = CreateUIObject("ConversationPanel", safeRoot.transform);
                RectTransform panelRt = panel.GetComponent<RectTransform>();
                panelRt.anchorMin = new Vector2(0.06f, 0.03f);
                panelRt.anchorMax = new Vector2(0.94f, 0.45f);
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;

                Image panelBg = panel.AddComponent<Image>();
                panelBg.color = new Color(0f, 0f, 0f, 0.92f);
                panelBg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                panelBg.type = Image.Type.Sliced;

                Outline panelOutline = panel.AddComponent<Outline>();
                panelOutline.effectColor = new Color(1f, 1f, 1f, 0.35f);
                panelOutline.effectDistance = new Vector2(2f, -2f);

                // HeaderBar
                GameObject header = CreateUIObject("HeaderBar", panel.transform);
                RectTransform headerRt = header.GetComponent<RectTransform>();
                headerRt.anchorMin = new Vector2(0f, 1f);
                headerRt.anchorMax = new Vector2(1f, 1f);
                headerRt.pivot = new Vector2(0.5f, 1f);
                headerRt.anchoredPosition = new Vector2(0f, -12f);
                headerRt.sizeDelta = new Vector2(-48f, 60f);

                GameObject nameGo = CreateUIObject("NpcNameText", header.transform);
                RectTransform nameRt = nameGo.GetComponent<RectTransform>();
                nameRt.anchorMin = new Vector2(0f, 0f);
                nameRt.anchorMax = new Vector2(0.72f, 1f);
                nameRt.offsetMin = Vector2.zero;
                nameRt.offsetMax = Vector2.zero;
                TextMeshProUGUI nameTmp = ConfigureTMP(nameGo.AddComponent<TextMeshProUGUI>(), font, "Neighbor", 34f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, false);

                GameObject leaveGo = CreateButton("LeaveButton", header.transform, font, "Leave", 26f, TextAlignmentOptions.Center, new Color(0.14f, 0.14f, 0.14f, 0.96f), new Color(1f, 1f, 1f, 0.55f), out Button leaveBtn, out TextMeshProUGUI leaveTmp);
                RectTransform leaveRt = leaveGo.GetComponent<RectTransform>();
                leaveRt.anchorMin = new Vector2(1f, 0.5f);
                leaveRt.anchorMax = new Vector2(1f, 0.5f);
                leaveRt.pivot = new Vector2(1f, 0.5f);
                leaveRt.anchoredPosition = Vector2.zero;
                leaveRt.sizeDelta = new Vector2(200f, 54f);

                // Divider
                GameObject divider = CreateUIObject("HeaderDivider", panel.transform);
                RectTransform divRt = divider.GetComponent<RectTransform>();
                divRt.anchorMin = new Vector2(0f, 1f);
                divRt.anchorMax = new Vector2(1f, 1f);
                divRt.pivot = new Vector2(0.5f, 1f);
                divRt.anchoredPosition = new Vector2(0f, -80f);
                divRt.sizeDelta = new Vector2(-48f, 2f);
                Image divImg = divider.AddComponent<Image>();
                divImg.color = new Color(1f, 1f, 1f, 0.2f);
                divImg.raycastTarget = false;

                // AnswerContainer
                GameObject answerContainer = CreateUIObject("AnswerContainer", panel.transform);
                RectTransform answerContRt = answerContainer.GetComponent<RectTransform>();
                answerContRt.anchorMin = new Vector2(0f, 0f);
                answerContRt.anchorMax = new Vector2(1f, 1f);
                answerContRt.anchoredPosition = new Vector2(0f, -45f);
                answerContRt.sizeDelta = new Vector2(-56f, -106f);

                GameObject answerGo = CreateUIObject("AnswerText", answerContainer.transform);
                RectTransform answerRt = answerGo.GetComponent<RectTransform>();
                answerRt.anchorMin = new Vector2(0f, 0.14f);
                answerRt.anchorMax = new Vector2(1f, 1f);
                answerRt.offsetMin = Vector2.zero;
                answerRt.offsetMax = Vector2.zero;
                TextMeshProUGUI answerTmp = ConfigureTMP(answerGo.AddComponent<TextMeshProUGUI>(), font, "", 30f, FontStyles.Normal, TextAlignmentOptions.TopLeft, false);

                GameObject hintGo = CreateUIObject("ContinueHintText", answerContainer.transform);
                RectTransform hintRt = hintGo.GetComponent<RectTransform>();
                hintRt.anchorMin = new Vector2(0.5f, 0f);
                hintRt.anchorMax = new Vector2(1f, 0.14f);
                hintRt.offsetMin = Vector2.zero;
                hintRt.offsetMax = Vector2.zero;
                TextMeshProUGUI hintTmp = ConfigureTMP(hintGo.AddComponent<TextMeshProUGUI>(), font, "Continue", 22f, FontStyles.Normal, TextAlignmentOptions.MidlineRight, false);
                hintTmp.color = new Color(0.78f, 0.78f, 0.78f, 0.9f);
                hintGo.SetActive(false);
                answerContainer.SetActive(false);

                // QuestionsContainer
                GameObject questionsContainer = CreateUIObject("QuestionsContainer", panel.transform);
                RectTransform qContRt = questionsContainer.GetComponent<RectTransform>();
                qContRt.anchorMin = new Vector2(0f, 0f);
                qContRt.anchorMax = new Vector2(1f, 1f);
                qContRt.anchoredPosition = new Vector2(0f, -45f);
                qContRt.sizeDelta = new Vector2(-56f, -106f);

                Button[] qButtons = new Button[NpcQuestionSet.QuestionCount];
                TextMeshProUGUI[] qLabels = new TextMeshProUGUI[NpcQuestionSet.QuestionCount];
                Vector2[] qAnchorsMin = { new Vector2(0f, 0.68f), new Vector2(0f, 0.35f), new Vector2(0f, 0.02f) };
                Vector2[] qAnchorsMax = { new Vector2(1f, 0.98f), new Vector2(1f, 0.65f), new Vector2(1f, 0.32f) };

                for (int i = 0; i < NpcQuestionSet.QuestionCount; i++)
                {
                    GameObject btnGo = CreateButton(
                        $"QuestionButton{i + 1}",
                        questionsContainer.transform,
                        font,
                        $"Question {i + 1}",
                        26f,
                        TextAlignmentOptions.MidlineLeft,
                        new Color(0.10f, 0.10f, 0.10f, 0.95f),
                        new Color(1f, 1f, 1f, 0.30f),
                        out qButtons[i],
                        out qLabels[i]);

                    RectTransform btnRt = btnGo.GetComponent<RectTransform>();
                    btnRt.anchorMin = qAnchorsMin[i];
                    btnRt.anchorMax = qAnchorsMax[i];
                    btnRt.offsetMin = Vector2.zero;
                    btnRt.offsetMax = Vector2.zero;

                    qLabels[i].enableAutoSizing = true;
                    qLabels[i].fontSizeMin = 18f;
                    qLabels[i].fontSizeMax = 28f;
                    RectTransform labelRt = qLabels[i].rectTransform;
                    labelRt.sizeDelta = new Vector2(-36f, -12f);
                }

                panel.SetActive(false);

                SerializedObject so = new SerializedObject(controller);
                so.FindProperty("panelRoot").objectReferenceValue = panel;
                so.FindProperty("npcNameText").objectReferenceValue = nameTmp;
                so.FindProperty("answerContainer").objectReferenceValue = answerContainer;
                so.FindProperty("answerText").objectReferenceValue = answerTmp;
                so.FindProperty("continueHintText").objectReferenceValue = hintTmp;
                so.FindProperty("questionsContainer").objectReferenceValue = questionsContainer;
                so.FindProperty("leaveButton").objectReferenceValue = leaveBtn;
                so.FindProperty("leaveButtonLabel").objectReferenceValue = leaveTmp;

                SerializedProperty btnsProp = so.FindProperty("questionButtons");
                btnsProp.arraySize = NpcQuestionSet.QuestionCount;
                SerializedProperty labelsProp = so.FindProperty("questionLabels");
                labelsProp.arraySize = NpcQuestionSet.QuestionCount;
                for (int i = 0; i < NpcQuestionSet.QuestionCount; i++)
                {
                    btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = qButtons[i];
                    labelsProp.GetArrayElementAtIndex(i).objectReferenceValue = qLabels[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[ConversationUIPrefabBuilder] Rebuilt prefab at {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI ConfigureTMP(
            TextMeshProUGUI tmp,
            TMP_FontAsset font,
            string text,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment,
            bool raycast)
        {
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = raycast;
            return tmp;
        }

        private static GameObject CreateButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string labelText,
            float fontSize,
            TextAlignmentOptions alignment,
            Color bgColor,
            Color outlineColor,
            out Button btn,
            out TextMeshProUGUI tmp)
        {
            GameObject go = CreateUIObject(name, parent);
            Image img = go.AddComponent<Image>();
            img.color = bgColor;
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            img.type = Image.Type.Sliced;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            btn = go.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            btn.colors = colors;

            GameObject labelGo = CreateUIObject("Label", go.transform);
            StretchFull(labelGo.GetComponent<RectTransform>());
            tmp = ConfigureTMP(labelGo.AddComponent<TextMeshProUGUI>(), font, labelText, fontSize, FontStyles.Normal, alignment, false);
            return go;
        }
    }
}
