Shader "URPLearn/PostProcessing/Bloom"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
    }

    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        HLSLINCLUDE
        #pragma shader_feature _BloomDebug

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "../Blur/Shader/Blur.hlsl"
        
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
        SAMPLER(sampler_MainTex);
        TEXTURE2D_X(_BloomTex);
        SAMPLER(sampler_BloomTex);

        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _Threshold;
        CBUFFER_END

        float4 SampleColor(float2 uv){
            return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex,uv);
        }

        float4 FragGetLight(Varyings i):SV_Target{
            float4 color = SampleColor(i.uv);
            float luminance = dot(float3(0.299,0.587,0.114),color.rgb);
            return color * clamp(luminance - _Threshold,0,1);
        }
        
        ///水平blur
        float4 FragBlurH(Varyings i) : SV_Target
        {
            return BoxBlur(_MainTex,i.uv * _MainTex_TexelSize.zw,2,float2(2,0));
        }

        //垂直blur
        float4 FragBlurV(Varyings i) : SV_Target
        {
            return BoxBlur(_MainTex,i.uv * _MainTex_TexelSize.zw,2,float2(0,2));
        }


        float4 FragAdd(Varyings i):SV_Target{
            float4 color = SampleColor(i.uv);
            float4 bloomColor = SAMPLE_TEXTURE2D_X(_BloomTex, sampler_BloomTex,i.uv);
            #if _BloomDebug
            return bloomColor;
            #else
            return color + bloomColor;
            #endif
        }


        ENDHLSL

        Pass{
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragGetLight

            ENDHLSL
        }

        //水平blur pass
        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragBlurH

            ENDHLSL
        }

        //垂直blur pass
        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragBlurV

            ENDHLSL
        }


        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragAdd

            ENDHLSL
        }
    }
}