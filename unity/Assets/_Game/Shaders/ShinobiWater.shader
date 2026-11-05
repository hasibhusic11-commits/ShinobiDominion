// Stylized water for ponds, streams and waterfalls. uv.y runs along the flow, and a mesh sets
// its speed through uv.x's scale. Two scrolling layers make bright foam lines with no refraction.
Shader "ShinobiDominion/Water"
{
    Properties
    {
        _DetailArray ("Detail Array", 2DArray) = "white" {}
        _DeepColor ("Deep", Color) = (0.13, 0.42, 0.56, 1)
        _ShallowColor ("Shallow", Color) = (0.30, 0.70, 0.74, 1)
        _FoamColor ("Foam", Color) = (0.88, 0.97, 0.98, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_DetailArray);
            SAMPLER(sampler_DetailArray);

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
            CBUFFER_END

            float _SD_AnimStrength;

            #define LAYER_WATER 7

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 flow : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float speed : TEXCOORD1;
                half shore : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.speed = input.flow.x;
                output.shore = input.color.r;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y * input.speed * _SD_AnimStrength;
                half a = SAMPLE_TEXTURE2D_ARRAY(_DetailArray, sampler_DetailArray, input.uv * 0.09 + float2(t * 0.012, t * 0.05), LAYER_WATER).r;
                half b = SAMPLE_TEXTURE2D_ARRAY(_DetailArray, sampler_DetailArray, input.uv * 0.16 + float2(-t * 0.018, t * 0.03), LAYER_WATER).r;
                half pattern = a * b;

                half3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(pattern * 1.4 + input.shore * 0.5));
                half foam = smoothstep(0.66, 0.8, pattern + input.shore * 0.4 + input.speed * 0.03);
                color = lerp(color, _FoamColor.rgb, foam * 0.85);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
