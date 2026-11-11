Shader"Custom/VHS_Mobile_URP"
{
    Properties
    {
        [HideInInspector] _BlitTexture ("Blit Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _BleedAmount ("Chromatic Bleed", Float) = 0.005
        _NoiseAmount ("Noise Strength", Float) = 0.05
        _FisheyeBend ("Fisheye Strength", Float) = 0.2
        _TimeSpeed ("Noise Scroll Speed", Float) = 1.0
        [Toggle] _BlackBorders ("Black Outside Fisheye", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
Name"VHS"
            Tags
{"LightMode"="UniversalForward"
}

ZTest Always

Cull Off

ZWrite Off

HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BlitTexture);SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_NoiseTex);       SAMPLER(sampler_NoiseTex);
float4 _BlitScaleBias;
float _BleedAmount;
float _NoiseAmount;
float _FisheyeBend;
float _TimeSpeed;
float _BlackBorders;

struct Attributes
{
    uint vertexID : SV_VertexID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

Varyings vert(Attributes input)
{
    Varyings output;
                // Full-screen triangle position and UV from the vertex index
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
    output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
    return output;
}

half4 frag(Varyings input) : SV_Target
{
    float2 uv = input.uv;

                // ----- Fisheye -----
    float2 centered = uv - 0.5;
    float len = length(centered);
    uv = 0.5 + centered * (1.0 + _FisheyeBend * len * len);

                // Handle out-of-bounds UVs
    if (_BlackBorders > 0.5)
    {
        if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
            return half4(0, 0, 0, 1);
    }
    else
    {
        uv = clamp(uv, 0.0, 1.0);
    }

                // ----- Chromatic Bleed -----
    float2 rUV = clamp(uv + float2(_BleedAmount, 0), 0.0005, 0.9995);
    float2 gUV = clamp(uv, 0.0005, 0.9995);
    float2 bUV = clamp(uv - float2(_BleedAmount, 0), 0.0005, 0.9995);

    float2 scaleBiasScale = (_BlitScaleBias.x == 0.0 && _BlitScaleBias.y == 0.0) ? float2(1.0, 1.0) : _BlitScaleBias.xy;
    float2 scaleBiasOffset = _BlitScaleBias.zw;

    half r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, rUV * scaleBiasScale + scaleBiasOffset).r;
    half g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, gUV * scaleBiasScale + scaleBiasOffset).g;
    half b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, bUV * scaleBiasScale + scaleBiasOffset).b;
    half4 col = half4(r, g, b, 1.0);

                // ----- Scrolling Noise -----
    float2 noiseUV = uv * 0.25;
    float scroll = _Time.y * _TimeSpeed;
    half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV + scroll).r;
    col.rgb += (noise - 0.5) * _NoiseAmount;

    return col;
}
            ENDHLSL
        }
    }

FallBack Off
}