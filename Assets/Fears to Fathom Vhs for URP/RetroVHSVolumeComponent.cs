using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenu("Post-processing/Retro VHS Horror")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class RetroVHSVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public BoolParameter enableRetroVHS = new BoolParameter(false);

    [Header("CRT Scanlines & Analog Tape")]
    public ClampedFloatParameter scanlineIntensity = new ClampedFloatParameter(0.18f, 0f, 0.5f);
    public ClampedFloatParameter scanlineCount = new ClampedFloatParameter(320f, 120f, 720f);
    public ClampedFloatParameter tapeWobble = new ClampedFloatParameter(0.0012f, 0f, 0.01f);
    public ClampedFloatParameter trackingStrength = new ClampedFloatParameter(0.0035f, 0f, 0.02f);

    [Header("Chromatic Bleed & Grain")]
    public ClampedFloatParameter chromaticBleed = new ClampedFloatParameter(0.0045f, 0f, 0.02f);
    public ClampedFloatParameter edgeChromatic = new ClampedFloatParameter(0.0055f, 0f, 0.02f);
    public ClampedFloatParameter grainIntensity = new ClampedFloatParameter(0.055f, 0f, 0.25f);

    [Header("Framing & Atmosphere")]
    public ClampedFloatParameter vignetteIntensity = new ClampedFloatParameter(0.26f, 0f, 0.8f);
    public ClampedFloatParameter fisheyeBend = new ClampedFloatParameter(0.035f, 0f, 0.25f);
    public BoolParameter blackBorders = new BoolParameter(false);
    public ColorParameter shadowTint = new ColorParameter(new Color(0.04f, 0.02f, 0.07f, 1f), false, false, true);

    public bool IsActive() => active && enableRetroVHS.value;

    public bool IsTileCompatible() => false;
}
