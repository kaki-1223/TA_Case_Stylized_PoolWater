Shader "Unlit/RippleInject"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Center("Center", Vector) = (0.5,0.5,0,0)
         _Radius("Radius", Float) = 0.05
         _Strength("Strength", Float) = 1.0
        _AspectRatio("Aspect Ratio", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" 
        "RenderPipeline"="UniversalPipeline" }
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

            half4 frag(Varyings IN) : SV_Target {
                float prev = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).r;

                float2 diff = IN.uv - _Center.xy;   // ← 修复：IN.uv 和 _Center.xy
                diff.x *= _AspectRatio;              // 补偿 X 方向拉伸
                float dist = length(diff);

                float circle = 1 - smoothstep(0.0, _Radius, dist);
                float result = prev + circle * _Strength;
                return half4(result, result, result, 1);
}

            ENDHLSL
        }
    }
}
