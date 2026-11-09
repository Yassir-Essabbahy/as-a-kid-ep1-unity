Shader "PSX/PSX_Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)

        [Header(PSX Retro Features)]
        [Toggle(_VERTEX_JITTER)] _EnableJitter("Enable Vertex Jitter", Float) = 1.0
        _JitterScale("Local Jitter Scale", Range(0, 2)) = 1.0
        [Toggle(_AFFINE_WARP)] _EnableAffine("Enable Affine Texture Mapping", Float) = 1.0
        _AffineStrength("Local Affine Strength", Range(0, 1)) = 0.35

        [Header(Blending and Alpha)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 0.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _VERTEX_JITTER
            #pragma shader_feature_local _AFFINE_WARP
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/PSX/PSX_Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                half4  color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float2 uvAffine     : TEXCOORD1;
                float  clipW        : TEXCOORD2;
                half4  color        : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float  _JitterScale;
                float  _AffineStrength;
                float  _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 clipPos = TransformWorldToHClip(posWS);

                #if defined(_VERTEX_JITTER)
                clipPos = PSX_SnapVertex(clipPos, _JitterScale);
                #endif

                output.positionCS = clipPos;

                float2 baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uv = baseUV;
                output.uvAffine = baseUV * clipPos.w;
                output.clipW = clipPos.w;
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                #if defined(_AFFINE_WARP)
                if (abs(input.clipW) > 0.0001)
                {
                    float2 affineUV = input.uvAffine / input.clipW;
                    float warpAmt = saturate(_AffineStrength * _PSX_AffineStrength);
                    uv = lerp(input.uv, affineUV, warpAmt);
                }
                #endif

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_PointClamp, uv);

                #if defined(_ALPHATEST_ON)
                clip(texColor.a * _BaseColor.a - _Cutoff);
                #endif

                half4 finalColor = texColor * _BaseColor * input.color;
                return finalColor;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
