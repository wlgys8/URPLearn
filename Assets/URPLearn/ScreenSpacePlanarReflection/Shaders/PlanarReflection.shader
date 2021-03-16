Shader "URPLearn/PlanarReflection"
{
    Properties
    {
    }

    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

        Blend One One
        HLSLINCLUDE

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

        TEXTURE2D_X(_ReflectionTex);
        TEXTURE2D_X_FLOAT(_CameraDepthTexture);

        SAMPLER(sampler_LinearClamp);

        CBUFFER_START(UnityPerMaterial)
        CBUFFER_END
        
        float SampleDepth(float2 uv){
            return LOAD_TEXTURE2D_X(_CameraDepthTexture, _ScreenParams.xy  * uv).x;
        }

        float4 Frag(Varyings i): SV_Target
        {
            float2 screenUV = i.positionHS.xy * (_ScreenParams.zw - 1);
            float depth = SampleDepth(screenUV);
            if(i.positionHS.z >= depth){
                float4 color = SAMPLE_TEXTURE2D_X(_ReflectionTex,sampler_LinearClamp,screenUV);
                return color;
            }else{
                discard;
                return float4(0,0,0,0);
            }
        }


        ENDHLSL

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
      
    }
}