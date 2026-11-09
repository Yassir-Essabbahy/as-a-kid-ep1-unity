using UnityEngine;

namespace PSXRetro
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PSXSystem : MonoBehaviour
    {
        private static PSXSystem _instance;
        public static PSXSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<PSXSystem>();
                    if (_instance == null && Application.isPlaying)
                    {
                        GameObject go = new GameObject("[PSX_System]");
                        _instance = go.AddComponent<PSXSystem>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Configuration Asset")]
        [SerializeField] private PSXSettings _settings;

        [Header("Runtime Preset Override")]
        public PSXPreset currentPreset = PSXPreset.PC_High;

        [Header("Direct Knobs (Synchronized with Settings)")]
        [Range(120, 480)] public int targetVerticalResolution = 240;
        [Range(8, 64)] public float colorPrecision = 32f;
        public bool ditherEnabled = true;
        [Range(0f, 2f)] public float ditherStrength = 1.0f;
        [Range(0f, 320f)] public float vertexPrecision = 160f;
        [Range(0f, 1f)] public float affineStrength = 0.25f;

        [Header("Fog Knobs")]
        public bool fogEnabled = true;
        public Color fogColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        [Range(0.01f, 5f)] public float fogDensity = 1.0f;
        public float fogStart = 2.0f;
        public float fogEnd = 38.0f;

        // Cached shader property IDs for zero-allocation per frame
        private static readonly int PropScreenResolution = Shader.PropertyToID("_PSX_ScreenResolution");
        private static readonly int PropVertexPrecision  = Shader.PropertyToID("_PSX_VertexPrecision");
        private static readonly int PropAffineStrength   = Shader.PropertyToID("_PSX_AffineStrength");
        private static readonly int PropColorPrecision   = Shader.PropertyToID("_PSX_ColorPrecision");
        private static readonly int PropDitherStrength   = Shader.PropertyToID("_PSX_DitherStrength");
        private static readonly int PropFogEnabled       = Shader.PropertyToID("_PSX_FogEnabled");
        private static readonly int PropFogColor         = Shader.PropertyToID("_PSX_FogColor");
        private static readonly int PropFogDensity       = Shader.PropertyToID("_PSX_FogDensity");
        private static readonly int PropFogStart         = Shader.PropertyToID("_PSX_FogStart");
        private static readonly int PropFogEnd           = Shader.PropertyToID("_PSX_FogEnd");

        public PSXSettings Settings => _settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[PSX_System]");
                _instance = go.AddComponent<PSXSystem>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            _instance = this;

            if (Application.isPlaying && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeDefaults();
        }

        private void OnEnable()
        {
            _instance = this;
            UpdateShaderGlobals();
        }

        private void InitializeDefaults()
        {
            if (_settings == null)
            {
#if UNITY_EDITOR
                _settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PSXSettings>("Assets/Settings/DefaultPSXSettings.asset");
#endif
                if (_settings == null)
                {
                    _settings = ScriptableObject.CreateInstance<PSXSettings>();
                    _settings.name = "RuntimePSXSettings";
                }
            }

            // Platform auto-detection
            if (Application.isMobilePlatform)
            {
                SetPreset(PSXPreset.Mobile_Low);
            }
            else
            {
                SetPreset(PSXPreset.PC_High);
            }
        }

        public void SetPreset(PSXPreset preset)
        {
            currentPreset = preset;
            if (_settings != null)
            {
                _settings.ApplyPreset(preset);
                SyncFromSettings();
            }
            UpdateShaderGlobals();
        }

        public void SyncFromSettings()
        {
            if (_settings == null) return;
            currentPreset = _settings.preset;
            targetVerticalResolution = _settings.targetVerticalResolution;
            colorPrecision = _settings.colorPrecision;
            ditherEnabled = _settings.ditherEnabled;
            ditherStrength = _settings.ditherStrength;
            vertexPrecision = _settings.vertexPrecision;
            affineStrength = _settings.affineStrength;
            fogEnabled = _settings.fogEnabled;
            fogColor = _settings.fogColor;
            fogDensity = _settings.fogDensity;
            fogStart = _settings.fogStart;
            fogEnd = _settings.fogEnd;
        }

        public void SyncToSettings()
        {
            if (_settings == null) return;
            _settings.preset = currentPreset;
            _settings.targetVerticalResolution = targetVerticalResolution;
            _settings.colorPrecision = colorPrecision;
            _settings.ditherEnabled = ditherEnabled;
            _settings.ditherStrength = ditherStrength;
            _settings.vertexPrecision = vertexPrecision;
            _settings.affineStrength = affineStrength;
            _settings.fogEnabled = fogEnabled;
            _settings.fogColor = fogColor;
            _settings.fogDensity = fogDensity;
            _settings.fogStart = fogStart;
            _settings.fogEnd = fogEnd;
        }

        private void OnValidate()
        {
            if (currentPreset != PSXPreset.Custom && _settings != null && currentPreset != _settings.preset)
            {
                SetPreset(currentPreset);
            }
            else
            {
                SyncToSettings();
            }
            UpdateShaderGlobals();
        }

        private void Update()
        {
            if (Application.isPlaying && currentPreset != PSXPreset.Custom)
            {
                int qLevel = QualitySettings.GetQualityLevel();
                PSXPreset target = (qLevel == 0 || Application.isMobilePlatform) ? PSXPreset.Mobile_Low : PSXPreset.PC_High;
                if (currentPreset != target)
                {
                    SetPreset(target);
                }
            }

            UpdateShaderGlobals();
        }

        public void UpdateShaderGlobals()
        {
            // Calculate dynamic aspect ratio from main camera or screen
            Camera cam = Camera.main;
            float aspect = (cam != null && cam.aspect > 0.001f) ? cam.aspect : ((float)Screen.width / Mathf.Max(1, Screen.height));
            if (aspect <= 0.001f) aspect = 16f / 9f;

            int height = Mathf.Clamp(targetVerticalResolution, 120, 1080);
            int width = Mathf.RoundToInt(height * aspect);

            Vector4 screenRes = new Vector4(width, height, 1.0f / width, 1.0f / height);

            Shader.SetGlobalVector(PropScreenResolution, screenRes);
            Shader.SetGlobalFloat(PropVertexPrecision, vertexPrecision);
            Shader.SetGlobalFloat(PropAffineStrength, affineStrength);
            Shader.SetGlobalFloat(PropColorPrecision, Mathf.Max(2f, colorPrecision));
            Shader.SetGlobalFloat(PropDitherStrength, ditherEnabled ? ditherStrength : 0f);
            Shader.SetGlobalFloat(PropFogEnabled, fogEnabled ? 1f : 0f);
            Shader.SetGlobalColor(PropFogColor, fogColor);
            Shader.SetGlobalFloat(PropFogDensity, fogDensity);
            Shader.SetGlobalFloat(PropFogStart, fogStart);
            Shader.SetGlobalFloat(PropFogEnd, Mathf.Max(fogStart + 0.1f, fogEnd));
        }

        public Vector2 GetAspectAwareResolution(Camera cam)
        {
            float aspect = (cam != null && cam.aspect > 0.001f) ? cam.aspect : ((float)Screen.width / Mathf.Max(1, Screen.height));
            if (aspect <= 0.001f) aspect = 16f / 9f;
            int height = Mathf.Clamp(targetVerticalResolution, 120, 1080);
            int width = Mathf.RoundToInt(height * aspect);
            return new Vector2(width, height);
        }
    }
}
