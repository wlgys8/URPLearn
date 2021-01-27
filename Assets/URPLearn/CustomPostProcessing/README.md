# CustomPostProcessing

在上一章[如何在URP中自定义RenderFeatures](https://github.com/wlgys8/URPLearn/wiki/Custom-Renderer-Features)的基础上，我们可以扩展实现自己的图像后处理。


CustomPostProcessing试图提供一个框架，解决以下问题: 

- 能够方便的扩展编写各种自定义的后处理
- 尽量减少Temporary RenderTexture的生成
- 尽量减少多余的Blit操作


# PostProcessingEffect

CustomPostProcessing提供一个虚类 `PostProcessingEffect`，继承自`ScriptableObject`。

用户如果要实现自定义的后处理，那么只需要继承`PostProcessingEffect`,并实现其中的`Renderer`方法

```csharp
public abstract void Render(CommandBuffer cmd, ref RenderingData renderingData,PostProcessingRenderContext context);

```

# PostProcessingRenderContext

`PostProcessingEffect`的`Render`方法提供了`PostProcessingRenderContext`上下文参数. 使用该对象，可以在内部维护的临时RenderTexture和Camera SourceTexture之间执行反复的ping-pongblit操作。以此将所有的后处理串连起来。

```csharp
context.BlitAndSwap(cmd,material);
```

# PostProcessingFeature

PostProcessingFeature里维护了 `PostProcessingEffect`列表。并在Pass中做了如下的Render操作:

```csharp

void Render(CommandBuffer cmd, ref RenderingData renderingData ,ScriptableRenderContext context)
{       

    var cameraDes = renderingData.cameraData.cameraTargetDescriptor;
    var colorAttachment = this.colorAttachment;
    try{
        _postContext.Prepare(ref renderingData,colorAttachment);
        foreach(var e in _effects){
            if(e && e.active){
                e.Render(cmd,ref renderingData,_postContext);
            }
        }
        _postContext.BlitBackToSource(cmd);
        context.ExecuteCommandBuffer(cmd);
    }finally{
        _postContext.Release(cmd);
    }
}  

```

# Next

有了这个简单框架后，我们可以写一个简单的后处理试试看: 

[调色后处理ColorTint](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/ColorTint)