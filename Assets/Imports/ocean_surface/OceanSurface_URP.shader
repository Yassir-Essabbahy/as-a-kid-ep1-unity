Shader "Custom/OceanSurface_URP"
{
    Properties
    {
        _BaseColor ("Albedo Tint", Color) = (0.324, 0.409, 0.587, 1.0)
        _WaterTex1 ("Water Texture 1", 2D) = "white" {}
        _WaterTex2 ("Water Texture 2", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _ScrollSpeed1 ("Scroll Speed 1", Vector) = (0.015, 0.001, 0, 0)
        _ScrollSpeed2 ("Scroll Speed 2", Vector) = (-0.015, -0.02, 0, 0)
        _BlendFactor ("Blend Factor", Range(0.0, 1.0)) = 0.27
        _Scale1 ("Scale 1", Vector) = (3.0, 3.0, 0, 0)
        _Scale2 ("Scale 2", Vector) = (3.0, 3.0, 0, 0)
        _WaveStrength ("Wave Strength", Float) = 0.2
        _WaveScale ("Wave Scale", Float) = 0.02
        _PixelationLevel ("Pixelation Level", Float) = 256.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_WaterTex1);
            SAMPLER(sampler_WaterTex1);

            TEXTURE2D(_WaterTex2);
            SAMPLER(sampler_WaterTex2);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _ScrollSpeed1;
                float4 _ScrollSpeed2;
                float4 _Scale1;
                float4 _Scale2;
                float _BlendFactor;
                float _WaveStrength;
                float _WaveScale;
                float _PixelationLevel;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                
                // Sample noise for wave displacement
                float2 noiseUV = worldPos.xz * _WaveScale;
                float noiseVal = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, noiseUV, 0).r;
                
                // Sin wave matching Godot formula: sin(pos.x * 0.2 + pos.z * 0.2 + _Time.y + noise * 10) * wave_strength
                float wave = sin(worldPos.x * 0.2 + worldPos.z * 0.2 + _Time.y * 1.5 + noiseVal * 10.0) * _WaveStrength;
                
                input.positionOS.y += wave;
                worldPos.y += wave;

                output.worldPos = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv1 = input.uv * _Scale1.xy + _ScrollSpeed1.xy * _Time.y;
                float2 uv2 = input.uv * _Scale2.xy + _ScrollSpeed2.xy * _Time.y;

                // Fractional UV repeat
                uv1 = frac(uv1);
                uv2 = frac(uv2);

                // Pixelation step (exact Godot implementation: floor(uv * level) / level)
                if (_PixelationLevel > 0.0)
                {
                    uv1 = floor(uv1 * _PixelationLevel) / _PixelationLevel;
                    uv2 = floor(uv2 * _PixelationLevel) / _PixelationLevel;
                }

                half4 c1 = SAMPLE_TEXTURE2D(_WaterTex1, sampler_WaterTex1, uv1);
                half4 c2 = SAMPLE_TEXTURE2D(_WaterTex2, sampler_WaterTex2, uv2);

                half4 blended = lerp(c1, c2, _BlendFactor);
                half3 finalRGB = blended.rgb * _BaseColor.rgb;

                // Main light lighting contribution for cohesive scene integration
                Light mainLight = GetMainLight();
                half NdotL = max(0.35, dot(half3(0, 1, 0), mainLight.direction));
                half3 lightCol = mainLight.color * NdotL + half3(0.25, 0.30, 0.35); // Ambient fill
                finalRGB *= lightCol;

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}
