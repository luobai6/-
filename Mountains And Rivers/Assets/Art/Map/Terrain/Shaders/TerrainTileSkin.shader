Shader "ShanheWeijin/TerrainTileSkin"
{
    Properties
    {
        [MainTexture] _TopMap ("顶面水墨纹理", 2D) = "white" {}
        _SideMap ("侧壁土层纹理", 2D) = "white" {}
        _EdgeMask ("手绘断续边线遮罩", 2D) = "white" {}
        [MainColor] _BaseColor ("地块综合色", Color) = (1, 1, 1, 1)
        _TopTint ("顶面色调", Color) = (1, 1, 1, 1)
        _SideTopTint ("侧壁上部深赭色", Color) = (0.557, 0.435, 0.314, 1)
        _SideBottomTint ("侧壁下部浅赭色", Color) = (0.765, 0.655, 0.482, 1)
        _EdgeTint ("顶侧青赭过渡色", Color) = (0.569, 0.518, 0.416, 1)
        _EdgeLineTint ("顶侧边缘线颜色", Color) = (0.282, 0.267, 0.220, 1)
        _WorldScale ("纹理世界尺寸", Range(0.25, 8)) = 2
        _SideGradientStrength ("侧壁赭色渐变强度", Range(0, 1)) = 1
        _EdgeWidth ("顶侧边缘带宽度", Range(0.02, 0.2)) = 0.09
        _EdgeLineWidth ("顶面墨线内收宽度", Range(0.005, 0.08)) = 0.018
        _VerticalEdgeLineWidth ("侧壁立边墨线宽度", Range(0.005, 0.05)) = 0.014
        _EdgeMaskWorldScale ("边线遮罩世界尺寸", Range(0.5, 4)) = 1.25
        _DefinitionTintStrength ("地块定义染色强度", Range(0, 1)) = 0.18
        [HideInInspector] _TopEdgeFlags ("顶面四方向边缘启用", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "地块前向表现"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            TEXTURE2D(_TopMap);
            SAMPLER(sampler_TopMap);
            TEXTURE2D(_SideMap);
            SAMPLER(sampler_SideMap);
            TEXTURE2D(_EdgeMask);
            SAMPLER(sampler_EdgeMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TopTint;
                half4 _SideTopTint;
                half4 _SideBottomTint;
                half4 _EdgeTint;
                half4 _EdgeLineTint;
                half _WorldScale;
                half _SideGradientStrength;
                half _EdgeWidth;
                half _EdgeLineWidth;
                half _VerticalEdgeLineWidth;
                half _EdgeMaskWorldScale;
                half _DefinitionTintStrength;
                half4 _TopEdgeFlags;
            CBUFFER_END

            float2 MirrorRepeat(float2 uv)
            {
                float2 doubled = frac(uv * 0.5) * 2.0;
                return 1.0 - abs(doubled - 1.0);
            }

            half SampleEdgeStroke(float edgeCoordinate, half lineBand01, half directionOffset, float edgeMaskScale)
            {
                float edgeMaskV = lerp(0.44, 0.60, lineBand01);
                float2 edgeMaskUV = float2(frac(edgeCoordinate / edgeMaskScale + directionOffset), edgeMaskV);
                half strokeMask = SAMPLE_TEXTURE2D(_EdgeMask, sampler_EdgeMask, edgeMaskUV).r;
                strokeMask = max(strokeMask,
                    SAMPLE_TEXTURE2D(_EdgeMask, sampler_EdgeMask, edgeMaskUV + float2(0.03, 0)).r);
                strokeMask = max(strokeMask,
                    SAMPLE_TEXTURE2D(_EdgeMask, sampler_EdgeMask, edgeMaskUV - float2(0.03, 0)).r);
                return smoothstep(0.05h, 0.30h, strokeMask);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float worldScale = max((float)_WorldScale, 0.01);

                float2 topUV = MirrorRepeat(input.positionWS.xz / worldScale);
                float2 sideBaseUV = abs(normalWS.x) > abs(normalWS.z)
                    ? input.positionWS.zy / worldScale
                    : input.positionWS.xy / worldScale;
                float2 sideUV = MirrorRepeat(sideBaseUV);

                half3 topColor = SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, topUV).rgb * _TopTint.rgb;
                half3 sideSample = SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, sideUV).rgb;

                // Unity 内置 Cube 的模型空间侧壁高度为 -0.5 到 0.5。
                // 使用模型空间而不是世界绝对高度，保证一级、二级地块都保持上深下浅。
                half sideHeight01 = saturate(input.positionOS.y + 0.5);
                half3 sideGradient = lerp(_SideBottomTint.rgb, _SideTopTint.rgb, sideHeight01);
                half sideLuminance = dot(sideSample, half3(0.299, 0.587, 0.114));
                half3 colorizedSide = sideGradient * lerp(0.78h, 1.18h, sideLuminance);
                half3 sideColor = lerp(sideSample, colorizedSide, _SideGradientStrength);

                // 侧壁最上方先承接顶面青绿色，再落入赭石色，形成绘画式顶侧过渡。
                // 顶面纹理使用同一世界坐标采样，过渡带沿地块连续，不额外生成程序噪声。
                half edgeStart = 1.0h - _EdgeWidth + (sideLuminance - 0.5h) * _EdgeWidth * 0.22h;
                half edgeBlendMask = smoothstep(edgeStart, 1.0h, sideHeight01);
                half3 transitionColor = lerp(_EdgeTint.rgb, topColor, 0.62h);
                transitionColor *= lerp(0.94h, 1.06h, sideLuminance);
                half3 edgedSideColor = lerp(sideColor, transitionColor, edgeBlendMask * 0.88h);

                // 墨线放在顶面内侧。生成器只启用邻居缺失或更低的方向，
                // 因此同高度相邻地块仍保持连续，不会出现九宫格描边。
                float edgeMaskScale = max((float)_EdgeMaskWorldScale, 0.01);
                half topEdgeWidth = max(_EdgeLineWidth, 0.001h);
                half distancePositiveX = 0.5h - input.positionOS.x;
                half distanceNegativeX = 0.5h + input.positionOS.x;
                half distancePositiveZ = 0.5h - input.positionOS.z;
                half distanceNegativeZ = 0.5h + input.positionOS.z;

                half positiveXMask = (1.0h - smoothstep(topEdgeWidth * 0.82h, topEdgeWidth, distancePositiveX))
                    * SampleEdgeStroke(input.positionWS.z, saturate(distancePositiveX / topEdgeWidth), 0.11h, edgeMaskScale)
                    * _TopEdgeFlags.x;
                half negativeXMask = (1.0h - smoothstep(topEdgeWidth * 0.82h, topEdgeWidth, distanceNegativeX))
                    * SampleEdgeStroke(input.positionWS.z, saturate(distanceNegativeX / topEdgeWidth), 0.37h, edgeMaskScale)
                    * _TopEdgeFlags.y;
                half positiveZMask = (1.0h - smoothstep(topEdgeWidth * 0.82h, topEdgeWidth, distancePositiveZ))
                    * SampleEdgeStroke(input.positionWS.x, saturate(distancePositiveZ / topEdgeWidth), 0.59h, edgeMaskScale)
                    * _TopEdgeFlags.z;
                half negativeZMask = (1.0h - smoothstep(topEdgeWidth * 0.82h, topEdgeWidth, distanceNegativeZ))
                    * SampleEdgeStroke(input.positionWS.x, saturate(distanceNegativeZ / topEdgeWidth), 0.83h, edgeMaskScale)
                    * _TopEdgeFlags.w;

                half topEdgeInkMask = saturate(max(max(positiveXMask, negativeXMask), max(positiveZMask, negativeZMask)));
                half topLuminance = dot(topColor, half3(0.299, 0.587, 0.114));
                half3 topEdgeLineColor = _EdgeLineTint.rgb * lerp(0.92h, 1.08h, topLuminance);
                half3 edgedTopColor = lerp(topColor, topEdgeLineColor, topEdgeInkMask * 0.88h);

                // 只有两个真实外露方向相交时才画侧壁立边，避免连续崖壁内部出现逐格竖线。
                half cornerPositiveXPositiveZ = _TopEdgeFlags.x * _TopEdgeFlags.z;
                half cornerPositiveXNegativeZ = _TopEdgeFlags.x * _TopEdgeFlags.w;
                half cornerNegativeXPositiveZ = _TopEdgeFlags.y * _TopEdgeFlags.z;
                half cornerNegativeXNegativeZ = _TopEdgeFlags.y * _TopEdgeFlags.w;

                half positiveXFace = step(0.75h, normalWS.x);
                half negativeXFace = step(0.75h, -normalWS.x);
                half positiveZFace = step(0.75h, normalWS.z);
                half negativeZFace = step(0.75h, -normalWS.z);
                half verticalWidth = max(_VerticalEdgeLineWidth, 0.001h);
                float verticalCoordinate = sideHeight01 * edgeMaskScale;

                half positiveXPositiveZLine = positiveXFace * cornerPositiveXPositiveZ
                    * (1.0h - smoothstep(verticalWidth * 0.82h, verticalWidth, distancePositiveZ))
                    * SampleEdgeStroke(verticalCoordinate, saturate(distancePositiveZ / verticalWidth), 0.07h, edgeMaskScale);
                half positiveXNegativeZLine = positiveXFace * cornerPositiveXNegativeZ
                    * (1.0h - smoothstep(verticalWidth * 0.82h, verticalWidth, distanceNegativeZ))
                    * SampleEdgeStroke(verticalCoordinate, saturate(distanceNegativeZ / verticalWidth), 0.29h, edgeMaskScale);
                half negativeXPositiveZLine = negativeXFace * cornerNegativeXPositiveZ
                    * (1.0h - smoothstep(verticalWidth * 0.82h, verticalWidth, distancePositiveZ))
                    * SampleEdgeStroke(verticalCoordinate, saturate(distancePositiveZ / verticalWidth), 0.53h, edgeMaskScale);
                half negativeXNegativeZLine = negativeXFace * cornerNegativeXNegativeZ
                    * (1.0h - smoothstep(verticalWidth * 0.82h, verticalWidth, distanceNegativeZ))
                    * SampleEdgeStroke(verticalCoordinate, saturate(distanceNegativeZ / verticalWidth), 0.79h, edgeMaskScale);

                half positiveZPositiveXLine = positiveZFace * cornerPositiveXPositiveZ
                    * (1.0h - smoothstep(verticalWidth * 0.82h, verticalWidth, distancePositiveX))
                    * SampleEdgeStroke(verticalCoordinate, saturate(distancePositiveX / verticalWidth), 0.13h, edgeMaskScale);
                half positiveZNegativeXLine = positiveZFace * cornerNegativeXPositiveZ
                    * (1.0h - smoothstep(verticalWidth * 0.82h, verticalWidth, distanceNegativeX))
                    * SampleEdgeStroke(verticalCoordinate, saturate(distanceNegativeX / verticalWidth), 0.41h, edgeMaskScale);
                half negativeZPositiveXLine = negativeZFace * cornerPositiveXNegativeZ
                    * (1.0h - smoothstep(verticalWidth * 0.82h, verticalWidth, distancePositiveX))
                    * SampleEdgeStroke(verticalCoordinate, saturate(distancePositiveX / verticalWidth), 0.67h, edgeMaskScale);
                half negativeZNegativeXLine = negativeZFace * cornerNegativeXNegativeZ
                    * (1.0h - smoothstep(verticalWidth * 0.82h, verticalWidth, distanceNegativeX))
                    * SampleEdgeStroke(verticalCoordinate, saturate(distanceNegativeX / verticalWidth), 0.91h, edgeMaskScale);

                half verticalEdgeInkMask = saturate(max(
                    max(max(positiveXPositiveZLine, positiveXNegativeZLine), max(negativeXPositiveZLine, negativeXNegativeZLine)),
                    max(max(positiveZPositiveXLine, positiveZNegativeXLine), max(negativeZPositiveXLine, negativeZNegativeXLine))));
                half3 verticalEdgeLineColor = _EdgeLineTint.rgb * lerp(0.94h, 1.06h, sideLuminance);
                edgedSideColor = lerp(edgedSideColor, verticalEdgeLineColor, verticalEdgeInkMask * 0.72h);

                // 普通地块只允许平顶和垂直侧壁，不再模拟圆缓坡肩。
                half topFactor = step(0.75h, normalWS.y);
                half3 definitionTint = lerp(half3(1, 1, 1), _BaseColor.rgb, _DefinitionTintStrength);
                half3 finalColor = lerp(edgedSideColor, edgedTopColor, topFactor) * definitionTint;
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}
