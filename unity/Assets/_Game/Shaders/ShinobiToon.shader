// The game's single shared world material. Colour comes from the mesh, so every building, wall
// and tree can batch together. Surface detail comes from one texture array.
//
// uv0.xy   surface coordinates in world units
// uv0.z    detail layer in the texture array
// uv1.x    wind or idle movement weight (0 = fixed)
// uv1.y    glow strength (0 = none), flickers in the shader
Shader "ShinobiDominion/Toon"
{
    Properties
    {
        _DetailArray ("Detail Array", 2DArray) = "white" {}
        _ShadowTint ("Shadow Tint", Color) = (0.56, 0.60, 0.74, 1)
        _RimColor ("Rim Color", Color) = (1.0, 0.92, 0.76, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D_ARRAY(_DetailArray);
        SAMPLER(sampler_DetailArray);

        CBUFFER_START(UnityPerMaterial)
            half4 _ShadowTint;
            half4 _RimColor;
            half _RimStrength;
        CBUFFER_END

        // Set by GameQuality. 0 stills all movement on low-end devices.
        float _SD_AnimStrength;

        float3 ApplyWind(float3 positionWS, float weight)
        {
            float t = _Time.y * 1.3;
            float phase = positionWS.x * 0.35 + positionWS.z * 0.27;
            positionWS.x += sin(t + phase) * weight * 0.05 * _SD_AnimStrength;
            positionWS.z += cos(t * 0.8 + phase) * weight * 0.04 * _SD_AnimStrength;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma require 2darray

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float3 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 uv : TEXCOORD2;
                half4 color : COLOR;
                float glow : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = ApplyWind(positionWS, input.uv1.x);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv0;
                output.color = input.color;
                output.glow = input.uv1.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half nl = dot(normal, light.direction);

                // A soft two-band ramp gives the cel-shaded look without losing form.
                half ramp = smoothstep(0.02, 0.22, nl);
                half shadow = lerp(0.35, 1.0, light.shadowAttenuation);
                half lit = ramp * shadow;

                half detail = SAMPLE_TEXTURE2D_ARRAY(_DetailArray, sampler_DetailArray, input.uv.xy, input.uv.z).r;
                half3 albedo = input.color.rgb * detail;

                half3 shade = lerp(_ShadowTint.rgb, light.color, lit);
                half3 color = albedo * shade;

                // A restrained rim keeps silhouettes crisp at isometric distance.
                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0 - dot(normal, viewDir)), 6.0);
                color += _RimColor.rgb * rim * _RimStrength * (0.35 + 0.65 * lit);

                if (input.glow > 0.0)
                {
                    half flicker = 0.85 + 0.15 * sin(_Time.y * 5.0 + input.positionWS.x * 3.1 + input.positionWS.z * 2.3) * _SD_AnimStrength;
                    color = lerp(color, input.color.rgb * 1.12 * flicker, saturate(input.glow));
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv1 : TEXCOORD1;
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = ApplyWind(positionWS, input.uv1.x);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
