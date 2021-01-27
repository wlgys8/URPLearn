
# ColorTint

在[PostProcessingFeature](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/CustomPostProcessing)基础上，实现简单的ColorTint。


首先，创建一个类`ColorTint`继承自`PostProcessingEffect`

```csharp
[CreateAssetMenu(menuName = "URPLearn/ColorTint")]
public class ColorTint : PostProcessingEffect{
    
}
```

然后实现其`Render`方法，利用`context.BlitAndSwap`来进行后处理绘制

```csharp

public override void Render(CommandBuffer cmd, ref RenderingData renderingData, PostProcessingRenderContext context)
{
    //....
    //....
    context.BlitAndSwap(cmd,_material);
}

```

写完之后，右键`Create/URPLearn/ColorTint`，即可创建一个Effect-ColorTint资源，我们将其添加到`PostProcessingFeature`的Effect列表中即可

