using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoroccanBeach
{
    public class PSXSceneController : MonoBehaviour
    {
        [Header("Twilight Atmosphere")]
        public Color twilightFogColor = new Color(0.12f, 0.14f, 0.22f, 1f);
        public Color ambientSkyColor = new Color(0.20f, 0.22f, 0.32f, 1f);
        public float fogStart = 15f;
        public float fogEnd = 85f;

        [Header("Audio")]
        public AudioSource oceanAudioSource;

        private void Awake()
        {
            ApplyAtmosphere();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Press Escape to toggle mouse cursor
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // Press R to reload scene (quick test reset)
            if (Input.GetKeyDown(KeyCode.R))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        public void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = twilightFogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientSkyColor;
        }
    }
}
