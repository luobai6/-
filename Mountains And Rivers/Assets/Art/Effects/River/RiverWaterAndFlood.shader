Shader "ShanheWeijin/RiverWaterAndFlood"
{
    Properties
    {
        _ControlMap ("河道控制图（R河面 G高度 B洪水 A湿润）", 2D) = "black" {}
        [HDR] _WaterColor ("平常水色", Color) = (0.40, 0.62, 0.66, 1)
        [HDR] _FloodColor ("洪水颜色", Color) = (0.46, 0.38, 0.25, 1)
        [HDR] _FoamColor ("岸边水沫颜色", Color) = (0.83, 0.84, 0.73, 1)
        _Opacity ("水面透明度", Range(0, 1)) = 0.42
        _FloodProgress ("洪水扩张进度", Range(0, 1)) = 0
        _FloodSoftness ("洪水边缘柔和度", Range(0.005, 0.2)) = 0.045
        _Turbidity ("浑浊程度", Range(0, 1)) = 0.35
        _FlowDirection ("流动方向（XY）", Vector) = (0, -1, 0, 0)
        _FlowSpeed ("流动速度", Range(0, 0.5)) = 0.06
        _LargeRippleScale ("大波纹尺度", Range(1, 20)) = 5
        _SmallRippleScale ("细波纹尺度", Range(2, 40)) = 14
        _RippleStrength ("波纹明暗强度", Range(0, 0.5)) = 0.12
        _FoamWidth ("岸边水沫宽度", Range(0.01, 0.5)) = 0.14
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
            Name "河水与洪水"
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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_ControlMap);
            SAMPLER(sampler_ControlMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _ControlMap_ST;
                half4 _WaterColor;
                half4 _FloodColor;
                half4 _FoamColor;
                half _Opacity;
                half _FloodProgress;
                half _FloodSoftness;
                half _Turbidity;
                float4 _FlowDirection;
                half _FlowSpeed;
                half _LargeRippleScale;
                half _SmallRippleScale;
                half _RippleStrength;
                half _FoamWidth;
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
                output.uv = TRANSFORM_TEX(input.uv, _ControlMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 control = SAMPLE_TEXTURE2D(_ControlMap, sampler_ControlMap, input.uv);
                half normalRiverMask = control.r;

                // B 通道由河心向外递减；进度越高，洪水阈值越低，水面逐渐漫出河槽。
                half floodThreshold = 1.0h - _FloodProgress;
                half floodMask = smoothstep(
                    floodThreshold - _FloodSoftness,
                    floodThreshold + _FloodSoftness,
                    control.b
                ) * step(0.001h, _FloodProgress);
                half waterMask = max(normalRiverMask, floodMask);

                float2 flowDirection = _FlowDirection.xy;
                flowDirection /= max(length(flowDirection), 0.001);
                float2 flowingUV = input.uv + flowDirection * (_Time.y * _FlowSpeed);
                float largeRipple = ValueNoise(flowingUV * _LargeRippleScale);
                float smallRipple = ValueNoise(flowingUV * _SmallRippleScale - _Time.y * _FlowSpeed * 0.37);
                half ripple = (largeRipple * 0.7 + smallRipple * 0.3 - 0.5) * _RippleStrength;

                half floodBlend = saturate(_FloodProgress * (0.65h + _Turbidity * 0.35h));
                half3 waterColor = lerp(_WaterColor.rgb, _FloodColor.rgb, floodBlend);
                waterColor *= 1.0h + ripple;

                half edgeBand = smoothstep(0.01h, _FoamWidth, waterMask)
                              * (1.0h - smoothstep(_FoamWidth, _FoamWidth * 2.5h, waterMask));
                half foamMovement = saturate(largeRipple * 0.7h + 0.15h);
                waterColor = lerp(waterColor, _FoamColor.rgb, edgeBand * foamMovement);

                // A 通道使刚被洪水覆盖的岸地略显湿暗，避免扩张边缘像硬贴片。
                half wetBank = control.a * _FloodProgress * (1.0h - normalRiverMask);
                waterColor = lerp(waterColor, _FloodColor.rgb * 0.72h, wetBank * _Turbidity * 0.35h);

                half alpha = waterMask * _Opacity;
                alpha += edgeBand * foamMovement * 0.10h;
                clip(alpha - 0.002h);
                return half4(waterColor, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
