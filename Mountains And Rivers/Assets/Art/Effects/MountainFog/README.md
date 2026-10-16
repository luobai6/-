# 山峰地块：安静呼吸式游雾

这是山峰地块的首版动态雾效。它使用两条透明网格雾带，不修改三张山峰原图，也不使用粒子或写实体积雾。

## 在测试场景中搭建

1. 打开 `Assets/Scenes/DiKuaiChangShi.unity`，找到山峰实例，在其下创建空物体 `FogRoot`。
2. 新建材质 `M_MountainFog_Back`，Shader 选择 `ShanheWeijin/CalmBreathingFog`。
3. 创建两个 Quad，命名为 `Fog_Back` 和 `Fog_Mid`，让 Quad 的正面朝向固定关卡相机。
4. 将 `Fog_Back` 放在后景山片与中景山片之间，将 `Fog_Mid` 放在中景山片与前景山片之间。它们应横向覆盖山谷，但不要遮住峰顶和主要松树剪影。
5. 两个 Quad 都添加 `CalmBreathingFogLayer`，使用同一个材质即可；脚本通过 `MaterialPropertyBlock` 提供不同参数，不会创建材质实例。
6. 关闭两个 MeshRenderer 的 Cast Shadows、Receive Shadows、Light Probes 和 Reflection Probes。

## 推荐初值

材质：

- Fog Color：青灰米白，例如 `(0.82, 0.86, 0.80)`；
- Opacity：`0.22–0.32`；
- Density：`0.45–0.55`；
- Noise Feather：`0.18–0.28`；
- Edge Feather：`0.16–0.24`；
- Large Noise Speed：`(0.003, 0.0006)`；
- Detail Noise Speed：`(-0.002, 0.0009)`；
- Breath Strength：`0.10–0.16`；
- Breath Cycles Per Second：`0.022–0.04`，对应约 25–45 秒一个完整呼吸周期。

雾层组件：

| 雾层 | Phase | Speed Multiplier | Opacity Multiplier |
|---|---:|---:|---:|
| Fog_Back | 0.18 | 0.78 | 0.72 |
| Fog_Mid | 0.63 | 1.08 | 1.00 |

将雾带横向拉宽，纵向保持较薄；两层的高度、宽度和中心点不要完全一致。

## 透明排序

- Shader 默认位于 Transparent 队列并关闭深度写入。
- 固定镜头下优先用实际前后位置区分山片与雾带。
- 如果透明排序仍跳动，为前后雾层复制两个材质，并分别把 Render Queue 设为 `3000` 和 `3001`；不要让所有透明对象共面。
- 默认 Depth Test 为 `Less Equal`。只有在明确需要雾始终覆盖山片时才改成 `Always`，否则会破坏空间关系。

## 验收观察点

- 静置观察 30–45 秒：雾应有浓淡呼吸和极慢错位，但不应像传送带横穿画面。
- 峰顶、主要松树剪影和山体外轮廓始终可辨。
- 雾主要遮住山片接缝、山腰凹谷和山脚，不覆盖整个地块。
- 移动固定相机或改变 Game 视图比例时，不出现明显共面闪烁或透明排序翻转。
- Scene 视图与 Play Mode 中参数表现接近，编辑模式调整组件参数时不会生成额外材质实例。

首版通过后，再决定是否增加极少量局部游雾粒子；当前不加入，以免雾变成棉花团或烟气。
