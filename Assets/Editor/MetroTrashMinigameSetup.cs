using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Story
{
    public static class MetroTrashMinigameSetup
    {
        [MenuItem("Tools/Setup Metro Trash Minigame in Active Scene")]
        public static void SetupActiveScene()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            int cansConfigured = ConfigureScene(activeScene);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[MetroTrashMinigameSetup] Completed setup in '{activeScene.name}'. Cans configured: {cansConfigured}");
        }

        [MenuItem("Tools/Setup Metro Trash Minigame in All Metro Scenes")]
        public static void SetupAllScenes()
        {
            string[] scenePaths = new string[]
            {
                "Assets/Scenes/Story.unity",
                "Assets/Scenes/Gameplay3.unity",
                "Assets/Scenes/Gameplay1.unity",
                "Assets/Scenes/Gameplay2.unity",
                "Assets/Scenes/Gameplay3 1.unity",
                "Assets/Scenes/TestScene 1.unity"
            };

            string originalScene = EditorSceneManager.GetActiveScene().path;

            foreach (string path in scenePaths)
            {
                if (!System.IO.File.Exists(path)) continue;

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int cans = ConfigureScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[MetroTrashMinigameSetup] Saved {scene.name} ({cans} cans wired).");
            }

            if (!string.IsNullOrEmpty(originalScene) && System.IO.File.Exists(originalScene))
            {
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }
        }

        public static int ConfigureScene(Scene scene)
        {
            // Always prefer the main UI Canvas named "Canvas"
            Canvas canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
            if (canvas == null)
            {
                var allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var c in allCanvases)
                {
                    if (c.name == "Canvas")
                    {
                        canvas = c;
                        break;
                    }
                }
            }

            if (canvas == null)
            {
                canvas = Object.FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                Debug.LogWarning($"[MetroTrashMinigameSetup] No Canvas found in {scene.name}.");
                return 0;
            }

            // Remove unwanted duplicate copies under CrosshairAndStamina if present
            var crosshairCanvas = GameObject.Find("CrosshairAndStamina");
            if (crosshairCanvas != null)
            {
                var dupToast = crosshairCanvas.transform.Find("ItemPickupToast");
                if (dupToast != null) Object.DestroyImmediate(dupToast.gameObject);

                var dupMinigame = crosshairCanvas.transform.Find("TimingMinigameUI");
                if (dupMinigame != null) Object.DestroyImmediate(dupMinigame.gameObject);
            }

            AudioClip searchSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Episode 1/Sounds/SearchingSoundeffect.mp3");
            TMP_FontAsset pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/GeistPixel-Regular-VariableFont_ELSH SDF.asset");
            Sprite ticketSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Assets/Episode 1/Interactables/Textures/Train_Ticket.png");

            // 1. Setup ItemPickupToast
            var toastTransform = canvas.transform.Find("ItemPickupToast");
            if (toastTransform != null)
            {
                for (int i = toastTransform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(toastTransform.GetChild(i).gameObject);
                }
            }
            GameObject toastGo = toastTransform != null ? toastTransform.gameObject : new GameObject("ItemPickupToast", typeof(RectTransform));
            if (toastTransform == null) toastGo.transform.SetParent(canvas.transform, false);

            var toastComp = toastGo.GetComponent<ItemPickupToast>();
            if (toastComp == null) toastComp = toastGo.AddComponent<ItemPickupToast>();
            toastComp.BuildDefaultToastUI();
            EditorUtility.SetDirty(toastComp);
            EditorUtility.SetDirty(toastGo);

            // 2. Setup TimingMinigameUI
            var minigameTransform = canvas.transform.Find("TimingMinigameUI");
            if (minigameTransform != null)
            {
                for (int i = minigameTransform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(minigameTransform.GetChild(i).gameObject);
                }
            }
            GameObject minigameGo = minigameTransform != null ? minigameTransform.gameObject : new GameObject("TimingMinigameUI", typeof(RectTransform));
            if (minigameTransform == null) minigameGo.transform.SetParent(canvas.transform, false);

            var minigameComp = minigameGo.GetComponent<TimingMinigameUI>();
            if (minigameComp == null) minigameComp = minigameGo.AddComponent<TimingMinigameUI>();
            minigameComp.rummagingLoopClip = searchSound;
            minigameComp.BuildDefaultMinigameUI();
            if (pixelFont != null)
            {
                if (minigameComp.titleText != null) minigameComp.titleText.font = pixelFont;
                if (minigameComp.hintText != null) minigameComp.hintText.font = pixelFont;
            }
            EditorUtility.SetDirty(minigameComp);
            EditorUtility.SetDirty(minigameGo);

            // 3. TeddyFindQuest gating
            var tfqGo = GameObject.Find("TeddyFindQuest_ToyHunt");
            TeddyFindQuest tfq = tfqGo != null ? tfqGo.GetComponent<TeddyFindQuest>() : null;
            if (tfq != null)
            {
                tfq.useTrashMinigame = true;
                EditorUtility.SetDirty(tfq);
            }

            DialogueTriggerZone dtz = tfqGo != null ? tfqGo.GetComponent<DialogueTriggerZone>() : null;
            if (dtz != null)
            {
                dtz.useTrashMinigame = true;
                EditorUtility.SetDirty(dtz);
            }

            // 4. Configure Trash Cans
            string[] canPaths = new string[] {
                "Floor1/Metro/Structure/P6/trash_can",
                "Floor1/Metro/Structure/P5/trash_can",
                "Floor1/Metro/Structure/P4/trash_can",
                "Floor1/Metro/Structure/P3/trash_can",
                "Floor1/Metro/Structure/P2/trash_can"
            };

            int count = 0;
            foreach (string p in canPaths)
            {
                GameObject can = GameObject.Find(p);
                if (can != null)
                {
                    var existingBox = can.GetComponent<BoxCollider>();
                    if (existingBox != null)
                    {
                        Object.DestroyImmediate(existingBox);
                    }

                    var mc = can.GetComponent<MeshCollider>();
                    if (mc == null)
                    {
                        mc = can.AddComponent<MeshCollider>();
                    }

                    var src = can.GetComponent<TrashTicketSource>();
                    if (src == null)
                    {
                        src = can.AddComponent<TrashTicketSource>();
                    }

                    src.questGiver = tfq;
                    src.requireQuestActive = false;
                    src.ticketIcon = ticketSprite;
                    src.minigameUI = minigameComp;
                    src.interactDistance = 2.8f;
                    src.hasTicket = false;
                    src.isSearched = false;
                    EditorUtility.SetDirty(src);
                    EditorUtility.SetDirty(can);
                    count++;
                }
            }

            return count;
        }
    }
}
