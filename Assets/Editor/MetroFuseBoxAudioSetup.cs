using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Story
{
    public static class MetroFuseBoxAudioSetup
    {
        [MenuItem("Tools/Setup Fuse Box Audio Pack")]
        public static void SetupFuseBoxAudio()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            bool configured = ConfigureFuseBoxInScene(activeScene);
            if (configured)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[MetroFuseBoxAudioSetup] Successfully wired Audio Pack in '{activeScene.name}'.");
            }
            else
            {
                Debug.LogWarning($"[MetroFuseBoxAudioSetup] No MetroFuseBoxPuzzle found in '{activeScene.name}'.");
            }
        }

        public static bool ConfigureFuseBoxInScene(Scene scene)
        {
            var fb = Object.FindAnyObjectByType<MetroFuseBoxPuzzle>();
            if (fb == null) return false;

            // Load optimized audio assets
            AudioClip hum = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Episode 1/Sounds/ElectricityHum.mp3");
            AudioClip fuseUp = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Episode 1/Sounds/FuseUp.wav");
            AudioClip fuseDown = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Episode 1/Sounds/FuseDown.wav");
            AudioClip doorClose = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Episode 1/Sounds/Fusebox_Close.mp3");

            // Prefer trimmed open clip if present to avoid 1.68s dead silence delay
            AudioClip doorOpen = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Episode 1/Sounds/FuseBox_Open_Trimmed.wav");
            if (doorOpen == null)
            {
                doorOpen = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Episode 1/Sounds/FuseBox_Open.mp3");
            }

            fb.electricityHumClip = hum;
            fb.fuseUpClip = fuseUp;
            fb.fuseDownClip = fuseDown;
            fb.doorOpenClip = doorOpen;
            fb.doorCloseClip = doorClose;

            // Volumes
            fb.electricityHumVolume = 0.55f;
            fb.switchVolume = 1.0f;
            fb.doorOpenVolume = 0.85f;
            fb.doorCloseVolume = 0.85f;

            // Ensure AudioSource is clean and configured for 3D spatialization
            var a = fb.GetComponent<AudioSource>();
            if (a == null) a = fb.gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0.85f;
            a.minDistance = 1.5f;
            a.maxDistance = 15.0f;
            a.rolloffMode = AudioRolloffMode.Linear;
            fb.audioSource = a;

            EditorUtility.SetDirty(a);
            EditorUtility.SetDirty(fb);
            EditorUtility.SetDirty(fb.gameObject);

            return true;
        }
    }
}
