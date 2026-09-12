Shader "WarSimulation/Stylized River"
{
    Properties
    {
        _BaseColor ("Water color", Color) = (0.025, 0.57, 0.68, 1)
        _RippleColor ("Ripple color", Color) = (0.32, 0.78, 0.82, 1)
        _FoamColor ("Flow streak color", Color) = (0.64, 0.9, 0.91, 1)
        _FlowSpeed ("Flow speed (meters/second, negative reverses)", Float) = 1.5
        _PatternScale ("Pattern size (meters)", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RippleColor;
                half4 _FoamColor;
                float _FlowSpeed;
                float _PatternScale;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 flow : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 flow : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.flow = input.flow;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.flow / max(abs(_PatternScale), 0.001);
                float travel = _Time.y * _FlowSpeed / max(abs(_PatternScale), 0.001);
                p.x -= travel;
                float phase = _Time.y * 0.65;
                float2 rippleUv = p * float2(4.5, 6.0);
                rippleUv += float2(
                    Noise(p * 2.0 + float2(phase * 0.23, -phase * 0.31)),
                    Noise(p * 2.0 + float2(-phase * 0.19, phase * 0.27) + 13.7)) * 0.8;
                float2 surfaceWarp = float2(
                    Noise(p * 1.3 + float2(phase * 0.21, -phase * 0.17)),
                    Noise(p * 1.3 + float2(-phase * 0.16, phase * 0.24) + 23.1)) - 0.5;
                float broad = Noise(p * float2(1.2, 2.5) + surfaceWarp * 1.4);
                float waveA = Noise(rippleUv + float2(phase * 0.18, phase * 0.35));
                float waveB = Noise(float2(rippleUv.x + rippleUv.y * 0.55,
                    rippleUv.y - rippleUv.x * 0.4) * 1.35 + float2(-phase * 0.24, -phase * 0.3) + 7.3);
                float ripples = waveA * 0.55 + waveB * 0.45;
                float edge = abs(ripples - 0.5);
                float aa = fwidth(edge);
                float ridge = 1.0 - smoothstep(0.01, 0.035 + aa, edge);
                float breaks = smoothstep(0.65, 0.85,
                    Noise(p * float2(5.0, 6.0) + float2(-phase * 0.3, phase * 0.4) + 53.0));
                ridge *= breaks;
                float2 fleckUv = p * float2(9.0, 12.0) + float2(phase * 0.35, -phase * 0.6) + 31.0;
                fleckUv += surfaceWarp * 2.0 + float2(
                    Noise(p * 5.0 + float2(-phase * 0.4, phase * 0.3)),
                    Noise(p * 5.0 + float2(phase * 0.3, phase * 0.45) + 41.7)) * 0.8;
                float flecks = Noise(fleckUv);
                float smallRipples = smoothstep(0.65, 0.82 + fwidth(flecks), flecks);
                float highlights = smoothstep(0.4, 0.8, waveB);
                half3 color = lerp(_BaseColor.rgb * 0.9, _RippleColor.rgb,
                    broad * 0.25 + ripples * 0.2);
                color = lerp(color, _RippleColor.rgb, ridge * 0.55);
                color = lerp(color, _FoamColor.rgb,
                    ridge * highlights * 0.45 + smallRipples * 0.18);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
