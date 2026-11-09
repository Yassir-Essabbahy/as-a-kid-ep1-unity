using UnityEngine;

namespace PSXRetro
{
    public enum PSXPreset
    {
        PC_High,
        Mobile_Low,
        Custom
    }

    [CreateAssetMenu(fileName = "PSXSettings", menuName = "PSX/PSX Settings", order = 1)]
    public class PSXSettings : ScriptableObject
    {
        [Header("Active Preset")]
        public PSXPreset preset = PSXPreset.PC_High;

        [Header("Resolution & Aspect")]
        [Tooltip("Target vertical resolution in pixels (e.g., 240 for 240p, 270 for 480x270). Horizontal resolution is calculated dynamically from camera aspect ratio so pixels are always square without stretching on 16:9, 20:9, 21:9.")]
        [Range(120, 480)]
        public int targetVerticalResolution = 240;

        [Header("Color & Dithering")]
        [Tooltip("Color steps per channel. 32 steps = authentic 15-bit PlayStation 1 RGB555 (32,768 colors).")]
        [Range(8, 64)]
        public float colorPrecision = 32f;

        [Tooltip("Enable 4x4 Bayer matrix ordered dithering to emulate hardware color blending.")]
        public bool ditherEnabled = true;

        [Range(0f, 2f)]
        public float ditherStrength = 1.0f;

        [Header("Geometry & Textures")]
        [Tooltip("Screen-space vertex snapping precision. Simulates the fixed-point coordinate truncation of the PS1 GTE.")]
        [Range(0f, 320f)]
        public float vertexPrecision = 160f;

        [Tooltip("Global intensity for non-perspective-correct (affine) texture coordinate distortion.")]
        [Range(0f, 1f)]
        public float affineStrength = 0.25f;

        [Header("PlayStation Fog")]
        public bool fogEnabled = true;
        public Color fogColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        [Range(0.01f, 5f)]
        public float fogDensity = 1.0f;
        public float fogStart = 2.0f;
        public float fogEnd = 38.0f;

        public void ApplyPreset(PSXPreset targetPreset)
        {
            preset = targetPreset;
            switch (targetPreset)
            {
                case PSXPreset.PC_High:
                    targetVerticalResolution = 270; // 480x270 at 16:9 (clean 4x integer scale to 1080p)
                    colorPrecision = 32f;           // Authentic 15-bit
                    ditherEnabled = true;
                    ditherStrength = 1.0f;
                    vertexPrecision = 180f;
                    affineStrength = 0.25f;
                    fogEnabled = true;
                    fogDensity = 1.0f;
                    fogStart = 3.0f;
                    fogEnd = 40.0f;
                    break;

                case PSXPreset.Mobile_Low:
                    targetVerticalResolution = 240; // 426x240 at 16:9, 533x240 at 20:9
                    colorPrecision = 32f;
                    ditherEnabled = true;
                    ditherStrength = 0.75f;
                    vertexPrecision = 140f;
                    affineStrength = 0.15f;
                    fogEnabled = true;
                    fogDensity = 0.85f;
                    fogStart = 2.0f;
                    fogEnd = 32.0f;
                    break;
            }
        }
    }
}
