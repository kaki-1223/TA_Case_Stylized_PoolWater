Shader "Unlit/FoamDecay"
{
    Properties
    {
        _MainTex ("Foam Texture", 2D) = "black" {}
        _Dacay("Dacay", Float) = 0.985

        _ErodeStrength ("Erode Strength", Float) = 0.015
        _NoiseScale ("Noise Scale", Float) = 90
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
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

            float _Decay;
            float _ErodeStrength;
            float _NoiseScale;

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
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }


            half4 frag(Varyings input) : SV_Target
            {
                float prev = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                float result = prev * _Decay;
                float noise = ValueNoise(input.uv * _NoiseScale);

                float erode = (1.0 - noise) * _ErodeStrength;
                result = saturate(result - erode);

                return half4(result, result, result, result);
            }

            ENDHLSL
        }
    }
}
