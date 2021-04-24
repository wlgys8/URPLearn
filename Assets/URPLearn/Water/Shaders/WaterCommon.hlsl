

//环境光
half3 GetAmbientLight(){
    return half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
}

half3 WaterDiffuse(float3 normal,Light light){
    return saturate(dot(normal,light.direction) * light.color) * light.shadowAttenuation;
}

//计算水体的高光
half3 WaterSpecular(float3 viewDir,float3 normal,float gloss,float shininess){
    Light mainLight = GetMainLight();
    float3 halfDir = normalize(mainLight.direction + viewDir);
    float nl = max(0,dot(halfDir,normal));
    return gloss * pow(nl,shininess) * mainLight.color;
}

//天空盒采样
half3 SampleSkybox(samplerCUBE cube,float3 normal,float3 viewDir,float smooth){
    float3 adjustNormal = float3(normal);
    adjustNormal.xz /= smooth;
    float3 refDir = reflect(-viewDir,adjustNormal);
    half4 color = texCUBE(cube,refDir);
    return color.rgb;
}


//计算反射系数
float GetReflectionCoefficient(float3 viewDir,float3 normal,float fresnelPower){
    float a = 1 - dot(viewDir,normal);
    return pow(a,fresnelPower);
}

//采样折射像素 
half4 SampleRefractionColor(float2 screenUV,float3 normalWS,float refractionPower,sampler2D cameraOpaqueTexture){
    //进行一个随机的UV扰动，来模拟折射效果
    float2 refractionUV = screenUV + normalWS.xz * refractionPower; 
    half4 color = tex2D(cameraOpaqueTexture,refractionUV);
    if(color.a > 0.1){
        //alpha不为0，说明UV偏移采样超出了透明物体的遮挡区域。因此废弃偏移，直接用原UV来采样
        color = tex2D(cameraOpaqueTexture,screenUV);
    }
    return color;
}

#define SAMPLE_REFRACTION(screenUV,normalWS,refractionFactor) SampleRefractionColor(screenUV,normalWS,refractionFactor,_CameraOpaqueTexture)