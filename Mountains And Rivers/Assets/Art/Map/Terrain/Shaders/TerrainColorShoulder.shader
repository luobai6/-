Shader "ShanheWeijin/TerrainColorShoulder"
{
    Properties
    {
        _TopColor ("顶面青绿色", Color) = (0.455, 0.541, 0.475, 1)
        _ShoulderColor ("坡肩青赭过渡色", Color) = (0.569, 0.518, 0.416, 1)
        _SideTopColor ("侧面上部深赭色", Color) = (0.557, 0.435, 0.314, 1)
        _SideBottomColor ("侧面下部浅赭色", Color) = (0.765, 0.655, 0.482, 1)
        _SideGradientBottom ("侧面渐变最低高度", Range(-0.5, 0.5)) = -0.25
        _SideGradientTop ("侧面渐变最高高度", Range(0, 1.5)) = 1
        _SideToShoulderEnd ("侧面转坡肩范围", Range(0.05, 0.75)) = 0.42
        _ShoulderToTopStart ("坡肩转顶面起点", Range(0.25, 0.95)) = 0.68
        _ShoulderToTopEnd ("坡肩转顶面终点", Range(0.45, 1)) = 0.94
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
            Name "浅石青地形前向表现"
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
                half3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _ShoulderColor;
                half4 _SideTopColor;
                half4 _SideBottomColor;
                half _SideGradientBottom;
                half _SideGradientTop;
                half _SideToShoulderEnd;
                half _ShoulderToTopStart;
                half _ShoulderToTopEnd;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positionInputs.positionWS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half upward = saturate(normalize(input.normalWS).y);
                half sideToShoulder = smoothstep(0.02h, _SideToShoulderEnd, upward);
                half shoulderToTop = smoothstep(_ShoulderToTopStart, _ShoulderToTopEnd, upward);
                half sideHeight = smoothstep(
                    _SideGradientBottom,
                    max(_SideGradientTop, _SideGradientBottom + 0.01h),
                    input.positionWS.y);
                half3 sideColor = lerp(_SideBottomColor.rgb, _SideTopColor.rgb, sideHeight);

                half3 color = lerp(sideColor, _ShoulderColor.rgb, sideToShoulder);
                color = lerp(color, _TopColor.rgb, shoulderToTop);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
