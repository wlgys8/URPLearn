
# Blur

各种模糊算法

- BoxBlur
- Gaussian Blur


# BoxBlur


均值模糊。 即取指定大小(size * size)范围内的像素，相加后取平均值。

[WIKI参考](https://en.wikipedia.org/wiki/Box_blur)


## 优化: 线性分解

在单个Pass实现时，对于n*n的BoxBlur,计算每个像素，需要对其周围的像素累计采样n^2次。

但是由于BoxBlur是线性可分的([参考SeparableFilter](https://en.wikipedia.org/wiki/Separable_filter)),所以可以拆分为两个Pass来实现:
- 第一个Pass在水平方向进行模糊
- 第二个Pass在垂直方向进行模糊

这样对每个像素，累计只需要采样 2 * n 次

## 优化: Bilinear采样

当我们使用Shader实现模糊算法时，可以利用GPU Bilinear采样的特性。 因为Bilinear采样本身就使用了线性插值，可以一次取到两个像素，又几乎没有开销。因此可以大大降低需要的采样次数。

使用Bilinear + 线性分解后，每个像素，累计只需要采样 n 次

注意: 

`仅在BlurScale为1时， Bilinear模式才正常模式等效。`

## 优化: 降采样

可以将原贴图缩小到1/2、1/4后(DownSample)，再进行Blit，然后还原到原大小(UpSample)。

这样，在Blur阶段，需要计算的像素量将大大减小。


# Gaussian Blur (高斯模糊)

[WIKI](https://en.wikipedia.org/wiki/Gaussian_blur)

高斯模糊在优化上，同样可以使用线性分解和Bilinear采样.



