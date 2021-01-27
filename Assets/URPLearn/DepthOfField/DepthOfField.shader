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
        float _FocusDistance;
        
        float _FocusDistanceThreshold;
        float _FarBlurDistance;

        float4 _DOFParams; 

        CBUFFER_END

        

        #define rcpF _DOFParams.x
        #define focalLength _DOFParams.y
        #define rcp_FURcpf _DOFParams.z

        float SampleDepth(float2 uv){
            return LOAD_TEXTURE2D_X(_CameraDepthTexture, _MainTex_TexelSize.zw * uv).x;
        }

        float SampleEyeLinearDepth(float2 uv){
            return LinearEyeDepth(SampleDepth(uv),_ZBufferParams);
        }

        float CalcualteDepthOffset(float2 uv){
            float depth = SampleEyeLinearDepth(uv);
            float depthOffset = max(0,abs(depth - _FocusDistance) - _FocusDistanceThreshold);
            depthOffset /= _FarBlurDistance;
            depthOffset = saturate(depthOffset);
            return depthOffset;
        }

        //计算像距
        float4 CalculateImageDistance(float objDis){
            return 1 / (rcpF - 0.001 / objDis);
        }

        //弥散圆直径
        float CalculateConfusionCircleDiam(float objDis){
            float imageDis = CalculateImageDistance(objDis);
            return abs(imageDis - focalLength) * rcp_FURcpf;
        }

        float CalculateBlurFactor(float2 uv){
            float depth = SampleEyeLinearDepth(uv);
            float objDis = depth - focalLength * 0.001;
            float blurDiam = clamp(CalculateConfusionCircleDiam(objDis),0,3);
            return blurDiam;
        }
        
        ///水平blur
        float4 FragH(Varyings i) : SV_Target
        {
            float factor = CalculateBlurFactor(i.uv);
            return BoxBlurH(_MainTex,i.uv * _MainTex_TexelSize.zw,factor,2);
        }

        //垂直blur
        float4 FragV(Varyings i) : SV_Target
        {
            float factor = CalculateBlurFactor(i.uv);
            // return factor;
            return BoxBlurV(_MainTex,i.uv * _MainTex_TexelSize.zw,factor,2);
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