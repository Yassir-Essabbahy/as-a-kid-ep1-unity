using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Credits
{
    [System.Serializable]
    public class CreditSlide
    {
        [TextArea(1, 3)] public string role;
        [TextArea(2, 5)] public string name;
    }

    /// <summary>
    /// Master controller for the End-Credits cutscene sequence.
    /// Orchestrates Cinemachine cameras, train wipe cut, raft drift,
    /// audio synchronization, letterbox presentation, and Main Menu handoff.
    /// </summary>
    public class CreditsSequenceController : MonoBehaviour
    {
        [Header("Cinemachine Cameras")]
        public CinemachineBrain cinemachineBrain;
        public CinemachineCamera vcamMetro;
        public CinemachineCamera vcamBeachTeddy;
        public CinemachineCamera vcamClassroom;
        public CinemachineCamera vcamRaftOcean;

        [Header("Vignette Lighting Roots")]
        public GameObject metroLightsRoot;
        public GameObject beachLightsRoot;
        public GameObject classroomLightsRoot;
        public GameObject oceanLightsRoot;

        [Header("Interactive Actors")]
        public CreditsTrainMover trainMover;
        public CreditsRaftBobber raftBobber;

        [Header("Audio")]
        public AudioSource musicSource;
        public AudioClip creditsMusic;
        public float musicVolume = 0.85f;
        public float musicFadeInDuration = 2.0f;

        [Header("Letterbox & Screen Fader")]
        public CanvasGroup letterboxBands;
        public CanvasGroup screenFaderGroup;
        public float sceneFadeInDuration = 1.5f;

        [Header("Credits Text UI")]
        public CanvasGroup creditTextGroup;
        public TextMeshProUGUI creditRoleText;
        public TextMeshProUGUI creditNameText;
        public TextMeshProUGUI returnPromptText;

        [Header("Timing (Seconds)")]
        public float shot1Duration = 6.5f;
        public float trainTriggerTime = 2.8f;
        public float shot2Duration = 7.0f;
        public float shot3Duration = 7.0f;
        public float shot4Duration = 8.5f;
        public float fadeToBlackDuration = 2.0f;

        [Header("Credit Slides Content")]
        public List<CreditSlide> slides = new List<CreditSlide>()
        {
            new CreditSlide { role = "EPISODE 1", name = "AS A KID" },
            new CreditSlide { role = "STORY & CREATIVE DIRECTION\nENVIRONMENT & LEVEL DESIGN", name = "Yassir Essabbahy" },
            new CreditSlide { role = "GAMEPLAY SYSTEMS & CINEMATICS\nSOUND DESIGN & AUDIO", name = "Core Team" },
            new CreditSlide { role = "SPECIAL THANKS", name = "To everyone who supported this journey.\n\nThank you for playing." }
        };

        private bool _canReturnToMenu = false;
        private bool _isReturning = false;
        private bool _trainWipeTriggered = false;

        private void Start()
        {
            StartCoroutine(RunCreditsCutscene());
        }

        private void Update()
        {
            if (_isReturning) return;

            // Once credits end, any key or mouse click returns to Main Menu
            if (_canReturnToMenu)
            {
                if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
                {
                    ReturnToMainMenu();
                }
            }
        }

        private IEnumerator RunCreditsCutscene()
        {
            // Initial State: Screen full black, bands visible
            if (screenFaderGroup != null)
            {
                screenFaderGroup.alpha = 1f;
                screenFaderGroup.blocksRaycasts = true;
            }
            if (letterboxBands != null)
            {
                letterboxBands.alpha = 1f;
            }
            if (creditTextGroup != null)
            {
                creditTextGroup.alpha = 0f;
            }
            if (returnPromptText != null)
            {
                returnPromptText.gameObject.SetActive(false);
            }

            // Start with only Metro lighting active
            SetActiveLighting(0);

            // Start / Ramp-up Credits Audio
            AudioListener.volume = 1f;
            if (CreditsMusicController.Instance != null && CreditsMusicController.Instance.audioSource != null)
            {
                // Already playing seamlessly from Ending scene! Ramp volume up to full credits volume
                musicSource = CreditsMusicController.Instance.audioSource;
                CreditsMusicController.Instance.RampUpToCreditsVolume(musicVolume, musicFadeInDuration);
            }
            else if (creditsMusic != null)
            {
                // Direct scene launch fallback
                var musicCtrl = CreditsMusicController.EnsureInstance(creditsMusic, musicVolume);
                musicSource = musicCtrl.audioSource;
                musicCtrl.RampUpToCreditsVolume(musicVolume, musicFadeInDuration);
            }
            else if (musicSource != null)
            {
                musicSource.loop = true;
                musicSource.volume = 0f;
                musicSource.Play();
                StartCoroutine(FadeAudioVolume(musicSource, musicVolume, musicFadeInDuration));
            }

            // Hook up train wipe event
            if (trainMover != null)
            {
                trainMover.OnWipePointReached += HandleTrainWipe;
            }

            // -------------------------------------------------------------
            // SHOT 1: Metro Platform & Moving Train Wipe Cut
            // -------------------------------------------------------------
            SetActiveCamera(vcamMetro, CinemachineBlendDefinition.Styles.Cut, 0f);
            DisplaySlide(0);

            // Fade in from black to reveal Metro
            StartCoroutine(FadeCanvasGroup(screenFaderGroup, 1f, 0f, sceneFadeInDuration));
            StartCoroutine(FadeCanvasGroup(creditTextGroup, 0f, 1f, 1.2f));

            // Schedule train departure
            yield return new WaitForSeconds(trainTriggerTime);
            if (trainMover != null)
            {
                trainMover.StartMoving();
            }

            // Wait until train wipe occurs or timeout
            float waitTimer = 0f;
            float maxWait = shot1Duration - trainTriggerTime;
            while (!_trainWipeTriggered && waitTimer < maxWait)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }

            // Fade out Slide 1 text
            yield return StartCoroutine(FadeCanvasGroup(creditTextGroup, 1f, 0f, 0.5f));

            // If train wipe didn't trigger, perform smooth dip-to-black safeguard
            if (!_trainWipeTriggered)
            {
                yield return StartCoroutine(FadeCanvasGroup(screenFaderGroup, 0f, 1f, 0.35f));
                SetActiveCamera(vcamBeachTeddy, CinemachineBlendDefinition.Styles.Cut, 0f);
                SetActiveLighting(1);
                yield return StartCoroutine(FadeCanvasGroup(screenFaderGroup, 1f, 0f, 0.45f));
            }
            else
            {
                SetActiveLighting(1);
            }

            // -------------------------------------------------------------
            // SHOT 2: Moroccan Beach - Table with Lone Teddy & Ocean
            // -------------------------------------------------------------
            DisplaySlide(1);
            StartCoroutine(FadeCanvasGroup(creditTextGroup, 0f, 1f, 1.0f));

            yield return new WaitForSeconds(shot2Duration - 1.2f);
            yield return StartCoroutine(FadeCanvasGroup(creditTextGroup, 1f, 0f, 0.5f));

            // Smooth film cross-fade to black (No flying camera through skybox void!)
            yield return StartCoroutine(FadeCanvasGroup(screenFaderGroup, 0f, 1f, 0.45f));

            // -------------------------------------------------------------
            // SHOT 3: Classroom - Empty Desks & WallClock (Cut behind black)
            // -------------------------------------------------------------
            SetActiveCamera(vcamClassroom, CinemachineBlendDefinition.Styles.Cut, 0f);
            SetActiveLighting(2);
            DisplaySlide(2);

            // Fade in from black to reveal Classroom cleanly
            yield return StartCoroutine(FadeCanvasGroup(screenFaderGroup, 1f, 0f, 0.55f));
            StartCoroutine(FadeCanvasGroup(creditTextGroup, 0f, 1f, 1.0f));

            yield return new WaitForSeconds(shot3Duration - 1.2f);
            yield return StartCoroutine(FadeCanvasGroup(creditTextGroup, 1f, 0f, 0.5f));

            // Smooth film cross-fade to black
            yield return StartCoroutine(FadeCanvasGroup(screenFaderGroup, 0f, 1f, 0.45f));

            // -------------------------------------------------------------
            // SHOT 4: Open Ocean - Raft Drifting into the Distance (Cut behind black)
            // -------------------------------------------------------------
            SetActiveCamera(vcamRaftOcean, CinemachineBlendDefinition.Styles.Cut, 0f);
            SetActiveLighting(3);
            DisplaySlide(3);

            // Fade in from black to reveal Raft Ocean
            yield return StartCoroutine(FadeCanvasGroup(screenFaderGroup, 1f, 0f, 0.55f));
            StartCoroutine(FadeCanvasGroup(creditTextGroup, 0f, 1f, 1.0f));

            yield return new WaitForSeconds(shot4Duration - 1.5f);
            yield return StartCoroutine(FadeCanvasGroup(creditTextGroup, 1f, 0f, 0.8f));

            // -------------------------------------------------------------
            // OUTRO: Fade to Black & Main Menu Prompt
            // -------------------------------------------------------------
            yield return StartCoroutine(FadeCanvasGroup(screenFaderGroup, 0f, 1f, fadeToBlackDuration));

            // Show prompt to return to Main Menu
            if (returnPromptText != null)
            {
                returnPromptText.gameObject.SetActive(true);
                returnPromptText.text = "[ PRESS ANY KEY TO RETURN TO MAIN MENU ]";
                StartCoroutine(PulsePromptText(returnPromptText));
            }

            _canReturnToMenu = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void SetActiveLighting(int shotIndex)
        {
            if (metroLightsRoot != null) metroLightsRoot.SetActive(shotIndex == 0);
            if (beachLightsRoot != null) beachLightsRoot.SetActive(shotIndex == 1);
            if (classroomLightsRoot != null) classroomLightsRoot.SetActive(shotIndex == 2);
            if (oceanLightsRoot != null) oceanLightsRoot.SetActive(shotIndex == 3);
        }

        private void HandleTrainWipe()
        {
            _trainWipeTriggered = true;
            // Immediate cut behind the passing train body into Shot 2
            if (vcamBeachTeddy != null)
            {
                SetActiveCamera(vcamBeachTeddy, CinemachineBlendDefinition.Styles.Cut, 0f);
            }
            SetActiveLighting(1);
        }

        private void SetActiveCamera(CinemachineCamera targetCam, CinemachineBlendDefinition.Styles blendStyle, float blendTime)
        {
            if (cinemachineBrain != null)
            {
                cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, blendTime);
            }

            if (vcamMetro != null) vcamMetro.Priority.Value = (targetCam == vcamMetro) ? 100 : 10;
            if (vcamBeachTeddy != null) vcamBeachTeddy.Priority.Value = (targetCam == vcamBeachTeddy) ? 100 : 10;
            if (vcamClassroom != null) vcamClassroom.Priority.Value = (targetCam == vcamClassroom) ? 100 : 10;
            if (vcamRaftOcean != null) vcamRaftOcean.Priority.Value = (targetCam == vcamRaftOcean) ? 100 : 10;
        }

        private void DisplaySlide(int index)
        {
            if (index < 0 || index >= slides.Count) return;

            var slide = slides[index];
            if (creditRoleText != null)
            {
                creditRoleText.text = slide.role;
                creditRoleText.gameObject.SetActive(!string.IsNullOrEmpty(slide.role));
            }
            if (creditNameText != null)
            {
                creditNameText.text = slide.name;
            }
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }

        private IEnumerator FadeAudioVolume(AudioSource source, float targetVolume, float duration)
        {
            if (source == null) yield break;

            float startVol = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVol, targetVolume, elapsed / duration);
                yield return null;
            }
            source.volume = targetVolume;
        }

        private IEnumerator PulsePromptText(TextMeshProUGUI prompt)
        {
            while (_canReturnToMenu && !_isReturning)
            {
                float alpha = 0.4f + Mathf.PingPong(Time.time * 1.5f, 0.6f);
                prompt.alpha = alpha;
                yield return null;
            }
        }

        public void ReturnToMainMenu()
        {
            if (_isReturning) return;
            _isReturning = true;

            StartCoroutine(ReturnToMenuRoutine());
        }

        private IEnumerator ReturnToMenuRoutine()
        {
            if (CreditsMusicController.Instance != null)
            {
                CreditsMusicController.Instance.FadeOutAndDestroy(1.0f);
            }
            else if (musicSource != null)
            {
                StartCoroutine(FadeAudioVolume(musicSource, 0f, 1.0f));
            }

            if (returnPromptText != null)
            {
                returnPromptText.gameObject.SetActive(false);
            }

            yield return StartCoroutine(FadeCanvasGroup(screenFaderGroup, screenFaderGroup ? screenFaderGroup.alpha : 0f, 1f, 1.0f));

            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
