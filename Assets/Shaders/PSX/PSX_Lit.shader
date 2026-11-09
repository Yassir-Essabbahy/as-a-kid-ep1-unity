Shader "PSX/PSX_Lit"
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

        [Header(Lighting and Surface)]
        [Toggle(_VERTEX_LIGHTING)] _VertexLighting("Gouraud / Vertex Lighting", Float) = 1.0
        _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [HideInInspector] _Cull("__cull", Float) = 2.0
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
        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _VERTEX_JITTER
            #pragma shader_feature_local _AFFINE_WARP
            #pragma shader_feature_local _VERTEX_LIGHTING
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/PSX/PSX_Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float2 uvAffine     : TEXCOORD1;
                float  clipW        : TEXCOORD2;
                half3  vertexLight  : COLOR0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EmissionColor;
                float  _JitterScale;
                float  _AffineStrength;
                float  _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 clipPos = TransformWorldToHClip(posWS);

                // Authentic PlayStation 1 Screen-Space Vertex Jitter
                #if defined(_VERTEX_JITTER)
                clipPos = PSX_SnapVertex(clipPos, _JitterScale);
                #endif

                output.positionCS = clipPos;

                float2 baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uv = baseUV;
                // Store perspective-divided UV multiplied by W for affine warping
                output.uvAffine = baseUV * clipPos.w;
                output.clipW = clipPos.w;

                // PlayStation 1 Gouraud / Vertex Diffuse Lighting
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                output.vertexLight = ambient + mainLight.color * NdotL;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Affine Texture Warping calculation
                float2 uv = input.uv;
                #if defined(_AFFINE_WARP)
                if (abs(input.clipW) > 0.0001)
                {
                    float2 affineUV = input.uvAffine / input.clipW;
                    float warpAmt = saturate(_AffineStrength * _PSX_AffineStrength);
                    uv = lerp(input.uv, affineUV, warpAmt);
                }
                #endif

                // Point sampling for authentic crunchy PS1 pixel texels
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_PointClamp, uv);

                #if defined(_ALPHATEST_ON)
                clip(texColor.a * _BaseColor.a - _Cutoff);
                #endif

                half3 finalColor;
                #if defined(_VERTEX_LIGHTING)
                finalColor = texColor.rgb * _BaseColor.rgb * input.vertexLight + _EmissionColor.rgb;
                #else
                finalColor = texColor.rgb * _BaseColor.rgb + _EmissionColor.rgb;
                #endif

                return half4(finalColor, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }

        // ShadowCaster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EmissionColor;
                float  _JitterScale;
                float  _AffineStrength;
                float  _Cutoff;
            CBUFFER_END

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_PointClamp, input.uv);
                clip(texColor.a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EmissionColor;
                float  _JitterScale;
                float  _AffineStrength;
                float  _Cutoff;
            CBUFFER_END

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_PointClamp, input.uv);
                clip(texColor.a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
