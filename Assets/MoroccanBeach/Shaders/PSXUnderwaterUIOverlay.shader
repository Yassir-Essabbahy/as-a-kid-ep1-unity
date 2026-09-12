Shader "Custom/PSXUnderwaterUIOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Underwater Tint", Color) = (0.04, 0.32, 0.46, 0.48)
        _VignetteColor ("Vignette Color", Color) = (0.01, 0.12, 0.22, 0.80)
        _VignettePower ("Vignette Power", Range(0.5, 4.0)) = 1.8
        _CausticSpeed ("Caustic Speed", Float) = 1.2
        _CausticScale ("Caustic Scale", Float) = 7.0
        _CausticStrength ("Caustic Strength", Range(0.0, 0.5)) = 0.07
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _VignetteColor;
            float _VignettePower;
            float _CausticSpeed;
            float _CausticScale;
            float _CausticStrength;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                // Vignette factor from screen center (0.5, 0.5)
                float2 d = (uv - 0.5) * 2.0;
                float dist = length(d);
                float vigFactor = saturate(pow(dist * 0.72, _VignettePower));

                // Procedural retro caustics (sinusoidal interference pattern)
                float t = _Time.y * _CausticSpeed;
                float c1 = sin(uv.x * _CausticScale + t) * cos(uv.y * _CausticScale + t * 0.8);
                float c2 = sin((uv.x + uv.y) * (_CausticScale * 0.7) - t * 1.2);
                float caustics = saturate((c1 + c2) * 0.5 + 0.5) * _CausticStrength;

                // Combine tint with vignette & caustics
                fixed4 col = lerp(_Color, _VignetteColor, vigFactor);
                col.rgb += caustics;

                // Modulate with UI vertex alpha (used for smooth fading in/out)
                col.a *= IN.color.a;

                return col;
            }
            ENDCG
        }
    }
}