

float4 BoxBlurH(Texture2D tex,float2 pixelCoord,float blurRadius,int halfSampleCount)
{
    float4 color = float4(0,0,0,1);
    float weight = rcp(halfSampleCount * 2 + 1);
    for(int i =  -halfSampleCount ; i <= halfSampleCount ; i ++){
        color += LOAD_TEXTURE2D_X(tex,pixelCoord + float2(i * blurRadius,0)) * weight;
    }
    return color;
}

float4 BoxBlurV(Texture2D tex,float2 pixelCoord,float blurRadius,int halfSampleCount)
{
    float4 color = float4(0,0,0,1);
    float weight = rcp(halfSampleCount * 2 + 1);
    for(int i =  -halfSampleCount ; i <= halfSampleCount ; i ++){
        color += LOAD_TEXTURE2D_X(tex,pixelCoord + float2(0,i * blurRadius)) * weight;
    }
    return color;
}