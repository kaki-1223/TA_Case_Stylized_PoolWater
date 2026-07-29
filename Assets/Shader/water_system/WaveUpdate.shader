Shader "Unlit/WaveUpdate"
{
    Properties
    {
        _MainTex ("Current Height", 2D) = "black" {}
        _PrevTex("Previous Height", 2D) = "black" {}
        _WaveSpeed("Wave Speed", Float) = 0.25
        _Damping("Damping", Float) = 0.985
        _AspectRatio("Aspect Ratio", Float) = 1.0

    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" 
    }
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

            TEXTURE2D(_PrevTex);
            SAMPLER(sampler_PrevTex);

            float4 _MainTex_TexelSize;
            float _WaveSpeed;
            float _Damping;
            float _AspectRatio;

            struct Attributes{
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings{
                float4 positionH : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionH = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target{
                float current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).r;
                float prev = SAMPLE_TEXTURE2D(_PrevTex, sampler_PrevTex, IN.uv).r;

                // 计算拉普拉斯值
                float hStep = _MainTex_TexelSize.x / _AspectRatio;
                float left  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-hStep, 0)).r;
                float right = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( hStep, 0)).r;
                // float left = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-_MainTex_TexelSize.x, 0)).r;
                // float right = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(_MainTex_TexelSize.x, 0)).r;
                float up = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, _MainTex_TexelSize.y)).r;
                float down = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, -_MainTex_TexelSize.y)).r;

                float laplacian = (left + right + up + down - 4 * current);

                // 波动方程离散化
                float newHeight = 2 * current - prev + laplacian * _WaveSpeed;

                // 阻尼
                newHeight *= _Damping;

                newHeight = clamp(newHeight, -1.0, 1.0);

                return half4(newHeight, newHeight, newHeight, 1);
            }

            ENDHLSL
        }
    }
}
