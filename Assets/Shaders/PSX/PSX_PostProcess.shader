Shader "Hidden/PSX/PSX_PostProcess"
{
    Properties
    {
        [HideInInspector] _BlitTexture("Source Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PSXPostProcessPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Shaders/PSX/PSX_Core.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.texcoord;

                // Ensure safe resolution parameters
                float2 res = _PSX_ScreenResolution.xy;
                if (res.x < 1.0 || res.y < 1.0)
                {
                    res = _ScreenParams.xy;
                }
                float2 invRes = 1.0 / res;

                // Aspect-aware point-sampled downsampling
                float2 pixel = floor(screenUV * res);
                float2 downsampledUV = (pixel + 0.5) * invRes;

                // Sample source scene color with point clamping
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, downsampledUV);

                // Authentic PlayStation 1 depth fog
                if (_PSX_FogEnabled > 0.5)
                {
                    float rawDepth = SampleSceneDepth(downsampledUV);
                    #if UNITY_REVERSED_Z
                    bool isGeometry = rawDepth > 0.00001;
                    #else
                    bool isGeometry = rawDepth < 0.99999;
                    #endif

                    if (isGeometry)
                    {
                        float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                        float fogSpan = max(0.1, _PSX_FogEnd - _PSX_FogStart);
                        float fogFactor = saturate((linearDepth - _PSX_FogStart) / fogSpan);
                        fogFactor = pow(fogFactor, max(0.05, 1.0 / max(0.01, _PSX_FogDensity)));
                        col.rgb = lerp(col.rgb, _PSX_FogColor.rgb, fogFactor);
                    }
                }

                // 15-bit color depth quantization with ordered Bayer dithering
                uint2 pixelCoord = (uint2)pixel;
                col.rgb = PSX_QuantizeColor(col.rgb, _PSX_ColorPrecision, pixelCoord, _PSX_DitherStrength);

                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
