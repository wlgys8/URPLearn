
# 写在前面

图像模糊技术在游戏图形开发中是一项非常基础的存在。诸多的后处理效果中，都需要使用模糊算法来优化锯齿、颗粒。此篇文章为模糊算法入门总结之用，涉及到的相关代码并不用作产品级使用。

# 1. 基础概念

在说到具体的模糊算法之前，先要了解一下图形处理中的一个基础概念: 卷积核(kernel).

kernel是矩阵形式的存在，一个3x3的kernel，形式大概是:

$$
\begin{bmatrix}
a & b & c  \\\ 
d & e & f  \\\ 
g & h & i
\end{bmatrix} 

$$

这里的3称作`kernelSize`

将其作用与(x,y)位置的像素，等效于采集(x,y)周围3x3范围的像素值，分别与`a ~ i`进行加权平均运算。


详细内容可参考别人的文章:

https://zhuanlan.zhihu.com/p/41212352

不同的模糊算法，实质上就是取不同的卷积核。

# 2. 模糊算法

这里只介绍两个基本的模糊算法:

- 均值模糊(Box Blur)
- 高斯模糊(Gaussian Blur)


## 2.1 均值模糊(Box Blur)

[WIKI参考](https://en.wikipedia.org/wiki/Box_blur)


均值模糊。 即取指定大小(size * size)范围内的像素，相加后取平均值。 一个3x3的均值模糊卷积核，对应形式如下:

$$
\begin{bmatrix}
1/9 & 1/9 & 1/9  \\\ 
1/9 & 1/9 & 1/9  \\\ 
1/9 & 1/9 & 1/9 
\end{bmatrix} 

$$

在URP的Shader中实现代码如下:

```hlsl

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

```

有时候为了增强模糊效果，还需要进行多次模糊迭代。

以下是对比效果图,分别是原图、单次迭代模糊，3次迭代模糊:

原图:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/blur/original.jpeg">

3tap,单次迭代:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/blur/boxblur3x3_1.jpeg">

3tap,3次迭代:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/blur/boxblur3x3_3.jpeg">

## 2.2 高斯模糊(Gaussian Blur)

[WIKI](https://en.wikipedia.org/wiki/Gaussian_blur)

不同于均值模糊，高斯模糊使用正态分布来为周围的像素分配权重。

这里有一个网站，可以计算高斯模糊采用的卷积核: [gaussian-kernel-calculator](http://dev.theomader.com/gaussian-kernel-calculator/)

要确定一个高斯卷积核，需要提供两个参数: `sigma` 和 `kernelSize`

kernelSize我们前面已经说了，`sigma`则是正态分布公式中的标准差。`sigma`的值越小，正态分布曲线越尖锐，反之则越平坦。

因此，对于固定kernelSize的高斯模糊算子，取的`sigma`越大，则结果越模糊。

通常来说，当我们在shader中实现高斯模糊时，会将预计算好的卷积核硬编码在代码中。

7tap,三次迭代高斯模糊:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/blur/gaussian7tap_3.jpeg">

看起来没有Box那么的模糊，是由正态分布的特性决定的。

# 3. 优化

下面谈谈在shader上实现模糊算法时的一些优化措施。 依旧以BoxBlur为例

## 3.1 线性分解

有一些卷积核，是可以线性分解的，[参考SeparableFilter](https://en.wikipedia.org/wiki/Separable_filter)。

例如对于3x3的BoxBlur

$$

\begin{bmatrix}
1/9 & 1/9 & 1/9  \\\ 
1/9 & 1/9 & 1/9  \\\ 
1/9 & 1/9 & 1/9 
\end{bmatrix} 

$$

实际上可以分解为:

$$

\begin{bmatrix}
1/3  \\\ 
1/3  \\\ 
1/3 
\end{bmatrix} * 
\begin{bmatrix}
1/3 & 1/3 & 1/3 
\end{bmatrix}

$$

原先我们使用`n * n`的BoxBlur卷积核计算每个像素时，对其周围的像素累计采样n^2次。 由上面的分解公式可以看出，我们可以用将这个过程分为两步:

- 首先在水平方向，使用$\begin{bmatrix}
1/3 & 1/3 & 1/3 
\end{bmatrix}$作为kernel进行模糊
- 然后在垂直方向，使用$\begin{bmatrix}
1/3  \\\ 
1/3  \\\ 
1/3 
\end{bmatrix}$作为kernel进行模糊

这样对每个像素，累计只需要进行 `2 * n` 次采样。复杂度由O(n^2)降为O(n)

对应到Shader实现上，即使用两个Pass来进行模糊:

```hlsl

half4 BoxBlur(Texture2D tex,float2 pixelCoord,float halfKernelSize,float2 offset){
    half4 color = half4(0,0,0,1);
    float weight = rcp(2 * halfKernelSize + 1);
    for(int i =  -halfKernelSize ; i <= halfKernelSize ; i ++){
        color += LOAD_TEXTURE2D_X(tex,pixelCoord + offset * i) * weight;
    }
    return color ;
}

///BoxFilter水平采样
half4 BoxBlurH(Texture2D tex,float2 pixelCoord,int halfKernelSize){
    return BoxBlur(tex,pixelCoord,halfKernelSize,float2(1,0));
}

///BoxFilter垂直采样
half4 BoxBlurV(Texture2D tex,float2 pixelCoord,int halfKernelSize){
    return BoxBlur(tex,pixelCoord,halfKernelSize,float2(0,1));
}
```

高斯卷积核也是可以线性分解的，以下采用`kernelSize=7、sigma=1`对应高斯核进行模糊算法实现,参数由以上提到的网站生成

```hlsl
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
```

- 水平模糊Pass时，offset取(1,0)
- 垂直模糊Pass时，offset取(0,1)

## 3.2 Bilinear采样

在Shader上实现模糊算法时，还可以进一步利用GPU的硬件特性来提高性能。考虑Bilinear采样:

由于Bilinear采样本身就使用了线性插值，可以一次取到两个像素，又几乎没有开销。因此可以大大降低需要的采样次数。

使用Bilinear + 线性分解后，每个像素的卷积计算，累计只需要采样`n + 1`次.

### 3.2.1 BoxBlur使用Bilinear采样实现

以BoxBlur为例, 假设我们要采样(i,j)和(i+1,j)两个位置的像素，进行均值运算，那么只要在贴图采样时，使用`Bilinear`模式，并将uv设为(i + 0.5,j)，即可以通过一次采样得到均值结果。

使用`Bilinear采样`优化后的BoxBlur Shader实现:

```hlsl
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

```

- 在水平模糊Pass时，令`offset = (1 / textureWidth,0)`;
- 在垂直模糊Pass时，令`offset = (0, 1 / textureHeight)`;

### 3.2.2 高斯模糊的Bilinear采样实现

高斯模糊同样可以使用Blinear采样来实现，只是采样uv位置计算稍微复杂一些。

考虑以下问题:

```
已知系数a,b和采样坐标u,v，求m,n，使得

m * BilinearSampleTex(u + n,v) = a * BilinearSampleTex(u,v) + b * BilinearSampleTex(u + 1,v).

```

不难得出：

```
n = b / (a + b)
m = (a + b)
```

利用以上公式，`size = 7,sigma = 1`的高斯模糊单纬度Bilinear采样实现代码如下:

```hlsl
half4 GaussianBlur7TapBilinear(Texture2D tex, sampler texSampler, float2 uv,float2 offset){
    half4 color = half4(0,0,0,0);
    color += 0.4333945 * SAMPLE_TEXTURE2D_X(tex, texSampler,uv + offset * 0.558020); 
    color += 0.4333945 * SAMPLE_TEXTURE2D_X(tex, texSampler,uv - offset * 0.558020);
    color += 0.066606 * SAMPLE_TEXTURE2D_X(tex, texSampler,uv + offset * 2.089782);
    color += 0.066606 * SAMPLE_TEXTURE2D_X(tex, texSampler,uv - offset * 2.089782);
    return color;
}
```


## 3.3 降采样

可以将原贴图缩小到1/2、1/4后(DownSample)，再进行Blit，然后还原到原大小(UpSample)。

这样，在Blit阶段，需要计算的像素量将大大减小


## 3.4 扩展卷积

在不增加kernelSize和迭代次数的情况下，可以采用`扩张卷积`的方式提升模糊效果。

详细介绍可以参考别人的文章: [什么是扩展卷积](https://zhuanlan.zhihu.com/p/81082191)

在shader代码实现上，即在卷积采样时使用非连续的采样间隔，以前面实现的`BoxBlur`函数为例:

```hlsl
half4 BoxBlur(Texture2D tex,float2 pixelCoord,float halfKernelSize,float2 offset);

```

进行标准水平模糊卷积:
```hlsl
BoxBlur(tex,pixelCoord,halfKernelSize,float2(1,0));
```

进行N倍扩展水平模糊卷积:

```hlsl
BoxBlur(tex,pixelCoord,halfKernelSize,float2(N,0));
```

