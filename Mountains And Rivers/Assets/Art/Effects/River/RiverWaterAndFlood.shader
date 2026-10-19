Shader "ShanheWeijin/RiverWaterAndFlood"
{
    Properties
    {
        _ControlMap ("河道控制图（R河面 G高度 B洪水 A湿润）", 2D) = "black" {}
        _AdvanceMap ("正向水头推进遮罩（黑色先到 白色后到）", 2D) = "white" {}
        _ReverseAdvanceMap ("反向水头推进遮罩（黑色先到 白色后到）", 2D) = "white" {}
        [HDR] _WaterColor ("平常水色", Color) = (0.40, 0.62, 0.66, 1)
        [HDR] _FloodColor ("洪水颜色", Color) = (0.46, 0.38, 0.25, 1)
        [HDR] _FoamColor ("岸边水沫颜色", Color) = (0.83, 0.84, 0.73, 1)
        _Opacity ("水面透明度", Range(0, 1)) = 0.42
        _FloodArrivalProgress ("洪水到达进度", Range(0, 1)) = 1
        _ArrivalSoftness ("水头边缘柔和度", Range(0.001, 0.2)) = 0.04
        [Toggle] _ReverseAdvance ("反向推进", Float) = 0
        _FloodProgress ("洪水扩张进度", Range(0, 1)) = 0
        _FloodSoftness ("洪水边缘柔和度", Range(0.005, 0.2)) = 0.045
        _MaxFloodExtent ("最大漫溢范围", Range(0, 1)) = 1
        _Turbidity ("浑浊程度", Range(0, 1)) = 0.35
        _FlowDirection ("流动方向（XY）", Vector) = (0, -1, 0, 0)
        _FlowSpeed ("流动速度", Range(0, 0.5)) = 0.06
        _LargeRippleScale ("大波纹尺度", Range(1, 20)) = 5
        _SmallRippleScale ("细波纹尺度", Range(2, 40)) = 14
        _RippleStrength ("波纹明暗强度", Range(0, 0.5)) = 0.12
        _FoamWidth ("岸边水沫宽度", Range(0.01, 0.5)) = 0.14
        [Toggle] _FloodWaveEnabled ("启用鼓起洪水浪头", Float) = 1
        _FloodWaveHeight ("浪头高度（局部空间）", Range(0, 1)) = 0.35
        _FloodWaveWidth ("浪头前后宽度", Range(0.005, 0.2)) = 0.08
        _FloodWaveFoamStrength ("浪头水沫强度", Range(0, 2)) = 1
        _OuterBankImpactStrength ("弯道外岸冲击强度", Range(0, 2)) = 0
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
            // 顶点阶段需要读取推进图，因此使用 Shader Model 3.0。
            #pragma target 3.0
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
                half waveBand : TEXCOORD1;
                half waveNoise : TEXCOORD2;
            };

            TEXTURE2D(_ControlMap);
            SAMPLER(sampler_ControlMap);
            TEXTURE2D(_AdvanceMap);
            SAMPLER(sampler_AdvanceMap);
            TEXTURE2D(_ReverseAdvanceMap);
            SAMPLER(sampler_ReverseAdvanceMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _ControlMap_ST;
                half4 _WaterColor;
                half4 _FloodColor;
                half4 _FoamColor;
                half _Opacity;
                half _FloodArrivalProgress;
                half _ArrivalSoftness;
                half _ReverseAdvance;
                half _FloodProgress;
                half _FloodSoftness;
                half _MaxFloodExtent;
                half _Turbidity;
                float4 _FlowDirection;
                half _FlowSpeed;
                half _LargeRippleScale;
                half _SmallRippleScale;
                half _RippleStrength;
                half _FoamWidth;
                half _FloodWaveEnabled;
                half _FloodWaveHeight;
                half _FloodWaveWidth;
                half _FloodWaveFoamStrength;
                half _OuterBankImpactStrength;
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
                float2 uv = TRANSFORM_TEX(input.uv, _ControlMap);
                half normalRiverMask = SAMPLE_TEXTURE2D_LOD(_ControlMap, sampler_ControlMap, uv, 0).r;
                half forwardAdvanceValue = SAMPLE_TEXTURE2D_LOD(_AdvanceMap, sampler_AdvanceMap, uv, 0).r;
                half reverseAdvanceValue = SAMPLE_TEXTURE2D_LOD(_ReverseAdvanceMap, sampler_ReverseAdvanceMap, uv, 0).r;
                half advanceValue = lerp(forwardAdvanceValue, reverseAdvanceValue, saturate(_ReverseAdvance));

                // 浪头只存在于洪水推进边界；进度为 0 或 1 时自动收起，
                // 避免在河道入口或出口长时间留下一道白线。
                half waveActive = step(0.001h, _FloodArrivalProgress)
                                * (1.0h - step(0.999h, _FloodArrivalProgress))
                                * step(0.5h, _FloodWaveEnabled);
                half waveDistance = abs(advanceValue - _FloodArrivalProgress);
                half waveBand = saturate(1.0h - waveDistance / max(_FloodWaveWidth, 0.001h));
                waveBand = waveBand * waveBand * (3.0h - 2.0h * waveBand);

                // 两组空间噪声让浪脊产生断续和高低变化，但保留横跨河宽的主体。
                half coarseNoise = ValueNoise(uv * 9.0 + _Time.y * float2(0.19, -0.13));
                half fineNoise = ValueNoise(uv * 23.0 - _Time.y * float2(0.11, 0.17));
                half waveNoise = saturate(0.48h + coarseNoise * 0.38h + fineNoise * 0.24h);
                half brokenCrest = smoothstep(0.28h, 0.62h, waveNoise);
                half finalWave = waveBand * normalRiverMask * waveActive * brokenCrest;

                float3 displacedPosition = input.positionOS.xyz;
                displacedPosition.y += finalWave * _FloodWaveHeight;
                output.positionCS = TransformObjectToHClip(displacedPosition);
                output.uv = uv;
                output.waveBand = finalWave;
                output.waveNoise = waveNoise;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 control = SAMPLE_TEXTURE2D(_ControlMap, sampler_ControlMap, input.uv);
                half normalRiverMask = control.r;

                // 正反方向分别使用独立遮罩，避免简单黑白反相导致弯道水头形状失真。
                // 两张遮罩都遵循黑色先到、白色后到；到达进度不负责向两岸扩张。
                half forwardAdvanceValue = SAMPLE_TEXTURE2D(_AdvanceMap, sampler_AdvanceMap, input.uv).r;
                half reverseAdvanceValue = SAMPLE_TEXTURE2D(_ReverseAdvanceMap, sampler_ReverseAdvanceMap, input.uv).r;
                half advanceValue = lerp(forwardAdvanceValue, reverseAdvanceValue, saturate(_ReverseAdvance));
                half arrivalMask = (1.0h - smoothstep(
                    _FloodArrivalProgress,
                    _FloodArrivalProgress + _ArrivalSoftness,
                    advanceValue
                )) * step(0.001h, _FloodArrivalProgress);

                // B 通道由河心向外递减。“洪水扩张进度”控制当前动画位置，
                // “最大漫溢范围”限制最终能够到达的最外层，两者取更严格的阈值。
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

                float2 flowDirection = _FlowDirection.xy;
                flowDirection /= max(length(flowDirection), 0.001);
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

                // 洪水前锋的鼓起浪脊。亮沫集中在顶部，噪声让它在河宽方向上参差断续。
                half crestFoam = saturate(input.waveBand * (0.72h + input.waveNoise * 0.45h) * _FloodWaveFoamStrength);
                waterColor = lerp(waterColor, _FoamColor.rgb * 1.08h, crestFoam);

                // 推进方向与河岸外法线越一致，说明水头越正面挤向该岸。
                // 这会自动把额外水沫保留在转弯外岸，而不是同时点亮两侧。
                // 直道材质默认将强度设为 0，不会改变现有效果。
                float2 advanceGradient = float2(ddx(advanceValue), ddy(advanceValue));
                float2 inwardGradient = float2(ddx(normalRiverMask), ddy(normalRiverMask));
                float2 downstreamDirection = normalize(advanceGradient + float2(0.00001, 0.00001));
                float2 outwardDirection = -normalize(inwardGradient + float2(0.00001, 0.00001));
                half outwardAlignment = smoothstep(0.15h, 0.75h, saturate(dot(downstreamDirection, outwardDirection)));
                half shorelineImpact = edgeBand * input.waveBand * outwardAlignment * _OuterBankImpactStrength;
                waterColor = lerp(waterColor, _FoamColor.rgb, saturate(shorelineImpact));

                // A 通道使刚被洪水覆盖的岸地略显湿暗，避免扩张边缘像硬贴片。
                half wetBank = control.a * _FloodProgress * arrivalMask * (1.0h - normalRiverMask);
                waterColor = lerp(waterColor, _FloodColor.rgb * 0.72h, wetBank * _Turbidity * 0.35h);

                half alpha = waterMask * _Opacity;
                alpha += edgeBand * foamMovement * 0.10h;
                alpha += crestFoam * 0.28h + shorelineImpact * 0.16h;
                clip(alpha - 0.002h);
                return half4(waterColor, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
