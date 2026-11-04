using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Story
{
    public static class MetroAudioAndTrainSetup
    {
        private const string RailwayPath = "Assets/Sounds/Train/Railway.mp3";
        private const string HornPath = "Assets/Sounds/Train/TrainHorns.wav";
        private const string AmbiancePath = "Assets/Assets/Episode 1/Sounds/OncfAmbiance.MP3";
        private const string Oncf1Path = "Assets/Assets/Episode 1/Sounds/Oncf1.MP3";
        private const string Oncf2Path = "Assets/Assets/Episode 1/Sounds/Oncf2.MP3";

        [MenuItem("Tools/Setup Metro Train & Sound System")]
        public static void SetupAll()
        {
            AudioClip railwayClip = AssetDatabase.LoadAssetAtPath<AudioClip>(RailwayPath);
            AudioClip hornClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HornPath);
            AudioClip ambianceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbiancePath);
            AudioClip oncf1Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Oncf1Path);
            AudioClip oncf2Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Oncf2Path);

            var announcementClips = new List<AudioClip>();
            if (oncf1Clip != null) announcementClips.Add(oncf1Clip);
            if (oncf2Clip != null) announcementClips.Add(oncf2Clip);

            string[] scenes = new[]
            {
                "Assets/Scenes/Gameplay3.unity",
                "Assets/Scenes/Gameplay1.unity",
                "Assets/Scenes/Gameplay2.unity",
                "Assets/Scenes/Gameplay3 1.unity"
            };

            string activeScenePath = EditorSceneManager.GetActiveScene().path;

            foreach (var sp in scenes)
            {
                SetupScene(sp, railwayClip, hornClip, ambianceClip, announcementClips.ToArray());
            }

            if (!string.IsNullOrEmpty(activeScenePath) && EditorSceneManager.GetActiveScene().path != activeScenePath)
            {
                EditorSceneManager.OpenScene(activeScenePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[MetroAudioAndTrainSetup] Completed all train and sound system setups!");
        }

        public static void SetupScene(string scenePath, AudioClip railway, AudioClip horn, AudioClip ambiance, AudioClip[] announcements)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[MetroAudioAndTrainSetup] Could not open scene: {scenePath}");
                return;
            }

            // 1. Configure TrainArrivalSequence
            var trains = Object.FindObjectsByType<TrainArrivalSequence>(FindObjectsInactive.Include);
            foreach (var t in trains)
            {
                t.moveSound = railway;
                t.hornSound = horn;
                t.delayBeforeMove = 3.5f;
                t.speed = 20f;
                t.loopPasses = true;
                t.minIntervalBetweenPasses = 45f;
                t.maxIntervalBetweenPasses = 75f;
                t.moveVolume = 0.85f;
                t.hornVolume = 0.90f;
                t.hornTriggerDistance = 80f;

                // Ensure kinematic Rigidbody to eliminate physics hierarchy recalculation stalls
                var rb = t.GetComponent<Rigidbody>();
                if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                // Ensure primary AudioSource
                if (t.audioSource == null)
                {
                    t.audioSource = t.GetComponent<AudioSource>();
                    if (t.audioSource == null) t.audioSource = t.gameObject.AddComponent<AudioSource>();
                }
                t.audioSource.playOnAwake = false;
                t.audioSource.spatialBlend = 1.0f;
                t.audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                t.audioSource.minDistance = 15f;
                t.audioSource.maxDistance = 150f;
                t.audioSource.dopplerLevel = 0.8f;

                // Ensure secondary AudioSource for horn
                var allSources = t.GetComponents<AudioSource>();
                if (allSources.Length > 1)
                {
                    t.hornAudioSource = allSources[1];
                }
                else
                {
                    t.hornAudioSource = t.gameObject.AddComponent<AudioSource>();
                }
                t.hornAudioSource.playOnAwake = false;
                t.hornAudioSource.spatialBlend = 1.0f;
                t.hornAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                t.hornAudioSource.minDistance = 25f;
                t.hornAudioSource.maxDistance = 200f;

                EditorUtility.SetDirty(t);
                EditorUtility.SetDirty(t.gameObject);
            }

            // 2. Configure MetroSoundSystem
            var soundSystem = Object.FindAnyObjectByType<MetroSoundSystem>();
            if (soundSystem == null)
            {
                var ssObj = new GameObject("MetroSoundSystem");
                soundSystem = ssObj.AddComponent<MetroSoundSystem>();
            }

            soundSystem.stationAmbiance = ambiance;
            soundSystem.ambianceVolume = 0.35f;
            soundSystem.stationAnnouncements = announcements;
            soundSystem.announcementVolume = 0.65f;
            soundSystem.enablePeriodicAnnouncements = true;
            soundSystem.minAnnouncementInterval = 75f;
            soundSystem.maxAnnouncementInterval = 130f;
            soundSystem.announceOnTrainApproach = true;

            // Link existing AmbianceAudioSource if present in scene
            var ambObj = GameObject.Find("AmbianceAudioSource");
            if (ambObj != null)
            {
                var ambSource = ambObj.GetComponent<AudioSource>();
                if (ambSource != null)
                {
                    ambSource.clip = ambiance;
                    ambSource.loop = true;
                    ambSource.volume = 0.35f;
                    ambSource.playOnAwake = true;
                    ambSource.spatialBlend = 0f;
                    soundSystem.ambianceSource = ambSource;
                    EditorUtility.SetDirty(ambSource);
                    EditorUtility.SetDirty(ambObj);
                }
            }

            EditorUtility.SetDirty(soundSystem);
            EditorUtility.SetDirty(soundSystem.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MetroAudioAndTrainSetup] Successfully updated '{scene.name}'.");
        }
    }
}
