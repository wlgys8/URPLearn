# URPLearn

此项目用于Unity通用渲染管线学习。

环境:

- Unity版本 2019.4.18f
- URP版本 7.5.2。


# URP源码解读

- [基础概念](https://github.com/wlgys8/URPLearn/wiki/URP-Basic-Concept)
- [SRP](https://github.com/wlgys8/URPLearn/wiki/SRP-Custom)
    - [SRP-Culling](https://github.com/wlgys8/URPLearn/wiki/SRP-Culling)
- [URP](https://github.com/wlgys8/URPLearn/wiki/URP-Source)
    - [URP-ForwardRender](https://github.com/wlgys8/URPLearn/wiki/URP-ForwardRender)

# URP后处理造轮子

1. [扩展RendererFeatures](https://github.com/wlgys8/URPLearn/wiki/Custom-Renderer-Features)

2. [自定义PostProcessing](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/CustomPostProcessing)

    2.1 [简单的ColorTint](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/ColorTint)

    2.2 [Blur - 模糊效果](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/Blur)

    2.3 [Bloom - 泛光特效](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/Bloom)

    2.4 [SSAO - 屏幕空间环境光遮蔽](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/SSAO)

    2.5 [DepthOfField - 景深](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/DepthOfField)

3. [ScreenSpacePlanarReflectionFeature(屏幕空间平面反射)](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/ScreenSpacePlanarReflection)

# URP中的GUP Instance功能

1. [用GPU Instance绘制草植](https://github.com/wlgys8/URPLearn/tree/master/Assets/URPLearn/GrassGPUInstances)

# 扩展阅读

1. [HDR相关原理](https://github.com/wlgys8/URPLearn/wiki/HDR)
    - ToneMapping
    - 浮点纹理
2. [什么是Gamma矫正、线性色彩空间和sRGB](https://zhuanlan.zhihu.com/p/66558476)

    以上文章大致总结以下就是:
    - 早期显示器输出亮度与电压不是线性关系，而是`l = u^2.2`幂次关系。因此线性的色彩空间，经由显示器输出后，会变暗。
    - sRGB编码就是通过反向的曲线，先将线性色彩变亮,即`sRGB = linearRGB^0.45`。再经由显示器，就能完美还原了。

3. [什么是Color-LUT](https://zhuanlan.zhihu.com/p/43241990)

4. [ConstantBuffer](https://github.com/wlgys8/URPLearn/wiki/CBuffer)




