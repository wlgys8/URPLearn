# Screen Space Planar Reflection （屏幕空间平面反射)

由于SSR（屏幕空间反射）需要使用RayMarching进行光线求交，计算量比较大。 
而镜片反射需要用摄像机再次渲染场景，导致DrawCall翻倍。
因而在SSR与PR(镜面反射)之间存在一种折中的选择，即(SSPR)屏幕空间镜面反射。


# 基本原理

首先要根据当前已经渲染完成的屏幕贴图(MainTex)，来计算出一张屏幕反射贴图(ReflectionTex)

以下过程在ComputeShader中进行:

- 根据像素UV和深度信息，重建像素的世界坐标。
- 根据反射平面参数使用Position和Normal可以定义反射平面)，计算镜像世界坐标。
- 将镜像世界坐标重投影到屏幕空间，得到投影UV(ReflectUV)
- 采集MainTex在UV处的像素，写入到ReflectionTex的ReflectUV处。

这样就得到了一张屏幕反射贴图

然后将反射贴图绘制到平面反射物体上即可。

# 最终效果图



# 实现

SSPR使用单独的RenderFeature来做。将时序放在后处理之前:

```csharp
this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

```

Pass绘制:

```csharp
public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
{
    var cmd = CommandBufferPool.Get(CommandBufferTag);
    try{
        // 生成反射贴图
        _ssprTexGenerator.Render(cmd,ref renderingData);
        cmd.SetRenderTarget(this.colorAttachment,this.depthAttachment);
        // 将反射贴图，绘制到反射平面上
        foreach(var planar in ReflectPlanar.activePlanars){
            var rd = planar.GetComponent<Renderer>();
            cmd.DrawRenderer(rd,_material);
        }
        _ssprTexGenerator.ReleaseTemporary(cmd);
        context.ExecuteCommandBuffer(cmd);
    }finally{
        CommandBufferPool.Release(cmd);
    }
}
```

其中ssprTexGenerator是利用ComputeShader,来绘制反射贴图。 注意绘制完之后，重设CommandBuffer的RenderTarget


## ComputeShader计算反射贴图

这里总共用了3个Pass来计算

第一个Pass是Clear:

```hlsl
[numthreads(8,8,1)]
void Clear (uint3 id : SV_DispatchThreadID)
{
    Result[id.xy] = float4(0,0,0,0);
}
```

第二个Pass为首次计算反射贴图:

```hlsl
[numthreads(8,8,1)]
void DrawReflectionTex1 (uint3 id : SV_DispatchThreadID){
    float3 uvAndDepth = GetMirrorUVDepthFromID(id);
    float2 toPixelCoord = uvAndDepth.xy * _MainTex_TexelSize.zw;
    Result[toPixelCoord] = float4(_MainTex[id.xy].rgb,uvAndDepth.z);
}
```

在这里，我们计算出每个像素镜像后的`uv`与`depth`信息,然后将当前像素的颜色，写入到镜像UV所在的位置. 同时将镜像后的depth,写入到alpha里。(为第三个Pass做准备)


由于多个像素，可能会通过镜像映射到同一个位置。我们必须根据深度比较，取离摄像机最近的一个像素。

第三个Pass主要是为了修正这个问题:

```hlsl

[numthreads(8,8,1)]
void DrawReflectionTex2 (uint3 id : SV_DispatchThreadID){
    float3 uvAndDepth = GetMirrorUVDepthFromID(id);
    float2 toPixelCoord = uvAndDepth.xy * _MainTex_TexelSize.zw;
    float4 originalColor = Result[toPixelCoord];
    if(uvAndDepth.z > originalColor.a){
        Result[toPixelCoord] = float4(_MainTex[id.xy].rgb,1);
    }else{
        Result[toPixelCoord] = float4(originalColor.rgb,1);
    }
}

```
如果镜像像素的深度大于之前的第二个Pass中写入的深度，那么覆盖写入。




# 待解决

- 本来想试试用Stencil Buffer，在后处理阶段通过Blit来绘制反射信息的，但是发现Stencil Buffer似乎没了。
- 开启HDR的情况下，反射像素的深度遮挡信息似乎不太精确。