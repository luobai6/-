Shader "ShanheWeijin/RiverBedParallax"
{
    Properties
    {
        _BaseMap ("河道颜色图", 2D) = "white" {}
        _ControlMap ("河道控制图（R河面 G高度 B洪水 A湿润）", 2D) = "white" {}
        [Toggle] _RotateBaseMap180 ("颜色图旋转180度", Float) = 0
        _Tint ("整体染色", Color) = (1, 1, 1, 1)
        _ParallaxStrength ("河床视差强度", Range(0, 0.06)) = 0.018
        _DepthDarkening ("深水压暗程度", Range(0, 0.5)) = 0.10
        _AlphaCutoff ("透明边缘裁切", Range(0, 0.2)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "河床视差"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirectionTS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_ControlMap);
            SAMPLER(sampler_ControlMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _RotateBaseMap180;
                half4 _Tint;
                half _ParallaxStrength;
                half _DepthDarkening;
                half _AlphaCutoff;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);

                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.viewDirectionTS = float3(
                    dot(viewDirectionWS, normalInputs.tangentWS),
                    dot(viewDirectionWS, normalInputs.bitangentWS),
                    dot(viewDirectionWS, normalInputs.normalWS)
                );
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirectionTS = normalize(input.viewDirectionTS);
                float denominator = max(abs(viewDirectionTS.z), 0.25);
                float2 parallaxDirection = viewDirectionTS.xy / denominator;

                // 三次轻量迭代足以给固定正交镜头制造河床下陷感，成本低于完整 POM。
                float2 displacedUV = input.uv;
                UNITY_UNROLL
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    half heightValue = SAMPLE_TEXTURE2D(_ControlMap, sampler_ControlMap, displacedUV).g;
                    half riverDepth = 1.0h - heightValue;
                    float2 targetUV = input.uv - parallaxDirection * riverDepth * _ParallaxStrength;
                    displacedUV = lerp(displacedUV, targetUV, 0.7);
                }

                // 彩图可独立旋转 180 度，不改变控制图、水面或洪水推进方向。
                float2 baseUV = lerp(displacedUV, 1.0 - displacedUV, _RotateBaseMap180);
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV) * _Tint;
                half heightValue = SAMPLE_TEXTURE2D(_ControlMap, sampler_ControlMap, displacedUV).g;
                half riverDepth = 1.0h - heightValue;
                baseColor.rgb *= 1.0h - riverDepth * _DepthDarkening;
                clip(baseColor.a - _AlphaCutoff);
                return baseColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
