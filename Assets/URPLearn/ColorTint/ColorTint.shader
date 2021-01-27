Shader "URPLearn/PostProcessing/ColorTint"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _TintColor("Color",Color) = (1,1,1,1)
    }

    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHS    : SV_POSITION;
                float2 uv            : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {

                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            TEXTURE2D_X(_MainTex);
            float4 _MainTex_TexelSize;
            SAMPLER(sampler_LinearClamp);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _TintColor;
            CBUFFER_END
            

            float4 Frag(Varyings i) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp,i.uv);
                return color * _TintColor;
            }
            ENDHLSL
        }
    }
}