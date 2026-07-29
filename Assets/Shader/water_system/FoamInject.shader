Shader "Unlit/FoamInject"
{
    Properties
    {
        _MainTex ("Sourece", 2D) = "black" {}
        _Center ("Center", Vector) =  (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0.05
        _Strength ("Strength", Float) = 1.0

        _InnerRadius ("Inner Radius", Range(0.0, 1.0)) = 0.45
        _NoiseScale ("Noise Scale", Float) = 80
        _NoiseThreshold ("Noise Threshold", Range(0.0, 1.0)) = 0.45

        _Seed ("Seed", Float) = 0

    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _Center;
            float _Radius;
            float _Strength;

            float _InnerRadius;
            float _NoiseScale;
            float _NoiseThreshold;
            float _Seed;


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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float prev = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;

                float2 delta = input.uv - _Center.xy;
                float dist = length(delta);
                float dist01 = saturate(dist / max(_Radius, 0.0001));

                // 圆形总范围，圆外为 0
                float circleMask = 1.0 - smoothstep(0.92, 1.0, dist01);

                // 基础外圈，但不要太完整
                float ring = smoothstep(0.45, 0.72, dist01) * (1.0 - smoothstep(0.75, 1.0, dist01));

                // 中心弱泡沫，不要完全空
                // float center = 1.0 - smoothstep(0.0, 0.62, dist01);
                // center *= 0.28;

                // // 中段残留
                // float mid = (1.0 - smoothstep(0.18, 0.9, dist01)) * 0.24;
                float center = 1.0 - smoothstep(0.0, 0.62, dist01);
                center *= 0.38;

                float mid = (1.0 - smoothstep(0.15, 0.9, dist01)) * 0.28;

                


                // 角度噪声，让圆环断开成不完整弧段
                float angle = atan2(delta.y, delta.x);
                float angle01 = angle / 6.2831853 + 0.5;

                float arcNoise = ValueNoise(float2(angle01 * 18.0 + _Seed, _Seed * 0.37));
                float arcMask = smoothstep(0.28, 0.75, arcNoise);

                // 空间破碎噪声
                float noiseA = ValueNoise(input.uv * _NoiseScale + _Center.xy * 37.0 + _Seed);
                float noiseB = ValueNoise(input.uv * _NoiseScale * 2.4 + _Center.yx * 91.0 + _Seed * 1.7);
                float noise = noiseA * 0.6 + noiseB * 0.4;

                // 不要切得像雪花，保留软过渡
                float broken = lerp(0.55, 1.0, smoothstep(_NoiseThreshold, 1.0, noise));

                // 外圈受 arcMask 影响，中心不要完全被 arcMask 切掉
                float foam = ring * arcMask * broken * 0.9;
                foam += center * broken * 1.4;
                foam += mid * broken * 1.2;

                foam *= circleMask;
                foam *= _Strength;

                float result = saturate(max(prev, foam));

                return half4(result, result, result, result);
            }



            ENDHLSL
        }
    }
}