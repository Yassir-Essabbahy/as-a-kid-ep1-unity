using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Story
{
    public static class MetroFootstepsAudioSetup
    {
        private static readonly string[] MetroClipPaths = new[]
        {
            "Assets/Assets/Episode 1/Sounds/Footsteps/Metro/Footstep01.wav",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Metro/Footstep02.wav",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Metro/Footstep03.wav",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Metro/Footstep04.wav"
        };

        private static readonly string[] ConcreteClipPaths = new[]
        {
            "Assets/Assets/Episode 1/Sounds/Footsteps/Concrete/Footstep Concrete 1.ogg",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Concrete/Footstep Concrete 2.ogg",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Concrete/Footstep Concrete 3.ogg",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Concrete/Footstep Concrete 4.ogg",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Concrete/Footstep Concrete 5.ogg"
        };

        private static readonly string[] SandClipPaths = new[]
        {
            "Assets/Assets/Episode 1/Sounds/Footsteps/Sand/Footstep Dirt 1.ogg",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Sand/Footstep Dirt 2.ogg",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Sand/Footstep Dirt 3.ogg",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Sand/Footstep Dirt 4.ogg",
            "Assets/Assets/Episode 1/Sounds/Footsteps/Sand/Footstep Dirt 5.ogg"
        };

        public static AudioClip[] LoadClips(string[] paths)
        {
            var list = new List<AudioClip>();
            foreach (var p in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip != null) list.Add(clip);
                else Debug.LogWarning($"[MetroFootstepsAudioSetup] Failed to load clip at: {p}");
            }
            return list.ToArray();
        }

        [MenuItem("Tools/Setup Metro Footstep Audio")]
        public static void SetupAll()
        {
            AudioClip[] metroClips = LoadClips(MetroClipPaths);
            AudioClip[] concreteClips = LoadClips(ConcreteClipPaths);
            AudioClip[] sandClips = LoadClips(SandClipPaths);

            Debug.Log($"[MetroFootstepsAudioSetup] Loaded {metroClips.Length} metro clips, {concreteClips.Length} concrete clips, {sandClips.Length} sand clips.");

            // 1. Update Prefab
            SetupPrefab(metroClips, concreteClips, sandClips);

            // 2. Update Scenes
            string[] metroScenePaths = new[]
            {
                "Assets/Scenes/Gameplay3.unity",
                "Assets/Scenes/Gameplay1.unity",
                "Assets/Scenes/Gameplay2.unity",
                "Assets/Scenes/Gameplay3 1.unity"
            };

            string activeScenePath = EditorSceneManager.GetActiveScene().path;

            foreach (var scenePath in metroScenePaths)
            {
                SetupScene(scenePath, metroClips, concreteClips, sandClips);
            }

            // Restore active scene if needed
            if (!string.IsNullOrEmpty(activeScenePath) && EditorSceneManager.GetActiveScene().path != activeScenePath)
            {
                EditorSceneManager.OpenScene(activeScenePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[MetroFootstepsAudioSetup] Completed all footstep setups!");
        }

        private static void SetupPrefab(AudioClip[] metro, AudioClip[] concrete, AudioClip[] sand)
        {
            string prefabPath = "Assets/ModularFirstPersonController/FirstPersonController/FirstPersonController.prefab";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot != null)
            {
                try
                {
                    var footsteps = prefabRoot.GetComponent<PlayerFootsteps>();
                    if (footsteps == null) footsteps = prefabRoot.AddComponent<PlayerFootsteps>();

                    footsteps.surfaceType = PlayerFootsteps.SurfaceType.Auto;
                    footsteps.metroClips = metro;
                    footsteps.concreteClips = concrete;
                    footsteps.sandClips = sand;

                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    Debug.Log($"[MetroFootstepsAudioSetup] Successfully updated prefab: {prefabPath}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
            else
            {
                Debug.LogError($"[MetroFootstepsAudioSetup] Could not load prefab: {prefabPath}");
            }
        }

        private static void SetupScene(string scenePath, AudioClip[] metro, AudioClip[] concrete, AudioClip[] sand)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[MetroFootstepsAudioSetup] Could not open scene: {scenePath}");
                return;
            }

            var allFootsteps = Object.FindObjectsByType<PlayerFootsteps>(FindObjectsInactive.Include);
            if (allFootsteps.Length == 0)
            {
                var fpc = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include);
                foreach (var player in fpc)
                {
                    var comp = player.gameObject.AddComponent<PlayerFootsteps>();
                    ConfigureFootstepsComponent(comp, metro, concrete, sand);
                    EditorUtility.SetDirty(player.gameObject);
                }
            }
            else
            {
                foreach (var footstep in allFootsteps)
                {
                    ConfigureFootstepsComponent(footstep, metro, concrete, sand);
                    EditorUtility.SetDirty(footstep);
                    EditorUtility.SetDirty(footstep.gameObject);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MetroFootstepsAudioSetup] Successfully updated footsteps in scene: {scenePath}");
        }

        private static void ConfigureFootstepsComponent(PlayerFootsteps comp, AudioClip[] metro, AudioClip[] concrete, AudioClip[] sand)
        {
            comp.surfaceType = PlayerFootsteps.SurfaceType.Metro;
            comp.metroClips = metro;
            comp.concreteClips = concrete;
            comp.sandClips = sand;
            comp.walkStepInterval = 0.50f;
            comp.sprintStepInterval = 0.30f;
            comp.crouchStepInterval = 0.65f;
            comp.minVelocityThreshold = 0.25f;
            comp.walkVolume = 0.65f;
            comp.sprintVolume = 0.85f;
            comp.crouchVolume = 0.40f;
            comp.landVolume = 0.75f;
        }
    }
}
