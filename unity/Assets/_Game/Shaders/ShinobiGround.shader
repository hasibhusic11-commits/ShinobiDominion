// The mountain and the village ground. Vertex colour holds blend weights: red = dirt, blue = dark
// moss. Grass is what remains. Rock and snow come from each pixel's height and slope, so their
// edges are smooth whatever the mesh's resolution. The map itself is flat, so placement
// is unaffected. Slopes are lit by the sun; flat ground comes out exactly as if unlit by slope.
Shader "ShinobiDominion/Ground"
{
    Properties
    {
        _DetailArray ("Detail Array", 2DArray) = "white" {}
        _GrassColor ("Grass", Color) = (0.30, 0.47, 0.22, 1)
        _GrassColorB ("Grass Variation", Color) = (0.40, 0.54, 0.25, 1)
        _DirtColor ("Dirt", Color) = (0.56, 0.47, 0.35, 1)
        _MossColor ("Moss", Color) = (0.24, 0.38, 0.22, 1)
        _RockColor ("Rock", Color) = (0.50, 0.51, 0.56, 1)
        _SnowColor ("Snow", Color) = (0.93, 0.95, 1.0, 1)
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
                half4 _MossColor;
                half4 _RockColor;
                half4 _SnowColor;
                half4 _ShadowTint;
            CBUFFER_END

            // Layer numbers must match ArtSurface in WorldRendering.cs.
            #define LAYER_GRASS 5
            #define LAYER_DIRT 6
            #define LAYER_ROCK 11
            #define LAYER_CRACKS 12

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
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
                // Darker clumps and sunlit drifts, at sizes between the blades and the big patches.
                half clumps = Sample(input.positionWS.zx * 0.13 + 0.61, LAYER_GRASS);
                grass *= lerp(0.88, 1.08, saturate((clumps - 0.82) * 5.0));
                grass *= grassDetail;

                half3 color = grass;
                color = lerp(color, _MossColor.rgb * grassDetail, input.color.b);

                // Sharpen blends with the texture so dirt edges look worn in, not airbrushed.
                half dirtMask = saturate((input.color.r + (grassDetail - 0.85) * 0.5 - 0.3) * 2.2);
                color = lerp(color, _DirtColor.rgb * Sample(uv * 0.8, LAYER_DIRT), dirtMask);

                // Mapped so it never stretches from this fixed camera, with no seam between faces.
                float3 normal = normalize(input.normalWS);
                float3 p = input.positionWS;
                float2 rockUv = float2(p.x - p.z, p.y + (p.x + p.z) * 0.5) * 0.1;
                half rockDetail = Sample(rockUv, LAYER_ROCK) * lerp(0.9, 1.04, Sample(rockUv * 3.7 + 0.17, LAYER_ROCK));
                half cracks = Sample(rockUv * 0.9 + 0.43, LAYER_CRACKS);
                // Large soft patches and faint layers give the rock form, without any fine noise.
                half patches = Sample(rockUv * 0.23 + 0.31, LAYER_ROCK);
                half layers = 0.5 + 0.5 * sin(p.y * 0.9 + patches * 6.0);
                half tone = lerp(0.68, 1.04, saturate((patches - 0.72) * 2.6)) * lerp(0.92, 1.03, layers);
                // Soft ledges: a shadow under each shelf of rock, on steep faces only.
                half broad = Sample(rockUv * 0.11 + 0.71, LAYER_ROCK);
                half ledgeWave = frac((p.y + patches * 9.0 + broad * 22.0) * 0.27);
                half ledge = lerp(0.8, 1.0, smoothstep(0.0, 0.4, ledgeWave));
                half ledgeFade = smoothstep(0.82, 0.98, broad + patches * 0.25 - 0.05);
                tone *= lerp(1.0, ledge, smoothstep(0.85, 0.65, normal.y) * ledgeFade);
                half3 rockTint = lerp(_RockColor.rgb, _RockColor.rgb * half3(1.12, 0.98, 0.84), saturate((macroB - 0.8) * 4.0));

                half wobble = (macro - 0.9) * 10.0 + (macroB - 0.9) * 6.0;
                half steep = smoothstep(0.76, 0.66, normal.y);
                half high = smoothstep(6.0, 9.0, p.y + wobble);
                half rockMask = max(steep, high);
                half3 rock = rockTint * rockDetail * tone * lerp(0.58, 1.0, cracks);

                // Moss settles in patches on the gentler rock, and only below the snow.
                half mossy = smoothstep(0.62, 0.84, normal.y) * smoothstep(0.78, 0.9, patches) * smoothstep(11.0, 7.0, p.y);
                rock = lerp(rock, _MossColor.rgb * rockDetail, mossy * 0.8);
                color = lerp(color, rock, rockMask);

                half snowMask = smoothstep(13.0, 16.0, p.y + wobble) * smoothstep(0.3, 0.45, normal.y);
                // Snow picks up blue shade in the cracks and hollows beneath it.
                half3 snow = _SnowColor.rgb * lerp(half3(0.74, 0.82, 0.96), half3(1.0, 1.0, 1.0), cracks * saturate((patches - 0.62) * 3.0));
                color = lerp(color, snow, snowMask);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(0.42, 1.0, light.shadowAttenuation);
                color *= lerp(_ShadowTint.rgb, light.color, shadow);

                // Relative to flat ground: the map keeps its colour, slopes turn with the sun.
                half flat = max(light.direction.y, 0.2);
                color *= clamp(dot(normal, light.direction) / flat, 0.5, 1.3);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
