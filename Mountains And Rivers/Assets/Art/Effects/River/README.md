# 河流地块效果使用说明

这套效果将河流拆成两层：底层颜色图负责水墨河床与岸线，上层程序化水面负责流动和洪水扩张。洪水范围由控制图决定，不做昂贵的真实流体模拟。

## 控制图通道

- R：平常河面范围。
- G：地形高度；白色较高，黑色为低洼河床。
- B：洪水可扩张范围；越亮越早被淹没。
- A：岸边湿润范围。

## 纹理导入设置

`T_River_Straight_Albedo.png`：开启 sRGB 与 Alpha Is Transparency，Wrap Mode 设为 Clamp，Compression 设为 None。

`T_River_Straight_Control.tga`：关闭 sRGB，关闭 Alpha Is Transparency，Wrap Mode 设为 Clamp，Compression 设为 None。

## 场景搭建

1. 在地块顶面放一个与格子等大的 Plane 或 Quad，命名为“河床”。材质使用 `ShanheWeijin/RiverBedParallax`，绑定颜色图和控制图。
2. 复制一层，略微抬高约 `0.01`，命名为“河水”。材质使用 `ShanheWeijin/RiverWaterAndFlood`，绑定同一张控制图。
3. 在父物体上添加 `RiverVisualController`，把“河水”的 Renderer 拖入“河水渲染器”。
4. 直线河道默认流向为 `(0, -1)`。地块旋转 90 度后，将流向改为与河道一致。
5. 在 Inspector 中把“洪水扩张进度”依次调到 `0`、`0.5`、`1`，检查河水是否从原河槽平滑扩向两岸。

## 推荐初始值

- 河床视差强度：`0.012–0.022`。固定正交镜头下宁可偏弱，避免岸线发生明显滑动。
- 水面透明度：`0.35–0.50`。
- 流动速度：`0.04–0.08`。
- 浑浊程度：平时 `0.15–0.35`，洪水 `0.55–0.85`。

如果边界出现白线，先确认两张纹理都使用 Clamp 且未压缩，再检查网格 UV 是否完整覆盖 0–1。
