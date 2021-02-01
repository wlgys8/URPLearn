

half4 BoxBlur(Texture2D tex,float2 pixelCoord,float halfSampleCount,float2 offset){
    half4 color = half4(0,0,0,1);
    float weight = rcp(2 * halfSampleCount + 1);
    for(int i =  -halfSampleCount ; i <= halfSampleCount ; i ++){
        color += LOAD_TEXTURE2D_X(tex,pixelCoord + offset * i) * weight;
    }
    return color ;
}

///BoxFilter水平采样
half4 BoxBlurH(Texture2D tex,float2 pixelCoord,int halfSampleCount,float radiusScale){
    return BoxBlur(tex,pixelCoord,halfSampleCount,float2(radiusScale,0));
}

///BoxFilter垂直采样
half4 BoxBlurV(Texture2D tex,float2 pixelCoord,int halfSampleCount,float radiusScale){
    return BoxBlur(tex,pixelCoord,halfSampleCount,float2(0,radiusScale));
}

half4 BoxBlurBilinear(Texture2D tex,sampler texSampler,float2 uv,int halfSampleCount,float2 offset){
    half4 color = half4(0,0,0,1);
    float weight = rcp(halfSampleCount * 2 + 1);
    if(halfSampleCount % 2 == 0){ //even
        color += SAMPLE_TEXTURE2D_X(tex,texSampler,uv) * weight;
        int quartSampleCount = floor(halfSampleCount / 2);
        for(int i = 1; i <= quartSampleCount; i ++){
            float uvOffset = (i * 2 - 0.5);
            color += SAMPLE_TEXTURE2D_X(tex,texSampler,uv + offset * uvOffset) * 2 * weight;
            color += SAMPLE_TEXTURE2D_X(tex,texSampler,uv - offset * uvOffset) * 2 * weight;
        }
    }else{ //odd
        color += SAMPLE_TEXTURE2D_X(tex,texSampler,uv + 0.75 * offset) * 1.5 * weight;
        color += SAMPLE_TEXTURE2D_X(tex,texSampler,uv - 0.75 * offset) * 1.5 * weight;
        int quartSampleCount = floor((halfSampleCount -1) / 2);
        for(int i = 1; i <= quartSampleCount; i ++){
            float uvOffset = (i * 2 + 0.5);
            color += SAMPLE_TEXTURE2D_X(tex,texSampler,uv + offset * uvOffset) * 2 * weight;
            color += SAMPLE_TEXTURE2D_X(tex,texSampler,uv - offset * uvOffset) * 2 * weight;
        }
    }

    return color ;
}

#define BOX_BLUR_BILINEAR(tex,uv,halfSampleCount,offset) BoxBlurBilinear(tex,sampler_LinearClamp,uv,halfSampleCount,offset)


/**

use following url to generate gaussian weights

http://dev.theomader.com/gaussian-kernel-calculator/
**/


///kernel size = 7,sigma = 1
half4 GaussianBlur7Tap(Texture2D tex,float2 pixelCoord,float2 offset){
    half4 color = half4(0,0,0,0);
    color += 0.383103 * LOAD_TEXTURE2D_X(tex,pixelCoord);
    color += 0.241843 * LOAD_TEXTURE2D_X(tex,pixelCoord + offset);
    color += 0.241843 * LOAD_TEXTURE2D_X(tex,pixelCoord - offset);
    color += 0.060626 * LOAD_TEXTURE2D_X(tex,pixelCoord + offset * 2);
    color += 0.060626 * LOAD_TEXTURE2D_X(tex,pixelCoord - offset * 2);
    color += 0.00598 * LOAD_TEXTURE2D_X(tex,pixelCoord + offset * 3);
    color += 0.00598 * LOAD_TEXTURE2D_X(tex,pixelCoord - offset * 3);
    return color;
}


///kernel size = 7,sigma = 1
half4 GaussianBlur7TapBilinear(Texture2D tex, sampler texSampler, float2 uv,float2 offset){
    half4 color = half4(0,0,0,0);
    color += 0.4333945 * SAMPLE_TEXTURE2D_X(tex, texSampler,uv + offset * 0.558020); 
    color += 0.4333945 * SAMPLE_TEXTURE2D_X(tex, texSampler,uv - offset * 0.558020);
    color += 0.066606 * SAMPLE_TEXTURE2D_X(tex, texSampler,uv + offset * 2.089782);
    color += 0.066606 * SAMPLE_TEXTURE2D_X(tex, texSampler,uv - offset * 2.089782);
    return color;
}

#define GAUSSIAN_BLUR_7TAP_BILINEAR(tex,uv,offset) GaussianBlur7TapBilinear(tex,sampler_LinearClamp,uv,offset)


