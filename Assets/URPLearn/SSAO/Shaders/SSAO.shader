Shader "URPLearn/PostProcessing/SSAO"
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

        #pragma shader_feature __AO_DEBUG__
        #pragma shader_feature _Blur

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
        SAMPLER(sampler_MainTex);

        ENDHLSL


        //first pass, calculate AO
        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag


            
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)

            float4 _MainTex_TexelSize;
            float4x4 CustomProjMatrix;
            float4x4 CustomInvProjMatrix;
            float _Atten;
            float _Contrast;
            float _SampleRadius;
            int _SampleCount;

            CBUFFER_END

            //根据UV和depth，重建像素在viewspace中的坐标
            float3 ReconstructPositionVS(float2 uv,float depth){
                float4 positionInHS = float4(uv * 2 - 1,depth,1);
                float4 positionVS = mul(CustomInvProjMatrix,positionInHS);
                positionVS /= positionVS.w;
                return positionVS.xyz;
            }
            float Random (float2 st) {
                return frac(sin(dot(st,float2(12.9898,78.233)))*43758.5453123);
            }

            float Random(float x){
                return frac(sin(x)* 43758.5453123);
            }

            float3 RandomSampleOffset(float2 uv,float index){
                float2 alphaBeta = float2(Random(uv) * PI * 2,Random(index) * PI);
                float2 sin2;
                float2 cos2;
                sincos(alphaBeta,sin2,cos2);
                return float3(cos2.y * cos2.x, sin2.y, cos2.y * sin2.x);
            }

            float2 ReProjectToUV(float3 positionVS){
                float4 positionHS = mul(CustomProjMatrix,float4(positionVS,1));
                return (positionHS.xy / positionHS.w + 1) * 0.5;
            }

            float SampleDepth(float2 uv){
                return LOAD_TEXTURE2D_X(_CameraDepthTexture, _MainTex_TexelSize.zw * uv).x;
            }

            float3x3 CreateTBN(float3 normal,float3 tangent){
                float3 bitangent = cross(normal, tangent);
                return float3x3(tangent,bitangent,normal);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex,uv);
                float depth = SampleDepth(uv);
                ///根据深度和UV信息，重建像素的世界坐标
                float3 positionVS = ReconstructPositionVS(uv,depth);

                float3 tangentVS = normalize(ddx(positionVS));
                //重建法线
                float3 normalVS = normalize(cross(ddy(positionVS),ddx(positionVS)));

                float3x3 TBN = CreateTBN(normalVS,tangentVS);

                float ao = 0;
                float radius = _SampleRadius;
                float sampleCount = _SampleCount;
                float rcpSampleCount = rcp(sampleCount);
                for(int i = 0; i < int(sampleCount); i ++){
                    float3 offset = RandomSampleOffset(uv,i);
                    offset = mul(TBN,offset);
                    float3 samplePositionVS = positionVS + offset * radius *  (1 + i) * rcpSampleCount;
                    float2 sampleUV = ReProjectToUV(samplePositionVS);
                    float sampleDepth = SampleDepth(sampleUV);
                    float3 hitPositionVS = ReconstructPositionVS(sampleUV,sampleDepth);
                    float3 hitOffset = hitPositionVS - positionVS;
                    float a = max(0,dot(hitOffset,normalVS) - 0.001); //0~radius
                    float b = dot(hitOffset,hitOffset) + 0.001; //0~ radius^2
                    ao += a * rcp(b); // 0 ~ 1/radius
                }
                ao *= radius * rcpSampleCount;
                ao = PositivePow(ao * _Atten, _Contrast);
                ao = 1 - saturate(ao);
                #if __AO_DEBUG__ || _Blur
                return float4(ao,ao,ao,1);
                #else
                return ao * color;
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment FragBlurH

            #include "../../Blur/Shader/Blur.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize;
            CBUFFER_END



            float4 FragBlurH(Varyings i) : SV_Target
            {

                return BoxBlur(_MainTex,i.uv * _MainTex_TexelSize.zw,2,float2(1,0));
            }


            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment FragBlurV

            #include "../../Blur/Shader/Blur.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize;
            CBUFFER_END



            float4 FragBlurV(Varyings i) : SV_Target
            {

                return BoxBlur(_MainTex,i.uv * _MainTex_TexelSize.zw,2,float2(0,1));
            }


            ENDHLSL
        }


        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment FragComb
            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize;
           
            CBUFFER_END

            TEXTURE2D_X(_AOTex);

            float4 FragComb(Varyings i) : SV_Target
            {
                
                float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex,i.uv);
                float ao = SAMPLE_TEXTURE2D_X(_AOTex,sampler_MainTex,i.uv);
                #if __AO_DEBUG__
                return ao;
                #else
                return   ao * color;
                #endif
            }


            ENDHLSL
        }

    }
}