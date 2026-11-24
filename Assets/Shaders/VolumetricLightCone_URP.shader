Shader "Custom/VolumetricLightCone_URP"
{
    Properties
    {
        _Color ("Light Tint", Color) = (0.82, 0.80, 1.0, 1.0)
        _Intensity ("Intensity", Range(0, 1.5)) = 0.16
        _FresnelPower ("Edge Softness (Fresnel)", Range(0.5, 6.0)) = 2.6
        _TopFade ("Top Fade", Range(0.01, 0.5)) = 0.08
        _BottomFade ("Bottom Fade Power", Range(0.5, 4.0)) = 1.7
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VolumetricCone"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _FresnelPower;
                half _TopFade;
                half _BottomFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 normalWS = normalize(input.normalWS);

                // Smooth volumetric edge fade using facing angle
                float ndotv = abs(dot(normalWS, viewDirWS));
                float fresnel = pow(saturate(ndotv), _FresnelPower);

                // Vertical cone fade (uv.y = 1 near fixture, 0 at bottom)
                float v = saturate(input.uv.y);
                float bottomFade = pow(v, _BottomFade);
                float topFade = smoothstep(1.0, 1.0 - _TopFade, v);

                // Camera near fade so walking directly inside a beam doesn't pop
                float camDist = length(_WorldSpaceCameraPos - input.positionWS);
                float nearFade = smoothstep(0.6, 2.5, camDist);

                half alpha = fresnel * bottomFade * topFade * nearFade * _Intensity;
                return half4(_Color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
