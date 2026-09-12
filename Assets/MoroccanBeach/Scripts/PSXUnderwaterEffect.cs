using UnityEngine;
using UnityEngine.UI;

namespace MoroccanBeach
{
    [DisallowMultipleComponent]
    public class PSXUnderwaterEffect : MonoBehaviour
    {
        [Header("Water Reference")]
        [Tooltip("Reference to the ocean plane or water surface object.")]
        public Transform waterPlane;
        [Tooltip("Base water level Y if waterPlane is not assigned.")]
        public float defaultWaterLevel = 0.05f;
        [Tooltip("Offset to trigger underwater slightly before/after exact Y level.")]
        public float waterLevelOffset = 0.0f;

        [Header("Camera & Status")]
        public Camera targetCamera;
        [SerializeField] private bool isUnderwater = false;
        public bool IsUnderwater => isUnderwater;

        [Header("Atmosphere - Above Water")]
        public Color aboveWaterFogColor = new Color(0.92f, 0.78f, 0.65f, 1f);
        public float aboveWaterFogStart = 45f;
        public float aboveWaterFogEnd = 160f;
        public Color aboveWaterAmbientLight = new Color(0.72f, 0.78f, 0.86f, 1f);

        [Header("Atmosphere - Underwater")]
        public Color underwaterFogColor = new Color(0.035f, 0.20f, 0.32f, 1f);
        public float underwaterFogStart = 0.5f;
        public float underwaterFogEnd = 22f;
        public Color underwaterAmbientLight = new Color(0.08f, 0.22f, 0.30f, 1f);

        [Header("Screen Overlay")]
        public Image underwaterOverlayImage;
        [Range(0f, 1f)]
        public float maxOverlayAlpha = 1.0f;
        public float transitionSpeed = 7.0f;

        [Header("Audio Muffling")]
        public AudioLowPassFilter lowPassFilter;
        public float aboveWaterCutoff = 22000f;
        public float underwaterCutoff = 650f;

        [Header("Player Physics in Water")]
        public bool adjustPlayerDrag = true;
        public float underwaterDrag = 2.0f;
        private Rigidbody playerRb;
        private float originalDrag = 0.0f;

        private float currentWeight = 0.0f;

        private void Awake()
        {
            InitializeReferences();
        }

        private void Start()
        {
            // Sync initial state immediately
            UpdateSubmergedState(true);
        }

        public void InitializeReferences()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) targetCamera = Camera.main;
            }

            if (waterPlane == null)
            {
                var oceanGO = GameObject.Find("Ocean_Water_Plane");
                if (oceanGO != null) waterPlane = oceanGO.transform;
            }

            // Read above water settings from PSXSceneController if available
            var sceneCtrl = Object.FindFirstObjectByType<PSXSceneController>();
            if (sceneCtrl != null)
            {
                aboveWaterFogColor = sceneCtrl.twilightFogColor;
                aboveWaterFogStart = sceneCtrl.fogStart;
                aboveWaterFogEnd = sceneCtrl.fogEnd;
                aboveWaterAmbientLight = sceneCtrl.ambientSkyColor;
            }
            else
            {
                aboveWaterFogColor = RenderSettings.fogColor;
                aboveWaterFogStart = RenderSettings.fogStartDistance;
                aboveWaterFogEnd = RenderSettings.fogEndDistance;
                aboveWaterAmbientLight = RenderSettings.ambientLight;
            }

            // Audio low pass filter setup
            if (targetCamera != null)
            {
                if (lowPassFilter == null)
                {
                    lowPassFilter = targetCamera.GetComponent<AudioLowPassFilter>();
                    if (lowPassFilter == null)
                    {
                        lowPassFilter = targetCamera.gameObject.AddComponent<AudioLowPassFilter>();
                    }
                }
                lowPassFilter.cutoffFrequency = aboveWaterCutoff;
                lowPassFilter.enabled = false;
            }

            // Rigidbody drag setup
            playerRb = GetComponentInParent<Rigidbody>();
            if (playerRb != null)
            {
                originalDrag = playerRb.linearDamping;
            }

            // Check or find UI overlay
            if (underwaterOverlayImage == null)
            {
                var overlayGO = GameObject.Find("UnderwaterTintOverlay");
                if (overlayGO != null)
                {
                    underwaterOverlayImage = overlayGO.GetComponent<Image>();
                }
            }

            if (underwaterOverlayImage != null)
            {
                Color col = underwaterOverlayImage.color;
                col.a = 0f;
                underwaterOverlayImage.color = col;
                underwaterOverlayImage.gameObject.SetActive(false);
            }
        }

        public float GetCurrentWaterLevel()
        {
            return (waterPlane != null ? waterPlane.position.y : defaultWaterLevel) + waterLevelOffset;
        }

        private void Update()
        {
            UpdateSubmergedState(false);
        }

        private void UpdateSubmergedState(bool immediate)
        {
            if (targetCamera == null) return;

            float camY = targetCamera.transform.position.y;
            float waterY = GetCurrentWaterLevel();

            isUnderwater = camY < waterY;
            float targetWeight = isUnderwater ? 1.0f : 0.0f;

            if (immediate)
            {
                currentWeight = targetWeight;
            }
            else
            {
                currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * transitionSpeed);
            }

            ApplyUnderwaterAtmosphere(currentWeight);
        }

        private void ApplyUnderwaterAtmosphere(float weight)
        {
            // 1. Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(aboveWaterFogColor, underwaterFogColor, weight);
            RenderSettings.fogStartDistance = Mathf.Lerp(aboveWaterFogStart, underwaterFogStart, weight);
            RenderSettings.fogEndDistance = Mathf.Lerp(aboveWaterFogEnd, underwaterFogEnd, weight);

            // 2. Ambient light
            RenderSettings.ambientLight = Color.Lerp(aboveWaterAmbientLight, underwaterAmbientLight, weight);

            // 3. UI Overlay
            if (underwaterOverlayImage != null)
            {
                if (weight > 0.001f)
                {
                    if (!underwaterOverlayImage.gameObject.activeSelf)
                    {
                        underwaterOverlayImage.gameObject.SetActive(true);
                    }
                    Color c = underwaterOverlayImage.color;
                    c.a = weight * maxOverlayAlpha;
                    underwaterOverlayImage.color = c;
                }
                else
                {
                    if (underwaterOverlayImage.gameObject.activeSelf)
                    {
                        Color c = underwaterOverlayImage.color;
                        c.a = 0f;
                        underwaterOverlayImage.color = c;
                        underwaterOverlayImage.gameObject.SetActive(false);
                    }
                }
            }

            // 4. Audio low pass filter
            if (lowPassFilter != null)
            {
                if (weight > 0.01f)
                {
                    if (!lowPassFilter.enabled) lowPassFilter.enabled = true;
                    lowPassFilter.cutoffFrequency = Mathf.Lerp(aboveWaterCutoff, underwaterCutoff, weight);
                }
                else
                {
                    if (lowPassFilter.enabled) lowPassFilter.enabled = false;
                }
            }

            // 5. Rigidbody drag
            if (adjustPlayerDrag && playerRb != null)
            {
                playerRb.linearDamping = Mathf.Lerp(originalDrag, underwaterDrag, weight);
            }
        }

        private void OnDisable()
        {
            // Reset to above water settings when script disabled
            ApplyUnderwaterAtmosphere(0.0f);
        }

        private void OnDestroy()
        {
            ApplyUnderwaterAtmosphere(0.0f);
        }
    }
}
