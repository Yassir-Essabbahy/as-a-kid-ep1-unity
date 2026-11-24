Shader "Custom/RetroVHS_URP"
{
    Properties
    {
        [HideInInspector] _BlitTexture ("Blit Texture", 2D) = "white" {}
        _ScanlineIntensity ("Scanline Intensity", Range(0, 0.5)) = 0.18
        _ScanlineCount ("Scanline Count", Range(120, 720)) = 320.0
        _ChromaticBleed ("Chromatic Bleed", Range(0, 0.02)) = 0.0045
        _EdgeChromatic ("Edge Chromatic Boost", Range(0, 0.02)) = 0.0055
        _TapeWobble ("Tape Horizontal Wobble", Range(0, 0.01)) = 0.0012
        _TrackingStrength ("Tracking Bar Strength", Range(0, 0.02)) = 0.0035
        _GrainIntensity ("Analog Grain", Range(0, 0.25)) = 0.055
        _VignetteIntensity ("Vignette Intensity", Range(0, 0.8)) = 0.26
        _FisheyeBend ("Subtle Lens Curve", Range(0, 0.25)) = 0.035
        [Toggle] _BlackBorders ("Black Outside Lens", Float) = 0
        _ShadowTint ("Shadow Tint", Color) = (0.04, 0.02, 0.07, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "RetroVHS"
            Tags { "LightMode"="UniversalForward" }

            ZTest Always
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitScaleBias;

            float _ScanlineIntensity;
            float _ScanlineCount;
            float _ChromaticBleed;
            float _EdgeChromatic;
            float _TapeWobble;
            float _TrackingStrength;
            float _GrainIntensity;
            float _VignetteIntensity;
            float _FisheyeBend;
            float _BlackBorders;
            half4 _ShadowTint;

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
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y;

                // Subtle CRT glass curvature
                float2 centered = uv - 0.5;
                float distSq = dot(centered, centered);
                uv = 0.5 + centered * (1.0 + _FisheyeBend * distSq);

                if (_BlackBorders > 0.5)
                {
                    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                        return half4(0, 0, 0, 1);
                }
                else
                {
                    uv = clamp(uv, 0.0005, 0.9995);
                }

                // Subtle analog tape wobble + rolling tracking bar (micro jitter only, preserves crisp architecture)
                float wobble = sin(uv.y * 120.0 - t * 11.0) * 0.35
                             + (Hash12(float2(floor(uv.y * 220.0), floor(t * 20.0))) - 0.5) * 0.65;
                float trackPos = frac(t * 0.11);
                float trackDist = abs(uv.y - trackPos);
                float trackBand = smoothstep(0.045, 0.0, trackDist);
                float trackNoise = (Hash12(float2(floor(uv.y * 220.0), floor(t * 24.0))) - 0.5);

                float xShift = wobble * _TapeWobble + trackBand * trackNoise * _TrackingStrength;
                float2 baseUV = uv;
                baseUV.x = clamp(baseUV.x + xShift, 0.0005, 0.9995);

                // Radial edge factor boosts RGB split subtly toward screen edges
                float edgeFactor = saturate(distSq * 3.2);
                float split = _ChromaticBleed + _EdgeChromatic * edgeFactor + trackBand * 0.0012;

                float2 scaleBiasScale = (_BlitScaleBias.x == 0.0 && _BlitScaleBias.y == 0.0) ? float2(1.0, 1.0) : _BlitScaleBias.xy;
                float2 scaleBiasOffset = _BlitScaleBias.zw;

                float2 rUV = clamp(baseUV + float2(split, 0.0), 0.0005, 0.9995) * scaleBiasScale + scaleBiasOffset;
                float2 gUV = clamp(baseUV, 0.0005, 0.9995) * scaleBiasScale + scaleBiasOffset;
                float2 bUV = clamp(baseUV - float2(split, 0.0), 0.0005, 0.9995) * scaleBiasScale + scaleBiasOffset;

                half r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, rUV).r;
                half g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, gUV).g;
                half b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, bUV).b;
                half3 col = half3(r, g, b);

                // Crisp horizontal CRT / VHS scanlines
                float scan = sin(uv.y * _ScanlineCount * 6.2831853);
                float scanMask = smoothstep(-0.6, 0.6, scan);
                float scanMul = 1.0 - _ScanlineIntensity * (1.0 - scanMask);
                col *= scanMul;

                // Fine analog VHS grain (stronger subtly in darks + tracking band)
                float grain = Hash12(uv * float2(520.0, 290.0) + frac(t * 17.3) * 123.4) - 0.5;
                float streakGrain = Hash12(float2(floor(uv.y * 260.0), floor(t * 28.0))) - 0.5;
                float lum = dot(col, half3(0.299, 0.587, 0.114));
                float darkMask = lerp(1.1, 0.45, saturate(lum * 1.35));
                col += (grain * 0.78 + streakGrain * 0.22) * _GrainIntensity * darkMask;
                col += trackBand * abs(trackNoise) * 0.025;

                // Gentle moody VHS purple-indigo shadow floor (prevents harsh pure-black crush)
                col = max(col, _ShadowTint.rgb * 0.45);

                // Smooth radial vignette
                float vig = 1.0 - saturate(distSq * 2.15) * _VignetteIntensity;
                col *= vig;

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
