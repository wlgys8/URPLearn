# DepthOfField (景深效果)

景深效果产生的本质原因，是相机的对焦和散焦机制。而其背后的光学原理，则是透镜成像。

根据凸透镜高斯成像公式:

```
1/f = 1/u + 1/v

f: 焦距 - 由凸透镜本身决定
v: 物距 - 物体到凸透镜的距离
u: 像距 - 物体经过凸透镜后，成像位置与镜片的距离.

```

当物体通过凸透镜形成的象距正好在胶片位置时，那么我们就能得到一个清晰的成像。反之，象距和胶片差距越大，成像越模糊。

更多理论参考: http://hyperphysics.phy-astr.gsu.edu/hbase/geoopt/lenseq.html


# 弥散圆

那么现在的问题是，给了一款设备的焦距(f)和对焦距离后，我们需要一个公式，来计算物体距离与呈现在胶片上模糊程度的精确关系。这就用到弥散圆的概念。

WIKI参考:
https://en.wikipedia.org/wiki/Circle_of_confusion


弥散圆的直径，利用相似三角形的原理，是很容易计算的。
由此，就完成了理论准备。


# FocalLength

在光学成像上，FocalLength是焦距的意思。 
在相机参数上，虽然也叫做焦距，但实际上指的是胶片到镜片的距离，在光学意义上应该为像距。

为避免混淆，这里称作`胶距`

# 相关公式

准备一下，输入参数有:

- focalLength 胶片到镜片的距离 (胶距)
- focusDistance 对焦距离 (物距)
- aperture 光圈参数 (定义为 镜片焦距/镜片直径)


运算符号:

- rcp 为倒数运算


那么有:

- 焦距公式

    ```
    f = rcp(rcp(focalLength) + rcp(focusDistance))
    ```

- 镜片直径:
    ```
    lensDiam = f * rcp(aperture)
    ```

- 根据物距计算像距:

    ```
    输入参数:
        objDis
    输出:
        imageDis = rcp(rcp(f) - rcp(objDis));
    ```

- 根据物距，计算弥散圆直径(CoC):

    ```

    输入参数:
        objDis

    输出:
        imageDis = CalculateImageDistance(objDis);
        CoC = abs(imageDis - focalLength) * lensDiam  / focalLength ;

    ```


# 代码

创建DepthOfField

```csharp
[CreateAssetMenu(menuName = "URPLearn/DepthOfField")]
public class DepthOfField : PostProcessingEffect{
    
}

```


提供4个配置参数

```csharp
[Tooltip("相机对焦的物距，单位m，在公式中记为u")]
[SerializeField]
private float _focusDistance = 1;

[Tooltip("相机的焦距(这里其实应该是成像胶片到镜头的距离),单位毫米，在公式中记为v")]
[SerializeField]
private float _focalLength;

[Tooltip("相机的光圈值F = f / 镜片直径")]
[SerializeField]
private float _aperture = 6.3f;

[Tooltip("Blur迭代次数，对性能有影响")]
[SerializeField]
private int _blurIteratorCount = 1;

```

然后将相关参数传入到材质球后，进行Blit操作。

```csharp
var DOFParams = new Vector4(
    rcpf,
    _focalLength,
    1 / (_focalLength * rcpf * _aperture),
    0
);
_material.SetVector("_DOFParams",DOFParams);

for(var i = 0; i < _blurIteratorCount; i ++){
    //水平blur
    context.BlitAndSwap(cmd,_material,0);
    //垂直blur
    context.BlitAndSwap(cmd,_material,1);
}

```

# 最终效果

<img src="https://raw.githubusercontent.com/wiki/wlgys8/URPLearn/.imgs/dof/depthOfField-final.jpeg" width="800"/>