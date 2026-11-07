// Heroes built in VRoid Studio: the Hero shader's lighting, plus what these models need.
// Alpha cut-out for hair and clothing edges, both sides drawn for thin cloth, softer light for the face,
// and less rim light on dark painted cloth so it doesn't wash out at steep angles.
Shader "ShinobiDominion/HeroVRoid"
{
    Properties
    {
        _BaseMap ("Painted Colours", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _LightFlatten ("Flatten Light (face material)", Range(0, 1)) = 0
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.64, 0.78, 1)
        _RimColor ("Rim", Color) = (1, 0.96, 0.88, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.28
        _RimDarkMask ("Rim Dark Cloth Mask", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _Cutoff;
            half _LightFlatten;
            half4 _ShadowTint;
            half4 _RimColor;
            half _RimStrength;
            half _RimDarkMask;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input, bool isFront : SV_IsFrontFace) : SV_Target
            {
                half4 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(painted.a - _Cutoff);

                float3 normal = normalize(input.normalWS) * (isFront ? 1.0 : -1.0);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half nl = dot(normal, light.direction);
                half ramp = smoothstep(-0.05, 0.2, nl);
                half lit = ramp * lerp(0.45, 1.0, light.shadowAttenuation);
                lit = lerp(lit, 1.0, _LightFlatten);

                half3 color = painted.rgb * lerp(_ShadowTint.rgb, light.color, lit);

                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0 - dot(normal, viewDir)), 4.0);
                // Dark painted cloth takes little rim. Bright surfaces keep it all.
                half brightness = dot(painted.rgb, half3(0.299, 0.587, 0.114));
                half rimMask = lerp(1.0, lerp(0.15, 1.0, smoothstep(0.02, 0.3, brightness)), _RimDarkMask);
                color += _RimColor.rgb * rim * _RimStrength * rimMask * (0.35 + 0.65 * lit) * (1.0 - _LightFlatten);
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

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(float4 positionOS : POSITION, float3 normalOS : NORMAL, float2 uv : TEXCOORD0)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                output.uv = uv;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
