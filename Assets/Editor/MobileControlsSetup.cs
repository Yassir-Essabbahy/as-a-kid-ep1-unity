using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ModularFirstPersonController;

namespace Story
{
    public static class MobileControlsSetup
    {
        private const string PrefabPath = "Assets/ModularFirstPersonController/FirstPersonController/FirstPersonController.prefab";
        private const string BaseSpritePath = "Assets/ModularFirstPersonController/FirstPersonController/JoystickBase.png";
        private const string KnobSpritePath = "Assets/ModularFirstPersonController/FirstPersonController/JoystickKnob.png";

        [MenuItem("Tools/Setup Mobile Touch Controls")]
        public static void SetupAll()
        {
            Sprite baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BaseSpritePath);
            Sprite knobSprite = AssetDatabase.LoadAssetAtPath<Sprite>(KnobSpritePath);

            if (baseSprite == null || knobSprite == null)
            {
                Debug.LogError($"[MobileControlsSetup] Sprites not found at '{BaseSpritePath}' or '{KnobSpritePath}'.");
                return;
            }

            // 1. Update FirstPersonController Prefab
            SetupPrefab(baseSprite, knobSprite);

            // 2. Update Active Build Scenes
            string[] scenePaths = new[]
            {
                "Assets/Scenes/House_Export.unity",
                "Assets/Scenes/Start.unity",
                "Assets/Scenes/Story.unity",
                "Assets/Scenes/MoroccanBeach.unity",
                "Assets/Scenes/Ending.unity",
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Credits.unity"
            };

            string activeScenePath = EditorSceneManager.GetActiveScene().path;

            foreach (var sp in scenePaths)
            {
                SetupScene(sp, baseSprite, knobSprite);
            }

            if (!string.IsNullOrEmpty(activeScenePath) && EditorSceneManager.GetActiveScene().path != activeScenePath)
            {
                EditorSceneManager.OpenScene(activeScenePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[MobileControlsSetup] Completed setup across prefab and scenes!");
        }

        public static void SetupPrefab(Sprite baseSprite, Sprite knobSprite)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[MobileControlsSetup] Could not load prefab: {PrefabPath}");
                return;
            }

            try
            {
                EnsureMobileControlsOnPlayer(prefabRoot, baseSprite, knobSprite);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Debug.Log($"[MobileControlsSetup] Successfully updated prefab: {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static void SetupScene(string scenePath, Sprite baseSprite, Sprite knobSprite)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[MobileControlsSetup] Could not open scene: {scenePath}");
                return;
            }

            var fpcList = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include);
            foreach (var fpc in fpcList)
            {
                EnsureMobileControlsOnPlayer(fpc.gameObject, baseSprite, knobSprite);
                EditorUtility.SetDirty(fpc.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MobileControlsSetup] Successfully updated scene: {scenePath}");
        }

        public static void EnsureMobileControlsOnPlayer(GameObject playerObj, Sprite baseSprite, Sprite knobSprite)
        {
            var fpc = playerObj.GetComponent<FirstPersonController>();
            if (fpc == null) return;

            Transform existingCanvasTransform = playerObj.transform.Find("MobileControlsCanvas");
            GameObject canvasObj;
            if (existingCanvasTransform != null)
            {
                canvasObj = existingCanvasTransform.gameObject;
            }
            else
            {
                canvasObj = new GameObject("MobileControlsCanvas");
                canvasObj.transform.SetParent(playerObj.transform, false);
            }

            // Canvas
            var canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // Above world, under HUD/PromptBox/Dialogue/PauseMenu

            // Canvas Scaler
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Graphic Raycaster
            var raycaster = canvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvasObj.AddComponent<GraphicRaycaster>();

            // Canvas Group
            var cg = canvasObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = canvasObj.AddComponent<CanvasGroup>();

            // MobileTouchControls component
            var controls = canvasObj.GetComponent<MobileTouchControls>();
            if (controls == null) controls = canvasObj.AddComponent<MobileTouchControls>();
            controls.controller = fpc;
            controls.forceShowInEditor = true;
            controls.dynamicJoystick = true;
            controls.maxJoystickRadius = 105f;
            controls.idleAlpha = 0.48f;
            controls.activeAlpha = 0.88f;
            controls.lookSensitivityX = 0.18f;
            controls.lookSensitivityY = 0.14f;

            // 1. Left Touch Zone (Movement Area)
            Transform leftTransform = canvasObj.transform.Find("LeftTouchZone");
            GameObject leftObj = leftTransform != null ? leftTransform.gameObject : new GameObject("LeftTouchZone");
            leftObj.transform.SetParent(canvasObj.transform, false);

            var leftRect = leftObj.GetComponent<RectTransform>();
            if (leftRect == null) leftRect = leftObj.AddComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.5f, 1f);
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = Vector2.zero;

            var leftImage = leftObj.GetComponent<Image>();
            if (leftImage == null) leftImage = leftObj.AddComponent<Image>();
            leftImage.color = new Color(0f, 0f, 0f, 0f); // Hidden / transparent
            leftImage.raycastTarget = true;

            var joystickZone = leftObj.GetComponent<MobileJoystickZone>();
            if (joystickZone == null) joystickZone = leftObj.AddComponent<MobileJoystickZone>();
            joystickZone.controls = controls;

            // Joystick Base (within Left Touch Zone)
            Transform baseTransform = leftObj.transform.Find("JoystickBase");
            GameObject baseObj = baseTransform != null ? baseTransform.gameObject : new GameObject("JoystickBase");
            baseObj.transform.SetParent(leftObj.transform, false);

            var baseRect = baseObj.GetComponent<RectTransform>();
            if (baseRect == null) baseRect = baseObj.AddComponent<RectTransform>();
            baseRect.anchorMin = new Vector2(0f, 0f);
            baseRect.anchorMax = new Vector2(0f, 0f);
            baseRect.pivot = new Vector2(0.5f, 0.5f);
            baseRect.anchoredPosition = new Vector2(280f, 240f);
            baseRect.sizeDelta = new Vector2(260f, 260f);

            var baseImage = baseObj.GetComponent<Image>();
            if (baseImage == null) baseImage = baseObj.AddComponent<Image>();
            baseImage.sprite = baseSprite;
            baseImage.color = new Color(1f, 1f, 1f, 0.48f); // Visible semi-transparent ring
            baseImage.raycastTarget = false;

            var baseCg = baseObj.GetComponent<CanvasGroup>();
            if (baseCg == null) baseCg = baseObj.AddComponent<CanvasGroup>();
            baseCg.blocksRaycasts = false;
            baseCg.interactable = false;

            // Joystick Handle (within Joystick Base)
            Transform handleTransform = baseObj.transform.Find("JoystickHandle");
            GameObject handleObj = handleTransform != null ? handleTransform.gameObject : new GameObject("JoystickHandle");
            handleObj.transform.SetParent(baseObj.transform, false);

            var handleRect = handleObj.GetComponent<RectTransform>();
            if (handleRect == null) handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(114f, 114f);

            var handleImage = handleObj.GetComponent<Image>();
            if (handleImage == null) handleImage = handleObj.AddComponent<Image>();
            handleImage.sprite = knobSprite;
            handleImage.color = new Color(1f, 1f, 1f, 0.65f); // Translucent knob
            handleImage.raycastTarget = false;

            // Wire Left Joystick References
            controls.leftTouchArea = leftRect;
            controls.joystickBase = baseRect;
            controls.joystickHandle = handleRect;
            controls.joystickCanvasGroup = baseCg;

            // 2. Right Look Zone (Hidden Camera Look Area)
            Transform rightTransform = canvasObj.transform.Find("RightLookZone");
            GameObject rightObj = rightTransform != null ? rightTransform.gameObject : new GameObject("RightLookZone");
            rightObj.transform.SetParent(canvasObj.transform, false);

            var rightRect = rightObj.GetComponent<RectTransform>();
            if (rightRect == null) rightRect = rightObj.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.5f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.offsetMin = Vector2.zero;
            rightRect.offsetMax = Vector2.zero;

            var rightImage = rightObj.GetComponent<Image>();
            if (rightImage == null) rightImage = rightObj.AddComponent<Image>();
            rightImage.color = new Color(0f, 0f, 0f, 0f); // Hidden / completely invisible
            rightImage.raycastTarget = true;

            var lookZone = rightObj.GetComponent<MobileLookZone>();
            if (lookZone == null) lookZone = rightObj.AddComponent<MobileLookZone>();
            lookZone.controls = controls;

            controls.rightLookArea = rightRect;

            EditorUtility.SetDirty(controls);
            EditorUtility.SetDirty(canvasObj);
        }
    }
}
