Shader "WarSimulation/Country River"
{
    Properties
    {
        _BaseMap ("Water", 2D) = "white" {}
        _FlowSpeed ("Current speed", Float) = 0.018
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _FlowSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=float2(input.positionWS.z/7.4,input.positionWS.x/40-_Time.y*_FlowSpeed);
                half3 water=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                return half4(water*(.78+.22*sun.shadowAttenuation),1);
            }
            ENDHLSL
        }
    }
}
