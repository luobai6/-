Shader "ShanheWeijin/CalmBreathingFog"
{
    Properties
    {
        [HDR] _FogColor ("雾色", Color) = (0.82, 0.86, 0.80, 1)
        _Opacity ("整体透明度", Range(0, 1)) = 0.28
        _Density ("雾密度", Range(0, 1)) = 0.48
        _Feather ("雾形羽化", Range(0.01, 0.5)) = 0.22
        _EdgeFeather ("边缘羽化", Range(0.01, 0.49)) = 0.18

        _NoiseScale1 ("主雾形尺度", Float) = 2.2
        _NoiseScale2 ("细节雾形尺度", Float) = 5.5
        _Speed1 ("主雾流速（XY）", Vector) = (0.003, 0.0006, 0, 0)
        _Speed2 ("细节雾流速（XY）", Vector) = (-0.002, 0.0009, 0, 0)

        _BreathStrength ("呼吸强度", Range(0, 0.5)) = 0.14
        _BreathCyclesPerSecond ("呼吸频率（次／秒）", Range(0.005, 0.1)) = 0.03

        [HideInInspector] _LayerPhase ("Layer Phase", Float) = 0
        [HideInInspector] _SpeedMultiplier ("Speed Multiplier", Float) = 1
        [HideInInspector] _OpacityMultiplier ("Opacity Multiplier", Float) = 1

        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("深度测试", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half _Opacity;
                half _Density;
                half _Feather;
                half _EdgeFeather;
                float _NoiseScale1;
                float _NoiseScale2;
                float4 _Speed1;
                float4 _Speed2;
                half _BreathStrength;
                float _BreathCyclesPerSecond;
                float _LayerPhase;
                float _SpeedMultiplier;
                half _OpacityMultiplier;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 blend = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float speedMultiplier = max(abs(_SpeedMultiplier), 0.0001);
                float time = _Time.y * speedMultiplier;

                float2 uv1 = input.uv * _NoiseScale1 + _Speed1.xy * time + _LayerPhase;
                float2 uv2 = input.uv * _NoiseScale2 + _Speed2.xy * time - _LayerPhase * 0.73;
                float broad = ValueNoise(uv1);
                float detail = ValueNoise(uv2);
                float noise = broad * 0.72 + detail * 0.28;

                float lower = _Density - _Feather;
                float upper = _Density + _Feather;
                float shape = smoothstep(lower, upper, noise);

                float edgeX = smoothstep(0.0, _EdgeFeather, input.uv.x)
                            * smoothstep(0.0, _EdgeFeather, 1.0 - input.uv.x);
                float edgeY = smoothstep(0.0, _EdgeFeather, input.uv.y)
                            * smoothstep(0.0, _EdgeFeather, 1.0 - input.uv.y);
                float edgeFade = edgeX * edgeY;

                float breath = 1.0 + sin(
                    time * _BreathCyclesPerSecond * 6.28318530718
                    + _LayerPhase * 6.28318530718
                ) * _BreathStrength;

                half alpha = saturate(shape * edgeFade * _Opacity * _OpacityMultiplier * breath);
                return half4(_FogColor.rgb, alpha * _FogColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
