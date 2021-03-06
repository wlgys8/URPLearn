# GrassGPUInstance

这个例子主要是学习GPU Instance功能在URP中的使用。 先看效果图

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/grassinstances/grass_anim_0.gif">


## 0. API分析

CommandBuffer中，绘制Mesh Instance有以下几个接口可用:

- DrawMeshInstanced	
- DrawMeshInstancedIndirect	
- DrawMeshInstancedProcedural

其中:

- `DrawMeshInstanced`需要自己提供一个`Matrix[]`数组，来操作每个Instance的位置、旋转、缩放。`一个批次最多绘制1023个对象`。

- `DrawMeshInstancedIndirect`则可以通过ComputeBuffer来提供这些数据。包括绘制数量也要通过ComputeBuffer提供

- `DrawMeshInstancedProcedural`跟`DrawMeshInstancedIndirect`类似，但绘制数量可以直接通过接口提供。


这里我们选用`DrawMeshInstancedProcedural`来实现。

```csharp
void CommandBuffer.DrawMeshInstancedProcedural(Mesh mesh, int submeshIndex, Material material, int shaderPass, int count, [MaterialPropertyBlock properties = null])
```

看接口参数，需要准备一个Mesh，一份材质/Shader.



## 1. Mesh生成

单个草，可以直接使用1x1的Quad面片来作为Mesh进行渲染，锚点放在底部中间。

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/grassinstances/grass_quad.jpeg">

## 2. 草的Shader实现


首先找一张草的贴图:

<img src="./Textures/grass.png" width=256>


支持深度写入和测试:
```
ZWrite On
ZTest On
```

还要支持双面显示:
```
Cull Off
```

从性能以及遮挡考虑，我们使用AlphaTest来代替AlphaBlend,最终效果:


<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/grassinstances/grass_tex.jpeg">


### 2.1 支持GPU Instance

[先看文档](https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstancedIndirect.html)


按照里面的意思是，Shader要支持GPU Instance,需要加入以下的编译选项:


```
//--------------------------------------
// GPU Instancing
#pragma multi_compile_instancing
#pragma instancing_options procedural:setup
```

其中`#pragma instancing_options procedural:setup`表示每次实例渲染的时候，都会执行以下setup这个函数。我们这里setup什么都不用做。


### 2.2 Instance结构定义

Shader中定义单个草的数据结构:

```hlsl
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    struct GrassInfo{
        float4x4 localToTerrian;
        float4 texParams;
    };
    StructuredBuffer<GrassInfo> _GrassInfos;
#endif
```

其中:

- `localToTerrian` 这个矩阵用来控制单株草在地面上的种植偏移和旋转.
- `texParams`用来控制草贴图在atlas中的采样(如果需要的话)

`StructuredBuffer<GrassInfo> _GrassInfos;`这个结构到时候从c#端，通过ComputeBuffer传进来。

有了_GrassInfos这个数组后，我们需要根据每个实例索引，去访问这个数组。那么如何获得这个索引呢？

在Builtin渲染管线，也即文档中所说的是，通过`unity_InstanceID`这个变量。但实测在URP中不起作用。经过搜索发现，可以在顶点函数输入结构中增加如下字段，来获取instanceID:

```hlsl
uint instanceID : SV_InstanceID;
```

### 2.3 Instance坐标变换

后我们需要在顶点函数中，针对每棵草进行顶点变换。每个草的顶点变换操作如下:

- 通过`localToTerrian`矩阵(PerGrassInstance)，让草的位置、旋转(y轴)在Terrian空间中形成随机分布
- 然后将顶点通过`_TerrianLocalToWorld`矩阵(PerTerrianMesh)变换到世界坐标
- 通过`UNITY_MATRIX_VP`矩阵，输出到片段着色器

```hlsl

//通过_GrassQuadSize来控制面片大小
positionOS.xy = positionOS.xy * _GrassQuadSize;

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    GrassInfo grassInfo = _GrassInfos[instanceID];

    //将顶点和法线从Quad本地空间变换到Terrian本地空间
    positionOS = mul(grassInfo.localToTerrian,float4(positionOS,1)).xyz;
    normalOS = mul(grassInfo.localToTerrian,float4(normalOS,0)).xyz;

    //UV偏移缩放
    uv = uv * grassInfo.texParams.xy + grassInfo.texParams.zw;

#endif

//从Terrian本地坐标转换到世界坐标
float4 positionWS = mul(_TerrianLocalToWorld,float4(positionOS,1));
positionWS /= positionWS.w;


//输出到片段着色器
output.positionWS = positionWS;
output.positionCS = mul(UNITY_MATRIX_VP,positionWS);
output.normalWS = mul(unity_ObjectToWorld, float4(normalOS, 0.0 )).xyz;

```

## 3. C#代码实现



### 3.1 构造ComputeBuffer

首先定义一个与Shader中一样的数据结构，表示每个草实例:

```csharp
public struct GrassInfo{
    public Matrix4x4 localToTerrian;
    public Vector4 texParams;
}
```

先简单一点,假设我们有一个10x10平面网格`terrianMesh`，针对这个Mesh，在其每个顶点1x1范围内随机生成`grassCountPerFace`颗草的实例:

```csharp
foreach(var v in terrianMesh.vertices){
    var vertexPosition = v;
    for(var i = 0; i < grassCountPerFace; i ++){
        
        //贴图参数，暂时不用管
        Vector2 texScale = Vector2.one;
        Vector2 texOffset = Vector2.zero;
        Vector4 texParams = new Vector4(texScale.x,texScale.y,texOffset.x,texOffset.y);

        //1x1范围内随机分布
        Vector3 offset = vertexPosition + new Vector3(Random.Range(0,1f),0,Random.Range(0,1f));
        //0到180度随机旋转
        float rot = Random.Range(0,180);
        //构造变换矩阵
        var localToTerrian = Matrix4x4.TRS(offset,Quaternion.Euler(0,rot,0),Vector3.one);
        var grassInfo = new GrassInfo(){
            localToTerrian = localToTerrian,
            texParams = texParams
        };
        grassInfos.Add(grassInfo);
        grassIndex ++;
        if(grassIndex >= maxGrassCount){
            break;
        }
    }
    if(grassIndex >= maxGrassCount){
        break;
    }
}
_grassCount = grassIndex;
_grassBuffer = new ComputeBuffer(_grassCount,64 + 16);
_grassBuffer.SetData(grassInfos);
```

其中`new ComputeBuffer`第二参数`64 + 16`，代表了`GrassInfo`这个结构的字节数,因为一个Matrix4x4是64个字节，一个Vector4是16个字节，因此总共`64+16`

### 3.2 材质球参数

这里我们通过MaterialPropertyBlock来给材质球设置参数:

```csharp
materialPropertyBlock.SetMatrix(ShaderProperties.TerrianLocalToWorld,transform.localToWorldMatrix);
materialPropertyBlock.SetBuffer(ShaderProperties.GrassInfos,grassBuffer);
materialPropertyBlock.SetVector(ShaderProperties.GrassQuadSize,_grassQuadSize);
```

### 3.3 渲染

最终渲染代码调用:
```csharo
cmd.DrawMeshInstancedProcedural(GrassUtil.unitMesh,0,grassTerrian.material,0,grassTerrian.grassCount,grassTerrian.materialPropertyBlock);

```

### 3.4 效果图

这里是10x10平米内，一万颗草的效果

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/grassinstances/grass_nolight.jpeg">


## 4. 光照

我们可以给草加上光照和阴影支持，带来更好的视觉效果。 

为了支持阴影，需要在shader里加入以下编译选项:

```hlsl
// Universal Pipeline keywords
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
```

frag实现:

```hlsl
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
```

`TransformWorldToShadowCoord`和`GetMainLight`都是URP中提供的函数，拿到光照阴影数据后，我们使用lambert光照模型来进行亮度计算。
- 因为是双面显示，所以我们对光向和normal的点积取绝对值，否则背面会全黑。
- minDotLN是为了避免光照接近90度导致太暗


效果:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/grassinstances/grass_light.jpeg">

可以看到不同朝向的草，会有不同的亮度。 以及方块在草地上投下的阴影。



## 5. 动画

到目前位置，草丛还是静态的。下面加入随风摆动的效果。

## 5.1 草的弯曲

为了实现草随风摆动，首先要考虑实现草的弯曲效果。考虑单个草片面弯曲，本质上是顶点绕着草根部（面片底部），进行旋转。如果是`刚体草`，那么旋转角度不受顶点高度影响。如果是一个有柔韧性的草面片，那就跟高度有关系了。这里先用刚体草进行模拟,因为我们毕竟也只使用了4个顶点的最简单的草面片。 总结一下规则:

假设草的生长方向为`grassUp`,风的方向为`windDir`,那么

- 利用`windDir = windDir - dot(windDir,grassUpWS)`将windDir与grassUp正交化
- 以windDir为x轴，grassUp为y轴，构造一个平面坐标系
- 无风情况下，草呈垂直状态，与y轴夹角为0。
- 随着风力增大，草与y轴夹角增大，往x方向弯曲，最大到90度

shader函数实现:
```hlsl

///计算被风影响后的世界坐标
///positionWS - 施加风力前的世界坐标
///grassUpWS - 草的生长方向，单位向量，世界坐标系
///windDir - 是风的方向，单位向量，世界坐标系
///windStrength - 风力强度,范围0-1
///vertexLocalHeight - 顶点在草面片空间中的高度
float3 applyWind(float3 positionWS,float3 grassUpWS,float3 windDir,float windStrength,float vertexLocalHeight){
    //根据风力，计算草弯曲角度，从0到90度
    float rad = windStrength * PI / 2;
    //得到wind与grassUpWS的正交向量
    windDir = windDir - dot(windDir,grassUpWS);

    float x,y;  //弯曲后,x为单位球在wind方向计量，y为grassUp方向计量
    sincos(rad,x,y);

    //offset表示grassUpWS这个位置的顶点，在风力作用下，会偏移到windedPos位置
    float3 windedPos = x * windDir + y * grassUpWS;

    //加上世界偏移
    return positionWS + (windedPos - grassUpWS) * vertexLocalHeight;
}
```

然后定义材质属性:

```
_Wind("Wind(x,y,z,str)",Vector) = (1,0,0,10)

```

xyz表示风向，w表示风强

草丛倾倒效果:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/grassinstances/grass_wind.jpeg">


## 5.2 加入摇摆动画

目前草丛已经可以往风的方向，根据风力强度倾倒，但还是缺少摇摆动画。 我们可以引用_Time.y，并利用正弦函数实现周期摇摆。同时为了让摇摆效果更随机，还要引入噪音贴图。

这里我们使用一张无缝衔接的Perlin噪音贴图:

<img src="./Textures/PerlinNoise.jpg" width=256>


扰动值计算:

```hlsl

float2 noiseUV = (positionWS.xz - time) / 30;
float noiseValue = tex2Dlod(_NoiseMap,float4(noiseUV,0,0)).r;

//通过sin函数进行周期摆动,乘以windStrength来控制摆动频率。通常风力越强，摆动频率越高
noiseValue = sin(noiseValue * windStrength);

//将扰动再加到风力上,_WindNoiseStrength为扰动幅度，通过材质球配置
windStrength += noiseValue * _WindNoiseStrength;

```

效果:

略


## 6. 精细化种植技术

先前是以平面模型顶点为基础，在1x1范围进行种植的，这个种植分布很粗糙，只能在水平、均匀分布的顶点模型上种植。现在期望在任意模型表面，实现平均密度种植。

首先考虑一个三角面，要实现任意一个三角面内平均密度种植技术，我们需要实现以下两个功能:

- 计算三角面面积
- 三角形内部平均分布随机点生成函数


### 6.1 三角形面积

给定三个顶点，我们可以利用以下公式来计算三角形面积

```
S = 1／2absinθ

其中a,b为两边长度，θ为两边夹角
```

代码实现:

```csharp
//计算三角形面积
public static float GetAreaOfTriangle(Vector3 p1,Vector3 p2,Vector3 p3){
    var vx = p2 - p1; 
    var vy = p3 - p1;
    var dotvxy = Vector3.Dot(vx,vy);
    var sqrArea = vx.sqrMagnitude * vy.sqrMagnitude -  dotvxy * dotvxy;
    return 0.5f * Mathf.Sqrt(sqrArea);
}
```

### 6.2 三角形内部平均分布随机点生成函数

考虑在1x1的方块内生成随机点，我们可以使用以下函数:

```csharp
var x = Random.Range(0,1f);
var y = Random.Range(0,1f);
```

推广到任意平行四边形:

```csharp

//vx,vy为平行四边形的两边向量

var x = Random.Range(0,1f) * vx;
var y = Random.Range(0,1f) * vy;


```

任意三角形，都可以视为相应的平行四边形按照对角线分割后的半边。因此我们可以使用以下函数来随机取点:

```csharp
/// <summary>
/// 三角形内部，取平均分布的随机点
/// </summary>
public static Vector3 RandomPointInsideTriangle(Vector3 p1,Vector3 p2,Vector3 p3){
    var x = Random.Range(0,1f);
    var y = Random.Range(0,1f);
    if(y > 1 - x){
        //如果随机到了右上区域，那么反转到左下
        var temp = y;
        y = 1 - x;
        x = 1 - temp;
    }
    var vx = p2 - p1;
    var vy = p3 - p1;
    return p1 + x * vx + y * vy;
}
```


### 6.3 种植实现

逻辑如下:

 - 要遍历一个Mesh的三角面
 - 计算三角面积，乘以密度，计算出需要种植的数量n
 - 在三角内部生成n个随机点，作为种植位
 - 用两边叉乘，得到面法线，作为草的生长方向

```csharp
var indices = terrianMesh.triangles;
var vertices = terrianMesh.vertices;

for(var j = 0; j < indices.Length / 3; j ++){
    var index1 = indices[j * 3];
    var index2 = indices[j * 3 + 1];
    var index3 = indices[j * 3 + 2];
    var v1 = vertices[index1];
    var v2 = vertices[index2];
    var v3 = vertices[index3];

    //面得到法向
    var normal = GrassUtil.GetFaceNormal(v1,v2,v3);

    //计算up到faceNormal的旋转四元数
    var upToNormal = Quaternion.FromToRotation(Vector3.up,normal);

    //三角面积
    var arena = GrassUtil.GetAreaOfTriangle(v1,v2,v3);

    //计算在该三角面中，需要种植的数量
    var countPerTriangle = Mathf.Max(1,_grassCountPerMeter * arena);

    for(var i = 0; i < countPerTriangle; i ++){

        //计算localToTerrian矩阵，并填充ComputeBuffer
        //.....
    }
}
```

如此这般，我们就可以在任意表面任意方向种草了

Cube和Sphere上种草效果:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/grassinstances/grass_cube.jpeg">


## 7. 风浪

为了追求更生动的草丛动画，我们还可以加入阶段性的麦浪效果。麦浪形成的本质原因，是风速在大空间范围的不均匀的分布。风力强的区域，草下弯的更厉害。当这个区域随着风往前进时，就形成了浪的效果。

在实现上，我们可以把一股浪分为四个部分:

- 头部 - 风力逐渐增强区域。草在该范围内所受风力逐渐增大。
- 中部 - 风力最强区域，草在该区域持续维持着被强风压倒的状态
- 尾部 - 风力逐渐减弱区域，草在这个区域中缓慢恢复到普通状态
- 静默 - 无强风区域，草持续维持普通状态


定义材质属性:

```
_StormParams("Storm(Begin,Keep,End,Slient)",Vector) = (1,100,40,100)
_StormStrength("BigWindStr",Range(0,40)) = 20

```

`_StormParams`的四个分量分别代表了强风四个阶段维持的距离。 `_StormStrength`代表了强风的强度。

我们用一个函数来计算强风的影响:

```hlsl
//windStrength为常态风力
//返回施加风浪影响后的最终风力
float applyStorm(float3 positionWS,float3 windDir,float windStrength){

}
```

首先计算positionWS在风向上的投影坐标

```hlsl
float disInWindDir = dot(positionWS,windDir);

```

为了让Storm沿着风向移动起来:

```hlsl
float disInWindDir = dot(positionWS - windDir * _Time.y,windDir);
```

让移动速度与风强挂钩:

```hlsl
float disInWindDir = dot(positionWS - windDir * _Time.y * (windStrength + _StormStrength),windDir);
```

这样我们就得到了在风向上，随时间和距离变化的一个值。

然后计算风浪周期:

```hlsl
float stormInterval = StormFront + StormMiddle + StormEnd + StormSlient;
```

计算disInWindDir在周期中的偏移:

```hlsl
//范围为0 ~ stormInterval
float offsetInInterval = stormInterval - (disInWindDir % stormInterval) - step(disInWindDir,0) * stormInterval;
```

根据offsetInInterval来计算当前处于风浪的那个部分，并得到加强系数:

```hlsl
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
    x = (StormFront + StormMiddle + StormEnd - offsetInInterval) * rcp(StormEnd);
}

//基础风力 + 强风力
return windStrength + _StormStrength * x;     
```

最终效果:


<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/grassinstances/grass_anim_1.gif">



## 8. 更多

目前种植这块代码(也即ComputeBuffer生成)，是在CPU上做的。 实际上对于产品级的草地渲染，还需要加入LOD和视锥裁剪，因此ComputeBuffer的数据应当根据摄像机的移动变换来动态计算。 这个过程让CPU做还是有点吃不消，更适合通过ComputeShader转到GPU上来计算.
