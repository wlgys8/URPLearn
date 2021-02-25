Shader "URPLearn/Grass"
{
    Properties
    {

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _NoiseMap("WaveNoiseMap", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Wind("Wind(x,y,z,str)",Vector) = (1,0,0,10)
        _WindNoiseStrength("WindNoiseStr",Range(0,20)) = 10
        _StormParams("Storm(Begin,Keep,End,Slient)",Vector) = (1,100,40,100)
        _StormStrength("StormStrength",Range(0,40)) = 20
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}

            ZWrite On
            ZTest On
            Cull Off

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard SRP library
            // All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // #include "UnityCG.cginc"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup


            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                float3 normalWS    : TEXCOORD1;
                float4 positionWS   : TEXCOORD2;
            };

            #pragma vertex PassVertex
            #pragma fragment PassFragment

            float _Cutoff;
            half4 _BaseColor;


            TEXTURE2D_X(_BaseMap);
            SAMPLER(sampler_BaseMap);
            sampler2D _NoiseMap;

            float4x4 _TerrianLocalToWorld;
            float2 _GrassQuadSize;
            float4 _Wind;
            float _WindNoiseStrength;
            float4 _StormParams;
            float _StormStrength;

            #define StormFront _StormParams.x
            #define StormMiddle _StormParams.y
            #define StormEnd _StormParams.z
            #define StormSlient _StormParams.w

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                struct GrassInfo{
                    float4x4 localToTerrian;
                    float4 texParams;
                };
                StructuredBuffer<GrassInfo> _GrassInfos;
            #endif

            void setup(){
            }

            ///根据风力，计算顶点的世界坐标偏移
            ///positionWS - 顶点的世界坐标
            ///grassUpWS - 草的生长方向
            ///windDir - 是风的方向，应该为单位向量
            ///windStrength - 风力强度,范围(0~1)
            ///vertexLocalHeight - 顶点在草面片空间中的高度
            float3 applyWind(float3 positionWS,float3 grassUpWS,float3 windDir,float windStrength,float vertexLocalHeight,int instanceID){
                //根据风力，计算草弯曲角度，从0到90度
                float rad = windStrength * PI * 0.9 / 2;

                
                //得到wind与grassUpWS的正交向量
                windDir = normalize(windDir - dot(windDir,grassUpWS) * grassUpWS);

                float x,y;  //弯曲后,x为单位球在wind方向计量，y为grassUp方向计量
                sincos(rad,x,y);

                //offset表示grassUpWS这个位置的顶点，在风力作用下，会偏移到windedPos位置
                float3 windedPos = x * windDir + y * grassUpWS;

                //加上世界偏移
                return positionWS + (windedPos - grassUpWS) * vertexLocalHeight;
            }


            float applyStorm(float3 positionWS,float3 windDir,float windStrength){
                //首先，计算世界坐标在风向上的投影距离，乘以一个时间time,就可以让这个值随着时间移动

                float stormInterval = StormFront + StormMiddle + StormEnd + StormSlient;

                float disInWindDir = dot(positionWS - windDir * _Time.y * (windStrength + _StormStrength),windDir);

                //范围为0 ~ stormInterval
                float offsetInInterval = stormInterval - (disInWindDir % stormInterval) - step(disInWindDir,0) * stormInterval;

                float x = 0;
                if(offsetInInterval < StormFront){
                    //前部,x从0到1
                    x = offsetInInterval * rcp(StormFront);
                }else if(offsetInInterval < StormFront + StormMiddle){
                    //中部
                    x = 1;
                }
                else if(offsetInInterval < StormFront + StormMiddle + StormEnd){
                    //尾部,x从1到0
                    x = (StormFront + StormMiddle + StormEnd - offsetInInterval) / StormEnd;
                }

                //基础风力 + 强力风力
                return windStrength + _StormStrength * x;               
            }


            Varyings PassVertex(Attributes input)
            {
                Varyings output;
                float2 uv = input.uv;
                float3 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;
                uint instanceID = input.instanceID;
                positionOS.xy = positionOS.xy * _GrassQuadSize;


                float localVertexHeight = positionOS.y;

                float3 grassUpDir = float3(0,1,0);

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    GrassInfo grassInfo = _GrassInfos[instanceID];

                    //将顶点和法线从Quad本地空间转换到Terrian本地空间
                    positionOS = mul(grassInfo.localToTerrian,float4(positionOS,1)).xyz;
                    normalOS = mul(grassInfo.localToTerrian,float4(normalOS,0)).xyz;
                    grassUpDir = mul(grassInfo.localToTerrian,float4(grassUpDir,0)).xyz;

                    //UV偏移缩放
                    uv = uv * grassInfo.texParams.xy + grassInfo.texParams.zw;

                #endif
                float4 positionWS = mul(_TerrianLocalToWorld,float4(positionOS,1));
                positionWS /= positionWS.w;

                grassUpDir = normalize(mul(_TerrianLocalToWorld,float4(grassUpDir,0)));

                float time = _Time.y;

                float3 windDir = normalize(_Wind.xyz);

                //风力强度，范围0~40 m/s
                float windStrength = _Wind.w ;


                //我们可以定时生成一波大风，带来麦浪的感觉
                windStrength = applyStorm(positionWS.xyz,windDir,windStrength);

                //生成一个扰动。扰动的频率，可以与风力挂钩，一般来说风力越强，抖动越厉害。
                float2 noiseUV = (positionWS.xz - time) / 30;
                float noiseValue = tex2Dlod(_NoiseMap,float4(noiseUV,0,0)).r;
                noiseValue = sin(noiseValue * windStrength);

                //将扰动再加到风力上
                windStrength += noiseValue * _WindNoiseStrength;

                //归一化后到0~1区间
                windStrength = saturate(windStrength / 40);

                positionWS.xyz = applyWind(positionWS.xyz,grassUpDir,windDir,windStrength,localVertexHeight,instanceID);
                
                
                output.uv = uv;
                output.positionWS = positionWS;
                output.positionCS = mul(UNITY_MATRIX_VP,positionWS);
                output.normalWS = mul(unity_ObjectToWorld, float4(normalOS, 0.0 )).xyz;
                return output;
            }

            half4 PassFragment(Varyings input) : SV_Target
            {
                half4 diffuseColor = SAMPLE_TEXTURE2D_X(_BaseMap,sampler_BaseMap,input.uv);
                if(diffuseColor.a < _Cutoff){
                    discard;
                    return 0 ;
                }
                //计算光照和阴影，光照采用Lembert Diffuse.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = mainLight.direction;
                float3 lightColor = mainLight.color;
                float3 normalWS = input.normalWS;
                float4 color = float4(1,1,1,1);
                float minDotLN = 0.2;
                color.rgb = max(minDotLN,abs(dot(lightDir,normalWS))) * lightColor * diffuseColor.rgb * _BaseColor.rgb * mainLight.shadowAttenuation;
                return color;
            }

            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
