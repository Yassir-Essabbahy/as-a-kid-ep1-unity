using System.Collections;
using UnityEngine;

namespace Credits
{
    /// <summary>
    /// Persistent audio controller that manages CreditsMusic across the transition
    /// from Ending.unity (playing at a subtle down volume) to Credits.unity (ramping up to full volume).
    /// </summary>
    public class CreditsMusicController : MonoBehaviour
    {
        public static CreditsMusicController Instance { get; private set; }

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip creditsMusicClip;

        [Header("Volumes")]
        public float endingVolume = 0.22f;
        public float creditsVolume = 0.85f;

        private Coroutine _volumeFadeCoroutine;

        public static CreditsMusicController EnsureInstance(AudioClip clip = null, float targetVolume = 0.85f)
        {
            if (Instance != null) return Instance;

            GameObject obj = new GameObject("PersistentCreditsMusic");
            CreditsMusicController ctrl = obj.AddComponent<CreditsMusicController>();
            ctrl.InitializeAudioSource();
            if (clip != null) ctrl.creditsMusicClip = clip;
            ctrl.creditsVolume = targetVolume;
            Instance = ctrl;
            return ctrl;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            InitializeAudioSource();
        }

        private void InitializeAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D stereo sound
            audioSource.ignoreListenerVolume = true;
            audioSource.ignoreListenerPause = true;
        }

        /// <summary>
        /// Starts playing the credits music at down volume for the Ending classroom scene.
        /// </summary>
        public static void PlayAtEndingVolume(AudioClip clip, float volume = 0.22f, float fadeInDuration = 2.0f)
        {
            var ctrl = EnsureInstance(clip, 0.85f);
            ctrl.endingVolume = volume;
            ctrl.StartEndingMusic(clip, volume, fadeInDuration);
        }

        private void StartEndingMusic(AudioClip clip, float volume, float fadeInDuration)
        {
            if (clip != null) creditsMusicClip = clip;
            if (audioSource == null) return;

            audioSource.clip = creditsMusicClip;
            audioSource.volume = 0f;
            audioSource.Play();

            if (_volumeFadeCoroutine != null) StopCoroutine(_volumeFadeCoroutine);
            _volumeFadeCoroutine = StartCoroutine(FadeVolumeRoutine(0f, volume, fadeInDuration));
        }

        /// <summary>
        /// Smoothly ramps volume up from the ending down-volume to the full credits volume.
        /// </summary>
        public void RampUpToCreditsVolume(float targetVolume = 0.85f, float duration = 2.5f)
        {
            creditsVolume = targetVolume;
            if (audioSource == null) return;

            if (!audioSource.isPlaying)
            {
                if (creditsMusicClip != null) audioSource.clip = creditsMusicClip;
                audioSource.volume = 0f;
                audioSource.Play();
            }

            if (_volumeFadeCoroutine != null) StopCoroutine(_volumeFadeCoroutine);
            _volumeFadeCoroutine = StartCoroutine(FadeVolumeRoutine(audioSource.volume, creditsVolume, duration));
        }

        /// <summary>
        /// Fades audio to zero and destroys the persistent GameObject (used when loading MainMenu).
        /// </summary>
        public void FadeOutAndDestroy(float duration = 1.2f)
        {
            if (_volumeFadeCoroutine != null) StopCoroutine(_volumeFadeCoroutine);
            StartCoroutine(FadeOutAndDestroyRoutine(duration));
        }

        private IEnumerator FadeVolumeRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            audioSource.volume = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            audioSource.volume = to;
            _volumeFadeCoroutine = null;
        }

        private IEnumerator FadeOutAndDestroyRoutine(float duration)
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                float startVol = audioSource.volume;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    audioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                    yield return null;
                }
                audioSource.volume = 0f;
                audioSource.Stop();
            }

            Instance = null;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
