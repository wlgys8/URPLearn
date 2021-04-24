
Shader "URPLearn/WaterShallow"
{
    Properties
    {

        [MainColor] _BaseColor("Color", Color) = (0.5,0.5,0.5,1)

        _Gloss("Gloss", Float) = 10.0
        _Shininess("Shininess",Float) = 200
        _FresnelPower("FresnelPower",Range(1,5)) = 3

        //法线相关
        _NormalMap1("Normal Map1", 2D) = "bump" {}
        _NormalMap2("Normal Map2", 2D) = "bump" {}
        _NormalScale("Normal Scale",Range(1,20)) = 5

        //天空盒
        _SkyBox("SkyBox",Cube) = "white" {}
        _SkyBoxReflectSmooth("SkyBoxReflectSmooth",Range(1,5)) = 3

        //白沫
        _FoamTex("FoamMap", 2D) = "white" {}
        _FoamPower("FoamPower",Range(0,1)) = 0.5
       
    }

    SubShader
    {

        HLSLPROGRAM

        ENDHLSL
    
        Tags{"RenderType" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" "IgnoreProjector" = "True"}
        LOD 300

        Pass{
            Name "RefractionMask"
            Cull Back
            Tags{"LightMode" = "RefractionMask"}

            Blend One Zero
            ZWrite Off
            ColorMask A 

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "./WaterCommon.hlsl"
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS               : SV_POSITION;
            };

            Varyings vert(Attributes input){
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target{
                return half4(0,0,0,0);
            }

            ENDHLSL
        }
   
        Pass
        {
            // "Lightmode" tag must be "UniversalForward" or not be defined in order for
            // to render objects.
            Name "WaterDefault"
            Tags{"LightMode" = "UniversalForward"}

            Blend One Zero
            // Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "./WaterCommon.hlsl"

            // Required to compile gles 2.0 with standard SRP library
            // All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // -------------------------------------
            // Material Keywords
            // unused shader_feature variants are stripped from build automatically

            #pragma shader_feature _RECEIVE_SHADOWS_OFF
    
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment


            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 uvLM         : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv                       : TEXCOORD0;
                float3 positionWS               : TEXCOORD1;
                half3  normalWS                 : TEXCOORD3;

                float4 positionCS               : SV_POSITION;
            };

            float _Gloss;
            float _Shininess;
            float _FresnelPower;
            
            sampler2D _NormalMap1;
            sampler2D _NormalMap2;
            float _NormalScale;


            samplerCUBE _SkyBox;
            float _SkyBoxReflectSmooth;

            
            sampler2D _FoamTex;
            float _FoamPower;

            float4x4 MatrixInvVP;
            float4x4 MatrixVP;

    
            sampler2D _CameraOpaqueTexture;
            sampler2D _CameraDepthTexture;
            sampler2D WaterReflectionTex;

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;
                output.normalWS = vertexNormalInput.normalWS;
                output.positionCS = vertexInput.positionCS;
                return output;
            }


            static float3 SampleNormal(sampler2D normalMap,float2 uv){
                float4 packedNormal = tex2D(normalMap, uv);
                float3 waterNormal = UnpackNormal(packedNormal);
                return float3(waterNormal.x,waterNormal.z,waterNormal.y);     
            }

            float3 SampleWaterNormal(float2 uv){
                float2 velocity = float2(1,0) * 0.02;
                float t = _Time.y ;
                float3 n1 = SampleNormal(_NormalMap1,(uv + velocity.yx * t  * 1.2) * _NormalScale * 1.5);
                float3 n2 = SampleNormal(_NormalMap2,(uv + velocity.xy * t ) * _NormalScale  ); 
                float3 n = n2 + n1  ;
                n = normalize(n);
                return n;
            }


            float SampleDepth(float2 uv){
                return tex2D(_CameraDepthTexture,uv).x;
            }

            float3 TransformPositionCSToWS(float3 positionCS){
                float4 positionWS = mul(MatrixInvVP,float4(positionCS,1));
                positionWS /= positionWS.w;
                return positionWS.xyz;
            }
        

            float3 ReconstructPositionWS(float2 uv, float depth){
                //使用uv和depth，可以得到ClipSpace的坐标
                float3 positionCS = float3(uv * 2 -1,depth);
                //然后将坐标从ClipSpace转换到世界坐标
                float3 positionWS = TransformPositionCSToWS(positionCS);
                return positionWS;
            }

            //计算白沫强度
            float GetFoamAtten(float3 positionWS,float2 screenUV){
                float depth = SampleDepth(screenUV);
                float3 behindPositionWS = ReconstructPositionWS(screenUV,depth);
                float dis = distance(positionWS,behindPositionWS);
                return pow(max(0,1 - dis / lerp(0.1,1,_FoamPower)),3);
            }


            half4 LitPassFragment(Varyings input) : SV_Target
            {
                float2 screenUV = input.positionCS.xy * (_ScreenParams.zw - 1);
                float2 uv = input.uv;
                float3 positionWS = input.positionWS;

                float time = _Time.y;

                float waterSurfaceDepth = input.positionCS.z;
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                //反射
                
                float3 waterNormal = SampleWaterNormal(uv);

                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                
                //主光源
                half3 specTerm = WaterSpecular(viewDir,waterNormal,_Gloss,_Shininess);
                half3 ambientTerm = GetAmbientLight();

                float3 reflectionTerm = specTerm;

                //SSPR反射
                half4 ssprTerm = tex2D(WaterReflectionTex,screenUV + waterNormal.xz * 0.02);
                reflectionTerm += ssprTerm;

                if(ssprTerm.a == 0){
                    //天空盒反射
                    reflectionTerm += SampleSkybox(_SkyBox,waterNormal,viewDir,_SkyBoxReflectSmooth);
                }

                //折射
                half4 refractionTerm = SAMPLE_REFRACTION(screenUV,waterNormal,0.02);
                float reflCoeff = GetReflectionCoefficient(viewDir,waterNormal,_FresnelPower);
                half3 color = lerp(refractionTerm.rgb,reflectionTerm,reflCoeff);

                //白沫
                float foamAtten = GetFoamAtten(positionWS,screenUV + waterNormal.xz * 0.1);
                //为白沫贴图增加UV扰动
                float2 foamUV = (uv + time * float2(0.01,0.01) + waterNormal.xz * 0.005) * 30;
                float foamDiffuse = tex2D(_FoamTex,foamUV).g;
                half3 foamTerm = (ambientTerm + WaterDiffuse(waterNormal,mainLight)) * foamDiffuse;
                
                color = lerp(color,foamTerm,foamAtten * foamDiffuse);

                return half4(color,1);
            }
            ENDHLSL
        }
    }

}