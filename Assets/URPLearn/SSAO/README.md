# URP屏幕空间环境光遮蔽后处理(SSAO)

SSAO的理论，在网上有很多的文章，这里就不列举了。

## 基本流程

SSAO的基本流程如下:

- 获得屏幕的深度图和法线图
- 利用uv和深度信息，可以重构每个像素在viewspace中的坐标positionVS,这个坐标表示我们要计算的被遮蔽点。
- 利用屏幕法线图，我们同样可以重构每个像素在viewspace中的法线normalVS
- 针对positionVS和normalVS，在法线朝向的半球，按照给定的半径(sampleRadius)，生成若干采样点(samplePositionVS)
- 将samplePositionVS重新投影到屏幕，计算出其对应的sampleUV
- 用sampleUV去深度图采样,得到sampleDepth.
- 利用sampleUV和sampleDepth,我们可以重构出一个hitPositionVS. 这个位置代表了最终计算出来的遮蔽点.
- 有了被遮点(positionVS)和遮蔽点(hitPositionVS)后,我们可以通过公式计算出一个遮蔽系数，这个系数就代表的遮蔽点对被遮点的阴影贡献度。


对比图

无SSAO:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/ssao/without-ssao.jpeg" width="600"/>

有SSAO:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/ssao/ssao.jpeg" width="600"/>




## 法线计算

粗糙一点的情况下，我们可以不需要屏幕法线贴图，而用面法线来替代:

```hlsl

 float3 normalVS = normalize(cross(ddy(positionVS),ddx(positionVS)));

```

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/ssao/ssao-normal.jpeg" width="600"/>

可以看出来，在Unity的ViewSpace中，z轴朝屏幕外，y轴朝下。


## 半球采样

因为法线半球的遮蔽点，才有阴影贡献，所以我们只在法线半球生成采样点就可以了。

为了简化计算，我们在切线空间(TangentSpace)中生成采样点:

```hlsl

float Random (float2 st) {
    return frac(sin(dot(st,float2(12.9898,78.233)))*43758.5453123);
}

float Random(float x){
    return frac(sin(x)* 43758.5453123);
}

//index表示第几个采样点
float3 RandomSampleOffset(float2 uv,float index){
    float2 alphaBeta = float2(Random(uv) * PI * 2,Random(index) * PI);
    float2 sin2;
    float2 cos2;
    sincos(alphaBeta,sin2,cos2);
    return float3(cos2.y * cos2.x, sin2.y, cos2.y * sin2.x);
}

```

这只是随手写的一个采样点生成算法。本质是:

利用uv随机生成一个角度alpha，再利用index随机生成一个角度beta. alpha代表绕normal旋转的一个角度,beta代表绕tangent旋转角度。 利用这两个随机角度，我们可以构建一个切线空间内的随机朝向单位向量(法线半球).

我们按照由近及远方式生成采样点。 让其分布尽量均匀

```
for(int i = 0; i < int(sampleCount); i ++){
    float3 offset = RandomSampleOffset(uv,i);
    offset = offset * radius *  (1 + i) * rcpSampleCount;
}
```

为了计算采样点在viewspace中的坐标，我们需要把offset从切线空间(TangentSpace)转到ViewSpace。

为此，我们需要构造一个TBN矩阵:

```
float3x3 CreateTBN(float3 normal,float3 tangent){
    float3 bitangent = cross(normal, tangent);
    return float3x3(tangent,bitangent,normal);
}
```

其中tangent可以使用:

```
float3 tangentVS = normalize(ddx(positionVS));

```

这样我们就可以:

```

float3 tangentVS = normalize(ddx(positionVS));
float3 normalVS = normalize(cross(ddy(positionVS),ddx(positionVS)));
float3x3 TBN = CreateTBN(normalVS,tangentVS);

//.....


offset = mul(TBN,offset); //将offset转到了ViewSpace

float3 samplePositionVS = positionVS + offset; //计算出采样点在viewspace中的坐标

```

## 遮蔽系数计算公式

在我们计算出遮蔽点 `hitPositionVS`后，我们计算其相对于`positionVS`偏移向量:

```
float3 hitOffset = hitPositionVS - positionVS;

```

然后计算系数a:

```

float a = max(0,dot(hitOffset,normalVS) - 0.001); //0~radius

```

系数a表示了偏移向量在法线上的投影，通常来说，偏移向量与法线之间的夹角越小（即代表了遮蔽点更接近于计算点的正上方），那么它对计算点的遮蔽影响越大。 a的分布范围为0 ~ radius. 其中radius为最大取样半径。


计算系数b:

```

float b = dot(hitOffset,hitOffset) + 0.001; //0~ radius^2

```

系数b衡量了遮蔽点与计算的距离(平方)。通常来说，距离越远，遮蔽影响越小,为反比关系。 分布范围为 0 ~ radius^2

那么单个遮蔽点的遮蔽系数为:

```
float aoAdd = a / b; 
```

符合我们前面分析的与系数a,b的正反比关系，分布范围为 0 ~ 1 / radius


将所有采样点的aoAdd加起来，我们即可以得到计算点整体的ao系数:

```

float ao = 0;
for(int i = 0; i < int(sampleCount); i ++){

    ///calculate aoAdd...

    ao += aoAdd;
}

```

由于aoAdd的分布范围为 `0 ~ 1/radius`,可知此处的ao分布范围为`sampleCount/radius`

对其进行归一化:

```
ao *= (radius / sampleCount);

```

然后可以加入atten和contrast来对ao进行强度和对比度调节:

```
ao = PositivePow(ao * _Atten, _Contrast);
```

单输出ao效果:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/ssao/ssao-debug.jpeg" width="600"/>

## Blur处理

在没有Blur处理的情况下，由于采样点数量限制，生成的AO图粒状比较明显。 因此我们可以加入Blur使其更平滑。
这里使用了简单的BoxBlur。


无Blur的情况:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/ssao/ssao-no-blur.jpeg" width="500"/>


有Blur情况:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/ssao/ssao-blur.jpeg" width="500"/>
