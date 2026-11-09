#ifndef PSX_CORE_INCLUDED
#define PSX_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(PSXGlobals)
    float4 _PSX_ScreenResolution; // (width, height, 1/width, 1/height)
    float _PSX_VertexPrecision;
    float _PSX_AffineStrength;
    float _PSX_ColorPrecision;
    float _PSX_DitherStrength;
    float _PSX_FogEnabled;
    float4 _PSX_FogColor;
    float _PSX_FogDensity;
    float _PSX_FogStart;
    float _PSX_FogEnd;
CBUFFER_END

// Screen-space vertex snapping (PlayStation 1 fixed-point geometry wobble)
inline float4 PSX_SnapVertex(float4 clipPos, float precisionScale)
{
    float precision = _PSX_VertexPrecision * precisionScale;
    if (precision > 0.0 && clipPos.w > 0.0001)
    {
        float2 ndc = clipPos.xy / clipPos.w;
        // Aspect-ratio-corrected snapping resolution
        float2 snapRes = _PSX_ScreenResolution.xy * (precision / 240.0);
        snapRes = max(snapRes, float2(16.0, 16.0));
        ndc = floor(ndc * snapRes + 0.5) / snapRes;
        clipPos.xy = ndc * clipPos.w;
    }
    return clipPos;
}

// Classical 4x4 Bayer Matrix for authentic PlayStation 1 ordered dithering
static const float PSX_BayerMatrix4x4[16] = {
     0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
    12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
     3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
    15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
};

inline float PSX_GetBayerDither(uint2 pixelCoord)
{
    uint x = pixelCoord.x & 3;
    uint y = pixelCoord.y & 3;
    return PSX_BayerMatrix4x4[y * 4 + x] - 0.5;
}

// Reduced color depth (15-bit style, 32 levels per channel) with ordered dithering
inline float3 PSX_QuantizeColor(float3 color, float precision, uint2 pixelCoord, float ditherStrength)
{
    if (precision <= 0.0) precision = 32.0; // default 5 bits = 32 levels (PS1 RGB555)
    float dither = PSX_GetBayerDither(pixelCoord) * (1.0 / precision) * ditherStrength;
    float3 dithered = saturate(color + dither);
    return floor(dithered * (precision - 1.0) + 0.5) / (precision - 1.0);
}

#endif
