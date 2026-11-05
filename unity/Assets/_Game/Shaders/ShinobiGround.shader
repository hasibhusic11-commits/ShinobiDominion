// The village ground. Vertex colour holds blend weights: red = dirt, green = stone, blue = dark
// moss. Grass is what remains. No terrain height, so placement is unaffected.
Shader "ShinobiDominion/Ground"
{
    Properties
    {
        _DetailArray ("Detail Array", 2DArray) = "white" {}
        _GrassColor ("Grass", Color) = (0.30, 0.47, 0.22, 1)
        _GrassColorB ("Grass Variation", Color) = (0.40, 0.54, 0.25, 1)
        _DirtColor ("Dirt", Color) = (0.56, 0.47, 0.35, 1)
        _StoneColor ("Stone", Color) = (0.60, 0.59, 0.56, 1)
        _MossColor ("Moss", Color) = (0.24, 0.38, 0.22, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.52, 0.58, 0.72, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_DetailArray);
            SAMPLER(sampler_DetailArray);

            CBUFFER_START(UnityPerMaterial)
                half4 _GrassColor;
                half4 _GrassColorB;
                half4 _DirtColor;
                half4 _StoneColor;
                half4 _MossColor;
                half4 _ShadowTint;
            CBUFFER_END

            // Layer numbers must match ArtSurface in WorldRendering.cs.
            #define LAYER_STONE 1
            #define LAYER_GRASS 5
            #define LAYER_DIRT 6

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                return output;
            }

            half Sample(float2 uv, int layer)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_DetailArray, sampler_DetailArray, uv, layer).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionWS.xz * 0.5;
                half grassDetail = Sample(uv, LAYER_GRASS);
                // A second, much larger sample stops the tiling reading as a pattern.
                half macro = Sample(input.positionWS.xz * 0.041 + 0.37, LAYER_GRASS);
                half macroB = Sample(input.positionWS.zx * 0.017, LAYER_DIRT);

                half3 grass = lerp(_GrassColor.rgb, _GrassColorB.rgb, saturate((macro - 0.8) * 2.0 + (macroB - 0.8) * 2.0 + 0.5));
                grass *= grassDetail;

                half3 color = grass;
                color = lerp(color, _MossColor.rgb * grassDetail, input.color.b);

                // Sharpen blends with the texture so dirt edges look worn in, not airbrushed.
                half dirtMask = saturate((input.color.r + (grassDetail - 0.85) * 0.5 - 0.3) * 2.2);
                color = lerp(color, _DirtColor.rgb * Sample(uv * 0.8, LAYER_DIRT), dirtMask);
                half stoneMask = saturate((input.color.g - 0.4) * 5.0);
                color = lerp(color, _StoneColor.rgb * Sample(uv * 0.6, LAYER_STONE), stoneMask);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(0.42, 1.0, light.shadowAttenuation);
                color *= lerp(_ShadowTint.rgb, light.color, shadow);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
