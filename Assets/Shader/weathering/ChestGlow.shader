Shader "Custom/TA/HolyChestGlow"
{
    Properties
    {
        [HDR]_Color ("Glow Color", Color) = (1, 0.85, 0.55, 1)
        _Intensity ("Intensity", Float) = 3
        _Radius ("Radius", Range(0.01, 1)) = 0.35
        _Softness ("Softness", Range(0.01, 1)) = 0.4
        _Alpha ("Alpha", Range(0, 1)) = 1

        //breath light
        _PulseSpeed ("Pulse Speed", Float) = 1.2
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.35
        _PulseRadius ("Pulse Radius", Range(0, 0.5)) = 0.08

        _CoreStrength ("Core Strength", Float) = 1.5
        _CoreSize ("Core Size", Range(0.01, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Radius;
                float _Softness;
                float _Alpha;

                float _PulseSpeed;
                float _PulseIntensity;
                float _PulseRadius;

                float _CoreStrength;
                float _CoreSize;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 centerUV = IN.uv - 0.5;
                float dist = length(centerUV);

                // 0 - 1 breathing wave
                float pulse01 = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;

                // -1 - 1 breathing wave
                float pulseSigned = pulse01 * 2.0 - 1.0;

                // Brightness breathing
                float intensityPulse = 1.0 + pulseSigned * _PulseIntensity;

                // Radius breathing
                float currentRadius = _Radius + pulseSigned * _PulseRadius;

                // Outer soft halo
                float halo = 1.0 - smoothstep(currentRadius, currentRadius + _Softness, dist);
                halo = pow(halo, 1.8);

                // Bright center core
                float core = 1.0 - smoothstep(0.0, _CoreSize, dist);
                core = pow(core, 2.5) * _CoreStrength;

                float glow = saturate(halo + core);

                float3 finalColor = _Color.rgb * _Intensity * intensityPulse * glow;
                float finalAlpha = glow * _Alpha * _Color.a;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}
