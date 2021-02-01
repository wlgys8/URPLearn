Shader "URPLearn/PostProcessing/GaussianBlur"
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

        #pragma shader_feature _BilinearMode

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "./Blur.hlsl"
        
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

        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _BlurScale;
        SAMPLER(sampler_LinearClamp);

        CBUFFER_END
        
        ///水平blur
        float4 FragH(Varyings i) : SV_Target
        {
            #if _BilinearMode
            return GAUSSIAN_BLUR_7TAP_BILINEAR(_MainTex,i.uv,half2(_BlurScale,0) * _MainTex_TexelSize.xy);
            #else
            return GaussianBlur7Tap(_MainTex,i.uv * _MainTex_TexelSize.zw,half2(_BlurScale,0));
            #endif
        }

        //垂直blur
        float4 FragV(Varyings i) : SV_Target
        {
            #if _BilinearMode
            return GAUSSIAN_BLUR_7TAP_BILINEAR(_MainTex,i.uv,half2(0,_BlurScale) * _MainTex_TexelSize.xy);
            #else
            return GaussianBlur7Tap(_MainTex,i.uv * _MainTex_TexelSize.zw,half2(0,_BlurScale));
            #endif
        }

        ENDHLSL

        //水平blur pass
        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragH

            ENDHLSL
        }

        //垂直blur pass
        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragV

            ENDHLSL
        }
    }
}