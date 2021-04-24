
# 浅水渲染(Shallow Water Render)

先放个效果动画

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/anim.gif">

# 1. 理论部分

水的渲染通常分为两个方面: 一是光照着色， 二是波形模拟。

## 1.1 光照着色

首先看一下水面的光照模型

<img src="https://mammothmemory.net/images/user/base/Physics/Refraction/Part%20reflection%20part%20refraction/part-reflection-part-refraction-r21image2.7402040.jpg">

当光线照射到水面时，会发生反射与折射。反射的光线，可以直接进入我们的眼睛，而折射的光线则会进入到水下，通过复杂的水下作用后，其中一部分会重新传播出水面被我们的眼睛捕捉。

因为光线传播是可逆的，因此在进行渲染时，我们通常是从视线出发，发射一条射线到水面。这条射线在水面一分为二，一部分通过反射回到水面之上，一部分通过折射进入到水面之下。因此水面的最终呈色应当符合如下公式:

$$
C = C_{reflect} + C_{refract}
$$

反射部分的着色，在实现上通常可以由如下几部分组成:

- 高光反射 (模拟太阳、呈现出波光粼粼效果)
- 天空盒反射 (对应的远景)
- 局部反射 (对应近景)

而折射部分的着色，由如下构成:

- 水下物体与光线作用后沿着折射路径返回到眼中的颜色
- 水自身的散射

水对长波(红)吸收强，而对短波(蓝)散射强。因此越深的水，会呈现出越深的蓝色。
而对于浅水渲染，通常可以不考虑水的散射效应。

因此水面的着色公式最终构成如下:

$$
C = C_{reflSpecular} + C_{reflSky} + C_{reflLocal} + C_{refract}
$$


## 1.2 波形模拟

波形模拟通常可分为法线贴图模拟和顶点动画模拟。 法线贴图模拟适合于波动不大、无交互、不近看的情形，优点是性能友好。而顶点动画模拟则对网格顶点密度有要求(通常要结合曲面细分)，可以模拟出波动大、可交互的水体，即便凑近看也能得到很好的效果。
对于浅水模拟，本文使用法线贴图来实现。如欲研究顶点动画模拟，可以看大神的巨著 - [真实感水体渲染技术总结
](https://zhuanlan.zhihu.com/p/95917609)


# 2. 开始动手

Demo中为了方便，使用了Unity自带的Plane来作为水体的Mesh，由于法线贴图的实现对网格密度没有要求，所以在实际应用中一个Quad其实也够了。

## 2.1 法线扰动

项目使用两张法线贴图混合来模拟水面波动效果。(节俭的情况下，使用一张也可以，配合不同的uv tile和采样方向)

<img src = "./Materials/water_normal1.jpg" width=300><img src = "./Materials/water_normal2.png" width=300>

对其中一张法线按水平方向移动采样，另一张则按垂直方向移动采样。

```hlsl
float3 SampleWaterNormal(float2 uv){
    float2 velocity = float2(1,0) * 0.02;
    float t = _Time.y ;
    float3 n1 = SampleNormal(_NormalMap1,(uv + velocity.yx * t  * 1.2) * _NormalScale * 1.5);
    float3 n2 = SampleNormal(_NormalMap2,(uv + velocity.xy * t ) * _NormalScale  ); 
    float3 n = n2 + n1  ;
    n = normalize(n);
    return n;
}
```

## 2.2 高光反射

使用Blinn-Phong公式实现，很简单:

```hlsl
//计算水体的高光
half3 WaterSpecular(float3 viewDir,float3 normal,float gloss,float shininess){
    Light mainLight = GetMainLight();
    float3 halfDir = normalize(mainLight.direction + viewDir);
    float nl = max(0,dot(halfDir,normal));
    return gloss * pow(nl,shininess) * mainLight.color;
}
```

到目前为止，假如仅使用法线贴图 + 高光反射 + 0.5透明Blend，呈现效果如下:



<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/normal_specular.jpeg">

可以看到，除了高光部分有些许水面波纹效果，没有高光效果的画面两侧基本上是看不出水纹的。而且Blend无法呈现出很亮的效果。 

## 2.3 天空盒反射

天空盒反射的基本原理是根据视线(ViewDir)和水体表面的法线(NormalDir)，通过镜面反射公式，计算出反射向量(ReflectDir)，然后利用反射向量去一个预制作好的环境Cube贴图上取样像素。关于CubeMap可以[参考Unity的文档](https://docs.unity3d.com/Manual/class-Cubemap.html)。

```hlsl
//天空盒采样
half3 SampleSkybox(samplerCUBE cube,float3 normal,float3 viewDir,float smooth){
    float3 adjustNormal = float3(normal);
    adjustNormal.xz /= smooth;
    float3 refDir = reflect(-viewDir,adjustNormal);
    half4 color = texCUBE(cube,refDir);
    return color.rgb;
}
```

加上天空盒反射后的效果如下:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/with_sky.jpeg">

在一些低端设备上，到目前为止效果应该就OK了，性价比相当高。 

## 2.4 折射

在真实场景中，由于折射效应的存在，水下的物体成像通常是有偏移的。对于非静态的水面，由于水面法线的变化，水下的物体通常呈如下扭曲状态

<img src="https://f12.baidu.com/it/u=4039505126,935848134&fm=173&app=25&f=JPEG?w=640&h=468">

在水体折射效果渲染上，可以有两种方式，一种是基于物理的方式去实现，一种是通过画面扰动来实现。实际上一开始我是通过物理的方式去做的，虽然最后没有采用，不过这里可以简单谈谈基本原理。

### 2.4.1 基于物理的实现

首先光线的入射角和折射角，满足[斯涅尔定律](https://zh.wikipedia.org/wiki/%E6%96%AF%E6%B6%85%E5%B0%94%E5%AE%9A%E5%BE%8B)。

对应的公式如下:

$$
n_1\theta_1 = n_2\theta_2
$$
其中，$n_1,n_2$分别是两种介质的折射率。

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/refract_cal.png" height=300>

基于屏幕空间的折射实现流程如下:

- 首先渲染非透明物体
- 在非透明物体渲染结束后，我们得到一张CameraOpaqueTexture和CameraDepthTexture。
- 开始渲染水体
- 根据水面O的uv，去采样深度图得到depth
- 根据uv和depth，可以重建出P‘的世界坐标
- 将P'投影到法线可以得到M点的世界坐标。
- 根据斯涅尔定律求出$\theta_2$，继而可以求得P点的世界坐标。
- 将P点重投影到屏幕，可以得到P点的像素uv,于是便采样到了P的像素。

假如水底是完全平坦的情况下，通过这种方式得到的折射效果将是完全物理正确的。而在水底凹凸不平坦的时候，P点将是近似的。

然后理论很完美，现实很残酷，在实际的应用中，会出现很多问题。例如当P点重投影回屏幕的路径上，如果有物体遮挡，那么会采样到该物体的像素。 如下图的所示:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/refract_cal2.png" height=300>

在这种情况下，水面上的黑色方块将会被"折射"到O点的水面上，这是完全错误的。实际上这种瑕疵，随着$\theta_1$角度增大和水的深度增加，会变得愈加明显很夸张。

我在网上查了一下，看到一篇论文，

[Two-phase real-time rendering method for realistic water refraction](https://www.sciencedirect.com/science/article/pii/S2096579620300164)

简单介绍一下里面如何优化这个问题:

首先前置一个步骤 "Broad phase"，在这个阶段，将位于水下的物体顶点，基于斯涅尔定律进行预偏移。这样原本会被物体挡住的P点，变成功挪到了可被摄像机看见的P'点。可被渲染到CameraOpaqueTexture中。然后在"Narrow phase"阶段，再基于像素级别进行折射修正。

但总的来说，基于物理的实现方式性价比不是很高。所以最后放弃了物理的实现。

### 2.4.2 基于画面随机扰动实现

这种实现，本质上是对CameraOpaqueTexture采样时进行随机扰动。通常可以使用法线的xz分量乘以一个系数来对采样uv进行偏移。

完整的理论可以参考gpugems2中的这篇:

[generic-refraction-simulation](https://developer.nvidia.com/gpugems/gpugems2/part-ii-shading-lighting-and-shadows/chapter-19-generic-refraction-simulation)

在uv的随机扰动中，依然会面临偏移后的采样像素超出合理区域的问题。

<img src="https://developer.nvidia.com/sites/all/modules/custom/gpugems/books/GPUGems2/elementLinks/19_refraction_03a.jpg">

如图，透明的水壶本应只折射其背后的像素，但我们基于uv的扰动是盲目的，因此反而采样到了位于水壶前面的棕色球体。

原文中说明如下:
>These artifacts are visible because the texture S has all the scene geometry rendered into it, including objects in front of the refractive mesh, and we are indiscriminately applying perturbation on every pixel. This leads to refraction "leakage" between objects in the scene

为了解决这个问题，可以引入`Refraction Mask Pass`.

这个Pass位于非透明物体渲染结束之后。这时候我们有一张Alpha均为1的CameraOpaqueTexture。在`Refraction Mask Pass`中，我们渲染水体且只往alpha通道写入0， 这样就依据alpha，构建了一个mask区域。 只有这个区域内的像素，才是位于水体背后的，可以被折射的。

在折射着色阶段，对偏移后的uv取色时，首先判定其alpha，如果不为0，则取色失败。这时候就放弃扰动，直接用无偏移的uv采样。

RefractionMask Pass shader 配置如下:

```hlsl
Blend One Zero
ZWrite Off
ColorMask A 
```
片段着色直接返回alpha 0:

```hlsl
half4 frag(Varyings input) : SV_Target{
    return half4(0,0,0,0);
}
```

再在c#中编写一个RefractionMaskPass脚本，顺序放在AfterRenderingOpaques:

```csharp
this.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
```

当我们兴致冲冲的完成了RefractionMask，去拿CameraOpaqueTexture时，结果发现alpha写入没有生效。

这是因为URP在实现CopyColor Pass时，所采用的Shader: `Hidden/Universal Render Pipeline/Sampling`并没有Copy Alpha，如下:

```hlsl
half4 FragBoxDownsample(Varyings input) : SV_Target
{
    half4 col = DownsampleBox4Tap(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv, _MainTex_TexelSize.xy, _SampleOffset);
    return half4(col.rgb, 1);
}
```

因此可以自己实现一个Copy Color Pass，把alpha也拷贝过来。

得到正确的CameraOpaqueTexture后，实现的折射采样函数如下:

```hlsl
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
```

取到折射像素后，我们要与之前的反射像素进行混合。这时候不必再使用管线的Blend混合了，我们可以基于物理的方式来做。

前面说过，光线在水表面，一部分被反射，一部分被折射。那么它们之前的能量分配符合什么规律呢？这里就引入了[`菲涅耳方程`](https://zh.wikipedia.org/wiki/%E8%8F%B2%E6%B6%85%E8%80%B3%E6%96%B9%E7%A8%8B
)。详细的公式可以看WIKI，一句话总结就是，视线与法线夹角越大，反射效应越强。由此带来了著名的[菲涅耳效应](https://zhuanlan.zhihu.com/p/19988903)。

在渲染实现上，通常是用近似公式来模拟`菲涅耳方程`，又或者将入射角与反射系数通过预计算存成一张1D贴图，供计算时索引。 此处我们用如下公式来近似:

```hlsl
//计算反射系数
float GetReflectionCoefficient(float3 viewDir,float3 normal,float fresnelPower){
    float a = 1 - dot(viewDir,normal);
    return pow(a,fresnelPower);
}
```

fresnelPower用来控制菲涅耳效应的强弱。此处fresnelPower越小，菲涅尔效应越强。

最终通过菲涅尔反射系数，将反射和折射进行混合:

```hlsl
float reflCoeff = GetReflectionCoefficient(viewDir,waterNormal,_FresnelPower);
half3 color = lerp(refractionTerm.rgb,reflectionTerm,reflCoeff);
```
呈现效果如下:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/refraction.jpeg">


## 2.5 局部物体反射

更进一步的，我们可以为水面增加局部物体反射。 CubeMap通常只能解决远景反射，对于近景误差会比较大。 局部物体反射一般有以下几种方式:

- 镜像摄像机
- 屏幕空间反射(SSR)
- 屏幕空间镜像反射(SSPR)

首先，镜像摄像机的开销是比较大的。它相当于要从镜像角度重新渲染一遍场景，因此这里先Pass掉。
SSR的话，需要进行RayMaching，开销也比较大，但如果要实现非平整物体的实时反射，目前也只能用SSR。好在我们此处模拟的水体反射是平面的，因此可以使用SSPR来实现。

这里直接使用了前文的成果:

[URP渲染管线 - 屏幕空间平面反射](https://zhuanlan.zhihu.com/p/357714920)

使用SSPRTexGenerator得到反射贴图WaterReflectionTex后，在Shader基于法线xz扰动采样如下:

```hlsl
//SSPR反射
half4 ssprTerm = tex2D(WaterReflectionTex,screenUV + waterNormal.xz * 0.02);
reflectionTerm += ssprTerm;
```

增加了局部物体反射后的效果图:


<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/with_sspr.jpeg">

## 2.6 白沫

在大多数的水体渲染中，白沫都是一个难点。很多相关的论文，都是在物理波形模拟的基础上去做计算的。由于此处我们是法线贴图实现的伪水波，因此只能通过一些简单的方式去模拟。下面提供一个思路。

首先白沫通常是出现在水流与物体碰撞处，因此大多数出现在水流与物体接触的边缘。因此我们可以通过接触检测算法，在水体与物体的接触处生成白沫效果。那么怎么去判定那些像素处于水体与物体接触附近呢？

我们可以利用深度图来做一个近似的判断。

再次引用这张图:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/refract_cal.png" height=300>

我们有O点的世界坐标，同时可以根据深度图重建P'点的世界坐标。通过计算O与P'的距离，就可以简单的判定O点是否位于物体附近。越近则白沫越强。

白沫强度计算函数如下:

```hlsl
float GetFoamAtten(float3 positionWS,float2 screenUV){
    float depth = SampleDepth(screenUV);
    float3 behindPositionWS = ReconstructPositionWS(screenUV,depth);
    float dis = distance(positionWS,behindPositionWS);
    return pow(max(0,1 - dis / lerp(0.1,1,_FoamPower)),3);
}
```
pow是为了让distance增加时，白沫快速消淡。_FoamPower则控制了白沫的分布范围大小。

从网上找一张白沫的贴图,实际应用中，我们只需要单通道就行了:

<img src="./Materials/Foam.jpg">

为了增加随机性，我们同样使用法线对白沫采样UV进行扰动。同时白沫应当是满足漫反射的，因此合起来白沫项计算如下:

```hlsl
float foamAtten = GetFoamAtten(positionWS,screenUV + waterNormal.xz * 0.1);
//为白沫贴图增加UV扰动
float2 foamUV = (uv + time * float2(0.01,0.01) + waterNormal.xz * 0.005) * 10;
float foamDiffuse = tex2D(_FoamTex,foamUV).g;
half3 foamTerm = (ambientTerm + WaterDiffuse(waterNormal,mainLight)) * foamDiffuse;
```

最终混合后的效果:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/foam1.jpeg">
<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/water/foam2.jpeg">



# 3. 其他 

实际上，本Demo还欠缺如下两项:

- 通过FlowMap模拟更丰富的流动效果
- 焦散效果

等后面有空再补吧。

参考:

[Real-time water rendering - Claes Johanson 2004 ](https://fileadmin.cs.lth.se/graphics/theses/projects/projgrid/projgrid-hq.pdf)

[generic-refraction-simulation](https://developer.nvidia.com/gpugems/gpugems2/part-ii-shading-lighting-and-shadows/chapter-19-generic-refraction-simulation)

[Realistic Water Using Bump Mapping and Refraction ](https://hydrogen2014imac.files.wordpress.com/2013/02/realisticwater.pdf)

[SPH Based Shallow Water Simulation](https://developer.download.nvidia.cn/GTC/SIGGRAPH_Asia_2011/PDF/WaterSim_Chentanez.pdf)