# 实时交互水面系统

[English](README.md) | [简体中文](README.zh-CN.md)

这是一个使用 Unity URP 制作的技术美术案例，包含实时交互涟漪、泡沫注入、折射、色散、焦散和风格化泳池氛围表现。

## 效果预览

<!-- 在 GitHub 的 Markdown 编辑页面上传 MP4，并将生成的视频附件链接粘贴到这里。 -->

## 项目简介

本项目使用 RenderTexture、Shader Graph 和 C# 搭建了一套实时交互水面系统。

鼠标点击或场景中的交互物体可以向水面注入扰动，生成扩散涟漪和局部泡沫。模拟得到的高度数据会进一步转换为交互法线与遮罩，并与基础水面材质、折射、色散、焦散和后处理结合，形成梦核风格的泳池场景。

## 核心亮点

- 使用轮换 RenderTexture 实现实时波形模拟
- 支持鼠标点击和物体交互注入涟漪与泡沫
- 通过四邻域高度采样将高度图转换为法线
- 使用深度差混合浅水色与深水色
- 双层流动法线与程序化顶点波浪
- 由交互法线驱动的折射与色散
- 独立泡沫模拟，并影响颜色、法线和 Smoothness
- 焦散叠加与 URP 后处理整合

## 技术拆解

### RenderTexture 波形模拟

波形模拟使用三张 RenderTexture：

- `previousHeightRT`：上一帧高度数据
- `currentHeightRT`：当前高度数据
- `tempRT`：下一帧写入目标

每次更新会读取当前帧和上一帧，将下一步结果写入 `tempRT`，随后交换三个 C# 引用。贴图数据仍保留在 GPU 中，系统只交换它们所承担的角色。

<p align="center">
  <img src="Docs/WaterSimulationPipeline.png" alt="RenderTexture 波形模拟流程" width="100%">
</p>

<p align="center"><em>上一帧状态 + 当前状态 → 波形更新 → 引用交换</em></p>

### 交互流程

交互系统的运行流程如下：

```text
鼠标点击 / WaterInteractor
        ↓
WaterSurface.cs
        ├─ InjectRipple()
        ├─ InjectFoam()
        └─ UpdateWaveSimulation()
        ↓
交互高度贴图 + 交互泡沫贴图
        ↓
水面 Shader Graph
```

高度贴图保存实时涟漪状态；泡沫使用独立贴图更新，因此可以拥有单独的注入、衰减和材质控制。

### 交互涟漪法线

系统会采样交互高度贴图当前 UV 四周的高度值，并通过左右、上下高度差计算梯度，再将梯度转换为涟漪法线。

同时，系统根据高度绝对值生成 `Ripple Mask`，用于控制交互区域对最终水面材质的影响范围。

### 交互涟漪重建

波形模拟的结果以动态高度场的形式保存在 RenderTexture 中。
水面 Shader 采样当前像素四周的高度，并根据高度差计算表面坡度，
从而重建交互涟漪的法线方向。

> Height Field → 四邻域高度采样 → 高度梯度 → Ripple Normal  
> Height Field → 绝对值与强度放大 → Ripple Mask  
> Ripple Normal + Ripple Mask → 水面法线、泡沫与折射响应

```hlsl
float hL = SampleHeight(uv + float2(-texelSize.x, 0));
float hR = SampleHeight(uv + float2( texelSize.x, 0));
float hD = SampleHeight(uv + float2(0, -texelSize.y));
float hU = SampleHeight(uv + float2(0,  texelSize.y));

float2 gradient = float2(hL - hR, hD - hU);
float3 rippleNormal = normalize(float3(gradient, 1.0));

float height = SampleHeight(uv);
float rippleMask = saturate(abs(height) * RippleMaskStrength);
```

### 基础水面材质

基础水面材质由以下部分组成：

- 两组不同 Tiling 和 Speed 的流动法线
- 基于 Fresnel 的边缘响应
- 基于深度差的浅水／深水颜色混合
- 两组不同方向的正弦波顶点位移
- 可调节的 Smoothness 与透明度

双层法线和双方向顶点波浪可以减少单一纹理与单一方向带来的规律感。

### 交互泡沫

泡沫使用独立 RenderTexture 进行模拟。采样值经过 `Smoothstep` 和 `Power` 重映射，用于控制泡沫范围、边缘和集中程度。

最终泡沫强度用于：

- 在水面颜色上叠加泡沫颜色
- 混合水面与泡沫的 Smoothness
- 将泡沫法线细节与水面法线混合

### 折射与色散

折射使用水面法线偏移 Screen UV，再采样 Scene Color，从而形成水下画面的扭曲。

色散在此基础上使用三套不同的 UV 分别采样 Scene Color，再重新组合 RGB：

- 红色通道：正方向偏移
- 绿色通道：原始 Screen UV
- 蓝色通道：反方向偏移

偏移方向由交互法线决定，因此色彩分离会随涟漪方向产生变化。

<p align="center">
  <img src="Docs/WaterOpticalEffects.png"
       alt="焦散、折射与色散效果对比"
       width="100%">
</p>

<p align="center">
  <em>焦散 &nbsp;|&nbsp; 基于法线的折射 &nbsp;|&nbsp; RGB 色散</em>
</p>


### 辅助视觉表现

最终场景还包含：

- 池底动态焦散
- Bloom
- Color Grading
- Film Grain
- Vignette

## 个人完成内容

- RenderTexture 涟漪模拟与缓冲轮换
- 涟漪和泡沫注入逻辑
- 水面 Shader Graph 制作
- 高度场法线重建
- 深度颜色、Fresnel 与顶点波浪
- 折射与色散效果
- 交互泡沫材质表现
- 焦散与后处理
- 场景整合与视觉呈现

## 项目信息

- 引擎：Unity 2022.3.62f1c1
- 渲染管线：Universal Render Pipeline
- Shader：Shader Graph
- 运行逻辑：C#
- 核心技术：RenderTexture、Graphics.Blit、高度场模拟、Scene Color 采样

## 运行方式

1. Clone 或下载本仓库。
2. 使用 Unity 2022.3.62f1c1 打开项目。
3. 打开主展示场景。
Main Scene: Assets/Scenes/pool.
4. 进入 Play Mode。
5. 点击水面或使用场景中的交互物体生成涟漪。

## 第三方资源

本项目使用的 3D 模型均来源于第三方素材网站，相关版权归原作者所有。
