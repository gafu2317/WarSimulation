Shader "WarSimulation/CharacterOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CombatOutlineColor ("Team Color", Color) = (0, 0, 0, 0)
        _OutlineWidth ("Width", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Silhouette"
            Blend Off
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _CombatOutlineColor;

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                float depth : TEXCOORD1;
                half alpha : TEXCOORD3;
            };

            Varyings MaskVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.depth = -TransformWorldToView(world).z;
                output.uv = input.uv;
                output.alpha = input.color.a * unity_SpriteColor.a;
                return output;
            }

            float4 MaskFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.alpha - 0.1);
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    sceneDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, sceneDepth);
                #endif
                float3 sceneWorld = ComputeWorldSpacePosition(uv, sceneDepth, UNITY_MATRIX_I_VP);
                float sceneEyeDepth = -TransformWorldToView(sceneWorld).z;
                clip(sceneEyeDepth - input.depth + 0.01);
                return float4(_CombatOutlineColor.rgb, input.depth);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment OutlineFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float _OutlineWidth;

            float4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                if (center.a > 0) return 0;

                float4 nearest = 0;
                float2 pixel = rcp(_ScreenParams.xy);
                const float2 directions[8] = {
                    float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                    float2(0.7071, 0.7071), float2(-0.7071, 0.7071),
                    float2(0.7071, -0.7071), float2(-0.7071, -0.7071)
                };
                for (int radius = 1; radius <= (int)_OutlineWidth; radius++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float4 sample = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp,
                            uv + directions[i] * pixel * radius, 0);
                        if (sample.a > 0 && (nearest.a == 0 || sample.a < nearest.a)) nearest = sample;
                    }
                    if (nearest.a > 0) break;
                }
                if (nearest.a == 0) return 0;
                float sceneDepth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    sceneDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, sceneDepth);
                #endif
                float3 sceneWorld = ComputeWorldSpacePosition(uv, sceneDepth, UNITY_MATRIX_I_VP);
                float sceneEyeDepth = -TransformWorldToView(sceneWorld).z;
                return float4(nearest.rgb, step(nearest.a, sceneEyeDepth + max(0.02, nearest.a * 0.001)));
            }
            ENDHLSL
        }
    }
}
