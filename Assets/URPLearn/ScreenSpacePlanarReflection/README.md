# Screen Space Planar Reflection （屏幕空间平面反射)

# 1. 前言
游戏中要实现实时反射技术通常有以下两种方式:

- 对于平面反射，可以使用一个摄像机从镜像角度渲染场景到RenderTexture上，然后再将结果合并到反射平面上
- 使用屏幕空间反射(SSR)

前者无需多言，后者可以参考文章: [screen-space-reflection](https://lettier.github.io/3d-game-shaders-for-beginners/screen-space-reflection.html) (有空也准备实现一个版本)

两者各有优劣如下

使用镜像摄像机渲染:

- 优点是反射效果与真实一致
- 缺点是再次渲染场景，导致DrawCall翻倍。如果有多个镜面，那不可想像。

SSR:

- 好处是屏幕空间计算，开销恒定，可以实现场景任意表面反射。
- 缺点1是需要使用RayMarching，开销大。
- 缺点2是反射质量没有前者好，且只能反射屏幕中的像素。对硬件要求高。


而屏幕空间平面反射是一种介于两者之间的技术

- 首先它在屏幕空间计算，无需额外渲染场景
- 无需RayMarching,性能友好
- 它只适用于平面反射
- 它同样只能反射屏幕之中的像素

实际项目中，通常是结合多种反射技术共同实现。

# 2. 基本原理

- 在屏幕空间，根据深度图和UV信息，可以重建每个像素的世界坐标PositionWS。
- 这时候给定一个平面，就可以计算出PositionWS对应的镜像世界坐标PositionMWS
- 将PositionMWS重新投影到屏幕，得到UV1
- 这样我们就可以知道，UV1处将反射UV处的像素


# 3. URP实现

URP中按照以下步骤来进行实现:

- 收集场景中的可反射平面Renderer
- 对上一步中收集的renderers进行分组，分布在同一平面空间的renderer归入到一组。
- 创建SSPRRenderFeature，用来构建渲染指令。
- 在后处理阶段，针对每个平面空间，执行以下操作:
  - 根据提供的平面数据，使用ComputeShader在屏幕空间计算反射贴图
  - 渲染该平面空间中的renderer列表，将反射贴图与之进行混合
  
本篇的侧重点在于反射贴图的生成，而在混合阶段只采取了简单的Blend。在项目中，应当根据实际光照模型和美术效果需求来做更细致的实现。



## 3.1 使用ComputeShader生成反射贴图

要实现SSPR，核心部分在于反射贴图的生成，这需要使用到ComputeShader功能。

ComputeShader计算的输入输出关系如下:

```
输入:
    - 平面参数
    - 摄像机参数
    - Camera ColorTexture //场景渲染后生成的颜色纹理
    - Camera DepthTexture //场景渲染后生成的深度纹理

输出:

    - 反射贴图
```
    
下面准备输入参数.

### 3.1.1 平面参数

数学上，平面可以用一个点(position)和一根法线向量(normal)来描述。 因此在c#中定义如下结构即可以描述一个平面:

```csharp
  public struct PlanarDescriptor{
      public Vector3 position;
      public Vector3 normal;
  }
```

在ComputeShader里，我们同样定义两个变量:

```hlsl
float4 _PlanarPosition;
float4 _PlanarNormal;
```

### 3.1.2 摄像机参数

反射贴图是被反射的像素最终投影到摄像机屏幕空间生成的，因此摄像机参数也是必须，此处我们准备好摄像机的ViewProject矩阵和它的逆矩阵。

在URP中实现如下:

```csharp
var cameraData = renderingData.cameraData;
var viewMatrix = cameraData.camera.worldToCameraMatrix;
//不知道为什么，第二个参数是false才能正常得到世界坐标
var projectMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(),false);
var matrixVP = projectMatrix * viewMatrix; //vp矩阵
var invMatrixVP = matrixVP.inverse; //vp逆矩阵

```

其中`GL.GetGPUProjectionMatrix`这个接口的第二个参数，似乎是跟平台的y轴反转有关系

在ComputeShader中同样定义如下变量:

```hlsl
float4x4 _MatrixInvVP;
float4x4 _MatrixVP;
```

### 3.1.3 Camera ColorTexture & DepthTexture

在URP管线中，摄像机在渲染阶段默认是往名为`_CameraColorTexture`的RenderTexture上渲染，深度信息则保存在`_CameraDepthTexture`中。我们在后处理阶段之前，可以在Shader中以全局的方式访问`_CameraColorTexture`和`_CameraDepthTexture`两张贴图。因此我们只需要在ComputeShader中定义:

```hlsl

Texture2D<float4> _CameraColorTexture;
Texture2D<float> _CameraDepthTexture;

```

### 3.1.4 输出反射贴图定义

在ComputeShader中首先定义一个可随机写入的纹理，来作为最终输出目标:

```hlsl
RWTexture2D<float4> _Result;
```

在c#中我们需要创建临时RenderTexture来为其做绑定:

```csharp

//定义_ReflectionTex
private int _reflectionTexID = Shader.PropertyToID("_ReflectionTex");

public void Render(CommandBuffer cmd, ref RenderingData renderingData,ref PlanarDescriptor planarDescriptor){
    var reflectionTexDes = renderingData.cameraData.cameraTargetDescriptor;
    reflectionTexDes.enableRandomWrite = true; //开启随机像素写入
    cmd.GetTemporaryRT(_reflectionTexID,reflectionTexDes); //申请临时RT

    //......

    //将临时纹理绑定到ComputeShader的_Result变量上
    cmd.SetComputeTextureParam(_computeShader,kernal,"_Result",_reflectionTexID);
}

//完成渲染后，要正确释放申请的临时RT
public void ReleaseTemporary(CommandBuffer cmd){
    cmd.ReleaseTemporaryRT(_reflectionTexID);
}

```

### 3.1.5 编写ComputeShader

这里我们使用三个kernal来实现，定义如下:

```hlsl
#pragma kernel Clear
#pragma kernel DrawReflectionTex1
#pragma kernel DrawReflectionTex2
```

其中

- Clear 用作清除反射贴图
- DrawReflectionTex1 用作首次渲染反射贴图
- DrawReflectionTex2 为二次渲染反射贴图，主要用来修复反射像素的遮挡问题

下面依次说明这三个 kernel pass.


### a. 清理反射贴图

这个pass很简单，像素全部设置为0就可以了

```hlsl
[numthreads(8,8,1)]
void Clear (uint3 id : SV_DispatchThreadID)
{
    _Result[id.xy] = float4(0,0,0,0);
}
```

### b.首次渲染反射贴图

根据前面的理论，反射贴图渲染分以下几个步骤:

- 在屏幕空间，根据深度图和UV信息，可以重建每个像素的世界坐标PositionWS。
- 这时候给定一个平面(position&normal)，就可以计算出PositionWS对应的镜像世界坐标PositionMWS
- 将PositionMWS重新投影到屏幕，得到UV1
- 这样我们就可以知道，UV1处将反射UV处的像素


### b.1 重建像素世界坐标

首先利用uv和depth，我们可以构建像素在ClipSpace的坐标:

```hlsl
float3 positionCS = float3(uv * 2 -1,depth);
```

然后利用ViewProject逆矩阵，可以将坐标从ClipSpace转到WorldSpace:

```hlsl
float4 positionWS = mul(_MatrixInvVP,float4(positionCS,1));
positionWS /= positionWS.w;
```

### b.2 计算镜像坐标

坐标P和法线N定义了一个反射平面，我们现在期望求坐标W在这个反射平面下的镜像坐标M

- 首先可以计算向量PW在法线上的投影向量`PW'`
- 然后令`W - 2 * PW'`，即可以得到镜像坐标M

代码实现:

```hlsl
float4 GetMirrorPositionWS(float3 positionWS){
    float normalProj = dot(positionWS - _PlanarPosition,_PlanarNormal);
    return float4(positionWS - normalProj * _PlanarNormal * 2,normalProj);
}
```

这里的normalProj可以视为`positionWS`到平面的距离，我们暂且把这个距离写入w分量，用作后续的剔除之用。

### b.3 重投影
 
这是`b.1`步骤的逆操作，代码也很简单:

```hlsl
float3 Reproject(float3 positionWS){
    float4 positionCS = mul(_MatrixVP,float4(positionWS,1));
    positionCS /= positionCS.w;
    positionCS.xy = (positionCS.xy + 1) * 0.5;
    return positionCS.xyz;
}
```

得到xy为uv,z为深度。

但是重投影的时候有一个注意点，即平面应当只反射位于其正面(即法线朝向)的像素，而不反射背面像素。这时候`b.2`中的w分量就可以派上用场了:

```hlsl
if(mirrorPositionWS.w > 0.01){
    float3 uvAndDepth = Reproject(mirrorPositionWS.xyz);
    return uvAndDepth;
}else{
    return float3(0,0,0);
}
```

可以看到，我们只对w分量为正的像素进行重投影。


### b.4 反射贴图生成

得到重投影的结果后，我们就可以采集源UV处的像素，将其写入到mirrorUV处，用代码实现如下:

```hlsl
[numthreads(8,8,1)]
void DrawReflectionTex1 (uint3 id : SV_DispatchThreadID){
    float2 uv = id.xy;
    float3 mirrorUVAndDepth = GetMirrorUVDepthFromID(id);
    float2 mirrorPixelCoord = mirrorUVAndDepth.xy * _MainTex_TexelSize.zw;
    _Result[mirrorPixelCoord] = float4(_CameraColorTexture[uv].rgb,mirrorUVAndDepth.z);
}
```

这样一张初步的反射贴图就生成了，效果如下:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/sspr/ref-tex-1.jpeg" />


对比正常视角:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/sspr/sspr-original.jpeg" />

### b.5 存在的问题

如果仔细观察，会发现`b.4`中生成的反射贴图还是存在问题的。例如地面上的桶没有出现在反射贴图中。 这是因为场景中的像素，经过平面反射后，可能会投影到同一个位置。 将b.4中的反射贴图混合到反射平面上可能有更直观的感觉:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/sspr/ref-tex-1-error.jpeg" />

可以看见，红圈区域都出现了反射异常。 本应该反射桶的地方，却反射了桶后方的墙壁。 本应反射围栏的地方，却反射了围栏后方的天空。这都是因为在写入反射像素的时候，没有做深度测试。 因此我们需要额外的一步pass来修正这个问题。

 ### c. 修正反射遮挡问题

由于在步骤b中，我们已将深度信息写入到了贴图的alpha通道中。因此在这里，我们可以将`b`的过程再走一遍，然后进行和原贴图中记录的像素深度作比较，将不正确的像素覆盖掉。

```hlsl
[numthreads(8,8,1)]
void DrawReflectionTex2 (uint3 id : SV_DispatchThreadID){
    float3 uvAndDepth = GetMirrorUVDepthFromID(id);
    float2 toPixelCoord = uvAndDepth.xy * _MainTex_TexelSize.zw;
    float4 originalColor = _Result[toPixelCoord];
    if(uvAndDepth.z > originalColor.a){
        _Result[toPixelCoord] = float4(_CameraColorTexture[id.xy].rgb,1);
    }else{
        _Result[toPixelCoord] = float4(originalColor.rgb,1);
    }
}
```

修复后的反射贴图:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/sspr/ref-tex-2.jpeg" />

贴到地面上的效果:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/sspr/sspr-2.jpeg" />


### d. 模糊处理

到目前位置生成的反射贴图，还存在一些瑕疵，比如那些密密麻麻的黑点和黑线。这是由于我们的像素计算是离散的，相邻的两个像素A、B，经过平面反射后，对应到反射贴图上可能会横跨多个像素，中间部分便成了黑洞。这里我们使用模糊来修正这个问题，但在实际项目中，应当根据反射的应用情况作调整，例如当反射应用于水体时，经过噪音贴图处理过后这些瑕疵基本就不可见了，那么就不需要作模糊处理了。 这一步不是这篇文章的重点，因此就简单的利用BoxBlur + Blit来进行了一遍模糊处理。 

最终效果:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/sspr/ref-tex-blur.jpeg" />



## 3.2 编写反射平面的混合shader

这个Shader是比较简单的，首先颜色混合模式采用

```hlsl
Blend One One
```

然后在顶点着色器中我们计算好了positionHS和uv:

```hlsl
output.positionHS = TransformObjectToHClip(input.positionOS);
output.uv = input.uv;
```

在frag中，计算像素屏幕空间的uv

```hlsl
float2 screenUV = i.positionHS.xy * (_ScreenParams.zw - 1);
```

并采样深度图在该位置的depth，进行深度测试。通过测试之后，则采样反射贴图并返回颜色。

```hlsl
float depth = SampleDepth(screenUV);
if(i.positionHS.z >= depth){
    float4 color = SAMPLE_TEXTURE2D_X(_ReflectionTex,sampler_LinearClamp,screenUV);
    return color;
}else{
    discard;
    return float4(0,0,0,0);
}
```

这样就ok了。


## 3.3 编写SSPRRenderFeature

扩展URP中RenderFeature的基础知识可以参考: [编写自定义RendererFeatures](https://github.com/wlgys8/URPLearn/wiki/Custom-Renderer-Features)

针对SSPRRenderFeature，我们将Pass的时序放在后处理之前:

```csharp
this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

```
然后在`Execute`中构建渲染指令:

```csharp
public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
{
    var cmd = CommandBufferPool.Get(CommandBufferTag);
    try{
        PlanarDescriptor planarDescriptor;
        Renderer renderer;
        //从Scene中获取反射平面信息和对应的Renderer
        ReflectPlanar.GetFromScene(out planarDescriptor,out renderer);
        cmd.Clear();
        var planarDescriptor = g.descriptor;
        var renderers = g.renderers;
        _ssprTexGenerator.Render(cmd,ref renderingData,ref planarDescriptor);
        cmd.SetRenderTarget(this.colorAttachment,this.depthAttachment);
        cmd.DrawRenderer(renderer,_material);
        _ssprTexGenerator.ReleaseTemporary(cmd);
        context.ExecuteCommandBuffer(cmd);
    }finally{
        CommandBufferPool.Release(cmd);
    }
}
```

其中:

- ssprTexGenerator即是3.1中所说的利用CompueteShader，来生成反射贴图
- cmd.DrawRenderer(rd,_material), 即利用3.2中所编写的shader，来绘制反射平面

最终效果:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/sspr/sspr-blur.jpeg" />



# 4. 多平面反射

到目前为止，我们已经实现了单个平面的反射。要实现多平面反射，最直接的办法就是收集场景中所有可见的反射平面，然后依次执行3中所述的流程。 用代码来表示就是:

```csharp
List<PlanarDescriptor> planars;
List<Renderer> renderers;
ReflectPlanar.GetVisiblesFromScene(out planars,out renderers);
for(var i = 0;i < planars.Length; i ++){
    RenderSSPR(cmd,planars[i],renderers[i]);
    context.ExecuteCommandBuffer(cmd);
}

```

这相当于，每一个反射平面Renderer对象，都需要进行一次反射贴图计算。 实际上，在某些时候，很多Renderer是分布在同一个平面空间上的，例如一块水平的地面，可能四处分布着不同的水洼，这些水洼既然处于同一个平面，那么其实只需要对它们执行一次反射贴图计算就行了。

因此，我们可以将收集到的Renderers进行分组，将属于同一平面空间的Renderer分配到一组。以此来优化反射贴图的计算次数。


## 4.1 判定两个Renderer是否属于同一平面

在前面我们已经定义了一个结构来描述一个平面:

```csharp
public struct PlanarDescriptor{
    public Vector3 position;
    public Vector3 normal;
}
```

其中:
- position取`renderer.transform.position`
- normal取`renderer.transform.up`

那么要判定两个`PlanarDescriptor`结构`p1`和`p2`相等，我们可以采用以下策略:

- p1.normal和p2.normal朝向一致
- p1.position在p2平面上

用代码来实现即:

```csharp

//判定两个向量朝向是否一致，因为是浮点数，所以我们留0.001误差
private static bool IsNormalEqual(Vector3 n1,Vector3 n2){
    return 1 - Vector3.Dot(n1,n2) < 0.001f;
}

//判定一个坐标是否在平面上
private static bool IsPositionInPlanar(Vector3 checkPos,PlanarDescriptor planar){
    return Vector3.Dot(planar.position - checkPos,planar.normal) < 0.01f;
}

//重载实现两个平面的相等判定
public static bool operator == (PlanarDescriptor p1,PlanarDescriptor p2){
    return  IsNormalEqual(p1.normal,p2.normal) && IsPositionInPlanar(p1.position,p2);
}

```

## 4.2 分组渲染代码

经过分组后，一个Group下有一个平面描述结构和多个Renderer结构，定义如下:

```csharp
public class PlanarRendererGroup{
    public PlanarDescriptor descriptor;
    public HashSet<Renderer> renderers = new HashSet<Renderer>();
}
```

然后我们需要针对每个Group，进行一次反射贴图计算，然后混合到到这个Group下所有的Renderer中

```csharp

ReflectPlanar.GetVisiblePlanarGroups(_planarRendererGroups);
foreach(var group in _planarRendererGroups.rendererGroups){
    cmd.Clear();
    var planarDescriptor = group.descriptor;
    var renderers = group.renderers;
    _ssprTexGenerator.Render(cmd,ref renderingData,ref planarDescriptor);
    cmd.SetRenderTarget(this.colorAttachment,this.depthAttachment);
    foreach(var rd in renderers){
        cmd.DrawRenderer(rd,_material);
    }
    _ssprTexGenerator.ReleaseTemporary(cmd);
    context.ExecuteCommandBuffer(cmd);
}
```

额外放置一个反射地面后的效果图:

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/sspr/sspr-final.jpeg" />

# 5. 更多

- ComputeShader两个Pass的反射计算实际上有重复内容，有优化空间。
- 对于高清反射需求，在不能作模糊的情况下，需要探索更有效的方式来修正反射瑕疵。
- 允许的情况下，可以降低反射贴图分辨率来提升性能。
- 在SSPR的边缘部分，可以作渐变淡出效果，或者过渡到Cube反射。
- 有报如下Waning，但运行效果，暂不清楚会有什么影响。

    ```
    CommandBuffer: temporary render texture _CameraColorTexture not found while executing SSPR-Reflection (SetComputeTextureParam)
    ```

- 此文只用作学习之用，并未经过产品使用，也未经过多平台测试。

