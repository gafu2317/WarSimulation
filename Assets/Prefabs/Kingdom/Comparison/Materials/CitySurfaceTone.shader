Shader "WarSimulation/City Surface Tone"
{
    Properties
    {
        [MainTexture] _BaseMap ("Surface detail", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint (RGB), saturation (A)", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 0.2
        _MetallicGlossMap ("Metallic and smoothness", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion strength", Range(0, 1)) = 1
        [HideInInspector] _SpecColor ("Specular", Color) = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _DetailAlbedoMapScale ("Detail", Float) = 1
        [HideInInspector] _DetailNormalMapScale ("Detail normal", Float) = 1
        [HideInInspector] _ClearCoatMask ("Coat", Float) = 0
        [HideInInspector] _ClearCoatSmoothness ("Coat smoothness", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            // Reuse Lit's buffer and depth passes; opacity is always one for masonry and timber.
            #define InitializeStandardLitSurfaceData InitializeUntonedSurface
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef InitializeStandardLitSurfaceData
            void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
            {
                InitializeUntonedSurface(uv, surface);
                half3 textureColour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half luminance = dot(textureColour, half3(0.2126, 0.7152, 0.0722));
                surface.albedo = lerp(luminance.xxx, textureColour, _BaseColor.a) * _BaseColor.rgb;
                surface.alpha = 1;
                surface.emission = 0;
            }
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
