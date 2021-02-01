Shader "URPLearn/PostProcessing/DepthOfField"
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
        TEXTURE2D_X_FLOAT(_CameraDepthTexture);

        CBUFFER_START(UnityPerMaterial)
        
        float4 _MainTex_TexelSize;
        float4 _DOFParams; 

        CBUFFER_END
        

        #define rcpF _DOFParams.x
        #define focalLength _DOFParams.y
        #define rcpFFA _DOFParams.z // rcp(_focalLength * rcpf * _aperture)

        float SampleDepth(float2 uv){
            return LOAD_TEXTURE2D_X(_CameraDepthTexture, _MainTex_TexelSize.zw * uv).x;
        }

        float SampleEyeLinearDepth(float2 uv){
            return LinearEyeDepth(SampleDepth(uv),_ZBufferParams);
        }

        //计算像距
        float4 CalculateImageDistance(float objDis){
            return rcp(rcpF - rcp(objDis));
        }

        //弥散圆直径
        float CalculateConfusionCircleDiam(float objDis){
            float imageDis = CalculateImageDistance(objDis);
            return abs(imageDis - focalLength) * rcpFFA;
        }

        float CalculateBlurFactor(float2 uv){
            float depth = SampleEyeLinearDepth(uv);
            float objDis = 1000 * depth - focalLength; //转为mm
            float blurDiam = clamp(CalculateConfusionCircleDiam(objDis),0,3);
            return blurDiam;
        }
        
        ///水平blur
        float4 FragH(Varyings i) : SV_Target
        {
            float factor = CalculateBlurFactor(i.uv);
            return BoxBlur(_MainTex,i.uv * _MainTex_TexelSize.zw,2,float2(factor,0));
        }

        //垂直blur
        float4 FragV(Varyings i) : SV_Target
        {
            float factor = CalculateBlurFactor(i.uv);
            return BoxBlur(_MainTex,i.uv * _MainTex_TexelSize.zw,2,float2(0,factor));
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