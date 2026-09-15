Shader "WarSimulation/SkillAtlas"
{
    Properties
    {
        _MainTex ("Shape", 2D) = "white" {}
        _AlphaFloor ("Alpha floor", Range(0,1)) = 0.05
        _AlphaCeiling ("Alpha ceiling", Range(0,1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _AlphaFloor;
                half _AlphaCeiling;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = input.color;
                if (input.uv.z > 0.5)
                {
                    half4 shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
                    half coverage = shape.a * lerp(0.75h, 1.0h, shape.r);
                    color.rgb *= lerp(0.45h, 1.0h, shape.r);
                    half floor = lerp(max(_AlphaFloor, input.uv.z - 1.0h), 1.01h, input.uv.w);
                    color.a *= smoothstep(floor, max(floor + 0.025h, _AlphaCeiling), coverage);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
