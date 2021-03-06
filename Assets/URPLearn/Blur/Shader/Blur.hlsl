
half4 BoxBlur(Texture2D tex,float2 pixelCoord,float halfKernelSize){
    half4 color = half4(0,0,0,1);
    int kernelSize = 2 * halfKernelSize + 1;
    float weight = rcp(kernelSize * kernelSize);
    for(int i =  -halfKernelSize ; i <= halfKernelSize ; i ++){
        for(int j =  -halfKernelSize ; j <= halfKernelSize ; j ++){
            color += LOAD_TEXTURE2D_X(tex,pixelCoord + float2(i,j)) * weight;
        }
    }
    return color ;
}

half4 BoxBlur(Texture2D tex,float2 pixelCoord,float halfKernelSize,float2 offset){
    half4 color = half4(0,0,0,1);
    float weight = rcp(2 * halfKernelSize + 1);
    for(int i =  -halfKernelSize ; i <= halfKernelSize ; i ++){
        color += LOAD_TEXTURE2D_X(tex,pixelCoord + offset * i) * weight;
    }
    return color ;
}

///BoxFilter水平采样
half4 BoxBlurH(Texture2D tex,float2 pixelCoord,int halfKernelSize,float radiusScale){
    return BoxBlur(tex,pixelCoord,halfKernelSize,float2(radiusScale,0));
}

///BoxFilter垂直采样
half4 BoxBlurV(Texture2D tex,float2 pixelCoord,int halfKernelSize,float radiusScale){
    return BoxBlur(tex,pixelCoord,halfKernelSize,float2(0,radiusScale));
}

half4 BoxBlurBilinear(Texture2D tex,sampler linearSampler,float2 uv,int halfKernelSize,float2 offset){
    half4 color = half4(0,0,0,1);
    float weight = rcp(halfKernelSize * 2 + 1);
    if(halfKernelSize % 2 == 0){ //even
        color += SAMPLE_TEXTURE2D_X(tex,linearSampler,uv) * weight;
        int quartKernelSize = floor(halfKernelSize / 2);
        for(int i = 1; i <= quartKernelSize; i ++){
            float uvOffset = (i * 2 - 0.5);
            color += SAMPLE_TEXTURE2D_X(tex,linearSampler,uv + offset * uvOffset) * 2 * weight;
            color += SAMPLE_TEXTURE2D_X(tex,linearSampler,uv - offset * uvOffset) * 2 * weight;
        }
    }else{ //odd
        color += SAMPLE_TEXTURE2D_X(tex,linearSampler,uv + 0.75 * offset) * 1.5 * weight;
        color += SAMPLE_TEXTURE2D_X(tex,linearSampler,uv - 0.75 * offset) * 1.5 * weight;
        int quartKernelSize = floor((halfKernelSize -1) / 2);
        for(int i = 1; i <= quartKernelSize; i ++){
            float uvOffset = (i * 2 + 0.5);
            color += SAMPLE_TEXTURE2D_X(tex,linearSampler,uv + offset * uvOffset) * 2 * weight;
            color += SAMPLE_TEXTURE2D_X(tex,linearSampler,uv - offset * uvOffset) * 2 * weight;
        }
    }
    return color ;
}

#define BOX_BLUR_BILINEAR(tex,uv,halfKernelSize,offset) BoxBlurBilinear(tex,sampler_LinearClamp,uv,halfKernelSize,offset)


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


