Shader "ShanheWeijin/ForkRiverWaterAndFlood"
{
    Properties
    {
        _ControlMap ("河道控制图（R河面 G高度 B洪水 A湿润）", 2D) = "black" {}
        _NorthInAdvanceMap ("北侧到中心遮罩（RGB先后 A有效范围）", 2D) = "white" {}
        _NorthOutAdvanceMap ("中心到北侧遮罩（RGB先后 A有效范围）", 2D) = "white" {}
        _EastInAdvanceMap ("东侧到中心遮罩（RGB先后 A有效范围）", 2D) = "white" {}
        _EastOutAdvanceMap ("中心到东侧遮罩（RGB先后 A有效范围）", 2D) = "white" {}
        _SouthInAdvanceMap ("南侧到中心遮罩（RGB先后 A有效范围）", 2D) = "white" {}
        _SouthOutAdvanceMap ("中心到南侧遮罩（RGB先后 A有效范围）", 2D) = "white" {}
        [HDR] _WaterColor ("平常水色", Color) = (0.40, 0.62, 0.66, 1)
        [HDR] _FloodColor ("洪水颜色", Color) = (0.46, 0.38, 0.25, 1)
        [HDR] _FoamColor ("岸边水沫颜色", Color) = (0.83, 0.84, 0.73, 1)
        _Opacity ("水面透明度", Range(0, 1)) = 0.42
        // ShaderLab 的 Enum 标签只使用 ASCII，避免中文标识符触发属性解析错误。
        // 对外调节仍通过 ForkRiverVisualController 的中文枚举完成。
        [Enum(North,0,East,1,South,2)] _IncomingPort ("来水端口（北0 东1 南2）", Float) = 0
        _EntryArrivalProgress ("入流推进进度", Range(0, 1)) = 0
        _NorthExitProgress ("北支路推进进度", Range(0, 1)) = 0
        _EastExitProgress ("东支路推进进度", Range(0, 1)) = 0
        _SouthExitProgress ("南支路推进进度", Range(0, 1)) = 0
        _ArrivalSoftness ("水头边缘柔和度", Range(0.001, 0.2)) = 0.04
        _FloodProgress ("洪水扩张进度", Range(0, 1)) = 0
        _FloodSoftness ("洪水边缘柔和度", Range(0.005, 0.2)) = 0.045
        _MaxFloodExtent ("最大漫溢范围", Range(0, 1)) = 1
        _Turbidity ("浑浊程度", Range(0, 1)) = 0.35
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
            Name "分叉河水与洪水"
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
            TEXTURE2D(_NorthInAdvanceMap);
            SAMPLER(sampler_NorthInAdvanceMap);
            TEXTURE2D(_NorthOutAdvanceMap);
            SAMPLER(sampler_NorthOutAdvanceMap);
            TEXTURE2D(_EastInAdvanceMap);
            SAMPLER(sampler_EastInAdvanceMap);
            TEXTURE2D(_EastOutAdvanceMap);
            SAMPLER(sampler_EastOutAdvanceMap);
            TEXTURE2D(_SouthInAdvanceMap);
            SAMPLER(sampler_SouthInAdvanceMap);
            TEXTURE2D(_SouthOutAdvanceMap);
            SAMPLER(sampler_SouthOutAdvanceMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _ControlMap_ST;
                half4 _WaterColor;
                half4 _FloodColor;
                half4 _FoamColor;
                half _Opacity;
                half _IncomingPort;
                half _EntryArrivalProgress;
                half _NorthExitProgress;
                half _EastExitProgress;
                half _SouthExitProgress;
                half _ArrivalSoftness;
                half _FloodProgress;
                half _FloodSoftness;
                half _MaxFloodExtent;
                half _Turbidity;
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

            half EvaluateArrival(half4 advanceData, half progress)
            {
                half timeMask = (1.0h - smoothstep(
                    progress,
                    progress + _ArrivalSoftness,
                    advanceData.r
                )) * step(0.001h, progress);
                return timeMask * advanceData.a;
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
                half4 northIn = SAMPLE_TEXTURE2D(_NorthInAdvanceMap, sampler_NorthInAdvanceMap, input.uv);
                half4 northOut = SAMPLE_TEXTURE2D(_NorthOutAdvanceMap, sampler_NorthOutAdvanceMap, input.uv);
                half4 eastIn = SAMPLE_TEXTURE2D(_EastInAdvanceMap, sampler_EastInAdvanceMap, input.uv);
                half4 eastOut = SAMPLE_TEXTURE2D(_EastOutAdvanceMap, sampler_EastOutAdvanceMap, input.uv);
                half4 southIn = SAMPLE_TEXTURE2D(_SouthInAdvanceMap, sampler_SouthInAdvanceMap, input.uv);
                half4 southOut = SAMPLE_TEXTURE2D(_SouthOutAdvanceMap, sampler_SouthOutAdvanceMap, input.uv);

                half northSelected = 1.0h - step(0.5h, _IncomingPort);
                half eastSelected = step(0.5h, _IncomingPort) * (1.0h - step(1.5h, _IncomingPort));
                half southSelected = step(1.5h, _IncomingPort);

                half entryArrival = EvaluateArrival(northIn, _EntryArrivalProgress) * northSelected
                                  + EvaluateArrival(eastIn, _EntryArrivalProgress) * eastSelected
                                  + EvaluateArrival(southIn, _EntryArrivalProgress) * southSelected;
                half northArrival = EvaluateArrival(northOut, _NorthExitProgress) * (1.0h - northSelected);
                half eastArrival = EvaluateArrival(eastOut, _EastExitProgress) * (1.0h - eastSelected);
                half southArrival = EvaluateArrival(southOut, _SouthExitProgress) * (1.0h - southSelected);
                half arrivalMask = max(entryArrival, max(northArrival, max(eastArrival, southArrival)));

                // 每张推进图的 Alpha 只标记它负责的河段，防止支路之间互相点亮。
                half normalRiverMask = control.r;
                half progressThreshold = 1.0h - _FloodProgress;
                half extentThreshold = 1.0h - _MaxFloodExtent;
                half floodThreshold = max(progressThreshold, extentThreshold);
                half floodAreaMask = step(0.001h, control.b) * step(0.001h, _MaxFloodExtent);
                half floodMask = smoothstep(
                    floodThreshold - _FloodSoftness,
                    floodThreshold + _FloodSoftness,
                    control.b
                ) * floodAreaMask * step(0.001h, _FloodProgress) * arrivalMask;
                half waterMask = max(normalRiverMask, floodMask);

                // 依据河段 Alpha 混合局部水流方向；旋转 Prefab 时流向会随贴图一起旋转。
                half northWeight = max(northIn.a, northOut.a);
                half eastWeight = max(eastIn.a, eastOut.a);
                half southWeight = max(southIn.a, southOut.a);
                float2 northDirection = lerp(float2(0.0, 1.0), float2(0.0, -1.0), northSelected);
                float2 eastDirection = lerp(float2(1.0, 0.0), float2(-1.0, 0.0), eastSelected);
                float2 southDirection = lerp(float2(0.0, -1.0), float2(0.0, 1.0), southSelected);
                float2 flowDirection = northDirection * northWeight
                                     + eastDirection * eastWeight
                                     + southDirection * southWeight;
                flowDirection = normalize(flowDirection + float2(0.0001, 0.0001));
                float2 flowingUV = input.uv + flowDirection * (_Time.y * _FlowSpeed);
                float largeRipple = ValueNoise(flowingUV * _LargeRippleScale);
                float smallRipple = ValueNoise(flowingUV * _SmallRippleScale - _Time.y * _FlowSpeed * 0.37);
                half ripple = (largeRipple * 0.7 + smallRipple * 0.3 - 0.5) * _RippleStrength;

                half floodBlend = saturate(_FloodProgress * (0.65h + _Turbidity * 0.35h)) * arrivalMask;
                half3 waterColor = lerp(_WaterColor.rgb, _FloodColor.rgb, floodBlend);
                waterColor *= 1.0h + ripple;

                half edgeBand = smoothstep(0.01h, _FoamWidth, waterMask)
                              * (1.0h - smoothstep(_FoamWidth, _FoamWidth * 2.5h, waterMask));
                half foamMovement = saturate(largeRipple * 0.7h + 0.15h);
                waterColor = lerp(waterColor, _FoamColor.rgb, edgeBand * foamMovement);

                half wetBank = control.a * _FloodProgress * arrivalMask * (1.0h - normalRiverMask);
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
